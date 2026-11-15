using TMPro;
using UnityEngine;

namespace SignSight.UI
{
    /// <summary>
    /// Icon glyph above a pinch button's label (v3.2 A3): ↻ replay, → next, ↺ try again, ☰ menu, ½× speed, face, book.
    /// Drawn in code (UIStyle.Icon) because the project fonts have no such glyphs. The label moves down to make room.
    /// </summary>
    public class ButtonIcon : MonoBehaviour
    {
        public string Icon = "next";
        public float Size = 0.034f;
        public float IconY = 0.024f;
        public float LabelY = -0.016f;

        private MeshRenderer _renderer;
        private string _shown;

        private void Awake() => Build();

        private void Build()
        {
            if (_renderer != null) return;
            var existing = transform.Find("Icon");
            if (existing != null) // a runtime copy of a button that already has one
            {
                _renderer = existing.GetComponent<MeshRenderer>();
                _shown = null;
                Set(Icon);
                return;
            }
            var label = GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                var p = label.transform.localPosition;
                label.transform.localPosition = new Vector3(p.x, LabelY, p.z);
            }
            float z = label != null ? label.transform.localPosition.z - 0.002f : -0.012f;
            _renderer = UIStyle.AddIcon(transform, Icon, new Vector3(0f, IconY, z), Size, "Icon").GetComponent<MeshRenderer>();
            _shown = Icon;
        }

        public void Set(string icon)
        {
            Build();
            if (_shown == icon) return;
            _shown = icon;
            _renderer.sharedMaterial = UIStyle.IconMaterial(icon);
        }
    }
}
