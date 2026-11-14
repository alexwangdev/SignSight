using System.Collections.Generic;
using UnityEngine;

namespace SignSight.Learn
{
    /// <summary>
    /// Teacher face (SPEC v3 §3.5): ARKit blendshapes looked up by name, smoothed in/out over 0.3 s, plus idle blinking.
    /// Display only — nothing here reads or scores the learner's face.
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

        private void Awake()
        {
            if (face == null) face = FindFaceRenderer(transform);
            _blinkL = IndexOf(BlinkLeft);
            _blinkR = IndexOf(BlinkRight);
            ScheduleBlink();
        }

        /// <summary>The renderer that carries the ARKit shapes (the one with MouthSmileLeft).</summary>
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

        /// <summary>Blendshape index by ARKit name. Rocketbox names look like "blendShape1.AK_44_MouthSmileLeft", so match the suffix.</summary>
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

        /// <summary>Fades a blendshape to a weight (0..100) over fadeSeconds.</summary>
        public void Set(string arkitName, float weight)
        {
            int i = IndexOf(arkitName);
            if (i < 0) return;
            _target[i] = Mathf.Clamp(weight, 0f, 100f);
            if (!_current.ContainsKey(i)) _current[i] = face.GetBlendShapeWeight(i);
        }

        /// <summary>Sets a blendshape at once (stills / checks).</summary>
        public void SetImmediate(string arkitName, float weight)
        {
            int i = IndexOf(arkitName);
            if (i < 0) return;
            _target[i] = _current[i] = Mathf.Clamp(weight, 0f, 100f);
            face.SetBlendShapeWeight(i, _current[i]);
        }

        /// <summary>THANK-YOU smile / HOT breath (SPEC v3 §3.5): smile on both mouth corners plus jaw.</summary>
        public void Expression(float smile, float jawOpen, bool immediate = false)
        {
            if (immediate)
            {
                SetImmediate(SmileLeft, smile);
                SetImmediate(SmileRight, smile);
                SetImmediate(JawOpen, jawOpen);
            }
            else
            {
                Set(SmileLeft, smile);
                Set(SmileRight, smile);
                Set(JawOpen, jawOpen);
            }
        }

        public void Neutral(bool immediate = false) => Expression(0f, 0f, immediate);

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
