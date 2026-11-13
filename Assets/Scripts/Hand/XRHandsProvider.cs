using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace SignSight.Hand
{
    /// <summary>
    /// IHandProvider backed by Unity XR Hands. This is both the Quest provider and the Simulator provider:
    /// on device and in Meta XR Simulator the joints arrive through the same OpenXR hand-tracking subsystem.
    /// </summary>
    public class XRHandsProvider : MonoBehaviour, IHandProvider
    {
        [Tooltip("Tracking space the joint poses are relative to (OVRCameraRig.trackingSpace). Found automatically if empty.")]
        [SerializeField] private Transform trackingSpace;
        [Tooltip("Head transform (CenterEyeAnchor). Defaults to Camera.main.")]
        [SerializeField] private Transform head;

        private XRHandSubsystem _subsystem;
        private readonly List<XRHandSubsystem> _found = new List<XRHandSubsystem>();
        private readonly Vector3[][] _pos = { new Vector3[HandFrame.JointCount], new Vector3[HandFrame.JointCount] };
        private readonly Quaternion[][] _rot = { new Quaternion[HandFrame.JointCount], new Quaternion[HandFrame.JointCount] };

        public Transform Head
        {
            get
            {
                if (head == null && Camera.main != null) head = Camera.main.transform;
                return head;
            }
        }

        public bool TryGetHand(HandSide side, out HandFrame frame)
        {
            frame = default;
            if (!EnsureSubsystem()) return false;

            var hand = side == HandSide.Left ? _subsystem.leftHand : _subsystem.rightHand;
            if (!hand.isTracked) return false;

            if (trackingSpace == null)
            {
                var rig = FindFirstObjectByType<OVRCameraRig>();
                if (rig != null) trackingSpace = rig.trackingSpace;
            }

            int s = (int)side;
            for (int i = 0; i < HandFrame.JointCount; i++)
            {
                var joint = hand.GetJoint(XRHandJointIDUtility.FromIndex(i));
                if (!joint.TryGetPose(out var pose)) return false; // need every joint for curl
                if (trackingSpace != null)
                {
                    _pos[s][i] = trackingSpace.TransformPoint(pose.position);
                    _rot[s][i] = trackingSpace.rotation * pose.rotation;
                }
                else
                {
                    _pos[s][i] = pose.position;
                    _rot[s][i] = pose.rotation;
                }
            }

            frame = new HandFrame { Side = side, Positions = _pos[s], Rotations = _rot[s] };
            return true;
        }

        private bool EnsureSubsystem()
        {
            if (_subsystem != null && _subsystem.running) return true;
            _found.Clear();
            SubsystemManager.GetSubsystems(_found);
            foreach (var s in _found)
            {
                if (s.running)
                {
                    _subsystem = s;
                    return true;
                }
            }
            return false;
        }
    }
}
