using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SignSight.Explore
{
    /// <summary>
    /// Explore with the camera (v3.3 Task B, MOCK ONLY): the camera frame is shown on a flat panel in front of the
    /// learner with the YOLO boxes drawn on it (thin outline + label, one colour). Pointing at a box on the panel selects
    /// it. This 2D box -> panel -> pointing-ray mapping exists only for the mock: on a Quest the box must be turned into
    /// a 3D ray with the camera pose and intrinsics plus a depth raycast, which is not validated (no headset).
    /// </summary>
    public class ExploreCameraView : MonoBehaviour
    {
        public static readonly Color BoxColor = new Color(0.62f, 0.72f, 1f);
        public float Width = 0.48f;
        public int MaxBoxes = 12;

        public YoloDetector Detector;
        public ICameraFrameProvider Provider;
        public string HighlightLabel;

        private MeshRenderer _screen;
        private Material _mat;
        private readonly List<LineRenderer> _boxes = new List<LineRenderer>();
        private readonly List<TMP_Text> _labels = new List<TMP_Text>();
        private Texture _shown;

        public float Height => Provider?.Frame != null ? Width * Provider.Frame.height / Provider.Frame.width : Width * 9f / 16f;

        private void Awake()
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "CameraFrame";
            q.transform.SetParent(transform, false);
            _screen = q.GetComponent<MeshRenderer>();
            _mat = new Material(Resources.Load<Material>("UI/UIIcon")) { name = "ExploreCameraFrame" };
            _mat.SetColor("_BaseColor", Color.white);
            _mat.renderQueue = 2998;
            _screen.sharedMaterial = _mat;
            // the quad keeps its MeshCollider: the pointing ray hits it and the hit UV says where
        }

        private void LateUpdate()
        {
            var frame = Provider != null && Provider.IsAvailable ? Provider.Frame : null;
            _screen.enabled = frame != null;
            if (frame != null && frame != _shown)
            {
                _mat.SetTexture("_BaseMap", frame);
                _shown = frame;
            }
            _screen.transform.localScale = new Vector3(Width, Height, 1f);
            DrawBoxes();
        }

        /// <summary>Label of the detection under a ray hit on the panel (largest score if boxes overlap), or null.</summary>
        public string LabelAt(RaycastHit hit)
        {
            if (Detector == null || hit.collider == null || hit.collider.transform.parent != transform) return null;
            var uv = hit.textureCoord;
            var p = new Vector2(uv.x, 1f - uv.y); // detections are top-left origin
            string best = null;
            float bestScore = -1f;
            foreach (var d in Detector.Detections)
            {
                if (d.Box.Contains(p) && d.Score > bestScore)
                {
                    best = d.Label;
                    bestScore = d.Score;
                }
            }
            return best;
        }

        private Vector3 Local(float u, float vTop) => new Vector3((u - 0.5f) * Width, (0.5f - vTop) * Height, -0.002f);

        private void DrawBoxes()
        {
            int n = Detector != null && _screen.enabled ? Mathf.Min(MaxBoxes, Detector.Detections.Count) : 0;
            while (_boxes.Count < n) AddBox();
            for (int i = 0; i < _boxes.Count; i++)
            {
                bool on = i < n;
                _boxes[i].enabled = on;
                _labels[i].gameObject.SetActive(on);
                if (!on) continue;
                var d = Detector.Detections[i];
                var r = d.Box;
                _boxes[i].SetPositions(new[]
                {
                    Local(r.xMin, r.yMin), Local(r.xMax, r.yMin), Local(r.xMax, r.yMax), Local(r.xMin, r.yMax),
                });
                bool hi = d.Label == HighlightLabel;
                _boxes[i].startWidth = _boxes[i].endWidth = hi ? 0.005f : 0.0025f;
                var l = _labels[i];
                l.text = $"{d.Label} {d.Score:0.00}";
                l.transform.localPosition = Local(r.xMin, r.yMin) + new Vector3(0.002f, 0.004f, -0.001f);
            }
        }

        private void AddBox()
        {
            var lr = new GameObject("Box").AddComponent<LineRenderer>();
            lr.transform.SetParent(transform, false);
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 4;
            var m = new Material(Resources.Load<Material>("UI/UIIcon")) { name = "ExploreBox" };
            m.SetColor("_BaseColor", BoxColor);
            lr.sharedMaterial = m;
            _boxes.Add(lr);
            var t = new GameObject("BoxLabel", typeof(RectTransform)).AddComponent<TextMeshPro>();
            t.transform.SetParent(transform, false);
            t.fontSize = 0.18f;
            t.color = BoxColor;
            t.alignment = TextAlignmentOptions.BottomLeft;
            t.rectTransform.pivot = new Vector2(0f, 0f);
            t.rectTransform.sizeDelta = new Vector2(0.2f, 0.02f);
            t.enableWordWrapping = false;
            _labels.Add(t);
        }
    }
}
