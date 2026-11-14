using System.Collections;
using SignSight.Hand;
using TMPro;
using UnityEngine;

namespace SignSight.Learn
{
    /// <summary>
    /// Correction (SPEC v3 §8): the target hand(s) (ghost) and your hand(s) side by side — two of each for two-handed
    /// signs — the 1–2 worst fingers marked green / yellow / red by curl difference, one concrete hint for the worst
    /// finger, what your sign looked like, and one movement hint line (§8.3). The target ghost replays the movement
    /// once at 0.5x speed (§8.1). Correct answers just show the points.
    /// </summary>
    public class FeedbackPanel : MonoBehaviour
    {
        public static readonly string[] FingerNames = { "Thumb", "Index", "Middle", "Ring", "Pinky" };
        public static readonly Color Green = new Color(0.25f, 0.85f, 0.4f, 0.9f);
        public static readonly Color Yellow = new Color(1f, 0.82f, 0.2f, 0.9f);
        public static readonly Color Red = new Color(1f, 0.3f, 0.25f, 0.95f);
        public static readonly Color Neutral = new Color(0.9f, 0.9f, 0.92f, 0.75f);
        public static readonly Color GhostColor = new Color(0.8f, 0.88f, 1f, 0.4f);
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
        [Tooltip("Movement hint line (§8.3). Created below LooksLike at runtime if empty.")]
        [SerializeField] private TMP_Text movementHint;

        [Header("Two hands / replay (SPEC v3 §8.1)")]
        [SerializeField] private float twoHandSize = 0.55f;
        [SerializeField] private float replaySpeed = 0.5f;
        [SerializeField] private float replayDelay = 0.6f;

        /// <summary>The last per-finger result (for tests and the video).</summary>
        public FingerReport LastReport { get; private set; }
        public string LastMovementHint { get; private set; } = "";
        public bool IsReplaying => _replay != null;

        public struct FingerReport
        {
            public float[] Diff;
            public int Worst;
            public int SecondWorst;     // -1 when only one finger is marked
            public Color[] Colors;
            public string Hint;
            public string LooksLike;
            // Two-handed signs: the non-dominant hand's comparison; LeftWorse = the hint is about that hand.
            public float[] LeftDiff;
            public Color[] LeftColors;
            public bool LeftWorse;
        }

        private GhostHand _targetLeft, _userLeft;
        private Vector3 _targetPos, _userPos;
        private Coroutine _replay;

        private void OnEnable()
        {
            if (score == null) score = FindFirstObjectByType<ScoreManager>();
            if (lessons == null) lessons = FindFirstObjectByType<LessonManager>();
            score.Scored += OnScored;
            lessons.PhaseChanged += OnPhase;
            EnsureParts();
            Hide();
        }

        private void OnDisable()
        {
            if (score != null) score.Scored -= OnScored;
            if (lessons != null) lessons.PhaseChanged -= OnPhase;
        }

        private void EnsureParts()
        {
            if (_targetLeft != null) return;
            _targetPos = targetHand.transform.localPosition;
            _userPos = userHand.transform.localPosition;
            // Left hands are mirrored copies of the right ones (one implementation).
            _targetLeft = Instantiate(targetHand.gameObject, targetHand.transform.parent).GetComponent<GhostHand>();
            _targetLeft.name = "TargetHandLeft";
            _targetLeft.IsLeft = true;
            _userLeft = Instantiate(userHand.gameObject, userHand.transform.parent).GetComponent<GhostHand>();
            _userLeft.name = "YourHandLeft";
            _userLeft.IsLeft = true;
            if (movementHint == null && looksLike != null)
            {
                movementHint = Instantiate(looksLike, looksLike.transform.parent);
                movementHint.name = "MovementHint";
                movementHint.transform.localPosition = looksLike.transform.localPosition + new Vector3(0f, -0.023f, 0f);
            }
        }

        private void OnPhase(LessonManager.Phase p)
        {
            if (p != LessonManager.Phase.Feedback) Hide();
        }

        private void Hide()
        {
            StopReplay();
            if (handsRoot != null) handsRoot.SetActive(false);
            SetText(title, "");
            SetText(hint, "");
            SetText(looksLike, "");
            SetText(movementHint, "");
        }

