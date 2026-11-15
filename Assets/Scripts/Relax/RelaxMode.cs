using System;
using System.Collections.Generic;
using SignSight.Hand;
using SignSight.Learn;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace SignSight.Relax
{
    /// <summary>
    /// Finger Relax (v3.4): a gentle ~1 minute stretch break between sign practice — not a lesson, no score, no stars,
    /// no Tutor, no "wrong". Five steps in a fixed order (the list below is data). Each step shows a ghost hand with the
    /// target pose and fills a ring while the target is held; it completes when the target is reached OR when its
    /// timeout runs out (auto-advance, "Nice — moving on"). Either hand counts (whichever is tracked first). If no hand
    /// is tracked the ring pauses ("Show me your hand"); nothing ever fails. Hands only: the learner's face is never read.
    /// Hand data: the existing IHandProvider + CurlCalculator; the wrist / curl history: a TrajectoryTracker buffer.
    /// </summary>
    public class RelaxMode : MonoBehaviour
    {
        // ---------------- thresholds (deliberately wide; tune here only) ----------------
        public const float OpenMaxCurl = 0.25f;          // step 1: mean curl of the 4 fingers below this = open
        public const float CloseMinCurl = 0.75f;         // step 1: mean curl above this = closed
        public const float OpenCloseHoldSeconds = 1f;    // each open / close is held this long
        public const int OpenCloseCycles = 2;
        public const float SpreadMin = 0.6f;             // step 2: CurlCalculator spread above this
        public const float SpreadMaxCurl = 0.4f;         // step 2: and the fingers extended (mean curl below this)
        public const float SpreadHoldSeconds = 2f;
        public const int SpreadReps = 2;
        public const float PianoTouch = 0.3f;            // step 3: thumb tip to fingertip / hand size (wrist -> middle knuckle) below this = touch
        public const float PianoMinGapSeconds = 0.3f;    // two taps at least this far apart
        public const float ShakeAmplitude = 0.03f;       // step 4: side-to-side wrist travel (m) within the window
        public const int ShakeReversals = 4;             // direction changes within the window
        public const float ShakeWindowSeconds = 2f;
        public const float ShakeHysteresis = 0.008f;     // a reversal counts after moving back this far (m)
        public const float WiggleChange = 0.3f;          // step 5: any finger's curl changing this much within 1 s
        public const float WiggleWindowSeconds = 1f;
        public const float MessageSeconds = 0.6f;        // "Nice — moving on" before the ghost moves
        public const float BetweenSeconds = 0.5f;        // ghost hand moves to the next pose

        public const string WhyLine = "Fingers feeling stiff? Stretch, shake it out — no score here.";
        public const string SafetyLine = "Stop if it hurts — gentle only, not medical advice.";
        public const string ShowHand = "Show me your hand";
        public const string MovingOn = "Nice — moving on";
        public const string FinishedTitle = "All loose — nice work";

        public enum Kind { OpenClose, Spread, FingerPiano, Shake, Wiggle }
        public enum State { Off, Step, Between, Finished }

        [Serializable]
        public class Step
        {
            public Kind kind;
            public string name;
            public string instruction;
            public float timeout;
        }

        /// <summary>The five steps, fixed order (~60–75 s with the timeouts).</summary>
        public static readonly Step[] Steps =
        {
            new Step { kind = Kind.OpenClose, name = "Open & Close", instruction = "Open your hand wide, then make a soft fist — twice", timeout = 20f },
            new Step { kind = Kind.Spread, name = "Spread Wide", instruction = "Spread your fingers wide, hold 2 s — twice", timeout = 15f },
            new Step { kind = Kind.FingerPiano, name = "Finger Piano", instruction = "Touch your thumb to each fingertip: index, middle, ring, pinky", timeout = 20f },
            new Step { kind = Kind.Shake, name = "Shake It Out", instruction = "Loose wrist — shake your hand side to side", timeout = 12f },
            new Step { kind = Kind.Wiggle, name = "Wiggle & Rest", instruction = "Wiggle your fingers, or just rest them", timeout = 8f },
        };

        public static readonly string[] PianoFingers = { "index", "middle", "ring", "pinky" };

        /// <summary>One measured frame of the tracked hand (what the steps read).</summary>
        public struct Sample
        {
            public bool Tracked;
            public float[] Curl;      // CurlCalculator [thumb, index, middle, ring, pinky]
            public float Spread;      // CurlCalculator
            public float[] TipGap;    // thumb tip -> index / middle / ring / pinky tip, divided by hand size
            public Vector3 Wrist;     // signer's body frame (TrajectoryTracker), metres
        }

        [SerializeField] private LessonManager lessons;
        [SerializeField] private TeacherAvatarDriver teacher;
        [SerializeField] private XRHandsProvider hands;
        [Tooltip("Ghost hand to copy the look from (the feedback panel's target hand).")]
        [SerializeField] private GhostHand ghostTemplate;
        // Left of the teacher, clear of MENU, the word card and the button row (viewport check in the Simulator).
        public static readonly Vector3 GhostPosition = new Vector3(-0.62f, -0.03f, 0.70f);
        public const float GhostSize = 0.8f;
        /// <summary>Where LessonUI puts the hold ring in Relax (next to the ghost hand).</summary>
        public static readonly Vector3 RingPosition = new Vector3(-0.44f, -0.05f, 0.70f);

        public State Current { get; private set; } = State.Off;
        public int StepIndex { get; private set; }
        public Step CurrentStep => StepIndex >= 0 && StepIndex < Steps.Length ? Steps[StepIndex] : null;
        /// <summary>0..1 toward the current step's target (the ring).</summary>
        public float Progress { get; private set; }
        public float StepElapsed { get; private set; }
        public bool HandTracked { get; private set; }
        public bool Wiggling { get; private set; }
        /// <summary>The last step ended by its target (true) or by its timeout (false).</summary>
        public bool LastCompletedByTarget { get; private set; }
        /// <summary>Per step: "target", "timeout" or "skip" (for the dev check / report).</summary>
        public readonly List<string> StepEndings = new List<string>();
        public string Status { get; private set; } = "";
        /// <summary>Opened from the lesson summary: DONE continues to the next lesson (LessonManager).</summary>
        public bool ReturnToLesson { get; set; }
        public GhostHand Ghost => _ghost;

        // ---- v3.7 visuals: read-only views of what the steps already measure (no new detection) ----
        /// <summary>The last measured frame (what the steps read).</summary>
        public Sample LastSample { get; private set; }
        /// <summary>Finger Piano: the target finger (0 index .. 3 pinky); 4 = all done.</summary>
        public int PianoFinger => _phase;
        /// <summary>Shake: direction changes counted in the current 2 s window.</summary>
        public int ShakeCount { get; private set; }
        /// <summary>A hold (Open &amp; Close, Spread Wide) is counting right now.</summary>
        public bool Holding => _hold > 0f;
        public RelaxVisual Visual => _visual;

        // v3.7 look: warm ghost, green while a hold counts, a small pop and a coloured sparkle per finished step
        public static readonly Color GhostWarm = new Color(1f, 0.9f, 0.75f, 0.6f);
        public static readonly Color GhostHold = new Color(0.55f, 0.95f, 0.6f, 0.6f);
        public const float PopSeconds = CelebrationFX.PopSeconds;   // 0.4 s, as the lesson celebration
        public const float PopSize = 0.9f;                          // GhostSize 0.8 -> 0.9 -> 0.8
        public const int SparkleParticles = 18;
        /// <summary>Sparkle colour per step: OpenClose green, Spread gold, Piano blue, Shake green, Wiggle blue.</summary>
        public static readonly Color[] SparkleColor =
        {
            new Color(0.45f, 0.9f, 0.5f), new Color(1f, 0.82f, 0.3f), new Color(0.5f, 0.7f, 1f),
            new Color(0.6f, 0.95f, 0.55f), new Color(0.6f, 0.85f, 1f),
        };
        public const float TeacherSmile = 30f;
        public int Sparkles { get; private set; }
        public int Pops { get; private set; }

        /// <summary>Test hook (Editor checks): measured frames come from here instead of the hands.</summary>
        public Func<Sample> DebugSample;

        public event Action Changed;
        /// <summary>DONE pressed on the last screen.</summary>
        public event Action DoneRequested;

        private GhostHand _ghost;
        private readonly TrajectoryTracker _traj = new TrajectoryTracker(3f);
        private HandSide _side = HandSide.Right;
        private bool _hasSide;
        private float _hold, _betweenT, _lastTap = -10f;
        private int _phase;          // OpenClose: 0 open,1 close,2 open,3 close; Spread: rep; Piano: finger
        private bool _needRelease;   // Spread: let go between the two holds
        private float[] _poseFrom = new float[5], _poseTo = new float[5];
        private CelebrationFX _fx;
        private Transform _fxAnchor;
        private RelaxVisual _visual;
        private float _popT = -1f;
        private float _fingerTint = -1f;

        private static readonly float[] Open = { 0f, 0f, 0f, 0f, 0f };
        private static readonly float[] Fist = { 0.7f, 0.95f, 0.95f, 0.95f, 0.95f };

        private void Awake()
        {
            if (lessons == null) lessons = FindFirstObjectByType<LessonManager>();
            if (teacher == null) teacher = FindFirstObjectByType<TeacherAvatarDriver>();
            if (hands == null) hands = FindFirstObjectByType<XRHandsProvider>();
            if (ghostTemplate == null)
                foreach (var g in FindObjectsByType<GhostHand>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (g.name == "TargetHand") ghostTemplate = g;
        }

        private void EnsureGhost()
        {
            if (_ghost != null) return;
            var ui = GameObject.Find("LearnUI");
            var go = new GameObject("RelaxGhostHand");
            go.SetActive(false); // Awake (Build) waits until the look is copied
            go.transform.SetParent(ui != null ? ui.transform : transform, false);
            go.transform.localPosition = GhostPosition;
            go.transform.localRotation = Quaternion.Euler(0f, 195f, 0f); // palm toward the learner, slightly turned
            _ghost = go.AddComponent<GhostHand>();
            if (ghostTemplate != null) JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(ghostTemplate), _ghost);
            _ghost.IsLeft = false;
            _ghost.SizeMultiplier = GhostSize;
            go.SetActive(true);
            _ghost.SetAllColors(GhostWarm); // v3.7: warm, still semi-transparent (the Learn feedback ghost is unchanged)
            go.SetActive(false);

            _fxAnchor = new GameObject("RelaxSparkle").transform;
            _fxAnchor.SetParent(go.transform.parent, false);
            _fxAnchor.localPosition = GhostPosition + new Vector3(0f, 0.08f, 0f);
            _fx = CelebrationFX.Create(_fxAnchor);
            // A soft sparkle per finished step: 18 particles, no stars, no title pop, no points.
            var ps = _fx.GetComponentInChildren<ParticleSystem>(true);
            if (ps != null) ps.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)SparkleParticles) });
            _visual = RelaxVisual.Create(go.transform.parent, this);
        }

        /// <summary>Entering / leaving Relax (LessonManager calls this).</summary>
        public void SetActive(bool on)
        {
            EnsureGhost();
            _ghost.gameObject.SetActive(on);
            StepEndings.Clear();
            if (on)
            {
                if (teacher != null && teacher.IsDemonstrating) teacher.Rest(); // the ghost hand demonstrates, the teacher rests
                if (teacher != null && teacher.Face != null) teacher.Face.Expression(TeacherSmile, 0f); // v3.7: a soft smile, hands at rest
                _hasSide = false;
                _traj.Clear();
                BeginStep(0);
            }
            else
            {
                Current = State.Off;
                ReturnToLesson = false;
                _fx?.Hide();
                if (teacher != null && teacher.Face != null && !teacher.IsDemonstrating) teacher.Face.Neutral();
            }
            Changed?.Invoke();
        }

        private void BeginStep(int i)
        {
            StepIndex = i;
            Current = State.Step;
            StepElapsed = 0f;
            Progress = 0f;
            _hold = 0f;
            _phase = 0;
            _needRelease = false;
            _lastTap = -10f;
            Wiggling = false;
            Status = "";
            if (_ghost != null) _ghost.Spread = Steps[i].kind == Kind.Spread ? 1f : 0f; // fingers fanned for Spread Wide
            SetGhostTarget(TargetPose(), immediate: i == 0);
            Changed?.Invoke();
        }

        /// <summary>SKIP: go to the next step (on the last step: the end screen).</summary>
        public void Skip()
        {
            if (Current != State.Step && Current != State.Between) return;
            if (Current == State.Step) StepEndings.Add("skip");
            NextStep();
        }

        /// <summary>DONE on the end screen.</summary>
        public void Done()
        {
            if (Current != State.Finished) return;
            DoneRequested?.Invoke();
        }

        private void NextStep()
        {
            if (StepIndex + 1 < Steps.Length) BeginStep(StepIndex + 1);
            else Finish();
        }

        private void Finish()
        {
            Current = State.Finished;
            Progress = 1f;
            Status = "";
            _ghost.Spread = 0f;
            SetGhostTarget(Open, false);
            _ghost.ClearMotion();
            Pop();
            Sparkle(SparkleColor[SparkleColor.Length - 1]); // confetti only: 0 stars
            Changed?.Invoke();
        }

        private void EndStep(bool byTarget)
        {
            LastCompletedByTarget = byTarget;
            StepEndings.Add(byTarget ? "target" : "timeout");
            Current = State.Between;
            _betweenT = 0f;
            Progress = byTarget ? 1f : Progress;
            Status = MovingOn;
            Pop(); // every finished step gets the small pop ("Nice — moving on", no fail)
            if (byTarget) Sparkle(SparkleColor[Mathf.Clamp(StepIndex, 0, SparkleColor.Length - 1)]); // target endings only
            Changed?.Invoke();
        }

        private void Pop()
        {
            _popT = 0f;
            Pops++;
        }

        private void Sparkle(Color c)
        {
            if (_fx == null) return;
            var ps = _fx.GetComponentInChildren<ParticleSystem>(true);
            if (ps != null)
            {
                var main = ps.main;
                main.startColor = new ParticleSystem.MinMaxGradient(c, Color.Lerp(c, Color.white, 0.4f)) { mode = ParticleSystemGradientMode.TwoColors };
            }
            _fx.Play(null, null, 0, _fxAnchor.localPosition);
            Sparkles++;
        }

        /// <summary>v3.7: the ghost pop (0.8 -> 0.9 -> 0.8 over 0.4 s) and the involved fingers turning green while a hold counts.</summary>
        private void AnimateLook(float dt)
        {
            if (_ghost == null) return;
            if (_popT >= 0f)
            {
                _popT += dt;
                float u = Mathf.Clamp01(_popT / PopSeconds);
                _ghost.SizeMultiplier = GhostSize + (PopSize - GhostSize) * Mathf.Sin(u * Mathf.PI);
                if (u >= 1f)
                {
                    _ghost.SizeMultiplier = GhostSize;
                    _popT = -1f;
                }
            }
            float tint = 0f;
            var step = CurrentStep;
            if (Current == State.Step && step != null && _hold > 0f)
            {
                float need = step.kind == Kind.OpenClose ? OpenCloseHoldSeconds : step.kind == Kind.Spread ? SpreadHoldSeconds : 1f;
                tint = Mathf.Clamp01(_hold / need);
            }
            tint = Mathf.Round(tint * 10f) / 10f; // repaint the mesh only when the shade changes
            if (Mathf.Approximately(tint, _fingerTint)) return;
            _fingerTint = tint;
            var c = Color.Lerp(GhostWarm, GhostHold, tint);
            // the involved fingers: index..pinky (and the thumb for Open & Close)
            bool thumb = step != null && step.kind == Kind.OpenClose;
            for (int f = 0; f < 5; f++) _ghost.SetFingerColor(f, f == 0 && !thumb ? GhostWarm : c);
        }

        // ---------------- per frame ----------------

        private void Update()
        {
            if (Current == State.Off || lessons == null || lessons.CurrentMode != LessonManager.Mode.Relax) return;
            float dt = Time.deltaTime;
            var s = Measure();
            HandTracked = s.Tracked;
            LastSample = s;
            AnimateGhost(dt);
            AnimateLook(dt);

            if (Current == State.Between)
            {
                _betweenT += dt;
                if (_betweenT >= MessageSeconds + BetweenSeconds) NextStep();
                return;
            }
            if (Current != State.Step) return;

            StepElapsed += dt; // the timeout runs with or without a hand, so nobody is ever stuck
            string before = Status;
            bool done = false;
            if (!s.Tracked && CurrentStep.kind != Kind.Wiggle)
            {
                Status = ShowHand; // ring pauses, nothing counts as a miss
            }
            else
            {
                Status = "";
                done = Evaluate(s, dt);
            }
            if (done) EndStep(true);
            else if (StepElapsed >= CurrentStep.timeout) EndStep(CurrentStep.kind == Kind.Wiggle);
            else if (Status != before) Changed?.Invoke();
        }

        public static float MeanFingerCurl(float[] c) => (c[1] + c[2] + c[3] + c[4]) * 0.25f;

        private bool Evaluate(in Sample s, float dt)
        {
            var step = CurrentStep;
            switch (step.kind)
            {
                case Kind.OpenClose:
                {
                    float m = MeanFingerCurl(s.Curl);
                    bool wantOpen = _phase % 2 == 0;
                    bool ok = wantOpen ? m < OpenMaxCurl : m > CloseMinCurl;
                    _hold = ok ? _hold + dt : 0f;
                    if (_hold >= OpenCloseHoldSeconds)
                    {
                        _phase++;
                        _hold = 0f;
                        if (_phase < OpenCloseCycles * 2) SetGhostTarget(TargetPose(), false);
                    }
                    Progress = Mathf.Clamp01((_phase + _hold / OpenCloseHoldSeconds) / (OpenCloseCycles * 2));
                    Status = _phase % 2 == 0 ? "Open wide" : "Soft fist";
                    return _phase >= OpenCloseCycles * 2;
                }
                case Kind.Spread:
                {
                    bool ok = s.Spread > SpreadMin && MeanFingerCurl(s.Curl) < SpreadMaxCurl;
                    if (_needRelease)
                    {
                        Status = "Relax your fingers, then spread again";
                        if (!ok) _needRelease = false;
                        return false;
                    }
                    _hold = ok ? _hold + dt : 0f;
                    if (_hold >= SpreadHoldSeconds)
                    {
                        _phase++;
                        _hold = 0f;
                        _needRelease = _phase < SpreadReps;
                    }
                    Progress = Mathf.Clamp01((_phase + _hold / SpreadHoldSeconds) / SpreadReps);
                    Status = $"Hold {_phase + 1} of {SpreadReps}";
                    return _phase >= SpreadReps;
                }
                case Kind.FingerPiano:
                {
                    int f = Mathf.Clamp(_phase, 0, 3);
                    if (s.TipGap[f] < PianoTouch && Time.time - _lastTap >= PianoMinGapSeconds)
                    {
                        _phase++;
                        _lastTap = Time.time;
                        if (_phase < 4) SetGhostTarget(TargetPose(), false);
                    }
                    Progress = Mathf.Clamp01(_phase / 4f);
                    Status = _phase < 4 ? $"Thumb to {PianoFingers[Mathf.Min(_phase, 3)]}" : "";
                    return _phase >= 4;
                }
                case Kind.Shake:
                {
                    ShakeMeasure(out float amp, out int rev);
                    ShakeCount = rev;
                    Progress = Mathf.Clamp01(Mathf.Min(amp / ShakeAmplitude, rev / (float)ShakeReversals));
                    return amp >= ShakeAmplitude && rev >= ShakeReversals;
                }
                case Kind.Wiggle:
                {
                    Wiggling = s.Tracked && WiggleMeasure() >= WiggleChange;
                    Status = Wiggling ? "Nice wiggling" : "";
                    Progress = Mathf.Clamp01(StepElapsed / step.timeout);
                    return false; // always ends at 8 s (the timeout)
                }
            }
            return false;
        }

        /// <summary>Side-to-side wrist travel and direction changes over the last 2 s of tracked frames.</summary>
        private void ShakeMeasure(out float amplitude, out int reversals)
        {
            amplitude = 0f;
            reversals = 0;
            var fr = _traj.Frames;
            float now = Time.time, min = float.MaxValue, max = float.MinValue, ext = 0f;
            int dir = 0;
            bool started = false;
            for (int i = 0; i < fr.Count; i++)
            {
                if (now - fr[i].Time > ShakeWindowSeconds || !fr[i].Right.Tracked) continue;
                float x = fr[i].Right.Wrist.x;
                min = Mathf.Min(min, x);
                max = Mathf.Max(max, x);
                if (!started) { ext = x; started = true; continue; }
                if (dir == 0)
                {
                    // first clear movement sets the direction
                    if (x - ext > ShakeHysteresis) { dir = 1; ext = x; }
                    else if (ext - x > ShakeHysteresis) { dir = -1; ext = x; }
                }
                else if (dir > 0)
                {
                    if (x > ext) ext = x;                                                       // still moving right
                    else if (ext - x > ShakeHysteresis) { reversals++; dir = -1; ext = x; }   // turned left
                }
                else
                {
                    if (x < ext) ext = x;                                                       // still moving left
                    else if (x - ext > ShakeHysteresis) { reversals++; dir = 1; ext = x; }    // turned right
                }
            }
            if (started) amplitude = max - min;
        }

        /// <summary>Largest change of any finger's curl within the last second.</summary>
        private float WiggleMeasure()
        {
            var fr = _traj.Frames;
            float now = Time.time, best = 0f;
            for (int f = 1; f < 5; f++)
            {
                float lo = float.MaxValue, hi = float.MinValue;
                for (int i = 0; i < fr.Count; i++)
                {
                    if (now - fr[i].Time > WiggleWindowSeconds || !fr[i].Right.Tracked || fr[i].Right.Curl == null) continue;
                    lo = Mathf.Min(lo, fr[i].Right.Curl[f]);
                    hi = Mathf.Max(hi, fr[i].Right.Curl[f]);
                }
                if (hi > lo) best = Mathf.Max(best, hi - lo);
            }
            return best;
        }

        // ---------------- hands ----------------

        /// <summary>Reads the tracked hand (whichever was tracked first, else the other) and buffers it.</summary>
        private Sample Measure()
        {
            Sample s;
            if (DebugSample != null)
            {
                s = DebugSample();
                var hs = new HandSample { Tracked = s.Tracked, Wrist = s.Wrist, Curl = s.Curl };
                _traj.Push(Time.time, hs, default, Vector3.zero, Quaternion.identity);
                return s;
            }
            s = new Sample { Tracked = false };
            HandFrame f = default;
            bool got = false;
            if (hands != null)
            {
                if (_hasSide && hands.TryGetHand(_side, out f)) got = true;
                else
                {
                    foreach (var side in new[] { HandSide.Right, HandSide.Left })
                    {
                        if (!hands.TryGetHand(side, out f)) continue;
                        _side = side;
                        _hasSide = true;
                        got = true;
                        break;
                    }
                }
            }
            if (!got)
            {
                _traj.Push(Time.time, default, default, Vector3.zero, Quaternion.identity);
                return s;
            }
            var feat = CurlCalculator.Compute(f, hands.Head);
            // Same normalisation as CurlCalculator's pinch: divide by hand size (wrist -> middle knuckle).
            float size = Vector3.Distance(f.Pos(XRHandJointID.Wrist), f.Pos(XRHandJointID.MiddleProximal));
            if (size < 1e-4f) size = 0.08f;
            var thumb = f.Pos(XRHandJointID.ThumbTip);
            s.Tracked = true;
            s.Curl = feat.Curl;
            s.Spread = feat.Spread;
            s.TipGap = new[]
            {
                Vector3.Distance(thumb, f.Pos(XRHandJointID.IndexTip)) / size,
                Vector3.Distance(thumb, f.Pos(XRHandJointID.MiddleTip)) / size,
                Vector3.Distance(thumb, f.Pos(XRHandJointID.RingTip)) / size,
                Vector3.Distance(thumb, f.Pos(XRHandJointID.LittleTip)) / size,
            };
            var sample = _traj.Sample(f, hands.Head); // body frame (TrajectoryTracker), wrist + curl history
            s.Wrist = sample.Wrist;
            _traj.Push(Time.time, sample, default, hands.Head != null ? hands.Head.position : Vector3.zero,
                       hands.Head != null ? hands.Head.rotation : Quaternion.identity);
            return s;
        }

        // ---------------- ghost hand ----------------

        private float[] TargetPose()
        {
            var step = CurrentStep;
            if (step == null) return Open;
            switch (step.kind)
            {
                case Kind.OpenClose: return _phase % 2 == 0 ? Open : Fist;
                case Kind.FingerPiano:
                {
                    var p = new float[] { 0.75f, 0.05f, 0.05f, 0.05f, 0.05f };
                    p[1 + Mathf.Clamp(_phase, 0, 3)] = 0.55f; // thumb and that finger bend toward each other
                    return p;
                }
                default: return Open;
            }
        }

        private float _blend = 1f;

        private void SetGhostTarget(float[] pose, bool immediate)
        {
            if (_ghost == null) return;
            Array.Copy(_poseTo, _poseFrom, 5);
            Array.Copy(pose, _poseTo, 5);
            _blend = immediate ? 1f : 0f;
            if (immediate) _ghost.Pose(_poseTo);
        }

        private void AnimateGhost(float dt)
        {
            if (_ghost == null || !_ghost.gameObject.activeSelf) return;
            var step = CurrentStep;
            float t = Time.time;
            if (_blend < 1f)
            {
                _blend = Mathf.Min(1f, _blend + dt / BetweenSeconds);
                var p = new float[5];
                for (int i = 0; i < 5; i++) p[i] = Mathf.Lerp(_poseFrom[i], _poseTo[i], Mathf.SmoothStep(0f, 1f, _blend));
                _ghost.Pose(p);
            }
            if (Current == State.Step && step != null && step.kind == Kind.Shake)
            {
                _ghost.SetMotion(new Vector3(Mathf.Sin(t * Mathf.PI * 2f * 2.5f) * 0.035f, 0f, 0f),
                                 Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 2f * 2.5f) * 12f));
            }
            else if (Current == State.Step && step != null && step.kind == Kind.Wiggle && _blend >= 1f)
            {
                var p = new float[5];
                for (int i = 0; i < 5; i++) p[i] = 0.35f + 0.3f * Mathf.Sin(t * 7f + i * 1.2f);
                p[0] = 0.15f;
                _ghost.Pose(p);
                _ghost.ClearMotion();
            }
            else _ghost.ClearMotion();
        }
    }
}
