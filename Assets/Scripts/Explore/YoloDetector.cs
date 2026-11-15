using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.Networking;

namespace SignSight.Explore
{
    /// <summary>
    /// On-device COCO object detection for Explore (v3.3 Task B) with Unity Inference Engine 2.6.1 (already installed).
    /// Model: YOLOv9-t from Meta's Unity-PassthroughCameraApiSamples (MultiObjectDetection, MIT licence per that repo's
    /// README — MultimediaTechLab/YOLO). Rebuilt from Meta's yolov9onnx.onnx with Meta's graph but Float16 weights
    /// instead of Meta's UInt8 (Editor/ExploreModelBuilder.cs): StreamingAssets/yolov9-t.sentis, 4.2 MB.
    /// Loaded only when Explore uses the camera (Load), released when leaving (Unload): Learn mode never loads it.
    /// Reads frames only through ICameraFrameProvider. No network, no cloud.
    /// </summary>
    public class YoloDetector : MonoBehaviour
    {
        public const string ModelFile = "yolov9-t.sentis";
        public const string LabelsFile = "coco-classes.txt";

        public struct Detection
        {
            public string Label;
            public float Score;
            public Rect Box; // normalised 0..1 in the frame, origin top-left (y down), like the model's output
        }

        [Range(0f, 1f)] public float ScoreThreshold = 0.4f;
        [Range(0f, 1f)] public float IoUThreshold = 0.6f;   // as in Meta's sample
        [Range(1f, 15f)] public float TargetFps = 8f;
        [Tooltip("CPU, as in Meta's sample (~90 ms per frame in the Editor after a slow first run).")]
        public BackendType Backend = BackendType.CPU;
        [Tooltip("Which texture row becomes tensor row 0 (Inference Engine default: TopLeft).")]
        public CoordOrigin Origin = CoordOrigin.TopLeft;

        /// <summary>Best raw score of the last frame per class (before the threshold), for tuning / the report.</summary>
        public readonly Dictionary<string, float> TopScores = new Dictionary<string, float>();

        public ICameraFrameProvider Provider;
        public bool IsLoaded => _worker != null;
        public IReadOnlyList<Detection> Detections => _detections;
        public float MeasuredFps { get; private set; }
        public float LastInferenceMs { get; private set; }
        public int Runs { get; private set; }

        private Worker _worker;
        private Vector2Int _inputSize;
        private string[] _labels;
        private readonly List<Detection> _detections = new List<Detection>();
        private Coroutine _loop;
        private float _fpsStart;
        private int _fpsRuns;

        public IEnumerator Load()
        {
            if (_worker != null) yield break;
            byte[] modelBytes = null, labelBytes = null;
            yield return ReadStreaming(ModelFile, b => modelBytes = b);
            yield return ReadStreaming(LabelsFile, b => labelBytes = b);
            if (modelBytes == null || labelBytes == null)
            {
                Debug.LogWarning("[YoloDetector] Model or labels missing in StreamingAssets.");
                yield break;
            }
            _labels = System.Text.Encoding.UTF8.GetString(labelBytes).Replace("\r", "").Split('\n');
            using (var ms = new MemoryStream(modelBytes))
            {
                var model = ModelLoader.Load(ms);
                var shape = model.inputs[0].shape;
                _inputSize = new Vector2Int(shape.Get(3), shape.Get(2)); // NCHW: width, height
                _worker = new Worker(model, Backend);
            }
            _loop = StartCoroutine(Loop());
        }

        public void Unload()
        {
            if (_loop != null) StopCoroutine(_loop);
            _loop = null;
            _detections.Clear();
            if (_worker != null)
            {
                _worker.Dispose();
                _worker = null;
            }
            if (_lb != null)
            {
                _lb.Release();
                _lb = null;
            }
        }

        private void OnDestroy() => Unload();

        private static IEnumerator ReadStreaming(string file, System.Action<byte[]> done)
        {
            string path = Path.Combine(Application.streamingAssetsPath, file);
            if (!path.Contains("://"))
            {
                done(File.Exists(path) ? File.ReadAllBytes(path) : null);
                yield break;
            }
            using var req = UnityWebRequest.Get(path); // Android: StreamingAssets live inside the APK
            yield return req.SendWebRequest();
            done(req.result == UnityWebRequest.Result.Success ? req.downloadHandler.data : null);
        }

        private IEnumerator Loop()
        {
            _fpsStart = Time.realtimeSinceStartup;
            while (_worker != null)
            {
                float t0 = Time.realtimeSinceStartup;
                if (Provider != null && Provider.IsAvailable && Provider.Frame != null) yield return RunOnce(Provider.Frame);
                else yield return null;
                float wait = 1f / TargetFps - (Time.realtimeSinceStartup - t0);
                if (wait > 0f) yield return new WaitForSecondsRealtime(wait);
            }
        }

