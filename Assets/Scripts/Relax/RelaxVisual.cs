using System.Collections.Generic;
using SignSight.Learn;
using UnityEngine;

namespace SignSight.Relax
{
    /// <summary>
    /// v3.7: one living object per Finger Relax step, beside (above) the ghost hand. Visuals only: it reads values
    /// RelaxMode already measures (mean finger curl, spread, the piano finger and fingertip gaps, the shake reversal
    /// count, Progress) and adds no detection. Each visual is ONE procedural mesh (vertex colours, the existing Confetti
    /// particle material): flower, sun, piano dots, leaves, bubbles = 5 renderers in all, at most 2 visible at once.
    /// The bubbles are the ambient background on every step (tinted per step) and come to the front as the Wiggle
    /// visual. Silent, no score.
    /// </summary>
    public class RelaxVisual : MonoBehaviour
    {
        /// <summary>Centre of the step visual (LearnUI local): left of the ghost hand, clear of MENU, ring and word card.</summary>
        public static readonly Vector3 VisualPosition = new Vector3(-0.80f, 0.12f, 0.72f);
        public const float VisualScale = 1.4f;
        public const int BubbleCount = 4;
        public const int MaxLeaves = 8;
        public const float LeafSeconds = 1.2f;

        // per-step ambient tint: OpenClose soft green, Spread warm amber, Piano soft blue, Shake pale green, Wiggle pale blue
        public static readonly Color[] StepTint =
        {
            new Color(0.62f, 0.92f, 0.62f), new Color(1f, 0.82f, 0.45f), new Color(0.6f, 0.75f, 1f),
            new Color(0.75f, 1f, 0.72f), new Color(0.72f, 0.88f, 1f),
        };
        private static readonly Color Petal = new Color(1f, 0.72f, 0.8f, 0.95f);
        private static readonly Color Gold = new Color(1f, 0.82f, 0.3f, 1f);
        private static readonly Color Amber = new Color(1f, 0.82f, 0.5f, 1f);
        private static readonly Color Green = new Color(0.45f, 0.9f, 0.5f, 1f);
        private static readonly Color Idle = new Color(0.8f, 0.82f, 0.88f, 0.55f);

        private RelaxMode _relax;
        private LessonManager _lessons;
        private Part _flower, _sun, _piano, _leaves, _bubbles;
        private float _bloom, _rays;
        private int _lastStep = -1, _lastReversals;
        private readonly List<(Vector3 p, Vector3 v, float t)> _leafList = new List<(Vector3, Vector3, float)>();

        public string VisibleVisual { get; private set; } = "";

        private class Part
        {
            public MeshRenderer R;
            public Mesh M;
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Color> C = new List<Color>();
            public readonly List<int> T = new List<int>();

            public void Begin() { V.Clear(); C.Clear(); T.Clear(); }

            /// <summary>An ellipse in the XY plane (facing the learner), rotated by 'deg'.</summary>
            public void Ellipse(Vector3 c, float rx, float ry, float deg, Color col, int seg = 12)
            {
                int start = V.Count;
                V.Add(c); C.Add(col);
                var q = Quaternion.Euler(0f, 0f, deg);
                for (int i = 0; i <= seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    V.Add(c + q * new Vector3(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry, 0f));
                    C.Add(new Color(col.r, col.g, col.b, col.a * 0.85f));
                }
                for (int i = 1; i <= seg; i++) { T.Add(start); T.Add(start + i); T.Add(start + i + 1); }
            }

            /// <summary>A thin triangle from 'from' pointing along 'deg' for 'len'.</summary>
            public void Ray(Vector3 from, float deg, float len, float width, Color col)
            {
                int s = V.Count;
                var d = new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad), 0f);
                var n = new Vector3(-d.y, d.x, 0f) * width * 0.5f;
                V.Add(from + n); V.Add(from - n); V.Add(from + d * len);
                C.Add(col); C.Add(col); C.Add(new Color(col.r, col.g, col.b, col.a * 0.4f));
                T.Add(s); T.Add(s + 1); T.Add(s + 2);
            }

