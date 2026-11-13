using SignSight.Hand;
using TMPro;
using UnityEngine;

namespace SignSight.Learn
{
    /// <summary>
    /// Correction (SPEC §8): the target hand (ghost) and your hand side by side, the 1–2 worst fingers marked
    /// green / yellow / red by curl difference, one concrete hint for the worst finger, and what your sign looked like.
    /// Correct answers just show the points.
    /// </summary>
    public class FeedbackPanel : MonoBehaviour
    {
        public static readonly string[] FingerNames = { "Thumb", "Index", "Middle", "Ring", "Pinky" };
        public static readonly Color Green = new Color(0.25f, 0.85f, 0.4f, 0.9f);
        public static readonly Color Yellow = new Color(1f, 0.82f, 0.2f, 0.9f);
        public static readonly Color Red = new Color(1f, 0.3f, 0.25f, 0.95f);
        public static readonly Color Neutral = new Color(0.9f, 0.9f, 0.92f, 0.75f);
        public const float CloseDiff = 0.15f, WrongDiff = 0.30f;

        [SerializeField] private ScoreManager score;
        [SerializeField] private LessonManager lessons;

        [Header("Parts")]
        [SerializeField] private GameObject handsRoot;
        [SerializeField] private GhostHand targetHand;
        [SerializeField] private GhostHand userHand;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text hint;
        [SerializeField] private TMP_Text looksLike;

        /// <summary>The last per-finger result (for tests and the video).</summary>
        public FingerReport LastReport { get; private set; }

        public struct FingerReport
        {
            public float[] Diff;
            public int Worst;
            public int SecondWorst;     // -1 when only one finger is marked
            public Color[] Colors;
            public string Hint;
            public string LooksLike;
        }

        private void OnEnable()
        {
            if (score == null) score = FindFirstObjectByType<ScoreManager>();
            if (lessons == null) lessons = FindFirstObjectByType<LessonManager>();
            score.Scored += OnScored;
            lessons.PhaseChanged += OnPhase;
            Hide();
        }

        private void OnDisable()
        {
            if (score != null) score.Scored -= OnScored;
            if (lessons != null) lessons.PhaseChanged -= OnPhase;
        }

        private void OnPhase(LessonManager.Phase p)
        {
            if (p != LessonManager.Phase.Feedback) Hide();
        }

        private void Hide()
        {
            if (handsRoot != null) handsRoot.SetActive(false);
            SetText(title, "");
            SetText(hint, "");
            SetText(looksLike, "");
        }

        private void OnScored(ScoreManager.Verdict v)
        {
            var target = lessons.CurrentTemplate;
            switch (v.Outcome)
            {
                case ScoreManager.Outcome.CorrectFirstTry:
                case ScoreManager.Outcome.CorrectSecondTry:
                    if (handsRoot != null) handsRoot.SetActive(false);
                    string pts = lessons.IsPractice ? "" : $" +{v.PointsAwarded - (v.StreakBonusAwarded ? ScoreManager.StreakBonus : 0)}";
                    SetText(title, $"<color=#4CD964>Correct!{pts}</color>");
                    SetText(hint, v.StreakBonusAwarded ? $"<color=#4CD964>Streak ×{score.Streak} +{ScoreManager.StreakBonus}</color>" : "");
                    SetText(looksLike, "");
                    return;
            }

            var report = Compare(v.Attempt, target);
            LastReport = report;
            if (handsRoot != null) handsRoot.SetActive(true);
            targetHand.Pose(target.curl);
            targetHand.SetAllColors(new Color(0.8f, 0.88f, 1f, 0.4f));
            userHand.Pose(v.Attempt.UserCurl);
            for (int f = 0; f < 5; f++) userHand.SetFingerColor(f, report.Colors[f]);

            if (v.Outcome == ScoreManager.Outcome.WrongCanRetry)
                SetText(title, $"<color=#FF5A4A>Not quite</color> {target.intent}  <size=70%>compare the hands, then Try Again</size>");
            else
                SetText(title, $"<color=#FF5A4A>+0</color> {target.intent}  <size=70%>here is the correct shape — moving on</size>");
            SetText(hint, report.Hint);
            SetText(looksLike, report.LooksLike);
        }

        /// <summary>Per-finger comparison and hint (SPEC §8.2). Public so it can be tested on its own.</summary>
        public static FingerReport Compare(RecognitionResult attempt, SignTemplate target)
        {
            var r = new FingerReport { Diff = new float[5], Colors = new Color[5], SecondWorst = -1 };
            var user = attempt.UserCurl ?? new float[5];
            for (int f = 0; f < 5; f++) r.Diff[f] = Mathf.Abs(user[f] - target.curl[f]);

            r.Worst = PickWorst(r.Diff, -1);
            int second = PickWorst(r.Diff, r.Worst);
            if (r.Diff[second] >= CloseDiff) r.SecondWorst = second;

            for (int f = 0; f < 5; f++)
            {
                bool marked = f == r.Worst || f == r.SecondWorst;
                if (r.Diff[f] < CloseDiff) r.Colors[f] = Green;
                else if (!marked) r.Colors[f] = Neutral; // only the 1–2 worst fingers are marked
                else r.Colors[f] = r.Diff[f] > WrongDiff ? Red : Yellow;
            }

            if (r.Diff[r.Worst] >= CloseDiff)
            {
                bool tooCurled = user[r.Worst] > target.curl[r.Worst];
                r.Hint = $"{FingerNames[r.Worst]}: {(tooCurled ? "straighten more" : "curl more")}";
            }
            else
            {
                // Fingers match, so the palm direction is what is off.
                r.Hint = $"Fingers look right — turn your palm to face {PalmWords(target.palmLabel)}";
            }

            if (attempt.Top1 != null && attempt.Top1.intent != target.intent)
            {
                r.LooksLike = $"Looks more like {attempt.Top1.intent} — check your {FingerNames[r.Worst].ToLowerInvariant()}{(r.Worst == 0 ? "" : " finger")}";
            }
            else r.LooksLike = "";
            return r;
        }

        // Fingers within this much of the worst count as tied; ties go to the fingers recognition weights most (§4.5).
        private const float TieMargin = 0.05f;
        private static readonly int[] TiePriority = { 1, 2, 3, 4, 0 }; // index, middle, ring, pinky, thumb

        private static int PickWorst(float[] diff, int exclude)
        {
            float max = -1f;
            for (int f = 0; f < 5; f++) if (f != exclude && diff[f] > max) max = diff[f];
            foreach (int f in TiePriority)
            {
                if (f != exclude && diff[f] >= max - TieMargin) return f;
            }
            return exclude == 0 ? 1 : 0;
        }

        private static string PalmWords(string label)
        {
            switch (label)
            {
                case "Forward": return "forward";
                case "TowardSelf": return "you";
                case "Up": return "up";
                case "Down": return "down";
                default: return "sideways";
            }
        }

        private static void SetText(TMP_Text t, string s)
        {
            if (t != null) t.text = s;
        }
    }
}
