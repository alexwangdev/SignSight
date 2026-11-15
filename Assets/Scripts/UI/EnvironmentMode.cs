using System.Collections;
using SignSight.Learn;
using UnityEngine;

namespace SignSight.UI
{
    /// <summary>
    /// v3.8 Task 0: passthrough per mode. Learn and Exam are virtual rooms (passthrough off, solid warm background);
    /// Start, Explore and Relax keep the real room (passthrough on). Hooked to LessonManager.ModeChanged only.
    /// Never hard-cuts: a black fade (0.3 s out, switch at black, 0.3 s in) on the learner's eye camera.
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
        private bool _virtual;
        private Coroutine _run;

        /// <summary>True while a virtual room is shown (passthrough off).</summary>
        public bool VirtualRoom => _virtual;
        /// <summary>Set by the checks; false only while a fade is running.</summary>
        public bool Fading { get; private set; }

        private void Awake()
        {
            _lessons = FindFirstObjectByType<LessonManager>();
            _passthrough = FindFirstObjectByType<OVRPassthroughLayer>();
            var anchor = GameObject.Find("CenterEyeAnchor");
            _eye = anchor != null ? anchor.GetComponent<Camera>() : Camera.main;
            _virtual = false;
            BuildFade();
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
            bool wantVirtual = Wants(m);
            if (wantVirtual == _virtual && _run == null) return; // Start/Explore/Relax etc: nothing to change
            if (_run != null) StopCoroutine(_run);
            _run = StartCoroutine(Transition());
        }

        /// <summary>Learn and Exam are virtual rooms; everything else shows the real room.</summary>
        public static bool Wants(LessonManager.Mode m) => m == LessonManager.Mode.Learn || m == LessonManager.Mode.Exam;

        private IEnumerator Transition()
        {
            Fading = true;
            _fade.gameObject.SetActive(true);
            yield return Fade(0f, 1f);

            // at black: switch to whatever the current mode needs (read again in case the mode moved on)
            bool wantVirtual = Wants(_lessons.CurrentMode);
            if (_passthrough != null) _passthrough.enabled = !wantVirtual;
            if (_eye != null)
            {
                _eye.clearFlags = CameraClearFlags.SolidColor;
                _eye.backgroundColor = wantVirtual ? RoomBackground : PassthroughBackground;
            }
            _virtual = wantVirtual;

            yield return Fade(1f, 0f);
            _fade.gameObject.SetActive(false);
            Fading = false;
            _run = null;
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
