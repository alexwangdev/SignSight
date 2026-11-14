#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using System.Text;
using SignSight.Hand;
using SignSight.Learn;
using UnityEngine;

namespace SignSight.DevTools
{
    /// <summary>
    /// Editor Play Mode only: plays one full lesson in real time (real teacher demonstrations, real phases and
    /// timers); only the learner's attempt is injected as a recogniser result (no hands in front of the camera).
    /// Logs every error / exception raised meanwhile. Writes Logs/v31-lesson-smoke.txt.
    /// </summary>
    public class LessonSmokeRun : MonoBehaviour
    {
        public bool Done { get; private set; }
        public readonly StringBuilder Log = new StringBuilder();
        private int _errors;

        public static LessonSmokeRun Begin(int lesson)
        {
            var r = new GameObject("LessonSmokeRun").AddComponent<LessonSmokeRun>();
            r.StartCoroutine(r.Run(lesson));
            return r;
        }

        private void OnEnable() => Application.logMessageReceived += OnLog;
        private void OnDisable() => Application.logMessageReceived -= OnLog;

        private void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                _errors++;
                Log.AppendLine($"  ERROR: {msg}");
            }
        }

        private IEnumerator Run(int lessonIndex)
        {
            var lm = FindFirstObjectByType<LessonManager>();
            while (lm.Current == LessonManager.Phase.Loading) yield return null;
            lm.StartLesson(lessonIndex);
            float t0 = Time.time;
            int n = 0;
            while (lm.Current != LessonManager.Phase.LessonSummary && Time.time - t0 < 300f)
            {
                var word = lm.CurrentWord;
                float ws = Time.time;
                while (lm.Current == LessonManager.Phase.Demonstrate) yield return null;
                Log.AppendLine($"{++n}. {word.intent}: demo {Time.time - ws:0.0}s, {(lm.CurrentIsPracticeOnly ? "practice only" : "scored")}; tutor: {lm.Tutor.Current?.Reason}");
                yield return new WaitForSeconds(0.5f);
                if (!lm.CurrentIsPracticeOnly)
                {
                    // Second word: wrong first, then right on the second try (exercises feedback + Try Again).
                    if (n == 2)
                    {
                        Inject(lm, lm.CurrentTemplate.intent == "NO" ? "YES" : "NO");
                        yield return new WaitForSeconds(1.5f);
                        lm.CopyAgain();
                        yield return new WaitForSeconds(0.5f);
                    }
                    Inject(lm, word.intent);
                    yield return new WaitForSeconds(1.0f);
                }
                lm.Next();
                yield return null;
            }
            var score = FindFirstObjectByType<ScoreManager>();
            Log.AppendLine($"End: {lm.Current}, words {n}, score {score.Score}, errors {_errors}, time {Time.time - t0:0}s, XR {UnityEngine.XR.XRSettings.isDeviceActive}");
            System.IO.File.WriteAllText("Logs/v31-lesson-smoke.txt", Log.ToString());
            Done = true;
        }

        private static void Inject(LessonManager lm, string signed)
        {
            var target = lm.CurrentTemplate;
            var top1 = lm.Tutor != null ? TemplateStore.FromDefaults().Get(signed) : null;
            bool ok = signed == target.intent;
            var e = new SignEval
            {
                Template = target, Completed = ok, Performed = true, Total = ok ? 0.1f : 0.5f,
                CurlRight = (float[])target.curlRight.Clone(), CurlLeft = target.HasLeftCurl ? (float[])target.curlLeft.Clone() : new float[5],
                HasRight = true, HasLeft = target.TwoHanded, PalmRight = target.palm, Hint = ok ? "" : "Move it more",
            };
            var r = new RecognitionResult
            {
                Top1 = top1, Distance = e.Total, MovementCompleted = ok, WithinTolerance = ok, UserCurl = e.CurlRight,
                UserPalm = target.palm, UserCurlLeft = e.CurlLeft, All = new[] { e }, MovementError = e.Hint,
            };
            typeof(LessonManager).GetMethod("OnRecognized", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(lm, new object[] { r });
        }
    }
}
#endif
