#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Text;
using SignSight.Learn;
using SignSight.Relax;
using UnityEngine;

namespace SignSight.DevTools
{
    /// <summary>
    /// Editor Play Mode only (v3.7): checks the Relax visual polish — one visual per step, ghost pop + sparkle on target
    /// endings only, step dots, breathing line, safety line, teacher smile and neutral on exit — with simulated
    /// measurements (RelaxMode.DebugSample, NOT real hands); then runs the unchanged RelaxCheck Task A + B.
    /// Writes Logs/v37-relax-visual-check.txt.
    /// </summary>
    public class V37Check : MonoBehaviour
    {
        public bool Done { get; private set; }
        public readonly StringBuilder Log = new StringBuilder();
        private int _errors;
        private RelaxMode.Sample _now;

        public static V37Check Begin()
        {
            var c = new GameObject("V37Check").AddComponent<V37Check>();
            c.StartCoroutine(c.Run());
            return c;
        }

        private void OnEnable() => Application.logMessageReceived += OnLog;
        private void OnDisable() => Application.logMessageReceived -= OnLog;

        private void OnLog(string msg, string stack, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && !msg.Contains("/api/exec"))
            {
                _errors++;
                Log.AppendLine($"  ERROR: {msg}");
            }
        }

        private void Check(string name, bool ok, string detail = "") =>
            Log.AppendLine($"CHECK {name}: {(ok ? "PASS" : "FAIL")}{(detail != "" ? " (" + detail + ")" : "")}");

        private static RelaxMode.Sample Hand(float curl, float spread = 0.3f, float[] gaps = null, float wristX = 0f) =>
            new RelaxMode.Sample
            {
                Tracked = true, Curl = new[] { curl * 0.6f, curl, curl, curl, curl }, Spread = spread,
                TipGap = gaps ?? new[] { 1f, 1f, 1f, 1f }, Wrist = new Vector3(wristX, -0.3f, 0.35f),
            };

        private static float Smile(TeacherAvatarDriver t)
        {
            var r = t.Face != null ? t.Face.Face : null;
            if (r == null) return -1f;
            int i = t.Face.IndexOf(FaceDriver.SmileLeft);
            return i >= 0 ? r.GetBlendShapeWeight(i) : -1f;
        }

