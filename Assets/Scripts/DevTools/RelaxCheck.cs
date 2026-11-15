#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using SignSight.Learn;
using SignSight.Relax;
using UnityEngine;

namespace SignSight.DevTools
{
    /// <summary>
    /// Editor Play Mode only: timed checks of Finger Relax (v3.4 §5). The hand frames are fed through
    /// RelaxMode.DebugSample (simulated measurements, NOT real hands): the step logic, timers, UI and return paths run
    /// for real. Part "taskB" also plays Lesson 1 for real with LessonSmokeRun. Writes Logs/v34-relax-check.txt.
    /// </summary>
    public class RelaxCheck : MonoBehaviour
    {
        public bool Done { get; private set; }
        public readonly StringBuilder Log = new StringBuilder();
        private int _errors;
        private int _attempts;

        public static RelaxCheck Begin(string part)
        {
            var c = new GameObject("RelaxCheck").AddComponent<RelaxCheck>();
            c.StartCoroutine(c.Run(part));
            return c;
        }

        private void OnEnable() => Application.logMessageReceived += OnLog;
        private void OnDisable() => Application.logMessageReceived -= OnLog;

        private void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                if (msg.Contains("/api/exec")) return; // the editor bridge's own HTTP noise
                _errors++;
                Log.AppendLine($"  ERROR: {msg}");
            }
        }

        private void Check(string name, bool ok, string detail = "") =>
            Log.AppendLine($"CHECK {name}: {(ok ? "PASS" : "FAIL")}{(detail != "" ? " (" + detail + ")" : "")}");

        // ---- simulated measurements ----
        private static RelaxMode.Sample Hand(float curl, float spread = 0.3f, float[] gaps = null, float wristX = 0f, float[] curls = null) =>
            new RelaxMode.Sample
            {
                Tracked = true,
                Curl = curls ?? new[] { curl * 0.6f, curl, curl, curl, curl },
                Spread = spread,
                TipGap = gaps ?? new[] { 1f, 1f, 1f, 1f },
                Wrist = new Vector3(wristX, -0.3f, 0.35f),
            };

        private RelaxMode.Sample _now;
        private static readonly RelaxMode.Sample NoHand = new RelaxMode.Sample { Tracked = false };

        private IEnumerator Run(string part)
        {
            var lm = FindFirstObjectByType<LessonManager>();
            while (lm.Current == LessonManager.Phase.Loading) yield return null;
            var relax = lm.Relax;
            var score = FindFirstObjectByType<ScoreManager>();
            lm.Attempted += (w, r) => { if (lm.CurrentMode == LessonManager.Mode.Relax) _attempts++; };
            relax.DebugSample = () => _now;
            var ui = GameObject.Find("LearnUI").transform;
            var wordText = ui.Find("WordCard").GetComponentsInChildren<TMPro.TMP_Text>(true).First(t => t.name == "Word");
            var instr = ui.Find("Instruction").GetComponent<TMPro.TMP_Text>();

            if (part == "taskA")
            {
                // ---- entry from the Start card (pinch = the card's select event) ----
                lm.GoStart();
                yield return null;
                var card = ui.Find("StartCards/RelaxCard").GetComponentInChildren<Oculus.Interaction.InteractableUnityEventWrapper>(true);
                int score0 = score.Score;
                _now = NoHand;
                card.WhenSelect.Invoke();
                yield return null;
                Check("Start card 'Finger Relax' -> Relax mode, step 1", lm.CurrentMode == LessonManager.Mode.Relax && relax.StepIndex == 0, $"{lm.CurrentMode}, step {relax.StepIndex + 1} {relax.CurrentStep?.name}");
                bool noScoreUi = !ui.Find("TopBar").gameObject.activeSelf && !ui.Find("TutorPanel").gameObject.activeSelf;
                Check("no top bar (score / streak) and no Tutor panel in Relax", noScoreUi);
                Check("safety line + why line on screen", instr.text.Contains(RelaxMode.SafetyLine) && instr.text.Contains(RelaxMode.WhyLine));
                Check("teacher rests (not demonstrating)", !FindFirstObjectByType<TeacherAvatarDriver>().IsDemonstrating);

                // ---- hand not tracked: ring pauses, "Show me your hand", no fail ----
                yield return new WaitForSeconds(2.5f);
                Check("no hand for 2.5 s: 'Show me your hand', ring at 0, still step 1", relax.Status == RelaxMode.ShowHand && relax.Progress == 0f && relax.StepIndex == 0 && relax.Current == RelaxMode.State.Step, $"status '{relax.Status}', progress {relax.Progress:0.00}");

                // ---- doing the steps (target path) ----
                float t0 = Time.time;
                // 1 Open & Close x2
                for (int c = 0; c < 2; c++)
                {
                    _now = Hand(0.1f); yield return new WaitForSeconds(1.15f);
                    _now = Hand(0.9f); yield return new WaitForSeconds(1.15f);
                }
                yield return new WaitForSeconds(0.1f);
                Check("step 1 Open & Close done by target", relax.StepEndings.Count >= 1 && relax.StepEndings[0] == "target", string.Join(",", relax.StepEndings));
                yield return WaitStep(relax, 1);
                // 2 Spread x2 (let go between)
                _now = Hand(0.1f, 0.8f); yield return new WaitForSeconds(2.15f);
                _now = Hand(0.3f, 0.3f); yield return new WaitForSeconds(0.3f);
                _now = Hand(0.1f, 0.8f); yield return new WaitForSeconds(2.15f);
                yield return new WaitForSeconds(0.1f);
                Check("step 2 Spread Wide done by target", relax.StepEndings.Count >= 2 && relax.StepEndings[1] == "target", string.Join(",", relax.StepEndings));
                yield return WaitStep(relax, 2);
                // 3 Finger piano
                for (int f = 0; f < 4; f++)
                {
                    var g = new[] { 1f, 1f, 1f, 1f };
                    g[f] = 0.1f;
                    _now = Hand(0.2f, 0.3f, g); yield return new WaitForSeconds(0.4f);
                    _now = Hand(0.2f); yield return new WaitForSeconds(0.15f);
                }
                Check("step 3 Finger Piano done by target", relax.StepEndings.Count >= 3 && relax.StepEndings[2] == "target", string.Join(",", relax.StepEndings));
                yield return WaitStep(relax, 3);
                // 4 Shake: wrist +-3 cm at 2.5 Hz
                float s0 = Time.time;
                while (relax.StepIndex == 3 && relax.Current == RelaxMode.State.Step && Time.time - s0 < 5f)
                {
                    _now = Hand(0.2f, 0.3f, null, 0.03f * Mathf.Sin((Time.time - s0) * Mathf.PI * 2f * 2.5f));
                    yield return null;
                }
                Check("step 4 Shake It Out done by target", relax.StepEndings.Count >= 4 && relax.StepEndings[3] == "target", $"{string.Join(",", relax.StepEndings)}, {Time.time - s0:0.0}s");
                yield return WaitStep(relax, 4);
                // 5 Wiggle (fingers move) — always 8 s
                float w0 = Time.time;
                bool wiggled = false;
                while (relax.Current == RelaxMode.State.Step && Time.time - w0 < 12f)
                {
                    float k = Time.time * 8f;
                    _now = Hand(0f, 0.3f, null, 0f, new[] { 0.2f, 0.3f + 0.25f * Mathf.Sin(k), 0.3f + 0.25f * Mathf.Sin(k + 1), 0.3f + 0.25f * Mathf.Sin(k + 2), 0.3f + 0.25f * Mathf.Sin(k + 3) });
                    wiggled |= relax.Wiggling;
                    yield return null;
                }
                Check("step 5 Wiggle & Rest ends after 8 s (wiggle seen)", wiggled && Mathf.Abs(Time.time - w0 - 8f) < 0.6f, $"{Time.time - w0:0.0}s");
                yield return new WaitForSeconds(1.3f);
                var fx = FindObjectsByType<CelebrationFX>(FindObjectsSortMode.None).FirstOrDefault(x => x.transform.parent != null && x.transform.parent.name == "RelaxSparkle");
                Check("end screen 'All loose — nice work' + one soft sparkle, DONE shown, SKIP hidden",
                      relax.Current == RelaxMode.State.Finished && wordText.text == RelaxMode.FinishedTitle && fx != null && fx.Plays >= 1 &&
                      ui.Find("Buttons/ReplayButton") != null,
                      $"{relax.Current}, '{wordText.text}', sparkles {fx?.Plays}, run {Time.time - t0:0}s, endings {string.Join(",", relax.StepEndings)}");
                LogButtons(ui);
                Check("score unchanged, no word attempts", score.Score == score0 && _attempts == 0, $"score {score0} -> {score.Score}, attempts {_attempts}");
                lm.Replay(); // DONE
                yield return null;
                Check("DONE (standalone) -> Start", lm.CurrentMode == LessonManager.Mode.Start, $"{lm.CurrentMode} / {lm.Current}");

                // ---- timeout path: a tracked hand that never matches ----
                lm.StartRelaxMode();
                _now = Hand(0.5f, 0.3f);
                float tt = Time.time;
                while (relax.Current != RelaxMode.State.Finished && Time.time - tt < 100f) yield return null;
                Check("all steps time out with a wrong / partial hand shape, never 'wrong', ends on the end screen",
                      relax.Current == RelaxMode.State.Finished && relax.StepEndings.Take(4).All(e => e == "timeout"),
                      $"{string.Join(",", relax.StepEndings)}, total {Time.time - tt:0.0}s");
                lm.Replay();
                yield return null;
                Check("DONE -> Start", lm.CurrentMode == LessonManager.Mode.Start);

                // ---- SKIP and MENU ----
                lm.StartRelaxMode();
                _now = NoHand;
                yield return null;
                lm.CopyAgain(); // SKIP
                yield return null;
                Check("SKIP -> next step", relax.StepIndex == 1 && relax.Current == RelaxMode.State.Step && relax.StepEndings[0] == "skip", $"step {relax.StepIndex + 1}");
                for (int i = 0; i < 4; i++) { lm.CopyAgain(); yield return null; }
                Check("SKIP on the last step -> end screen", relax.Current == RelaxMode.State.Finished);
                lm.StartRelaxMode();
                yield return null;
                lm.CopyAgain();
                yield return null;
                lm.Next(); // MENU
                yield return null;
                Check("MENU mid-way -> Start (not the lesson list)", lm.CurrentMode == LessonManager.Mode.Start && !relax.Ghost.gameObject.activeSelf, $"{lm.CurrentMode} / {lm.Current}");
                System.IO.File.WriteAllText("Logs/v34-relax-check-taskA.txt", Log.ToString() + $"Errors during the check: {_errors}\n");
            }
            else if (part == "taskB")
            {
                // ---- finish Lesson 1 for real, then RELAX on the summary ----
                lm.GoStart();
                lm.StartLearnMode();
                var smoke = LessonSmokeRun.Begin(0);
                while (!smoke.Done) yield return null;
                Log.AppendLine("Lesson 1 (LessonSmokeRun): " + smoke.Log.ToString().Trim().Split('\n').Last());
                var relaxBtn = ui.Find("Buttons/RelaxButton");
                var nextLabel = ui.Find("Buttons/NextButton/Label")?.GetComponent<TMPro.TMP_Text>();
                Check("Lesson 1 summary shows RELAX and NEXT LESSON", lm.Current == LessonManager.Phase.LessonSummary && relaxBtn != null && relaxBtn.gameObject.activeSelf && nextLabel != null && nextLabel.text.Contains("LESSON"),
                      $"{lm.Current}, relax {relaxBtn?.gameObject.activeSelf}, next '{nextLabel?.text.Replace("\n", " ")}'");
                LogButtons(ui);
                int score0 = score.Score;
                relaxBtn.GetComponentInChildren<Oculus.Interaction.InteractableUnityEventWrapper>(true).WhenSelect.Invoke(); // pinch RELAX
                yield return null;
                Check("RELAX -> Relax mode (pending Lesson 2)", lm.CurrentMode == LessonManager.Mode.Relax && lm.PendingLessonAfterRelax == 1 && relax.ReturnToLesson);
                yield return RunThroughBySkip(lm, relax);
                Log.AppendLine($"  end screen: '{wordText.text}' / instruction ok {instr.text.Contains(RelaxMode.SafetyLine)}");
                lm.Replay(); // DONE
                yield return null;
                Check("finish Relax -> Lesson 2 starts automatically", lm.CurrentMode == LessonManager.Mode.Learn && lm.LessonIndex == 1 && lm.Current == LessonManager.Phase.Demonstrate,
                      $"{lm.CurrentMode}, lesson {lm.LessonIndex + 1}, {lm.Current}, word {lm.CurrentWord?.intent}");
                Check("score unchanged, no word attempts during Relax", score.Score == score0 && _attempts == 0, $"score {score0} -> {score.Score}");

                // ---- NEXT LESSON without Relax (summary forced for Lesson 1) ----
                ForceSummary(lm, 0);
                yield return null;
                lm.Next();
                yield return null;
                Check("NEXT LESSON (no Relax) -> Lesson 2 straight away", lm.CurrentMode == LessonManager.Mode.Learn && lm.LessonIndex == 1 && lm.Current == LessonManager.Phase.Demonstrate, $"lesson {lm.LessonIndex + 1}, {lm.Current}");

                // ---- MENU out of inter-lesson Relax ----
                ForceSummary(lm, 0);
                yield return null;
                lm.StartRelaxThenNextLesson();
                yield return null;
                lm.CopyAgain();
                yield return null;
                lm.Next(); // MENU
                yield return new WaitForSeconds(0.5f);
                Check("MENU out of inter-lesson Relax -> Start, Lesson 2 NOT started", lm.CurrentMode == LessonManager.Mode.Start && lm.CurrentWord == null && lm.PendingLessonAfterRelax == LessonManager.NoPending && !relax.ReturnToLesson,
                      $"{lm.CurrentMode}, word {lm.CurrentWord?.intent ?? "none"}");
                lm.StartRelaxMode();
                yield return RunThroughBySkip(lm, relax);
                lm.Replay();
                yield return null;
                Check("a later standalone Relax still returns to Start", lm.CurrentMode == LessonManager.Mode.Start);

                // ---- last lesson ----
                ForceSummary(lm, lm.Data.lessons.Length - 1); // the last lesson (v3.5: Lesson 6)
                yield return null;
                var lbl = nextLabel != null ? nextLabel.text.Replace("\n", " ") : "";
                lm.StartRelaxThenNextLesson();
                yield return null;
                Check($"Last lesson (Lesson {lm.Data.lessons.Length}) summary -> RELAX (pending = Complete)", lm.CurrentMode == LessonManager.Mode.Relax && lm.PendingLessonAfterRelax == -1, $"summary next label '{lbl}'");
                yield return RunThroughBySkip(lm, relax);
                lm.Replay();
                yield return null;
                Check("finish Relax after the last lesson -> Complete screen", lm.CurrentMode == LessonManager.Mode.Learn && lm.Current == LessonManager.Phase.Complete, $"{lm.CurrentMode} / {lm.Current}");
                lm.Next(); // MENU at the end -> Start
                yield return null;
                Check("Complete -> MENU -> Start", lm.CurrentMode == LessonManager.Mode.Start);
                System.IO.File.WriteAllText("Logs/v34-relax-check-taskB.txt", Log.ToString() + $"Errors during the check: {_errors}\n");
            }
            relax.DebugSample = null;
            Done = true;
        }

        private static IEnumerator WaitStep(RelaxMode relax, int index)
        {
            float t = Time.time;
            while ((relax.StepIndex != index || relax.Current != RelaxMode.State.Step) && Time.time - t < 3f) yield return null;
        }

        private static IEnumerator RunThroughBySkip(LessonManager lm, RelaxMode relax)
        {
            for (int i = 0; i < 5 && relax.Current != RelaxMode.State.Finished; i++)
            {
                yield return new WaitForSeconds(0.3f);
                lm.CopyAgain(); // SKIP
            }
            yield return null;
        }

        private static void ForceSummary(LessonManager lm, int lesson)
        {
            // Test shortcut: start the lesson, then jump to its summary (Lesson 1 was also finished for real above).
            lm.StartLesson(lesson);
            typeof(LessonManager).GetMethod("SetPhase", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(lm, new object[] { LessonManager.Phase.LessonSummary });
        }

        private void LogButtons(Transform ui)
        {
            var on = ui.Find("Buttons").GetComponentsInChildren<Transform>(false)
                .Where(t => t.parent == ui.Find("Buttons") && t.gameObject.activeSelf)
                .Select(t => $"{t.name}[{t.Find("Label")?.GetComponent<TMPro.TMP_Text>()?.text.Replace("\n", " ")}]");
            Log.AppendLine("  buttons: " + string.Join(", ", on));
        }
    }
}
#endif
