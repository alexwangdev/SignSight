using System.Collections.Generic;
using SignSight.Learn;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

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

        [Header("Progress ring (SPEC v3 §6: blue ring fills over the 3 s attempt window)")]
        [SerializeField] private SignSight.Hand.SignRecognizer recognizer;
        [SerializeField] private UnityEngine.UI.Image holdRing;
        [SerializeField] private GameObject holdRingRoot;

        [Header("v3.1: Tutor panel (left of the word card) and demo speed toggle; created at runtime when empty")]
        [SerializeField] private TMP_Text tutorPanel;
        [SerializeField] private GameObject speedButton;
        // Left of the teacher, level with the word card on the right (same depth); speed toggle below it, outside the
        // button row so the row keeps its v3 width. Tuned in the Simulator view.
        private static readonly Vector3 TutorPanelPosition = new Vector3(-0.72f, 0.08f, 1.35f);
        private static readonly Vector3 SpeedButtonPosition = new Vector3(-0.30f, -0.05f, 0.60f);

        private readonly List<GameObject> _lessonButtons = new List<GameObject>();
        private TMP_Text _speedLabel;

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

        // ---------------- v3.1 runtime parts ----------------

        /// <summary>A copy of an existing pinch button with a new label and action (the scene file stays unchanged).</summary>
        private static GameObject CloneButton(GameObject src, string name, string label, UnityAction action)
        {
            var go = Instantiate(src, src.transform.parent);
            go.name = name;
            SetButton(go, label, action);
            return go;
        }

        private static void SetButton(GameObject go, string label, UnityAction action)
        {
            var wrap = go.GetComponentInChildren<Oculus.Interaction.InteractableUnityEventWrapper>(true);
            if (wrap != null && action != null)
            {
                var ev = wrap.WhenSelect;
                for (int i = 0; i < ev.GetPersistentEventCount(); i++) ev.SetPersistentListenerState(i, UnityEventCallState.Off);
                ev.RemoveAllListeners();
                ev.AddListener(action);
            }
            var tmp = go.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null && label != null) tmp.text = label;
        }

        private void EnsureParts()
        {
            if (_lessonButtons.Count == 0 && lessons.Data?.lessons != null && lesson1Button != null)
            {
                for (int i = 0; i < lessons.Data.lessons.Length; i++)
                {
                    int index = i;
                    var l = lessons.Data.lessons[i];
                    string topic = l.title.Contains("·") ? l.title.Substring(l.title.IndexOf('·') + 1).Trim() : l.title;
                    string label = $"LESSON {l.id}\n<size=55%>{topic}</size>";
                    GameObject b = i == 0 ? lesson1Button : i == 1 && lesson2Button != null ? lesson2Button
                        : CloneButton(lesson1Button, $"Lesson{l.id}Button", label, null);
                    SetButton(b, label, () => lessons.StartLesson(index));
                    _lessonButtons.Add(b);
                }
            }
            if (speedButton == null && replayButton != null)
                speedButton = CloneButton(replayButton, "SpeedButton", "", () => { lessons.ToggleDemoSpeed(); Refresh(); });
            if (speedButton != null && _speedLabel == null) _speedLabel = speedButton.GetComponentInChildren<TMP_Text>(true);
            if (tutorPanel == null && instruction != null)
            {
                tutorPanel = Instantiate(instruction, instruction.transform.parent);
                tutorPanel.name = "TutorPanel";
                tutorPanel.transform.localPosition = TutorPanelPosition;
                tutorPanel.alignment = TextAlignmentOptions.Left;
                tutorPanel.fontSize = instruction.fontSize * 1.25f; // twice as far away as the instruction line
                tutorPanel.rectTransform.sizeDelta = new Vector2(0.48f, 0.30f);
                tutorPanel.text = "";
            }
        }

        private string NonManualLine()
        {
            var t = lessons.CurrentTemplate;
            return t != null && !string.IsNullOrEmpty(t.nonManual) ? $"\n<color=#FFD27F>{t.nonManual}</color>" : "";
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
            EnsureParts();
            var lesson = lessons.CurrentLesson;
            var word = lessons.CurrentWord;
            var tutor = lessons.Tutor;

            // Top bar (hidden during feedback: the feedback column shows the points and sits in front of it)
            if (topBar != null) topBar.gameObject.SetActive(phase != LessonManager.Phase.Feedback);
            if (topBar != null)
            {
                int n = lesson != null ? lesson.words.Length : 0;
                int pos = !lessons.IsPractice && tutor != null ? Mathf.Clamp(tutor.DoneThisRun, 1, n) : Mathf.Min(lessons.WordIndex + 1, n);
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
                    wordCard?.Show($"Learn {lessons.TotalWords} signs", "Pick a lesson");
                    SetInstruction("Pinch a lesson to start");
                    break;
                case LessonManager.Phase.Demonstrate:
                    wordCard?.Show(word.intent, word.meaning, word.cue + NonManualLine());
                    SetInstruction($"Watch the teacher{(lessons.DemoSpeed < 0.75f ? " (half speed)" : "")}");
                    break;
                case LessonManager.Phase.Copy:
                    wordCard?.Show(word.intent, word.meaning, word.cue + NonManualLine());
                    SetInstruction(lessons.CurrentIsPracticeOnly
                        ? "<color=#FFD27F>Practice only — not scored</color>\n<size=75%>Copy the teacher, then pinch Next</size>"
                        : lessons.LastAttempt.HasValue
                            ? $"You signed <b>{lessons.LastAttempt.Value.Top1.intent}</b>"
                            : "Your turn — copy the sign");
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
            if (_lessonButtons.Count > 0) foreach (var b in _lessonButtons) Show(b, phase == LessonManager.Phase.Menu);
            else
            {
                Show(lesson1Button, phase == LessonManager.Phase.Menu);
                Show(lesson2Button, phase == LessonManager.Phase.Menu);
            }
            Show(replayButton, phase == LessonManager.Phase.Demonstrate || phase == LessonManager.Phase.Copy);
            // Speed toggle sits on its own, left of the button row (under the Tutor panel), so the row keeps its v3 width.
            if (speedButton != null)
            {
                speedButton.SetActive(phase == LessonManager.Phase.Demonstrate || phase == LessonManager.Phase.Copy);
                speedButton.transform.localPosition = SpeedButtonPosition;
            }
            if (_speedLabel != null) _speedLabel.text = lessons.DemoSpeed < 0.75f ? "SPEED\n0.5x" : "SPEED\n1x";

            // Tutor panel: the agent's decision and its reason, in plain text (SPEC v3.1 §4.1).
            if (tutorPanel != null)
            {
                bool showTutor = !lessons.IsPractice && tutor != null &&
                                 (phase == LessonManager.Phase.Demonstrate || phase == LessonManager.Phase.Copy || phase == LessonManager.Phase.Feedback);
                tutorPanel.gameObject.SetActive(showTutor);
                if (showTutor) tutorPanel.text = tutor.PanelText();
            }
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
