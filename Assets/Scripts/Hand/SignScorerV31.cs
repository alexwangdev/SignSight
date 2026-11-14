using System.Collections.Generic;
using UnityEngine;

namespace SignSight.Hand
{
    /// <summary>
    /// v3.1 movement detectors for the 42 new words (tap, stroke, circle, twist, turn, squeeze, converge, hold, rock)
    /// and their anchor-based location. The 8 v3 signs never reach this file: their movement types and location rules
    /// are unchanged in TrajectoryTracker.cs.
    /// </summary>
    public static partial class SignScorer
    {
        /// <summary>Contact point within this distance of an anchor counts as touching it.</summary>
        /// <summary>A tap: the contact point within TapNear of the anchor, then lifting off by TapLift.</summary>
        public const float TapNear = 0.06f;
        private const float TapLift = 0.015f;
        /// <summary>
        /// A stroke starts where the hand pauses (a local minimum of speed, below StrokeStartSpeed), so passing through
        /// another sign's start spot (WOMAN's chin on the way down from MAN's forehead) is not a start.
        /// </summary>
        private const float StrokeStartSpeed = 0.6f; // m/s
        /// <summary>A stroke is a roughly straight path: path length at most this times the straight-line travel.</summary>
        private const float StrokeStraightness = 1.3f; // a half circle is 1.57
        /// <summary>A turn happens at the wrist: the palm moves at most this far meanwhile (NEED vs. arm movements).</summary>
        private const float TurnMaxTravel = 0.10f;
        private const float EndPalmDegrees = 50f;

        // ---------------- location ----------------

        private static float AnchorDist(SignTemplate t, in TrajFrame f, bool left)
        {
            var hand = left ? f.Left : f.Right;
            if (!hand.Tracked || string.IsNullOrEmpty(left ? t.leftAnchor : t.anchor)) return 1f;
            Vector3 p;
            bool ok = left ? SignAnchors.LeftTarget(t, out p) : SignAnchors.RightTarget(t, f, out p);
            if (!ok) return 1f;
            return Dist(SignAnchors.Contact(hand, left ? t.leftContact : t.contact), p);
        }

        /// <summary>Location for templates with a named anchor (v3.1): contact point to anchor, minus the allowed slack.</summary>
        private static float AnchorLocationError(SignTemplate t, in TrajFrame f)
        {
            float err = f.Right.Tracked ? Mathf.Max(0f, AnchorDist(t, f, false) - t.anchorRadius) : LocationScale;
            if (t.TwoHanded)
            {
                if (!f.Left.Tracked) err = Mathf.Max(err, LocationScale * 0.5f);
                else if (SignAnchors.HasLeftTarget(t))
                    err = Mathf.Max(err, Mathf.Max(0f, AnchorDist(t, f, true) - t.anchorRadius));
            }
            return Mathf.Clamp01(err / LocationScale);
        }

        // ---------------- movement ----------------

        private static MoveResult DetectV31(SignTemplate t, IReadOnlyList<TrajFrame> f, int from)
        {
            var r = DetectV31Raw(t, f, from);
            // A movement that did not complete (wrong direction, wrong end palm, too few repeats) never scores as perfect.
            if (!r.Completed) r.Ratio = Mathf.Min(r.Ratio, 0.6f);
            return r;
        }

        private static MoveResult DetectV31Raw(SignTemplate t, IReadOnlyList<TrajFrame> f, int from)
        {
            switch (t.movement.type)
            {
                case MovementTypes.Tap: return Tap(t, f, from);
                case MovementTypes.Stroke: return Stroke(t, f, from);
                case MovementTypes.Circle: return CircleMove(t, f, from);
                case MovementTypes.Twist: return Twist(t, f, from);
                case MovementTypes.Turn: return Turn(t, f, from);
                case MovementTypes.Squeeze: return Squeeze(t, f, from);
                case MovementTypes.Converge: return Converge(t, f, from);
                case MovementTypes.Hold: return Hold(t, f, from);
                case MovementTypes.Rock: return Rock(t, f, from);
                default: return new MoveResult { Completed = false, Ratio = 0f, Start = from };
            }
        }

