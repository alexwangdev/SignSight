using System.Collections.Generic;
using SignSight.Learn;
using TMPro;
using UnityEngine;

namespace SignSight.UI
{
    /// <summary>
    /// Lesson screen (SPEC §6): top bar (lesson progress, total progress, score, streak), the word card, a one-line
    /// instruction, and pinch buttons that change with the step: Menu → Lesson 1 / Lesson 2; demonstration → Replay;
    /// copying → Replay / Next; after scoring → Try Again / Next.
    /// </summary>
    public class LessonUI : MonoBehaviour
    {
        [SerializeField] private LessonManager lessons;
        [SerializeField] private WordCard wordCard;
        [SerializeField] private TMP_Text topBar;
        [SerializeField] private TMP_Text instruction;

        [Header("Buttons (laid out left to right, lower centre)")]
        [SerializeField] private GameObject lesson1Button;
        [SerializeField] private GameObject lesson2Button;
        [SerializeField] private GameObject replayButton;
        [SerializeField] private GameObject tryAgainButton;
        [SerializeField] private GameObject nextButton;
        [SerializeField] private TMP_Text nextLabel;
        [Tooltip("Completion screen: practise the words that were not right on the first try.")]
        [SerializeField] private GameObject practiceButton;
        [Tooltip("Centre-to-centre spacing of visible buttons (m).")]
        [SerializeField] private float buttonSpacing = 0.14f;

        [SerializeField] private ScoreManager score;

        [Header("Hold ring (SPEC §6: blue ring fills over 0.6 s while holding)")]
        [SerializeField] private SignSight.Hand.SignRecognizer recognizer;
        [SerializeField] private UnityEngine.UI.Image holdRing;
        [SerializeField] private GameObject holdRingRoot;

        private void Update()
        {
            if (holdRing == null) return;
            bool copying = lessons.Current == LessonManager.Phase.Copy;
            float p = copying && recognizer != null ? recognizer.HoldProgress : 0f;
            if (holdRingRoot != null && holdRingRoot.activeSelf != copying) holdRingRoot.SetActive(copying);
            holdRing.fillAmount = p;
        }

        private Vector3 _buttonRowCentre;
        private bool _rowCaptured;

        private void OnEnable()
        {
            if (lessons == null) lessons = FindFirstObjectByType<LessonManager>();
            if (score == null) score = FindFirstObjectByType<ScoreManager>();
            lessons.PhaseChanged += Refresh;
            if (score != null) score.Changed += Refresh;
            Refresh(lessons.Current);
        }

        private void OnDisable()
        {
            if (lessons != null) lessons.PhaseChanged -= Refresh;
            if (score != null) score.Changed -= Refresh;
        }

        public void Refresh() => Refresh(lessons.Current);

        /// <summary>Completion screen button: practise the missed words (§7).</summary>
        public void PracticeMissed()
        {
            if (score == null || score.Missed.Count == 0) return;
            var words = new LessonManager.Word[score.Missed.Count];
            for (int i = 0; i < words.Length; i++) words[i] = score.Missed[i];
            lessons.StartPractice(words);
        }

        private static string StarText(int stars) =>
            $"<color=#FFD24A>{new string('★', stars)}</color><color=#555555>{new string('★', 3 - stars)}</color>";

