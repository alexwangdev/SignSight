using UnityEngine;

namespace SignSight.Learn
{
    /// <summary>
    /// A simple right hand built from capsules (palm + 3 segments per finger), posed from curl[5] (SPEC §8.1).
    /// Used twice in the feedback panel: the semi-transparent target hand and the learner's hand, same size and
    /// orientation, so they can be compared side by side. Each finger can be coloured on its own (§8.2).
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
            var root = new GameObject("HandRoot").transform;
            root.SetParent(transform, false);
            root.localScale = Vector3.one * scale;

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
            SetAllColors(baseColor);
        }

        private Renderer Setup(GameObject go)
        {
            var r = go.GetComponent<Renderer>();
            if (material != null) r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        }

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
            Paint(_palmRenderer, c);
            for (int f = 0; f < 5; f++) SetFingerColor(f, c);
        }

        public void SetFingerColor(int finger, Color c)
        {
            foreach (var r in _fingerRenderers[finger]) Paint(r, c);
        }

        private void Paint(Renderer r, Color c)
        {
            r.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, c);
            r.SetPropertyBlock(_block);
        }
    }
}
