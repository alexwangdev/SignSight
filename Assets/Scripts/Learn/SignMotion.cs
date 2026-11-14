using SignSight.Hand;
using UnityEngine;

namespace SignSight.Learn
{
    /// <summary>
    /// One sample of a sign's movement (SPEC v3 §4.3), as offsets from the start pose in the signer's space
    /// (x = signer's right, y = up, z = toward the partner; metres). Rotations pre-multiply the hand's start rotation.
    /// </summary>
    public struct MotionSample
    {
        public Vector3 RightOffset;
        public Quaternion RightRotation;
        public float[] RightCurl;   // null = keep the start curl
        public Vector3 LeftOffset;
        public Quaternion LeftRotation; // v3.1: pre-multiplies the left hand's start rotation
        public float[] LeftCurl;        // v3.1: null = keep the start curl
    }

    /// <summary>
    /// The movement of every sign, in one place. The teacher (TeacherAvatarDriver) and the feedback ghost hands
    /// (GhostHand, SPEC v3 §8.1) both use it, so they always show the same numbers.
    /// </summary>
    public static class SignMotion
    {
        public const float CrankRadius = 0.035f;

        /// <param name="u">0..1 through the movement.</param>
        /// <param name="rightFingerDir">Direction of the right hand's fingers (signer space): the crank rolls about it.</param>
        public static MotionSample Sample(SignTemplate t, float u, Vector3 rightFingerDir)
        {
            var s = new MotionSample { RightRotation = Quaternion.identity, LeftRotation = Quaternion.identity };
            var m = t.movement;
            float amp = m.amplitude;
            int reps = Mathf.Max(1, m.repeats);
            float cm = amp * 0.01f;

            switch (m.type)
            {
                case MovementTypes.Wave: // wrist yaw ±amp, side to side, drifting slightly outward
                    s.RightRotation = Quaternion.AngleAxis(amp * Mathf.Sin(2f * Mathf.PI * reps * u), Vector3.forward);
                    s.RightOffset = new Vector3(0.04f * Smooth(u), 0f, 0f);
                    break;
                case MovementTypes.WristNod: // wrist pitch ±amp, like a head nodding
                    s.RightRotation = Quaternion.AngleAxis(amp * Mathf.Sin(2f * Mathf.PI * reps * u), Vector3.right);
                    break;
                case MovementTypes.Snap: // index + middle snap down onto the thumb
                    s.RightCurl = Lerp(t.curlRight, t.EndCurlRight, Smooth(u / 0.6f));
                    break;
                case MovementTypes.PushForward: // forward and a bit down, fingers tipping toward the partner
                {
                    float k = Smooth(u);
                    s.RightOffset = new Vector3(0f, -0.35f * cm, 0.95f * cm) * k;
                    s.RightRotation = Quaternion.AngleAxis(45f * k, Vector3.right);
                    break;
                }
                case MovementTypes.Crank: // top fist turns a grinder crank (small horizontal circle); bottom fist still
                {
                    float phi = 2f * Mathf.PI * reps * Smooth(u);
                    s.RightOffset = new Vector3(CrankRadius * Mathf.Sin(phi), 0f, CrankRadius * (Mathf.Cos(phi) - 1f));
                    // Forearm follows the crank: wrist roll of ±amp about the forearm axis.
                    s.RightRotation = Quaternion.AngleAxis(amp * Mathf.Sin(phi), rightFingerDir.normalized);
                    break;
                }
                case MovementTypes.FlickOut: // quick flick outward and downward from the mouth
                {
                    float k = EaseOut(u);
                    s.RightOffset = new Vector3(0.35f * cm, -0.9f * cm, 0.65f * cm) * k;
                    s.RightRotation = Quaternion.AngleAxis(70f * k, Vector3.right);
                    break;
                }
                case MovementTypes.Shake: // both fists shiver side to side together (amplitude = total travel)
                {
                    float x = 0.5f * cm * Mathf.Sin(2f * Mathf.PI * reps * u);
                    s.RightOffset = new Vector3(x, 0f, 0f);
                    s.LeftOffset = s.RightOffset;
                    break;
                }
                case MovementTypes.Lift: // both hands rise together with a slight forward arc
                {
                    float k = Smooth(u);
                    s.RightOffset = new Vector3(0f, cm * k, 0.04f * Mathf.Sin(0.5f * Mathf.PI * k));
                    s.LeftOffset = s.RightOffset;
                    break;
                }
                default:
                    SampleV31(t, u, rightFingerDir, ref s);
                    break;
            }
            return s;
        }