        private void Refresh(LessonManager.Phase phase)
        {
            var lesson = lessons.CurrentLesson;
            var word = lessons.CurrentWord;

            // Top bar
            if (topBar != null)
            {
                int n = lesson != null ? lesson.words.Length : 0;
                int pos = Mathf.Min(lessons.WordIndex + 1, n);
                string progress = phase == LessonManager.Phase.Menu || phase == LessonManager.Phase.Loading || lesson == null
                    ? "SignSight Learn"
                    : lessons.IsPractice
                        ? $"Practice · {pos} / {n}"
                        : $"Lesson {lesson.id} · {pos} / {n}    <color=#9DB7FF>{lessons.OverallPosition} / {lessons.TotalWords}</color>";
                int s = score != null ? score.Score : 0, st = score != null ? score.Streak : 0;
                topBar.text = $"{progress}    Score {s}    Streak ×{st}";
            }

            // Word card + instruction
            switch (phase)
            {
                case LessonManager.Phase.Loading:
                    wordCard?.Hide();
                    SetInstruction("Loading lessons...");
                    break;
                case LessonManager.Phase.Menu:
                    wordCard?.Show("Learn 8 signs", "Pick a lesson");
                    SetInstruction("Pinch a lesson to start");
                    break;
                case LessonManager.Phase.Demonstrate:
                    wordCard?.Show(word.intent, word.meaning);
                    SetInstruction("Watch the teacher");
                    break;
                case LessonManager.Phase.Copy:
                    wordCard?.Show(word.intent, word.meaning);
                    SetInstruction(lessons.LastAttempt.HasValue
                        ? $"You signed <b>{lessons.LastAttempt.Value.Top1.intent}</b>"
                        : "Your turn: copy the sign and hold it");
                    break;
                case LessonManager.Phase.Feedback:
                    wordCard?.Hide(); // the comparison hands use the space; the word is in the feedback title
                    SetInstruction("");
                    break;
                case LessonManager.Phase.LessonSummary:
                    wordCard?.Show($"{lesson.title} done", $"Score {(score != null ? score.Score : 0)}");
                    SetInstruction("");
                    break;
                case LessonManager.Phase.Complete:
                    if (lessons.IsPractice)
                    {
                        wordCard?.Show("Practice done", "Nice work");
                    }
                    else
                    {
                        int total = score != null ? score.Score : 0;
                        wordCard?.Show($"{StarText(ScoreManager.Stars(total))}", $"Total {total}");
                    }
                    SetInstruction("");
                    break;
            }

            // Buttons
            var visible = new List<GameObject>();
            void Show(GameObject b, bool on) { if (b == null) return; b.SetActive(on); if (on) visible.Add(b); }
            Show(lesson1Button, phase == LessonManager.Phase.Menu);
            Show(lesson2Button, phase == LessonManager.Phase.Menu);
            Show(replayButton, phase == LessonManager.Phase.Demonstrate || phase == LessonManager.Phase.Copy);
            Show(tryAgainButton, phase == LessonManager.Phase.Feedback && score != null && score.CanTryAgain);
            Show(practiceButton, phase == LessonManager.Phase.Complete && !lessons.IsPractice && score != null && score.Missed.Count > 0);
            bool next = phase == LessonManager.Phase.Copy || phase == LessonManager.Phase.Feedback ||
                        phase == LessonManager.Phase.LessonSummary || phase == LessonManager.Phase.Complete;
            Show(nextButton, next);
            if (nextLabel != null)
            {
                nextLabel.text = phase == LessonManager.Phase.LessonSummary
                    ? (lessons.LessonIndex + 1 < lessons.Data.lessons.Length ? "NEXT\nLESSON" : "FINISH")
                    : phase == LessonManager.Phase.Complete ? "MENU" : "NEXT";
            }
            LayOut(visible);
        }

        public void SetInstruction(string text)
        {
            if (instruction != null) instruction.text = text;
        }

        private void LayOut(List<GameObject> visible)
        {
            // Each button reference is a slot holding the pinchable disc and its label.
            if (!_rowCaptured && nextButton != null)
            {
                _buttonRowCentre = nextButton.transform.localPosition;
                _rowCaptured = true;
            }
            float start = -(visible.Count - 1) * buttonSpacing * 0.5f;
            for (int i = 0; i < visible.Count; i++)
            {
                var p = _buttonRowCentre;
                p.x = start + i * buttonSpacing;
                visible[i].transform.localPosition = p;
            }
        }
    }
}