        private static float MaxSeconds(SignTemplate t, float k = 1.8f) => Mathf.Max(0.6f, t.movement.durationSec * k + 0.4f);

        // tap: the contact point comes within TapNear of the anchor and lifts off again by >= TapLift, 'repeats' times
        // (a dip in the contact-to-anchor distance, so a few cm of tracking offset does not matter). Two-handed signs
        // with a body anchor for the left hand (SICK) need the left hand to touch too. One touch held for 0.35 s also
        // counts when one touch is enough.
        private static MoveResult Tap(SignTemplate t, IReadOnlyList<TrajFrame> f, int from)
        {
            int need = Mathf.Max(1, t.movement.repeats);
            bool leftToo = SignAnchors.HasLeftTarget(t) && t.movement.movingHand == "both";
            var r = new MoveResult { Start = -1 };
            float near = TapNear + StillAllowance * 0.5f;
            var right = new DipCounter(near);
            var left = new DipCounter(near);
            float minD = 1f;
            for (int i = from; i < f.Count; i++)
            {
                float d = AnchorDist(t, f[i], false);
                if (f[i].Right.Tracked) minD = Mathf.Min(minD, d);
                if (right.Step(d, f[i].Time) && r.Start < 0) r.Start = i;
                if (leftToo) left.Step(AnchorDist(t, f[i], true), f[i].Time);
                bool done = right.Dips >= need && (!leftToo || left.Dips >= need);
                if (!done && need == 1 && right.HeldFor(f[i].Time) >= 0.35f && (!leftToo || left.HeldFor(f[i].Time) >= 0.35f)) done = true;
                if (done && !r.Completed)
                {
                    r.Completed = true;
                    r.CompletedTime = f[i].Time;
                }
            }
            int touches = right.Dips;
            r.Measured = touches;
            r.Ratio = r.Completed ? 1f : touches > 0 ? Mathf.Clamp01((float)touches / need) * 0.8f
                : 0.5f * Mathf.Clamp01(1f - (minD - near) / 0.15f);
            if (r.Start < 0) r.Start = from;
            r.Hint = r.Completed ? "" : touches == 0
                ? $"Touch your {AnchorWord(t.anchor)} with your {ContactWord(t.contact)}"
                : $"Tap {Times(need)} — you tapped {Times(touches)}";
            return r;
        }

        /// <summary>Counts dips of a distance below 'near' that are followed by a lift of TapLift (hysteresis).</summary>
        private class DipCounter
        {
            private readonly float _near;
            private bool _armed = true;
            private float _valley = float.MaxValue, _peak = float.MinValue, _nearSince = float.NaN;
            public int Dips;

            public DipCounter(float near) => _near = near;

            /// <summary>Returns true on the frame the first contact (distance below near) happens.</summary>
            public bool Step(float d, float time)
            {
                if (d >= 1f) return false; // not tracked
                bool first = false;
                if (d < _near)
                {
                    if (float.IsNaN(_nearSince)) { _nearSince = time; first = Dips == 0; }
                }
                else _nearSince = float.NaN;
                if (_armed)
                {
                    _valley = Mathf.Min(_valley, d);
                    if (_valley < _near && d > _valley + TapLift)
                    {
                        Dips++;
                        _armed = false;
                        _peak = d;
                    }
                }
                else
                {
                    _peak = Mathf.Max(_peak, d);
                    if (d < _peak - TapLift)
                    {
                        _armed = true;
                        _valley = d;
                    }
                }
                return first;
            }

            public float HeldFor(float time) => float.IsNaN(_nearSince) ? 0f : time - _nearSince;
        }