        private void OnScored(ScoreManager.Verdict v)
        {
            var target = lessons.CurrentTemplate;
            switch (v.Outcome)
            {
                case ScoreManager.Outcome.CorrectFirstTry:
                case ScoreManager.Outcome.CorrectSecondTry:
                    StopReplay();
                    if (handsRoot != null) handsRoot.SetActive(false);
                    string pts = lessons.IsPractice ? "" : $" +{v.PointsAwarded - (v.StreakBonusAwarded ? ScoreManager.StreakBonus : 0)}";
                    SetText(title, $"<color=#4CD964>Correct!{pts}</color>");
                    SetText(hint, v.StreakBonusAwarded ? $"<color=#4CD964>Streak ×{score.Streak} +{ScoreManager.StreakBonus}</color>" : "");
                    SetText(looksLike, "");
                    SetText(movementHint, "");
                    return;
            }
            // Compare against what the learner did for the word being learned: the target's own start pose,
            // not Top1's (Top1 may have judged a different moment of the attempt).
            var attempt = v.Attempt;
            var te = attempt.For(target.intent);
            if (te.HasValue && te.Value.CurlRight != null)
            {
                attempt.UserCurl = te.Value.CurlRight;
                attempt.UserCurlLeft = te.Value.HasLeft ? te.Value.CurlLeft : null; // not seen: no left-hand finger marks
                attempt.UserPalm = te.Value.PalmRight;
            }
            ShowCorrection(attempt, target, v.Outcome == ScoreManager.Outcome.WrongCanRetry);
        }

        /// <summary>Shows the ghost-hand comparison, hints and the 0.5x replay for a wrong attempt.</summary>
        public void ShowCorrection(RecognitionResult attempt, SignTemplate target, bool canRetry)
        {
            EnsureParts();
            var report = Compare(attempt, target);
            LastReport = report;
            if (handsRoot != null) handsRoot.SetActive(true);

            bool two = target.TwoHanded;
            LayoutHands(target, two);

            targetHand.Pose(target.curlRight);
            targetHand.SetAllColors(GhostColor);
            userHand.Pose(attempt.UserCurl ?? new float[5]);
            for (int f = 0; f < 5; f++) userHand.SetFingerColor(f, report.Colors[f]);
            if (two)
            {
                _targetLeft.Pose(target.HasLeftCurl ? target.curlLeft : target.curlRight);
                _targetLeft.SetAllColors(GhostColor);
                bool seen = attempt.UserCurlLeft != null && attempt.UserCurlLeft.Length == 5;
                _userLeft.gameObject.SetActive(seen); // a hand that was not seen is not drawn
                if (seen)
                {
                    _userLeft.Pose(attempt.UserCurlLeft);
                    for (int f = 0; f < 5; f++) _userLeft.SetFingerColor(f, report.LeftColors != null ? report.LeftColors[f] : Neutral);
                }
            }

            SetText(title, canRetry
                ? $"<color=#FF5A4A>Not quite</color> {target.intent}  <size=70%>compare the hands, then Try Again</size>"
                : $"<color=#FF5A4A>+0</color> {target.intent}  <size=70%>here is the correct sign — moving on</size>");
            SetText(hint, report.Hint);
            SetText(looksLike, report.LooksLike);
            LastMovementHint = MovementHint(attempt, target);
            SetText(movementHint, string.IsNullOrEmpty(LastMovementHint) ? "" : $"<color=#7FC8FF>Movement:</color> {LastMovementHint}");

            StopReplay();
            _replay = StartCoroutine(Replay(target));
        }

        /// <summary>The movement line (§8.3): what was wrong with the movement for the word being learned.</summary>
        public static string MovementHint(RecognitionResult attempt, SignTemplate target)
        {
            var e = attempt.For(target.intent);
            if (e.HasValue && !string.IsNullOrEmpty(e.Value.Hint)) return e.Value.Hint;
            if (e.HasValue && !e.Value.Completed) return DefaultMovementHint(target);
            if (e.HasValue && e.Value.Location >= 0.5f) return StartHint(target);
            return "";
        }

