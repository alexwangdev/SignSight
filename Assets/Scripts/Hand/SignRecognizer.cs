using System;
using System.Collections.Generic;
using UnityEngine;

namespace SignSight.Hand
{
    /// <summary>One recognition event (SPEC §4.5). Confidences are 0..1; Gap = Top1 - Top2 confidence.</summary>
    public struct RecognitionResult
    {
        public SignTemplate Top1;
        public SignTemplate Top2;
        public float Confidence;
        public float Top2Confidence;
        public float Gap => Confidence - Top2Confidence;
        /// <summary>Normalised distance from the held hand to Top1 (0 = identical).</summary>
        public float Distance;
        /// <summary>Distance is within Top1's tolerance.</summary>
        public bool WithinTolerance;
        /// <summary>The learner's smoothed curl[5] at the moment of the event (SPEC §4.5, used by §8 feedback).</summary>
        public float[] UserCurl;
        /// <summary>The learner's palm direction (head-yaw frame) at the moment of the event.</summary>
        public Vector3 UserPalm;

        public override string ToString() =>
            $"{Top1?.intent} {Confidence:0.00} (dist {Distance:0.00}{(WithinTolerance ? "" : ", outside tolerance")}) / {Top2?.intent} {Top2Confidence:0.00}";
    }

    /// <summary>
    /// Distance matching + 0.6 s hold + 1 s lockout + Top2 output + 5-frame smoothing (SPEC §4.5).
    /// A sign fires once when its template stays the closest match, within tolerance, for holdSeconds.
    /// After firing, nothing fires during the lockout, and the same sign cannot fire again until the hand
    /// changes shape or drops (so holding one sign never repeats it).
    /// </summary>
    public class SignRecognizer : MonoBehaviour
    {
        [SerializeField] private XRHandsProvider handProvider;
        [SerializeField] private HandSide signingHand = HandSide.Right;
        [Tooltip("Recognition only runs while this is on (e.g. during the 'copy it' step of a lesson).")]
        [SerializeField] private bool listening = true;
        [Tooltip("Off (teaching): any held shape fires with its nearest template, so a wrong attempt still gets feedback. " +
                 "On: only shapes within a template's tolerance fire.")]
        [SerializeField] private bool onlyFireWithinTolerance = false;

        [Header("Timing (SPEC §4.5)")]
        [SerializeField] private float holdSeconds = 0.6f;
        [SerializeField] private float lockoutSeconds = 1f;
        [SerializeField] private float trackingLossClearSeconds = 0.5f;
        [SerializeField] private int smoothingFrames = 5;
        [Tooltip("Seconds the hand must show something else before the same sign can fire again.")]
        [SerializeField] private float rearmSeconds = 0.3f;

        [Header("UI pauses")]
        [Tooltip("Recognition pauses while a hand ray points at a button, so reaching to pinch is not read as a sign.")]
        [SerializeField] private bool pauseWhilePointingAtUi = true;

        /// <summary>Fired once per held sign.</summary>
        public event Action<RecognitionResult> Recognized;
        /// <summary>Closest candidate every frame (null when none): for small preview text only, never spoken.</summary>
        public event Action<TemplateScore?> PreviewChanged;

        public TemplateStore Store { get; private set; }
        public TemplateScore? Preview { get; private set; }
        public bool InLockout(float now) => now < _lockoutUntil;

        private readonly Queue<float[]> _curlHistory = new Queue<float[]>();
        private readonly Queue<Vector3> _palmHistory = new Queue<Vector3>();
        private string _candidate;
        private float _candidateSince;
        private float _lastTrackedTime = float.NegativeInfinity;
        private float _lockoutUntil = float.NegativeInfinity;
        private string _lastFired;   // cannot fire again until re-armed
        private bool _rearmed = true;
        private float _differentSince = float.NaN;

        private float _pausedUntil = float.NegativeInfinity;
        private Oculus.Interaction.RayInteractor[] _rays;

        /// <summary>
        /// 0..1 progress of the current hold toward firing (for the blue hold ring, SPEC §6). 0 when nothing is
        /// building up: not listening, no candidate, in lockout, or the same sign is still being held after firing.
        /// </summary>
        public float HoldProgress
        {
            get
            {
                float now = Time.time;
                if (!listening || _candidate == null || now < _lockoutUntil) return 0f;
                if (_candidate == _lastFired && !_rearmed) return 0f;
                return Mathf.Clamp01((now - _candidateSince) / holdSeconds);
            }
        }

        /// <summary>See the inspector tooltip: whether only shapes within tolerance fire.</summary>
        public bool OnlyFireWithinTolerance
        {
            get => onlyFireWithinTolerance;
            set => onlyFireWithinTolerance = value;
        }

        /// <summary>Turns recognition on or off. Turning it off clears the current candidate.</summary>
        public bool Listening
        {
            get => listening;
            set
            {
                if (listening == value) return;
                listening = value;
                if (!value) ClearCandidate();
            }
        }