        // stroke: from near the anchor, the palm travels >= 60 % of 'amplitude' along 'direction'. When both hands move
        // (movingHand = both) the left hand must travel the mirrored direction too.
        private static MoveResult Stroke(SignTemplate t, IReadOnlyList<TrajFrame> f, int from)
        {
            var dir = t.direction.sqrMagnitude > 1e-6f ? t.direction.normalized : Vector3.down;
            var dirL = new Vector3(-dir.x, dir.y, dir.z);
            bool both = t.TwoHanded && t.movement.movingHand == "both";
            float amp = Mathf.Max(1f, t.movement.amplitude);
            float thr = Threshold(t, amp * 0.6f);
            float maxS = MaxSeconds(t);
            float startSlack = t.anchorRadius + 0.04f + StillAllowance * 0.5f;
            var r = new MoveResult { Start = -1 };
            float best = 0f;
            bool endPalm = t.endPalm.sqrMagnitude > 1e-4f;
            bool wrongEnd = false;
            for (int i = from; i < f.Count; i++)
            {
                var a = f[i];
                if (!a.Right.Tracked || (both && !a.Left.Tracked)) continue;
                if (AnchorDist(t, a, false) > startSlack) continue;
                if (!IsPause(f, i)) continue;
                float path = 0f;
                Vector3 prev = a.Right.Palm;
                for (int j = i + 1; j < f.Count && f[j].Time - a.Time <= maxS; j++)
                {
                    var b = f[j];
                    if (!b.Right.Tracked) continue;
                    path += Scaled(b.Right.Palm - prev).magnitude;
                    prev = b.Right.Palm;
                    float m = Along(b.Right.Palm - a.Right.Palm, dir);
                    // A curved path (e.g. a circle that dips down) is not a stroke.
                    if (path > StrokeStraightness * Scaled(b.Right.Palm - a.Right.Palm).magnitude + 0.02f) m *= 0.5f;
                    if (both)
                    {
                        if (!b.Left.Tracked) continue;
                        m = Mathf.Min(m, Along(b.Left.Palm - a.Left.Palm, dirL));
                    }
                    bool endOk = !endPalm || Vector3.Angle(b.Right.Normal, t.endPalm) <= EndPalmDegrees;
                    if (!endOk) m *= 0.7f;
                    if (m > best)
                    {
                        best = m;
                        r.Start = i;
                        wrongEnd = !endOk;
                    }
                    if (!r.Completed && m >= thr && endOk)
                    {
                        r.Completed = true;
                        r.CompletedTime = b.Time;
                    }
                }
            }
            r.Measured = best;
            r.Ratio = Mathf.Clamp01(best / amp);
            bool started = r.Start >= 0;
            if (!started) r.Start = from;
            r.Hint = r.Completed ? ""
                : !started ? $"Start at your {AnchorWord(t.anchor)} — pause there, then move"
                : wrongEnd && best >= thr * 0.7f ? $"End with your palm facing {PalmWord(t.endPalm)}"
                : $"Move {DirectionWord(dir)} more{(both ? " with both hands" : "")} — {best:0} cm of about {amp:0} cm";
            return r;
        }

