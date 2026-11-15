#if UNITY_EDITOR
using System.Collections;
using System.Text;
using SignSight.Explore;
using SignSight.Learn;
using UnityEngine;

namespace SignSight.DevTools
{
    /// <summary>
    /// Editor Play Mode only: timed checks of the Explore pointing hold with a test ray (no hands needed).
    /// 1) a ray sweeping across the objects (0.3 s each) selects nothing; 2) holding 0.8 s on the cup selects COFFEE
    /// ("Cup — COFFEE"); 3) pointing at empty space for 3 s shows nothing. Writes Logs/v33-explore-check.txt.
    /// </summary>
    public class ExploreCheck : MonoBehaviour
    {
        public bool Done { get; private set; }
        public readonly StringBuilder Log = new StringBuilder();

        public static ExploreCheck Begin()
        {
            var c = new GameObject("ExploreCheck").AddComponent<ExploreCheck>();
            c.StartCoroutine(c.Run());
            return c;
        }

        private IEnumerator Run()
        {
            var lm = FindFirstObjectByType<LessonManager>();
            lm.StartExploreMode();
            var ex = lm.Explore;
            var cam = Camera.main.transform;
            Vector3 o = cam.position + cam.forward * 0.1f;
            Ray At(string name)
            {
                var t = GameObject.Find("LearnUI/ExploreObjects/" + name).transform;
                return new Ray(o, (t.position + Vector3.up * 0.04f - o).normalized);
            }
            yield return null;

            int before = ex.Selections;
            foreach (var n in new[] { "Apple", "Cup", "Bottle" })
            {
                ex.DebugRay = At(n);
                yield return new WaitForSeconds(0.3f);
            }
            ex.DebugRay = new Ray(o, Vector3.up); // away
            yield return new WaitForSeconds(0.2f);
            Log.AppendLine($"CHECK passing ray selects nothing: {(ex.Selections == before ? "PASS" : "FAIL")} (selections {before} -> {ex.Selections})");

            ex.DebugRay = At("Cup");
            yield return new WaitForSeconds(0.8f);
            ex.DebugRay = null;
            bool cup = ex.SelectedTemplate != null && ex.SelectedTemplate.intent == "COFFEE" && ex.Selected.Meaning == "Cup — COFFEE";
            Log.AppendLine($"CHECK hold 0.6 s on cup -> COFFEE: {(cup ? "PASS" : "FAIL")} (state {ex.Current}, word {ex.Selected.Word}, meaning {ex.Selected.Meaning})");
            float t0 = Time.time;
            while (ex.Current == ExploreMode.State.Demonstrating && Time.time - t0 < 10f) yield return null;
            Log.AppendLine($"  teacher finished COFFEE demo after {Time.time - t0:0.0} s, state {ex.Current}");

            ex.Back();
            before = ex.Selections;
            ex.DebugRay = new Ray(o, Vector3.up);
            yield return new WaitForSeconds(3.0f);
            ex.DebugRay = null;
            Log.AppendLine($"CHECK no hit for 3 s shows nothing: {(ex.Selections == before && ex.Current == ExploreMode.State.Pointing ? "PASS" : "FAIL")}");

            lm.Next(); // MENU
            Log.AppendLine($"CHECK MENU from Explore -> Start: {(lm.CurrentMode == LessonManager.Mode.Start ? "PASS" : "FAIL")}");
            System.IO.File.WriteAllText("Logs/v33-explore-check.txt", Log.ToString());
            Done = true;
        }
    }
}
#endif
