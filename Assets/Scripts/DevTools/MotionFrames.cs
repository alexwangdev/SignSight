#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SignSight.Hand;
using SignSight.Learn;
using UnityEngine;

namespace SignSight.DevTools
{
    /// <summary>
    /// Editor Play Mode only (v3.10): the teacher demonstrates each word and the eye camera is sampled every 0.25 s into a
    /// contact sheet (4 columns, 16 frames, t = 0 … 3.75 s). One PNG per word in <paramref name="folder"/>, plus a log with
    /// the largest wrist correction the clamp applied. Uses the canonical template (TemplateStore.Canonical), like the lessons.
    /// </summary>
    public class MotionFrames : MonoBehaviour
    {
        public bool Done { get; private set; }
        public readonly StringBuilder Log = new StringBuilder();
        private const int Cols = 4, Rows = 4, W = 240, H = 252;

        public static MotionFrames Begin(string[] intents, string folder)
        {
            var c = new GameObject("MotionFrames").AddComponent<MotionFrames>();
            c.StartCoroutine(c.Run(intents, folder));
            return c;
        }

        private IEnumerator Run(string[] intents, string folder)
        {
            var lm = FindFirstObjectByType<LessonManager>();
            while (lm.Current == LessonManager.Phase.Loading) yield return null;
            var teacher = FindFirstObjectByType<TeacherAvatarDriver>();
            var eye = GameObject.Find("CenterEyeAnchor").GetComponent<Camera>();
            lm.GoStart();
            yield return new WaitForSecondsRealtime(1f);
            Directory.CreateDirectory(folder);

            foreach (var intent in intents)
            {
                var t = TemplateStore.Canonical(intent);
                if (t == null) { Log.AppendLine($"{intent}: no template"); continue; }
                var sheet = new Texture2D(Cols * W, Rows * H, TextureFormat.RGB24, false);
                sheet.SetPixels32(new Color32[Cols * W * Rows * H]);
                teacher.Demonstrate(t);
                float t0 = Time.time;
                int n = 0;
                while (n < Cols * Rows && Time.time - t0 < 6f)
                {
                    float due = n * 0.25f;
                    if (Time.time - t0 >= due)
                    {
                        yield return Snap(eye, sheet, n);
                        n++;
                    }
                    else yield return null;
                }
                while (teacher.IsDemonstrating && Time.time - t0 < 14f) yield return null;
                File.WriteAllBytes(Path.Combine(folder, intent + ".png"), UnityEngine.ImageConversion.EncodeToPNG(sheet));
                Destroy(sheet);
                Log.AppendLine($"{intent}: frames {n} (0.25 s apart), demo {Time.time - t0:0.0}s, max wrist correction {teacher.WristClampedMaxDeg:0.0} deg | hold: {teacher.HoldReport}");
                yield return new WaitForSecondsRealtime(0.5f);
            }
            File.WriteAllText(Path.Combine(folder, "motion-log.txt"), Log.ToString());
            Done = true;
        }

        private static IEnumerator Snap(Camera eye, Texture2D sheet, int index)
        {
            yield return new WaitForEndOfFrame();
            var rt = RenderTexture.GetTemporary(W * 2, H * 2, 24);
            var prev = eye.targetTexture;
            eye.targetTexture = rt;
            eye.Render();
            eye.targetTexture = prev;
            RenderTexture.active = rt;
            var full = new Texture2D(W * 2, H * 2, TextureFormat.RGB24, false);
            full.ReadPixels(new Rect(0, 0, W * 2, H * 2), 0, 0);
            full.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            // centre crop, 2x zoom: the teacher fills the cell. Columns 15 %..65 % of the view, full height.
            var src = full.GetPixels32();
            var dst = new Color32[W * H];
            int x0 = Mathf.RoundToInt(W * 2 * 0.15f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    dst[y * W + x] = src[(2 * y) * (W * 2) + x0 + x];
            int col = index % Cols, row = Rows - 1 - index / Cols;
            sheet.SetPixels32(col * W, row * H, W, H, dst);
            Destroy(full);
        }
    }
}
#endif
