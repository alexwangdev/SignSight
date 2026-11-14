using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace SignSight.Hand
{
    /// <summary>
    /// One hand in one frame, in the signer's body frame: origin at the (smoothed) head, x = signer's right,
    /// y = up, z = forward (toward the partner), yaw only. Metres.
    /// </summary>
    public struct HandSample
    {
        public bool Tracked;
        public Vector3 Wrist, Palm, IndexTip, MiddleTip, ThumbTip;
        public Vector3 Normal;   // unit vector out of the palm
        public Vector3 Axis;     // unit vector wrist -> middle knuckle
        public float Size;       // wrist -> middle knuckle, metres
        public float[] Curl;     // CurlCalculator, unchanged
    }

    /// <summary>Both hands + head for one frame. Head is stored for demo-sync display only, never for scoring.</summary>
    public struct TrajFrame
    {
        public float Time;
        public HandSample Right, Left;
        public Vector3 HeadPos;      // world
        public Quaternion HeadRot;   // world
    }

    /// <summary>
    /// Ring buffer of both hands (SPEC v3 §4.6) plus the movement feature extraction for the eight movement types.
    /// Plain class: SignRecognizer and TemplateRecorder each feed one from an IHandProvider every frame.
    /// </summary>
    public class TrajectoryTracker
    {
        public float BufferSeconds;
        private readonly List<TrajFrame> _frames = new List<TrajFrame>();
        private Vector3 _origin;
        private Quaternion _yaw = Quaternion.identity;
        private bool _hasOrigin;

        public IReadOnlyList<TrajFrame> Frames => _frames;

        /// <param name="bufferSeconds">
        /// SPEC v3 says 1.5 s; 3 s is used so a movement can still be scored after the short settle that
        /// follows its completion (a slow two-crank COFFEE takes ~1.5 s on its own).
        /// </param>
        public TrajectoryTracker(float bufferSeconds = 3f) => BufferSeconds = bufferSeconds;

        public void Clear()
        {
            _frames.Clear();
            _hasOrigin = false;
        }

        /// <summary>Reads both hands from the provider and appends one frame.</summary>
        public void Push(IHandProvider provider, float time)
        {
            var head = provider?.Head;
            HandFrame r = default, l = default;
            bool hasR = provider != null && provider.TryGetHand(HandSide.Right, out r);
            HandSample right = hasR ? Sample(r, head) : default;   // sample before the next TryGetHand reuses buffers
            bool hasL = provider != null && provider.TryGetHand(HandSide.Left, out l);
            HandSample left = hasL ? Sample(l, head) : default;
            Push(time, right, left, head != null ? head.position : Vector3.zero, head != null ? head.rotation : Quaternion.identity);
        }

        public void Push(float time, HandSample right, HandSample left, Vector3 headPos, Quaternion headRot)
        {
            _frames.Add(new TrajFrame { Time = time, Right = right, Left = left, HeadPos = headPos, HeadRot = headRot });
            while (_frames.Count > 0 && time - _frames[0].Time > BufferSeconds) _frames.RemoveAt(0);
        }

        /// <summary>Converts one tracked hand to the body frame. The body frame follows the head slowly (low-pass).</summary>
        public HandSample Sample(HandFrame f, Transform head)
        {
            if (head != null)
            {
                var flat = Vector3.ProjectOnPlane(head.forward, Vector3.up);
                var yaw = flat.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(flat.normalized, Vector3.up) : _yaw;
                if (!_hasOrigin)
                {
                    _origin = head.position;
                    _yaw = yaw;
                    _hasOrigin = true;
                }
                else
                {
                    _origin = Vector3.Lerp(_origin, head.position, 0.1f);
                    _yaw = Quaternion.Slerp(_yaw, yaw, 0.1f);
                }
            }
            var inv = Quaternion.Inverse(_yaw);
            Vector3 P(XRHandJointID id) => inv * (f.Pos(id) - _origin);

            var feat = CurlCalculator.Compute(f, head);
            var s = new HandSample
            {
                Tracked = true,
                Wrist = P(XRHandJointID.Wrist),
                IndexTip = P(XRHandJointID.IndexTip),
                MiddleTip = P(XRHandJointID.MiddleTip),
                ThumbTip = P(XRHandJointID.ThumbTip),
                Curl = feat.Curl,
            };
            var knuckle = P(XRHandJointID.MiddleProximal);
            s.Palm = (s.Wrist + knuckle) * 0.5f;
            s.Size = Mathf.Max(0.04f, Vector3.Distance(s.Wrist, knuckle));
            s.Axis = (knuckle - s.Wrist).normalized;
            s.Normal = (inv * CurlCalculator.PalmNormalWorld(f)).normalized;
            return s;
        }
    }

    /// <summary>Result of scoring the buffer against one template (SPEC v3 §4.6 step 3).</summary>
    public struct SignEval
    {
        public SignTemplate Template;
        public bool Completed;       // the movement was done right (every part of the §4.6 feature met)
        public bool Performed;       // the core motion happened (fires an attempt), even if a required part was wrong
        public float CompletedTime;  // when it was first met
        public float Handshape, Location, Movement, Total; // each 0..1; Total = 0.4 H + 0.2 L + 0.4 M
        public float Measured;       // measured movement amplitude, in the template's unit
        public string Hint;          // movement hint (SPEC v3 §8.3); empty when the movement was fine
        public float[] CurlRight, CurlLeft;
        public bool HasRight, HasLeft;   // the hand was tracked at the start pose
        public Vector3 PalmRight;
        public int StartIndex;

        public float Confidence => Mathf.Clamp01(1f - Total);
    }

    /// <summary>Movement features (SPEC v3 §4.6 step 2) and the total distance.</summary>
    public static partial class SignScorer
    {
        public const float HandshapeWeight = 0.4f, LocationWeight = 0.2f, MovementWeight = 0.4f;
        public const float LocationScale = 0.20f; // location distance = error / 20 cm, capped at 1
        public const float StartPoseSeconds = 0.25f;

        // Body-frame anchors (relative to the eyes). The learner's body is not tracked; these are typical
        // seated-adult offsets from the headset and are deliberately generous.
        private static readonly Vector3 Temple = new Vector3(0.10f, 0.02f, 0.04f);
        private static readonly Vector3 Mouth = new Vector3(0f, -0.08f, 0.06f);
        private static readonly Vector3 Chin = new Vector3(0f, -0.11f, 0.06f);
        private const float ChestTop = -0.18f, ChestBottom = -0.62f, ChestNear = 0.08f, ChestFar = 0.65f, ChestHalfWidth = 0.38f;

        /// <summary>Hand raised into signing space (used to open an attempt window).</summary>
        public static bool InSigningSpace(in HandSample h) => h.Tracked && h.Palm.y > ChestBottom && h.Palm.z > 0.02f;

        /// <summary>Scores every frame from index 'from' against the template.</summary>
        public static SignEval Evaluate(SignTemplate t, IReadOnlyList<TrajFrame> frames, int from)
        {
            var e = new SignEval { Template = t, Hint = "", Movement = 1f, Location = 1f, Handshape = 1f, StartIndex = from };
            if (frames.Count - from < 3)
            {
                e.Total = 1f;
                return e;
            }

            var mv = DetectMovement(t, frames, from);
            e.Completed = mv.Completed;
            e.Performed = mv.Performed;
            e.CompletedTime = mv.CompletedTime;
            e.Measured = mv.Measured;
            e.Hint = mv.Hint ?? "";
            e.StartIndex = Mathf.Clamp(mv.Start, from, frames.Count - 1);
            e.Movement = t.movement.type == MovementTypes.None ? 0f : 1f - Mathf.Clamp01(mv.Ratio);

            StartPose(t, frames, e.StartIndex, out e.CurlRight, out e.CurlLeft, out e.PalmRight, out bool hasR, out bool hasL);
            e.HasRight = hasR;
            e.HasLeft = hasL;
            float hR = hasR ? TemplateStore.Distance(e.CurlRight, e.PalmRight, t) : 1f;
            if (t.TwoHanded)
            {
                float hL = hasL && t.HasLeftCurl ? TemplateStore.CurlDistance(e.CurlLeft, t.curlLeft) : 1f;
                e.Handshape = 0.5f * (hR + hL);
            }
            else e.Handshape = hR;

            // Start location: best over the first 0.5 s of the movement (the hand is still at its start spot then;
            // a single frame is too sensitive to where exactly the movement was judged to begin).
            e.Location = 1f;
            for (int i = e.StartIndex; i < frames.Count && frames[i].Time - frames[e.StartIndex].Time <= 0.5f; i++)
            {
                e.Location = Mathf.Min(e.Location, LocationError(t, frames[i]));
            }
            e.Total = HandshapeWeight * e.Handshape + LocationWeight * e.Location + MovementWeight * e.Movement;
            return e;
        }

        // ---------------- start pose ----------------

        private static void StartPose(SignTemplate t, IReadOnlyList<TrajFrame> f, int start, out float[] curlR, out float[] curlL,
                                      out Vector3 palmR, out bool hasR, out bool hasL)
        {
            curlR = new float[5];
            curlL = new float[5];
            palmR = Vector3.zero;
            int nR = 0, nL = 0;
            float t0 = f[start].Time;
            for (int i = start; i < f.Count && f[i].Time - t0 <= StartPoseSeconds; i++)
            {
                if (f[i].Right.Tracked)
                {
                    for (int k = 0; k < 5; k++) curlR[k] += f[i].Right.Curl[k];
                    palmR += f[i].Right.Normal;
                    nR++;
                }
                if (f[i].Left.Tracked)
                {
                    for (int k = 0; k < 5; k++) curlL[k] += f[i].Left.Curl[k];
                    nL++;
                }
            }
            hasR = nR > 0;
            hasL = nL > 0;
            for (int k = 0; k < 5; k++)
            {
                if (hasR) curlR[k] /= nR;
                if (hasL) curlL[k] /= nL;
            }
            palmR = palmR.sqrMagnitude > 1e-6f ? palmR.normalized : Vector3.forward;
        }

        // ---------------- location ----------------

        private static float ChestError(in HandSample h)
        {
            if (!h.Tracked) return LocationScale;
            var p = h.Palm;
            float dy = p.y > ChestTop ? p.y - ChestTop : p.y < ChestBottom ? ChestBottom - p.y : 0f;
            float dz = p.z < ChestNear ? ChestNear - p.z : p.z > ChestFar ? p.z - ChestFar : 0f;
            float dx = Mathf.Max(0f, Mathf.Abs(p.x) - ChestHalfWidth);
            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>Closest of palm centre / index tip / middle tip to a point.</summary>
        private static float NearDistance(in HandSample h, Vector3 point)
        {
            if (!h.Tracked) return 1f;
            return Mathf.Min(Vector3.Distance(h.Palm, point), Mathf.Min(Vector3.Distance(h.IndexTip, point), Vector3.Distance(h.MiddleTip, point)));
        }

        /// <summary>
        /// How much front-to-back (z) counts in stillness / location distances. 1 on device. The Simulator's webcam
        /// estimates depth from a single RGB camera and it jitters by 20+ cm, so the editor turns it down.
        /// </summary>
        public static float DepthWeight = 1f;

        /// <summary>
        /// Simulator-only jitter handling for "the bottom hand stays still" (COFFEE). 0 on device (spec values).
        /// The webcam reports a fist held still as moving ~30 cm, so the editor smooths the bottom-hand path over
        /// NoiseSmoothSeconds, measures a robust (90th-percentile) travel, and allows StillAllowance on top of 4 cm.
        /// </summary>
        public static float NoiseSmoothSeconds = 0f;
        public static float StillAllowance = 0f;

        /// <summary>Sets the Simulator values in the editor and the spec values everywhere else.</summary>
        public static void ConfigureForPlatform(float editorDepthWeight = 0.4f, float editorSmoothSeconds = 0.3f, float editorStillAllowance = 0.10f)
        {
            bool sim = Application.isEditor;
            DepthWeight = sim ? editorDepthWeight : 1f;
            NoiseSmoothSeconds = sim ? editorSmoothSeconds : 0f;
            StillAllowance = sim ? editorStillAllowance : 0f;
        }

        /// <summary>Recorded start location: allowed slack around where the learner started when recording.</summary>
        public const float RecordedStartRadius = 0.07f;

        private static float Dist(Vector3 a, Vector3 b)
        {
            var d = a - b;
            d.z *= DepthWeight;
            return d.magnitude;
        }

        /// <summary>Start-location distance 0..1 = how far outside the allowed region / 20 cm.</summary>
        public static float LocationError(SignTemplate t, in TrajFrame f)
        {
            float err;
            if (t.hasRecordedStart)
            {
                err = f.Right.Tracked ? Mathf.Max(0f, Dist(f.Right.Palm, t.recordedStartRight) - RecordedStartRadius) : LocationScale;
                if (t.TwoHanded)
                {
                    float l = f.Left.Tracked ? Mathf.Max(0f, Dist(f.Left.Palm, t.recordedStartLeft) - RecordedStartRadius) : LocationScale;
                    err = Mathf.Max(err, l);
                }
                return Mathf.Clamp01(err / LocationScale);
            }
            if (!string.IsNullOrEmpty(t.anchor)) return AnchorLocationError(t, f); // v3.1 words (SignScorerV31.cs)
            switch (t.movement.type)
            {
                case MovementTypes.Wave: err = Mathf.Max(0f, NearDistance(f.Right, Temple) - 0.20f); break;
                case MovementTypes.PushForward: err = Mathf.Max(0f, NearDistance(f.Right, Chin) - 0.15f); break;
                case MovementTypes.FlickOut: err = Mathf.Max(0f, NearDistance(f.Right, Mouth) - 0.12f); break;
                case MovementTypes.Crank:
                {
                    float apart = f.Right.Tracked && f.Left.Tracked ? Dist(f.Right.Palm, f.Left.Palm) : 1f;
                    err = Mathf.Max(Mathf.Max(ChestError(f.Right), ChestError(f.Left)), apart - 0.12f);
                    break;
                }
                case MovementTypes.Shake:
                case MovementTypes.Lift:
                    err = Mathf.Max(ChestError(f.Right), ChestError(f.Left));
                    break;
                default: err = ChestError(f.Right); break;
            }
            return Mathf.Clamp01(err / LocationScale);
        }

        // ---------------- movement ----------------

        private struct MoveResult
        {
            public bool Completed;
            public bool? PerformedFlag;
            /// <summary>The core motion happened (enough to count as an attempt), even if a required part was wrong.</summary>
            public bool Performed
            {
                get => PerformedFlag ?? Completed;
                set => PerformedFlag = value;
            }
            public float CompletedTime;
            public float Ratio;     // 0..1 measured / template amplitude (and other required parts)
            public float Measured;
            public int Start;
            public string Hint;
        }

        /// <summary>The spec threshold, scaled down if the learner recorded a smaller amplitude than the default.</summary>
        private static float Threshold(SignTemplate t, float specThreshold)
        {
            var c = TemplateStore.Canonical(t.intent);
            float def = c != null && c.movement.amplitude > 0f ? c.movement.amplitude : t.movement.amplitude;
            if (def <= 0f) return specThreshold;
            return specThreshold * Mathf.Min(1f, t.movement.amplitude / def);
        }

        private static MoveResult DetectMovement(SignTemplate t, IReadOnlyList<TrajFrame> f, int from)
        {
            switch (t.movement.type)
            {
                case MovementTypes.Wave: return Wave(t, f, from);
                case MovementTypes.WristNod: return WristNod(t, f, from);
                case MovementTypes.Snap: return Snap(t, f, from);
                case MovementTypes.PushForward: return PushForward(t, f, from);
                case MovementTypes.Crank: return Crank(t, f, from);
                case MovementTypes.FlickOut: return FlickOut(t, f, from);
                case MovementTypes.Shake: return Shake(t, f, from);
                case MovementTypes.Lift: return Lift(t, f, from);
                default: return DetectV31(t, f, from); // v3.1 types (SignScorerV31.cs)
            }
        }

        // wave: wrist yaw crosses zero >= 2 times in 1.2 s, amplitude >= 15 deg
        private static MoveResult Wave(SignTemplate t, IReadOnlyList<TrajFrame> f, int from)
        {
            // Only while the hand points up: near "down" the angle wraps around and would look like huge swings.
            var r = Oscillation(f, from, h => h.Right.Tracked && h.Right.Axis.y > 0.3f, h => Mathf.Atan2(h.Right.Axis.x, h.Right.Axis.y) * Mathf.Rad2Deg,
                                swingsNeeded: 2, maxSeconds: t.movement.durationSec * 1.5f, hysteresis: 6f, halfAmplitude: true,
                                threshold: Threshold(t, 15f), templateAmp: t.movement.amplitude);
            if (!r.Completed) r.Hint = "Wave it — side to side, twice";
            return r;
        }

        // wrist-nod: wrist pitch crosses zero >= 2 times in 1.0 s, amplitude >= 15 deg
        private static MoveResult WristNod(SignTemplate t, IReadOnlyList<TrajFrame> f, int from)
        {
            var r = Oscillation(f, from, h => h.Right.Tracked && h.Right.Axis.y > 0.3f, h => Mathf.Atan2(h.Right.Axis.z, h.Right.Axis.y) * Mathf.Rad2Deg,
                                swingsNeeded: 2, maxSeconds: t.movement.durationSec * 1.5f, hysteresis: 6f, halfAmplitude: true,
                                threshold: Threshold(t, 15f), templateAmp: t.movement.amplitude);
            if (!r.Completed) r.Hint = "Nod your wrist — up and down, twice";
            return r;
        }

        // snap: index + middle tip to thumb tip distance shrinks >= 40 % within 0.4 s
        private static MoveResult Snap(SignTemplate t, IReadOnlyList<TrajFrame> f, int from)
        {
            var r = new MoveResult { Start = from, Hint = "Snap your fingers down onto your thumb" };
            float window = Mathf.Max(0.4f, t.movement.durationSec) + 0.2f; // tracking-lag margin
            float best = 0f;
            for (int i = from; i < f.Count; i++)
            {
                var a = f[i].Right;
                if (!a.Tracked) continue;
                // Start shape: index + middle out, ring + pinky down (the NO handshape), so closing a fist is not a snap.
                if (a.Curl[1] > 0.45f || a.Curl[2] > 0.45f || a.Curl[3] < 0.5f || a.Curl[4] < 0.5f) continue;
                float d0 = SnapGap(a);
                if (d0 < 1e-3f) continue;
                for (int j = i + 1; j < f.Count && f[j].Time - f[i].Time <= window; j++)
                {
                    if (!f[j].Right.Tracked) continue;
                    float shrink = (d0 - SnapGap(f[j].Right)) / d0;
                    if (shrink > best)
                    {
                        best = shrink;
                        r.Start = i;
                    }
                    if (!r.Completed && shrink * 100f >= Threshold(t, 40f))
                    {
                        r.Completed = true;
                        r.CompletedTime = f[j].Time;
                    }
                }
            }
            r.Measured = best * 100f;
            r.Ratio = Mathf.Clamp01(r.Measured / Mathf.Max(1f, t.movement.amplitude));
            if (r.Completed) r.Hint = "";
            return r;
        }

        private static float SnapGap(in HandSample h) =>
            0.5f * (Vector3.Distance(h.IndexTip, h.ThumbTip) + Vector3.Distance(h.MiddleTip, h.ThumbTip)) / h.Size;

        // push-forward: hand displaces >= 12 cm toward the partner, starting < 15 cm from the chin
        private static MoveResult PushForward(SignTemplate t, IReadOnlyList<TrajFrame> f, int from)
        {
            var r = Displacement(t, f, from, startOk: h => NearDistance(h, Chin) <= 0.15f, maxSeconds: 1.5f,
                                 measure: d => d.z, threshold: Threshold(t, 12f));
            if (!r.Completed) r.Hint = r.Start < 0 ? "Start at your chin" : "Push forward from your chin";
            if (r.Start < 0) r.Start = Closest(f, from, h => NearDistance(h, Chin));
            return r;
        }

        // flick-out: starts at the mouth (< 12 cm), moves outward + downward >= 15 cm in < 0.7 s
        private static MoveResult FlickOut(SignTemplate t, IReadOnlyList<TrajFrame> f, int from)
        {
            var r = Displacement(t, f, from, startOk: h => NearDistance(h, Mouth) <= 0.12f, maxSeconds: 0.7f + 0.2f,
                                 measure: d => d.y < -0.03f && d.z > -0.02f ? d.magnitude : 0f, threshold: Threshold(t, 15f));
            if (!r.Completed) r.Hint = r.Start < 0 ? "Start at your mouth" : "Flick faster, from your mouth outward";
            if (r.Start < 0) r.Start = Closest(f, from, h => NearDistance(h, Mouth));
            return r;
        }

        // crank: dominant wrist roll >= 30 deg x 2 (or two small circles), non-dominant moves < 4 cm, hands < 12 cm apart
        private static MoveResult Crank(SignTemplate t, IReadOnlyList<TrajFrame> f, int from)
        {
            int reps = Mathf.Max(1, t.movement.repeats);
            float maxSeconds = t.movement.durationSec * 1.6f;
            // Only judge the crank once the fists are together (not while the hands are still coming up into place).
            int seg = from;
            while (seg < f.Count - 1 && !(f[seg].Right.Tracked && Together(f[seg]))) seg++;
            var roll = Oscillation(f, seg, h => h.Right.Tracked, null, swingsNeeded: reps, maxSeconds: maxSeconds,
                                   hysteresis: 8f, halfAmplitude: false, threshold: Threshold(t, 30f),
                                   templateAmp: t.movement.amplitude, series: RollSeries(f, seg));
            var circle = Circles(f, seg, maxSeconds, out int circleStart, out float circleEnd);
            float circleRatio = Mathf.Clamp01(circle / reps);
            bool circleDone = circle >= reps * 0.75f;

            var r = new MoveResult { Measured = roll.Measured };
            r.Performed = roll.Completed || circleDone;
            r.Completed = r.Performed;
            r.CompletedTime = roll.Completed ? roll.CompletedTime : circleEnd;
            r.Start = roll.Completed || !circleDone ? roll.Start : circleStart;
            float motion = Mathf.Max(roll.Ratio, circleRatio);

            // The bottom hand must stay still while the top hand cranks (judged over the crank itself).
            r.Start = Mathf.Max(r.Start, seg);
            float crankSeconds = r.Completed ? Mathf.Min(maxSeconds, r.CompletedTime - f[r.Start].Time + 0.1f) : maxSeconds;
            float bottomMove = NoiseSmoothSeconds > 0f
                ? RobustTravel(f, r.Start, left: true, seconds: crankSeconds)
                : MaxTravel(f, r.Start, left: true, seconds: crankSeconds);
            bool hasLeft = bottomMove >= 0f;
            float limit = 0.04f + StillAllowance; // spec 4 cm; Simulator adds its measured jitter allowance
            float still = !hasLeft ? 0f : bottomMove <= limit ? 1f : Mathf.Clamp01(1f - (bottomMove - limit) / 0.02f);
            r.Ratio = motion * still;
            // The crank is only complete with the bottom hand still (SPEC v3 §4.6): cranking both hands is performed but wrong.
            r.Completed = r.Performed && hasLeft && bottomMove <= limit;

            if (!hasLeft) r.Hint = "Use both fists — one on top of the other";
            else if (bottomMove > limit) r.Hint = $"Bottom hand: keep it still (moved {bottomMove * 100f:0} cm, should be < {limit * 100f:0} cm)";
            else if (!r.Completed) r.Hint = $"Crank more — 30° × 2 (only {roll.Measured:0}°)";
            else r.Hint = "";
            return r;
        }

        /// <summary>COFFEE start: fists close together (or only the right hand seen, so a one-hand attempt still gets a hint).</summary>
        private static bool Together(in TrajFrame a) => !a.Left.Tracked || Dist(a.Right.Palm, a.Left.Palm) < 0.15f;

        // shake: both hands oscillate >= 3 times in 1.0 s, amplitude >= 2.5 cm
        private static MoveResult Shake(SignTemplate t, IReadOnlyList<TrajFrame> f, int from)
        {
            int cycles = Mathf.Max(3, Mathf.RoundToInt(t.movement.repeats * 0.6f)); // spec: >= 3 oscillations
            float maxSeconds = 1.0f + 0.3f;
            float thr = Threshold(t, 2.5f) * 0.01f;
            var right = Oscillation(f, from, h => h.Right.Tracked, h => h.Right.Palm.x, swingsNeeded: cycles * 2,
                                    maxSeconds: maxSeconds, hysteresis: 0.008f, halfAmplitude: false, threshold: thr,
                                    templateAmp: t.movement.amplitude * 0.01f, smooth: false);
            var left = Oscillation(f, from, h => h.Left.Tracked, h => h.Left.Palm.x, swingsNeeded: cycles * 2,
                                   maxSeconds: maxSeconds, hysteresis: 0.008f, halfAmplitude: false, threshold: thr,
                                   templateAmp: t.movement.amplitude * 0.01f, smooth: false);
            var r = new MoveResult
            {
                Completed = right.Completed && left.Completed,
                CompletedTime = Mathf.Max(right.CompletedTime, left.CompletedTime),
                Ratio = Mathf.Min(right.Ratio, left.Ratio),
                Measured = Mathf.Min(right.Measured, left.Measured) * 100f,
                Start = Mathf.Min(right.Start, left.Start),
            };
            if (r.Completed) r.Hint = "";
            else if (!left.Completed && right.Completed || left.Completed && !right.Completed) r.Hint = "Shake both fists together";
            else if (right.Measured < thr && right.Swings >= 2) r.Hint = "Shake bigger — 3–5 cm";
            else r.Hint = $"Shake faster — 4–6 times per second{(right.Frequency > 0.1f ? $" (yours: {right.Frequency:0} Hz)" : "")}";
            return r;
        }

        // lift: both hands rise together >= 8 cm, dominant staying < 8 cm above the non-dominant palm
        private static MoveResult Lift(SignTemplate t, IReadOnlyList<TrajFrame> f, int from)
        {
            var r = new MoveResult { Start = -1, Hint = "Lift both hands up together" };
            float thr = Threshold(t, 8f) * 0.01f;
            float best = 0f;
            bool everTogether = false;
            for (int i = from; i < f.Count; i++)
            {
                var a = f[i];
                if (!a.Right.Tracked || !a.Left.Tracked) continue;
                if (!Stacked(a)) continue;
                everTogether = true;
                for (int j = i + 1; j < f.Count && f[j].Time - a.Time <= 1.5f; j++)
                {
                    var b = f[j];
                    if (!b.Right.Tracked || !b.Left.Tracked) continue;
                    float rise = Mathf.Min(b.Right.Palm.y - a.Right.Palm.y, b.Left.Palm.y - a.Left.Palm.y);
                    if (!Stacked(b)) rise *= 0.5f; // hands came apart
                    if (rise > best)
                    {
                        best = rise;
                        r.Start = i;
                    }
                    if (!r.Completed && rise >= thr)
                    {
                        r.Completed = true;
                        r.CompletedTime = b.Time;
                    }
                }
            }
            r.Measured = best * 100f;
            r.Ratio = Mathf.Clamp01(r.Measured / Mathf.Max(1f, t.movement.amplitude));
            if (r.Completed) r.Hint = "";
            else if (!everTogether) r.Hint = "Rest your fist on your flat palm, then lift both";
            if (r.Start < 0) r.Start = from;
            return r;
        }

        private static bool Stacked(in TrajFrame a)
        {
            var d = a.Right.Palm - a.Left.Palm;
            return d.y > -0.03f && d.y < 0.12f && new Vector2(d.x, d.z).magnitude < 0.12f;
        }

        // ---------------- helpers ----------------

        /// <summary>Oscillation result: swings are moves between turning points (hysteresis-filtered).</summary>
        private struct OscResult
        {
            public bool Completed;
            public float CompletedTime;
            public float Ratio, Measured, Frequency;
            public int Start, Swings;
            public string Hint;

            public static implicit operator MoveResult(OscResult o) => new MoveResult
            {
                Completed = o.Completed, CompletedTime = o.CompletedTime, Ratio = o.Ratio, Measured = o.Measured, Start = o.Start, Hint = o.Hint,
            };
        }

        private static OscResult Oscillation(IReadOnlyList<TrajFrame> f, int from, System.Func<TrajFrame, bool> has, System.Func<TrajFrame, float> value,
                                             int swingsNeeded, float maxSeconds, float hysteresis, bool halfAmplitude,
                                             float threshold, float templateAmp, List<(float v, float t, int i)> series = null,
                                             bool smooth = true)
        {
            var res = new OscResult { Start = from };
            if (series == null)
            {
                series = new List<(float, float, int)>();
                for (int i = from; i < f.Count; i++)
                {
                    if (has(f[i])) series.Add((value(f[i]), f[i].Time, i));
                }
                // Smoothing helps noisy angles, but at ~30 fps a 3-frame average cuts a 5 Hz shake to 2/3: skip it there.
                if (smooth) Smooth(series);
            }
            if (series.Count < 4)
            {
                res.Ratio = 0f;
                return res;
            }

            // Turning points with hysteresis.
            var turns = new List<(float v, float t, int i)>();
            float ext = series[0].v, refV = series[0].v;
            int extI = 0, dir = 0;
            for (int k = 1; k < series.Count; k++)
            {
                float x = series[k].v;
                if (dir == 0)
                {
                    if (x - refV >= hysteresis) dir = 1;
                    else if (refV - x >= hysteresis) dir = -1;
                    if (dir != 0)
                    {
                        turns.Add(series[0]); // movement start
                        ext = x;
                        extI = k;
                    }
                    continue;
                }
                if (dir > 0 ? x > ext : x < ext)
                {
                    ext = x;
                    extI = k;
                }
                else if (Mathf.Abs(ext - x) >= hysteresis)
                {
                    turns.Add(series[extI]);
                    dir = -dir;
                    ext = x;
                    extI = k;
                }
            }

            // Full swings between turning points (the first "turn" is the start, so its swing is a half swing: skip it).
            var swings = new List<(float amp, float t0, float t1, int i0)>();
            for (int k = 2; k < turns.Count; k++)
            {
                swings.Add((Mathf.Abs(turns[k].v - turns[k - 1].v), turns[k - 1].t, turns[k].t, turns[k - 1].i));
            }
            // An ongoing final swing counts once it has moved past the hysteresis.
            if (turns.Count >= 2 && Mathf.Abs(ext - turns[turns.Count - 1].v) >= hysteresis)
            {
                swings.Add((Mathf.Abs(ext - turns[turns.Count - 1].v), turns[turns.Count - 1].t, series[extI].t, turns[turns.Count - 1].i));
            }
            res.Swings = swings.Count;

            float bestAmp = 0f, bestCount = 0f;
            for (int k = 0; k < swings.Count; k++)
            {
                int n = 0;
                float sum = 0f, min = float.MaxValue;
                for (int m = k; m < swings.Count && swings[m].t1 - swings[k].t0 <= maxSeconds && n < swingsNeeded; m++)
                {
                    n++;
                    sum += swings[m].amp;
                    min = Mathf.Min(min, swings[m].amp);
                    float amp = (halfAmplitude ? 0.5f : 1f) * sum / n;
                    if (n > bestCount || n == bestCount && amp > bestAmp)
                    {
                        bestCount = n;
                        bestAmp = amp;
                        res.Start = swings[k].i0;
                        float span = swings[m].t1 - swings[k].t0;
                        res.Frequency = span > 0.05f ? 0.5f * n / span : 0f;
                    }
                    if (n == swingsNeeded && amp >= threshold && !res.Completed)
                    {
                        res.Completed = true;
                        res.CompletedTime = swings[m].t1;
                    }
                }
            }
            res.Measured = bestAmp;
            res.Ratio = Mathf.Clamp01(bestCount / swingsNeeded) * Mathf.Clamp01(bestAmp / Mathf.Max(1e-4f, templateAmp));
            return res;
        }

        private static void Smooth(List<(float v, float t, int i)> s)
        {
            if (s.Count < 3) return;
            var copy = s.ToArray();
            for (int k = 1; k < s.Count - 1; k++) s[k] = ((copy[k - 1].v + copy[k].v + copy[k + 1].v) / 3f, copy[k].t, copy[k].i);
        }

        /// <summary>Integrated roll of the right hand about its own long axis, degrees.</summary>
        private static List<(float v, float t, int i)> RollSeries(IReadOnlyList<TrajFrame> f, int from)
        {
            var s = new List<(float, float, int)>();
            float roll = 0f;
            Vector3 prevN = Vector3.zero;
            for (int i = from; i < f.Count; i++)
            {
                var h = f[i].Right;
                if (!h.Tracked) continue;
                if (prevN != Vector3.zero)
                {
                    var a = Vector3.ProjectOnPlane(prevN, h.Axis);
                    var b = Vector3.ProjectOnPlane(h.Normal, h.Axis);
                    if (a.sqrMagnitude > 1e-4f && b.sqrMagnitude > 1e-4f) roll += Vector3.SignedAngle(a, b, h.Axis);
                }
                prevN = h.Normal;
                s.Add((roll, f[i].Time, i));
            }
            Smooth(s);
            return s;
        }

        /// <summary>Number of horizontal circles drawn by the right palm within maxSeconds (radius >= 1.2 cm).</summary>
        private static float Circles(IReadOnlyList<TrajFrame> f, int from, float maxSeconds, out int start, out float endTime)
        {
            start = from;
            endTime = 0f;
            float best = 0f;
            int last = f.Count - 1;
            // Look back from the newest frame over maxSeconds.
            int first = last;
            while (first > from && f[last].Time - f[first - 1].Time <= maxSeconds) first--;
            var c = Vector2.zero;
            int n = 0;
            for (int i = first; i <= last; i++)
            {
                if (!f[i].Right.Tracked) continue;
                c += new Vector2(f[i].Right.Palm.x, f[i].Right.Palm.z);
                n++;
            }
            if (n < 6) return 0f;
            c /= n;
            float total = 0f, prevA = float.NaN;
            for (int i = first; i <= last; i++)
            {
                if (!f[i].Right.Tracked) continue;
                var p = new Vector2(f[i].Right.Palm.x, f[i].Right.Palm.z) - c;
                if (p.magnitude < 0.012f) continue;
                float a = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
                if (!float.IsNaN(prevA)) total += Mathf.DeltaAngle(prevA, a);
                prevA = a;
                float turns = Mathf.Abs(total) / 360f;
                if (turns > best)
                {
                    best = turns;
                    endTime = f[i].Time;
                }
            }
            start = first;
            return best;
        }

        /// <summary>Largest distance the hand moves from where it was at 'start', within 'seconds'; -1 when not tracked.</summary>
        private static float MaxTravel(IReadOnlyList<TrajFrame> f, int start, bool left, float seconds)
        {
            Vector3? p0 = null;
            float max = -1f;
            for (int i = start; i < f.Count && f[i].Time - f[start].Time <= seconds; i++)
            {
                var h = left ? f[i].Left : f[i].Right;
                if (!h.Tracked) continue;
                if (p0 == null) p0 = h.Palm;
                max = Mathf.Max(max, Dist(h.Palm, p0.Value));
            }
            return max;
        }

        /// <summary>
        /// Simulator-only travel: the hand path is smoothed over NoiseSmoothSeconds, then the 90th-percentile distance
        /// from the median position is taken (x2, so a real back-and-forth reads like its full travel). -1 when not tracked.
        /// </summary>
        private static float RobustTravel(IReadOnlyList<TrajFrame> f, int start, bool left, float seconds)
        {
            var pts = new List<(float t, Vector3 p)>();
            for (int i = start; i < f.Count && f[i].Time - f[start].Time <= seconds; i++)
            {
                var h = left ? f[i].Left : f[i].Right;
                if (h.Tracked) pts.Add((f[i].Time, new Vector3(h.Palm.x, h.Palm.y, h.Palm.z * DepthWeight)));
            }
            if (pts.Count == 0) return -1f;
            if (pts.Count < 3) return 0f;
            var sm = new List<Vector3>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 sum = Vector3.zero;
                int n = 0;
                for (int j = i; j >= 0 && pts[i].t - pts[j].t <= NoiseSmoothSeconds; j--)
                {
                    sum += pts[j].p;
                    n++;
                }
                sm.Add(sum / n);
            }
            float Median(System.Func<Vector3, float> c)
            {
                var v = sm.ConvertAll(x => c(x));
                v.Sort();
                return v[v.Count / 2];
            }
            var med = new Vector3(Median(p => p.x), Median(p => p.y), Median(p => p.z));
            var d = sm.ConvertAll(p => Vector3.Distance(p, med));
            d.Sort();
            return 2f * d[Mathf.Min(d.Count - 1, Mathf.FloorToInt(d.Count * 0.9f))];
        }

        private static MoveResult Displacement(SignTemplate t, IReadOnlyList<TrajFrame> f, int from, System.Func<HandSample, bool> startOk,
                                               float maxSeconds, System.Func<Vector3, float> measure, float threshold)
        {
            var r = new MoveResult { Start = -1 };
            float best = 0f;
            for (int i = from; i < f.Count; i++)
            {
                if (!f[i].Right.Tracked || !startOk(f[i].Right)) continue;
                for (int j = i + 1; j < f.Count && f[j].Time - f[i].Time <= maxSeconds; j++)
                {
                    if (!f[j].Right.Tracked) continue;
                    float m = measure(f[j].Right.Palm - f[i].Right.Palm) * 100f;
                    if (m > best)
                    {
                        best = m;
                        r.Start = i;
                    }
                    if (!r.Completed && m >= threshold)
                    {
                        r.Completed = true;
                        r.CompletedTime = f[j].Time;
                    }
                }
            }
            r.Measured = best;
            r.Ratio = Mathf.Clamp01(best / Mathf.Max(1f, t.movement.amplitude));
            return r;
        }

        private static int Closest(IReadOnlyList<TrajFrame> f, int from, System.Func<HandSample, float> dist)
        {
            int best = from;
            float bd = float.MaxValue;
            for (int i = from; i < f.Count; i++)
            {
                if (!f[i].Right.Tracked) continue;
                float d = dist(f[i].Right);
                if (d < bd)
                {
                    bd = d;
                    best = i;
                }
            }
            return best;
        }
    }
}