        // circle: the contact point's path turns through >= 75 % of 360° × repeats (turning of the travel direction, in the plane
        // named by 'axis'), and the circle is at least 3 cm across.
        private static MoveResult CircleMove(SignTemplate t, IReadOnlyList<TrajFrame> f, int from)
        {
            int reps = Mathf.Max(1, t.movement.repeats);
            float need = 360f * reps * 0.75f;
            float maxS = MaxSeconds(t);
            string plane = t.movement.axis;
            var r = new MoveResult { Start = from };
            float best = 0f;
            for (int i = from; i < f.Count; i++)
            {
                if (!f[i].Right.Tracked) continue;
                Vector2 last = Project(SignAnchors.Contact(f[i].Right, t.contact), plane);
                Vector2 prevStep = Vector2.zero;
                float turn = 0f;
                Vector2 min = last, max = last;
                for (int j = i + 1; j < f.Count && f[j].Time - f[i].Time <= maxS; j++)
                {
                    if (!f[j].Right.Tracked) continue;
                    var p = Project(SignAnchors.Contact(f[j].Right, t.contact), plane);
                    var step = p - last;
                    if (step.magnitude < 0.006f) continue;
                    if (prevStep != Vector2.zero) turn += Vector2.SignedAngle(prevStep, step);
                    prevStep = step;
                    last = p;
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                    float size = (max - min).magnitude;
                    float a = Mathf.Abs(turn);
                    if (size < 0.03f) continue;
                    if (a > best)
                    {
                        best = a;
                        r.Start = i;
                    }
                    if (!r.Completed && a >= need)
                    {
                        r.Completed = true;
                        r.CompletedTime = f[j].Time;
                    }
                }
            }
            r.Measured = best / 360f;
            r.Ratio = Mathf.Clamp01(best / (360f * reps));
            r.Hint = r.Completed ? "" : $"Make {(reps > 1 ? $"{reps} circles" : "a full circle")} — you made {r.Measured:0.0}";
            return r;
        }

        private static Vector2 Project(Vector3 p, string plane)
        {
            switch (plane)
            {
                case "sagittal": return new Vector2(p.z * DepthWeight, p.y);
                case "horizontal": return new Vector2(p.x, p.z * DepthWeight);
                default: return new Vector2(p.x, p.y);
            }
        }

        // twist: the wrist rolls back and forth about the hand axis, >= 60 % of 'amplitude' degrees, 'repeats' swings.
        private static MoveResult Twist(SignTemplate t, IReadOnlyList<TrajFrame> f, int from)
        {
            int swings = Mathf.Max(2, t.movement.repeats);
            var r = Oscillation(f, from, h => h.Right.Tracked, h => Roll(h.Right), swingsNeeded: swings,
                                maxSeconds: MaxSeconds(t, 1.6f), hysteresis: 6f, halfAmplitude: true,
                                threshold: Threshold(t, t.movement.amplitude * 0.6f), templateAmp: t.movement.amplitude);
            if (!r.Completed) r.Hint = r.Swings < swings ? $"Twist your wrist back and forth — {Times(Mathf.Max(1, t.movement.repeats))}"
                : $"Twist further — about {t.movement.amplitude:0}° each way";
            return r;
        }

        private static float Roll(in HandSample h)
        {
            var axis = h.Axis.sqrMagnitude > 1e-6f ? h.Axis : Vector3.up;
            var refDir = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.8f ? Vector3.forward : Vector3.up;
            var a = Vector3.ProjectOnPlane(refDir, axis);
            var b = Vector3.ProjectOnPlane(h.Normal, axis);
            if (a.sqrMagnitude < 1e-6f || b.sqrMagnitude < 1e-6f) return 0f;
            return Vector3.SignedAngle(a, b, axis);
        }

