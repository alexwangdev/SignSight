using System;
using SignSight.Hand;
using SignSight.Learn;
using UnityEngine;

namespace SignSight.Explore
{
    /// <summary>
    /// Explore mode (v3.3 Task A): point at an object with the index finger (other fingers curled), hold 0.6 s on it,
    /// see its English word, and the teacher shows its sign — the same Demonstrate() as in Learn mode. Teaching only:
    /// no score, no stars, no Tutor, no "wrong". Objects with no template show "Not in my 50 words yet" and the teacher
    /// does nothing. The hand data comes from the existing XRHandsProvider; nothing reads the learner's face.
    /// </summary>
    public class ExploreMode : MonoBehaviour
    {
        public enum State { Pointing, Demonstrating, Shown }

        public const float HoldSeconds = 0.6f;
        public const float RayLength = 2.0f;
        // Pointing pose: index straight, middle / ring / pinky curled (CurlCalculator, 0 straight .. 1 curled).
        public const float IndexMaxCurl = 0.35f, OthersMinCurl = 0.5f;

        [SerializeField] private LessonManager lessons;
        [SerializeField] private TeacherAvatarDriver teacher;
        [SerializeField] private XRHandsProvider hands;
        [SerializeField] private GameObject objectsRoot;

        public State Current { get; private set; } = State.Pointing;
        public CocoMapping.Entry Selected { get; private set; }
        public SignTemplate SelectedTemplate { get; private set; }
        public float HoldProgress { get; private set; }
        public bool IsPointing { get; private set; }
        public ExploreObject Target { get; private set; }
        /// <summary>Label under the pointing ray (a virtual object, or a YOLO box on the camera panel).</summary>
        public string TargetLabel { get; private set; }
        public int Selections { get; private set; }

        // ---- v3.3 Task B: camera + YOLO (Editor: mock camera; device: not implemented, virtual objects) ----
        /// <summary>Explore uses the camera when the model and a mock source exist (Editor only). Off = virtual objects.</summary>
        public bool PreferCamera = true;
        public static readonly Vector3 CameraPanelPosition = new Vector3(0.38f, 0.0f, 0.95f); // clear of MENU and the instruction (viewport check)
        public bool UsingCamera { get; private set; }
        public YoloDetector Detector { get; private set; }
        public MockCameraProvider Mock { get; private set; }
        public ExploreCameraView View { get; private set; }

        public static bool CameraAvailable =>
            Application.isEditor &&
            System.IO.File.Exists(System.IO.Path.Combine(Application.streamingAssetsPath, YoloDetector.ModelFile)) &&
            (System.IO.File.Exists(MockCameraProvider.MockPath("explore-mock.png")) ||
             System.IO.File.Exists(MockCameraProvider.MockPath("explore-mock.mp4")) || WebCamTexture.devices.Length > 0);

        /// <summary>Test hook: a ray to use instead of the hands (Editor checks without a webcam).</summary>
        public Ray? DebugRay;

        public event Action Changed;

        private float _holdStart = float.NaN;
        private LineRenderer _line;

        private void Awake()
        {
            if (lessons == null) lessons = FindFirstObjectByType<LessonManager>();
            if (teacher == null) teacher = FindFirstObjectByType<TeacherAvatarDriver>();
            if (hands == null) hands = FindFirstObjectByType<XRHandsProvider>();
            if (objectsRoot == null)
            {
                var o = GameObject.Find("ExploreObjects");
                if (o != null) objectsRoot = o;
            }
            _line = new GameObject("PointingRay").AddComponent<LineRenderer>();
            _line.transform.SetParent(transform, false);
            _line.positionCount = 2;
            _line.startWidth = _line.endWidth = 0.003f;
            var mat = new Material(Resources.Load<Material>("UI/UIIcon")) { name = "PointingRay" }; // plain, untextured
            mat.SetColor("_BaseColor", new Color(0.62f, 0.72f, 1f, 0.8f));
            _line.sharedMaterial = mat;
            _line.enabled = false;

            // Start card small line: real objects when the (Editor) camera path is available.
            if (PreferCamera && CameraAvailable)
            {
                var sub = GameObject.Find("LearnUI/StartCards/ExploreCard/Subtitle");
                var t = sub != null ? sub.GetComponent<TMPro.TMP_Text>() : null;
                if (t != null) t.text = t.text.Replace("3 objects to try", "Real objects (mock camera)");
            }
        }

        private void OnEnable()
        {
            if (teacher != null) teacher.DemonstrationFinished += OnDemoFinished;
        }

        private void OnDisable()
        {
            if (teacher != null) teacher.DemonstrationFinished -= OnDemoFinished;
        }

        /// <summary>Entering / leaving Explore (LessonManager calls this).</summary>
        public void SetActive(bool on)
        {
            UsingCamera = on && PreferCamera && CameraAvailable;
            if (objectsRoot != null) objectsRoot.SetActive(on && !UsingCamera);
            SetCamera(UsingCamera);
            Current = State.Pointing;
            Selected = default;
            SelectedTemplate = null;
            ResetHold();
            if (!on && teacher != null && teacher.IsDemonstrating) teacher.Rest();
            _line.enabled = false;
            Changed?.Invoke();
        }

        private bool InExplore => lessons != null && lessons.CurrentMode == LessonManager.Mode.Explore;

