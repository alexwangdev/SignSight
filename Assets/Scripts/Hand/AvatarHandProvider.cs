using UnityEngine;
using UnityEngine.XR.Hands;

namespace SignSight.Hand
{
    /// <summary>
    /// Self-test hand source: reads the 26 OpenXR hand joints off a Humanoid avatar (the teacher), so the teacher's own
    /// demonstration can be fed through the same recognizer the learner uses. Dev / test only — not used in lessons.
    /// Joints the rig does not have (metacarpals, tips) are estimated from the neighbouring bones.
    /// </summary>
    public class AvatarHandProvider : IHandProvider
    {
        private readonly Animator _animator;
        private readonly Transform _head;
        private readonly Vector3[][] _pos = { new Vector3[HandFrame.JointCount], new Vector3[HandFrame.JointCount] };
        private readonly Quaternion[][] _rot = { new Quaternion[HandFrame.JointCount], new Quaternion[HandFrame.JointCount] };
        private readonly Vector3[][] _tipLocalDir = { new Vector3[5], new Vector3[5] };

        public Transform Head => _head;
        public bool LeftEnabled = true, RightEnabled = true;

        /// <summary>Test hook: the left hand also follows the right hand's movement (e.g. "cranking both hands" for COFFEE).</summary>
        public bool CopyRightMotionToLeft
        {
            get => _copy;
            set
            {
                _copy = value;
                _rightRef = _animator.GetBoneTransform(HumanBodyBones.RightHand).position;
            }
        }
        private bool _copy;
        private Vector3 _rightRef;

        private static readonly HumanBodyBones[][] Right =
        {
            new[] { HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal },
            new[] { HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal },
            new[] { HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal },
            new[] { HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal },
            new[] { HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal },
        };
        private static readonly HumanBodyBones[][] Left =
        {
            new[] { HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal },
            new[] { HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal },
            new[] { HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal },
            new[] { HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal },
            new[] { HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal },
        };

        // XR finger chains after the wrist/palm: thumb has metacarpal, proximal, distal, tip; others metacarpal..tip.
        private static readonly XRHandJointID[] ThumbIds =
            { XRHandJointID.ThumbMetacarpal, XRHandJointID.ThumbProximal, XRHandJointID.ThumbDistal, XRHandJointID.ThumbTip };
        private static readonly XRHandJointID[][] FingerIds =
        {
            new[] { XRHandJointID.IndexMetacarpal, XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, XRHandJointID.IndexTip },
            new[] { XRHandJointID.MiddleMetacarpal, XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip },
            new[] { XRHandJointID.RingMetacarpal, XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, XRHandJointID.RingTip },
            new[] { XRHandJointID.LittleMetacarpal, XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, XRHandJointID.LittleTip },
        };

        /// <param name="head">A transform at the avatar's eyes, facing where the avatar faces.</param>
        public AvatarHandProvider(Animator animator, Transform head)
        {
            _animator = animator;
            _head = head;
            for (int s = 0; s < 2; s++)
            {
                var bones = s == 0 ? Left : Right;
                for (int f = 0; f < 5; f++)
                {
                    var mid = animator.GetBoneTransform(bones[f][1]);
                    var dist = animator.GetBoneTransform(bones[f][2]);
                    _tipLocalDir[s][f] = dist.InverseTransformDirection(dist.position - mid.position).normalized;
                }
                for (int i = 0; i < HandFrame.JointCount; i++) _rot[s][i] = Quaternion.identity;
            }
        }

        public bool TryGetHand(HandSide side, out HandFrame frame)
        {
            frame = default;
            if (side == HandSide.Left ? !LeftEnabled : !RightEnabled) return false;
            int s = side == HandSide.Left ? 0 : 1;
            var bones = s == 0 ? Left : Right;
            var p = _pos[s];
            var wrist = _animator.GetBoneTransform(s == 0 ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand).position;
            var midKnuckle = _animator.GetBoneTransform(bones[2][0]).position;
            p[XRHandJointID.Wrist.ToIndex()] = wrist;
            p[XRHandJointID.Palm.ToIndex()] = (wrist + midKnuckle) * 0.5f;

            // Thumb: the rig's three thumb bones are metacarpal, proximal, distal.
            for (int j = 0; j < 3; j++) p[ThumbIds[j].ToIndex()] = _animator.GetBoneTransform(bones[0][j]).position;
            p[ThumbIds[3].ToIndex()] = Tip(s, 0, bones[0][2], 0.025f);

            for (int f = 1; f < 5; f++)
            {
                var ids = FingerIds[f - 1];
                var prox = _animator.GetBoneTransform(bones[f][0]).position;
                p[ids[0].ToIndex()] = Vector3.Lerp(wrist, prox, 0.3f);
                for (int j = 0; j < 3; j++) p[ids[j + 1].ToIndex()] = _animator.GetBoneTransform(bones[f][j]).position;
                p[ids[4].ToIndex()] = Tip(s, f, bones[f][2], 0.022f);
            }
            if (s == 0 && _copy)
            {
                var delta = _animator.GetBoneTransform(HumanBodyBones.RightHand).position - _rightRef;
                for (int i = 0; i < HandFrame.JointCount; i++) p[i] += delta;
            }
            frame = new HandFrame { Side = side, Positions = p, Rotations = _rot[s] };
            return true;
        }

        private Vector3 Tip(int s, int f, HumanBodyBones distal, float length)
        {
            var d = _animator.GetBoneTransform(distal);
            return d.position + d.TransformDirection(_tipLocalDir[s][f]) * length;
        }
    }
}
