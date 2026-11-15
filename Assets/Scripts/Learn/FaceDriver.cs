using System.Collections.Generic;
using System.Text;
using SignSight.Hand;
using UnityEngine;

namespace SignSight.Learn
{
    /// <summary>
    /// Teacher face (SPEC v3 §3.4, v3.2 B1): sets ARKit blendshapes on the Rocketbox face by name suffix (never by
    /// index), fades every expression in / out over fadeSeconds, and blinks while idle. Display only — nothing here reads
    /// or scores the learner's face.
    /// </summary>
    public class FaceDriver : MonoBehaviour
    {
        public const string SmileLeft = "MouthSmileLeft";
        public const string SmileRight = "MouthSmileRight";
        public const string JawOpen = "JawOpen";
        public const string BlinkLeft = "EyeBlinkLeft";
        public const string BlinkRight = "EyeBlinkRight";

        [SerializeField] private SkinnedMeshRenderer face;
        [SerializeField] private float fadeSeconds = 0.3f;
        [Tooltip("Display gain on every expression weight (clamped to 100). The Rocketbox ARKit shapes are subtle: at the " +
                 "data's values a smile is hard to see at demo distance. One gain keeps the presets' proportions.")]
        [SerializeField] private float intensity = 1.8f;

        public float Intensity
        {
            get => intensity;
            set => intensity = Mathf.Max(0f, value);
        }

        [Header("Idle blink")]
        [SerializeField] private bool idleBlink = true;
        [SerializeField] private Vector2 blinkIntervalSeconds = new Vector2(4f, 6f);
        [SerializeField] private float blinkSeconds = 0.2f;

        private readonly Dictionary<string, int> _index = new Dictionary<string, int>();
        private readonly Dictionary<int, float> _target = new Dictionary<int, float>();
        private readonly Dictionary<int, float> _current = new Dictionary<int, float>();
        private float _nextBlink, _blinkStart = -1f;
        private int _blinkL = -1, _blinkR = -1;

        public SkinnedMeshRenderer Face => face;

        // ---------------- presets (code, not JSON; v3.2 B1) ----------------
        // Only presets that a word uses are kept. Removed as unused: Raised (no yes/no-question word in the 50),
        // Pucker (Lifeprint's HOT does not mention blowing).

        public static SignFace Preset(string name)
        {
            switch (name)
            {
                case "Smile": return new SignFace { smile = 60, cheekSquint = 30, name = "smile" };
                case "Sad": return new SignFace { browInnerUp = 60, mouthFrown = 50, name = "sad" };
                case "Furrowed": return new SignFace { browInnerUp = 70, browDown = 40, eyeSquint = 20, name = "eyebrows furrowed — WH-question" };
                case "Tired": return new SignFace { eyeSquint = 50, browDown = 30, name = "tired" };
                case "Pain": return new SignFace { browDown = 60, mouthPress = 50, eyeSquint = 40, name = "pain" };
                default: return null;
            }
        }

        /// <summary>The values to show: the named preset if there is one, else the fields.</summary>
        public static SignFace Resolve(SignFace f)
        {
            if (f == null) return new SignFace();
            if (string.IsNullOrEmpty(f.preset)) return f;
            var p = Preset(f.preset);
            if (p == null) return f;
            if (!string.IsNullOrEmpty(f.name)) p.name = f.name;
            return p;
        }

        /// <summary>
        /// Plain words for what the face does (v3.2 B4), naming brows / eyes / mouth, e.g.
        /// "eyebrows furrowed — WH-question: inner brows up and drawn down, eyes narrowed". Empty for a neutral face.
        /// </summary>
        public static string Describe(SignFace raw)
        {
            if (raw == null || raw.IsNeutral) return "";
            var f = Resolve(raw);
            var parts = new List<string>();
            if (f.browInnerUp >= 15f && f.browDown >= 15f) parts.Add("brows pulled together");
            else if (f.browInnerUp >= 15f) parts.Add("inner brows up");
            else if (f.browDown >= 15f) parts.Add("brows down");
            if (f.browOuterUp >= 15f) parts.Add("brows raised");
            if (f.eyeSquint >= 15f) parts.Add("eyes narrowed");
            if (f.eyeWide >= 15f) parts.Add("eyes wide");
            if (f.smile >= 15f) parts.Add("mouth corners up");
            if (f.mouthFrown >= 15f) parts.Add("mouth corners down");
            if (f.mouthPress >= 15f) parts.Add("lips pressed");
            if (f.mouthPucker >= 15f) parts.Add("lips pushed out");
            if (f.jawOpen >= 15f) parts.Add("mouth a little open");
            if (f.noseSneer >= 15f) parts.Add("nose wrinkled");
            var sb = new StringBuilder();
            sb.Append(string.IsNullOrEmpty(f.name) ? "expression" : f.name);
            if (parts.Count > 0) sb.Append(" — ").Append(string.Join(", ", parts));
            return sb.ToString();
        }

