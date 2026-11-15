#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Text;
using SignSight.Explore;
using SignSight.Learn;
using UnityEngine;

namespace SignSight.DevTools
{
    /// <summary>
    /// Editor Play Mode only: timed checks of Explore with the mock camera + YOLO (v3.3 Task B, §B5), pointing with a
    /// test ray at the boxes on the camera panel. Writes Logs/v33-explore-camera-check.txt.
    /// </summary>
    public class ExploreCameraCheck : MonoBehaviour
    {
        public bool Done { get; private set; }
        public readonly StringBuilder Log = new StringBuilder();

        public static ExploreCameraCheck Begin()
        {
            var c = new GameObject("ExploreCameraCheck").AddComponent<ExploreCameraCheck>();
            c.StartCoroutine(c.Run());
            return c;
        }

        private static Ray RayTo(ExploreMode ex, Rect box)
        {
            var v = ex.View;
            float w = v.Width, h = v.Height;
            var local = new Vector3((box.center.x - 0.5f) * w, (0.5f - box.center.y) * h, 0f);
            var world = v.transform.TransformPoint(local);
            var o = Camera.main.transform.position + Camera.main.transform.forward * 0.1f;
            return new Ray(o, (world - o).normalized);
        }

        private IEnumerator Run()
        {
            var lm = FindFirstObjectByType<LessonManager>();
            lm.StartExploreMode();
            var ex = lm.Explore;
            var d = ex.Detector;
            float t0 = Time.time;
            while (d.Runs < 15 && Time.time - t0 < 20f) yield return null;
            yield return new WaitForSeconds(2.2f); // let the fps window fill
            Log.AppendLine($"Mock source: {ex.Mock.Source}; backend {d.Backend}; model loaded {d.IsLoaded}");
            Log.AppendLine($"Detection rate {d.MeasuredFps:0.0} fps (target {d.TargetFps}), last inference {d.LastInferenceMs:0} ms, editor frame rate {1f / Time.smoothDeltaTime:0} fps");
            Log.AppendLine("Detections (>= " + d.ScoreThreshold + "): " + string.Join("; ", d.Detections.Select(x => $"{x.Label} {x.Score:0.00}")));
            Log.AppendLine("Best raw score per class: " + string.Join(", ", d.TopScores.OrderByDescending(kv => kv.Value).Take(6).Select(kv => $"{kv.Key} {kv.Value:0.00}")));

            // 1) point + hold on the apple's box
            var apple = d.Detections.FirstOrDefault(x => x.Label == "apple");
            bool hasApple = apple.Label == "apple";
            Log.AppendLine($"CHECK apple box present (>= 0.4): {(hasApple && apple.Score >= 0.4f ? "PASS" : "FAIL")} ({apple.Score:0.00})");
            if (hasApple)
            {
                ex.DebugRay = RayTo(ex, apple.Box);
                yield return new WaitForSeconds(0.8f);
                ex.DebugRay = null;
                bool ok = ex.SelectedTemplate != null && ex.SelectedTemplate.intent == "APPLE";
                Log.AppendLine($"CHECK hold 0.6 s on the apple box -> APPLE + teacher demo: {(ok && ex.Current == ExploreMode.State.Demonstrating ? "PASS" : "FAIL")} (state {ex.Current}, card {ex.Selected.Word} / {ex.Selected.Meaning})");
                float t1 = Time.time;
                while (ex.Current == ExploreMode.State.Demonstrating && Time.time - t1 < 10f) yield return null;
                Log.AppendLine($"  demo finished after {Time.time - t1:0.0} s");
                ex.Back();
                yield return new WaitForSeconds(0.5f);
            }

            // 2) an empty part of the panel selects nothing
            int before = ex.Selections;
            ex.DebugRay = RayTo(ex, new Rect(0.45f, 0.05f, 0.1f, 0.05f)); // grey area above the photos
            yield return new WaitForSeconds(1.2f);
            ex.DebugRay = null;
            Log.AppendLine($"CHECK pointing at no box selects nothing: {(ex.Selections == before ? "PASS" : "FAIL")}");

            // 3) unmapped label through YOLO (banana scores below 0.4 on this frame: threshold lowered for this check only)
            float thr = d.ScoreThreshold;
            d.ScoreThreshold = 0.3f;
            t0 = Time.time;
            while (!d.Detections.Any(x => x.Label == "banana") && Time.time - t0 < 3f) yield return null;
            var banana = d.Detections.FirstOrDefault(x => x.Label == "banana");
            if (banana.Label == "banana")
            {
                ex.DebugRay = RayTo(ex, banana.Box);
                yield return new WaitForSeconds(0.8f);
                ex.DebugRay = null;
                var teacher = FindFirstObjectByType<TeacherAvatarDriver>();
                bool ok = ex.SelectedTemplate == null && ex.Selected.Meaning == CocoMapping.NotInWords && !teacher.IsDemonstrating;
                Log.AppendLine($"CHECK banana (threshold 0.3 for this check) -> \"{ex.Selected.Word} — {ex.Selected.Meaning}\", teacher idle: {(ok ? "PASS" : "FAIL")} (score {banana.Score:0.00})");
                ex.Back();
            }
            else Log.AppendLine("CHECK banana: not detected even at 0.3");
            d.ScoreThreshold = thr;

            // 4) leaving Explore unloads the model; Learn never loads it
            lm.Next(); // MENU
            yield return null;
            Log.AppendLine($"CHECK MENU -> Start and model unloaded: {(lm.CurrentMode == LessonManager.Mode.Start && !d.IsLoaded ? "PASS" : "FAIL")}");
            lm.StartLearnMode();
            yield return new WaitForSeconds(0.5f);
            Log.AppendLine($"CHECK Learn mode runs with no YOLO loaded: {(!d.IsLoaded ? "PASS" : "FAIL")}");
            lm.Next();
            System.IO.File.WriteAllText("Logs/v33-explore-camera-check.txt", Log.ToString());
            Done = true;
        }
    }
}
#endif