        // turn: the palm direction turns once by >= 65 % of 'amplitude' degrees (both hands when movingHand = both).
        private static MoveResult Turn(SignTemplate t, IReadOnlyList<TrajFrame> f, int from)
        {
            bool both = t.TwoHanded && t.movement.movingHand == "both";
            float amp = Mathf.Max(5f, t.movement.amplitude);
            float thr = Threshold(t, amp * 0.65f);
            float maxS = MaxSeconds(t);
            var r = new MoveResult { Start = from };
            float best = 0f;
            float startSlack = t.anchorRadius + 0.04f + StillAllowance * 0.5f;
            for (int i = from; i < f.Count; i++)
            {
                var a = f[i];
                if (!a.Right.Tracked || (both && !a.Left.Tracked)) continue;
                // Like a stroke, a turn starts where the hand pauses at its start spot — raising the hand into place
                // also turns it 30°+, and must not count.
                if (AnchorDist(t, a, false) > startSlack || !IsPause(f, i)) continue;
                for (int j = i + 1; j < f.Count && f[j].Time - a.Time <= maxS; j++)
                {
                    var b = f[j];
                    if (!b.Right.Tracked || (both && !b.Left.Tracked)) continue;
                    if (Dist(a.Right.Palm, b.Right.Palm) > TurnMaxTravel + StillAllowance) break; // the arm moved: not a wrist turn
                    float m = Mathf.Max(Vector3.Angle(a.Right.Normal, b.Right.Normal), Vector3.Angle(a.Right.Axis, b.Right.Axis));
                    if (both) m = Mathf.Min(m, Mathf.Max(Vector3.Angle(a.Left.Normal, b.Left.Normal), Vector3.Angle(a.Left.Axis, b.Left.Axis)));
                    if (m > best)
                    {
                        best = m;
                        r.Start = i;
                    }
                    if (!r.Completed && m >= thr)
                    {
                        r.Completed = true;
                        r.CompletedTime = b.Time;
                    }
                }
            }
            r.Measured = best;
            r.Ratio = Mathf.Clamp01(best / amp);
            r.Hint = r.Completed ? "" : $"Turn your {(both ? "hands" : "hand")} further — {best:0}° of about {amp:0}°";
            return r;
        }

        // squeeze: the fingers named in 'axis' close and open (mean curl swings >= 60 % of 'amplitude' %), 'repeats' times.
        private static MoveResult Squeeze(SignTemplate t, IReadOnlyList<TrajFrame> f, int from)
        {
            int[] fingers = SqueezeFingers(t.movement.axis);
            int swings = Mathf.Max(2, 2 * Mathf.Max(1, t.movement.repeats) - 1);
            float amp = Mathf.Max(10f, t.movement.amplitude) * 0.01f;
            var r = Oscillation(f, from, h => h.Right.Tracked && h.Right.Curl != null, h => MeanCurl(h.Right.Curl, fingers),
                                swingsNeeded: swings, maxSeconds: MaxSeconds(t, 1.8f), hysteresis: 0.06f, halfAmplitude: false,
                                threshold: Threshold(t, t.movement.amplitude * 0.6f) * 0.01f, templateAmp: amp, smooth: false);
            r.Measured *= 100f;
            // Judge the handshape at the open (start) shape, not in the middle of a squeeze.
            float open = float.MaxValue;
            for (int i = from; i < f.Count; i++)
            {
                if (!f[i].Right.Tracked || f[i].Right.Curl == null) continue;
                if (r.Completed && f[i].Time > r.CompletedTime) break;
                float c = MeanCurl(f[i].Right.Curl, fingers);
                if (c < open - 0.02f)
                {
                    open = c;
                    r.Start = i;
                }
            }
            if (!r.Completed) r.Hint = r.Swings < swings
                ? $"{FingerWords(t.movement.axis)}: close and open {Times(Mathf.Max(1, t.movement.repeats))}"
                : $"{FingerWords(t.movement.axis)}: close them further";
            return r;
        }

        private static int[] SqueezeFingers(string axis)
        {
            switch (axis)
            {
                case "index": return new[] { 1 };
                case "index-middle": return new[] { 1, 2 };
                case "thumb-index": return new[] { 0, 1 };
                case "four": return new[] { 1, 2, 3, 4 };
                default: return new[] { 0, 1, 2, 3, 4 };
            }
        }

        private static float MeanCurl(float[] c, int[] fingers)
        {
            float s = 0f;
            foreach (int i in fingers) s += c[i];
            return s / fingers.Length;
        }

