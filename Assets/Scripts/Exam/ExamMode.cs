using System;
using System.Collections;
using System.Collections.Generic;
using SignSight.Hand;
using SignSight.Learn;
using UnityEngine;

namespace SignSight.Exam
{
    /// <summary>
    /// Café Final Exam (v3.5): a fixed 7-turn script, no free chat, no LLM. The teacher is the clerk behind a counter;
    /// he signs each question (TeacherAvatarDriver.Demonstrate, the same call as Learn) and the word card shows the
    /// question as text. The learner answers by signing; the answer is checked with the lesson rule
    /// (ScoreManager.IsCorrect) against the turn's SET of valid answers. Per turn: 2 attempts, +10 first try, +5 second
    /// try, 0 after two wrong tries or 10 s without a recognition (then the clerk shows a valid answer once). HINT shows
    /// the valid answers; a hinted turn scores at most +5 (SecondTryPoints), even when the next attempt is right.
    /// The exam keeps its own total and stars (ScoreManager.Stars on that total); it never writes to the lesson score,
    /// the streak, the LearnerProfile or the Tutor. Hands only: the learner's face is never read.
    /// The café is props in the learner's room (passthrough stays on): counter, menu board, cup — scene objects built by
    /// SignSightModesSetup (primitives), shown only in Exam mode. The teacher is moved behind the counter while the
    /// exam runs and put back on exit (the same avatar, never duplicated).
    /// </summary>
    public class ExamMode : MonoBehaviour
    {
        public const float AnswerTimeoutSeconds = 10f;  // no recognition for this long -> 0 points, the clerk shows the sign
        public const float FeedbackSeconds = 1.6f;      // how long "Right!" / "Looks more like X" stays before moving on
        public const int MaxPoints = 70;

        [Serializable]
        public class Turn
        {
            public string question;      // the clerk's line, as text on the word card (visual-only)
            public string[] clerkSigns;  // what the clerk signs first (receptive), in order; empty = he waits
            public string prompt;        // what the learner should do — never the answer word
            public string[] expected;    // valid answers (a choice turn has more than one)
        }

        /// <summary>The fixed script (7 scored learner turns). TEA was dropped from Lesson 6 (Lifeprint text gives no movement), so the drinks are COFFEE / MILK / WATER.</summary>
        public static readonly Turn[] Script =
        {
            new Turn { question = "Hello! Good morning!", clerkSigns = new[] { "HELLO", "GOOD-MORNING" }, prompt = "Greet the clerk", expected = new[] { "HELLO" } },
            new Turn { question = "What do you want?", clerkSigns = new[] { "WHAT", "WANT" }, prompt = "Tell him you would like to order", expected = new[] { "WANT" } },
            new Turn { question = "(The clerk waits — look at the menu)", clerkSigns = new string[0], prompt = "Order a drink from the menu", expected = new[] { "COFFEE", "MILK", "WATER" } },
            new Turn { question = "(The clerk nods)", clerkSigns = new string[0], prompt = "Be polite", expected = new[] { "PLEASE" } },
            new Turn { question = "Hot? Cold?", clerkSigns = new[] { "HOT", "COLD" }, prompt = "Pick one", expected = new[] { "HOT", "COLD" } },
            new Turn { question = "One? Two?", clerkSigns = new[] { "ONE", "TWO" }, prompt = "How many?", expected = new[] { "ONE", "TWO", "THREE" } },
            new Turn { question = "Here you go. Thank you!", clerkSigns = new[] { "THANK-YOU" }, prompt = "Say it back", expected = new[] { "THANK-YOU" } },
        };
        public const string MenuItems = "COFFEE · MILK · WATER";
        public const string ClosingSign = "FINISH";

        public enum State { Off, ClerkSigning, Answer, Feedback, ClerkShows, Closing, Done }
        public enum Result { None, FirstTry, SecondTry, Missed }

