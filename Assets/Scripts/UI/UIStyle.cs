using System.Collections.Generic;
using UnityEngine;

namespace SignSight.UI
{
    /// <summary>
    /// One shared look for every floating panel and icon (v3.2 A2 / A3): dark rounded panel at 75 % opacity, white
    /// text, the same corner radius everywhere; icons drawn in code (the project's fonts have no ↻ ↺ ☰ ★ glyphs and no
    /// font may be added). Materials live in Resources/UI so they are included in builds.
    /// </summary>
    public static class UIStyle
    {
        public static readonly Color PanelColor = new Color(0.05f, 0.06f, 0.08f, 0.75f);
        public const float CornerRadius = 0.012f;   // metres, same on every panel
        public const float Padding = 0.012f;        // metres around text
        public const string Amber = "#FFD27F";      // non-manual (face / head) lines only

        private static Material _panel, _icon;
        private static readonly Dictionary<string, Texture2D> Icons = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Material> IconMats = new Dictionary<string, Material>();

        public static Material PanelMaterial
        {
            get
            {
                if (_panel == null) _panel = Resources.Load<Material>("UI/UIPanel");
                return _panel;
            }
        }

        /// <summary>Material showing one code-drawn icon (white, alpha from the drawing).</summary>
        public static Material IconMaterial(string icon)
        {
            if (IconMats.TryGetValue(icon, out var m) && m != null) return m;
            if (_icon == null) _icon = Resources.Load<Material>("UI/UIIcon");
            m = new Material(_icon) { name = "UIIcon_" + icon };
            m.SetTexture("_BaseMap", Icon(icon));
            m.renderQueue = 3001; // over the panels and disc labels
            IconMats[icon] = m;
            return m;
        }

        // ---------------- rounded rectangle mesh ----------------

