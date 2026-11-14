using System;
using System.Collections;
using SignSight.Hand;
using UnityEngine;

namespace SignSight.Learn
{
    /// <summary>
    /// Teacher v3 (SPEC v3 §3.4): performs a full sign from its action definition — both arms, a keyframe sequence
    /// (move to start, perform the movement, hold the end pose, return), head nod / shake and face via FaceDriver.
    /// No animation clips: bones are posed directly from a Humanoid avatar. Fingers bend by the same curl[5] numbers
    /// the recognizer checks.
    ///
    /// Teacher space: x = teacher's right, y = up, z = toward the learner. Template palms use this space.
    /// </summary>
    public class TeacherAvatarDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private FaceDriver face;

        [Header("Sequence timing (SPEC v3 §3.4)")]
        [SerializeField] private float toStartSeconds = 0.4f;
        [SerializeField] private float holdEndSeconds = 0.8f;
        [SerializeField] private float returnSeconds = 0.4f;
        [Tooltip("Head nod / shake lasts at least this long, so a 0.4 s snap still gets a readable head shake.")]
        [SerializeField] private float minHeadSeconds = 0.8f;

        [Header("Chest signing spot, right hand, relative to the right shoulder (metres; left is mirrored)")]
        [SerializeField] private Vector3 handOffsetFromShoulder = new Vector3(-0.05f, -0.12f, 0.32f);

        [Header("Finger bend at curl = 1 (degrees per joint: proximal, intermediate, distal)")]
        [SerializeField] private Vector3 fingerMaxDegrees = new Vector3(80f, 100f, 70f);
        [SerializeField] private Vector3 thumbMaxDegrees = new Vector3(45f, 45f, 55f);
        [Tooltip("Sideways splay at spread = 1, per finger: index, middle, ring, pinky (degrees away from the middle finger).")]
        [SerializeField] private Vector4 spreadDegrees = new Vector4(10f, 0f, 8f, 16f);
        [Tooltip("Relaxed hand at rest.")]
        [SerializeField] private float[] restCurl = { 0.15f, 0.12f, 0.12f, 0.12f, 0.12f };

        [Header("Rest pose")]
        [SerializeField] private float armsDownDegrees = 75f;
        [SerializeField] private bool seated = true;

        /// <summary>Raised when a demonstration has fully returned to rest.</summary>
        public event Action DemonstrationFinished;
        public bool IsDemonstrating { get; private set; }
        public FaceDriver Face => face;

        // ---- Per-arm rig ----
        private class Arm
        {
            public float Side; // +1 right, -1 left
            public Transform Upper, Lower, Hand;
            public Transform[][] Fingers;
            public Quaternion[][] FingerRest;
            public Vector3[][] BendAxisLocal;
            public Vector3[] SpreadAxisLocal;
            public Quaternion UpperRest, LowerRest, HandRest;
            public Quaternion FrameToBone;   // (finger dir, palm normal) frame -> hand bone rotation
            public Vector3 RestPos;          // rest wrist position, teacher space
            public Quaternion RestRot;       // rest hand bone rotation, teacher space
            public Vector3 Shoulder;         // teacher space
            public float PalmLength;         // wrist -> middle knuckle
            public float HandLength;         // wrist -> middle fingertip
        }

        /// <summary>One hand's pose in teacher space.</summary>
        private struct HandPose
        {
            public Vector3 Pos;            // wrist
            public Quaternion BoneRot;     // hand bone rotation (teacher space)
            public float[] Curl;
            public float Spread;
        }

        private static readonly HumanBodyBones[][] RightFingerBones =
        {
            new[] { HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal },
            new[] { HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal },
            new[] { HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal },
            new[] { HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal },
            new[] { HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal },
        };
        private static readonly HumanBodyBones[][] LeftFingerBones =
        {
            new[] { HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal },
            new[] { HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal },
            new[] { HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal },
            new[] { HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal },
            new[] { HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal },
        };

        private Arm _right, _left;
        private Transform _head;
        private Quaternion _headRest;
        private Vector3 _eyes;   // teacher space, mid-point between the eyes
        private bool _ready;
        private Coroutine _routine;

