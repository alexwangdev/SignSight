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

        [Header("v3.10 wrist limits: how far the hand may turn away from the forearm (degrees)")]
        [Tooltip("v3.10 verify: OFF = v3.9 wrist behaviour. The clamp broke 9 scored self-test words (wrist turn too small); see v310-verify.txt.")]
        [SerializeField] private bool wristClampEnabled = false;
        [Tooltip("v3.12: wrist flexion / extension (pitch), degrees. Standard wrist range of motion: flexion ~70-80, extension ~60-70; 70 used.")]
        [SerializeField] private float wristPitchMaxDeg = 70f;
        [Tooltip("v3.12: wrist radial / ulnar deviation (yaw), degrees. Standard range: radial ~20, ulnar ~30-35; 30 used.")]
        [SerializeField] private float wristYawMaxDeg = 30f;
        [Tooltip("v3.12: wrist twist TOLERANCE, degrees, one value for all words. Twist is forearm pronation / supination; the roll search leaves about 17 deg on WHERE and under 3 deg on the others, so 0 punished the search, not the wrist. 15 used.")]
        [SerializeField] private float wristTwistMaxDeg = 15f;
        [Tooltip("Sideways splay at spread = 1, per finger: index, middle, ring, pinky (degrees away from the middle finger).")]
        [SerializeField] private Vector4 spreadDegrees = new Vector4(10f, 0f, 8f, 16f);
        [Tooltip("Relaxed hand at rest.")]
        [SerializeField] private float[] restCurl = { 0.15f, 0.12f, 0.12f, 0.12f, 0.12f };

        [Header("Rest pose")]
        [SerializeField] private float armsDownDegrees = 75f;
        [SerializeField] private bool seated = true;

        /// <summary>Raised when a demonstration has fully returned to rest.</summary>
        /// <summary>Demo speed: 1 = normal, 0.5 = half speed (v3.1, learner-selectable). Timing only; shapes are unchanged.</summary>
        public float Speed
        {
            get => _speed;
            set => _speed = Mathf.Clamp(value, 0.25f, 1f);
        }
        private float _speed = 1f;

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
            public Vector3 Pole;             // v3.10 step 2: damped elbow pole direction (teacher space)
            public Vector3 Chosen;           // v3.11: damped elbow pole after the swivel search
            public float Roll;               // v3.11: damped forearm roll (pronation / supination), degrees
            public bool RollInit;
            public float DevAngle, DevSwing, DevTwist; // v3.11 measurement: wrist deviation from the forearm (degrees)
            public float DevPitch, DevYaw;             // v3.12: flexion (+) / extension (-) and radial-ulnar yaw (degrees)
            public Quaternion DevPrev;       // v3.15: last wrist deviation during travel (keeps the blend on one side)
            public bool DevInit;
            public bool FollowInit;          // v3.10 steps 3-4: the damped follow state below is set
            public Vector3 FollowPos;        // damped wrist position (teacher space)
            public Quaternion FollowRot;     // damped hand bone rotation (teacher space)
            public float[] FollowCurl;       // damped finger curl
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
            WristClampedMaxDeg = 0f;
            SetWord(template);
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
            SetWord(template);
            var plan = Plan(template);
            float m = Mathf.Clamp01(u);
            ApplyMovement(template, plan, m, 1f);
            float dur = Mathf.Max(0.01f, template.movement.durationSec);
            ApplyHead(template, u < 0f ? 0f : m * dur / Mathf.Max(dur, minHeadSeconds));
            if (face != null) face.SetExpression(template.face, immediate: true);
        }

        /// <summary>Editor checks: the settled hold pose (u = 1) of a sign and its wrist deviation report.</summary>
        public string MeasureHold(SignTemplate template, float u = 1f, float w = 1f)
        {
            PoseAt(template, 1f);
            var plan = Plan(template);
            // the elbow pole and the forearm roll are damped: repeat the end pose until they have settled
            for (int i = 0; i < 400; i++) ApplyMovement(template, plan, u, w);
            RecordHold();
            return HoldReport;
        }

        [Header("v3.14 arm fixes (YES, BAD, HOW only; the other words keep the v3.13 arm)")]
        [Tooltip("Words that use the v3.14 arm fixes: roll-aware swivel, carried end finger direction, mirrored left hand.")]
        [SerializeField] private string[] armFixWords = { "YES", "BAD", "HOW" };
        [Tooltip("Swivel objective for those words. 0 = v3.11 (total wrist deviation, before the forearm roll). " +
                 "1 = wrist swing left after the forearm roll has taken the twist. 2 = same, pitch and yaw each as a share of its range.")]
        [SerializeField] private int armFixObjective = 1;
        [Tooltip("Where the right hand's fingers end (teacher space) when the shortest turn to the end palm would leave them pointing back at the signer.")]
        [SerializeField] private Vector3 endFingerTarget = new Vector3(-1f, 0f, 0.6f);
        [Tooltip("Elbow prior for those words: cost in wrist degrees per degree the elbow turns away from the down-and-out / back-and-out pole. Keeps the elbow out of the body and below the head while the hand travels.")]
        [SerializeField] private float armFixPolePrior = 0.3f;
        [Tooltip("v3.15: the swivel objective and the travel rule apply to every word, except the words listed in armFixRevertWords (kept on the v3.13 arm because the new one measured worse for them).")]
        [SerializeField] private bool armFixAllWords = true;
        [SerializeField] private string[] armFixRevertWords = { }; // v3.16: empty. The v3.15 list was chosen by wrist bend alone, which rewarded impossible elbows
        [Tooltip("v3.17: when the wrists are crossed, the right wrist sits this far in front of the left one (metres), so the forearms do not pass through each other. 0 = off.")]
        [SerializeField] private float crossedHandsGap = 0.05f;
        private bool _fix;        // v3.14 swivel objective at the sign itself (and the travel rule)
        private bool _travel;     // v3.15: travel rule only (every word): between rest and the sign the hand follows the forearm
        private bool _fixLayout;  // v3.14 layout rules (mirrored left hand, carried end finger direction): armFixWords only
        private float _blendW = 1f; // weight of the sign pose over the rest pose in the frame being posed (1 = the sign itself)

        /// <summary>
        /// v3.14: while the hand travels between rest and the sign, it first follows the forearm and only turns into the
        /// sign's orientation over the last part of the travel. Half-way between two unrelated orientations there is no arm
        /// that carries the hand, and the wrist took the whole difference.
        /// </summary>
        private float WristWeight() => _blendW >= 1f ? 1f
            // close to rest (the first / last 15 % of the travel) the pose is the rest pose itself, so leaving and reaching rest has no snap
            : Mathf.Max(Smooth((_blendW - 0.4f) / 0.6f), 1f - Smooth(_blendW / 0.15f));

        private void SetWord(SignTemplate t)
        {
            _fix = false;
            _fixLayout = false;
            _travel = false;
            if (t == null || string.IsNullOrEmpty(t.intent)) return;
            string w = t.intent;
            int hash = w.IndexOf('#');
            if (hash >= 0) w = w.Substring(0, hash);
            _fixLayout = Listed(armFixWords, w);
            _fix = armFixAllWords ? !Listed(armFixRevertWords, w) : _fixLayout;
            _travel = armFixAllWords || _fix;
        }

        private static bool Listed(string[] list, string w)
        {
            if (list == null) return false;
            foreach (var x in list) if (x == w) return true;
            return false;
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

        /// <summary>
        /// v3.2 B4: replays only the face and head of a sign (hands at rest) at 'speed', so the learner looks at the
        /// face. Display only. Raises DemonstrationFinished at the end like a normal demonstration.
        /// </summary>
        public void DemonstrateFace(SignTemplate template, float speed = 0.5f)
        {
            if (!_ready) Setup();
            if (!_ready || template == null) return;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(RunFace(template, Mathf.Clamp(speed, 0.25f, 1f)));
        }

        public bool IsFaceOnly { get; private set; }

        private IEnumerator RunFace(SignTemplate t, float speed)
        {
            IsDemonstrating = true;
            IsFaceOnly = true;
            float slow = 1f / speed;
            ApplyRest();
            float headDur = Mathf.Max(t.movement.durationSec, minHeadSeconds) * slow;
            float total = headDur + holdEndSeconds * slow;
            if (face != null) face.SetExpression(t.face);
            for (float tau = 0f; tau < total; tau += Time.deltaTime)
            {
                ApplyArm(_right, RestPose(_right));
                ApplyArm(_left, RestPose(_left));
                ApplyHead(t, t.head.type == "none" ? 0f : tau / headDur);
                yield return null;
            }
            ApplyHead(t, 1f);
            if (face != null) face.Neutral();
            for (float r = 0f; r < returnSeconds * slow; r += Time.deltaTime) yield return null;
            IsDemonstrating = false;
            IsFaceOnly = false;
            _routine = null;
            DemonstrationFinished?.Invoke();
        }

        // ================= Sequence =================

        [Header("v3.15 timing (v3.10 steps 3-4): OFF (all 0). Tried at 0.10 / 0.05 / 0.5 / 40 and measured rougher, see v3-15-report.md")]
        [Tooltip("Step 3: the wrist rotation follows the arm position by this many seconds (0 = off).")]
        [SerializeField] private float wristLagSeconds = 0f;
        [Tooltip("Step 3: the finger curl follows the arm position by this many seconds (0 = off).")]
        [SerializeField] private float curlLagSeconds = 0f;
        [Tooltip("Step 3: ease-out for the move to the start; ease-in-out (this strength, 0..1) for the movement itself. 0 = v3.14 timing.")]
        [SerializeField] private float phaseEasing = 0f;
        [Tooltip("Step 4: exponential damp of the hand pose across phase changes, per second (0 = off). 40 = a 25 ms time constant.")]
        [SerializeField] private float phaseDamp = 0f;

        /// <summary>The phases of one demonstration on a single clock (seconds from the start).</summary>
        private sealed class Timeline
        {
            public SignTemplate T, Beat;
            public SignPlan Plan, Plan2;
            public HandPose R1, L1, R2, L2;            // end of beat 1, start of beat 2
            public float ToStart, Dur, Dur2, Return;
            public float TMove, THold, TBeat, TMove2, THold2, TReturn, Total;
        }

        /// <summary>Both hand poses at time x on the demonstration clock; w = weight of the sign over the rest pose.</summary>
        private void Eval(Timeline tl, float x, out HandPose r, out HandPose l, out float w)
        {
            w = 1f;
            if (x <= 0f)
            {
                r = RestPose(_right);
                l = RestPose(_left);
                w = 0f;
                return;
            }
            if (x < tl.TMove) // 1) move to the start
            {
                float k = x / tl.ToStart;
                w = phaseEasing > 0f ? EaseOut(k) : Smooth(k);
                Poses(tl.T, tl.Plan, 0f, out r, out l);
                r = Blend(RestPose(_right), r, w);
                l = tl.Plan.LeftActive ? Blend(RestPose(_left), l, w) : RestPose(_left);
                return;
            }
            if (x < tl.TBeat) // 2) the movement, 3) the hold
            {
                float u = Mathf.Clamp01((x - tl.TMove) / tl.Dur);
                if (phaseEasing > 0f) u = Mathf.Lerp(u, Smooth(u), phaseEasing);
                Poses(tl.T, tl.Plan, u, out r, out l);
                if (!tl.Plan.LeftActive) l = RestPose(_left);
                return;
            }
            SignTemplate last = tl.T;
            SignPlan lastPlan = tl.Plan;
            if (tl.Beat != null)
            {
                if (x < tl.TMove2) // 3b) into the second beat
                {
                    float k = Smooth((x - tl.TBeat) / tl.ToStart);
                    r = Blend(tl.R1, tl.R2, k);
                    l = Blend(tl.L1, tl.L2, k);
                    return;
                }
                if (x < tl.TReturn)
                {
                    float u = Mathf.Clamp01((x - tl.TMove2) / tl.Dur2);
                    if (phaseEasing > 0f) u = Mathf.Lerp(u, Smooth(u), phaseEasing);
                    Poses(tl.Beat, tl.Plan2, u, out r, out l);
                    if (!tl.Plan2.LeftActive) l = RestPose(_left);
                    return;
                }
                last = tl.Beat;
                lastPlan = tl.Plan2;
            }
            // 4) back to rest
            w = 1f - Smooth((x - tl.TReturn) / tl.Return);
            Poses(last, lastPlan, 1f, out r, out l);
            r = Blend(RestPose(_right), r, w);
            l = lastPlan.LeftActive ? Blend(RestPose(_left), l, w) : RestPose(_left);
        }

        /// <summary>Step 4: the pose follows its target through an exponential damp, so a phase change has no velocity jump.</summary>
        private void Damp(Arm a, ref HandPose p)
        {
            if (phaseDamp <= 0f) return;
            if (!a.FollowInit || a.FollowCurl == null)
            {
                a.FollowPos = p.Pos;
                a.FollowRot = p.BoneRot;
                a.FollowCurl = (float[])p.Curl.Clone();
                a.FollowInit = true;
            }
            float k = 1f - Mathf.Exp(-phaseDamp * Time.deltaTime);
            a.FollowPos = Vector3.Lerp(a.FollowPos, p.Pos, k);
            a.FollowRot = Quaternion.Slerp(a.FollowRot, p.BoneRot, k);
            for (int i = 0; i < 5; i++) a.FollowCurl[i] = Mathf.Lerp(a.FollowCurl[i], p.Curl[i], k);
            p.Pos = a.FollowPos;
            p.BoneRot = a.FollowRot;
            p.Curl = a.FollowCurl;
        }

        private IEnumerator Run(SignTemplate t)
        {
            IsDemonstrating = true;
            float slow = 1f / _speed;
            var beat = SignMotion.BeatTemplate(t);
            var tl = new Timeline
            {
                T = t, Beat = beat, Plan = Plan(t),
                ToStart = toStartSeconds * slow,
                Dur = Mathf.Max(0.05f, t.movement.durationSec) * slow,
                Return = returnSeconds * slow,
            };
            float hold = (beat != null ? 0.25f : holdEndSeconds) * slow;
            tl.TMove = tl.ToStart;
            tl.THold = tl.TMove + tl.Dur;
            tl.TBeat = tl.THold + hold;
            tl.TMove2 = tl.THold2 = tl.TReturn = tl.TBeat;
            if (beat != null)
            {
                // Compound signs (GOOD-MORNING, TEACHER, TODAY): move into the second beat and perform it.
                tl.Plan2 = Plan(beat);
                Poses(t, tl.Plan, 1f, out tl.R1, out tl.L1);
                Poses(beat, tl.Plan2, 0f, out tl.R2, out tl.L2);
                tl.Dur2 = Mathf.Max(0.05f, beat.movement.durationSec) * slow;
                tl.TMove2 = tl.TBeat + tl.ToStart;
                tl.THold2 = tl.TMove2 + tl.Dur2;
                tl.TReturn = tl.THold2 + holdEndSeconds * slow;
            }
            tl.Total = tl.TReturn + tl.Return;
            float headDur = t.head.type == "none" ? 0f : Mathf.Max(tl.Dur, minHeadSeconds);
            _right.FollowInit = _left.FollowInit = false;
            bool faceOn = false, faceOff = false;
            ApplyHead(t, 0f);

            for (float x = 0f; x < tl.Total; x += Time.deltaTime)
            {
                // Step 3: the arm position leads; the wrist rotation and the finger curl follow a little later.
                // The lags close over the return, so the demonstration still ends at rest on time.
                float open = Mathf.Clamp01((tl.Total - x) / tl.Return);
                Eval(tl, x, out var r, out var l, out _);
                Eval(tl, x - wristLagSeconds * open, out var rr, out var rl, out float wRot);
                Eval(tl, x - curlLagSeconds * open, out var cr, out var cl, out _);
                r.BoneRot = rr.BoneRot; r.Curl = cr.Curl; r.Spread = cr.Spread;
                l.BoneRot = rl.BoneRot; l.Curl = cl.Curl; l.Spread = cl.Spread;
                Damp(_right, ref r);
                Damp(_left, ref l);
                _blendW = wRot;
                ApplyArm(_right, r);
                ApplyArm(_left, l);
                _blendW = 1f;

                // face fades in with the movement and out with the return; the head moves with the movement and the hold
                if (!faceOn && x >= tl.TMove) { faceOn = true; if (face != null) face.SetExpression(t.face); }
                if (!faceOff && x >= tl.TReturn) { faceOff = true; if (face != null) face.Neutral(); }
                if (x >= tl.TBeat) ApplyHead(t, 1f);
                else ApplyHead(t, headDur > 0f && x > tl.TMove ? (x - tl.TMove) / headDur : 0f);
                if (x >= tl.THold && x < tl.TBeat) RecordHold();
                yield return null;
            }
            if (face != null && !faceOff) face.Neutral();
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
            public float EndYaw;      // v3.14: turn of the right hand about the end palm normal over the movement (degrees)
            public Vector3 EndAxis;
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
            if (!string.IsNullOrEmpty(t.anchor)) return PlanAnchored(t);
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

        // ---------------- v3.1: start pose from a named anchor (SignAnchors) ----------------

        private Vector3 AnchorPoint(string name, Vector3 offset)
        {
            SignAnchors.TryGet(name, out var p);
            return _eyes + p + offset;
        }

        private SignPlan PlanAnchored(SignTemplate t)
        {
            var plan = new SignPlan();
            float[] curlR = t.curlRight;
            Vector3 palmR = t.palm.sqrMagnitude > 1e-4f ? t.palm.normalized : Vector3.forward;
            Vector3 fdR = t.fingerDir.sqrMagnitude > 1e-4f ? t.fingerDir.normalized : Vector3.up;
            var r = Pose(_right, ChestSpot(_right), fdR, palmR, curlR, t.spread);

            if (t.TwoHanded)
            {
                Vector3 palmL = t.leftPalm.sqrMagnitude > 1e-4f ? t.leftPalm.normalized : Mirror(palmR);
                Vector3 fdL = t.leftFingerDir.sqrMagnitude > 1e-4f ? t.leftFingerDir.normalized : Mirror(fdR);
                float[] curlL = t.HasLeftCurl ? t.curlLeft : curlR;
                var l = Pose(_left, ChestSpot(_left), fdL, palmL, curlL, t.spread);
                Vector3 lTarget;
                if (t.leftAnchor == "mirror") lTarget = Mirror(AnchorPoint(t.anchor, t.anchorOffset) - _eyes) + _eyes;
                else if (SignAnchors.TryGet(t.leftAnchor, out _)) lTarget = AnchorPoint(t.leftAnchor, t.leftOffset);
                else lTarget = Mirror(ChestSpot(_right) - _eyes) + _eyes;
                l.Pos = PlaceContact(_left, l, t.leftContact, lTarget);
                plan.LeftStart = l;
                plan.LeftActive = true;
            }

            Vector3 rTarget = SignAnchors.IsOnLeftHand(t.anchor) && plan.LeftActive
                ? LeftHandPoint(plan.LeftStart, t.anchor)
                : AnchorPoint(t.anchor, t.anchorOffset);
            r.Pos = PlaceContact(_right, r, t.contact, rTarget);
            if (plan.LeftActive && crossedHandsGap > 0f && r.Pos.x < plan.LeftStart.Pos.x - 0.02f
                && Mathf.Abs(r.Pos.y - plan.LeftStart.Pos.y) < 0.08f && Mathf.Abs(r.Pos.z - plan.LeftStart.Pos.z) < crossedHandsGap)
            {
                // v3.17: the wrists are crossed (right wrist on the left side) at the same depth, so the two forearms pass
                // through each other. The right arm goes in front, by about one forearm thickness.
                r.Pos.z = plan.LeftStart.Pos.z + crossedHandsGap;
            }
            plan.RightStart = r;
            if (_fixLayout && plan.LeftActive && t.leftAnchor == "mirror")
            {
                // v3.14: "mirror" = the left hand is the mirror image of the right hand, whatever contact point each one names
                plan.LeftStart.Pos = Mirror(r.Pos - _eyes) + _eyes;
            }
            if (_fixLayout && t.movement.type == MovementTypes.Stroke && t.endPalm.sqrMagnitude > 1e-4f && t.palm.sqrMagnitude > 1e-4f)
            {
                // v3.14: the template fixes the END PALM, not the end finger direction. The shortest turn from the start
                // palm to the end palm can leave the fingers pointing back at the signer, which no arm can carry.
                // Then the hand also turns about the end palm normal, so the fingers end pointing away and across the body.
                Vector3 n = t.endPalm.normalized;
                Vector3 fd = FingerDirOf(Quaternion.FromToRotation(t.palm, t.endPalm) * r.BoneRot, _right);
                if (fd.z < -0.5f)
                {
                    Vector3 from = Vector3.ProjectOnPlane(fd, n), to = Vector3.ProjectOnPlane(new Vector3(endFingerTarget.x * _right.Side, endFingerTarget.y, endFingerTarget.z), n);
                    if (from.sqrMagnitude > 1e-6f && to.sqrMagnitude > 1e-6f)
                    {
                        plan.EndYaw = Vector3.SignedAngle(from, to, n);
                        plan.EndAxis = n;
                    }
                }
            }
            if (!plan.LeftActive) plan.LeftStart = RestPose(_left);
            return plan;
        }

        /// <summary>Wrist position that puts the hand's contact point on the target (measured on the posed rig).</summary>
        private Vector3 PlaceContact(Arm a, HandPose p, string contact, Vector3 target)
        {
            for (int i = 0; i < 4; i++)
            {
                ApplyArm(a, p);
                p.Pos += target - ContactPoint(a, contact);
            }
            return p.Pos;
        }

        private Vector3 Local(Vector3 world) => transform.InverseTransformPoint(world);

        private Vector3 TipOf(Arm a, int finger, float extra)
        {
            var d = a.Fingers[finger][2];
            var m = a.Fingers[finger][1];
            if (d == null || m == null) return Local(a.Hand.position);
            return Local(d.position + (d.position - m.position).normalized * extra);
        }

        /// <summary>Same points as SignAnchors.Contact on the learner's hand (AvatarHandProvider uses the same bones).</summary>
        private Vector3 ContactPoint(Arm a, string contact)
        {
            Vector3 wrist = Local(a.Hand.position);
            Vector3 palm = (wrist + Local(a.Fingers[2][0].position)) * 0.5f;
            switch (contact)
            {
                case "thumb": return TipOf(a, 0, 0.025f);
                case "index": return TipOf(a, 1, 0.022f);
                case "middle": return TipOf(a, 2, 0.022f);
                case "fingers": return (TipOf(a, 1, 0.022f) + TipOf(a, 2, 0.022f)) * 0.5f;
                case "knuckle": return Vector3.Lerp(palm, TipOf(a, 1, 0.022f), 0.6f);
                default: return palm;
            }
        }

        /// <summary>A point on the posed left hand (left-palm | left-wrist | left-index | left-back).</summary>
        private Vector3 LeftHandPoint(HandPose left, string name)
        {
            ApplyArm(_left, left);
            Vector3 wrist = Local(_left.Hand.position);
            Vector3 palm = (wrist + Local(_left.Fingers[2][0].position)) * 0.5f;
            Vector3 n = Vector3.Cross(Local(_left.Fingers[1][0].position) - wrist, Local(_left.Fingers[4][0].position) - wrist).normalized;
            switch (name)
            {
                case "left-wrist": return wrist;
                case "left-index": return TipOf(_left, 1, 0.022f);
                case "left-back": return palm - n * 0.02f;
                default: return palm + n * 0.02f;
            }
        }

        private HandPose RestPose(Arm a) => new HandPose { Pos = a.RestPos, BoneRot = a.RestRot, Curl = restCurl, Spread = 0f };

        // ================= Movement (§4.3) =================

        /// <summary>Poses both arms at movement phase u (0..1), blended from rest by weight w.</summary>
        private void ApplyMovement(SignTemplate t, SignPlan plan, float u, float w)
        {
            Poses(t, plan, u, out var r, out var l);
            _blendW = w;
            ApplyArm(_right, Blend(RestPose(_right), r, w));
            ApplyArm(_left, plan.LeftActive ? Blend(RestPose(_left), l, w) : RestPose(_left));
            _blendW = 1f;
        }

        private void Poses(SignTemplate t, SignPlan plan, float u, out HandPose r, out HandPose l)
        {
            r = plan.RightStart;
            l = plan.LeftStart;
            // Same movement numbers as the feedback ghost hands (SignMotion).
            var s = SignMotion.Sample(t, u, FingerDirOf(r.BoneRot, _right));
            r.Pos += s.RightOffset;
            r.BoneRot = s.RightRotation * r.BoneRot;
            if (plan.EndYaw != 0f) r.BoneRot = Quaternion.AngleAxis(plan.EndYaw * Smooth(u), plan.EndAxis) * r.BoneRot;
            if (s.RightCurl != null) r.Curl = s.RightCurl;
            if (!plan.LeftActive) return;
            l.Pos += s.LeftOffset;
            l.BoneRot = s.LeftRotation * l.BoneRot;
            if (s.LeftCurl != null) l.Curl = s.LeftCurl;
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
            if (t.head.type == "tilt" || t.head.type == "bow")
            {
                // v3.2: held head positions (WH-question tilt, SAD bow): ease in, hold, ease out.
                float env = Smooth(hu / 0.2f) * (1f - Smooth((hu - 0.8f) / 0.2f));
                a = t.head.amplitudeDeg * env;
            }
            Vector3 axis = t.head.type == "nod" || t.head.type == "bow" ? transform.right
                : t.head.type == "shake" ? transform.up
                : t.head.type == "tilt" ? transform.forward : Vector3.zero;
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
            Quaternion desired = transform.rotation * p.BoneRot;
            SolveTwoBone(a, transform.TransformPoint(KeepOutOfTorso(a, p.Pos)), desired);
            Quaternion hand = ClampWrist(a, desired);
            if (_travel && _blendW < 1f)
            {
                // the hand turns from "following the forearm" into the sign's orientation. The turn is taken as an
                // angle about an axis and kept on the same side from frame to frame: a shortest-path blend flips sides
                // when the two orientations pass 180 degrees apart, and the hand would jump.
                Quaternion neutral = a.Lower.rotation * a.HandRest;
                Quaternion dev = Quaternion.Inverse(neutral) * hand;
                if (a.DevInit && Quaternion.Dot(dev, a.DevPrev) < 0f) dev = new Quaternion(-dev.x, -dev.y, -dev.z, -dev.w);
                a.DevPrev = dev;
                a.DevInit = true;
                var ax = new Vector3(dev.x, dev.y, dev.z);
                float ang = 2f * Mathf.Acos(Mathf.Clamp(dev.w, -1f, 1f)) * Mathf.Rad2Deg;
                hand = ax.sqrMagnitude < 1e-10f ? neutral : neutral * Quaternion.AngleAxis(ang * WristWeight(), ax.normalized);
            }
            else a.DevInit = false;
            a.Hand.rotation = hand;

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

        /// <summary>Largest wrist correction (degrees) the clamp applied since the last Demonstrate: a check, not a control.</summary>
        public float WristClampedMaxDeg { get; private set; }

        /// <summary>
        /// v3.10 step 1: the wrist may turn away from the forearm by at most wristSwingMaxDeg (pitch + yaw) and
        /// wristTwistMaxDeg (roll). The template's hand rotation is clamped to that range, never snapped past it.
        /// </summary>
        private Quaternion ClampWrist(Arm a, Quaternion desired)
        {
            Quaternion neutral = a.Lower.rotation * a.HandRest;               // the hand following the forearm exactly
            Quaternion dev = Quaternion.Inverse(neutral) * desired;           // wrist deviation, in the hand's frame
            if (dev.w < 0f) dev = new Quaternion(-dev.x, -dev.y, -dev.z, -dev.w);
            Vector3 fLocal = Quaternion.Inverse(neutral) * (a.Hand.position - a.Lower.position);
            Vector3 ax = fLocal.normalized;                                    // forearm axis, hand frame
            Vector3 f = (a.Hand.position - a.Lower.position).normalized;       // forearm axis, world
            // twist = rotation about the forearm (carried by the roll search; limited to wristTwistMaxDeg)
            Vector3 proj = Vector3.Dot(ax, new Vector3(dev.x, dev.y, dev.z)) * ax;
            Quaternion twist = new Quaternion(proj.x, proj.y, proj.z, dev.w);
            float tn = twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w;
            twist = tn < 1e-8f ? Quaternion.identity : Quaternion.Normalize(twist);
            Quaternion swing = dev * Quaternion.Inverse(twist);
            float twistSigned = 2f * Mathf.Atan2(Vector3.Dot(ax, new Vector3(twist.x, twist.y, twist.z)), twist.w) * Mathf.Rad2Deg;

            // pitch / yaw in the wrist's own frame. The hand's frame: forward = finger, up = palm normal (FrameToBone).
            Quaternion inv = Quaternion.Inverse(a.FrameToBone);
            Vector3 pn = neutral * inv * Vector3.up;                           // palm normal, neutral hand
            Vector3 lat = Vector3.Cross(f, pn).normalized;                     // lateral axis (thumb - pinky line)
            Vector3 fingerN = neutral * inv * Vector3.forward;
            Vector3 fingerD = desired * inv * Vector3.forward;
            // angle of the finger in the sagittal plane (pitch: flexion towards the palm is positive) and the lateral plane (yaw)
            float pitch = Mathf.DeltaAngle(Mathf.Atan2(Vector3.Dot(fingerN, pn), Vector3.Dot(fingerN, f)) * Mathf.Rad2Deg,
                                           Mathf.Atan2(Vector3.Dot(fingerD, pn), Vector3.Dot(fingerD, f)) * Mathf.Rad2Deg);
            float yaw = Mathf.DeltaAngle(Mathf.Atan2(Vector3.Dot(fingerN, lat), Vector3.Dot(fingerN, f)) * Mathf.Rad2Deg,
                                         Mathf.Atan2(Vector3.Dot(fingerD, lat), Vector3.Dot(fingerD, f)) * Mathf.Rad2Deg);

            // v3.12 measurement, always recorded (degrees)
            a.DevAngle = Quaternion.Angle(neutral, desired);
            a.DevSwing = Quaternion.Angle(Quaternion.identity, swing);
            a.DevTwist = Mathf.Abs(twistSigned);
            a.DevPitch = pitch;
            a.DevYaw = yaw;
            if (!wristClampEnabled) return desired; // v3.9 behaviour: the template's hand rotation, unclamped

            // per-axis clamp: the excess beyond each limit is taken off the desired orientation about that axis
            float ep = Mathf.Sign(pitch) * Mathf.Max(0f, Mathf.Abs(pitch) - wristPitchMaxDeg);
            float ey = Mathf.Sign(yaw) * Mathf.Max(0f, Mathf.Abs(yaw) - wristYawMaxDeg);
            float et = Mathf.Sign(twistSigned) * Mathf.Max(0f, Mathf.Abs(twistSigned) - wristTwistMaxDeg);
            // signs: +rotation about lat raises the pitch; +rotation about pn lowers the yaw; +rotation about f raises the twist
            Quaternion clamped = Quaternion.AngleAxis(-et, f) * Quaternion.AngleAxis(ey, pn) * Quaternion.AngleAxis(-ep, lat) * desired;
            WristClampedMaxDeg = Mathf.Max(WristClampedMaxDeg, Mathf.Abs(ep), Mathf.Abs(ey), Mathf.Abs(et));
            return clamped;
        }

        [Tooltip("v3.10 step 2: how quickly the elbow direction follows the hand (per second, exponential damp).")]
        [SerializeField] private float poleDamping = 8f;

        /// <summary>
        /// v3.10 step 2: the elbow points down and out when the hand is high (face, temple, chin) and back and out when it
        /// is low (chest). The direction is damped over time, so the elbow travels with the hand instead of jumping.
        /// </summary>
        private Vector3 ElbowPole(Arm arm, Vector3 target, Vector3 dir)
        {
            Vector3 tl = transform.InverseTransformPoint(target);
            Vector3 sl = transform.InverseTransformPoint(arm.Upper.position);
            // height of the hand relative to the shoulder: 0 = at or below the chest spot, 1 = above the shoulder
            float high = Smooth(Mathf.Clamp01((tl.y - sl.y + 0.15f) / 0.45f));
            Vector3 hi = -transform.up + transform.right * (arm.Side * 0.4f);            // down and out
            Vector3 lo = -transform.forward * 0.8f - transform.up * 0.2f + transform.right * (arm.Side * 0.2f); // back and out
            Vector3 want = Vector3.Lerp(lo, hi, high);
            if (arm.Pole == Vector3.zero) arm.Pole = want;
            else arm.Pole = Vector3.Lerp(arm.Pole, want, 1f - Mathf.Exp(-poleDamping * Time.deltaTime));
            Vector3 p = Vector3.ProjectOnPlane(arm.Pole, dir);
            return p.sqrMagnitude > 1e-6f ? p.normalized : Vector3.ProjectOnPlane(hi, dir).normalized;
        }

        /// <summary>Analytic two-bone IK: places the elbow so the hand reaches the target, elbow pointing down and out.</summary>
        [Header("v3.11 arm carries the angle")]
        [Tooltip("Swivel search: turn the elbow plane about the shoulder-hand line and keep the swivel at which the forearm " +
                 "carries the hand's orientation best. Moves the arm only; the hand's end pose is unchanged.")]
        [SerializeField] private bool swivelSearch = true;
        [SerializeField] private int swivelSteps = 36;

        /// <summary>Hold-pose wrist deviation (both arms), written each hold frame: the measurement for the v3.11 report.</summary>
        public string HoldReport { get; private set; } = "";

        private void RecordHold()
        {
            HoldReport = $"R pitch {_right.DevPitch:0.0} yaw {_right.DevYaw:0.0} twist {_right.DevTwist:0.0} total {_right.DevAngle:0.0} | " +
                         $"L pitch {_left.DevPitch:0.0} yaw {_left.DevYaw:0.0} twist {_left.DevTwist:0.0} total {_left.DevAngle:0.0}";
        }

        /// <summary>Two-bone solve for one elbow pole, from the rest pose (the bones are reset first).</summary>
        private static void ApplyTwoBone(Arm arm, Vector3 a, Vector3 dir, float d, float cosA, float sinA, float lenA, Vector3 pole)
        {
            arm.Upper.localRotation = arm.UpperRest;
            arm.Lower.localRotation = arm.LowerRest;
            Vector3 b = arm.Lower.position;
            Vector3 elbow = a + dir * (lenA * cosA) + pole * (lenA * sinA);
            arm.Upper.rotation = Quaternion.FromToRotation(b - a, elbow - a) * arm.Upper.rotation;
            b = arm.Lower.position;
            Vector3 c = arm.Hand.position;
            arm.Lower.rotation = Quaternion.FromToRotation(c - b, (a + dir * d) - b) * arm.Lower.rotation;
        }

        /// <summary>
        /// Two-bone IK with the elbow pole chosen by the swivel search: the candidate poles are the height-based base pole
        /// turned about the shoulder-hand line; the one whose forearm best carries the hand's orientation wins, and the
        /// winner is damped over time so the elbow travels instead of jumping between candidates.
        /// </summary>
        private void SolveTwoBone(Arm arm, Vector3 target, Quaternion desired)
        {
            // called after the arm is reset to its rest pose: the lengths come from the rest bones
            Vector3 a = arm.Upper.position, b = arm.Lower.position, c = arm.Hand.position;
            float lenA = Vector3.Distance(a, b), lenB = Vector3.Distance(b, c);
            Vector3 toTarget = target - a;
            float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(lenA - lenB) + 1e-3f, lenA + lenB - 1e-3f);
            Vector3 dir = toTarget.normalized;
            float cosA = Mathf.Clamp((lenA * lenA + d * d - lenB * lenB) / (2f * lenA * d), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            Vector3 pole = ElbowPole(arm, target, dir);

            if (swivelSearch && swivelSteps > 1)
            {
                float bestErr = float.MaxValue;
                Vector3 best = pole;
                for (int k = 0; k < swivelSteps; k++)
                {
                    Vector3 cand = Quaternion.AngleAxis(k * 360f / swivelSteps, dir) * pole;
                    ApplyTwoBone(arm, a, dir, d, cosA, sinA, lenA, cand);
                    float err;
                    if (_fix && armFixObjective > 0)
                    {
                        err = WristWeight() * SwingAfterRoll(arm, desired) + armFixPolePrior * Vector3.Angle(cand, pole);
                    }
                    else
                    {
                        // v3.11 objective at the sign itself (these words measured no better with the new one)
                        err = Quaternion.Angle(arm.Lower.rotation * arm.HandRest, desired);
                        if (_travel && _blendW < 1f)
                        {
                            // v3.15: on the way between rest and the sign the elbow keeps to its pole and out of the body;
                            // the v3.11 objective takes over as the hand arrives, so the sign's own pose is unchanged
                            float ww = WristWeight();
                            err = ww * err + (1f - ww) * Vector3.Angle(cand, pole);
                        }
                    }
                    // v3.16: where the shoulder can put the elbow comes first, for every word and at every moment; the wrist
                    // bend is only compared between elbows a person could have. (Before, the lowest wrist bend won even
                    // with the elbow behind the head or inside the chest.)
                    err += elbowPenaltyPerMetre * ElbowViolation(arm);
                    if (err < bestErr) { bestErr = err; best = cand; }
                }
                // damp the winner, so a change of winner moves the elbow instead of snapping it
                if (arm.Chosen == Vector3.zero) arm.Chosen = best;
                else arm.Chosen = Vector3.Lerp(arm.Chosen, best, 1f - Mathf.Exp(-poleDamping * Time.deltaTime));
                Vector3 p = Vector3.ProjectOnPlane(arm.Chosen, dir);
                pole = p.sqrMagnitude > 1e-6f ? p.normalized : Vector3.ProjectOnPlane(best, dir).normalized;
            }
            ApplyTwoBone(arm, a, dir, d, cosA, sinA, lenA, pole);

            if (forearmRoll && rollSteps > 0)
            {
                // the forearm rolls about its own axis (pronation / supination): the one lever the swivel cannot move.
                // Search +-rollLimitDeg, keep the roll whose forearm best carries the hand's orientation, damp it.
                Vector3 axis = (arm.Hand.position - arm.Lower.position).normalized;
                Quaternion baseRot = arm.Lower.rotation;
                float bestErr = float.MaxValue, bestRoll = 0f;
                for (int k = 0; k <= rollSteps; k++)
                {
                    float r = -rollLimitDeg + k * (2f * rollLimitDeg / rollSteps);
                    arm.Lower.rotation = Quaternion.AngleAxis(r, axis) * baseRot;
                    float err = Quaternion.Angle(arm.Lower.rotation * arm.HandRest, desired);
                    if (err < bestErr) { bestErr = err; bestRoll = r; }
                }
                if (!arm.RollInit) { arm.Roll = bestRoll; arm.RollInit = true; }
                else arm.Roll = Mathf.Lerp(arm.Roll, bestRoll, 1f - Mathf.Exp(-poleDamping * Time.deltaTime));
                arm.Lower.rotation = Quaternion.AngleAxis(arm.Roll, axis) * baseRot;
            }
        }

        [Header("v3.16 elbow limits (teacher space, metres): where a shoulder can put the elbow")]
        [Tooltip("How far the chest front is ahead of the shoulder joints, plus half an arm.")]
        [SerializeField] private float torsoFrontFromShoulder = 0.14f;
        [Tooltip("The elbow stays at least this far out from the mid-line, on its own side. The shoulder joint is at about 0.21.")]
        [SerializeField] private float minElbowLateral = 0.11f;
        [Tooltip("The elbow rises at most this far above the shoulder joint (not beside or behind the head).")]
        [SerializeField] private float maxElbowAboveShoulder = 0.05f;
        [Tooltip("The elbow goes at most this far behind the shoulder joint.")]
        [SerializeField] private float maxElbowBehindShoulder = 0.12f;
        [Tooltip("Cost of breaking an elbow limit, in wrist degrees per metre. 3000 = 30 degrees per centimetre: in effect a hard limit, but graded, so the nearest allowed elbow wins when no candidate is inside.")]
        [SerializeField] private float elbowPenaltyPerMetre = 3000f;

        [Tooltip("v3.16: the wrist stays at least this far in front of the eyes plane when it is below the neck and between the shoulders (the chest surface; the 'chest' anchor is at 0.08).")]
        [SerializeField] private float chestFrontFromEyes = 0.08f;

        /// <summary>
        /// v3.16: a wrist target inside the torso is moved straight forward to the chest surface. Some signs stroke down from
        /// the throat along a line that is inside this avatar's chest; the hand then disappeared into the body.
        /// </summary>
        private Vector3 KeepOutOfTorso(Arm a, Vector3 pos)
        {
            float halfWidth = Mathf.Abs(a.Shoulder.x) - 0.03f;
            float front = _eyes.z + chestFrontFromEyes;
            if (pos.z >= front || Mathf.Abs(pos.x) >= halfWidth) return pos;
            // full push below the shoulder line, fading out over the 8 cm above it (the neck is further back)
            float k = Mathf.Clamp01((a.Shoulder.y + 0.04f - pos.y) / 0.08f);
            pos.z = Mathf.Lerp(pos.z, front, k);
            return pos;
        }

        /// <summary>
        /// v3.16: how far (metres) the elbow is outside what a shoulder allows: crossed in toward the mid-line, inside the
        /// torso, above the shoulder line, or behind the body. 0 = a place a person's elbow can be.
        /// </summary>
        private float ElbowViolation(Arm arm)
        {
            Vector3 e = Local(arm.Lower.position);
            float v = 0f;
            float lateral = e.x * arm.Side;
            if (lateral < minElbowLateral) v += minElbowLateral - lateral;
            float up = e.y - (arm.Shoulder.y + maxElbowAboveShoulder);
            if (up > 0f) v += up;
            float back = (arm.Shoulder.z - maxElbowBehindShoulder) - e.z;
            if (back > 0f) v += back;
            // torso box: between the shoulders, behind the chest front, below the shoulder line
            float halfWidth = Mathf.Abs(arm.Shoulder.x) - 0.03f;
            float front = arm.Shoulder.z + torsoFrontFromShoulder;
            if (Mathf.Abs(e.x) < halfWidth && e.z < front && e.y < arm.Shoulder.y + 0.05f)
                v += Mathf.Min(halfWidth - Mathf.Abs(e.x), front - e.z); // the shortest way out
            return v;
        }

        /// <summary>
        /// v3.14: what the wrist still has to bend once the forearm roll (up to rollLimitDeg) has taken the twist. The v3.11
        /// objective counted the twist against a swivel although the roll search removes it afterwards, so it picked elbows
        /// that traded twist for a sideways wrist bend.
        /// </summary>
        private float SwingAfterRoll(Arm arm, Quaternion desired)
        {
            Quaternion neutral = arm.Lower.rotation * arm.HandRest;
            Vector3 f = (arm.Hand.position - arm.Lower.position).normalized;
            Quaternion d = desired * Quaternion.Inverse(neutral);
            if (d.w < 0f) d = new Quaternion(-d.x, -d.y, -d.z, -d.w);
            float tw = 2f * Mathf.Atan2(Vector3.Dot(new Vector3(d.x, d.y, d.z), f), d.w) * Mathf.Rad2Deg;
            float lim = forearmRoll ? rollLimitDeg : 0f;
            Quaternion rolled = Quaternion.AngleAxis(Mathf.Clamp(tw, -lim, lim), f) * neutral;
            if (armFixObjective < 2) return Quaternion.Angle(rolled, desired);
            Quaternion inv = Quaternion.Inverse(arm.FrameToBone);
            Vector3 pn = rolled * inv * Vector3.up;
            Vector3 lat = Vector3.Cross(f, pn).normalized;
            Vector3 fd = desired * inv * Vector3.forward;
            float pitch = Mathf.Atan2(Vector3.Dot(fd, pn), Vector3.Dot(fd, f)) * Mathf.Rad2Deg;
            float yaw = Mathf.Atan2(Vector3.Dot(fd, lat), Vector3.Dot(fd, f)) * Mathf.Rad2Deg;
            float a = pitch / wristPitchMaxDeg, b = yaw / wristYawMaxDeg;
            // the residual twist beyond the roll limit still counts, as plain degrees over a pitch range
            float rest = Quaternion.Angle(rolled, desired) / wristPitchMaxDeg;
            return Mathf.Sqrt(a * a + b * b) + 0.25f * rest;
        }

        [Tooltip("v3.11: search the forearm's roll (pronation / supination) so the palm can turn without the wrist doing it.")]
        [SerializeField] private bool forearmRoll = true;
        [Tooltip("Largest forearm roll either way, degrees (human pronation / supination is about 90 each way).")]
        [SerializeField] private float rollLimitDeg = 90f;
        [SerializeField] private int rollSteps = 36;

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