        // converge: the two hands' contact points come together (< 6 cm) 'repeats' times.
        private static MoveResult Converge(SignTemplate t, IReadOnlyList<TrajFrame> f, int from)
        {
            int need = Mathf.Max(1, t.movement.repeats);
            float near = 0.06f + StillAllowance * 0.5f;
            var r = new MoveResult { Start = -1 };
            int meets = 0;
            bool together = false, wasApart = false;
            float minD = 1f;
            for (int i = from; i < f.Count; i++)
            {
                var a = f[i];
                if (!a.Right.Tracked || !a.Left.Tracked) continue;
                float d = Dist(SignAnchors.Contact(a.Right, t.contact), SignAnchors.Contact(a.Left, t.leftContact));
                minD = Mathf.Min(minD, d);
                if (d > near + 0.04f)
                {
                    wasApart = true;
                    together = false;
                }
                else if (!together && d < near && wasApart)
                {
                    together = true;
                    meets++;
                    if (r.Start < 0) r.Start = i;
                    if (meets >= need && !r.Completed)
                    {
                        r.Completed = true;
                        r.CompletedTime = a.Time;
                    }
                }
            }
            r.Measured = meets;
            r.Ratio = r.Completed ? 1f : meets > 0 ? 0.8f * meets / need : 0.5f * Mathf.Clamp01(1f - (minD - near) / 0.2f);
            if (r.Start < 0) r.Start = from;
            r.Hint = r.Completed ? "" : $"Bring your {ContactWord(t.contact)}s together {Times(need)} — you did {Times(meets)}";
            return r;
        }

        // hold: both hands at the anchor, crossed when axis = crossed, still (< 3 cm) for 0.5 s.
        private static MoveResult Hold(SignTemplate t, IReadOnlyList<TrajFrame> f, int from)
        {
            const float holdSeconds = 0.5f;
            bool crossed = t.movement.axis == "crossed";
            float slack = t.anchorRadius + 0.04f + StillAllowance * 0.5f;
            var r = new MoveResult { Start = -1 };
            int since = -1;
            float bestHeld = 0f;
            bool everCrossed = false;
            for (int i = from; i < f.Count; i++)
            {
                var a = f[i];
                bool ok = a.Right.Tracked && (!t.TwoHanded || a.Left.Tracked) && AnchorDist(t, a, false) <= slack;
                if (ok && crossed) ok = a.Right.Palm.x < a.Left.Palm.x;
                if (ok && crossed) everCrossed = true;
                if (ok && since >= 0)
                {
                    var s = f[since];
                    float move = Dist(a.Right.Palm, s.Right.Palm);
                    if (t.TwoHanded) move = Mathf.Max(move, Dist(a.Left.Palm, s.Left.Palm));
                    if (move > 0.03f + StillAllowance) since = i;
                }
                if (!ok)
                {
                    since = -1;
                    continue;
                }
                if (since < 0) since = i;
                float held = a.Time - f[since].Time;
                if (held > bestHeld)
                {
                    bestHeld = held;
                    r.Start = since;
                }
                if (!r.Completed && held >= holdSeconds)
                {
                    r.Completed = true;
                    r.CompletedTime = a.Time;
                }
            }
            r.Measured = bestHeld;
            r.Ratio = Mathf.Clamp01(bestHeld / holdSeconds);
            if (r.Start < 0) r.Start = from;
            r.Hint = r.Completed ? "" : crossed && !everCrossed ? $"Cross your wrists on your {AnchorWord(t.anchor)}"
                : $"Hold it on your {AnchorWord(t.anchor)} for half a second";
            return r;
        }

        // rock: both forearms sway side to side together, >= 2 swings of >= 50 % of 'amplitude' cm, slower than COLD.
        private static MoveResult Rock(SignTemplate t, IReadOnlyList<TrajFrame> f, int from)
        {
            int swings = Mathf.Max(2, t.movement.repeats);
            var r = Oscillation(f, from, h => h.Right.Tracked && h.Left.Tracked, h => 0.5f * (h.Right.Palm.x + h.Left.Palm.x),
                                swingsNeeded: swings, maxSeconds: MaxSeconds(t, 1.6f), hysteresis: 0.01f, halfAmplitude: false,
                                threshold: Threshold(t, t.movement.amplitude * 0.5f) * 0.01f, templateAmp: t.movement.amplitude * 0.01f);
            r.Measured *= 100f;
            if (!r.Completed) r.Hint = r.Swings < swings ? "Rock both arms side to side, twice" : "Rock a little wider";
            return r;
        }

