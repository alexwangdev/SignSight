using System;
using System.Collections.Generic;
using UnityEngine;

namespace SignSight.Hand
{
    /// <summary>One recognition event (SPEC v3 §4.6 step 4). Confidences are 0..1; Gap = Top1 - Top2 confidence.</summary>
    public struct RecognitionResult
    {
        public SignTemplate Top1;
        public SignTemplate Top2;
        public float Confidence;
        public float Top2Confidence;
        public float Gap => Confidence - Top2Confidence;
        /// <summary>Total distance to Top1: 0.4 handshape + 0.2 location + 0.4 movement (0 = identical).</summary>
        public float Distance;
        public float HandshapeDistance, LocationDistance, MovementDistance;
        /// <summary>Top1's movement was performed inside the attempt window.</summary>
        public bool MovementCompleted;
        /// <summary>Distance is within Top1's tolerance.</summary>
        public bool WithinTolerance;
        /// <summary>What was wrong with the movement (SPEC v3 §8.3), e.g. "Bottom hand: keep it still"; empty when fine.</summary>
        public string MovementError;
        /// <summary>The learner's start-pose curl[5] (dominant hand) and palm direction (head-yaw frame), for §8 feedback.</summary>
        public float[] UserCurl;
        public Vector3 UserPalm;
        /// <summary>Non-dominant start-pose curl[5] (two-handed signs).</summary>
        public float[] UserCurlLeft;
        /// <summary>Every template's score for this attempt, best first — so feedback can explain the lesson target
        /// (e.g. COFFEE: "Bottom hand: keep it still") even when Top1 is another word.</summary>
        public SignEval[] All;

        /// <summary>This attempt scored against one template (null if it is not in the store).</summary>
        public SignEval? For(string intent)
        {
            if (All == null) return null;
            foreach (var e in All)
            {
                if (e.Template != null && e.Template.intent == intent) return e;
            }
            return null;
        }

