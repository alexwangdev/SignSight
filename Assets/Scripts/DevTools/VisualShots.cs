#if UNITY_EDITOR
using System.Collections;
using System.IO;
using SignSight.Hand;
using SignSight.Learn;
using UnityEngine;

namespace SignSight.DevTools
{
    /// <summary>
    /// Editor Play Mode only: captures of a word's demonstration at fixed times after the start: "move" (mid move to
    /// start), "mid" (mid movement) and "hold" (the end hold). Two framings per moment (v3.15): the full-room eye view
    /// (900 x 945, prefix-WORD-moment.png) and a close-up on the teacher's torso and hands from a second camera
    /// (prefix-WORD-moment-closeup.png). A strip of the close-ups (move | mid | hold) per word is saved in "strips".
    /// Times are from the demonstration start (move to start is 0.4 s, then the movement, then the hold).
    /// </summary>
    public class VisualShots : MonoBehaviour
    {
        public bool Done { get; private set; }
        public string Current { get; private set; } = "";
        private const int W = 900, H = 945;

        public static VisualShots Begin(string[] words, string folder, bool allMoments, string prefix = "v312b", bool fullRoom = true)
        {
            var c = new GameObject("VisualShots").AddComponent<VisualShots>();
            c.StartCoroutine(c.Run(words, folder, allMoments, prefix, fullRoom));
            return c;
        }

        private IEnumerator Run(string[] words, string folder, bool allMoments, string prefix, bool fullRoom)
        {
            var lm = FindFirstObjectByType<LessonManager>();
            while (lm.Current == LessonManager.Phase.Loading) yield return null;
            var teacher = FindFirstObjectByType<TeacherAvatarDriver>();
            var eye = GameObject.Find("CenterEyeAnchor").GetComponent<Camera>();
            Directory.CreateDirectory(folder);
            string strips = Path.Combine(folder, "strips");
            if (allMoments) Directory.CreateDirectory(strips);
            lm.GoStart();
            yield return new WaitForSecondsRealtime(1f);

            // close-up camera: in front of the teacher's chest, framing torso + both hands (about 0.9 m wide at the teacher)
            var closeGo = new GameObject("VisualShotsCloseCam");
            var close = closeGo.AddComponent<Camera>();
            close.CopyFrom(eye);
            close.stereoTargetEye = StereoTargetEyeMask.None;
            close.targetTexture = null;
            close.enabled = false;
            close.fieldOfView = 40f;
            close.aspect = (float)W / H;
            var tt = teacher.transform;
            closeGo.transform.position = tt.TransformPoint(new Vector3(0f, 1.38f, 1.3f));
            closeGo.transform.LookAt(tt.TransformPoint(new Vector3(0f, 1.38f, 0f)), tt.up);

            // hold-only runs also save review sheets: 6 words per sheet (3 x 2, half size), listed in sheets/index.txt
            string sheets = Path.Combine(folder, "sheets");
            if (!allMoments) Directory.CreateDirectory(sheets);
            Texture2D sheet = null;
            int cell = 0, sheetNo = 0;
            var index = new System.Text.StringBuilder();
            var names = new System.Collections.Generic.List<string>();

            foreach (var word in words)
            {
                var t = TemplateStore.Canonical(word);
                if (t == null) continue;
                Current = word;
                teacher.Rest();
                yield return null;
                teacher.Demonstrate(t);
                float t0 = Time.time;
                // times follow the word's own movement: move to start 0.4 s, then the movement, then the end hold
                float dur = Mathf.Max(0.05f, t.movement.durationSec);
                bool compound = SignMotion.BeatTemplate(t) != null; // first-beat hold is only 0.25 s
                float holdAt = 0.4f + dur + (compound ? 0.15f : 0.3f);
                var shots = allMoments
                    ? new[] { ("move", 0.25f), ("mid", 0.4f + dur * 0.5f), ("hold", holdAt) }
                    : new[] { ("hold", holdAt) };
                var strip = allMoments ? new Texture2D(3 * (W / 2), H / 2, TextureFormat.RGB24, false) : null;
                int k = 0;
                foreach (var (name, when) in shots)
                {
                    while (Time.time - t0 < when) yield return null;
                    yield return new WaitForEndOfFrame();
                    if (fullRoom) Destroy(Save(eye, Path.Combine(folder, $"{prefix}-{word}-{name}.png")));
                    var tex = Save(close, Path.Combine(folder, $"{prefix}-{word}-{name}-closeup.png"));
                    if (strip != null)
                    {
                        var src = tex.GetPixels32();
                        var dst = new Color32[(W / 2) * (H / 2)];
                        for (int y = 0; y < H / 2; y++)
                            for (int x = 0; x < W / 2; x++)
                                dst[y * (W / 2) + x] = src[(2 * y) * W + 2 * x];
                        strip.SetPixels32(k * (W / 2), 0, W / 2, H / 2, dst);
                    }
                    if (!allMoments)
                    {
                        if (sheet == null) { sheet = new Texture2D(3 * (W / 2), 2 * (H / 2), TextureFormat.RGB24, false); sheet.SetPixels32(new Color32[sheet.width * sheet.height]); }
                        var src2 = tex.GetPixels32();
                        var dst2 = new Color32[(W / 2) * (H / 2)];
                        for (int y = 0; y < H / 2; y++)
                            for (int x = 0; x < W / 2; x++)
                                dst2[y * (W / 2) + x] = src2[(2 * y) * W + 2 * x];
                        sheet.SetPixels32((cell % 3) * (W / 2), (1 - cell / 3) * (H / 2), W / 2, H / 2, dst2);
                        names.Add(word);
                        cell++;
                        if (cell == 6) { FlushSheet(ref sheet, sheets, prefix, ref sheetNo, names, index); cell = 0; }
                    }
                    Destroy(tex);
                    k++;
                }
                if (strip != null)
                {
                    File.WriteAllBytes(Path.Combine(strips, $"{prefix}-{word}-strip.png"), UnityEngine.ImageConversion.EncodeToPNG(strip));
                    Destroy(strip);
                }
                while (teacher.IsDemonstrating && Time.time - t0 < 14f) yield return null;
                yield return new WaitForSecondsRealtime(0.3f);
            }
            if (!allMoments)
            {
                if (cell > 0) FlushSheet(ref sheet, sheets, prefix, ref sheetNo, names, index);
                File.WriteAllText(Path.Combine(sheets, "index.txt"), index.ToString());
            }
            Destroy(closeGo);
            Current = "";
            Done = true;
        }

        private static void FlushSheet(ref Texture2D sheet, string dir, string prefix, ref int no, System.Collections.Generic.List<string> names, System.Text.StringBuilder index)
        {
            no++;
            string file = $"{prefix}-sheet-{no:00}.png";
            File.WriteAllBytes(Path.Combine(dir, file), UnityEngine.ImageConversion.EncodeToPNG(sheet));
            index.AppendLine(file + ": " + string.Join(", ", names) + "  (left to right, top row first)");
            names.Clear();
            Destroy(sheet);
            sheet = null;
        }

        private static Texture2D Save(Camera cam, string path)
        {
            var rt = RenderTexture.GetTemporary(W, H, 24);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(path, UnityEngine.ImageConversion.EncodeToPNG(tex));
            return tex;
        }
    }
}
#endif
