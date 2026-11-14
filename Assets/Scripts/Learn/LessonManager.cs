using System;
using System.Collections;
using System.IO;
using SignSight.Hand;
using UnityEngine;
using UnityEngine.Networking;

namespace SignSight.Learn
{
    /// <summary>
    /// 2 lessons, 8 words (SPEC §2.2), run through the main loop (§2.3): teacher demonstrates → you copy →
    /// (score + correct, P3) → next word. Both lessons are open from the menu; no gating.
    /// Loaded from StreamingAssets/lessons.json.
    /// </summary>
    public class LessonManager : MonoBehaviour
    {
        public const string FileName = "lessons.json";

        public enum Phase { Loading, Menu, Demonstrate, Copy, Feedback, LessonSummary, Complete }

        /// <summary>cue: one-line movement cue for the word card (SPEC v3 §6), e.g. "Both fists — only the top hand cranks".</summary>
        [Serializable] public class Word { public string intent; public string meaning; public string cue; }
        [Serializable] public class Lesson { public int id; public string title; public Word[] words; }
        [Serializable] public class LessonFile { public Lesson[] lessons; }

        [SerializeField] private TeacherAvatarDriver teacher;
        [SerializeField] private SignRecognizer recognizer;

        public LessonFile Data { get; private set; }
        public Phase Current { get; private set; } = Phase.Loading;
        public int LessonIndex { get; private set; }
        public int WordIndex { get; private set; }
        /// <summary>True while practising the missed words (§7); practice is not scored.</summary>
        public bool IsPractice => _practice != null;
        public Lesson CurrentLesson => _practice ?? (Data != null && LessonIndex < Data.lessons.Length ? Data.lessons[LessonIndex] : null);
        public Word CurrentWord => CurrentLesson != null && WordIndex < CurrentLesson.words.Length ? CurrentLesson.words[WordIndex] : null;
        public SignTemplate CurrentTemplate => CurrentWord != null ? recognizer.Store.Get(CurrentWord.intent) : null;
        public int TotalWords { get; private set; }
        /// <summary>1-based position of the current word across all lessons (e.g. 6 of 8).</summary>
        public int OverallPosition { get; private set; }
        /// <summary>The learner's last held sign while copying (null until one arrives).</summary>
        public RecognitionResult? LastAttempt { get; private set; }

        public event Action<Phase> PhaseChanged;
        /// <summary>A held sign arrived during the copy step. ScoreManager decides correct / wrong.</summary>
        public event Action<Word, RecognitionResult> Attempted;
        /// <summary>Next was pressed while copying, before any scored attempt.</summary>
        public event Action<Word> Skipped;
        /// <summary>A new word started (not raised by Replay).</summary>
        public event Action<Word> WordStarted;

        private Lesson _practice;

        private IEnumerator Start()
        {
            if (teacher == null) teacher = FindFirstObjectByType<TeacherAvatarDriver>();
            if (recognizer == null) recognizer = FindFirstObjectByType<SignRecognizer>();
            recognizer.Listening = false;
            recognizer.Recognized += OnRecognized;
            teacher.DemonstrationFinished += OnDemonstrationFinished;
            yield return Load();
            if (Data?.lessons == null || Data.lessons.Length == 0)
            {
                Debug.LogError("[LessonManager] lessons.json missing or empty.");
                yield break;
            }
            TotalWords = 0;
            foreach (var l in Data.lessons) TotalWords += l.words.Length;
            SetPhase(Phase.Menu);
        }

        private void OnDestroy()
        {
            if (recognizer != null) recognizer.Recognized -= OnRecognized;
            if (teacher != null) teacher.DemonstrationFinished -= OnDemonstrationFinished;
        }

        private IEnumerator Load()
        {
            string path = Path.Combine(Application.streamingAssetsPath, FileName);
            string json = null;
            if (path.Contains("://"))
            {
                using var req = UnityWebRequest.Get(path);
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success) json = req.downloadHandler.text;
            }
            else if (File.Exists(path))
            {
                json = File.ReadAllText(path);
            }
            if (json == null) yield break;
            try { Data = JsonUtility.FromJson<LessonFile>(json); }
            catch (Exception e) { Debug.LogError("[LessonManager] Bad lessons.json: " + e.Message); }
        }