        private static string DefaultMovementHint(SignTemplate t)
        {
            switch (t.movement.type)
            {
                case MovementTypes.Wave: return "Wave it — side to side, twice";
                case MovementTypes.WristNod: return "Nod your wrist — up and down, twice";
                case MovementTypes.Snap: return "Snap your fingers down onto your thumb";
                case MovementTypes.PushForward: return "Push forward from your chin";
                case MovementTypes.Crank: return "Crank more — 30° × 2";
                case MovementTypes.FlickOut: return "Flick faster, from your mouth outward";
                case MovementTypes.Shake: return "Shake faster — 4–6 times per second";
                case MovementTypes.Lift: return "Lift both hands up together";
                default: return "";
            }
        }

        private static string StartHint(SignTemplate t)
        {
            switch (t.movement.type)
            {
                case MovementTypes.PushForward: return "Start at your chin";
                case MovementTypes.FlickOut: return "Start at your mouth";
                case MovementTypes.Wave: return "Start beside your forehead";
                default: return "Start in front of your chest";
            }
        }

        // ---------------- layout + replay ----------------

        /// <summary>Where the non-dominant hand sits relative to the dominant one (signer's space, real-hand metres).</summary>
        private static Vector3 LeftOffset(SignTemplate t)
        {
            switch (t.movement.type)
            {
                case MovementTypes.Crank: return new Vector3(-0.03f, -0.10f, 0f);  // COFFEE: under the top fist
                case MovementTypes.Lift: return new Vector3(-0.03f, -0.09f, 0f);   // HELP: flat hand under the fist
                default: return new Vector3(-0.16f, 0f, 0f);                     // COLD: side by side
            }
        }

        private void LayoutHands(SignTemplate t, bool two)
        {
            float size = two ? twoHandSize : 1f;
            foreach (var g in new[] { targetHand, userHand, _targetLeft, _userLeft }) g.SizeMultiplier = size;
            _targetLeft.gameObject.SetActive(two);
            _userLeft.gameObject.SetActive(two);
            PlacePair(targetHand, _targetLeft, _targetPos, t, two);
            PlacePair(userHand, _userLeft, _userPos, t, two);
        }

        private static void PlacePair(GhostHand right, GhostHand left, Vector3 slot, SignTemplate t, bool two)
        {
            var rot = right.transform.localRotation;
            right.ClearMotion();
            left.ClearMotion();
            if (!two)
            {
                right.transform.localPosition = slot;
                return;
            }
            // Centre the pair on the slot; offsets are in the hand's own (signer) space, scaled like the drawing.
            Vector3 off = rot * (LeftOffset(t) * right.DrawScale);
            right.transform.localPosition = slot - off * 0.5f;
            left.transform.localPosition = slot + off * 0.5f;
            left.transform.localRotation = rot;
        }

        /// <summary>The target ghost replays the movement once at replaySpeed (§8.1), then returns to the start pose.</summary>
        private IEnumerator Replay(SignTemplate t)
        {
            if (t.movement.type == MovementTypes.None) yield break;
            yield return new WaitForSeconds(replayDelay);
            float dur = Mathf.Max(0.1f, t.movement.durationSec) / Mathf.Max(0.05f, replaySpeed);
            for (float time = 0f; time <= dur; time += Time.deltaTime)
            {
                ApplyReplay(t, time / dur);
                yield return null;
            }
            ApplyReplay(t, 1f);
            yield return new WaitForSeconds(0.8f);
            targetHand.ClearMotion();
            _targetLeft.ClearMotion();
            targetHand.Pose(t.curlRight);
            _replay = null;
        }

        /// <summary>Poses the target ghost(s) at movement phase u (also used for stills in tests).</summary>
        public void ApplyReplay(SignTemplate t, float u)
        {
            // Ghost local space is the signer's space; its fingers point +Y.
            var s = SignMotion.Sample(t, u, Vector3.up);
            targetHand.SetMotion(s.RightOffset * targetHand.DrawScale, s.RightRotation);
            if (s.RightCurl != null) targetHand.Pose(s.RightCurl);
            if (t.TwoHanded) _targetLeft.SetMotion(s.LeftOffset * _targetLeft.DrawScale, Quaternion.identity);
        }