        public override string ToString() =>
            $"{Top1?.intent} {Confidence:0.00} (dist {Distance:0.00} = H{HandshapeDistance:0.00} L{LocationDistance:0.00} M{MovementDistance:0.00}" +
            $"{(MovementCompleted ? "" : ", no movement")}{(WithinTolerance ? "" : ", outside tolerance")}" +
            $"{(string.IsNullOrEmpty(MovementError) ? "" : $", \"{MovementError}\"")}) / {Top2?.intent} {Top2Confidence:0.00}";
    }

    /// <summary>
    /// Trajectory recognition (SPEC v3 §4.6). Both hands are buffered every frame; each frame every template is scored
    /// on handshape (0.4) + start location (0.2) + movement (0.4). An attempt window opens when a hand comes up into
    /// signing space and lasts 3 s. When a movement is completed inside the window, the recognizer waits for the
    /// hand to settle (so a longer movement is not cut short by a shorter one it contains), then fires once with
    /// Top1 / Top2, followed by a 1 s lockout. Nothing fires without a completed movement; the closest candidate is only
    /// shown as preview. Head and face are never scored.
    /// </summary>
    public class SignRecognizer : MonoBehaviour
    {
        [SerializeField] private XRHandsProvider handProvider;
        [Tooltip("Recognition only runs while this is on (e.g. during the 'copy it' step of a lesson).")]
        [SerializeField] private bool listening = true;
        [Tooltip("Off (teaching): any completed movement fires with its nearest template, so a wrong attempt still gets feedback. " +
                 "On: only attempts within the template's tolerance fire.")]
        [SerializeField] private bool onlyFireWithinTolerance = false;

        [Header("Timing (SPEC v3 §4.6)")]
        [SerializeField] private float attemptWindowSeconds = 3f;
        [SerializeField] private float lockoutSeconds = 1f;
        [Tooltip("After a movement completes, wait at least this long (and until the hand is still) before firing.")]
        [SerializeField] private float settleMinSeconds = 0.25f;
        [SerializeField] private float settleMaxSeconds = 1.0f;
        [SerializeField] private float stillSpeed = 0.15f; // m/s
        [SerializeField] private float bufferSeconds = 3f;
        [Tooltip("After a fire, the hands must pause this long (or drop) before the next attempt can start.")]
        [SerializeField] private float rearmStillSeconds = 0.3f;
        [Tooltip("Editor / Simulator only: how much webcam depth (z) counts in stillness and location checks. Device uses 1.")]
        [SerializeField] private float editorDepthWeight = 0.4f;
        [Tooltip("Editor / Simulator only: smoothing window (s) for the COFFEE bottom-hand stillness check. Device uses none.")]
        [SerializeField] private float editorSmoothSeconds = 0.3f;
        [Tooltip("Editor / Simulator only: extra metres allowed on top of the spec 4 cm bottom-hand limit (webcam jitter). Device uses 0.")]
        [SerializeField] private float editorStillAllowance = 0.10f;

        [Header("UI pauses")]
        [Tooltip("Recognition pauses while a hand ray points at a button, so reaching to pinch is not read as a sign.")]
        [SerializeField] private bool pauseWhilePointingAtUi = true;

        /// <summary>Fired once per completed attempt.</summary>
        public event Action<RecognitionResult> Recognized;
        /// <summary>Closest candidate (null when none): for small preview text only, never scored.</summary>
        public event Action<TemplateScore?> PreviewChanged;

        public TemplateStore Store { get; private set; }
        public TemplateScore? Preview { get; private set; }
        /// <summary>Movement hint for the preview candidate, e.g. "Wave it — side to side, twice" (never scored).</summary>
        public string PreviewHint { get; private set; } = "";
        public TrajectoryTracker Tracker => _tracker;
        public IReadOnlyList<SignEval> LastEvals => _evals;
        public bool InLockout(float now) => now < _lockoutUntil;

        private IHandProvider _provider;
        private TrajectoryTracker _tracker;
        private readonly List<SignEval> _evals = new List<SignEval>();
        private float _windowStart = float.NaN;
        private float _pendingSince = float.NaN;
        private float _lastFireTime = float.NegativeInfinity;
        private float _lockoutUntil = float.NegativeInfinity;
        private float _pausedUntil = float.NegativeInfinity;
        private bool _armed = true;
        private float _stillSince = float.NaN;
        private Oculus.Interaction.RayInteractor[] _rays;

        /// <summary>0..1 through the 3 s attempt window (for the blue progress ring, SPEC v3 §6). 0 when no window is open.</summary>
        public float HoldProgress
        {
            get
            {
                if (!listening || float.IsNaN(_windowStart) || Time.time < _lockoutUntil) return 0f;
                return Mathf.Clamp01((Time.time - _windowStart) / attemptWindowSeconds);
            }
        }

        public bool OnlyFireWithinTolerance
        {
            get => onlyFireWithinTolerance;
            set => onlyFireWithinTolerance = value;
        }

        public bool PauseWhilePointingAtUi
        {
            get => pauseWhilePointingAtUi;
            set => pauseWhilePointingAtUi = value;
        }

        /// <summary>Turns recognition on or off. Either way the buffer and any open attempt are cleared.</summary>
        public bool Listening
        {
            get => listening;
            set
            {
                if (listening == value) return;
                listening = value;
                ResetAttempt();
            }
        }

        private void Awake()
        {
            SignScorer.ConfigureForPlatform(editorDepthWeight, editorSmoothSeconds, editorStillAllowance);
            EnsureStore();
            _tracker = new TrajectoryTracker(bufferSeconds);
            if (handProvider == null) handProvider = FindFirstObjectByType<XRHandsProvider>();
            if (_provider == null) _provider = handProvider;
        }

        /// <summary>Uses another hand source (e.g. the teacher avatar for self-tests). Null restores the XR hands.</summary>
        public void SetProvider(IHandProvider provider)
        {
            _provider = provider ?? handProvider;
            ResetAttempt();
        }

        public void PauseFor(float seconds) => _pausedUntil = Mathf.Max(_pausedUntil, Time.time + seconds);
        public void ResumeAfter(float seconds) => _pausedUntil = Time.time + seconds;

        public void ReloadTemplates() => Store = TemplateStore.Load();
        public void UseStore(TemplateStore store) => Store = store;

        private void EnsureStore()
        {
            if (Store == null) Store = TemplateStore.Load();
        }

        private bool IsPaused()
        {
            if (Time.time < _pausedUntil) return true;
            if (!pauseWhilePointingAtUi) return false;
            if (_rays == null || _rays.Length == 0) _rays = FindObjectsByType<Oculus.Interaction.RayInteractor>(FindObjectsSortMode.None);
            foreach (var r in _rays)
            {
                if (r != null && r.isActiveAndEnabled && r.Candidate != null) return true;
            }
            return false;
        }

        private void Update()
        {
            if (!listening)
            {
                SetPreview(null, "");
                return;
            }
            if (IsPaused())
            {
                // Reaching for a button is not a sign: drop what was buffered.
                ResetAttempt();
                SetPreview(null, "");
                return;
            }
            if (_tracker == null) _tracker = new TrajectoryTracker(bufferSeconds);
            _tracker.Push(_provider, Time.time);
            Process(Time.time);
        }

        /// <summary>One recognition step over the current buffer. Public so it can be driven with synthetic frames.</summary>
        public void Process(float now)
        {
            EnsureStore();
            var frames = _tracker.Frames;
            if (frames.Count == 0) return;
            var latest = frames[frames.Count - 1];

            // After a fire, re-arm only once the hands have paused (or dropped), and judge only what comes after that:
            // the tail of the movement that just fired must not count as a new attempt.
            bool handUp = SignScorer.InSigningSpace(latest.Right) || SignScorer.InSigningSpace(latest.Left);
            if (HandStill(frames)) { if (float.IsNaN(_stillSince)) _stillSince = now; }
            else _stillSince = float.NaN;
            if (!_armed && now >= _lockoutUntil && (!handUp || !float.IsNaN(_stillSince) && now - _stillSince >= rearmStillSeconds))
            {
                _armed = true;
                _lastFireTime = now;
            }

            // Attempt window: opens when a hand is up in signing space, lasts attemptWindowSeconds.
            if (float.IsNaN(_windowStart))
            {
                if (handUp && _armed && now >= _lockoutUntil) _windowStart = now;
            }
            else if (float.IsNaN(_pendingSince) && now - _windowStart > attemptWindowSeconds)
            {
                _windowStart = handUp ? now : float.NaN; // no movement in time: no fire, start a new window
            }

            // Score every template on the frames since the last fire.
            int from = 0;
            while (from < frames.Count && frames[from].Time <= _lastFireTime) from++;
            _evals.Clear();
            if (frames.Count - from < 3)
            {
                SetPreview(null, "");
                return;
            }
            foreach (var t in Store.Templates) _evals.Add(SignScorer.Evaluate(t, frames, from));
            _evals.Sort((a, b) => a.Total.CompareTo(b.Total));
            SetPreview(new TemplateScore { Template = _evals[0].Template, Distance = _evals[0].Total }, _evals[0].Hint);

            if (now < _lockoutUntil || float.IsNaN(_windowStart)) return;

            // A movement completed inside the window: start settling.
            if (float.IsNaN(_pendingSince))
            {
                foreach (var e in _evals)
                {
                    if (e.Performed && e.CompletedTime >= _windowStart)
                    {
                        _pendingSince = now;
                        break;
                    }
                }
                if (float.IsNaN(_pendingSince)) return;
            }

            float waited = now - _pendingSince;
            if (waited < settleMinSeconds) return;
            if (waited < settleMaxSeconds && !HandStill(frames)) return;
            Fire(now);
        }

        private bool HandStill(IReadOnlyList<TrajFrame> f)
        {
            var b = f[f.Count - 1];
            for (int i = f.Count - 2; i >= 0; i--)
            {
                if (b.Time - f[i].Time < 0.15f) continue;
                var a = f[i];
                float dt = Mathf.Max(1e-3f, b.Time - a.Time);
                float vr = a.Right.Tracked && b.Right.Tracked ? Vector3.Distance(a.Right.Palm, b.Right.Palm) / dt : 0f;
                float vl = a.Left.Tracked && b.Left.Tracked ? Vector3.Distance(a.Left.Palm, b.Left.Palm) / dt : 0f;
                return Mathf.Max(vr, vl) < stillSpeed;
            }
            return false;
        }

        private void Fire(float now)
        {
            var top1 = _evals[0];
            var top2 = _evals.Count > 1 ? _evals[1] : default;
            bool within = top1.Total <= top1.Template.tolerance;

            _pendingSince = float.NaN;
            _windowStart = float.NaN;
            _lastFireTime = now;
            _lockoutUntil = now + lockoutSeconds;
            _armed = false;
            if (onlyFireWithinTolerance && !(within && top1.Completed)) return;

            var result = new RecognitionResult
            {
                Top1 = top1.Template,
                Top2 = top2.Template,
                Confidence = top1.Confidence,
                Top2Confidence = top2.Template != null ? top2.Confidence : 0f,
                Distance = top1.Total,
                HandshapeDistance = top1.Handshape,
                LocationDistance = top1.Location,
                MovementDistance = top1.Movement,
                MovementCompleted = top1.Completed,
                WithinTolerance = within,
                MovementError = top1.Hint ?? "",
                UserCurl = (float[])top1.CurlRight.Clone(),
                UserPalm = top1.PalmRight,
                UserCurlLeft = (float[])top1.CurlLeft.Clone(),
                All = _evals.ToArray(),
            };
            Recognized?.Invoke(result);
        }

        /// <summary>
        /// Scores the buffer against one specific template (e.g. the lesson target) — used for feedback on what the
        /// learner did relative to the word they were asked for. Null when nothing is buffered.
        /// </summary>
        public SignEval? EvaluateAgainst(SignTemplate t)
        {
            foreach (var e in _evals)
            {
                if (e.Template == t) return e;
            }
            return null;
        }

        private void ResetAttempt()
        {
            _tracker?.Clear();
            _evals.Clear();
            _windowStart = float.NaN;
            _pendingSince = float.NaN;
            _lastFireTime = float.NegativeInfinity;
            _armed = true;
            _stillSince = float.NaN;
        }

        private void SetPreview(TemplateScore? score, string hint)
        {
            bool changed = Preview.HasValue != score.HasValue
                           || (score.HasValue && Preview.HasValue && Preview.Value.Template != score.Value.Template);
            Preview = score;
            PreviewHint = hint ?? "";
            if (changed) PreviewChanged?.Invoke(score);
        }
    }
}