        // field -> ARKit blendshape names (left / right set together)
        private static readonly (string field, string[] shapes)[] Map =
        {
            ("smile", new[] { "MouthSmileLeft", "MouthSmileRight" }),
            ("jawOpen", new[] { "JawOpen" }),
            ("browInnerUp", new[] { "BrowInnerUp" }),
            ("browDown", new[] { "BrowDownLeft", "BrowDownRight" }),
            ("browOuterUp", new[] { "BrowOuterUpLeft", "BrowOuterUpRight" }),
            ("eyeSquint", new[] { "EyeSquintLeft", "EyeSquintRight" }),
            ("eyeWide", new[] { "EyeWideLeft", "EyeWideRight" }),
            ("mouthFrown", new[] { "MouthFrownLeft", "MouthFrownRight" }),
            ("mouthPress", new[] { "MouthPressLeft", "MouthPressRight" }),
            ("mouthPucker", new[] { "MouthPucker" }),
            ("cheekSquint", new[] { "CheekSquintLeft", "CheekSquintRight" }),
            ("noseSneer", new[] { "NoseSneerLeft", "NoseSneerRight" }),
        };

        private static float Value(SignFace f, string field)
        {
            switch (field)
            {
                case "smile": return f.smile;
                case "jawOpen": return f.jawOpen;
                case "browInnerUp": return f.browInnerUp;
                case "browDown": return f.browDown;
                case "browOuterUp": return f.browOuterUp;
                case "eyeSquint": return f.eyeSquint;
                case "eyeWide": return f.eyeWide;
                case "mouthFrown": return f.mouthFrown;
                case "mouthPress": return f.mouthPress;
                case "mouthPucker": return f.mouthPucker;
                case "cheekSquint": return f.cheekSquint;
                case "noseSneer": return f.noseSneer;
                default: return 0f;
            }
        }

        private void Awake()
        {
            if (face == null) face = FindFaceRenderer(transform);
            _blinkL = IndexOf(BlinkLeft);
            _blinkR = IndexOf(BlinkRight);
            ScheduleBlink();
        }

        public static SkinnedMeshRenderer FindFaceRenderer(Transform root)
        {
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = smr.sharedMesh;
                if (mesh == null) continue;
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    if (mesh.GetBlendShapeName(i).EndsWith(SmileLeft)) return smr;
                }
            }
            return null;
        }

        public int IndexOf(string arkitName)
        {
            if (face == null || face.sharedMesh == null) return -1;
            if (_index.TryGetValue(arkitName, out int cached)) return cached;
            int found = -1;
            var mesh = face.sharedMesh;
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                if (mesh.GetBlendShapeName(i).EndsWith(arkitName)) { found = i; break; }
            }
            if (found < 0) Debug.LogWarning($"[FaceDriver] Blendshape {arkitName} not found on {face.name}.");
            _index[arkitName] = found;
            return found;
        }

        public void Set(string arkitName, float weight)
        {
            int i = IndexOf(arkitName);
            if (i < 0) return;
            _target[i] = Mathf.Clamp(weight, 0f, 100f);
            if (!_current.ContainsKey(i)) _current[i] = face.GetBlendShapeWeight(i);
        }

        public void SetImmediate(string arkitName, float weight)
        {
            int i = IndexOf(arkitName);
            if (i < 0) return;
            _target[i] = _current[i] = Mathf.Clamp(weight, 0f, 100f);
            face.SetBlendShapeWeight(i, _current[i]);
        }

        /// <summary>Shows an expression (all fields; unset fields go to 0), fading over fadeSeconds unless immediate.</summary>
        public void SetExpression(SignFace expression, bool immediate = false)
        {
            var f = Resolve(expression);
            foreach (var (field, shapes) in Map)
            {
                float v = Mathf.Min(100f, Value(f, field) * intensity);
                foreach (var s in shapes)
                {
                    if (immediate) SetImmediate(s, v);
                    else Set(s, v);
                }
            }
        }

        /// <summary>v3 API, kept for compatibility: smile + jaw only.</summary>
        public void Expression(float smile, float jawOpen, bool immediate = false) =>
            SetExpression(new SignFace { smile = smile, jawOpen = jawOpen }, immediate);

        public void Neutral(bool immediate = false) => SetExpression(new SignFace(), immediate);

        private void Update()
        {
            if (face == null) return;
            float step = 100f / Mathf.Max(0.01f, fadeSeconds) * Time.deltaTime;
            foreach (var kv in _target)
            {
                float cur = _current[kv.Key];
                if (Mathf.Approximately(cur, kv.Value)) continue;
                cur = Mathf.MoveTowards(cur, kv.Value, step);
                _current[kv.Key] = cur;
                face.SetBlendShapeWeight(kv.Key, cur);
            }
            UpdateBlink();
        }

        private void UpdateBlink()
        {
            if (!idleBlink || _blinkL < 0 || _blinkR < 0) return;
            float now = Time.time;
            if (_blinkStart < 0f && now >= _nextBlink) _blinkStart = now;
            if (_blinkStart < 0f) return;
            float u = (now - _blinkStart) / blinkSeconds;
            float w = u >= 1f ? 0f : 100f * (1f - Mathf.Abs(2f * u - 1f)); // close then open
            face.SetBlendShapeWeight(_blinkL, w);
            face.SetBlendShapeWeight(_blinkR, w);
            if (u >= 1f)
            {
                _blinkStart = -1f;
                ScheduleBlink();
            }
        }

        private void ScheduleBlink() => _nextBlink = Time.time + Random.Range(blinkIntervalSeconds.x, blinkIntervalSeconds.y);
    }
}
