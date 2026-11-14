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
            var s = new MotionSample { RightRotation = Quaternion.identity };
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
            }
            return s;
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