        public class TurnRecord
        {
            public Result Result;
            public int Points;
            public bool Hinted;
            public string Signed;      // the accepted answer (or the last attempt's top guess)
            public string Shown;       // the sign the clerk showed after a missed turn
            public bool TimedOut;
        }

        [SerializeField] private LessonManager lessons;
        [SerializeField] private TeacherAvatarDriver teacher;
        [SerializeField] private SignRecognizer recognizer;
        [Tooltip("Counter, menu board and cup (scene objects, inactive outside the exam).")]
        [SerializeField] private GameObject cafeRoot;
        [SerializeField] private GameObject cup;

        /// <summary>Teacher position while the exam runs (LearnUI local): behind the counter.</summary>
        public static readonly Vector3 TeacherCafePosition = new Vector3(-0.30f, -1.67f, 1.85f); // v3.9: feet behind the counter (z > 1.375)

        public State Current { get; private set; } = State.Off;
        public int TurnIndex { get; private set; }
        public Turn CurrentTurn => TurnIndex >= 0 && TurnIndex < Script.Length ? Script[TurnIndex] : null;
        public int Attempts { get; private set; }
        public bool Hinted { get; private set; }
        public int Total { get; private set; }
        public int Stars => ScoreManager.Stars(Total);
        public string Feedback { get; private set; } = "";
        public bool LastWasCorrect { get; private set; }
        public readonly List<TurnRecord> Records = new List<TurnRecord>();
        /// <summary>0..1 toward the answer timeout (for tests / the UI).</summary>
        public float AnswerTime => _answerT;
        public event Action Changed;

        private readonly Queue<string> _queue = new Queue<string>();
        private float _answerT, _stateT;
        private Transform _teacherT;
        private Vector3 _teacherHome;
        private Quaternion _teacherHomeRot;
        private bool _moved;

        private void Awake()
        {
            if (lessons == null) lessons = FindFirstObjectByType<LessonManager>();
            if (teacher == null) teacher = FindFirstObjectByType<TeacherAvatarDriver>();
            if (recognizer == null) recognizer = FindFirstObjectByType<SignRecognizer>();
            if (cafeRoot == null)
            {
                var c = GameObject.Find("CafeProps");
                if (c != null) cafeRoot = c;
            }
        }

        private void OnEnable()
        {
            if (teacher != null) teacher.DemonstrationFinished += OnDemoFinished;
            if (recognizer != null) recognizer.Recognized += OnRecognized;
        }

        private void OnDisable()
        {
            if (teacher != null) teacher.DemonstrationFinished -= OnDemoFinished;
            if (recognizer != null) recognizer.Recognized -= OnRecognized;
        }

        private bool InExam => lessons != null && lessons.CurrentMode == LessonManager.Mode.Exam;

        /// <summary>Entering / leaving the exam (LessonManager calls this).</summary>
        public void SetActive(bool on)
        {
            if (cafeRoot != null)
            {
                cafeRoot.SetActive(on);
                // The menu text sits on the menu board (set here: the saved scene drops a RectTransform's x / y
                // under the plain CafeProps parent).
                var board = cafeRoot.transform.Find("MenuBoard");
                var text = cafeRoot.transform.Find("MenuText");
                if (on && board != null && text != null) text.localPosition = board.localPosition + new Vector3(0f, 0f, -0.003f);
            }
            MoveTeacher(on);
            if (on) Begin();
            else
            {
                Current = State.Off;
                _queue.Clear();
                if (recognizer != null) recognizer.Listening = false;
                if (teacher != null && teacher.IsDemonstrating) teacher.Rest();
            }
            Changed?.Invoke();
        }

        private void MoveTeacher(bool cafe)
        {
            if (teacher == null) return;
            if (_teacherT == null) _teacherT = teacher.transform;
            if (cafe && !_moved)
            {
                _teacherHome = _teacherT.localPosition;
                _teacherHomeRot = _teacherT.localRotation;
                _teacherT.localPosition = TeacherCafePosition;
                _moved = true;
            }
            else if (!cafe && _moved)
            {
                _teacherT.localPosition = _teacherHome;
                _teacherT.localRotation = _teacherHomeRot;
                _moved = false;
            }
        }

