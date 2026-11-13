using System.Collections;
using UnityEngine;

namespace SignSight.UI
{
    /// <summary>
    /// Places the UI root at the seated user's head position and facing (yaw only) once tracking starts,
    /// so children keep their offsets inside the seated field of view (SPEC §9), wherever the head starts.
    /// </summary>
    public class SeatedUiAnchor : MonoBehaviour
    {
        [Tooltip("Head transform (CenterEyeAnchor). Defaults to Camera.main.")]
        [SerializeField] private Transform head;
        [Tooltip("Frames to wait for head tracking to settle before anchoring.")]
        [SerializeField] private int settleFrames = 10;

        private IEnumerator Start()
        {
            for (int i = 0; i < settleFrames; i++) yield return null;
            Recenter();
        }

        public void Recenter()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (head == null)
            {
                Debug.LogError("[SeatedUiAnchor] No head transform found.");
                return;
            }
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            transform.SetPositionAndRotation(head.position, Quaternion.LookRotation(forward.normalized, Vector3.up));
        }
    }
}
