#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using SignSight.Exam;
using SignSight.Hand;
using SignSight.Learn;
using UnityEngine;

namespace SignSight.DevTools
{
    /// <summary>
    /// Editor Play Mode only: timed checks of the Café Final Exam (v3.5 §6). The clerk's demonstrations, timers,
    /// scoring and UI run for real; only the learner's attempts are injected as recogniser results (as in
    /// LessonSmokeRun) — they are NOT real hands. Writes Logs/v35-exam-check.txt.
    /// </summary>
    public class ExamCheck : MonoBehaviour
    {
        public bool Done { get; private set; }
        public readonly StringBuilder Log = new StringBuilder();
        private int _errors;

        public static ExamCheck Begin()
        {
            var c = new GameObject("ExamCheck").AddComponent<ExamCheck>();
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

        /// <summary>A recogniser result for 'signed' that the lesson rule accepts (or not) — like LessonSmokeRun.Inject.</summary>
        private static RecognitionResult Result(string signed, bool movementDone = true)
        {
            var t = TemplateStore.Canonical(signed);
            return new RecognitionResult { Top1 = t, Distance = 0.1f, MovementCompleted = movementDone, WithinTolerance = true, All = new SignEval[0] };
        }

        private IEnumerator WaitAnswer(ExamMode ex, int turn, float max = 20f)
        {
            float t0 = Time.time;
            while (!(ex.Current == ExamMode.State.Answer && ex.TurnIndex == turn) && Time.time - t0 < max) yield return null;
        }

        private IEnumerator WaitState(ExamMode ex, ExamMode.State s, float max = 20f)
        {
            float t0 = Time.time;
            while (ex.Current != s && Time.time - t0 < max) yield return null;
        }

        private IEnumerator Run()
        {
            var lm = FindFirstObjectByType<LessonManager>();
            while (lm.Current == LessonManager.Phase.Loading) yield return null;
            var ex = lm.Exam;
            var ui = GameObject.Find("LearnUI").transform;
            var word = ui.Find("WordCard").GetComponentsInChildren<TMPro.TMP_Text>(true).First(t => t.name == "Word");
            var meaning = ui.Find("WordCard").GetComponentsInChildren<TMPro.TMP_Text>(true).First(t => t.name != "Word");
            var teacher = FindFirstObjectByType<TeacherAvatarDriver>();
            var cafe = ui.Find("CafeProps");
            var score = FindFirstObjectByType<ScoreManager>();

            // ---- entry from the Start card ----
            lm.GoStart();
            yield return null;
            Vector3 teacherHome = teacher.transform.localPosition;
            ui.Find("StartCards/ExamCard").GetComponentInChildren<Oculus.Interaction.InteractableUnityEventWrapper>(true).WhenSelect.Invoke();
            yield return null;
            // v3.10a: the café props appear at black, after the fade (0.3 s out, 0.3 s in)
            var envMode = lm.GetComponent<SignSight.UI.EnvironmentMode>();
            while (envMode.Fading) yield return null;
            Check("Start card 'Final Exam' -> Exam mode, turn 1, café props on, teacher behind the counter",
                  lm.CurrentMode == LessonManager.Mode.Exam && ex.TurnIndex == 0 && cafe.gameObject.activeSelf &&
                  teacher.transform.localPosition == ExamMode.TeacherCafePosition,
                  $"{lm.CurrentMode}, {ex.Current}, teacher {teacher.transform.localPosition}");
            Check("no top bar / Tutor panel in the exam", !ui.Find("TopBar").gameObject.activeSelf && !ui.Find("TutorPanel").gameObject.activeSelf);

            // ---- happy path: 7 first-try answers ----
            float t0 = Time.time;
            bool answerHidden = true;
            for (int i = 0; i < ExamMode.Script.Length; i++)
            {
                yield return WaitAnswer(ex, i);
                // the answer word must not be on the card before HINT
                var exp = ExamMode.Script[i].expected;
                string card = word.text + " " + meaning.text;
                if (exp.Any(e => card.Contains(e)) && i != 0 && i != 6) answerHidden = false; // turns 1 / 7: the clerk's own line is HELLO / THANK-YOU
                ex.Attempt(Result(exp[0]));
                yield return null;
            }
            yield return WaitState(ex, ExamMode.State.Done);
            Check("happy path: 7 first-try answers -> 70 / 70, 3 stars, end screen",
                  ex.Current == ExamMode.State.Done && ex.Total == 70 && ex.Stars == 3 && ex.Records.All(r => r.Result == ExamMode.Result.FirstTry),
                  $"{ex.Total} / 70, {ex.Stars} stars, {Time.time - t0:0} s, card '{word.text}'");
            Check("the answer word is not shown before HINT (turns 2-6)", answerHidden);
            Check("cup on the counter after the drink order", ui.Find("CafeProps/CafeCup").gameObject.activeSelf);
            for (int i = 0; i < 7; i++) Log.AppendLine("  " + StripTags(ex.BreakdownLine(i)));
            yield return new WaitForSeconds(0.3f);

            // ---- RETRY: realistic path ----
            lm.Replay(); // RETRY EXAM
            yield return null;
            Check("RETRY EXAM -> turn 1, fresh total", ex.TurnIndex == 0 && ex.Total == 0 && ex.Current != ExamMode.State.Done);
            // 1 first try
            yield return WaitAnswer(ex, 0); ex.Attempt(Result("HELLO"));
            // 2 wrong (GOODBYE), then right -> +5
            yield return WaitAnswer(ex, 1); ex.Attempt(Result("GOODBYE"));
            yield return null;
            string fb = ex.Feedback;
            Check("wrong attempt shows a 'Looks more like' line", fb.StartsWith("Looks more like GOODBYE"), fb);
            yield return WaitAnswer(ex, 1); ex.Attempt(Result("WANT"));
            // 3 two wrong -> missed, the clerk shows COFFEE
            yield return WaitAnswer(ex, 2); ex.Attempt(Result("BREAD"));
            yield return WaitAnswer(ex, 2); ex.Attempt(Result("EAT"));
            yield return null;
            Check("two wrong tries -> 0, the clerk shows the sign", ex.Current == ExamMode.State.ClerkShows && teacher.IsDemonstrating && ex.Records[2].Shown == "COFFEE",
                  $"{ex.Current}, shown {ex.Records[2].Shown}");
            // 4 HINT, then right on the first attempt -> max +5
            yield return WaitAnswer(ex, 3);
            lm.CopyAgain(); // HINT
            yield return null;
            Check("HINT shows the answer on the card", meaning.text.Contains("Hint: PLEASE"), StripTags(meaning.text).Replace("\n", " | "));
            ex.Attempt(Result("PLEASE"));
            // 5 timeout (10 s, no recognition) -> 0, the clerk shows HOT
            yield return WaitAnswer(ex, 4);
            float tt = Time.time;
            yield return WaitState(ex, ExamMode.State.ClerkShows, 15f);
            Check("10 s without a recognition -> 0, the clerk shows the sign", ex.Records[4].Result == ExamMode.Result.Missed && ex.Records[4].TimedOut,
                  $"after {Time.time - tt:0.0} s, shown {ex.Records[4].Shown}");
            // 6, 7 first try
            yield return WaitAnswer(ex, 5); ex.Attempt(Result("ONE"));
            yield return WaitAnswer(ex, 6); ex.Attempt(Result("THANK-YOU"));
            yield return WaitState(ex, ExamMode.State.Done);
            int expect = 10 + 5 + 0 + 5 + 0 + 10 + 10;
            Check("realistic path: total matches the arithmetic (10+5+0+5+0+10+10 = 40) and stars = ScoreManager.Stars(40)",
                  ex.Total == expect && ex.Stars == ScoreManager.Stars(expect), $"{ex.Total}, {ex.Stars} stars");
            var r = ex.Records;
            Check("breakdown: second try, missed, hinted, timed out listed correctly",
                  r[1].Result == ExamMode.Result.SecondTry && r[1].Points == 5 && r[2].Result == ExamMode.Result.Missed && r[2].Points == 0 &&
                  r[3].Hinted && r[3].Points == 5 && r[4].TimedOut);
            for (int i = 0; i < 7; i++) Log.AppendLine("  " + StripTags(ex.BreakdownLine(i)));
            yield return new WaitForSeconds(0.5f);

            // ---- choice turns ----
            lm.Replay();
            yield return WaitAnswer(ex, 0); ex.Attempt(Result("HELLO"));
            yield return WaitAnswer(ex, 1); ex.Attempt(Result("WANT"));
            yield return WaitAnswer(ex, 2); ex.Attempt(Result("WATER"));
            yield return WaitAnswer(ex, 3); ex.Attempt(Result("PLEASE"));
            yield return WaitAnswer(ex, 4); ex.Attempt(Result("WATER")); // outside {HOT, COLD}
            yield return null;
            string outside = ex.Feedback;
            Check("a sign outside the set (WATER for HOT? COLD?) is a wrong attempt with a 'Looks more like' line",
                  ex.Records[4].Result == ExamMode.Result.None && ex.Attempts == 1 && outside.StartsWith("Looks more like WATER"), outside);
            yield return WaitAnswer(ex, 4); ex.Attempt(Result("COLD"));
            yield return WaitAnswer(ex, 5); ex.Attempt(Result("THREE"));
            yield return WaitAnswer(ex, 6); ex.Attempt(Result("THANK-YOU"));
            yield return WaitState(ex, ExamMode.State.Done);
            Check("choice turns: WATER (drink), COLD, THREE accepted with the same points as COFFEE / HOT / ONE",
                  ex.Records[2].Points == 10 && ex.Records[2].Signed == "WATER" && ex.Records[4].Points == 5 && ex.Records[4].Signed == "COLD" &&
                  ex.Records[5].Points == 10 && ex.Records[5].Signed == "THREE", $"{ex.Records[2].Signed} +{ex.Records[2].Points}, {ex.Records[4].Signed} +{ex.Records[4].Points} (second try), {ex.Records[5].Signed} +{ex.Records[5].Points}");
            lm.Replay();
            yield return WaitAnswer(ex, 0); ex.Attempt(Result("HELLO"));
            yield return WaitAnswer(ex, 1); ex.Attempt(Result("WANT"));
            yield return WaitAnswer(ex, 2); ex.Attempt(Result("MILK"));
            yield return WaitAnswer(ex, 3); ex.Attempt(Result("PLEASE"));
            yield return WaitAnswer(ex, 4); ex.Attempt(Result("HOT"));
            yield return WaitAnswer(ex, 5); ex.Attempt(Result("TWO"));
            Check("choice turns: MILK, HOT, TWO first try +10 each", ex.Records[2].Points == 10 && ex.Records[4].Points == 10 && ex.Records[5].Points == 10);

            // ---- MENU -> Start, teacher back home, café hidden ----
            lm.Next(); // MENU
            yield return null;
            while (lm.GetComponent<SignSight.UI.EnvironmentMode>().Fading) yield return null; // café hides at black
            Check("MENU -> Start; teacher back at his place; café props hidden",
                  lm.CurrentMode == LessonManager.Mode.Start && teacher.transform.localPosition == teacherHome && !cafe.gameObject.activeSelf,
                  $"{lm.CurrentMode}, teacher {teacher.transform.localPosition}");

            // ---- entry from the Lesson 6 summary (score untouched) ----
            lm.StartLesson(5);
            typeof(LessonManager).GetMethod("SetPhase", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(lm, new object[] { LessonManager.Phase.LessonSummary });
            yield return null;
            var examBtn = ui.Find("Buttons/ExamButton");
            int lessonScore = score.Score;
            Check("Lesson 6 summary shows the FINAL EXAM button", examBtn != null && examBtn.gameObject.activeSelf);
            examBtn.GetComponentInChildren<Oculus.Interaction.InteractableUnityEventWrapper>(true).WhenSelect.Invoke();
            yield return null;
            Check("FINAL EXAM from the summary -> Exam mode, lesson score unchanged", lm.CurrentMode == LessonManager.Mode.Exam && score.Score == lessonScore,
                  $"{lm.CurrentMode}, lesson score {lessonScore} -> {score.Score}");
            lm.StartLesson(4);
            typeof(LessonManager).GetMethod("SetPhase", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(lm, new object[] { LessonManager.Phase.LessonSummary });
            yield return null;
            Check("Lesson 5 summary has no FINAL EXAM button", !examBtn.gameObject.activeSelf);
            lm.GoStart();

            System.IO.File.WriteAllText("Logs/v35-exam-check.txt", Log + $"Errors during the check: {_errors}\n");
            Done = true;
        }

        private static string StripTags(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "");
    }
}
#endif