            public void End(bool show)
            {
                M.Clear();
                M.SetVertices(V);
                M.SetColors(C);
                M.SetTriangles(T, 0);
                M.RecalculateBounds();
                R.enabled = show && V.Count > 0;
            }
        }

        public static RelaxVisual Create(Transform parent, RelaxMode relax)
        {
            var go = new GameObject("RelaxVisual");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = VisualPosition;
            go.transform.localScale = Vector3.one * VisualScale;
            var v = go.AddComponent<RelaxVisual>();
            v._relax = relax;
            v.Build();
            return v;
        }

        private Part NewPart(string name, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var p = new Part { M = new Mesh { name = name } };
            p.M.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = p.M;
            p.R = go.AddComponent<MeshRenderer>();
            p.R.sharedMaterial = mat;
            p.R.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            p.R.receiveShadows = false;
            p.R.enabled = false;
            return p;
        }

        private void Build()
        {
            _lessons = FindFirstObjectByType<LessonManager>();
            var mat = new Material(Resources.Load<Material>("UI/Confetti")) { name = "RelaxVisual" };
            mat.renderQueue = 2999;
            mat.SetFloat("_Cull", 0f); // flat shapes, seen from the front whatever their winding
            _bubbles = NewPart("Bubbles", mat);
            _bubbles.R.transform.localPosition = new Vector3(0f, 0f, 0.04f); // behind the step visual
            _flower = NewPart("Flower", mat);
            _sun = NewPart("Sun", mat);
            _piano = NewPart("PianoDots", mat);
            _leaves = NewPart("Leaves", mat);
        }

        private void LateUpdate()
        {
            bool on = _relax != null && _lessons != null && _lessons.CurrentMode == LessonManager.Mode.Relax &&
                      (_relax.Current == RelaxMode.State.Step || _relax.Current == RelaxMode.State.Between);
            var step = on ? _relax.CurrentStep : null;
            if (step == null)
            {
                foreach (var p in new[] { _flower, _sun, _piano, _leaves, _bubbles }) p.R.enabled = false;
                VisibleVisual = "";
                _lastStep = -1;
                return;
            }
            if (_relax.StepIndex != _lastStep)
            {
                // a new step: only its own visual is shown
                _lastStep = _relax.StepIndex;
                _leafList.Clear();
                _lastReversals = 0;
                _bloom = 0f;
                _rays = 0f;
            }
            var s = _relax.LastSample;
            float dt = Time.deltaTime, t = Time.time;
            var kind = step.kind;

            // ---- flower: petals follow the mean finger curl (open hand = bloomed, fist = closed) ----
            _flower.Begin();
            if (kind == RelaxMode.Kind.OpenClose)
            {
                if (s.Tracked && s.Curl != null) _bloom = Mathf.MoveTowards(_bloom, 1f - RelaxMode.MeanFingerCurl(s.Curl), dt * 3f);
                float b = Mathf.Clamp01(_bloom);
                _flower.Ray(new Vector3(0f, -0.012f, 0f), -90f, 0.05f, 0.006f, new Color(0.4f, 0.75f, 0.4f, 0.9f)); // stem
                for (int i = 0; i < 5; i++)
                {
                    float ang = Mathf.Lerp(90f + (i - 2) * 9f, 90f + i * 72f, b);
                    float len = Mathf.Lerp(0.016f, 0.032f, b), wid = Mathf.Lerp(0.007f, 0.013f, b);
                    var dir = new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad), 0f);
                    _flower.Ellipse(dir * (0.006f + len * 0.5f), wid, len * 0.5f, ang - 90f, Petal);
                }
                _flower.Ellipse(Vector3.zero, 0.009f, 0.009f, 0f, new Color(1f, 0.85f, 0.35f, 1f));
            }
            _flower.End(kind == RelaxMode.Kind.OpenClose);

            // ---- sun: ray length follows the spread (or the step's progress) ----
            _sun.Begin();
            if (kind == RelaxMode.Kind.Spread)
            {
                float target = Mathf.Clamp01(Mathf.Max(s.Tracked ? s.Spread : 0f, _relax.Progress));
                _rays = Mathf.MoveTowards(_rays, target, dt * 2f);
                float len = Mathf.Lerp(0.006f, 0.034f, _rays);
                for (int i = 0; i < 10; i++) _sun.Ray(Vector3.zero, i * 36f + t * 10f, 0.016f + len, 0.009f, Gold);
                _sun.Ellipse(Vector3.zero, 0.016f, 0.016f, 0f, new Color(1f, 0.9f, 0.45f, 1f), 16);
            }
            _sun.End(kind == RelaxMode.Kind.Spread);

            // ---- piano: 4 dots (index, middle, ring, pinky); target amber, touched / done green ----
            _piano.Begin();
            if (kind == RelaxMode.Kind.FingerPiano)
            {
                int phase = _relax.PianoFinger;
                for (int f = 0; f < 4; f++)
                {
                    bool done = f < phase || (f == phase && s.Tracked && s.TipGap != null && s.TipGap[f] < RelaxMode.PianoTouch);
                    bool target = f == phase && !done;
                    float r = target ? 0.009f + 0.002f * Mathf.Sin(t * 6f) : 0.008f;
                    var c = new Vector3((f - 1.5f) * 0.026f, 0f, 0f);
                    if (target) _piano.Ellipse(c, r * 1.8f, r * 1.8f, 0f, new Color(Amber.r, Amber.g, Amber.b, 0.25f)); // glow
                    _piano.Ellipse(c, r, r, 0f, done ? Green : target ? Amber : Idle);
                }
            }
            _piano.End(kind == RelaxMode.Kind.FingerPiano);

            // ---- shake: a few leaves drift off and fade each time a reversal is counted ----
            _leaves.Begin();
            if (kind == RelaxMode.Kind.Shake)
            {
                int rev = _relax.ShakeCount;
                if (rev > _lastReversals && _leafList.Count < MaxLeaves)
                {
                    var side = (_leafList.Count % 2 == 0) ? 1f : -1f;
                    _leafList.Add((new Vector3(side * 0.01f, 0f, 0f), new Vector3(side * Random.Range(0.02f, 0.05f), Random.Range(0.01f, 0.04f), 0f), 0f));
                }
                _lastReversals = rev;
                for (int i = _leafList.Count - 1; i >= 0; i--)
                {
                    var (p, v, age) = _leafList[i];
                    age += dt;
                    if (age > LeafSeconds) { _leafList.RemoveAt(i); continue; }
                    p += v * dt;
                    _leafList[i] = (p, v, age);
                    float a = 1f - age / LeafSeconds;
                    _leaves.Ellipse(p, 0.006f, 0.011f, age * 180f + i * 40f, new Color(0.55f, 0.92f, 0.5f, a));
                }
                // a resting leaf so the step always shows its object
                _leaves.Ellipse(Vector3.zero, 0.008f, 0.015f, 20f * Mathf.Sin(t * 3f), new Color(0.5f, 0.88f, 0.45f, 0.9f));
            }
            _leaves.End(kind == RelaxMode.Kind.Shake);

            // ---- bubbles: ambient on every step (tinted), the Wiggle visual itself (front, brighter) ----
            _bubbles.Begin();
            bool wiggle = kind == RelaxMode.Kind.Wiggle;
            var tint = StepTint[Mathf.Clamp(_relax.StepIndex, 0, StepTint.Length - 1)];
            for (int i = 0; i < BubbleCount; i++)
            {
                float speed = wiggle ? 0.035f : 0.02f;
                float u = Mathf.Repeat(t * speed / 0.16f + i / (float)BubbleCount, 1f); // 0..1 up the column, looping
                float x = (i - (BubbleCount - 1) * 0.5f) * 0.035f + 0.008f * Mathf.Sin(t * 1.3f + i);
                float y = -0.06f + u * 0.16f;
                float alpha = Mathf.Sin(u * Mathf.PI) * (wiggle ? 0.75f : 0.35f);
                float r = wiggle ? 0.009f : 0.006f;
                _bubbles.Ellipse(new Vector3(x, y, 0f), r, r, 0f, new Color(tint.r, tint.g, tint.b, alpha), 14);
            }
            _bubbles.End(true);
            VisibleVisual = kind.ToString();
        }
    }
}
