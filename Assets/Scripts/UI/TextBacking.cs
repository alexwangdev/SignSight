using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SignSight.UI
{
    /// <summary>
    /// Dark rounded backing behind floating text (v3.2 A2): sized to the union of the given texts' rendered bounds plus
    /// padding, hidden when they are all empty, so no text floats bare over a bright room. One style (UIStyle).
    /// Put it on any object; the backing is drawn in that object's local space, just behind the texts.
    /// </summary>
    public class TextBacking : MonoBehaviour
    {
        public List<TMP_Text> Texts = new List<TMP_Text>();
        public float Padding = UIStyle.Padding;
        /// <summary>Optional minimum size (metres), e.g. for a titled panel.</summary>
        public Vector2 MinSize = Vector2.zero;
        public float Depth = 0.004f; // behind the text (TMP faces -Z)

        private MeshRenderer _renderer;
        private Mesh _mesh;
        private Bounds _last;
        private bool _visible;

        public static TextBacking For(GameObject host, params TMP_Text[] texts)
        {
            var b = host.GetComponent<TextBacking>();
            if (b == null) b = host.AddComponent<TextBacking>();
            foreach (var t in texts)
                if (t != null && !b.Texts.Contains(t)) b.Texts.Add(t);
            return b;
        }

        private void EnsureRenderer()
        {
            if (_renderer != null) return;
            var go = new GameObject("Backing");
            go.transform.SetParent(transform, false);
            _mesh = new Mesh { name = "BackingMesh" };
            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = go.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = UIStyle.PanelMaterial;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }

        private void LateUpdate()
        {
            EnsureRenderer();
            bool any = false;
            var b = new Bounds();
            foreach (var t in Texts)
            {
                if (t == null || !t.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(t.text)) continue;
                t.ForceMeshUpdate();
                var tb = t.textBounds;
                if (tb.size.x <= 1e-5f) continue;
                // text bounds are in the text's local space: bring both corners into ours
                var a = transform.InverseTransformPoint(t.transform.TransformPoint(tb.min));
                var c = transform.InverseTransformPoint(t.transform.TransformPoint(tb.max));
                var lb = new Bounds((a + c) * 0.5f, new Vector3(Mathf.Abs(c.x - a.x), Mathf.Abs(c.y - a.y), 0f));
                if (!any) b = lb; else b.Encapsulate(lb);
                any = true;
            }
            if (any != _visible)
            {
                _visible = any;
                _renderer.enabled = any;
            }
            if (!any) return;
            var size = new Vector2(Mathf.Max(MinSize.x, b.size.x + 2f * Padding), Mathf.Max(MinSize.y, b.size.y + 2f * Padding));
            var nb = new Bounds(b.center, size);
            if (nb == _last) return;
            _last = nb;
            UIStyle.RoundedRect(size.x, size.y, UIStyle.CornerRadius, _mesh);
            _renderer.transform.localPosition = new Vector3(b.center.x, b.center.y, Depth);
        }
    }
}