        // Face anchors (teacher space), derived from the eyes.
        private Vector3 Mouth => _eyes + new Vector3(0f, -0.075f, 0.035f);
        private Vector3 Chin => _eyes + new Vector3(0f, -0.105f, 0.03f);
        private Vector3 Temple(float side) => _eyes + new Vector3(side * 0.07f, 0.03f, -0.01f);
        private Vector3 ChestCenter => (_right.Shoulder + _left.Shoulder) * 0.5f + new Vector3(0f, handOffsetFromShoulder.y, handOffsetFromShoulder.z);
        private Vector3 ChestSpot(Arm a) => a.Shoulder + new Vector3(a.Side * handOffsetFromShoulder.x, handOffsetFromShoulder.y, handOffsetFromShoulder.z);

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
            if (face == null) face = GetComponent<FaceDriver>();

            PoseRestBody();
            _right = BuildArm(1f, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, RightFingerBones);
            _left = BuildArm(-1f, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, LeftFingerBones);
            if (_right == null || _left == null)
            {
                Debug.LogError("[Teacher] Arm or finger bones are missing from the Humanoid mapping.");
                return;
            }

            _head = animator.GetBoneTransform(HumanBodyBones.Head);
            _headRest = _head.localRotation;
            // Eyes are not in the Humanoid mapping for Rocketbox; fall back to the rig names (SPEC v3 §3.4 fallback).
            var le = animator.GetBoneTransform(HumanBodyBones.LeftEye) ?? FindByName("Bip01 LEye");
            var re = animator.GetBoneTransform(HumanBodyBones.RightEye) ?? FindByName("Bip01 REye");
            Vector3 eyesWorld = le != null && re != null
                ? (le.position + re.position) * 0.5f
                : _head.position + transform.up * 0.09f + transform.forward * 0.07f;
            _eyes = transform.InverseTransformPoint(eyesWorld);

