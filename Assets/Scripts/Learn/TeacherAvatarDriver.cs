using System;
using System.Collections;
using UnityEngine;

namespace SignSight.Learn
{
    /// <summary>
    /// Drives the teacher's right arm and fingers from a sign template (SPEC §3.2):
    /// fingers bend by curl[5] (the same numbers the recognizer checks), the arm rises to chest height and the
    /// palm turns to the template's palm direction. 0.5 s into the sign, 1.5 s hold, 0.5 s back to rest.
    /// No animation clips: bones are posed directly from a Humanoid avatar.
    /// </summary>
    public class TeacherAvatarDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        [Header("Timing (SPEC §3.2)")]
        [SerializeField] private float blendInSeconds = 0.5f;
        [SerializeField] private float holdSeconds = 1.5f;
        [SerializeField] private float blendOutSeconds = 0.5f;

        [Header("Signing hand target, in the teacher's own space (metres)")]
        [Tooltip("Where the right hand goes while signing, relative to the right shoulder (x = teacher's right, y = up, z = forward).")]
        [SerializeField] private Vector3 handOffsetFromShoulder = new Vector3(-0.05f, -0.12f, 0.32f);

        [Header("Finger bend at curl = 1 (degrees per joint: proximal, intermediate, distal)")]
        [SerializeField] private Vector3 fingerMaxDegrees = new Vector3(80f, 100f, 70f);
        [SerializeField] private Vector3 thumbMaxDegrees = new Vector3(45f, 45f, 55f);
        [Tooltip("Relaxed hand at rest.")]
        [SerializeField] private float[] restCurl = { 0.15f, 0.12f, 0.12f, 0.12f, 0.12f };

        [Header("Rest pose")]
        [SerializeField] private float armsDownDegrees = 75f;
        [SerializeField] private bool seated = true;

        /// <summary>Raised when a demonstration has fully returned to rest.</summary>
        public event Action DemonstrationFinished;
        public bool IsDemonstrating { get; private set; }

        private static readonly HumanBodyBones[][] RightFingers =
        {
            new[] { HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal },
            new[] { HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal },
            new[] { HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal },
            new[] { HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal },
            new[] { HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal },
        };

        private Transform _upperArm, _lowerArm, _hand;
        private Transform[][] _fingerBones;
        private Quaternion[][] _fingerRest;
        private Vector3[][] _fingerAxisLocal;   // bend axis in each bone's local space
        private Quaternion _upperRest, _lowerRest, _handRest;
        private Quaternion _handFrameToBone;   // converts a (finger dir, palm normal) frame into the hand bone rotation
        private Vector3 _restHandLocal;        // rest hand position in teacher space
        private Quaternion _restHandRotLocal;  // rest hand rotation in teacher space
        private bool _ready;

        // Current blend state
        private float _weight;
        private float[] _targetCurl = new float[5];
        private Vector3 _targetPalm = Vector3.forward;
        private Coroutine _routine;

        private void Awake() => Setup();

        public void Setup()
        {
            if (_ready) return;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman)
            {
                Debug.LogError("[Teacher] Needs a Humanoid Animator.");
                return;
            }

            _upperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _lowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _hand = animator.GetBoneTransform(HumanBodyBones.RightHand);

            PoseRestBody();