        private IEnumerator RunOnce(Texture frame)
        {
            float t0 = Time.realtimeSinceStartup;
            var lb = Letterbox(frame, out var content);
            var transform = new TextureTransform().SetCoordOrigin(Origin);
            using var input = new Tensor<float>(new TensorShape(1, 3, _inputSize.y, _inputSize.x));
            TextureConverter.ToTensor(lb, input, transform);
            _worker.Schedule(input);

            var boxesA = (_worker.PeekOutput(0) as Tensor<float>).ReadbackAndCloneAsync().GetAwaiter();
            while (!boxesA.IsCompleted) yield return null;
            using var boxes = boxesA.GetResult();
            var idsA = (_worker.PeekOutput(1) as Tensor<int>).ReadbackAndCloneAsync().GetAwaiter();
            while (!idsA.IsCompleted) yield return null;
            using var ids = idsA.GetResult();
            var scoresA = (_worker.PeekOutput(2) as Tensor<float>).ReadbackAndCloneAsync().GetAwaiter();
            while (!scoresA.IsCompleted) yield return null;
            using var scores = scoresA.GetResult();

            TopScores.Clear();
            if (scores.shape[0] > 0)
            {
                var sa = scores.AsReadOnlyNativeArray();
                for (int i = 0; i < sa.Length; i++)
                {
                    string name = ids[i] >= 0 && ids[i] < _labels.Length ? _labels[ids[i]].Trim() : "?";
                    if (!TopScores.TryGetValue(name, out var s) || sa[i] > s) TopScores[name] = sa[i];
                }
            }
            var raw = new List<(int classId, Vector4 box, float score)>();
            if (boxes.shape[0] > 0) NonMaxSuppression(raw, boxes, ids, scores, IoUThreshold, ScoreThreshold);
            _detections.Clear();
            foreach (var (classId, b, score) in raw)
            {
                _detections.Add(new Detection
                {
                    Label = classId >= 0 && classId < _labels.Length ? _labels[classId].Trim() : "?",
                    Score = score,
                    // model pixels -> letterbox content -> 0..1 in the original frame (top-left origin)
                    Box = new Rect((b.x - content.x) / content.width, (b.y - content.y) / content.height,
                                   (b.z - b.x) / content.width, (b.w - b.y) / content.height),
                });
            }
            LastInferenceMs = (Time.realtimeSinceStartup - t0) * 1000f;
            Runs++;
            _fpsRuns++;
            float el = Time.realtimeSinceStartup - _fpsStart;
            if (el >= 2f)
            {
                MeasuredFps = _fpsRuns / el;
                _fpsRuns = 0;
                _fpsStart = Time.realtimeSinceStartup;
            }
        }

        private RenderTexture _lb;
        private static readonly Color Pad = new Color(114f / 255f, 114f / 255f, 114f / 255f); // YOLO's usual grey

        /// <summary>
        /// Fits the frame into the model's square input keeping its aspect ratio (grey bars), instead of squashing it.
        /// On the mock frame this raised the apple from 0.26 to 0.59. 'content' = where the frame sits, in model pixels
        /// with a top-left origin.
        /// </summary>
        private Texture Letterbox(Texture frame, out Rect content)
        {
            if (_lb == null)
                _lb = new RenderTexture(_inputSize.x, _inputSize.y, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            float s = Mathf.Min((float)_inputSize.x / frame.width, (float)_inputSize.y / frame.height);
            float cw = frame.width * s, ch = frame.height * s;
            content = new Rect((_inputSize.x - cw) * 0.5f, (_inputSize.y - ch) * 0.5f, cw, ch);
            var prev = RenderTexture.active;
            RenderTexture.active = _lb;
            GL.Clear(true, true, Pad);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, _inputSize.x, _inputSize.y, 0); // y down, so the rect is top-left based
            Graphics.DrawTexture(new Rect(content.x, content.y, cw, ch), frame);
            GL.PopMatrix();
            RenderTexture.active = prev;
            return _lb;
        }

        // ---- NMS: copied from Meta's SentisInferenceRunManager.NonMaxSuppression (Unity-PassthroughCameraApiSamples,
        // MultiObjectDetection); only change: the score is kept with each detection. ----
        private static void NonMaxSuppression(List<(int classId, Vector4 box, float score)> outDetections, Tensor<float> boxes,
                                              Tensor<int> classIDs, Tensor<float> scores, float iouThreshold, float scoreThreshold)
        {
            outDetections.Clear();
            var filteredIndices = new List<int>();
            NativeArray<float>.ReadOnly scoresArray = scores.AsReadOnlyNativeArray();
            for (int i = 0; i < scoresArray.Length; i++)
            {
                if (scoresArray[i] >= scoreThreshold) filteredIndices.Add(i);
            }
            if (filteredIndices.Count == 0) return;
            filteredIndices.Sort((a, b) => scoresArray[b].CompareTo(scoresArray[a]));
            bool[] suppressed = new bool[filteredIndices.Count];
            for (int i = 0; i < filteredIndices.Count; i++)
            {
                if (suppressed[i]) continue;
                int idx = filteredIndices[i];
                outDetections.Add((classIDs[idx], GetBox(idx), scoresArray[idx]));
                for (int j = i + 1; j < filteredIndices.Count; j++)
                {
                    if (suppressed[j]) continue;
                    int jdx = filteredIndices[j];
                    if (CalculateIoU(GetBox(idx), GetBox(jdx)) > iouThreshold) suppressed[j] = true;
                }
            }
            Vector4 GetBox(int i) => new Vector4(boxes[i, 0], boxes[i, 1], boxes[i, 2], boxes[i, 3]);
        }

        private static float CalculateIoU(Vector4 boxA, Vector4 boxB)
        {
            float x1 = Mathf.Max(boxA.x, boxB.x), y1 = Mathf.Max(boxA.y, boxB.y);
            float x2 = Mathf.Min(boxA.z, boxB.z), y2 = Mathf.Min(boxA.w, boxB.w);
            float inter = Mathf.Max(0, x2 - x1) * Mathf.Max(0, y2 - y1);
            float union = (boxA.z - boxA.x) * (boxA.w - boxA.y) + (boxB.z - boxB.x) * (boxB.w - boxB.y) - inter;
            return union == 0 ? 0 : inter / union;
        }
    }
}
