using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace SignSight.Hand
{
    /// <summary>
    /// Template Recorder v2 (SPEC v3 §4.5, §5.3): pick a word, (1) hold its start pose still for 1 s, then (2) perform
    /// the full movement once. The start pose (both hands for two-handed signs) and the measured movement amplitude
    /// are saved as one recording; up to 3 recordings per word are averaged into the template. While idle, the live
    /// readout runs the same trajectory scoring as the lessons, so a recording can be tested straight away.
    /// </summary>
    public class TemplateRecorder : MonoBehaviour
    {
        private enum State { Idle, GetReady, Holding, Performing }

        [SerializeField] private XRHandsProvider handProvider;
        [Tooltip("Hand used for signing. Falls back to the other hand when this one is not tracked.")]
        [SerializeField] private HandSide signingHand = HandSide.Right;

        [Header("Timing")]
        [Tooltip("Seconds the start pose must be held still to be recorded.")]
        [SerializeField] private float holdSeconds = 1f;
        [Tooltip("Seconds after pressing Record before the hold starts counting (time to form the sign).")]
        [SerializeField] private float getReadySeconds = 1.5f;
        [Tooltip("Seconds without tracking before the hold is reset.")]
        [SerializeField] private float trackingLossReset = 0.5f;
        [Tooltip("Seconds allowed to perform the movement once after the start pose is captured.")]
        [SerializeField] private float performSeconds = 4f;

        [Header("Stillness")]
        [SerializeField] private float maxCurlDeviation = 0.12f;
        [SerializeField] private float maxPalmDeviationDegrees = 25f;

        [Header("UI")]
        [SerializeField] private TMP_Text intentText;
        [SerializeField] private TMP_Text promptText;
        [SerializeField] private TMP_Text liveText;

        private struct Frame { public float Time; public float[] Curl; public Vector3 Palm; public float[] CurlLeft; }

        private TemplateStore _store;
        private readonly TrajectoryTracker _tracker = new TrajectoryTracker(3f);
        private int _intentIndex;
        private State _state = State.Idle;
        private float _stateStart;
        private float _lastTrackedTime;
        private readonly List<Frame> _window = new List<Frame>();
        private SignSample _pending;          // start pose waiting for its movement
        private float _performStartTime;
        private float _bestAmplitude;
        private bool _performed;
        private float _performedAt;
        private string _message = "";

        private IHandProvider _providerOverride;
        private IHandProvider Provider => _providerOverride ?? handProvider;

        /// <summary>Uses another hand source (e.g. the teacher avatar for self-tests). Null restores the XR hands.</summary>
        public void SetProvider(IHandProvider provider) => _providerOverride = provider;

        public TemplateStore Store => _store;
        public SignTemplate CurrentTemplate => _store.Templates[_intentIndex];
        public HandFeatures? LastFeatures { get; private set; }

        private void Awake()
        {
            SignScorer.ConfigureForPlatform(); // Simulator jitter handling in the editor, spec values on device
            _store = TemplateStore.Load();
            if (handProvider == null) handProvider = FindFirstObjectByType<XRHandsProvider>();
            if (handProvider == null) Debug.LogError("[TemplateRecorder] No XRHandsProvider in the scene.");
        }

        // ---- Button actions (wired to pinch buttons) ----

        public void NextIntent() => SelectIntent(_intentIndex + 1);
        public void PreviousIntent() => SelectIntent(_intentIndex - 1);

        public void SelectIntent(int index)
        {
            int n = _store.Templates.Count;
            _intentIndex = ((index % n) + n) % n;
            CancelRecording();
            _message = "";
        }

        public void StartRecording()
        {
            _state = State.GetReady;
            _stateStart = Time.time;
            _window.Clear();
            _pending = null;
            _message = "";
        }

        public void CancelRecording()
        {
            _state = State.Idle;
            _window.Clear();
            _pending = null;
        }

        public void ClearCurrentIntent()
        {
            _store.ClearIntent(CurrentTemplate.intent);
            _store.Save();
            _message = $"Cleared recordings for {CurrentTemplate.intent}.";
        }

        /// <summary>Changes the current word's distance threshold (initial 0.35, adjustable here).</summary>
        public void AdjustTolerance(float delta)
        {
            var t = CurrentTemplate;
            t.tolerance = Mathf.Clamp(t.tolerance + delta, 0.05f, 0.8f);
            _store.Save();
            _message = $"{t.intent} threshold = {t.tolerance:0.00}";
        }

        public void IncreaseTolerance() => AdjustTolerance(0.05f);
        public void DecreaseTolerance() => AdjustTolerance(-0.05f);

        public void ResetAllTemplates()
        {
            _store.ResetToDefaults();
            _message = "All templates reset to defaults.";
        }

        // ---- Per frame ----

        private void Update()
        {
#if UNITY_EDITOR
            EditorKeys();
#endif
            _tracker.Push(Provider, Time.time);
            var tracked = TryReadHand(out var feat, out var leftCurl);
            if (tracked)
            {
                _lastTrackedTime = Time.time;
                LastFeatures = feat;
            }
            else if (Time.time - _lastTrackedTime > trackingLossReset)
            {
                LastFeatures = null;
                if (_state == State.Holding) _window.Clear();
            }

            if (_state == State.GetReady && Time.time - _stateStart >= getReadySeconds)
            {
                _state = State.Holding;
                _window.Clear();
            }

            if (_state == State.Holding && tracked)
            {
                _window.Add(new Frame { Time = Time.time, Curl = (float[])feat.Curl.Clone(), Palm = feat.PalmNormal, CurlLeft = leftCurl });
                TrimToStillWindow();
                if (_window.Count > 1 && _window[_window.Count - 1].Time - _window[0].Time >= holdSeconds)
                {
                    CaptureStartPose();
                }
            }

            if (_state == State.Performing) UpdatePerforming();

            RefreshUi();
        }

        private bool TryReadHand(out HandFeatures feat, out float[] leftCurl)
        {
            feat = default;
            leftCurl = null;
            var p = Provider;
            if (p == null) return false;
            var other = signingHand == HandSide.Right ? HandSide.Left : HandSide.Right;
            bool got = p.TryGetHand(signingHand, out var frame);
            if (got) feat = CurlCalculator.Compute(frame, p.Head);
            if (p.TryGetHand(other, out var o))
            {
                var of = CurlCalculator.Compute(o, p.Head);
                if (got) leftCurl = of.Curl;
                else
                {
                    feat = of;
                    got = true;
                }
            }
            return got;
        }

        /// <summary>Drops frames from the start of the window until every frame is close to the newest one.</summary>
        private void TrimToStillWindow()
        {
            var newest = _window[_window.Count - 1];
            int firstStill = _window.Count - 1;
            for (int i = _window.Count - 2; i >= 0; i--)
            {
                if (!IsClose(_window[i], newest)) break;
                firstStill = i;
            }
            if (firstStill > 0) _window.RemoveRange(0, firstStill);
        }

        private bool IsClose(Frame a, Frame b)
        {
            for (int i = 0; i < 5; i++)
            {
                if (Mathf.Abs(a.Curl[i] - b.Curl[i]) > maxCurlDeviation) return false;
            }
            return Vector3.Angle(a.Palm, b.Palm) <= maxPalmDeviationDegrees;
        }

        /// <summary>Step 1 done: the held start pose becomes the pending sample; step 2 asks for the movement.</summary>
        private void CaptureStartPose()
        {
            var sample = new SignSample();
            var palm = Vector3.zero;
            var left = new float[5];
            int nLeft = 0;
            foreach (var f in _window)
            {
                for (int i = 0; i < 5; i++) sample.curl[i] += f.Curl[i] / _window.Count;
                palm += f.Palm;
                if (f.CurlLeft != null)
                {
                    for (int i = 0; i < 5; i++) left[i] += f.CurlLeft[i];
                    nLeft++;
                }
            }
            sample.palm = palm.normalized;
            if (CurrentTemplate.TwoHanded && nLeft > 0)
            {
                for (int i = 0; i < 5; i++) left[i] /= nLeft;
                sample.curlLeft = left;
            }
            // Where the hands were during the hold (body frame) = this recording's start location.
            float from = _window[0].Time, to = _window[_window.Count - 1].Time;
            Vector3 sr = Vector3.zero, sl = Vector3.zero;
            int nr = 0, nl = 0;
            foreach (var fr in _tracker.Frames)
            {
                if (fr.Time < from || fr.Time > to) continue;
                if (fr.Right.Tracked) { sr += fr.Right.Palm; nr++; }
                if (fr.Left.Tracked) { sl += fr.Left.Palm; nl++; }
            }
            if (nr > 0 && (!CurrentTemplate.TwoHanded || nl > 0))
            {
                sample.hasStart = true;
                sample.startRight = sr / nr;
                sample.startLeft = nl > 0 ? sl / nl : Vector3.zero;
            }

            _pending = sample;
            _window.Clear();

            if (CurrentTemplate.movement.type == MovementTypes.None)
            {
                SaveSample(0f);
                return;
            }
            _state = State.Performing;
            _performStartTime = Time.time;
            _bestAmplitude = 0f;
            _performed = false;
        }

        /// <summary>Step 2: watch the movement with the lesson scoring and keep the largest amplitude measured.</summary>
        private void UpdatePerforming()
        {
            var frames = _tracker.Frames;
            int from = 0;
            while (from < frames.Count && frames[from].Time < _performStartTime) from++;
            // Measure against the §4.3 definition, so the spec thresholds decide whether the movement happened.
            var canon = TemplateStore.Canonical(CurrentTemplate.intent) ?? CurrentTemplate;
            var e = SignScorer.Evaluate(canon, frames, from);
            _bestAmplitude = Mathf.Max(_bestAmplitude, e.Measured);
            if (e.Performed && !_performed)
            {
                _performed = true;
                _performedAt = Time.time;
            }
            float elapsed = Time.time - _performStartTime;
            if (_performed && Time.time - _performedAt >= 0.6f)
            {
                SaveSample(_bestAmplitude);
                if (!string.IsNullOrEmpty(e.Hint)) _message += $"  (note: {e.Hint})";
            }
            else if (elapsed >= performSeconds)
            {
                if (_bestAmplitude > 0f)
                {
                    SaveSample(_bestAmplitude);
                    _message += $"  Movement was small ({_bestAmplitude:0.#} {canon.movement.unit}): {e.Hint}";
                }
                else
                {
                    _state = State.Idle;
                    _pending = null;
                    _message = $"No movement seen — press Record and try again. {e.Hint}";
                }
            }
        }

        private void SaveSample(float amplitude)
        {
            var t = CurrentTemplate;
            _pending.movementAmplitude = amplitude;
            _store.AddSample(t.intent, _pending);
            _store.Save();
            _state = State.Idle;
            _message = $"Saved recording {t.samples.Count}/{TemplateStore.MaxSamplesPerIntent} for {t.intent}" +
                       (amplitude > 0f ? $" (movement {amplitude:0.#} {t.movement.unit})" : "") + ". Now test it.";
            Debug.Log($"[TemplateRecorder] {t.intent} sample {t.samples.Count}: curl=[{Join(_pending.curl)}] palm={_pending.palm:F2}" +
                      (_pending.curlLeft.Length == 5 ? $" left=[{Join(_pending.curlLeft)}]" : "") +
                      $" movement {t.movement.type} {amplitude:0.#} {t.movement.unit} -> template curl=[{Join(t.curlRight)}] " +
                      $"palm={t.palmLabel} amplitude={t.movement.amplitude:0.#}. Saved to {TemplateStore.UserPath}");
            _pending = null;
        }

        // ---- UI ----

        private void RefreshUi()
        {
            var t = CurrentTemplate;
            if (intentText != null)
            {
                intentText.text = $"{t.intent}  <size=60%>\"{t.phrase}\"   recordings {t.samples.Count}/{TemplateStore.MaxSamplesPerIntent}   threshold {t.tolerance:0.00}</size>";
            }

            if (promptText != null)
            {
                switch (_state)
                {
                    case State.GetReady:
                        promptText.text = $"Get ready: make the {t.intent} start shape...";
                        break;
                    case State.Holding:
                        float held = _window.Count > 1 ? _window[_window.Count - 1].Time - _window[0].Time : 0f;
                        promptText.text = LastFeatures == null
                            ? "Hand not tracked. Show your hand."
                            : $"1) Hold the start shape still for 1 second  ({Mathf.Min(held, holdSeconds):0.0}s)";
                        break;
                    case State.Performing:
                        promptText.text = $"2) Now perform the movement once: {t.movement.type}  ({Mathf.Max(0f, performSeconds - (Time.time - _performStartTime)):0.0}s)";
                        break;
                    default:
                        promptText.text = string.IsNullOrEmpty(_message)
                            ? "Pinch Record, hold the start shape, then perform the movement once."
                            : _message;
                        break;
                }
            }

            if (liveText != null) liveText.text = LiveReadout();
        }

        private string LiveReadout()
        {
            if (LastFeatures == null) return "No hand tracked";
            var f = LastFeatures.Value;
            var sb = new StringBuilder();
            // Same trajectory scoring as the lessons, over the last 3 s.
            var evals = new List<SignEval>();
            foreach (var t in _store.Templates) evals.Add(SignScorer.Evaluate(t, _tracker.Frames, 0));
            evals.Sort((a, b) => a.Total.CompareTo(b.Total));
            if (evals.Count > 1)
            {
                sb.Append($"Top1: {Score(evals[0])}\nTop2: {Score(evals[1])}\n");
                if (!string.IsNullOrEmpty(evals[0].Hint)) sb.Append($"<size=80%>{evals[0].Hint}</size>\n");
            }
            sb.Append($"<size=70%>curl [{Join(f.Curl)}]  palm {f.Palm}  pinch {f.Pinch:0.00}  spread {f.Spread:0.00}</size>");
            return sb.ToString();
        }

        private static string Score(SignEval e)
        {
            bool ok = e.Completed && e.Total <= e.Template.tolerance;
            string mark = ok ? "<color=#4CD964>match</color>" : e.Completed ? "<color=#FF9500>too far</color>" : "<color=#FF9500>no movement</color>";
            return $"{e.Template.intent}  conf {e.Confidence:0.00}  (dist {e.Total:0.00}: H{e.Handshape:0.00} L{e.Location:0.00} M{e.Movement:0.00})  {mark}";
        }

        private static string Join(float[] v)
        {
            var parts = new string[v.Length];
            for (int i = 0; i < v.Length; i++) parts[i] = v[i].ToString("0.00");
            return string.Join(", ", parts);
        }

#if UNITY_EDITOR
        // Editor-only shortcuts for driving the recorder from the Meta XR Simulator window. Not in builds.
        private void EditorKeys()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.rightBracketKey.wasPressedThisFrame) NextIntent();
            if (kb.leftBracketKey.wasPressedThisFrame) PreviousIntent();
            if (kb.enterKey.wasPressedThisFrame) StartRecording();
            if (kb.backspaceKey.wasPressedThisFrame) ClearCurrentIntent();
            if (kb.equalsKey.wasPressedThisFrame) IncreaseTolerance();
            if (kb.minusKey.wasPressedThisFrame) DecreaseTolerance();
        }
#endif
    }
}
