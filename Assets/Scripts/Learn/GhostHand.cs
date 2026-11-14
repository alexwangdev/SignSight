using UnityEngine;

namespace SignSight.Learn
{
    /// <summary>
    /// A simple hand built from capsules (palm + 3 segments per finger), posed from curl[5] (SPEC v3 §8.1).
    /// Used in the feedback panel for the semi-transparent target hand(s) and the learner's hand(s), same size and
    /// orientation, so they can be compared side by side. Each finger can be coloured on its own (§8.2).
    /// A left hand is the right hand mirrored (one implementation, SPEC v3 §8.1). It can replay a sign's movement
    /// through SetMotion (offsets in the signer's space, which is this transform's local space).
    /// Local space: fingers point +Y, the palm faces +Z (toward the viewer), thumb on -X.
    /// </summary>
    public class GhostHand : MonoBehaviour
    {
        [SerializeField] private Material material;
        [SerializeField] private Color baseColor = new Color(0.85f, 0.9f, 1f, 0.45f);
        [Tooltip("Overall size (1 = average adult hand).")]
        [SerializeField] private float scale = 1.6f;

        // Same per-joint bend at curl = 1 as the teacher (proximal, intermediate, distal).
        private static readonly Vector3 FingerMax = new Vector3(80f, 100f, 70f);
        private static readonly Vector3 ThumbMax = new Vector3(45f, 45f, 55f);

        private static readonly float[][] Lengths =
        {
            new[] { 0.040f, 0.032f, 0.026f }, // thumb
            new[] { 0.045f, 0.027f, 0.022f }, // index
            new[] { 0.050f, 0.030f, 0.024f }, // middle
            new[] { 0.046f, 0.028f, 0.022f }, // ring
            new[] { 0.036f, 0.022f, 0.020f }, // pinky
        };
        private static readonly float[] BaseX = { -0.040f, -0.027f, -0.009f, 0.009f, 0.026f };
        private const float Radius = 0.0085f;

        private Transform _motionRoot, _handRoot;
        private bool _left;
        private Transform[][] _joints;
        private Renderer[][] _fingerRenderers;
        private Renderer _palmRenderer;
        private MaterialPropertyBlock _block;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private bool _built;

        private void Awake() => Build();

        public void Build()
        {
            if (_built) return;
            _built = true;
            _block = new MaterialPropertyBlock();
            // MotionRoot carries the movement (unmirrored); HandRoot carries the size and the left/right mirror.
            _motionRoot = new GameObject("MotionRoot").transform;
            _motionRoot.SetParent(transform, false);
            var root = new GameObject("HandRoot").transform;
            root.SetParent(_motionRoot, false);
            _handRoot = root;
            ApplyScale();

            var palm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            palm.name = "Palm";
            Destroy(palm.GetComponent<Collider>());
            palm.transform.SetParent(root, false);
            palm.transform.localPosition = new Vector3(-0.005f, -0.045f, 0f);
            palm.transform.localScale = new Vector3(0.078f, 0.09f, 0.022f);
            _palmRenderer = Setup(palm);

            _joints = new Transform[5][];
            _fingerRenderers = new Renderer[5][];
            for (int f = 0; f < 5; f++)
            {
                _joints[f] = new Transform[3];
                _fingerRenderers[f] = new Renderer[3];
                Transform parent = root;
                for (int j = 0; j < 3; j++)
                {
                    var joint = new GameObject($"F{f}J{j}").transform;
                    joint.SetParent(parent, false);
                    if (j == 0)
                    {
                        joint.localPosition = f == 0 ? new Vector3(BaseX[0], -0.065f, 0.006f) : new Vector3(BaseX[f], 0f, 0f);
                        // Thumb points up and out at rest.
                        joint.localRotation = f == 0 ? Quaternion.Euler(0f, 0f, 40f) : Quaternion.identity;
                    }
                    else
                    {
                        joint.localPosition = new Vector3(0f, Lengths[f][j - 1], 0f);
                    }
                    var seg = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    seg.name = "Segment";
                    Destroy(seg.GetComponent<Collider>());
                    seg.transform.SetParent(joint, false);
                    float len = Lengths[f][j];
                    seg.transform.localPosition = new Vector3(0f, len * 0.5f, 0f);
                    seg.transform.localScale = new Vector3(Radius * 2f, (len + Radius * 2f) * 0.5f, Radius * 2f);
                    _fingerRenderers[f][j] = Setup(seg);
                    _joints[f][j] = joint;
                    parent = joint;
                }
            }
            CombineIntoSkinnedMesh();
            SetAllColors(baseColor);
        }

        // ---- One skinned mesh per hand (SPEC v3 §9: draw calls < 100) ----
        // The 16 primitive parts are merged into one mesh; each part is bound 100 % to its joint, so the fingers
        // still bend, and per-finger colour lives in vertex colours. 1 draw call per hand instead of 16.

        private SkinnedMeshRenderer _skin;
        private Mesh _mesh;
        private Color[] _colors;
        private (int start, int count) _palmRange;
        private (int start, int count)[][] _segRanges;
        private bool _windingFlipped;

