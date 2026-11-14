using System;
using System.Collections;
using System.IO;
using SignSight.Hand;
using UnityEngine;
using UnityEngine.Networking;

namespace SignSight.Learn
{
    /// <summary>
    /// 5 lessons x 10 words (SPEC v3.1 §2.1), run through the main loop: teacher demonstrates -> you copy -> score +
    /// correct -> next word. The next word is chosen by the Tutor Agent (§4), not only in lesson order. All lessons are
    /// open from the menu; no gating. TEACH-ONLY words (scored = false) are demonstrated and practised, never scored.
    /// Loaded from StreamingAssets/lessons.json.
    /// </summary>
    public class LessonManager : MonoBehaviour
    {
        public const string FileName = "lessons.json";
        /// <summary>The v3.1 word list is exactly 50 words (5 x 10); anything else is a data error.</summary>
        public const int ExpectedWordCount = 50;

        public enum Phase { Loading, Menu, Demonstrate, Copy, Feedback, LessonSummary, Complete }

        /// <summary>cue: one-line movement cue for the word card (SPEC v3 §6), e.g. "Both fists — only the top hand cranks".</summary>
        [Serializable] public class Word { public string intent; public string meaning; public string cue; }
        [Serializable] public class Lesson { public int id; public string title; public Word[] words; }
        [Serializable] public class LessonFile { public Lesson[] lessons; }

        [SerializeField] private TeacherAvatarDriver teacher;
        [SerializeField] private SignRecognizer recognizer;
        [SerializeField] private TutorAgent tutor;

        public LessonFile Data { get; private set; }
        public Phase Current { get; private set; } = Phase.Loading;
        public int LessonIndex { get; private set; }
        /// <summary>Words shown so far in this lesson run (repeats and reviews included).</summary>
        public int WordIndex { get; private set; }
        /// <summary>True while practising the missed words (§7); practice is not scored.</summary>
        public bool IsPractice => _practice != null;
        public Lesson CurrentLesson => _practice ?? (Data != null && LessonIndex < Data.lessons.Length ? Data.lessons[LessonIndex] : null);
        public Word CurrentWord => _current;
        public SignTemplate CurrentTemplate => CurrentWord != null ? recognizer.Store.Get(CurrentWord.intent) : null;
        /// <summary>The current word is TEACH-ONLY: practise by watching, not scored, not counted toward stars.</summary>
        public bool CurrentIsPracticeOnly => CurrentTemplate != null && !CurrentTemplate.scored;
        public int TotalWords { get; private set; }
        /// <summary>1-based position of the current word in its lesson (by lesson order).</summary>
        public int OverallPosition { get; private set; }
        public RecognitionResult? LastAttempt { get; private set; }
        public TutorAgent Tutor => tutor;

        /// <summary>Teacher demo speed, 1 or 0.5 (learner-selectable; visual-only learning needs a slow view).</summary>
        public float DemoSpeed
        {
            get => teacher != null ? teacher.Speed : 1f;
            set { if (teacher != null) teacher.Speed = value; }
        }

        public event Action<Phase> PhaseChanged;
        public event Action<Word, RecognitionResult> Attempted;
        public event Action<Word> Skipped;
        public event Action<Word> WordStarted;

        private Lesson _practice;
        private int _practiceIndex;
        private Word _current;

