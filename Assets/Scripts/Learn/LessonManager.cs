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
        /// <summary>The word list is exactly 62 words (v3.1: 5 x 10; v3.5: + Lesson 6 Café, 12 words); anything else is a data error.</summary>
        public const int ExpectedWordCount = 62;

        public enum Phase { Loading, Menu, Demonstrate, Copy, Feedback, LessonSummary, Complete }

        /// <summary>
        /// v3.3: which part of the app is open — separate from Phase. Start = the mode choice (entry point);
        /// Learn = lessons (Phase.Menu means "pick a lesson" here only); Explore = point at objects, no scoring;
        /// Relax (v3.4) = Finger Relax stretch break, no scoring; Exam (v3.5) = Café Final Exam, its own score.
        /// </summary>
        public enum Mode { Start, Learn, Explore, Relax, Exam }

        /// <summary>cue: one-line movement cue for the word card (SPEC v3 §6), e.g. "Both fists — only the top hand cranks".</summary>
        [Serializable] public class Word { public string intent; public string meaning; public string cue; }
        [Serializable] public class Lesson { public int id; public string title; public Word[] words; }
        [Serializable] public class LessonFile { public Lesson[] lessons; }

        [SerializeField] private TeacherAvatarDriver teacher;
        [SerializeField] private SignRecognizer recognizer;
        [SerializeField] private TutorAgent tutor;
        [SerializeField] private SignSight.Explore.ExploreMode explore;
        [SerializeField] private SignSight.Relax.RelaxMode relax;
        [SerializeField] private SignSight.Exam.ExamMode exam;

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
        public Mode CurrentMode { get; private set; } = Mode.Start;
        public SignSight.Explore.ExploreMode Explore => explore;
        public SignSight.Relax.RelaxMode Relax => relax;
        public SignSight.Exam.ExamMode Exam => exam;
        /// <summary>v3.4: lesson to start when an inter-lesson Relax is finished (-1 = the Complete screen); NoPending otherwise.</summary>
        public int PendingLessonAfterRelax => _pendingLessonAfterRelax;
        public const int NoPending = -2;
        private int _pendingLessonAfterRelax = NoPending;
        public event Action<Mode> ModeChanged;

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
            if (explore == null) explore = FindFirstObjectByType<SignSight.Explore.ExploreMode>();
            if (explore != null) explore.SetActive(false);
            if (relax == null) relax = FindFirstObjectByType<SignSight.Relax.RelaxMode>();
            if (relax != null)
            {
                relax.SetActive(false);
                relax.DoneRequested += OnRelaxDone;
            }
            if (exam == null) exam = FindFirstObjectByType<SignSight.Exam.ExamMode>();
            if (exam != null) exam.SetActive(false);
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
            CurrentMode = Mode.Start; // v3.3: the app opens on the mode choice, not the lesson list
            SetPhase(Phase.Menu);
        }

        // ---- v3.3 modes ----

        /// <summary>Back to the Start screen (MENU from the lesson list, from Explore, and at the end of a run).</summary>
        public void GoStart()
        {
            recognizer.Listening = false;
            _practice = null;
            _current = null;
            if (teacher != null && teacher.IsDemonstrating) teacher.Rest();
            SwitchMode(Mode.Start);
        }

        /// <summary>Start card 1: Learn &amp; Test — the lesson list.</summary>
        public void StartLearnMode() => SwitchMode(Mode.Learn);

        /// <summary>Start card 2: Explore — point at objects.</summary>
        public void StartExploreMode() => SwitchMode(Mode.Explore);

        /// <summary>Start card 3 (v3.4): Finger Relax on its own; finishing or MENU goes back to Start.</summary>
        public void StartRelaxMode()
        {
            _pendingLessonAfterRelax = NoPending;
            SwitchMode(Mode.Relax);
        }

        /// <summary>
        /// v3.4 Task B: RELAX on the lesson summary. Finishing Relax (DONE) starts the next lesson, or the Complete
        /// screen after the last lesson. MENU out of Relax goes to Start and starts nothing. Always the learner's choice.
        /// </summary>
        public void StartRelaxThenNextLesson()
        {
            if (CurrentMode != Mode.Learn || Current != Phase.LessonSummary || Data == null) return;
            _pendingLessonAfterRelax = LessonIndex + 1 < Data.lessons.Length ? LessonIndex + 1 : -1;
            // Keep the summary phase (not Menu): Phase.Menu resets score and streak, and Relax must change neither.
            SwitchMode(Mode.Relax, Phase.LessonSummary);
            if (relax != null) relax.ReturnToLesson = true;
        }

        /// <summary>Start card 4 (v3.5): the Café Final Exam. Open from Start — no gating.</summary>
        public void StartExamMode() => SwitchMode(Mode.Exam);

        /// <summary>
        /// v3.5: TAKE THE FINAL EXAM on the Lesson 6 summary. Keeps the summary phase (not Menu), so the lesson score
        /// and streak are not reset; the exam keeps its own total. MENU from the exam goes to Start.
        /// </summary>
        public void StartExamFromSummary()
        {
            if (CurrentMode != Mode.Learn || Current != Phase.LessonSummary) return;
            SwitchMode(Mode.Exam, Phase.LessonSummary);
        }

        private void OnRelaxDone()
        {
            int pending = _pendingLessonAfterRelax;
            bool back = relax != null && relax.ReturnToLesson && pending != NoPending;
            _pendingLessonAfterRelax = NoPending;
            if (!back)
            {
                GoStart(); // standalone Relax: back to the Start screen
                return;
            }
            // Back to Learn through the summary phase (not Menu), so the score carries on exactly as with NEXT LESSON.
            if (pending >= 0)
            {
                SwitchMode(Mode.Learn, Phase.LessonSummary);
                StartLesson(pending);
            }
            else SwitchMode(Mode.Learn, Phase.Complete);
        }

        private void SwitchMode(Mode m, Phase phase = Phase.Menu)
        {
            // The mode changes first: Explore / Relax notify the UI from SetActive, which must see the new mode.
            var previous = CurrentMode;
            CurrentMode = m;
            if (explore != null) explore.SetActive(m == Mode.Explore);
            if (m == Mode.Relax)
            {
                recognizer.Listening = false; // Relax has no sign recognition and no word attempts
                if (teacher != null && teacher.IsDemonstrating) teacher.Rest();
            }
            if (relax != null && (m == Mode.Relax || previous == Mode.Relax)) relax.SetActive(m == Mode.Relax);
            if (m == Mode.Exam)
            {
                recognizer.Listening = false; // the exam turns listening on for the learner's turns only
                if (teacher != null && teacher.IsDemonstrating) teacher.Rest();
            }
            if (exam != null && (m == Mode.Exam || previous == Mode.Exam)) exam.SetActive(m == Mode.Exam);
            if (m != Mode.Relax) _pendingLessonAfterRelax = NoPending;
            ModeChanged?.Invoke(m);
            SetPhase(phase);
        }

        private void OnDestroy()
        {
            if (recognizer != null) recognizer.Recognized -= OnRecognized;
            if (teacher != null) teacher.DemonstrationFinished -= OnDemonstrationFinished;
            if (relax != null) relax.DoneRequested -= OnRelaxDone;
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
            if (CurrentMode != Mode.Learn) SwitchMode(Mode.Learn);
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
        public void StartLesson6() => StartLesson(5);

        public void ToggleDemoSpeed() => DemoSpeed = DemoSpeed > 0.75f ? 0.5f : 1f;

        public void Replay()
        {
            if (CurrentMode == Mode.Explore)
            {
                explore?.Replay();
                return;
            }
            if (CurrentMode == Mode.Relax)
            {
                relax?.Done(); // the Replay slot is DONE on Relax's end screen
                return;
            }
            if (CurrentMode == Mode.Exam)
            {
                // the Replay slot: the clerk signs the question again; RETRY EXAM on the end screen
                if (exam != null && exam.Current == SignSight.Exam.ExamMode.State.Done) exam.Retry();
                else exam?.ReplayQuestion();
                return;
            }
            if (CurrentWord == null || (Current != Phase.Demonstrate && Current != Phase.Copy && Current != Phase.Feedback)) return;
            BeginWord(false);
        }

        public void Next()
        {
            // v3.3: Next is the MENU button outside a lesson: back to Start (v3.4: also out of Relax, which starts
            // no pending lesson).
            if (CurrentMode != Mode.Learn || Current == Phase.Menu)
            {
                GoStart();
                return;
            }
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
                    GoStart(); // v3.3: MENU at the end lands on Start
                    break;
            }
        }

        public void CopyAgain()
        {
            if (CurrentMode == Mode.Explore)
            {
                explore?.Back(); // the Try Again button is "BACK" in Explore
                return;
            }
            if (CurrentMode == Mode.Relax)
            {
                relax?.Skip(); // the Try Again button is "SKIP" in Relax
                return;
            }
            if (CurrentMode == Mode.Exam)
            {
                exam?.Hint(); // the Try Again button is "HINT" in the exam
                return;
            }
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
