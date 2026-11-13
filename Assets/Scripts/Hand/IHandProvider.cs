using UnityEngine;
using UnityEngine.XR.Hands;

namespace SignSight.Hand
{
    public enum HandSide { Left, Right }

    /// <summary>One hand's 26 joints for one frame, in world space. Indexed by XRHandJointID.ToIndex().</summary>
    public struct HandFrame
    {
        public const int JointCount = 26;

        public HandSide Side;
        public Vector3[] Positions;
        public Quaternion[] Rotations;

        public Vector3 Pos(XRHandJointID id) => Positions[id.ToIndex()];
    }

    /// <summary>
    /// Source of hand joints. Recognition code only talks to this interface, so the same code runs
    /// on Quest, in Meta XR Simulator, or from a future MediaPipe bridge (SPEC §10.2).
    /// </summary>
    public interface IHandProvider
    {
        /// <summary>The signer's head (CenterEyeAnchor). Used to express palm direction relative to the signer.</summary>
        Transform Head { get; }

        /// <summary>Returns false when that hand is not tracked this frame.</summary>
        bool TryGetHand(HandSide side, out HandFrame frame);
    }
}