        /// <summary>Start (or RETRY) the 7 turns with a fresh total.</summary>
        public void Begin()
        {
            Records.Clear();
            for (int i = 0; i < Script.Length; i++) Records.Add(new TurnRecord());
            Total = 0;
            if (cup != null) cup.SetActive(false);
            if (teacher != null && teacher.IsDemonstrating) teacher.Rest();
            StartTurn(0);
        }

        private void StartTurn(int i)
        {
            TurnIndex = i;
            Attempts = 0;
            Hinted = false;
            Feedback = "";
            _queue.Clear();
            foreach (var s in Script[i].clerkSigns) _queue.Enqueue(s);
            if (recognizer != null) recognizer.Listening = false;
            Current = State.ClerkSigning;
            Changed?.Invoke();
            if (!PlayNext()) BeginAnswer();
        }

        /// <summary>Plays the next queued clerk sign; false when the queue is empty.</summary>
        private bool PlayNext()
        {
            while (_queue.Count > 0)
            {
                var t = TemplateStore.Canonical(_queue.Dequeue());
                if (t == null) continue;
                teacher.Demonstrate(t);
                return true;
            }
            return false;
        }

        private void BeginAnswer()
        {
            Current = State.Answer;
            _answerT = 0f;
            if (recognizer != null) recognizer.Listening = true;
            Changed?.Invoke();
        }

        private void OnDemoFinished()
        {
            if (!InExam) return;
            if (PlayNext()) return;
            switch (Current)
            {
                case State.ClerkSigning: BeginAnswer(); break;
                case State.ClerkShows: NextTurn(); break;
                case State.Closing:
                    Current = State.Done;
                    Changed?.Invoke();
                    break;
            }
        }

        private void Update()
        {
            if (!InExam) return;
            if (Current == State.Answer)
            {
                _answerT += Time.deltaTime;
                if (_answerT >= AnswerTimeoutSeconds) Miss(timedOut: true);
            }
            else if (Current == State.Feedback)
            {
                _stateT += Time.deltaTime;
                if (_stateT >= FeedbackSeconds)
                {
                    if (LastWasCorrect) NextTurn();
                    else BeginAnswer(); // second try
                }
            }
        }

        /// <summary>True if the attempt is a correct sign for any word in the set (the lesson rule, per word).</summary>
        public static bool Matches(Turn turn, RecognitionResult r, out string which)
        {
            foreach (var e in turn.expected)
            {
                if (ScoreManager.IsCorrect(new LessonManager.Word { intent = e }, r))
                {
                    which = e;
                    return true;
                }
            }
            which = null;
            return false;
        }

        private void OnRecognized(RecognitionResult r) => Attempt(r);

        /// <summary>One learner attempt (from the recognizer; tests may call it with a result).</summary>
        public void Attempt(RecognitionResult r)
        {
            if (!InExam || Current != State.Answer) return;
            Attempts++;
            var rec = Records[TurnIndex];
            if (Matches(CurrentTurn, r, out var which))
            {
                // HINT rule: a hinted turn scores at most SecondTryPoints, even if the first attempt is right.
                int pts = Attempts == 1 && !Hinted ? ScoreManager.FirstTryPoints : ScoreManager.SecondTryPoints;
                rec.Result = Attempts == 1 ? Result.FirstTry : Result.SecondTry;
                rec.Points = pts;
                rec.Hinted = Hinted;
                rec.Signed = which;
                Total += pts;
                LastWasCorrect = true;
                Feedback = $"Right — {which}  +{pts}";
                if (TurnIndex == 2 && cup != null) cup.SetActive(true); // the ordered drink arrives
                ToFeedback();
                return;
            }
            rec.Signed = r.Top1 != null ? r.Top1.intent : "";
            if (Attempts >= ScoreManager.MaxAttempts)
            {
                Miss(timedOut: false);
                return;
            }
            LastWasCorrect = false;
            // Same wording as the lesson feedback panel's "looks like" line.
            Feedback = r.Top1 != null ? $"Looks more like {r.Top1.intent} — try again" : "Not recognised — try again";
            ToFeedback();
        }