        /// <summary>Loads YOLO + the mock camera on entering Explore, releases both on leaving (Learn never loads them).</summary>
        private void SetCamera(bool on)
        {
            if (on)
            {
                if (View == null)
                {
                    var root = new GameObject("ExploreCamera");
                    var ui = GameObject.Find("LearnUI");
                    root.transform.SetParent(ui != null ? ui.transform : transform, false);
                    root.transform.localPosition = CameraPanelPosition;
                    Mock = root.AddComponent<MockCameraProvider>();
                    Detector = root.AddComponent<YoloDetector>();
                    View = new GameObject("View").AddComponent<ExploreCameraView>();
                    View.transform.SetParent(root.transform, false);
                }
                Mock.gameObject.SetActive(true);
                Mock.enabled = true;
                Detector.Provider = Mock;
                View.Provider = Mock;
                View.Detector = Detector;
                StartCoroutine(Detector.Load());
            }
            else if (View != null)
            {
                Detector.Unload();
                Mock.enabled = false;
                Mock.gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            // The camera panel shows only while pointing, so it never covers the word card or the teacher's demo.
            if (View != null) View.gameObject.SetActive(UsingCamera && InExplore && Current == State.Pointing);
        }

        private void Update()
        {
            if (!InExplore || Current != State.Pointing)
            {
                _line.enabled = false;
                return;
            }
            bool had = IsPointing;
            IsPointing = TryGetRay(out var ray);
            ExploreObject hit = null;
            string label = null;
            float len = RayLength;
            if (IsPointing && Physics.Raycast(ray, out var info, RayLength, ~0, QueryTriggerInteraction.Collide))
            {
                hit = info.collider.GetComponentInParent<ExploreObject>();
                if (hit != null) label = hit.Label;
                else if (UsingCamera && View != null) label = View.LabelAt(info); // a YOLO box on the camera panel
                len = info.distance;
            }
            _line.enabled = IsPointing;
            if (IsPointing)
            {
                _line.SetPosition(0, ray.origin);
                _line.SetPosition(1, ray.origin + ray.direction * len);
            }

            if (hit != Target)
            {
                if (Target != null) Target.SetTargeted(false);
                Target = hit;
                if (Target != null) Target.SetTargeted(true);
            }
            if (label != TargetLabel)
            {
                TargetLabel = label;
                if (View != null) View.HighlightLabel = label;
                _holdStart = label != null ? Time.time : float.NaN; // a passing ray restarts the hold
            }
            float p = TargetLabel != null && !float.IsNaN(_holdStart) ? Mathf.Clamp01((Time.time - _holdStart) / HoldSeconds) : 0f;
            bool changed = had != IsPointing || !Mathf.Approximately(p, HoldProgress);
            HoldProgress = p;
            if (p >= 1f) Select(TargetLabel);
            else if (changed) Changed?.Invoke();
        }

        private bool TryGetRay(out Ray ray)
        {
            if (DebugRay.HasValue)
            {
                ray = DebugRay.Value;
                return true;
            }
            ray = default;
            if (hands == null) return false;
            foreach (var side in new[] { HandSide.Right, HandSide.Left })
            {
                if (!hands.TryGetHand(side, out var f)) continue;
                var curl = CurlCalculator.Compute(f, hands.Head).Curl;
                if (curl[1] > IndexMaxCurl || curl[2] < OthersMinCurl || curl[3] < OthersMinCurl || curl[4] < OthersMinCurl) continue;
                var tip = f.Pos(UnityEngine.XR.Hands.XRHandJointID.IndexTip);
                var prox = f.Pos(UnityEngine.XR.Hands.XRHandJointID.IndexProximal);
                var dir = tip - prox;
                if (dir.sqrMagnitude < 1e-6f) continue;
                ray = new Ray(tip, dir.normalized);
                return true;
            }
            return false;
        }

        private void ResetHold()
        {
            if (Target != null) Target.SetTargeted(false);
            Target = null;
            TargetLabel = null;
            if (View != null) View.HighlightLabel = null;
            _holdStart = float.NaN;
            HoldProgress = 0f;
            IsPointing = false;
        }

        /// <summary>Shows the word for an object label and, if it is one of the 50 words, the teacher's sign.</summary>
        public void Select(string label)
        {
            ResetHold();
            Selected = CocoMapping.Lookup(label);
            Selections++;
            SelectedTemplate = Selected.Intent != null ? TemplateStore.Canonical(Selected.Intent) : null;
            if (SelectedTemplate != null)
            {
                Current = State.Demonstrating;
                teacher.Demonstrate(SelectedTemplate);
            }
            else Current = State.Shown; // not one of the 50 words: the word and "Not in my 50 words yet", no demo
            Changed?.Invoke();
        }

        public void Replay()
        {
            if (SelectedTemplate == null || Current == State.Pointing) return;
            Current = State.Demonstrating;
            teacher.Demonstrate(SelectedTemplate);
            Changed?.Invoke();
        }

        public void ReplayFace()
        {
            if (SelectedTemplate == null || Current == State.Pointing) return;
            Current = State.Demonstrating;
            teacher.DemonstrateFace(SelectedTemplate, 0.5f);
            Changed?.Invoke();
        }

        /// <summary>Back to pointing at another object.</summary>
        public void Back()
        {
            if (teacher.IsDemonstrating) teacher.Rest();
            Current = State.Pointing;
            Selected = default;
            SelectedTemplate = null;
            ResetHold();
            Changed?.Invoke();
        }

        private void OnDemoFinished()
        {
            if (!InExplore || Current != State.Demonstrating) return;
            Current = State.Shown;
            Changed?.Invoke();
        }
    }
}