        private void Awake()
        {
            EnsureStore();
            if (handProvider == null) handProvider = FindFirstObjectByType<XRHandsProvider>();
        }

        /// <summary>Stops recognition for a while (e.g. right after a button press).</summary>
        public void PauseFor(float seconds) => _pausedUntil = Mathf.Max(_pausedUntil, Time.time + seconds);

        /// <summary>Replaces any current pause with one that ends after this many seconds.</summary>
        public void ResumeAfter(float seconds) => _pausedUntil = Time.time + seconds;

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

        /// <summary>Reloads templates.json, e.g. after recording new templates.</summary>
        public void ReloadTemplates() => Store = TemplateStore.Load();

        public void UseStore(TemplateStore store) => Store = store;

        private void EnsureStore()
        {
            if (Store == null) Store = TemplateStore.Load();
        }

        private void Update()
        {
            if (!listening)
            {
                SetPreview(null);
                return;
            }
            if (IsPaused())
            {
                // Freeze, do not clear: the sign that just fired must not fire again when the pause ends
                // while the hand is still holding it.
                SetPreview(null);
                _lastTrackedTime = Time.time;
                return;
            }
            Process(ReadHand(), Time.time);
        }

        private HandFeatures? ReadHand()
        {
            if (handProvider == null) return null;
            var other = signingHand == HandSide.Right ? HandSide.Left : HandSide.Right;
            if (!handProvider.TryGetHand(signingHand, out var frame) && !handProvider.TryGetHand(other, out frame)) return null;
            return CurlCalculator.Compute(frame, handProvider.Head);
        }

        /// <summary>
        /// One recognition step. Public so the timing rules can be tested with synthetic input.
        /// Pass null when no hand is tracked.
        /// </summary>
        public void Process(HandFeatures? features, float now)
        {
            EnsureStore();

            if (features == null)
            {
                if (now - _lastTrackedTime > trackingLossClearSeconds) ClearCandidate();
                return;
            }
            _lastTrackedTime = now;

            Smooth(features.Value, out var curl, out var palm);
            var ranked = Store.Rank(curl, palm);
            var top1 = ranked[0];
            var top2 = ranked.Count > 1 ? ranked[1] : default;
            SetPreview(top1);

            // v2.0 (SPEC §4.5 / §7): any held, stable shape fires with its nearest template; scoring decides
            // correct vs wrong from the distance. Optionally only fire inside the tolerance (v1 behaviour).
            string match = (!onlyFireWithinTolerance || top1.WithinTolerance) ? top1.Template.intent : null;

            // Re-arm once the hand has stopped showing the sign that last fired for rearmSeconds (ignores jitter).
            if (match == _lastFired)
            {
                _differentSince = float.NaN;
            }
            else
            {
                if (float.IsNaN(_differentSince)) _differentSince = now;
                if (now - _differentSince >= rearmSeconds) _rearmed = true;
            }

            if (match != _candidate)
            {
                _candidate = match;
                _candidateSince = now;
            }

            if (match == null || now < _lockoutUntil) return;
            if (match == _lastFired && !_rearmed) return;
            if (now - _candidateSince < holdSeconds) return;

            var result = new RecognitionResult
            {
                Top1 = top1.Template,
                Top2 = top2.Template,
                Confidence = top1.Confidence,
                Top2Confidence = top2.Template != null ? top2.Confidence : 0f,
                Distance = top1.Distance,
                WithinTolerance = top1.WithinTolerance,
                UserCurl = (float[])curl.Clone(),
                UserPalm = palm,
            };
            _lockoutUntil = now + lockoutSeconds;
            _lastFired = match;
            _rearmed = false;
            Recognized?.Invoke(result);
        }

        private void Smooth(HandFeatures f, out float[] curl, out Vector3 palm)
        {
            _curlHistory.Enqueue((float[])f.Curl.Clone());
            _palmHistory.Enqueue(f.PalmNormal);
            while (_curlHistory.Count > smoothingFrames) _curlHistory.Dequeue();
            while (_palmHistory.Count > smoothingFrames) _palmHistory.Dequeue();

            curl = new float[5];
            foreach (var c in _curlHistory)
            {
                for (int i = 0; i < 5; i++) curl[i] += c[i] / _curlHistory.Count;
            }
            palm = Vector3.zero;
            foreach (var p in _palmHistory) palm += p;
            palm = palm.sqrMagnitude > 1e-6f ? palm.normalized : f.PalmNormal;
        }

        private void ClearCandidate()
        {
            _curlHistory.Clear();
            _palmHistory.Clear();
            _candidate = null;
            _rearmed = true; // hand dropped: the same sign may be signed again
            _lastFired = null;
            _differentSince = float.NaN;
            SetPreview(null);
        }

        private void SetPreview(TemplateScore? score)
        {
            bool changed = Preview.HasValue != score.HasValue
                           || (score.HasValue && Preview.HasValue && Preview.Value.Template != score.Value.Template);
            Preview = score;
            if (changed) PreviewChanged?.Invoke(score);
        }
    }
}
