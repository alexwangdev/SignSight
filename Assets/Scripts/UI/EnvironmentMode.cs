using System.Collections;
using SignSight.Learn;
using UnityEngine;

namespace SignSight.UI
{
    /// <summary>Which room the learner sees: the real room (passthrough on), the virtual classroom or the virtual café.</summary>
    public enum Room { None, Classroom, Cafe }

    /// <summary>
    /// v3.8 Task 0, v3.9 Task B: the room per mode. Start and Learn show the virtual classroom, Exam the virtual café,
    /// Explore and Relax the real room (passthrough on). Hooked to LessonManager.ModeChanged only.
    /// A fade (0.3 s out, switch at black, 0.3 s in) runs whenever the ROOM changes, never when it stays the same:
    /// Start ↔ Learn and Learn → Start do not fade; Start → Exam (classroom → café) does, although passthrough stays off.
    /// </summary>
    public class EnvironmentMode : MonoBehaviour
    {
        public const float FadeSeconds = 0.3f;
        public static readonly Color RoomBackground = new Color(0.35f, 0.27f, 0.22f, 1f);
        public static readonly Color PassthroughBackground = new Color(0f, 0f, 0f, 0f);

        private LessonManager _lessons;
        private OVRPassthroughLayer _passthrough;
        private Camera _eye;
        private Transform _fade;
        private Material _fadeMat;
        private Room _room = Room.None;
        private Coroutine _run;

        /// <summary>The room now on screen.</summary>
        public Room CurrentRoom => _room;
        /// <summary>True while a virtual room is shown (passthrough off).</summary>
        public bool VirtualRoom => _room != Room.None;
        /// <summary>True only while a fade is running.</summary>
        public bool Fading { get; private set; }

        /// <summary>The room a mode shows.</summary>
        public static Room RoomFor(LessonManager.Mode m)
        {
            switch (m)
            {
                case LessonManager.Mode.Start:
                case LessonManager.Mode.Learn: return Room.Classroom;
                case LessonManager.Mode.Exam: return Room.Cafe;
                default: return Room.None; // Explore, Relax
            }
        }

        private void Awake()
        {
            _lessons = FindFirstObjectByType<LessonManager>();
            _passthrough = FindFirstObjectByType<OVRPassthroughLayer>();
            var anchor = GameObject.Find("CenterEyeAnchor");
            _eye = anchor != null ? anchor.GetComponent<Camera>() : Camera.main;
            BuildFade();
        }

        private void Start()
        {
            // the boot screen is Start: show its room straight away, without a fade
            if (_lessons == null) return;
            _room = RoomFor(_lessons.CurrentMode);
            ApplyRoom(_room);
        }

        private void OnEnable()
        {
            if (_lessons != null) _lessons.ModeChanged += OnModeChanged;
        }

        private void OnDisable()
        {
            if (_lessons != null) _lessons.ModeChanged -= OnModeChanged;
        }

        private void OnDestroy()
        {
            if (_fadeMat != null) Destroy(_fadeMat);
        }

        private void OnModeChanged(LessonManager.Mode m)
        {
            // same room before and after (e.g. Start → Learn, Learn → Start): nothing changes on screen, so no fade
            if (RoomFor(m) == _room && _run == null) return;
            if (_run != null) StopCoroutine(_run);
            _run = StartCoroutine(Transition());
        }

        private IEnumerator Transition()
        {
            Fading = true;
            _fade.gameObject.SetActive(true);
            yield return Fade(0f, 1f);

            // at black: switch to whatever the current mode needs (read again in case the mode moved on)
            _room = RoomFor(_lessons.CurrentMode);
            ApplyRoom(_room);

            yield return Fade(1f, 0f);
            _fade.gameObject.SetActive(false);
            Fading = false;
            _run = null;
        }

        private void ApplyRoom(Room r)
        {
            if (_passthrough != null) _passthrough.enabled = r == Room.None;
            if (_eye != null)
            {
                _eye.clearFlags = CameraClearFlags.SolidColor;
                _eye.backgroundColor = r == Room.None ? PassthroughBackground : RoomBackground;
            }
        }

        private IEnumerator Fade(float from, float to)
        {
            float t = 0f;
            while (t < FadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                SetFadeAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(t / FadeSeconds)));
                yield return null;
            }
            SetFadeAlpha(to);
        }

        private void SetFadeAlpha(float a)
        {
            var c = new Color(0f, 0f, 0f, a);
            _fadeMat.SetColor("_BaseColor", c);
            _fadeMat.SetColor("_Color", c);
        }

        private void BuildFade()
        {
            // a black quad just in front of the learner's eye camera: it covers the whole view during the fade
            if (_eye == null) return;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "EnvironmentFade";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(_eye.transform, false);
            quad.transform.localPosition = new Vector3(0f, 0f, 0.3f);
            quad.transform.localScale = new Vector3(3f, 3f, 1f);
            _fadeMat = new Material(Resources.Load<Material>("UI/Confetti")) { name = "EnvironmentFade" };
            _fadeMat.SetFloat("_Cull", 0f);
            _fadeMat.color = new Color(0f, 0f, 0f, 0f);
            quad.GetComponent<Renderer>().sharedMaterial = _fadeMat;
            _fade = quad.transform;
            _fade.gameObject.SetActive(false);
        }
    }
}
