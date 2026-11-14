using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using SignSight.Hand;
using SignSight.Learn;
using UnityEngine;

namespace SignSight.EditorTools
{
    /// <summary>
    /// Editor-only Play Mode check of the Tutor Agent (SPEC v3.1 §4.3). Attempts are injected as recogniser results
    /// (so the lesson flow, ScoreManager, FeedbackPanel and the agent all run for real); the hands are not involved.
    /// Writes Logs/v31-tutor-test.txt.
    /// </summary>
    public static class TutorPlayTest
    {
        private static readonly BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;
        private static StringBuilder _log;

        private static LessonManager L => Object.FindFirstObjectByType<LessonManager>();

        private static void Demo() => typeof(LessonManager).GetMethod("OnDemonstrationFinished", Priv).Invoke(L, null);

        private static void Attempt(string signed, string worstFingerOff = null)
        {
            var lm = L;
            var target = lm.CurrentTemplate;
            var store = TemplateStore.FromDefaults();
            var top1 = store.Get(signed);
            var curl = (float[])target.curlRight.Clone();
            if (worstFingerOff == "index") curl[1] = Mathf.Clamp01(curl[1] > 0.5f ? 0.05f : 0.95f);
            bool correct = signed == target.intent;
            var e = new SignEval
            {
                Template = target, Completed = correct, Performed = true, Total = correct ? 0.1f : 0.5f,
                CurlRight = curl, CurlLeft = target.HasLeftCurl ? (float[])target.curlLeft.Clone() : new float[5],
                HasRight = true, HasLeft = target.TwoHanded, PalmRight = target.palm,
                Hint = correct ? "" : MovementHintFor(target),
            };
            var r = new RecognitionResult
            {
                Top1 = top1, Distance = correct ? 0.1f : 0.5f, MovementCompleted = correct, WithinTolerance = correct,
                UserCurl = curl, UserPalm = target.palm, UserCurlLeft = e.CurlLeft, All = new[] { e },
                MovementError = e.Hint,
            };
            typeof(LessonManager).GetMethod("OnRecognized", Priv).Invoke(lm, new object[] { r });
        }

        private static string MovementHintFor(SignTemplate t) => t.movement.type == MovementTypes.Tap ? "Tap twice — you tapped once" : "";

        private static void Line(string s)
        {
            _log.AppendLine(s);
        }

        private static string State()
        {
            var lm = L;
            var t = lm.Tutor;
            return $"{lm.Current} word={lm.CurrentWord?.intent} rule={t.Current?.Rule} reason=\"{t.Current?.Reason}\" focus={t.Current?.FocusFinger}";
        }

        /// <summary>Plays one lesson: every word right on the first try, except the words in 'failTwice'.</summary>
        private static List<string> PlayLesson(int index, string failTwice, string onlyFirstTryCorrect = null)
        {
            var lm = L;
            var shown = new List<string>();
            lm.StartLesson(index);
            for (int guard = 0; guard < 30 && lm.Current == LessonManager.Phase.Demonstrate; guard++)
            {
                string w = lm.CurrentWord.intent;
                shown.Add(w);
                Line($"  show {w}: {State()}");
                Demo();
                if (lm.CurrentIsPracticeOnly)
                {
                    Line($"    practice only (not scored): instruction shown, Next");
                    lm.Next();
                    continue;
                }
                bool fail = w == failTwice && shown.FindAll(x => x == w).Count == 1;
                if (fail)
                {
                    string other = w == "NO" ? "YES" : "NO";
                    Attempt(other, "index");
                    Line($"    try 1 wrong -> {Object.FindFirstObjectByType<ScoreManager>().Last?.Outcome}; hint: {lm.Tutor.LastFingerHint} | {lm.Tutor.LastLooksLike}");
                    lm.CopyAgain();
                    Attempt(other, "index");
                    Line($"    try 2 wrong -> {Object.FindFirstObjectByType<ScoreManager>().Last?.Outcome}; hint: {lm.Tutor.LastFingerHint} | {lm.Tutor.LastLooksLike} | {lm.Tutor.LastMovementHint}");
                }
                else Attempt(w);
                lm.Next();
            }
            Line($"  end: {lm.Current}, score {Object.FindFirstObjectByType<ScoreManager>().Score}");
            return shown;
        }

        public static string Run(bool resetProfile = true)
        {
            _log = new StringBuilder();
            var lm = L;
            if (resetProfile) lm.Tutor.ResetProfile();
            Line($"Profile: {LearnerProfile.PathOnDisk}");

            Line("Run 1, Lesson 1: fail YES twice (index finger off both times), everything else right first try");
            var run1 = PlayLesson(0, "YES");
            int yesAt = run1.IndexOf("YES");
            bool repeatedNext = yesAt >= 0 && yesAt + 1 < run1.Count && run1[yesAt + 1] == "YES";
            Line($"CHECK repeat-after-two-fails: {(repeatedNext ? "PASS" : "FAIL")} (order: {string.Join(", ", run1)})");

            Line("Run 2, Lesson 1: everything right first try");
            var run2 = PlayLesson(0, null);
            Line("Run 3, Lesson 1: words right first try twice in a row should be shown less often (skipped)");
            var run3 = PlayLesson(0, null);
            bool demoted = !run3.Contains("HELLO") && lm.Tutor.Skipped.Contains("HELLO");
            Line($"CHECK demote-after-two-first-try: {(demoted ? "PASS" : "FAIL")} (run 3 order: {string.Join(", ", run3)}; skipped: {string.Join(", ", lm.Tutor.Skipped)})");
            var yes = lm.Tutor.Profile.Find("YES");
            Line($"YES state: attempts {yes.attempts}, wrongVisits {yes.wrongVisits}, worstFingers [{string.Join(",", yes.worstFingerHistory)}], consecutiveFirstTry {yes.consecutiveFirstTry}");

            Line("Run 4, Lesson 3");
            PlayLesson(2, null);
            lm.Next();
            Line("Run 5, Lesson 4 (two lessons after Lesson 1): a review word from Lesson 1 should appear");
            var run4 = PlayLesson(3, null);
            Line($"CHECK review: {(run4.Exists(w => System.Array.FindIndex(lm.Data.lessons[0].words, x => x.intent == w) >= 0) ? "PASS" : "FAIL")} (order: {string.Join(", ", run4)})");
            lm.Next();
            System.IO.File.WriteAllText("Logs/v31-tutor-test.txt", _log.ToString());
            return _log.ToString();
        }

        /// <summary>After a Play Mode restart: is the profile still there?</summary>
        public static string CheckPersisted()
        {
            var p = LearnerProfile.Load();
            var yes = p.Find("YES");
            var hello = p.Find("HELLO");
            string s = $"lessonsStarted {p.lessonsStarted}, words {p.words.Count}; YES wrongVisits {yes?.wrongVisits}, HELLO consecutiveFirstTry {hello?.consecutiveFirstTry}";
            bool ok = p.lessonsStarted >= 4 && yes != null && yes.wrongVisits >= 1 && hello != null && hello.consecutiveFirstTry >= 2;
            s = $"CHECK persistence across Play Mode restart: {(ok ? "PASS" : "FAIL")} ({s})";
            System.IO.File.AppendAllText("Logs/v31-tutor-test.txt", s + "\n");
            return s;
        }
    }
}