        private IEnumerator Start()
        {
            if (teacher == null) teacher = FindFirstObjectByType<TeacherAvatarDriver>();
            if (recognizer == null) recognizer = FindFirstObjectByType<SignRecognizer>();
            if (tutor == null) tutor = FindFirstObjectByType<TutorAgent>() ?? gameObject.AddComponent<TutorAgent>();
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
            // SPEC v3.1 §2.1: assert the word count; never silently add or drop a word.
            Debug.Assert(TotalWords == ExpectedWordCount, $"[LessonManager] lessons.json has {TotalWords} words, expected {ExpectedWordCount}.");
            if (TotalWords != ExpectedWordCount)
                Debug.LogError($"[LessonManager] lessons.json has {TotalWords} words, expected {ExpectedWordCount}.");
            foreach (var l in Data.lessons)
                foreach (var w in l.words)
                    if (recognizer.Store.Get(w.intent) == null) Debug.LogError($"[LessonManager] No template for {w.intent}.");
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

        public void StartLesson(int index)
        {
            if (Data == null) return;
            _practice = null;
            LessonIndex = Mathf.Clamp(index, 0, Data.lessons.Length - 1);
            WordIndex = 0;
            tutor.BeginRun(Data.lessons[LessonIndex], Data);
            NextFromTutor();
        }

        public void StartPractice(Word[] words)
        {
            if (words == null || words.Length == 0) return;
            _practice = new Lesson { id = 0, title = "Practice", words = words };
            _practiceIndex = 0;
            WordIndex = 0;
            _current = words[0];
            BeginWord(true);
        }

        public void StartLesson1() => StartLesson(0);
        public void StartLesson2() => StartLesson(1);
        public void StartLesson3() => StartLesson(2);
        public void StartLesson4() => StartLesson(3);
        public void StartLesson5() => StartLesson(4);

        public void ToggleDemoSpeed() => DemoSpeed = DemoSpeed > 0.75f ? 0.5f : 1f;

        public void Replay()
        {
            if (CurrentWord == null || (Current != Phase.Demonstrate && Current != Phase.Copy && Current != Phase.Feedback)) return;
            BeginWord(false);
        }

        public void Next()
        {
            switch (Current)
            {
                case Phase.Copy:
                case Phase.Feedback:
                    if (Current == Phase.Copy)
                    {
                        if (CurrentIsPracticeOnly) tutor.RecordPractised(CurrentWord);
                        else Skipped?.Invoke(CurrentWord);
                    }
                    WordIndex++;
                    if (IsPractice)
                    {
                        _practiceIndex++;
                        if (_practiceIndex >= _practice.words.Length)
                        {
                            recognizer.Listening = false;
                            SetPhase(Phase.Complete);
                        }
                        else
                        {
                            _current = _practice.words[_practiceIndex];
                            BeginWord(true);
                        }
                    }
                    else NextFromTutor();
                    break;
                case Phase.LessonSummary:
                    if (LessonIndex + 1 < Data.lessons.Length) StartLesson(LessonIndex + 1);
                    else SetPhase(Phase.Complete);
                    break;
                case Phase.Complete:
                    _practice = null;
                    SetPhase(Phase.Menu);
                    break;
            }
        }

        public void CopyAgain()
        {
            if (CurrentWord == null) return;
            LastAttempt = null;
            recognizer.Listening = !CurrentIsPracticeOnly;
            SetPhase(Phase.Copy);
        }

        public void ShowFeedback()
        {
            recognizer.Listening = false;
            SetPhase(Phase.Feedback);
        }

        // ---- Flow ----

        private void NextFromTutor()
        {
            var d = tutor.NextWord();
            if (!d.HasValue)
            {
                recognizer.Listening = false;
                _current = null;
                SetPhase(Phase.LessonSummary);
                return;
            }
            _current = d.Value.Word;
            BeginWord(true);
        }

        private void BeginWord(bool newWord)
        {
            recognizer.Listening = false;
            LastAttempt = null;
            var lesson = CurrentLesson;
            OverallPosition = lesson != null ? Array.FindIndex(lesson.words, w => w.intent == _current.intent) + 1 : 0;
            if (newWord) WordStarted?.Invoke(CurrentWord);
            var t = CurrentTemplate;
            if (t == null)
            {
                Debug.LogError($"[LessonManager] No template for {CurrentWord?.intent}");
                return;
            }
            SetPhase(Phase.Demonstrate);
            // The teacher always shows the definition; recordings only tune recognition.
            teacher.Demonstrate(TemplateStore.Canonical(t.intent) ?? t);
        }

        private void OnDemonstrationFinished()
        {
            if (Current != Phase.Demonstrate) return;
            recognizer.Listening = !CurrentIsPracticeOnly;
            SetPhase(Phase.Copy);
        }

        private void OnRecognized(RecognitionResult r)
        {
            if (Current != Phase.Copy || CurrentIsPracticeOnly) return;
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