            animator.enabled = false; // bones are posed directly from here on
            _ready = true;
            ApplyRest();
        }

        private Arm BuildArm(float side, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones hand, HumanBodyBones[][] fingerIds)
        {
            var a = new Arm
            {
                Side = side,
                Upper = animator.GetBoneTransform(upper),
                Lower = animator.GetBoneTransform(lower),
                Hand = animator.GetBoneTransform(hand),
            };
            if (a.Upper == null || a.Lower == null || a.Hand == null) return null;
            var indexK = animator.GetBoneTransform(fingerIds[1][0]);
            var middleK = animator.GetBoneTransform(fingerIds[2][0]);
            var littleK = animator.GetBoneTransform(fingerIds[4][0]);
            if (indexK == null || middleK == null || littleK == null) return null;

            // Palm normal from the hand's own geometry (same construction as CurlCalculator).
            // The cross product points out of the back of the right hand and out of the palm of the left hand.
            Vector3 n = Vector3.Cross(indexK.position - a.Hand.position, littleK.position - a.Hand.position).normalized;
            Vector3 palmNormal = side > 0 ? -n : n;
            Vector3 fingerDir = (middleK.position - a.Hand.position).normalized;

            a.Fingers = new Transform[5][];
            a.FingerRest = new Quaternion[5][];
            a.BendAxisLocal = new Vector3[5][];
            a.SpreadAxisLocal = new Vector3[5];
            for (int f = 0; f < 5; f++)
            {
                a.Fingers[f] = new Transform[3];
                a.FingerRest[f] = new Quaternion[3];
                a.BendAxisLocal[f] = new Vector3[3];
                for (int j = 0; j < 3; j++)
                {
                    var bone = animator.GetBoneTransform(fingerIds[f][j]);
                    a.Fingers[f][j] = bone;
                    if (bone == null) continue;
                    a.FingerRest[f][j] = bone.localRotation;
                    Vector3 dir = BoneDirection(bone);
                    // Rotating dir about (dir x toward) moves the fingertip toward the palm side.
                    // The thumb also folds across the palm, toward the middle-finger knuckle.
                    Vector3 toward = palmNormal;
                    if (f == 0) toward = (palmNormal + (middleK.position - bone.position).normalized).normalized;
                    Vector3 axis = Vector3.Cross(dir, toward);
                    if (axis.sqrMagnitude < 1e-6f) axis = a.Hand.right;
                    a.BendAxisLocal[f][j] = bone.InverseTransformDirection(axis.normalized);
                }
                // Splay: rotate the proximal bone away from the middle finger, in the palm plane.
                var prox = a.Fingers[f][0];
                if (f == 0 || f == 2 || prox == null) continue;
                Vector3 away = Vector3.ProjectOnPlane(prox.position - middleK.position, palmNormal);
                Vector3 pd = BoneDirection(prox);
                Vector3 sAxis = Vector3.Cross(pd, away);
                if (sAxis.sqrMagnitude > 1e-8f) a.SpreadAxisLocal[f] = prox.InverseTransformDirection(sAxis.normalized);
            }

            var tip = a.Fingers[2][2];
            a.PalmLength = Vector3.Distance(a.Hand.position, middleK.position);
            a.HandLength = tip != null
                ? a.PalmLength + Vector3.Distance(middleK.position, tip.position) + 0.025f // distal bone length is not in the rig
                : a.PalmLength * 1.9f;

            a.UpperRest = a.Upper.localRotation;
            a.LowerRest = a.Lower.localRotation;
            a.HandRest = a.Hand.localRotation;
            a.RestPos = transform.InverseTransformPoint(a.Hand.position);
            a.RestRot = Quaternion.Inverse(transform.rotation) * a.Hand.rotation;
            a.Shoulder = transform.InverseTransformPoint(a.Upper.position);
            a.FrameToBone = Quaternion.Inverse(Quaternion.LookRotation(fingerDir, palmNormal)) * a.Hand.rotation;
            return a;
        }

        // ================= Public API =================

        /// <summary>Performs one sign from its action definition. Replaces any demonstration in progress.</summary>
        public void Demonstrate(SignTemplate template)
        {
            if (!_ready) Setup();
            if (!_ready || template == null) return;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Run(template));
        }

        /// <summary>
        /// Holds one moment of a sign without timing (checks and stills).
        /// u &lt; 0: start pose; 0..1: point in the movement; head and face are set to match.
        /// </summary>
        public void PoseAt(SignTemplate template, float u)
        {
            if (!_ready) Setup();
            if (!_ready || template == null) return;
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            IsDemonstrating = false;
            var plan = Plan(template);
            float m = Mathf.Clamp01(u);
            ApplyMovement(template, plan, m, 1f);
            float dur = Mathf.Max(0.01f, template.movement.durationSec);
            ApplyHead(template, u < 0f ? 0f : m * dur / Mathf.Max(dur, minHeadSeconds));
            if (face != null) face.Expression(template.face.smile, template.face.jawOpen, immediate: true);
        }

        /// <summary>Back to the rest pose, neutral face.</summary>
        public void Rest()
        {
            if (!_ready) Setup();
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            IsDemonstrating = false;
            ApplyRest();
            if (face != null) face.Neutral(immediate: true);
        }

        // ================= Sequence =================

        private IEnumerator Run(SignTemplate t)
        {
            IsDemonstrating = true;
            var plan = Plan(t);
            float dur = Mathf.Max(0.05f, t.movement.durationSec);
            float headDur = t.head.type == "none" ? 0f : Mathf.Max(dur, minHeadSeconds);
            ApplyHead(t, 0f);

            // 1) Move to the start location, fingers to the start curl, palm to the start orientation.
            for (float s = 0f; s < toStartSeconds; s += Time.deltaTime)
            {
                ApplyMovement(t, plan, 0f, Smooth(s / toStartSeconds));
                yield return null;
            }

            // 2) Perform the movement; face fades in with it, head moves with it.
            if (face != null) face.Expression(t.face.smile, t.face.jawOpen);
            float tau = 0f;
            for (; tau < dur; tau += Time.deltaTime)
            {
                ApplyMovement(t, plan, tau / dur, 1f);
                ApplyHead(t, headDur > 0f ? tau / headDur : 0f);
                yield return null;
            }

            // 3) Hold the end pose so the learner sees the end point (the head finishes its nod / shake).
            for (float h = 0f; h < holdEndSeconds; h += Time.deltaTime, tau += Time.deltaTime)
            {
                ApplyMovement(t, plan, 1f, 1f);
                ApplyHead(t, headDur > 0f ? tau / headDur : 0f);
                yield return null;
            }
            ApplyHead(t, 1f);

            // 4) Back to rest; face fades out.
            if (face != null) face.Neutral();
            for (float r = 0f; r < returnSeconds; r += Time.deltaTime)
            {
                ApplyMovement(t, plan, 1f, 1f - Smooth(r / returnSeconds));
                yield return null;
            }
            ApplyRest();
            IsDemonstrating = false;
            _routine = null;
            DemonstrationFinished?.Invoke();
        }

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        private static float EaseOut(float x)
        {
            x = Mathf.Clamp01(x);
            return 1f - (1f - x) * (1f - x);
        }

        // ================= Sign layout (§4.3 start locations) =================

        private struct SignPlan
        {
            public HandPose RightStart, LeftStart;
            public bool LeftActive;
        }

        /// <summary>Hand pose from a finger direction and palm normal (teacher space).</summary>
        private HandPose Pose(Arm a, Vector3 pos, Vector3 fingerDir, Vector3 palm, float[] curl, float spread)
        {
            Vector3 p = palm.normalized;
            Vector3 fd = Vector3.ProjectOnPlane(fingerDir, p);
            if (fd.sqrMagnitude < 1e-6f) fd = Vector3.ProjectOnPlane(Vector3.up, p);
            if (fd.sqrMagnitude < 1e-6f) fd = Vector3.ProjectOnPlane(Vector3.forward, p);
            return new HandPose
            {
                Pos = pos,
                BoneRot = Quaternion.LookRotation(fd.normalized, p) * a.FrameToBone,
                Curl = curl,
                Spread = spread,
            };
        }

        private static Vector3 Mirror(Vector3 v) => new Vector3(-v.x, v.y, v.z);

        private static Vector3 FingerDirOf(Quaternion boneRot, Arm a) => (boneRot * Quaternion.Inverse(a.FrameToBone)) * Vector3.forward;

        private SignPlan Plan(SignTemplate t)
        {
            var plan = new SignPlan();
            float[] curlR = t.curlRight;
            float[] curlL = t.HasLeftCurl ? t.curlLeft : restCurl;
            Vector3 palmR = t.palm.sqrMagnitude > 1e-4f ? t.palm.normalized : Vector3.forward;
            Vector3 up = Vector3.up, fwd = Vector3.forward;

            switch (t.movement.type)
            {
                case MovementTypes.Wave: // HELLO: B-hand beside the temple, fingers up, palm forward
                {
                    var tmp = Pose(_right, Vector3.zero, up + fwd * 0.15f, palmR, curlR, t.spread);
                    Vector3 tip = Temple(1f) + new Vector3(0.10f, 0.02f, 0.05f);
                    tmp.Pos = tip - FingerDirOf(tmp.BoneRot, _right) * _right.HandLength;
                    plan.RightStart = tmp;
                    break;
                }
                case MovementTypes.PushForward: // THANK-YOU: flat hand, fingertips at the lips, palm toward self
                {
                    var tmp = Pose(_right, Vector3.zero, up, palmR, curlR, t.spread);
                    Vector3 tip = Chin + new Vector3(0f, 0.035f, 0.035f);
                    tmp.Pos = tip - FingerDirOf(tmp.BoneRot, _right) * _right.HandLength;
                    plan.RightStart = tmp;
                    break;
                }
                case MovementTypes.FlickOut: // HOT: claw at the mouth, palm toward self
                {
                    var tmp = Pose(_right, Vector3.zero, up, palmR, curlR, t.spread);
                    Vector3 palmCenter = Mouth + new Vector3(0f, -0.06f, 0.09f);
                    tmp.Pos = palmCenter - FingerDirOf(tmp.BoneRot, _right) * (_right.PalmLength * 0.6f);
                    plan.RightStart = tmp;
                    break;
                }
                case MovementTypes.Crank: // COFFEE: S-fists stacked, dominant on top, bottom fist thumb-side up
                {
                    Vector3 bottom = ChestCenter + new Vector3(-0.05f, -0.02f, -0.02f);
                    var l = Pose(_left, Vector3.zero, fwd + Vector3.right * 0.35f, Vector3.right, curlL, 0f);
                    l.Pos = bottom;
                    var r = Pose(_right, Vector3.zero, fwd - Vector3.right * 0.35f, palmR, curlR, 0f);
                    r.Pos = bottom + new Vector3(0.025f, 0.075f, 0.0f);
                    plan.LeftStart = l;
                    plan.RightStart = r;
                    plan.LeftActive = true;
                    break;
                }
                case MovementTypes.Shake: // COLD: two S-fists ~15 cm apart, palms in
                {
                    Vector3 c = ChestCenter + new Vector3(0f, -0.02f, -0.02f);
                    plan.RightStart = Pose(_right, c + new Vector3(0.075f, 0f, 0f), up + fwd * 0.3f, palmR, curlR, 0f);
                    plan.LeftStart = Pose(_left, c + new Vector3(-0.075f, 0f, 0f), up + fwd * 0.3f, Mirror(palmR), curlL, 0f);
                    plan.LeftActive = true;
                    break;
                }
                case MovementTypes.Lift: // HELP: dominant A-hand resting on the non-dominant flat palm (palm up)
                {
                    var l = Pose(_left, Vector3.zero, fwd + Vector3.right * 0.4f, up, curlL, 0f);
                    l.Pos = ChestCenter + new Vector3(-0.06f, -0.12f, -0.03f);
                    Vector3 lFinger = FingerDirOf(l.BoneRot, _left);
                    Vector3 palmCenter = l.Pos + lFinger * (_left.PalmLength * 0.8f);
                    var r = Pose(_right, Vector3.zero, fwd - Vector3.right * 0.2f, palmR, curlR, 0f);
                    Vector3 rFinger = FingerDirOf(r.BoneRot, _right);
                    r.Pos = palmCenter + up * 0.05f - rFinger * (_right.PalmLength * 0.55f);
                    plan.LeftStart = l;
                    plan.RightStart = r;
                    plan.LeftActive = true;
                    break;
                }
                default: // YES, NO (and anything without movement): chest height, fingers up
                    plan.RightStart = Pose(_right, ChestSpot(_right), up, palmR, curlR, t.spread);
                    break;
            }

            if (!plan.LeftActive) plan.LeftStart = RestPose(_left);
            return plan;
        }

        private HandPose RestPose(Arm a) => new HandPose { Pos = a.RestPos, BoneRot = a.RestRot, Curl = restCurl, Spread = 0f };

        // ================= Movement (§4.3) =================

        /// <summary>Poses both arms at movement phase u (0..1), blended from rest by weight w.</summary>
        private void ApplyMovement(SignTemplate t, SignPlan plan, float u, float w)
        {
            HandPose r = plan.RightStart, l = plan.LeftStart;
            // Same movement numbers as the feedback ghost hands (SignMotion).
            var s = SignMotion.Sample(t, u, FingerDirOf(r.BoneRot, _right));
            r.Pos += s.RightOffset;
            r.BoneRot = s.RightRotation * r.BoneRot;
            if (s.RightCurl != null) r.Curl = s.RightCurl;
            l.Pos += s.LeftOffset;

            ApplyArm(_right, Blend(RestPose(_right), r, w));
            ApplyArm(_left, plan.LeftActive ? Blend(RestPose(_left), l, w) : RestPose(_left));
        }

        private static void RotateAboutWrist(ref HandPose p, Quaternion q) => p.BoneRot = q * p.BoneRot;

        private static float[] LerpCurl(float[] a, float[] b, float s)
        {
            var c = new float[5];
            for (int i = 0; i < 5; i++) c[i] = Mathf.Lerp(a[i], b[i], s);
            return c;
        }

        private static HandPose Blend(HandPose a, HandPose b, float w) => new HandPose
        {
            Pos = Vector3.Lerp(a.Pos, b.Pos, w),
            BoneRot = Quaternion.Slerp(a.BoneRot, b.BoneRot, w),
            Curl = LerpCurl(a.Curl, b.Curl, w),
            Spread = Mathf.Lerp(a.Spread, b.Spread, w),
        };

        // ================= Head (§3.4) =================

        /// <summary>YES: pitch ±amp nods; NO: yaw ±amp shakes; others still. hu = 0..1 through the head motion.</summary>
        private void ApplyHead(SignTemplate t, float hu)
        {
            if (_head == null) return;
            _head.localRotation = _headRest;
            if (hu <= 0f || hu >= 1f || t.head == null || t.head.type == "none") return;
            float a = t.head.amplitudeDeg * Mathf.Sin(2f * Mathf.PI * Mathf.Max(1, t.head.repeats) * hu);
            Vector3 axis = t.head.type == "nod" ? transform.right : t.head.type == "shake" ? transform.up : Vector3.zero;
            if (axis == Vector3.zero) return;
            _head.rotation = Quaternion.AngleAxis(a, axis) * _head.rotation;
        }

        // ================= Bone posing =================

        private void ApplyRest()
        {
            ApplyArm(_right, RestPose(_right));
            ApplyArm(_left, RestPose(_left));
            if (_head != null) _head.localRotation = _headRest;
        }

        private void ApplyArm(Arm a, HandPose p)
        {
            if (!_ready) return;
            a.Upper.localRotation = a.UpperRest;
            a.Lower.localRotation = a.LowerRest;
            a.Hand.localRotation = a.HandRest;
            SolveTwoBone(a, transform.TransformPoint(p.Pos));
            a.Hand.rotation = transform.rotation * p.BoneRot;

            for (int f = 0; f < 5; f++)
            {
                float curl = p.Curl[f];
                Vector3 max = f == 0 ? thumbMaxDegrees : fingerMaxDegrees;
                for (int j = 0; j < 3; j++)
                {
                    var bone = a.Fingers[f][j];
                    if (bone == null) continue;
                    float deg = curl * (j == 0 ? max.x : j == 1 ? max.y : max.z);
                    Quaternion q = a.FingerRest[f][j];
                    if (j == 0 && f > 0 && a.SpreadAxisLocal[f] != Vector3.zero)
                    {
                        float sd = p.Spread * spreadDegrees[f - 1];
                        q *= Quaternion.AngleAxis(sd, a.SpreadAxisLocal[f]);
                    }
                    bone.localRotation = q * Quaternion.AngleAxis(deg, a.BendAxisLocal[f][j]);
                }
            }
        }

        /// <summary>Analytic two-bone IK: places the elbow so the hand reaches the target, elbow pointing down and out.</summary>
        private void SolveTwoBone(Arm arm, Vector3 target)
        {
            Vector3 a = arm.Upper.position, b = arm.Lower.position, c = arm.Hand.position;
            float lenA = Vector3.Distance(a, b), lenB = Vector3.Distance(b, c);
            Vector3 toTarget = target - a;
            float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(lenA - lenB) + 1e-3f, lenA + lenB - 1e-3f);
            Vector3 dir = toTarget.normalized;
            float cosA = Mathf.Clamp((lenA * lenA + d * d - lenB * lenB) / (2f * lenA * d), -1f, 1f);
            Vector3 pole = Vector3.ProjectOnPlane(-transform.up + transform.right * (arm.Side * 0.4f), dir).normalized;
            Vector3 elbow = a + dir * (lenA * cosA) + pole * (lenA * Mathf.Sqrt(1f - cosA * cosA));

            arm.Upper.rotation = Quaternion.FromToRotation(b - a, elbow - a) * arm.Upper.rotation;
            b = arm.Lower.position;
            c = arm.Hand.position;
            arm.Lower.rotation = Quaternion.FromToRotation(c - b, (a + dir * d) - b) * arm.Lower.rotation;
        }

        private void PoseRestBody()
        {
            // Point each limb at a target direction instead of rotating by a fixed angle, so the rest pose is
            // right whatever the model's bind pose is (T-pose, A-pose, ...).
            float down = armsDownDegrees * Mathf.Deg2Rad;
            Vector3 ArmDir(float side) => (-transform.up * Mathf.Sin(down) + transform.right * side * Mathf.Cos(down)).normalized;
            AimBone(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, ArmDir(-1f));
            AimBone(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, ArmDir(1f));
            AimBone(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, ArmDir(-1f));
            AimBone(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, ArmDir(1f));

            if (!seated) return;
            AimBone(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, transform.forward);   // thighs forward
            AimBone(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, transform.forward);
            AimBone(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, -transform.up);          // shins down
            AimBone(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, -transform.up);
        }

        /// <summary>Rotates a bone so the direction to its child bone matches the target (world space).</summary>
        private void AimBone(HumanBodyBones bone, HumanBodyBones child, Vector3 targetDir)
        {
            var b = animator.GetBoneTransform(bone);
            var c = animator.GetBoneTransform(child);
            if (b == null || c == null) return;
            Vector3 current = c.position - b.position;
            if (current.sqrMagnitude < 1e-8f) return;
            b.rotation = Quaternion.FromToRotation(current, targetDir) * b.rotation;
        }

        private Transform FindByName(string boneName)
        {
            foreach (var tr in GetComponentsInChildren<Transform>(true))
            {
                if (tr.name == boneName) return tr;
            }
            return null;
        }

        private static Vector3 BoneDirection(Transform bone)
        {
            if (bone.childCount > 0) return (bone.GetChild(0).position - bone.position).normalized;
            return bone.parent != null ? (bone.position - bone.parent.position).normalized : bone.forward;
        }
    }
}
