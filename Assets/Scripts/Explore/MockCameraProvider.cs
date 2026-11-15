using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace SignSight.Explore
{
    /// <summary>
    /// Editor-only camera stand-in for Explore (v3.3 Task B). The Passthrough Camera API gives no frames in the Editor or
    /// the XR Simulator, so the detector is fed from, in order of preference:
    ///   1. a video clip   StreamingAssets/explore-mock.mp4   (if present),
    ///   2. a still image  StreamingAssets/explore-mock.png   (CC0 photos of an apple, cup, bottle, bananas — see
    ///      workspace/.../explore-mock/compose_mock.py for the sources),
    ///   3. a webcam whose name contains WebcamName (e.g. "DroidCam" — a phone pointed at the desk, so the laptop
    ///      webcam stays free for Simulator hand tracking).
    /// Video / webcam frames are copied at FramesPerSecond (5-10), not 30. Not available outside the Editor.
    /// </summary>
    public class MockCameraProvider : MonoBehaviour, ICameraFrameProvider
    {
        public enum Kind { Auto, Video, Image, Webcam }

        public Kind Use = Kind.Auto;
        public string WebcamName = "DroidCam";
        [Range(1f, 30f)] public float FramesPerSecond = 8f;

        public Texture2D Frame { get; private set; }
        public bool IsAvailable => Application.isEditor && Frame != null;
        public string Source { get; private set; } = "none";

        private VideoPlayer _video;
        private RenderTexture _videoRt;
        private WebCamTexture _webcam;
        private float _next;

        public static string MockPath(string file) => Path.Combine(Application.streamingAssetsPath, file);

        private void OnEnable()
        {
            if (!Application.isEditor) return; // never on a device: QuestCameraProvider is the (future) device path
            var kind = Use;
            if (kind == Kind.Auto)
                kind = File.Exists(MockPath("explore-mock.mp4")) ? Kind.Video
                    : File.Exists(MockPath("explore-mock.png")) ? Kind.Image : Kind.Webcam;
            switch (kind)
            {
                case Kind.Video: StartVideo(); break;
                case Kind.Image: LoadImage(); break;
                default: StartWebcam(); break;
            }
        }

        private void OnDisable()
        {
            if (_webcam != null) { _webcam.Stop(); _webcam = null; }
            if (_video != null) { Destroy(_video); _video = null; }
            if (_videoRt != null) { _videoRt.Release(); _videoRt = null; }
            if (Frame != null) { Destroy(Frame); Frame = null; }
            Source = "none";
        }

        private void LoadImage()
        {
            // linear = true: the model must get the file's pixel values, not sRGB-decoded (darker) ones — the project
            // renders in Linear colour space. (With sRGB decoding the apple scored 0.04.)
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false, true) { name = "explore-mock.png" };
            tex.LoadImage(File.ReadAllBytes(MockPath("explore-mock.png")));
            Frame = tex;
            Source = $"image explore-mock.png ({tex.width}x{tex.height})";
        }

        private void StartVideo()
        {
            _video = gameObject.AddComponent<VideoPlayer>();
            _video.url = MockPath("explore-mock.mp4");
            _video.isLooping = true;
            _video.audioOutputMode = VideoAudioOutputMode.None;
            _videoRt = new RenderTexture(1280, 720, 0);
            _video.renderMode = VideoRenderMode.RenderTexture;
            _video.targetTexture = _videoRt;
            _video.Play();
            Source = "video explore-mock.mp4";
        }

        private void StartWebcam()
        {
            string device = null;
            foreach (var d in WebCamTexture.devices)
                if (d.name.Contains(WebcamName)) device = d.name;
            if (device == null && WebCamTexture.devices.Length > 0) device = WebCamTexture.devices[0].name;
            if (device == null)
            {
                Source = "no webcam";
                return;
            }
            _webcam = new WebCamTexture(device, 1280, 720, 30);
            _webcam.Play();
            Source = $"webcam {device}";
        }

        private void Update()
        {
            if (_video == null && _webcam == null) return;
            if (Time.time < _next) return;
            _next = Time.time + 1f / FramesPerSecond;
            if (_webcam != null && _webcam.didUpdateThisFrame && _webcam.width > 16)
            {
                if (Frame == null || Frame.width != _webcam.width || Frame.height != _webcam.height)
                    Frame = new Texture2D(_webcam.width, _webcam.height, TextureFormat.RGBA32, false, true) { name = "webcam" };
                Frame.SetPixels32(_webcam.GetPixels32());
                Frame.Apply();
            }
            else if (_video != null && _video.isPrepared)
            {
                if (Frame == null) Frame = new Texture2D(_videoRt.width, _videoRt.height, TextureFormat.RGB24, false, true) { name = "video" };
                var prev = RenderTexture.active;
                RenderTexture.active = _videoRt;
                Frame.ReadPixels(new Rect(0, 0, _videoRt.width, _videoRt.height), 0, 0);
                Frame.Apply();
                RenderTexture.active = prev;
            }
        }
    }
}
