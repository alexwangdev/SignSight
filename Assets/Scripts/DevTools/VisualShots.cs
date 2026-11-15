#if UNITY_EDITOR
using System.Collections;
using System.IO;
using SignSight.Hand;
using SignSight.Learn;
using UnityEngine;

namespace SignSight.DevTools
{
    /// <summary>
    /// Editor Play Mode only (v3.12 Task 4): full-size eye-view captures (900 x 945) of a word's demonstration at fixed times
    /// after the start: "move" (mid move to start), "mid" (mid movement) and "hold" (the end hold). Files:
    /// <folder>/v312b-WORD-moment.png. Times are from the demonstration start (move to start is 0.4 s; the hold is at 2.4-3.2 s).
    /// </summary>
    public class VisualShots : MonoBehaviour
    {
        public bool Done { get; private set; }
        private const int W = 900, H = 945;

        public static VisualShots Begin(string[] words, string folder, bool allMoments, string prefix = "v312b")
        {
            var c = new GameObject("VisualShots").AddComponent<VisualShots>();
            c.StartCoroutine(c.Run(words, folder, allMoments, prefix));
            return c;
        }

        private IEnumerator Run(string[] words, string folder, bool allMoments, string prefix)
        {
            var lm = FindFirstObjectByType<LessonManager>();
            while (lm.Current == LessonManager.Phase.Loading) yield return null;
            var teacher = FindFirstObjectByType<TeacherAvatarDriver>();
            var eye = GameObject.Find("CenterEyeAnchor").GetComponent<Camera>();
            Directory.CreateDirectory(folder);
            lm.GoStart();
            yield return new WaitForSecondsRealtime(1f);

            foreach (var word in words)
            {
                var t = TemplateStore.Canonical(word);
                if (t == null) continue;
                teacher.Demonstrate(t);
                float t0 = Time.time;
                // times follow the word's own movement: move to start 0.4 s, then the movement, then the end hold 0.8 s
                float dur = Mathf.Max(0.05f, t.movement.durationSec);
                // (name, time after start)
                var shots = allMoments
                    ? new[] { ("move", 0.25f), ("mid", 0.4f + dur * 0.5f), ("hold", 0.4f + dur + 0.3f) }
                    : new[] { ("hold", 0.4f + dur + 0.3f) };
                foreach (var (name, when) in shots)
                {
                    while (Time.time - t0 < when) yield return null;
                    yield return new WaitForEndOfFrame();
                    Save(eye, Path.Combine(folder, $"{prefix}-{word}-{name}.png"));
                }
                while (teacher.IsDemonstrating && Time.time - t0 < 14f) yield return null;
                yield return new WaitForSecondsRealtime(0.4f);
            }
            Done = true;
        }

        private static void Save(Camera eye, string path)
        {
            var rt = RenderTexture.GetTemporary(W, H, 24);
            var prev = eye.targetTexture;
            eye.targetTexture = rt;
            eye.Render();
            eye.targetTexture = prev;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(path, UnityEngine.ImageConversion.EncodeToPNG(tex));
            Destroy(tex);
        }
    }
}
#endif
