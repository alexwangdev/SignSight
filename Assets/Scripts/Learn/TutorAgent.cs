using System;
using System.Collections.Generic;
using System.Text;
using SignSight.Hand;
using UnityEngine;

namespace SignSight.Learn
{
    /// <summary>
    /// Adaptive Tutor Agent (SPEC v3.1 §4): the teaching loop made adaptive. Rule-based, on-device, no network.
    ///   Perceive  - every scored attempt: word, attempts, first-try result, worst finger, movement error, total, time.
    ///   Remember  - per-word state in LearnerProfile (JSON in Application.persistentDataPath).
    ///   Decide    - which word comes next: (a) failed twice -> repeat now; (b) same finger red twice -> repeat with
    ///               only that finger highlighted; (c) not seen for 2+ lessons -> review; (d) first try twice in a
    ///               row -> shown less often (skipped this round); (e) otherwise the next word of the lesson.
    ///   Act       - the next word, plus a hint chosen by error type (never a generic "try again"), and the reason,
    ///               shown in plain text on the Tutor panel.
    /// </summary>
    public class TutorAgent : MonoBehaviour
    {
        public static TutorAgent Instance { get; private set; }

        [SerializeField] private LessonManager lessons;
        [SerializeField] private ScoreManager score;
        [Tooltip("Immediate repeats of the same word per lesson run (rule a), so a hard word cannot loop forever.")]
        [SerializeField] private int maxImmediateRepeats = 2;
        [Tooltip("Spaced-repetition reviews added per lesson run (rule c).")]
        [SerializeField] private int maxReviewsPerRun = 1;
        [Tooltip("A review is added after this many words of the run, not as the first word.")]
        [SerializeField] private int reviewAfterWords = 3;

        public const float RedDiff = FeedbackPanel.WrongDiff;   // a finger is red above this curl difference
        public const float CloseDiff = FeedbackPanel.CloseDiff;

        public enum Rule { None, RepeatFailed, FocusFinger, Review, Next }

        public struct Decision
        {
            public LessonManager.Word Word;
            public Rule Rule;
            public string Reason;     // "because ..." in plain words
            public int FocusFinger;   // -1 = none
            public bool Review;
        }

        public LearnerProfile Profile { get; private set; }
        public Decision? Current { get; private set; }
        public int DoneThisRun => _done.Count;
        public int RunSize => _lesson != null ? _lesson.words.Length : 0;
        public IReadOnlyList<string> Skipped => _skippedDemoted;
        /// <summary>Last personalised hints (for tests and the report).</summary>
        public string LastFingerHint { get; private set; } = "";
        public string LastLooksLike { get; private set; } = "";
        public string LastMovementHint { get; private set; } = "";

        public event Action Changed;

        private LessonManager.Lesson _lesson;
        private LessonManager.LessonFile _all;
        private readonly HashSet<string> _done = new HashSet<string>();
        private readonly Dictionary<string, int> _repeats = new Dictionary<string, int>();
        private readonly HashSet<string> _focusShown = new HashSet<string>();
        private readonly List<string> _skippedDemoted = new List<string>();
        private string _pendingRepeat;
        private int _reviews;
        private float _copyStart = float.NaN;

        private static readonly string[] Fingers = { "thumb", "index finger", "middle finger", "ring finger", "pinky" };
        private static readonly string[] FingerTitles = { "Thumb", "Index", "Middle", "Ring", "Pinky" };

        private void Awake()
        {
            Instance = this;
            Profile = LearnerProfile.Load();
        }

        private void OnEnable()
        {
            if (lessons == null) lessons = FindFirstObjectByType<LessonManager>();
            if (score == null) score = FindFirstObjectByType<ScoreManager>();
            if (score != null) score.Scored += OnScored;
            if (lessons != null) lessons.PhaseChanged += OnPhase;
        }

