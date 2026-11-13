using System;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace SignSight.Hand
{
    /// <summary>Palm facing, relative to the signer (SPEC §4.2).</summary>
    public enum PalmLabel { Forward, TowardSelf, Up, Down, Sideways }

    /// <summary>Per-frame hand-shape features. curl: 0 = fully extended, 1 = fully curled.</summary>
    [Serializable]
    public struct HandFeatures
    {
        public const int Thumb = 0, Index = 1, Middle = 2, Ring = 3, Pinky = 4;

        public float[] Curl;       // [thumb, index, middle, ring, pinky], each 0..1
        public float Pinch;        // thumb tip to index tip, divided by hand size, 0..1 (0 = touching)
        public float Spread;       // how far the fingers fan apart, 0..1
        public Vector3 PalmNormal; // unit vector out of the palm, in the signer's head-yaw frame (+z = toward the partner)
        public PalmLabel Palm;
    }

    /// <summary>
    /// Joints → curl[5] + pinch + spread + palm (SPEC §4.2).
    /// Method (fixed for the whole project): curl is the sum of the bend angles between consecutive
    /// finger bones, divided by that finger's full-curl angle and clamped to 0..1. No absolute positions are used.
    /// </summary>
    public static class CurlCalculator
    {
        // Sum of bend angles (degrees) that counts as fully curled. Thumb bends less than the fingers.
        public const float ThumbFullCurlDegrees = 120f;
        public const float FingerFullCurlDegrees = 230f;
        // Average angle between neighbouring fingers that counts as fully spread.
        public const float FullSpreadDegrees = 25f;

        private static readonly XRHandJointID[] ThumbChain =
            { XRHandJointID.ThumbMetacarpal, XRHandJointID.ThumbProximal, XRHandJointID.ThumbDistal, XRHandJointID.ThumbTip };

        private static readonly XRHandJointID[][] FingerChains =
        {
            new[] { XRHandJointID.IndexMetacarpal, XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, XRHandJointID.IndexTip },
            new[] { XRHandJointID.MiddleMetacarpal, XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip },
            new[] { XRHandJointID.RingMetacarpal, XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, XRHandJointID.RingTip },
            new[] { XRHandJointID.LittleMetacarpal, XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, XRHandJointID.LittleTip },
        };

        public static HandFeatures Compute(in HandFrame f, Transform head)
        {
            var feat = new HandFeatures { Curl = new float[5] };

            feat.Curl[HandFeatures.Thumb] = Mathf.Clamp01(BendDegrees(f, ThumbChain) / ThumbFullCurlDegrees);
            for (int i = 0; i < 4; i++)
            {
                feat.Curl[i + 1] = Mathf.Clamp01(BendDegrees(f, FingerChains[i]) / FingerFullCurlDegrees);
            }

            float handSize = Vector3.Distance(f.Pos(XRHandJointID.Wrist), f.Pos(XRHandJointID.MiddleProximal));
            if (handSize < 1e-4f) handSize = 0.08f;
            feat.Pinch = Mathf.Clamp01(Vector3.Distance(f.Pos(XRHandJointID.ThumbTip), f.Pos(XRHandJointID.IndexTip)) / handSize);

            float spread = 0f;
            for (int i = 0; i < 3; i++)
            {
                spread += Vector3.Angle(BoneDir(f, FingerChains[i], 1), BoneDir(f, FingerChains[i + 1], 1));
            }
            feat.Spread = Mathf.Clamp01(spread / 3f / FullSpreadDegrees);

            feat.PalmNormal = ToHeadYawFrame(PalmNormalWorld(f), head);
            feat.Palm = Classify(feat.PalmNormal);
            return feat;
        }

        /// <summary>Unit vector pointing out of the palm, world space.</summary>
        public static Vector3 PalmNormalWorld(in HandFrame f)
        {
            var wrist = f.Pos(XRHandJointID.Wrist);
            var toIndex = f.Pos(XRHandJointID.IndexProximal) - wrist;
            var toLittle = f.Pos(XRHandJointID.LittleProximal) - wrist;
            var n = Vector3.Cross(toIndex, toLittle).normalized;
            // The cross product points out of the back of a right hand and out of the palm of a left hand.
            return f.Side == HandSide.Right ? -n : n;
        }

        /// <summary>Expresses a world direction relative to where the signer faces (yaw only), so leaning does not matter.</summary>
        public static Vector3 ToHeadYawFrame(Vector3 worldDir, Transform head)
        {
            if (head == null) return worldDir;
            var flat = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (flat.sqrMagnitude < 1e-4f) return worldDir;
            return Quaternion.Inverse(Quaternion.LookRotation(flat.normalized, Vector3.up)) * worldDir;
        }

        public static PalmLabel Classify(Vector3 n)
        {
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            if (ay >= ax && ay >= az) return n.y > 0 ? PalmLabel.Up : PalmLabel.Down;
            if (az >= ax) return n.z > 0 ? PalmLabel.Forward : PalmLabel.TowardSelf;
            return PalmLabel.Sideways;
        }

        /// <summary>Default palm direction for a label (used only for the initial §4.3 templates).</summary>
        public static Vector3 DirectionOf(PalmLabel label)
        {
            switch (label)
            {
                case PalmLabel.Forward: return Vector3.forward;
                case PalmLabel.TowardSelf: return Vector3.back;
                case PalmLabel.Up: return Vector3.up;
                case PalmLabel.Down: return Vector3.down;
                default: return Vector3.left; // palm facing the thumb side of a right hand
            }
        }

        private static float BendDegrees(in HandFrame f, XRHandJointID[] chain)
        {
            float sum = 0f;
            for (int i = 1; i < chain.Length - 1; i++)
            {
                sum += Vector3.Angle(f.Pos(chain[i]) - f.Pos(chain[i - 1]), f.Pos(chain[i + 1]) - f.Pos(chain[i]));
            }
            return sum;
        }

        private static Vector3 BoneDir(in HandFrame f, XRHandJointID[] chain, int bone)
        {
            return (f.Pos(chain[bone + 1]) - f.Pos(chain[bone])).normalized;
        }
    }
}