        private void ToFeedback()
        {
            if (recognizer != null) recognizer.Listening = false;
            Current = State.Feedback;
            _stateT = 0f;
            Changed?.Invoke();
        }

        /// <summary>Second wrong try or timeout: 0 points, the clerk shows a valid answer once, then the next turn.</summary>
        private void Miss(bool timedOut)
        {
            if (recognizer != null) recognizer.Listening = false;
            var rec = Records[TurnIndex];
            rec.Result = Result.Missed;
            rec.Points = 0;
            rec.Hinted = Hinted;
            rec.TimedOut = timedOut;
            rec.Shown = CurrentTurn.expected[0];
            LastWasCorrect = false;
            Feedback = $"{(timedOut ? "Time is up" : "Not this time")} — the clerk shows {rec.Shown}";
            if (TurnIndex == 2 && cup != null) cup.SetActive(true);
            Current = State.ClerkShows;
            Changed?.Invoke();
            var t = TemplateStore.Canonical(rec.Shown);
            if (t != null) teacher.Demonstrate(t);
            else NextTurn();
        }

        private void NextTurn()
        {
            if (TurnIndex + 1 < Script.Length)
            {
                StartTurn(TurnIndex + 1);
                return;
            }
            // The clerk closes with FINISH (receptive only), then the end screen.
            if (recognizer != null) recognizer.Listening = false;
            Current = State.Closing;
            Feedback = "";
            Changed?.Invoke();
            var f = TemplateStore.Canonical(ClosingSign);
            if (f != null) teacher.Demonstrate(f);
            else
            {
                Current = State.Done;
                Changed?.Invoke();
            }
        }

        /// <summary>HINT: shows the valid answers; this turn now scores at most +5.</summary>
        public void Hint()
        {
            if (!InExam || (Current != State.Answer && Current != State.Feedback) || Hinted) return;
            Hinted = true;
            Changed?.Invoke();
        }

        /// <summary>REPLAY: the clerk signs the question again (the answer timer starts over).</summary>
        public void ReplayQuestion()
        {
            if (!InExam || Current != State.Answer || CurrentTurn.clerkSigns.Length == 0) return;
            StartTurnReplay();
        }

        private void StartTurnReplay()
        {
            int attempts = Attempts;
            bool hinted = Hinted;
            StartTurn(TurnIndex);
            Attempts = attempts;
            Hinted = hinted;
        }

        /// <summary>RETRY EXAM on the end screen.</summary>
        public void Retry()
        {
            if (!InExam || Current != State.Done) return;
            Begin();
        }

        /// <summary>One line per turn for the end screen.</summary>
        public string BreakdownLine(int i)
        {
            var t = Script[i];
            var r = Records[i];
            string set = string.Join("/", t.expected);
            string chose = t.expected.Length > 1 && !string.IsNullOrEmpty(r.Signed) ? $" {r.Signed}" : "";
            string res;
            switch (r.Result)
            {
                case Result.FirstTry: res = $"<color=#7CE38B>1st try</color>{chose} +{r.Points}{(r.Hinted ? " (hint)" : "")}"; break;
                case Result.SecondTry: res = $"<color=#FFD27F>2nd try</color>{chose} +{r.Points}{(r.Hinted ? " (hint)" : "")}"; break;
                case Result.Missed: res = $"<color=#FF9A8A>missed</color>{(r.TimedOut ? " (time)" : "")} · clerk showed {r.Shown}"; break;
                default: res = "—"; break;
            }
            return $"{i + 1}. {set}: {res}";
        }
    }
}
