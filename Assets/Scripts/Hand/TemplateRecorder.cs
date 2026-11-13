using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace SignSight.Hand
{
    /// <summary>
    /// Template Recorder (SPEC §4.4): pick an intent, make the sign and hold it for 1 s, the held shape is saved
    /// as one recording; up to 3 recordings per intent are averaged into the template. The live readout shows
    /// the closest templates and confidence so a recording can be tested straight away.
    /// </summary>
    public class TemplateRecorder : MonoBehaviour
    {
        private enum State { Idle, GetReady, Holding }

        [SerializeField] private XRHandsProvider handProvider;
        [Tooltip("Hand used for signing. Falls back to the other hand when this one is not tracked.")]
        [SerializeField] private HandSide signingHand = HandSide.Right;

        [Header("Timing")]
        [Tooltip("Seconds the sign must be held still to be recorded.")]
        [SerializeField] private float holdSeconds = 1f;
        [Tooltip("Seconds after pressing Record before the hold starts counting (time to form the sign).")]
        [SerializeField] private float getReadySeconds = 1.5f;
        [Tooltip("Seconds without tracking before the hold is reset.")]
        [SerializeField] private float trackingLossReset = 0.5f;

        [Header("Stillness")]
        [SerializeField] private float maxCurlDeviation = 0.12f;
        [SerializeField] private float maxPalmDeviationDegrees = 25f;

        [Header("UI")]
        [SerializeField] private TMP_Text intentText;
        [SerializeField] private TMP_Text promptText;
        [SerializeField] private TMP_Text liveText;

        private struct Frame { public float Time; public float[] Curl; public Vector3 Palm; }

        private TemplateStore _store;
        private int _intentIndex;
        private State _state = State.Idle;
        private float _stateStart;
        private float _lastTrackedTime;
        private readonly List<Frame> _window = new List<Frame>();
        private string _message = "";

        public TemplateStore Store => _store;
        public SignTemplate CurrentTemplate => _store.Templates[_intentIndex];
        public HandFeatures? LastFeatures { get; private set; }

        private void Awake()
        {
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
            _message = "";
        }

        public void CancelRecording()
        {
            _state = State.Idle;
            _window.Clear();
        }

        public void ClearCurrentIntent()
        {
            _store.ClearIntent(CurrentTemplate.intent);
            _store.Save();
            _message = $"Cleared recordings for {CurrentTemplate.intent}.";
        }

        /// <summary>Changes the current intent's distance threshold (SPEC §4.5: initial 0.35, adjustable here).</summary>
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
            var tracked = TryReadHand(out var feat);
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
                _window.Add(new Frame { Time = Time.time, Curl = (float[])feat.Curl.Clone(), Palm = feat.PalmNormal });
                TrimToStillWindow();
                if (_window.Count > 1 && _window[_window.Count - 1].Time - _window[0].Time >= holdSeconds)
                {
                    SaveWindowAsSample();
                }
            }

            RefreshUi();
        }

        private bool TryReadHand(out HandFeatures feat)
        {
            feat = default;
            if (handProvider == null) return false;
            var other = signingHand == HandSide.Right ? HandSide.Left : HandSide.Right;
            if (!handProvider.TryGetHand(signingHand, out var frame) && !handProvider.TryGetHand(other, out frame)) return false;
            feat = CurlCalculator.Compute(frame, handProvider.Head);
            return true;
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

        private void SaveWindowAsSample()
        {
            var sample = new SignSample();
            var palm = Vector3.zero;
            foreach (var f in _window)
            {
                for (int i = 0; i < 5; i++) sample.curl[i] += f.Curl[i] / _window.Count;
                palm += f.Palm;
            }
            sample.palm = palm.normalized;

            var t = CurrentTemplate;
            _store.AddSample(t.intent, sample);
            _store.Save();
            _state = State.Idle;
            _window.Clear();
            _message = $"Saved recording {t.samples.Count}/{TemplateStore.MaxSamplesPerIntent} for {t.intent}. Now test it.";
            Debug.Log($"[TemplateRecorder] {t.intent} sample {t.samples.Count}: curl=[{Join(sample.curl)}] palm={sample.palm:F2} -> template curl=[{Join(t.curl)}] palm={t.palmLabel}. Saved to {TemplateStore.UserPath}");
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
                        promptText.text = $"Get ready: make the {t.intent} sign...";
                        break;
                    case State.Holding:
                        float held = _window.Count > 1 ? _window[_window.Count - 1].Time - _window[0].Time : 0f;
                        promptText.text = LastFeatures == null
                            ? "Hand not tracked. Show your hand."
                            : $"Make this sign and hold it for 1 second  ({Mathf.Min(held, holdSeconds):0.0}s)";
                        break;
                    default:
                        promptText.text = string.IsNullOrEmpty(_message) ? "Pinch Record, then make the sign and hold it." : _message;
                        break;
                }
            }

            if (liveText != null) liveText.text = LiveReadout();
        }

        private string LiveReadout()
        {
            if (LastFeatures == null) return "No hand tracked";
            var f = LastFeatures.Value;
            var ranked = _store.Rank(f.Curl, f.PalmNormal);
            var sb = new StringBuilder();
            sb.Append($"Top1: {Score(ranked[0])}\nTop2: {Score(ranked[1])}\n");
            sb.Append($"<size=70%>curl [{Join(f.Curl)}]  palm {f.Palm}  pinch {f.Pinch:0.00}  spread {f.Spread:0.00}</size>");
            return sb.ToString();
        }

        private static string Score(TemplateScore s)
        {
            string mark = s.WithinTolerance ? "<color=#4CD964>match</color>" : "<color=#FF9500>too far</color>";
            return $"{s.Template.intent}  conf {s.Confidence:0.00}  (dist {s.Distance:0.00})  {mark}";
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