            // Finger bend axes from the model's own geometry (works for any humanoid rig).
            Vector3 palmNormal = HandPalmNormalWorld();
            _fingerBones = new Transform[5][];
            _fingerRest = new Quaternion[5][];
            _fingerAxisLocal = new Vector3[5][];
            for (int f = 0; f < 5; f++)
            {
                _fingerBones[f] = new Transform[3];
                _fingerRest[f] = new Quaternion[3];
                _fingerAxisLocal[f] = new Vector3[3];
                for (int j = 0; j < 3; j++)
                {
                    var bone = animator.GetBoneTransform(RightFingers[f][j]);
                    _fingerBones[f][j] = bone;
                    if (bone == null) continue;
                    _fingerRest[f][j] = bone.localRotation;
                    Vector3 dir = BoneDirection(bone);
                    // Rotating dir about (dir x n) moves the fingertip toward the palm side.
                    // The thumb also folds across the palm, toward the middle-finger knuckle.
                    Vector3 toward = palmNormal;
                    if (f == 0)
                    {
                        var midKnuckle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal).position;
                        toward = (palmNormal + (midKnuckle - bone.position).normalized).normalized;
                    }
                    Vector3 axisWorld = Vector3.Cross(dir, toward);
                    if (axisWorld.sqrMagnitude < 1e-6f) axisWorld = _hand.right;
                    _fingerAxisLocal[f][j] = bone.InverseTransformDirection(axisWorld.normalized);
                }
            }

            _upperRest = _upperArm.localRotation;
            _lowerRest = _lowerArm.localRotation;
            _handRest = _hand.localRotation;
            _restHandLocal = transform.InverseTransformPoint(_hand.position);
            _restHandRotLocal = Quaternion.Inverse(transform.rotation) * _hand.rotation;
            _handFrameToBone = Quaternion.Inverse(Quaternion.LookRotation(HandFingerDirWorld(), palmNormal)) * _hand.rotation;

            animator.enabled = false; // bones are posed directly from here on
            _ready = true;
            Apply();
        }

        /// <summary>Shows one sign: 0.5 s in, 1.5 s hold, 0.5 s out. Replaces any demonstration in progress.</summary>
        public void Demonstrate(float[] curl, Vector3 palm)
        {
            if (!_ready) Setup();
            if (!_ready) return;
            Array.Copy(curl, _targetCurl, 5);
            _targetPalm = palm.sqrMagnitude > 1e-4f ? palm.normalized : Vector3.forward;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Run());
        }

        /// <summary>Holds a sign shape without timing (used for checks and stills).</summary>
        public void Hold(float[] curl, Vector3 palm, float weight = 1f)
        {
            if (!_ready) Setup();
            if (_routine != null) StopCoroutine(_routine);
            IsDemonstrating = false;
            Array.Copy(curl, _targetCurl, 5);
            _targetPalm = palm.sqrMagnitude > 1e-4f ? palm.normalized : Vector3.forward;
            _weight = weight;
            Apply();
        }

        private IEnumerator Run()
        {
            IsDemonstrating = true;
            float start = _weight;
            for (float t = 0; t < blendInSeconds; t += Time.deltaTime)
            {
                _weight = Mathf.Lerp(start, 1f, Smooth(t / blendInSeconds));
                Apply();
                yield return null;
            }
            _weight = 1f;
            Apply();
            yield return new WaitForSeconds(holdSeconds);
            for (float t = 0; t < blendOutSeconds; t += Time.deltaTime)
            {
                _weight = Mathf.Lerp(1f, 0f, Smooth(t / blendOutSeconds));
                Apply();
                yield return null;
            }
            _weight = 0f;
            Apply();
            IsDemonstrating = false;
            _routine = null;
            DemonstrationFinished?.Invoke();
        }

        private static float Smooth(float x) => x * x * (3f - 2f * Mathf.Clamp01(x));

        /// <summary>Poses arm, hand and fingers for the current weight (0 = rest, 1 = full sign).</summary>
        private void Apply()
        {
            if (!_ready) return;
            _upperArm.localRotation = _upperRest;
            _lowerArm.localRotation = _lowerRest;
            _hand.localRotation = _handRest;

            // Arm: hand moves from rest to the signing spot in front of the chest.
            Vector3 signLocal = transform.InverseTransformPoint(_upperArm.position) + handOffsetFromShoulder;
            Vector3 targetLocal = Vector3.Lerp(_restHandLocal, signLocal, _weight);
            SolveTwoBone(transform.TransformPoint(targetLocal));

            // Palm: rotate the hand so its palm faces the template direction (teacher space).
            Quaternion signRot = transform.rotation * HandFrame(_targetPalm) * _handFrameToBone;
            Quaternion restRot = transform.rotation * _restHandRotLocal;
            _hand.rotation = Quaternion.Slerp(restRot, signRot, _weight);

            // Fingers
            for (int f = 0; f < 5; f++)
            {
                float curl = Mathf.Lerp(restCurl[f], _targetCurl[f], _weight);
                Vector3 max = f == 0 ? thumbMaxDegrees : fingerMaxDegrees;
                for (int j = 0; j < 3; j++)
                {
                    var bone = _fingerBones[f][j];
                    if (bone == null) continue;
                    float deg = curl * (j == 0 ? max.x : j == 1 ? max.y : max.z);
                    bone.localRotation = _fingerRest[f][j] * Quaternion.AngleAxis(deg, _fingerAxisLocal[f][j]);
                }
            }
        }

        /// <summary>Hand frame for a palm direction: fingers point up, unless the palm faces up/down, then forward.</summary>
        private static Quaternion HandFrame(Vector3 palmTeacherSpace)
        {
            Vector3 n = palmTeacherSpace.normalized;
            Vector3 up = Mathf.Abs(Vector3.Dot(n, Vector3.up)) > 0.7f ? Vector3.forward : Vector3.up;
            Vector3 finger = Vector3.ProjectOnPlane(up, n).normalized;
            return Quaternion.LookRotation(finger, n);
        }

        /// <summary>Analytic two-bone IK: places the elbow so the hand reaches the target, elbow pointing down and out.</summary>
        private void SolveTwoBone(Vector3 target)
        {
            Vector3 a = _upperArm.position, b = _lowerArm.position, c = _hand.position;
            float lenA = Vector3.Distance(a, b), lenB = Vector3.Distance(b, c);
            Vector3 toTarget = target - a;
            float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(lenA - lenB) + 1e-3f, lenA + lenB - 1e-3f);
            Vector3 dir = toTarget.normalized;
            float cosA = Mathf.Clamp((lenA * lenA + d * d - lenB * lenB) / (2f * lenA * d), -1f, 1f);
            Vector3 pole = Vector3.ProjectOnPlane(-transform.up + transform.right * -0.4f, dir).normalized;
            Vector3 elbow = a + dir * (lenA * cosA) + pole * (lenA * Mathf.Sqrt(1f - cosA * cosA));

            _upperArm.rotation = Quaternion.FromToRotation(b - a, elbow - a) * _upperArm.rotation;
            b = _lowerArm.position;
            c = _hand.position;
            _lowerArm.rotation = Quaternion.FromToRotation(c - b, (a + dir * d) - b) * _lowerArm.rotation;
        }

        private void PoseRestBody()
        {
            // Arms down from the T-pose (both sides), around the teacher's forward axis.
            var lu = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            if (lu != null) lu.rotation = Quaternion.AngleAxis(armsDownDegrees, transform.forward) * lu.rotation;
            if (_upperArm != null) _upperArm.rotation = Quaternion.AngleAxis(-armsDownDegrees, transform.forward) * _upperArm.rotation;

            if (!seated) return;
            foreach (var (upper, lower) in new[] {
                (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg),
                (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg) })
            {
                var u = animator.GetBoneTransform(upper);
                var l = animator.GetBoneTransform(lower);
                if (u != null) u.rotation = Quaternion.AngleAxis(-90f, transform.right) * u.rotation; // thighs forward
                if (l != null) l.rotation = Quaternion.AngleAxis(90f, transform.right) * l.rotation;  // shins down
            }
        }

        private Vector3 HandFingerDirWorld()
        {
            var mid = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            return (mid.position - _hand.position).normalized;
        }

        /// <summary>Unit vector out of the right palm (same construction as CurlCalculator).</summary>
        private Vector3 HandPalmNormalWorld()
        {
            var index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal).position;
            var little = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal).position;
            Vector3 n = Vector3.Cross(index - _hand.position, little - _hand.position).normalized;
            return -n; // right hand: the cross product points out of the back of the hand
        }

        private static Vector3 BoneDirection(Transform bone)
        {
            if (bone.childCount > 0) return (bone.GetChild(0).position - bone.position).normalized;
            return bone.parent != null ? (bone.position - bone.parent.position).normalized : bone.forward;
        }
    }
}