        private void StopReplay()
        {
            if (_replay != null) StopCoroutine(_replay);
            _replay = null;
            if (targetHand != null) targetHand.ClearMotion();
            if (_targetLeft != null) _targetLeft.ClearMotion();
        }

        // ---------------- per-finger comparison ----------------

        /// <summary>Per-finger comparison and hint (SPEC v3 §8.2), both hands for two-handed signs. Public for tests.</summary>
        public static FingerReport Compare(RecognitionResult attempt, SignTemplate target)
        {
            var r = new FingerReport { Diff = new float[5], Colors = new Color[5], SecondWorst = -1 };
            var user = attempt.UserCurl ?? new float[5];
            for (int f = 0; f < 5; f++) r.Diff[f] = Mathf.Abs(user[f] - target.curlRight[f]);

            // Two-handed: compare the non-dominant hand too; the hint goes to whichever hand is further off.
            float[] userL = null;
            // (A left hand that was not seen is not finger-marked: the movement line says "use both hands".)
            if (target.TwoHanded && target.HasLeftCurl && attempt.UserCurlLeft != null && attempt.UserCurlLeft.Length == 5)
            {
                userL = attempt.UserCurlLeft;
                r.LeftDiff = new float[5];
                for (int f = 0; f < 5; f++) r.LeftDiff[f] = Mathf.Abs(userL[f] - target.curlLeft[f]);
                r.LeftWorse = Max(r.LeftDiff) > Max(r.Diff) + 0.05f;
            }

            float[] diff = r.LeftWorse ? r.LeftDiff : r.Diff;
            float[] mine = r.LeftWorse ? userL : user;
            float[] want = r.LeftWorse ? target.curlLeft : target.curlRight;

            r.Worst = PickWorst(diff, -1);
            int second = PickWorst(diff, r.Worst);
            if (diff[second] >= CloseDiff) r.SecondWorst = second;

            Color[] marked = new Color[5];
            for (int f = 0; f < 5; f++)
            {
                bool isMarked = f == r.Worst || f == r.SecondWorst;
                if (diff[f] < CloseDiff) marked[f] = Green;
                else if (!isMarked) marked[f] = Neutral; // only the 1–2 worst fingers are marked
                else marked[f] = diff[f] > WrongDiff ? Red : Yellow;
            }
            Color[] other = new Color[5];
            float[] otherDiff = r.LeftWorse ? r.Diff : r.LeftDiff;
            for (int f = 0; f < 5; f++) other[f] = otherDiff != null && otherDiff[f] < CloseDiff ? Green : Neutral;
            r.Colors = r.LeftWorse ? other : marked;
            r.LeftColors = r.LeftDiff == null ? null : r.LeftWorse ? marked : other;

            string who = target.TwoHanded ? (r.LeftWorse ? "Left hand — " : "Right hand — ") : "";
            if (diff[r.Worst] >= CloseDiff)
            {
                bool tooCurled = mine[r.Worst] > want[r.Worst];
                r.Hint = $"{who}{FingerNames[r.Worst]}: {(tooCurled ? "straighten more" : "curl more")}";
            }
            else if (attempt.UserPalm.sqrMagnitude > 1e-4f && Vector3.Angle(attempt.UserPalm, target.palm) > 50f)
            {
                // Fingers match but the palm faces the wrong way.
                r.Hint = $"Fingers look right — turn your palm to face {PalmWords(target.palmLabel)}";
            }
            else
            {
                // Hand shape is fine: the movement line below says what to fix.
                r.Hint = "<color=#4CD964>Hand shape looks right</color>";
            }

            if (attempt.Top1 != null && attempt.Top1.intent != target.intent)
            {
                r.LooksLike = $"Looks more like {attempt.Top1.intent} — check your {FingerNames[r.Worst].ToLowerInvariant()}{(r.Worst == 0 ? "" : " finger")}";
            }
            else r.LooksLike = "";
            return r;
        }

        private static float Max(float[] v)
        {
            float m = 0f;
            foreach (var x in v) m = Mathf.Max(m, x);
            return m;
        }

        // Fingers within this much of the worst count as tied; ties go to the fingers recognition weights most.
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