        private void OnDisable()
        {
            if (score != null) score.Scored -= OnScored;
            if (lessons != null) lessons.PhaseChanged -= OnPhase;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Forget everything (tests / "start over").</summary>
        public void ResetProfile()
        {
            Profile = new LearnerProfile();
            Profile.Save();
            Changed?.Invoke();
        }

        // ================= Decide =================

        public void BeginRun(LessonManager.Lesson lesson, LessonManager.LessonFile all)
        {
            _lesson = lesson;
            _all = all;
            _done.Clear();
            _repeats.Clear();
            _focusShown.Clear();
            _skippedDemoted.Clear();
            _pendingRepeat = null;
            _reviews = 0;
            Current = null;
            Profile.lessonsStarted++;
            // Rule d: words right on the first try twice in a row are not drilled again this round.
            foreach (var w in lesson.words)
            {
                var s = Profile.Find(w.intent);
                if (s != null && s.Demoted) _skippedDemoted.Add(w.intent);
            }
            Profile.Save();
        }

        /// <summary>The next word and why, or null when the lesson run is complete.</summary>
        public Decision? NextWord()
        {
            if (_lesson == null) return null;
            string last = Current?.Word?.intent;
            Decision? d = null;

            // (a) Failed twice in a row -> repeat it next, immediately.
            if (_pendingRepeat != null && Count(_repeats, _pendingRepeat) < maxImmediateRepeats)
            {
                var w = Find(_pendingRepeat);
                var s = Profile.Get(_pendingRepeat);
                int finger = s.RepeatedWorstFinger;
                _repeats[_pendingRepeat] = Count(_repeats, _pendingRepeat) + 1;
                d = new Decision
                {
                    Word = w, Rule = Rule.RepeatFailed, FocusFinger = finger,
                    Reason = "you missed it twice" + (finger >= 0 ? $" (your {Fingers[finger]} both times)" : ""),
                };
                _pendingRepeat = null;
            }

            // (b) Same finger red 2+ times, not right since -> repeat with only that finger highlighted.
            if (d == null)
            {
                foreach (var w in _lesson.words)
                {
                    if (w.intent == last || _focusShown.Contains(w.intent) || !_done.Contains(w.intent)) continue;
                    var s = Profile.Find(w.intent);
                    if (s == null || s.streak > 0) continue;
                    int finger = s.RepeatedWorstFinger;
                    if (finger < 0) continue;
                    _focusShown.Add(w.intent);
                    d = new Decision
                    {
                        Word = w, Rule = Rule.FocusFinger, FocusFinger = finger,
                        Reason = $"your {Fingers[finger]} was off twice",
                    };
                    break;
                }
            }

            // (c) Not seen for 2+ lessons -> spaced-repetition review (from any lesson).
            if (d == null && _reviews < maxReviewsPerRun && _done.Count >= reviewAfterWords && _all != null)
            {
                LessonManager.Word best = null;
                int bestAge = 1;
                foreach (var l in _all.lessons)
                {
                    foreach (var w in l.words)
                    {
                        if (Array.Exists(_lesson.words, x => x.intent == w.intent)) continue;
                        var s = Profile.Find(w.intent);
                        if (s == null || s.lastSeenLesson < 0) continue;
                        int age = Profile.lessonsStarted - s.lastSeenLesson;
                        if (age > bestAge)
                        {
                            bestAge = age;
                            best = w;
                        }
                    }
                }
                if (best != null)
                {
                    _reviews++;
                    d = new Decision
                    {
                        Word = best, Rule = Rule.Review, FocusFinger = -1, Review = true,
                        Reason = $"review — last practised {bestAge} lessons ago",
                    };
                }
            }

            // (d) + (e) Next word of the lesson in order, skipping the ones shown less often.
            if (d == null)
            {
                foreach (var w in _lesson.words)
                {
                    if (_done.Contains(w.intent) || _skippedDemoted.Contains(w.intent)) continue;
                    var s = Profile.Find(w.intent);
                    bool isNew = s == null || s.visits == 0;
                    d = new Decision
                    {
                        Word = w, Rule = Rule.Next, FocusFinger = -1,
                        Reason = isNew ? "next new word in this lesson" : "next word in this lesson",
                    };
                    break;
                }
            }

            Current = d;
            if (d.HasValue) Seen(d.Value.Word);
            Changed?.Invoke();
            return d;
        }

        private void Seen(LessonManager.Word w)
        {
            var s = Profile.Get(w.intent);
            s.visits++;
            s.lastSeenLesson = Profile.lessonsStarted;
            s.lastSeenUtc = DateTime.UtcNow.ToString("o");
            _done.Add(w.intent);
            Profile.Save();
        }

        private LessonManager.Word Find(string intent)
        {
            if (_lesson != null)
            {
                foreach (var w in _lesson.words) if (w.intent == intent) return w;
            }
            if (_all != null)
            {
                foreach (var l in _all.lessons)
                    foreach (var w in l.words)
                        if (w.intent == intent) return w;
            }
            return new LessonManager.Word { intent = intent, meaning = intent, cue = "" };
        }

        private static int Count(Dictionary<string, int> d, string k) => d.TryGetValue(k, out int n) ? n : 0;

        // ================= Perceive + Remember =================

        private void OnPhase(LessonManager.Phase p)
        {
            if (p == LessonManager.Phase.Copy && lessons.LastAttempt == null) _copyStart = Time.time;
        }

        /// <summary>A TEACH-ONLY word was practised (never scored).</summary>
        public void RecordPractised(LessonManager.Word w)
        {
            Profile.Get(w.intent).practised++;
            Profile.Save();
        }

        private void OnScored(ScoreManager.Verdict v)
        {
            var target = lessons.CurrentTemplate;
            if (target == null || v.Word == null) return;
            var s = Profile.Get(v.Word.intent);
            s.attempts++;
            s.lastTimeTaken = float.IsNaN(_copyStart) ? 0f : Time.time - _copyStart;
            _copyStart = Time.time;

            var a = v.Attempt;
            var e = a.For(target.intent);
            int worst = WorstFinger(e, target, out bool leftHand, out float mine, out float want, out float diff);

            switch (v.Outcome)
            {
                case ScoreManager.Outcome.CorrectFirstTry:
                    s.correctFirstTry++;
                    s.consecutiveFirstTry++;
                    s.streak++;
                    s.lastVisitFailedTwice = false;
                    break;
                case ScoreManager.Outcome.CorrectSecondTry:
                    s.correctSecondTry++;
                    s.consecutiveFirstTry = 0;
                    s.streak++;
                    s.lastVisitFailedTwice = false;
                    break;
                default:
                    s.consecutiveFirstTry = 0;
                    s.streak = 0;
                    if (worst >= 0 && diff > RedDiff) s.worstFingerHistory.Add(worst);
                    string mv = e.HasValue && !string.IsNullOrEmpty(e.Value.Hint) ? e.Value.Hint : "";
                    s.movementErrorHistory.Add(mv);
                    if (v.Outcome == ScoreManager.Outcome.WrongMovedOn)
                    {
                        s.wrongVisits++;
                        s.lastVisitFailedTwice = true;
                        _pendingRepeat = v.Word.intent;
                    }
                    break;
            }
            Profile.Save();
            Changed?.Invoke();
        }

        /// <summary>Worst finger of the attempt against the target's start curl (the hand that is further off).</summary>
        private int WorstFinger(SignEval? e, SignTemplate t, out bool left, out float mine, out float want, out float diff)
        {
            left = false;
            mine = want = diff = 0f;
            if (!e.HasValue || e.Value.CurlRight == null || !e.Value.HasRight) return -1;
            int best = -1;
            for (int f = 0; f < 5; f++)
            {
                float d = Mathf.Abs(e.Value.CurlRight[f] - t.curlRight[f]);
                if (d > diff + 0.05f || best < 0) { best = f; diff = d; mine = e.Value.CurlRight[f]; want = t.curlRight[f]; }
            }
            if (t.TwoHanded && t.HasLeftCurl && e.Value.HasLeft && e.Value.CurlLeft != null)
            {
                for (int f = 0; f < 5; f++)
                {
                    float d = Mathf.Abs(e.Value.CurlLeft[f] - t.curlLeft[f]);
                    if (d > diff + 0.05f) { best = f; diff = d; mine = e.Value.CurlLeft[f]; want = t.curlLeft[f]; left = true; }
                }
            }
            return best;
        }

        // ================= Act: hints =================

        /// <summary>
        /// Rewrites the feedback hints for this learner (called by FeedbackPanel for a wrong attempt): a named finger
        /// with numbers, the word it looked like and the feature that tells them apart, and a movement hint.
        /// Also limits the finger marks to the focus finger when the agent asked for one (rule b).
        /// </summary>
        public void Personalise(ref FeedbackPanel.FingerReport report, RecognitionResult attempt, SignTemplate target, ref string movementHint)
        {
            int focus = Current.HasValue && Current.Value.Word != null && Current.Value.Word.intent == target.intent ? Current.Value.FocusFinger : -1;
            float[] diff = report.LeftWorse ? report.LeftDiff : report.Diff;
            float[] mine = report.LeftWorse ? attempt.UserCurlLeft : attempt.UserCurl;
            float[] want = report.LeftWorse ? target.curlLeft : target.curlRight;
            int f = focus >= 0 ? focus : report.Worst;

            if (focus >= 0 && report.Colors != null)
            {
                // Only the finger the agent is working on is marked; the rest are plain.
                for (int i = 0; i < 5; i++)
                {
                    if (i == focus) continue;
                    report.Colors[i] = FeedbackPanel.Neutral;
                    if (report.LeftColors != null) report.LeftColors[i] = FeedbackPanel.Neutral;
                }
            }

            string who = target.TwoHanded ? (report.LeftWorse ? "Left hand — " : "Right hand — ") : "";
            if (diff != null && mine != null && want != null && f >= 0 && diff[f] >= CloseDiff)
            {
                bool tooCurled = mine[f] > want[f];
                report.Hint = $"{who}{FingerTitles[f]}: {(tooCurled ? "straighten more" : "bend more")} — yours is {Pct(mine[f])} bent, target is {Pct(want[f])}";
            }
            else if (focus >= 0 && diff != null)
            {
                report.Hint = $"{FingerTitles[focus]}: <color=#4CD964>right this time</color> — keep it like that";
            }
            LastFingerHint = report.Hint ?? "";

            if (attempt.Top1 != null && attempt.Top1.intent != target.intent)
                report.LooksLike = $"Looks more like {attempt.Top1.intent} — check {Distinguish(target, attempt.Top1)}";
            LastLooksLike = report.LooksLike ?? "";

            movementHint = MovementHint(attempt, target, movementHint);
            LastMovementHint = movementHint ?? "";
        }

        private static string Pct(float curl) => $"{Mathf.RoundToInt(Mathf.Clamp01(curl) * 100f)}%";

        private static string MovementHint(RecognitionResult a, SignTemplate t, string fallback)
        {
            var e = a.For(t.intent);
            bool done = e.HasValue && e.Value.Completed;
            if (done) return fallback;
            switch (t.movement.type)
            {
                case MovementTypes.FlickOut:
                    return "Flick faster — out and down in under 0.7 s";
                case MovementTypes.Crank:
                    if (e.HasValue && !string.IsNullOrEmpty(e.Value.Hint)) return e.Value.Hint; // "Bottom hand: keep it still — it moved 9 cm"
                    return "Bottom hand: keep it still — only the top fist turns";
            }
            if (e.HasValue && !string.IsNullOrEmpty(e.Value.Hint)) return e.Value.Hint;
            return fallback;
        }

        /// <summary>The feature that tells the target apart from the sign it was mistaken for.</summary>
        public static string Distinguish(SignTemplate target, SignTemplate other)
        {
            if (target.TwoHanded != other.TwoHanded)
                return target.TwoHanded ? $"both hands — {target.intent} uses two" : $"one hand only — {target.intent} uses one";
            if (!string.IsNullOrEmpty(target.anchor) && !string.IsNullOrEmpty(other.anchor) && target.anchor != other.anchor)
                return $"where it starts — your {SignScorer.AnchorWord(target.anchor)}, not your {SignScorer.AnchorWord(other.anchor)}";
            int f = -1;
            float best = 0.3f;
            for (int i = 0; i < 5; i++)
            {
                float d = Mathf.Abs(target.curlRight[i] - other.curlRight[i]);
                if (d > best) { best = d; f = i; }
            }
            if (f >= 0)
                return $"your {Fingers[f]} — {(target.curlRight[f] < other.curlRight[f] ? "straight" : "bent")} in {target.intent}";
            if (target.movement.type != other.movement.type)
                return $"the movement — {MovementWords(target)}";
            if (target.endPalm.sqrMagnitude > 1e-4f)
                return $"the end — {target.intent} ends with the palm {PalmWords(target.endPalm)}";
            if (Vector3.Angle(target.palm, other.palm) > 45f)
                return $"your palm — it faces {PalmWords(target.palm)} in {target.intent}";
            return $"the movement — {MovementWords(target)}";
        }

        private static string PalmWords(Vector3 p)
        {
            float ax = Mathf.Abs(p.x), ay = Mathf.Abs(p.y), az = Mathf.Abs(p.z);
            if (ay >= ax && ay >= az) return p.y < 0f ? "down" : "up";
            if (az >= ax) return p.z > 0f ? "forward" : "toward you";
            return "sideways";
        }

        public static string MovementWords(SignTemplate t)
        {
            var m = t.movement;
            string times = m.repeats >= 2 ? " twice" : "";
            switch (m.type)
            {
                case MovementTypes.Wave: return "wave side to side" + times;
                case MovementTypes.WristNod: return "nod at the wrist" + times;
                case MovementTypes.Snap: return "snap the fingers onto the thumb";
                case MovementTypes.PushForward: return "push forward from the chin";
                case MovementTypes.Crank: return "the top fist cranks, the bottom stays still";
                case MovementTypes.FlickOut: return "flick out and down, fast";
                case MovementTypes.Shake: return "shiver side to side, fast";
                case MovementTypes.Lift: return "lift both hands together";
                case MovementTypes.Tap: return $"tap{times}";
                case MovementTypes.Stroke: return $"move {(t.direction.y < -0.5f ? "down" : t.direction.z > 0.5f ? "forward" : t.direction.z < -0.5f ? "back toward you" : "across")}";
                case MovementTypes.Circle: return "circle" + times;
                case MovementTypes.Twist: return "twist back and forth";
                case MovementTypes.Turn: return "turn the hand once";
                case MovementTypes.Squeeze: return "close and open" + times;
                case MovementTypes.Converge: return "bring the hands together" + times;
                case MovementTypes.Hold: return "cross and hold";
                case MovementTypes.Rock: return "rock side to side";
                default: return m.type;
            }
        }

        // ================= Panel =================

        /// <summary>Plain-text reasoning for the Tutor panel (the agent's work, visible to the learner).</summary>
        public string PanelText()
        {
            if (!Current.HasValue || Current.Value.Word == null) return "";
            var d = Current.Value;
            var sb = new StringBuilder();
            sb.Append("<b><color=#9DB7FF>Tutor</color></b>\n");
            sb.Append($"Next: <b>{d.Word.intent}</b> — because {d.Reason}");
            if (d.FocusFinger >= 0) sb.Append($"\nFocus: your {Fingers[d.FocusFinger]}");
            if (_skippedDemoted.Count > 0)
                sb.Append($"\n<size=80%><color=#AAAAAA>Shown less often (right first try twice): {string.Join(", ", _skippedDemoted)}</color></size>");
            return sb.ToString();
        }
    }
}