        // ---------------- helpers ----------------

        /// <summary>Travel along a direction (cm), minus half the sideways travel, so a different direction does not count.</summary>
        private static float Along(Vector3 delta, Vector3 dir)
        {
            var d = Scaled(delta);
            float along = Vector3.Dot(d, dir);
            float side = (d - dir * along).magnitude;
            return (along - 0.5f * side) * 100f;
        }

        /// <summary>The hand pauses at frame i: its speed is (about) a local minimum and below StrokeStartSpeed.</summary>
        private static bool IsPause(IReadOnlyList<TrajFrame> f, int i)
        {
            float s = Speed(f, i);
            if (s > StrokeStartSpeed + StillAllowance) return false;
            for (int k = Mathf.Max(0, i - 4); k <= Mathf.Min(f.Count - 1, i + 4); k++)
            {
                if (k != i && f[k].Right.Tracked && Speed(f, k) < s - 0.05f) return false;
            }
            return true;
        }

        /// <summary>Dominant palm speed around frame i (m/s, over about 0.1 s).</summary>
        private static float Speed(IReadOnlyList<TrajFrame> f, int i)
        {
            int a = i, b = i;
            while (a > 0 && f[i].Time - f[a - 1].Time <= 0.1f) a--;
            while (b < f.Count - 1 && f[b + 1].Time - f[i].Time <= 0.1f) b++;
            if (a == b || !f[a].Right.Tracked || !f[b].Right.Tracked) return 0f;
            return Dist(f[a].Right.Palm, f[b].Right.Palm) / Mathf.Max(1e-3f, f[b].Time - f[a].Time);
        }

        private static string PalmWord(Vector3 p)
        {
            float ax = Mathf.Abs(p.x), ay = Mathf.Abs(p.y), az = Mathf.Abs(p.z);
            if (ay >= ax && ay >= az) return p.y < 0f ? "down" : "up";
            if (az >= ax) return p.z > 0f ? "forward" : "toward you";
            return "sideways";
        }

        private static Vector3 Scaled(Vector3 d)
        {
            d.z *= DepthWeight;
            return d;
        }

        private static string Times(int n) => n == 1 ? "once" : n == 2 ? "twice" : $"{n} times";

        public static string AnchorWord(string anchor)
        {
            switch (anchor)
            {
                case "side-head": return "cheek, by the side of your head";
                case "upper-chest": return "upper chest";
                case "neutral": return "space in front of you";
                case "left-palm": return "left palm";
                case "left-wrist": return "left wrist";
                case "left-index": return "left index finger";
                case "left-back": return "back of your left hand";
                case "face": return "face";
                default: return string.IsNullOrEmpty(anchor) ? "start spot" : anchor;
            }
        }

        public static string ContactWord(string contact)
        {
            switch (contact)
            {
                case "thumb": return "thumb tip";
                case "index": return "index finger";
                case "middle": return "middle finger";
                case "fingers": return "fingertips";
                case "knuckle": return "index knuckle";
                default: return "hand";
            }
        }

        private static string FingerWords(string axis)
        {
            switch (axis)
            {
                case "index": return "Index finger";
                case "index-middle": return "Index and middle fingers";
                case "thumb-index": return "Thumb and index";
                case "four": return "Fingers";
                default: return "Whole hand";
            }
        }

        private static string DirectionWord(Vector3 d)
        {
            float ax = Mathf.Abs(d.x), ay = Mathf.Abs(d.y), az = Mathf.Abs(d.z);
            if (ay >= ax && ay >= az) return d.y < 0f ? (az > 0.4f ? (d.z > 0 ? "down and forward" : "down and in") : "down") : "up";
            if (az >= ax) return d.z > 0f ? (ay > 0.4f ? "forward and down" : "forward") : "back toward you";
            return d.x > 0f ? "outward" : "inward";
        }
    }
}