        private void CombineIntoSkinnedMesh()
        {
            var go = new GameObject("GhostMesh");
            go.transform.SetParent(transform, false);
            var smrT = go.transform;

            var bones = new System.Collections.Generic.List<Transform>();
            var verts = new System.Collections.Generic.List<Vector3>();
            var normals = new System.Collections.Generic.List<Vector3>();
            var weights = new System.Collections.Generic.List<BoneWeight>();
            var tris = new System.Collections.Generic.List<int>();

            (int, int) Add(Renderer r, Transform bone)
            {
                int bi = bones.IndexOf(bone);
                if (bi < 0) { bi = bones.Count; bones.Add(bone); }
                var src = r.GetComponent<MeshFilter>().sharedMesh;
                var m = smrT.worldToLocalMatrix * r.transform.localToWorldMatrix;
                int start = verts.Count;
                var sv = src.vertices; var sn = src.normals; var st = src.triangles;
                for (int i = 0; i < sv.Length; i++)
                {
                    verts.Add(m.MultiplyPoint3x4(sv[i]));
                    normals.Add(m.MultiplyVector(sn[i]).normalized);
                    weights.Add(new BoneWeight { boneIndex0 = bi, weight0 = 1f });
                }
                foreach (int t in st) tris.Add(start + t);
                Destroy(r.gameObject);
                return (start, sv.Length);
            }

            _palmRange = Add(_palmRenderer, _handRoot);
            _segRanges = new (int, int)[5][];
            for (int f = 0; f < 5; f++)
            {
                _segRanges[f] = new (int, int)[3];
                for (int j = 0; j < 3; j++) _segRanges[f][j] = Add(_fingerRenderers[f][j], _joints[f][j]);
            }

            var bind = new Matrix4x4[bones.Count];
            for (int i = 0; i < bones.Count; i++) bind[i] = bones[i].worldToLocalMatrix * smrT.localToWorldMatrix;

            _mesh = new Mesh { name = "GhostHandMesh" };
            _mesh.SetVertices(verts);
            _mesh.SetNormals(normals);
            _mesh.SetTriangles(tris, 0);
            _mesh.boneWeights = weights.ToArray();
            _mesh.bindposes = bind;
            _colors = new Color[verts.Count];
            _mesh.colors = _colors;
            _mesh.RecalculateBounds();

            _skin = go.AddComponent<SkinnedMeshRenderer>();
            _skin.sharedMesh = _mesh;
            _skin.bones = bones.ToArray();
            _skin.rootBone = _handRoot;
            _skin.updateWhenOffscreen = true;
            _skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _skin.receiveShadows = false;
            if (material != null) _skin.sharedMaterial = material;
            _palmRenderer = null;
            _fingerRenderers = null;
            ApplyWinding();
        }

        /// <summary>A mirrored (left) skinned hand needs its triangle winding flipped to keep back-face culling right.</summary>
        private void ApplyWinding()
        {
            if (_mesh == null || _windingFlipped == _left) return;
            var t = _mesh.triangles;
            for (int i = 0; i < t.Length; i += 3) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
            _mesh.triangles = t;
            _windingFlipped = _left;
        }

        private void PaintRange((int start, int count) r, Color c)
        {
            for (int i = r.start; i < r.start + r.count; i++) _colors[i] = c;
        }

        private Renderer Setup(GameObject go)
        {
            var r = go.GetComponent<Renderer>();
            if (material != null) r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        }

        /// <summary>Left hand = the right hand mirrored across its local X.</summary>
        public bool IsLeft
        {
            get => _left;
            set
            {
                _left = value;
                if (_built)
                {
                    ApplyScale();
                    ApplyWinding();
                }
            }
        }

        /// <summary>Extra size factor (e.g. smaller when two hands share the panel).</summary>
        public float SizeMultiplier
        {
            get => _size;
            set
            {
                _size = value;
                if (_built) ApplyScale();
            }
        }
        private float _size = 1f;

        /// <summary>How much bigger than a real hand this one is drawn; movement offsets are scaled by the same factor.</summary>
        public float DrawScale => scale * _size;

        private void ApplyScale()
        {
            float s = DrawScale;
            _handRoot.localScale = new Vector3(_left ? -s : s, s, s);
        }

        /// <summary>Moves the hand from its start pose (metres / rotation in the signer's space = local space).</summary>
        public void SetMotion(Vector3 offset, Quaternion rotation)
        {
            if (!_built) Build();
            _motionRoot.localPosition = offset;
            _motionRoot.localRotation = rotation;
        }

        public void ClearMotion() => SetMotion(Vector3.zero, Quaternion.identity);

        /// <summary>Poses the hand. curl: 0 = straight, 1 = fully curled.</summary>
        public void Pose(float[] curl)
        {
            if (!_built) Build();
            for (int f = 0; f < 5; f++)
            {
                float c = Mathf.Clamp01(curl[f]);
                Vector3 max = f == 0 ? ThumbMax : FingerMax;
                for (int j = 0; j < 3; j++)
                {
                    float deg = c * (j == 0 ? max.x : j == 1 ? max.y : max.z);
                    // Rotating the bone (+Y) about (Y x d) bends it toward d. Fingers bend toward the palm side (+Z);
                    // the thumb bends toward the palm side and across the palm (+X + Z).
                    Quaternion bend = f == 0
                        ? Quaternion.AngleAxis(deg, new Vector3(1f, 0f, -1f).normalized)
                        : Quaternion.AngleAxis(deg, Vector3.right);
                    Quaternion rest = (f == 0 && j == 0) ? Quaternion.Euler(0f, 0f, 40f) : Quaternion.identity;
                    _joints[f][j].localRotation = rest * bend;
                }
            }
        }

        public void SetAllColors(Color c)
        {
            if (!_built) Build();
            PaintRange(_palmRange, c);
            for (int f = 0; f < 5; f++) foreach (var r in _segRanges[f]) PaintRange(r, c);
            _mesh.colors = _colors;
        }

        public void SetFingerColor(int finger, Color c)
        {
            if (!_built) Build();
            foreach (var r in _segRanges[finger]) PaintRange(r, c);
            _mesh.colors = _colors;
        }
    }
}