        // ---------------- v3.1 movement types ----------------

        private static bool BothMove(SignTemplate t) => t.TwoHanded && t.movement.movingHand == "both";

        /// <summary>The same vector seen from the left hand (mirror across the signer's mid-line).</summary>
        public static Vector3 MirrorX(Vector3 v) => new Vector3(-v.x, v.y, v.z);

        public static Quaternion MirrorX(Quaternion q) => new Quaternion(q.x, -q.y, -q.z, q.w);

        /// <summary>0 -> 1 -> 0, 'reps' times over u (out and back).</summary>
        private static float OutBack(float u, int reps) => 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * reps * Mathf.Clamp01(u));

        private static void SampleV31(SignTemplate t, float u, Vector3 rightFingerDir, ref MotionSample s)
        {
            var m = t.movement;
            int reps = Mathf.Max(1, m.repeats);
            float cm = m.amplitude * 0.01f;
            bool both = BothMove(t);
            var dir = t.direction.sqrMagnitude > 1e-6f ? t.direction.normalized : Vector3.zero;
            bool hasEnd = t.endCurlRight != null && t.endCurlRight.Length == 5;

            switch (m.type)
            {
                case MovementTypes.Tap: // touch, lift off by 'amplitude' and touch again; ends touching
                {
                    var away = dir != Vector3.zero ? dir : Vector3.forward;
                    float k = reps == 1 ? 0.6f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(u)) : OutBack(u, reps);
                    s.RightOffset = away * (cm * k);
                    if (both && !string.IsNullOrEmpty(t.leftAnchor) && !t.leftAnchor.StartsWith("left-"))
                        s.LeftOffset = MirrorX(away) * (cm * k);
                    break;
                }
                case MovementTypes.Stroke: // travel 'amplitude' along 'direction' (out and back when repeated)
                {
                    var d = dir != Vector3.zero ? dir : Vector3.down;
                    float k = reps == 1 ? Smooth(u) : OutBack(u, reps);
                    s.RightOffset = d * (cm * k);
                    if (t.endPalm.sqrMagnitude > 1e-4f && t.palm.sqrMagnitude > 1e-4f)
                        s.RightRotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(t.palm, t.endPalm), Smooth(u));
                    if (hasEnd) s.RightCurl = Lerp(t.curlRight, t.endCurlRight, Smooth(u / 0.7f));
                    if (both) s.LeftOffset = MirrorX(d) * (cm * k);
                    break;
                }
                case MovementTypes.Circle: // 'reps' circles of radius 'amplitude' in the named plane
                {
                    float phi = 2f * Mathf.PI * reps * Smooth(u);
                    float a = cm * Mathf.Sin(phi), b = cm * (Mathf.Cos(phi) - 1f);
                    s.RightOffset = m.axis == "sagittal" ? new Vector3(0f, b, a)
                        : m.axis == "horizontal" ? new Vector3(a, 0f, b) : new Vector3(a, b, 0f);
                    if (both) s.LeftOffset = m.axis == "sagittal" ? s.RightOffset : MirrorX(s.RightOffset);
                    break;
                }
                case MovementTypes.Twist: // wrist roll back and forth about the fingers' direction
                {
                    float cycles = Mathf.Max(1f, reps * 0.5f);
                    s.RightRotation = Quaternion.AngleAxis(m.amplitude * Mathf.Sin(2f * Mathf.PI * cycles * u), rightFingerDir.normalized);
                    if (both) s.LeftRotation = MirrorX(s.RightRotation);
                    break;
                }
                case MovementTypes.Turn: // one turn of the hand: pitch (about the signer's x), roll (about the fingers) or yaw
                {
                    var axis = m.axis == "roll" ? rightFingerDir.normalized : m.axis == "yaw" ? Vector3.up : Vector3.right;
                    float sign = m.axis == "pitch-back" ? -1f : 1f; // pitch: fingers tip toward the partner; pitch-back: toward the signer
                    s.RightRotation = Quaternion.AngleAxis(sign * m.amplitude * Smooth(u), axis);
                    if (hasEnd) s.RightCurl = Lerp(t.curlRight, t.endCurlRight, Smooth(u));
                    if (both) s.LeftRotation = MirrorX(s.RightRotation);
                    break;
                }
                case MovementTypes.Squeeze: // fingers close to the end curl and open again, 'reps' times
                {
                    var end = hasEnd ? t.endCurlRight : t.curlRight;
                    s.RightCurl = Lerp(t.curlRight, end, OutBack(u, reps));
                    break;
                }
                case MovementTypes.Converge: // hands start apart, come together, 'reps' times; ends together
                {
                    var d = dir != Vector3.zero ? dir : Vector3.right;
                    s.RightOffset = -d * (0.5f * cm * (1f - OutBack(u, reps))); // apart at 0 and between meetings
                    s.LeftOffset = MirrorX(s.RightOffset);
                    if (m.axis == "index-jab-twist")
                    {
                        // PAIN / HURT: the index fingers twist in opposite directions as they jab together.
                        s.RightRotation = Quaternion.AngleAxis(45f * OutBack(u, reps), rightFingerDir.normalized);
                        s.LeftRotation = Quaternion.Inverse(MirrorX(s.RightRotation));
                    }
                    break;
                }
                case MovementTypes.Hold: // press in and hold
                    s.RightOffset = new Vector3(0f, 0f, -0.01f * Smooth(u * 3f));
                    s.LeftOffset = s.RightOffset;
                    break;
                case MovementTypes.Rock: // both forearms sway side to side together
                {
                    float cycles = Mathf.Max(1f, reps * 0.5f);
                    float x = 0.5f * cm * Mathf.Sin(2f * Mathf.PI * cycles * u);
                    s.RightOffset = new Vector3(x, 0f, 0f);
                    s.LeftOffset = s.RightOffset;
                    s.RightRotation = Quaternion.AngleAxis(-x * 250f, Vector3.forward);
                    s.LeftRotation = s.RightRotation;
                    break;
                }
            }
        }

        /// <summary>The second beat of a compound sign as a template of its own (teacher display only).</summary>
        public static SignTemplate BeatTemplate(SignTemplate t)
        {
            var b = t.beat2;
            if (b == null || !b.Present) return null;
            return new SignTemplate
            {
                intent = t.intent + "#2",
                phrase = t.phrase,
                hands = !string.IsNullOrEmpty(b.leftAnchor) ? "both" : "right",
                curlRight = b.curlRight != null && b.curlRight.Length == 5 ? b.curlRight : t.curlRight,
                curlLeft = b.curlLeft != null && b.curlLeft.Length == 5 ? b.curlLeft : t.curlLeft,
                spread = t.spread,
                palm = b.palm != Vector3.zero ? b.palm : t.palm,
                anchor = b.anchor,
                contact = b.contact,
                fingerDir = b.fingerDir != Vector3.zero ? b.fingerDir : t.fingerDir,
                leftAnchor = b.leftAnchor,
                leftPalm = b.leftPalm,
                leftFingerDir = b.leftFingerDir,
                movement = b.movement,
                direction = b.direction,
                head = new SignHead(),
                face = t.face,
                scored = false,
            };
        }

        public static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        public static float EaseOut(float x)
        {
            x = Mathf.Clamp01(x);
            return 1f - (1f - x) * (1f - x);
        }

        public static float[] Lerp(float[] a, float[] b, float s)
        {
            var c = new float[5];
            for (int i = 0; i < 5; i++) c[i] = Mathf.Lerp(a[i], b[i], s);
            return c;
        }
    }
}