        private string Visible(Transform ui)
        {
            var v = ui.Find("RelaxVisual");
            if (v == null) return "none";
            return string.Join("+", v.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled).Select(r => r.name));
        }

        private IEnumerator Run()
        {
            var lm = FindFirstObjectByType<LessonManager>();
            while (lm.Current == LessonManager.Phase.Loading) yield return null;
            var relax = lm.Relax;
            var teacher = FindFirstObjectByType<TeacherAvatarDriver>();
            var ui = GameObject.Find("LearnUI").transform;
            var meaning = ui.Find("WordCard").GetComponentsInChildren<TMPro.TMP_Text>(true).First(t => t.name != "Word");
            var instr = ui.Find("Instruction").GetComponent<TMPro.TMP_Text>();
            relax.DebugSample = () => _now;

            lm.GoStart();
            _now = Hand(0.5f);
            lm.StartRelaxMode();
            yield return new WaitForSeconds(1.0f);
            Check("teacher soft smile in Relax, hands at rest", Smile(teacher) > 10f && !teacher.IsDemonstrating, $"smile {Smile(teacher):0}");
            Check("ghost is warm (not the Learn ghost's pale blue)", RelaxMode.GhostWarm == new Color(1f, 0.9f, 0.75f, 0.6f));

            string[] expect = { "Bubbles+Flower", "Bubbles+Sun", "Bubbles+PianoDots", "Bubbles+Leaves", "Bubbles" };
            string[] dots = { "● ○ ○ ○ ○", "● ● ○ ○ ○", "● ● ● ○ ○", "● ● ● ● ○", "● ● ● ● ●" };
            int pops0 = relax.Pops, sp0 = relax.Sparkles;
            // ---- each step reached by its target (simulated measurements) ----
            for (int i = 0; i < 5; i++)
            {
                float t0 = Time.time;
                while ((relax.StepIndex != i || relax.Current != RelaxMode.State.Step) && Time.time - t0 < 4f) yield return null;
                yield return null;
                string vis = Visible(ui);
                string plain = System.Text.RegularExpressions.Regex.Replace(meaning.text, "<.*?>", "");
                Check($"step {i + 1} {relax.CurrentStep.name}: only its visual shows; dots line; breathing + safety lines",
                      vis.Split('+').OrderBy(x => x).SequenceEqual(expect[i].Split('+').OrderBy(x => x)) && plain.Contains(dots[i]) &&
                      instr.text.Contains("Breathe in") && instr.text.Contains(RelaxMode.SafetyLine),
                      $"visible {vis}; dots '{dots[i]}'");
                // do the step
                switch (relax.CurrentStep.kind)
                {
                    case RelaxMode.Kind.OpenClose:
                        for (int c = 0; c < 2; c++) { _now = Hand(0.1f); yield return new WaitForSeconds(1.15f); _now = Hand(0.9f); yield return new WaitForSeconds(1.15f); }
                        break;
                    case RelaxMode.Kind.Spread:
                        _now = Hand(0.1f, 0.8f);
                        yield return new WaitForSeconds(1.0f);
                        Check("Spread: involved fingers turn green while the hold counts", relax.Holding);
                        yield return new WaitForSeconds(1.15f);
                        _now = Hand(0.3f, 0.3f); yield return new WaitForSeconds(0.3f);
                        _now = Hand(0.1f, 0.8f); yield return new WaitForSeconds(2.15f);
                        break;
                    case RelaxMode.Kind.FingerPiano:
                        for (int f = 0; f < 4; f++)
                        {
                            var g = new[] { 1f, 1f, 1f, 1f }; g[f] = 0.1f;
                            _now = Hand(0.2f, 0.3f, g); yield return new WaitForSeconds(0.4f);
                            _now = Hand(0.2f); yield return new WaitForSeconds(0.15f);
                        }
                        break;
                    case RelaxMode.Kind.Shake:
                        float s0 = Time.time;
                        while (relax.StepIndex == 3 && relax.Current == RelaxMode.State.Step && Time.time - s0 < 5f)
                        {
                            _now = Hand(0.2f, 0.3f, null, 0.03f * Mathf.Sin((Time.time - s0) * Mathf.PI * 2f * 2.5f));
                            yield return null;
                        }
                        break;
                    case RelaxMode.Kind.Wiggle:
                        _now = Hand(0.3f);
                        float w0 = Time.time;
                        while (relax.Current == RelaxMode.State.Step && Time.time - w0 < 10f) yield return null;
                        break;
                }
            }
            float e0 = Time.time;
            while (relax.Current != RelaxMode.State.Finished && Time.time - e0 < 4f) yield return null;
            Check("target endings: ghost pop on each of the 5 steps + Finish; a sparkle on each target ending + Finish",
                  relax.Pops - pops0 == 6 && relax.Sparkles - sp0 == 6 && relax.StepEndings.All(x => x == "target"),
                  $"pops {relax.Pops - pops0}, sparkles {relax.Sparkles - sp0}, endings {string.Join(",", relax.StepEndings)}");
            Check("end screen: step visuals hidden", Visible(ui) == "", Visible(ui));

            // ---- timeout endings: pop, no sparkle (Wiggle always ends by its 8 s, counted as its target) ----
            lm.Replay(); // DONE -> Start
            yield return null;
            Check("DONE -> Start; teacher face back to neutral", lm.CurrentMode == LessonManager.Mode.Start, $"smile {Smile(teacher):0}");
            yield return new WaitForSeconds(0.6f);
            Check("teacher smile faded to neutral after leaving Relax", Smile(teacher) < 3f, $"smile {Smile(teacher):0}");
            _now = Hand(0.5f);
            lm.StartRelaxMode();
            pops0 = relax.Pops; sp0 = relax.Sparkles;
            float tt = Time.time;
            while (relax.Current != RelaxMode.State.Finished && Time.time - tt < 100f) yield return null;
            Check("timeout endings: pop yes, sparkle no (only Wiggle's 8 s end and Finish sparkle)",
                  relax.Pops - pops0 == 6 && relax.Sparkles - sp0 == 2, $"pops {relax.Pops - pops0}, sparkles {relax.Sparkles - sp0}, endings {string.Join(",", relax.StepEndings)}");
            lm.Next(); // MENU
            yield return null;

            // ---- props untouched ----
            Transform room = null, cafe = null;
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (!t.gameObject.scene.IsValid()) continue;
                if (t.name == "ClassroomProps") room = t;
                if (t.name == "CafeProps") cafe = t;
            }
            Check("MENU -> Start; classroom / café props hidden and unmoved", lm.CurrentMode == LessonManager.Mode.Start &&
                  !room.gameObject.activeSelf && !cafe.gameObject.activeSelf && room.Find("TeacherDesk").localPosition == new Vector3(-0.95f, -1.67f, 2.05f));
            relax.DebugSample = null;
            System.IO.File.WriteAllText("Logs/v37-relax-visual-check.txt", Log + $"Errors during the check: {_errors}\n");

            // ---- the unchanged v3.4 RelaxCheck, Task A + B ----
            var a = RelaxCheck.Begin("taskA");
            while (!a.Done) yield return null;
            lm.Tutor.ResetProfile();
            var b = RelaxCheck.Begin("taskB");
            while (!b.Done) yield return null;
            lm.GoStart();
            System.IO.File.WriteAllText("Logs/v37-done.txt", "done\n");
            Done = true;
        }
    }
}
#endif