        // ---- Buttons ----

        /// <summary>Menu: start a lesson (0 = Lesson 1, 1 = Lesson 2). Both are always open.</summary>
        public void StartLesson(int index)
        {
            if (Data == null) return;
            _practice = null;
            LessonIndex = Mathf.Clamp(index, 0, Data.lessons.Length - 1);
            WordIndex = 0;
            BeginWord(true);
        }

        /// <summary>Practise only the given words (the completion screen's "Practice the words you missed").</summary>
        public void StartPractice(Word[] words)
        {
            if (words == null || words.Length == 0) return;
            _practice = new Lesson { id = 0, title = "Practice", words = words };
            WordIndex = 0;
            BeginWord(true);
        }

        public void StartLesson1() => StartLesson(0);
        public void StartLesson2() => StartLesson(1);

        /// <summary>Watch the teacher demonstrate the current word again.</summary>
        public void Replay()
        {
            if (CurrentWord == null || (Current != Phase.Demonstrate && Current != Phase.Copy && Current != Phase.Feedback)) return;
            BeginWord(false);
        }

        /// <summary>Next word (or the lesson summary / next lesson / menu, depending on where we are).</summary>
        public void Next()
        {
            switch (Current)
            {
                case Phase.Copy:
                case Phase.Feedback:
                    if (Current == Phase.Copy) Skipped?.Invoke(CurrentWord);
                    WordIndex++;
                    if (WordIndex >= CurrentLesson.words.Length)
                    {
                        recognizer.Listening = false;
                        SetPhase(IsPractice ? Phase.Complete : Phase.LessonSummary);
                    }
                    else BeginWord(true);
                    break;
                case Phase.LessonSummary:
                    if (LessonIndex + 1 < Data.lessons.Length)
                    {
                        LessonIndex++;
                        WordIndex = 0;
                        BeginWord(true);
                    }
                    else SetPhase(Phase.Complete);
                    break;
                case Phase.Complete:
                    _practice = null;
                    SetPhase(Phase.Menu);
                    break;
            }
        }

        /// <summary>Copy the same word again (used by Try Again in P3).</summary>
        public void CopyAgain()
        {
            if (CurrentWord == null) return;
            LastAttempt = null;
            recognizer.Listening = true;
            SetPhase(Phase.Copy);
        }

        /// <summary>P3: scoring is done for this attempt; stop listening and show feedback.</summary>
        public void ShowFeedback()
        {
            recognizer.Listening = false;
            SetPhase(Phase.Feedback);
        }

        // ---- Flow ----

        private void BeginWord(bool newWord)
        {
            recognizer.Listening = false;
            LastAttempt = null;
            OverallPosition = 1 + WordIndex;
            if (!IsPractice) for (int i = 0; i < LessonIndex; i++) OverallPosition += Data.lessons[i].words.Length;
            if (newWord) WordStarted?.Invoke(CurrentWord);
            var t = CurrentTemplate;
            if (t == null)
            {
                Debug.LogError($"[LessonManager] No template for {CurrentWord?.intent}");
                return;
            }
            SetPhase(Phase.Demonstrate);
            // The teacher always shows the §4.3 definition; recordings only tune recognition.
            teacher.Demonstrate(TemplateStore.Canonical(t.intent) ?? t);
        }

        private void OnDemonstrationFinished()
        {
            if (Current != Phase.Demonstrate) return;
            recognizer.Listening = true;
            SetPhase(Phase.Copy);
        }

        private void OnRecognized(RecognitionResult r)
        {
            if (Current != Phase.Copy) return;
            LastAttempt = r;
            Attempted?.Invoke(CurrentWord, r);
            PhaseChanged?.Invoke(Current); // refresh the UI with the attempt
        }

        private void SetPhase(Phase p)
        {
            Current = p;
            PhaseChanged?.Invoke(p);
        }
    }
}