        /// <summary>Rounded rectangle in the XY plane, centred, facing -Z (towards the viewer like TMP text).</summary>
        public static Mesh RoundedRect(float w, float h, float r, Mesh reuse = null)
        {
            r = Mathf.Min(r, Mathf.Min(w, h) * 0.5f);
            const int seg = 6;
            var verts = new List<Vector3> { Vector3.zero };
            float hw = w * 0.5f - r, hh = h * 0.5f - r;
            Vector2[] centres = { new Vector2(hw, hh), new Vector2(-hw, hh), new Vector2(-hw, -hh), new Vector2(hw, -hh) };
            for (int c = 0; c < 4; c++)
            {
                for (int s = 0; s <= seg; s++)
                {
                    float a = (c * 90f + s * 90f / seg) * Mathf.Deg2Rad;
                    verts.Add(new Vector3(centres[c].x + Mathf.Cos(a) * r, centres[c].y + Mathf.Sin(a) * r, 0f));
                }
            }
            var tris = new List<int>();
            int n = verts.Count - 1;
            for (int i = 1; i <= n; i++)
            {
                int j = i == n ? 1 : i + 1;
                tris.Add(0); tris.Add(j); tris.Add(i); // clockwise seen from -Z
            }
            var mesh = reuse != null ? reuse : new Mesh { name = "RoundedRect" };
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A child quad showing an icon, size in metres, facing the viewer.</summary>
        public static GameObject AddIcon(Transform parent, string icon, Vector3 localPos, float size, string name = "Icon")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(size, size, size);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = Quad();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = IconMaterial(icon);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        private static Mesh _quad;

        private static Mesh Quad()
        {
            if (_quad != null) return _quad;
            _quad = new Mesh { name = "IconQuad" };
            _quad.SetVertices(new List<Vector3> { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(0.5f, 0.5f), new Vector3(-0.5f, 0.5f) });
            _quad.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
            _quad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            _quad.RecalculateBounds();
            return _quad;
        }

        // ---------------- icons drawn in code ----------------

        public static Texture2D Icon(string name)
        {
            if (Icons.TryGetValue(name, out var t) && t != null) return t;
            const int S = 64;
            var px = new float[S * S];
            void Disk(float cx, float cy, float rr)
            {
                for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    px[y * S + x] = Mathf.Max(px[y * S + x], Mathf.Clamp01(rr - d + 0.5f));
                }
            }
            void Line(float x0, float y0, float x1, float y1, float w)
            {
                int steps = Mathf.CeilToInt(Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1)) * 2f);
                for (int i = 0; i <= steps; i++) Disk(Mathf.Lerp(x0, x1, i / (float)steps), Mathf.Lerp(y0, y1, i / (float)steps), w * 0.5f);
            }
            void Arc(float cx, float cy, float rr, float a0, float a1, float w)
            {
                for (float a = a0; a <= a1; a += 2f)
                    Disk(cx + Mathf.Cos(a * Mathf.Deg2Rad) * rr, cy + Mathf.Sin(a * Mathf.Deg2Rad) * rr, w * 0.5f);
            }
            void Tri(Vector2 a, Vector2 b, Vector2 c)
            {
                for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
                    bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                    if (!(neg && pos)) px[y * S + x] = 1f;
                }
            }
            switch (name)
            {
                case "replay": // ↻ clockwise circular arrow
                    Arc(32, 32, 19, 60, 330, 7);
                    Tri(new Vector2(43, 52), new Vector2(57, 40), new Vector2(38, 36));
                    break;
                case "tryagain": // ↺ anticlockwise circular arrow
                    Arc(32, 32, 19, 210, 480, 7);
                    Tri(new Vector2(21, 52), new Vector2(7, 40), new Vector2(26, 36));
                    break;
                case "next": // →
                    Line(10, 32, 46, 32, 8);
                    Tri(new Vector2(40, 48), new Vector2(58, 32), new Vector2(40, 16));
                    break;
                case "menu": // ☰
                    Line(12, 46, 52, 46, 7); Line(12, 32, 52, 32, 7); Line(12, 18, 52, 18, 7);
                    break;
                case "lesson": // open book
                    Line(10, 46, 30, 42, 5); Line(30, 42, 30, 14, 4); Line(10, 46, 10, 18, 5); Line(10, 18, 30, 14, 5);
                    Line(54, 46, 34, 42, 5); Line(34, 42, 34, 14, 4); Line(54, 46, 54, 18, 5); Line(54, 18, 34, 14, 5);
                    break;
                case "speed": // two chevrons
                    Line(14, 46, 28, 32, 6); Line(28, 32, 14, 18, 6); Line(34, 46, 48, 32, 6); Line(48, 32, 34, 18, 6);
                    break;
                case "face": // face outline, eyes, smile
                    Arc(32, 32, 24, 0, 360, 5);
                    Disk(24, 38, 3.5f); Disk(40, 38, 3.5f);
                    Arc(32, 30, 11, 205, 335, 4);
                    break;
                case "star": // ★
                {
                    var p = new Vector2[10];
                    for (int i = 0; i < 10; i++)
                    {
                        float a = (90f + i * 36f) * Mathf.Deg2Rad, rr = i % 2 == 0 ? 30f : 12f;
                        p[i] = new Vector2(32 + Mathf.Cos(a) * rr, 31 + Mathf.Sin(a) * rr);
                    }
                    for (int i = 0; i < 10; i++) Tri(new Vector2(32, 31), p[i], p[(i + 1) % 10]);
                    break;
                }
                case "eye": // Explore: an eye (outline + pupil)
                    Arc(32, 14, 26, 42, 138, 5);
                    Arc(32, 50, 26, 222, 318, 5);
                    Disk(32, 32, 9);
                    break;
                case "hand": // Finger Relax: an open hand (palm + four fingers + thumb)
                    // same height as the other icons (y 16..48) so it never reaches the label below
                    Disk(31, 25, 9);
                    Line(23, 29, 21, 45, 5); Line(28, 31, 28, 49, 5); Line(34, 31, 35, 48, 5); Line(39, 29, 41, 43, 5);
                    Line(39, 21, 48, 29, 5);
                    break;
                case "cup": // Café Final Exam: a cup with a handle and steam (y 14..50, like the other icons)
                    Line(16, 38, 18, 18, 5); Line(18, 18, 38, 18, 5); Line(38, 18, 40, 38, 5); Line(16, 38, 40, 38, 5);
                    Arc(44, 29, 6, -90, 90, 4);
                    Line(23, 43, 25, 50, 3); Line(31, 43, 33, 50, 3);
                    break;
                case "check": // ✓
                    Line(12, 32, 26, 18, 8); Line(26, 18, 52, 46, 8);
                    break;
                case "practice": // circle with a dot
                    Arc(32, 32, 20, 0, 360, 6); Disk(32, 32, 7);
                    break;
            }
            t = new Texture2D(S, S, TextureFormat.RGBA32, false) { name = "Icon_" + name, wrapMode = TextureWrapMode.Clamp };
            var cols = new Color32[S * S];
            for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(px[i])));
            t.SetPixels32(cols);
            t.Apply();
            Icons[name] = t;
            return t;
        }

        private static float Cross(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
    }
}
