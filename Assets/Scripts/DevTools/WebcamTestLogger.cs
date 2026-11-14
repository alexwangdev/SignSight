#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using SignSight.Hand;
using SignSight.Learn;
using UnityEngine;

namespace SignSight.DevTools
{
    /// <summary>
    /// Editor Play Mode only: logs every recogniser attempt during a real-hands test (Simulator + webcam) — the target,
    /// Top-1 / Top-2, the target's handshape / location / movement terms and hint, plus hand-tracking uptime.
    /// Writes Logs/v31-webcam-&lt;time&gt;.txt (one line per attempt).
    /// </summary>
    public class WebcamTestLogger : MonoBehaviour
    {
        public string PathOut;
        private SignRecognizer _rec;
        private LessonManager _lm;
        private int _frames, _right, _left;
        private readonly StringBuilder _log = new StringBuilder();

        public static WebcamTestLogger Begin(string[] words)
        {
            var go = new GameObject("WebcamTestLogger");
            var l = go.AddComponent<WebcamTestLogger>();
            l.PathOut = $"Logs/v31-webcam-{System.DateTime.Now:HHmmss}.txt";
            l._lm = FindFirstObjectByType<LessonManager>();
            l._rec = FindFirstObjectByType<SignRecognizer>();
            l._rec.Recognized += l.OnRec;
            var all = l._lm.Data.lessons.SelectMany(x => x.words).ToList();
            l._lm.StartPractice(words.Select(w => all.First(x => x.intent == w)).ToArray());
            l.Write($"start {System.DateTime.Now:T}, words: {string.Join(" ", words)}");
            return l;
        }

        private void OnDestroy()
        {
            if (_rec != null) _rec.Recognized -= OnRec;
        }

        private void Update()
        {
            var p = FindFirstObjectByType<XRHandsProvider>();
            if (p == null || _lm.Current != LessonManager.Phase.Copy) return;
            _frames++;
            if (p.TryGetHand(HandSide.Right, out _)) _right++;
            if (p.TryGetHand(HandSide.Left, out _)) _left++;
        }

        private void OnRec(RecognitionResult r)
        {
            var target = _lm.CurrentTemplate;
            if (target == null) return;
            var e = r.For(target.intent);
            bool ok = ScoreManager.IsCorrect(_lm.CurrentWord, r);
            string te = e.HasValue
                ? $"target total {e.Value.Total:0.00} (H {e.Value.Handshape:0.00} L {e.Value.Location:0.00} M {e.Value.Movement:0.00}) completed {e.Value.Completed} hint \"{e.Value.Hint}\""
                : "target not evaluated";
            var top3 = r.All?.OrderBy(x => x.Total).Take(3).Select(x => $"{x.Template.intent} {x.Total:0.00}{(x.Completed ? "*" : "")}");
            float up = _frames > 0 ? 1f / _frames : 0f;
            Write($"{System.DateTime.Now:T} {target.intent}: {(ok ? "CORRECT" : "wrong")} top1 {r.Top1?.intent} {r.Distance:0.00} " +
                  $"| {te} | top3 {string.Join(", ", top3 ?? Enumerable.Empty<string>())} | tracked R {_right * up:0.00} L {_left * up:0.00}");
            _frames = _right = _left = 0;
        }

        public void Write(string s)
        {
            _log.AppendLine(s);
            File.WriteAllText(PathOut, _log.ToString());
            Debug.Log("[WebcamTest] " + s);
        }
    }
}
#endif
