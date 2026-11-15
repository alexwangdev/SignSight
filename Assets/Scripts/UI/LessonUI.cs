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

        [Header("v3.2: Tutor panel (scene object, left of the teacher), speed toggle and FACE replay buttons")]
        [SerializeField] private GameObject tutorRoot;
        [SerializeField] private TMP_Text tutorPanel;
        [SerializeField] private GameObject speedButton;
        [SerializeField] private GameObject faceButton;
        [SerializeField] private TeacherAvatarDriver teacher;
        // Speed toggle sits on its own, left of the button row; FACE sits right of it. Tuned in the Simulator view.
        private static readonly Vector3 SpeedButtonPosition = new Vector3(-0.30f, -0.12f, 0.60f);
        private static readonly Vector3 FaceButtonPosition = new Vector3(0.30f, -0.12f, 0.60f);

        [Header("v3.3: Start screen (two mode cards) and Explore")]
        [SerializeField] private GameObject startCards;
        [SerializeField] private SignSight.Explore.ExploreMode explore;
        // On Start the word card becomes the title, centred above the teacher.
        private static readonly Vector3 StartTitlePosition = new Vector3(0.30f, 0.36f, 1.35f);
        private const float StartTitleScale = 0.7f;
        private static readonly Vector3 MenuCornerPosition = new Vector3(-0.42f, 0.10f, 0.75f);
        private static readonly Vector3 StartInstructionPosition = new Vector3(0.155f, -0.17f, 0.70f);
        [Header("v3.4: Finger Relax (third mode + RELAX on the lesson summary)")]
        [SerializeField] private SignSight.Relax.RelaxMode relax;
        private GameObject _relaxButton;
        private GameObject _ringLabel;
        private Vector3 _ringHome;
        private Vector3 _cardHome, _instructionHome;
        private bool _cardHomeCaptured;

        private readonly List<GameObject> _lessonButtons = new List<GameObject>();
        private TMP_Text _speedLabel, _faceLabel;

        private void Update()
        {
            if (holdRing == null) return;
            bool copying = lessons.CurrentMode == LessonManager.Mode.Learn && lessons.Current == LessonManager.Phase.Copy;
            float p = copying && recognizer != null ? recognizer.HoldProgress : 0f;
            if (lessons.CurrentMode == LessonManager.Mode.Explore && explore != null)
            {
                // Explore: the same ring fills while the pointing ray holds on an object (0.6 s).
                copying = explore.Current == SignSight.Explore.ExploreMode.State.Pointing && explore.HoldProgress > 0f;
                p = explore.HoldProgress;
            }
            if (lessons.CurrentMode == LessonManager.Mode.Relax && relax != null)
            {
                // Relax: the same ring fills toward the step's target; it pauses while no hand is tracked.
                copying = relax.Current == SignSight.Relax.RelaxMode.State.Step || relax.Current == SignSight.Relax.RelaxMode.State.Between;
                p = relax.Progress;
            }
            if (holdRingRoot != null)
            {
                // Relax: the ring sits next to the ghost hand, without Learn's "3s" label.
                if (_ringLabel == null)
                {
                    _ringHome = holdRingRoot.transform.localPosition;
                    var l = holdRingRoot.transform.Find("Label");
                    _ringLabel = l != null ? l.gameObject : holdRingRoot;
                }
                bool inRelax = lessons.CurrentMode == LessonManager.Mode.Relax;
                holdRingRoot.transform.localPosition = inRelax ? SignSight.Relax.RelaxMode.RingPosition : _ringHome;
                if (_ringLabel != holdRingRoot && _ringLabel.activeSelf == inRelax) _ringLabel.SetActive(!inRelax);
            }
            if (holdRingRoot != null && holdRingRoot.activeSelf != copying) holdRingRoot.SetActive(copying);
            holdRing.fillAmount = p;
            if (wordCard != null && teacher != null) wordCard.HighlightFace(teacher.IsFaceOnly);
        }

        /// <summary>Speed button: teacher demo at 1x / 0.5x.</summary>
        public void ToggleSpeed()
        {
            lessons.ToggleDemoSpeed();
            Refresh();
        }

        /// <summary>
        /// FACE button (v3.2 B4): replays only the teacher's face and head at 0.5x, hands at rest, with the expression
        /// line highlighted. Demonstration only; nothing reads the learner's face.
        /// </summary>
        public void ReplayFace()
        {
            if (lessons.CurrentMode == LessonManager.Mode.Explore)
            {
                explore?.ReplayFace();
                return;
            }
            var t = lessons.CurrentTemplate;
            if (teacher == null || t == null || lessons.Current != LessonManager.Phase.Demonstrate) return;
            teacher.DemonstrateFace(SignSight.Hand.TemplateStore.Canonical(t.intent) ?? t, 0.5f);
        }

        private string FaceLine()
        {
            var t = lessons.CurrentTemplate;
            return t != null ? FaceDriver.Describe(t.face) : "";
        }

        private Vector3 _buttonRowCentre;
        private bool _rowCaptured;

        private void OnEnable()
        {
            if (lessons == null) lessons = FindFirstObjectByType<LessonManager>();
            if (score == null) score = FindFirstObjectByType<ScoreManager>();
            if (teacher == null) teacher = FindFirstObjectByType<TeacherAvatarDriver>();
            if (explore == null) explore = FindFirstObjectByType<SignSight.Explore.ExploreMode>();
            lessons.PhaseChanged += Refresh;
            lessons.ModeChanged += OnModeChanged;
            if (explore != null) explore.Changed += Refresh;
            if (relax == null) relax = FindFirstObjectByType<SignSight.Relax.RelaxMode>();
            if (relax != null) relax.Changed += Refresh;
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
                    string label = $"LESSON {l.id}\n<size=60%>{topic}</size>";
                    GameObject b = i == 0 ? lesson1Button : i == 1 && lesson2Button != null ? lesson2Button
                        : CloneButton(lesson1Button, $"Lesson{l.id}Button", label, null);
                    SetButton(b, label, () => lessons.StartLesson(index));
                    _lessonButtons.Add(b);
                }
            }
            if (_relaxButton == null && tryAgainButton != null)
            {
                // v3.4 Task B: RELAX on the lesson summary (a runtime copy of a row button; the scene stays unchanged).
                _relaxButton = CloneButton(tryAgainButton, "RelaxButton", "RELAX", lessons.StartRelaxThenNextLesson);
                var ri = _relaxButton.GetComponent<ButtonIcon>();
                if (ri != null) ri.Set("hand");
                _relaxButton.SetActive(false);
            }
            if (speedButton != null && _speedLabel == null) _speedLabel = speedButton.GetComponentInChildren<TMP_Text>(true);
            if (faceButton != null && _faceLabel == null)
            {
                foreach (var t in faceButton.GetComponentsInChildren<TMP_Text>(true))
                    if (t.name == "Label") _faceLabel = t;
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
            if (lessons != null) lessons.ModeChanged -= OnModeChanged;
            if (explore != null) explore.Changed -= Refresh;
            if (relax != null) relax.Changed -= Refresh;
            if (score != null) score.Changed -= Refresh;
        }

        private void OnModeChanged(LessonManager.Mode m) => Refresh();

        public void Refresh() => Refresh(lessons.Current);

        /// <summary>Completion screen button: practise the missed words (§7).</summary>
        public void PracticeMissed()
        {
            if (score == null || score.Missed.Count == 0) return;
            var words = new LessonManager.Word[score.Missed.Count];
            for (int i = 0; i < words.Length; i++) words[i] = score.Missed[i];
            lessons.StartPractice(words);
        }

        // The project fonts have no star glyph (and no font may be added), so the stars are written out.
        private static string StarText(int stars) => $"<color=#FFD24A>{stars} of 3 stars</color>";

        private void Refresh(LessonManager.Phase phase)
        {
            EnsureParts();
            if (wordCard != null && !_cardHomeCaptured)
            {
                _cardHome = wordCard.transform.localPosition;
                _instructionHome = instruction != null ? instruction.transform.localPosition : Vector3.zero;
                _cardHomeCaptured = true;
            }
            bool start = lessons.CurrentMode == LessonManager.Mode.Start;
            if (startCards != null) startCards.SetActive(start && phase != LessonManager.Phase.Loading);
            if (wordCard != null)
            {
                // Start: the word card is a smaller title above the teacher; the instruction goes under the cards.
                wordCard.transform.localPosition = start ? StartTitlePosition : _cardHome;
                wordCard.transform.localScale = Vector3.one * (start ? StartTitleScale : 1f);
            }
            if (instruction != null) instruction.transform.localPosition = start ? StartInstructionPosition : _instructionHome;
            if (phase != LessonManager.Phase.Loading && lessons.CurrentMode != LessonManager.Mode.Learn)
            {
                RefreshStartOrExplore();
                return;
            }
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
                    wordCard?.Show(word.intent, word.meaning, word.cue + NonManualLine(), FaceLine());
                    SetInstruction($"Watch the teacher{(lessons.DemoSpeed < 0.75f ? " (half speed)" : "")}");
                    break;
                case LessonManager.Phase.Copy:
                    wordCard?.Show(word.intent, word.meaning, word.cue + NonManualLine(), FaceLine());
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
            // Speed toggle sits on its own, left of the button row, so the row keeps its v3 width.
            if (speedButton != null)
            {
                speedButton.SetActive(phase == LessonManager.Phase.Demonstrate || phase == LessonManager.Phase.Copy);
                speedButton.transform.localPosition = SpeedButtonPosition;
            }
            if (_speedLabel != null) _speedLabel.text = lessons.DemoSpeed < 0.75f ? "½×\n<size=60%>SPEED</size>" : "1×\n<size=60%>SPEED</size>";
            // FACE replay: only while the teacher demonstrates a word whose face does something.
            if (faceButton != null)
            {
                var ft = lessons.CurrentTemplate;
                bool hasFace = ft != null && ft.face != null && !ft.face.IsNeutral;
                faceButton.SetActive(phase == LessonManager.Phase.Demonstrate && hasFace);
                faceButton.transform.localPosition = FaceButtonPosition;
                if (hasFace && _faceLabel != null)
                    _faceLabel.text = $"FACE\n<size=60%>{FaceDriver.Resolve(ft.face).name.Split('—')[0].Trim()}</size>";
            }

            // Tutor panel: the agent's decision and its reason, in plain text (SPEC v3.1 §4.1).
            if (tutorPanel != null)
            {
                bool showTutor = !lessons.IsPractice && tutor != null &&
                                 (phase == LessonManager.Phase.Demonstrate || phase == LessonManager.Phase.Copy || phase == LessonManager.Phase.Feedback);
                (tutorRoot != null ? tutorRoot : tutorPanel.gameObject).SetActive(showTutor);
                // The panel has its own TUTOR title line.
                if (showTutor) tutorPanel.text = tutor.PanelText().Replace("<b><color=#9DB7FF>Tutor</color></b>\n", "");
            }
            Show(tryAgainButton, phase == LessonManager.Phase.Feedback && score != null && score.CanTryAgain);
            var tryLabel = tryAgainButton != null ? tryAgainButton.transform.Find("Label")?.GetComponent<TMP_Text>() : null;
            if (tryLabel != null) tryLabel.text = "TRY\nAGAIN";
            SetIcon(tryAgainButton, "tryagain");
            SetLabel(replayButton, _replayText);
            SetIcon(replayButton, "replay");
            Show(practiceButton, phase == LessonManager.Phase.Complete && !lessons.IsPractice && score != null && score.Missed.Count > 0);
            // v3.4 Task B: the learner may take a 1-minute Finger Relax before the next lesson (never forced).
            Show(_relaxButton, phase == LessonManager.Phase.LessonSummary && !lessons.IsPractice);
            bool next = phase == LessonManager.Phase.Copy || phase == LessonManager.Phase.Feedback ||
                        phase == LessonManager.Phase.LessonSummary || phase == LessonManager.Phase.Complete ||
                        phase == LessonManager.Phase.Menu; // v3.3: MENU back to Start from the lesson list
            if (phase == LessonManager.Phase.Menu && nextButton != null)
            {
                // Five lesson buttons fill the row: MENU sits on its own, top-left (left of the teacher).
                nextButton.SetActive(true);
                nextButton.transform.localPosition = MenuCornerPosition;
            }
            else Show(nextButton, next);
            if (nextLabel != null)
            {
                nextLabel.text = phase == LessonManager.Phase.LessonSummary
                    ? (lessons.LessonIndex + 1 < lessons.Data.lessons.Length ? "NEXT\nLESSON" : "FINISH")
                    : phase == LessonManager.Phase.Complete || phase == LessonManager.Phase.Menu ? "MENU" : "NEXT";
                var icon = nextButton != null ? nextButton.GetComponent<ButtonIcon>() : null;
                if (icon != null) icon.Set(phase == LessonManager.Phase.Complete || phase == LessonManager.Phase.Menu ? "menu" : "next");
            }
            LayOut(visible);
        }

        /// <summary>v3.3: Start screen (two mode cards) and Explore. No score, stars or Tutor outside Learn.</summary>
        private void RefreshStartOrExplore()
        {
            bool isStart = lessons.CurrentMode == LessonManager.Mode.Start;
            if (topBar != null) topBar.gameObject.SetActive(false);
            if (tutorPanel != null) (tutorRoot != null ? tutorRoot : tutorPanel.gameObject).SetActive(false);
            var visible = new List<GameObject>();
            void Show(GameObject b, bool on) { if (b == null) return; b.SetActive(on); if (on) visible.Add(b); }
            foreach (var b in _lessonButtons) Show(b, false);
            Show(lesson1Button, false);
            Show(lesson2Button, false);
            Show(practiceButton, false);
            Show(_relaxButton, false);

            if (lessons.CurrentMode == LessonManager.Mode.Relax)
            {
                RefreshRelax(visible, Show);
                return;
            }
            // Relax relabels Try Again / Replay: restore them (Explore sets its own BACK label below).
            SetLabel(replayButton, _replayText);
            SetIcon(replayButton, "replay");
            SetIcon(tryAgainButton, "tryagain");

            if (isStart)
            {
                wordCard?.Show("SignSight Learn", $"Learn {lessons.TotalWords} signs · Watch the face, copy the hands");
                SetInstruction("Pinch a mode to start");
                Show(replayButton, false);
                Show(tryAgainButton, false);
                Show(nextButton, false);
                if (speedButton != null) speedButton.SetActive(false);
                if (faceButton != null) faceButton.SetActive(false);
                LayOut(visible);
                return;
            }

            // Explore
            var st = explore != null ? explore.Current : SignSight.Explore.ExploreMode.State.Pointing;
            var tpl = explore != null ? explore.SelectedTemplate : null;
            if (st == SignSight.Explore.ExploreMode.State.Pointing)
            {
                wordCard?.Hide();
                SetInstruction(explore != null && explore.UsingCamera
                    ? "Point at a box in the camera view, hold 0.6 s\n<size=70%>Mock camera (Editor) · try: apple · cup · bottle</size>"
                    : "Point at an object, hold 0.6 s\n<size=70%>Try: apple · cup · bottle</size>");
            }
            else
            {
                var e = explore.Selected;
                string cue = "";
                if (tpl != null && lessons.Data != null)
                    foreach (var l in lessons.Data.lessons)
                        foreach (var w in l.words)
                            if (w.intent == tpl.intent) cue = w.cue;
                string meaning = tpl != null ? e.Meaning : $"<color={UIStyle.Amber}>{e.Meaning}</color>";
                wordCard?.Show(e.Word, meaning, cue, tpl != null ? FaceDriver.Describe(tpl.face) : "");
                SetInstruction(tpl == null ? "Point at something else"
                    : st == SignSight.Explore.ExploreMode.State.Demonstrating ? "Watch the teacher — then copy the sign"
                    : "Copy the sign — Replay, or Back to try another object");
            }
            bool shown = st == SignSight.Explore.ExploreMode.State.Shown;
            Show(replayButton, shown && tpl != null);
            Show(tryAgainButton, shown || (st == SignSight.Explore.ExploreMode.State.Demonstrating));
            var backLabel = tryAgainButton != null ? tryAgainButton.transform.Find("Label")?.GetComponent<TMP_Text>() : null;
            if (backLabel != null) backLabel.text = "BACK";
            Show(nextButton, true);
            if (nextLabel != null) nextLabel.text = "MENU";
            var icon = nextButton != null ? nextButton.GetComponent<ButtonIcon>() : null;
            if (icon != null) icon.Set("menu");
            if (speedButton != null)
            {
                speedButton.SetActive(tpl != null);
                speedButton.transform.localPosition = SpeedButtonPosition;
            }
            if (_speedLabel != null) _speedLabel.text = lessons.DemoSpeed < 0.75f ? "½×\n<size=60%>SPEED</size>" : "1×\n<size=60%>SPEED</size>";
            if (faceButton != null)
            {
                bool hasFace = tpl != null && tpl.face != null && !tpl.face.IsNeutral;
                faceButton.SetActive(hasFace && st != SignSight.Explore.ExploreMode.State.Pointing);
                faceButton.transform.localPosition = FaceButtonPosition;
                if (hasFace && _faceLabel != null)
                    _faceLabel.text = $"FACE\n<size=60%>{FaceDriver.Resolve(tpl.face).name.Split('—')[0].Trim()}</size>";
            }
            LayOut(visible);
        }

        private static void SetIcon(GameObject button, string icon)
        {
            var bi = button != null ? button.GetComponent<ButtonIcon>() : null;
            if (bi != null) bi.Set(icon);
        }

        private string _replayText;

        private void SetLabel(GameObject button, string text)
        {
            var l = button != null ? button.transform.Find("Label")?.GetComponent<TMP_Text>() : null;
            if (l == null) return;
            if (button == replayButton && _replayText == null) _replayText = l.text; // the scene's own Replay label
            if (text != null) l.text = text;
        }

        /// <summary>
        /// v3.4 Finger Relax: step name + one instruction line on the word card, the ghost hand shows the pose, the ring
        /// fills toward the target. SKIP always (during the steps), DONE on the end screen, MENU top-left. No score,
        /// stars, streak or Tutor. The safety line is on every Relax screen.
        /// </summary>
        private void RefreshRelax(List<GameObject> visible, System.Action<GameObject, bool> Show)
        {
            var R = SignSight.Relax.RelaxMode.State.Finished;
            var st = relax != null ? relax.Current : R;
            if (st == R)
            {
                int pending = lessons.PendingLessonAfterRelax;
                string next = relax != null && relax.ReturnToLesson && pending != LessonManager.NoPending
                    ? (pending >= 0 ? $"Pinch DONE to start Lesson {pending + 1}" : "Pinch DONE to see your results")
                    : "Pinch DONE to go back";
                wordCard?.Show(SignSight.Relax.RelaxMode.FinishedTitle, next);
            }
            else if (relax != null && relax.CurrentStep != null)
            {
                var step = relax.CurrentStep;
                string status = relax.Status;
                string line = $"<color=#9DB7FF>Step {relax.StepIndex + 1} of {SignSight.Relax.RelaxMode.Steps.Length}</color>";
                if (!string.IsNullOrEmpty(status))
                    line += status == SignSight.Relax.RelaxMode.ShowHand ? $"\n<color={UIStyle.Amber}>{status}</color>" : $"\n{status}";
                wordCard?.Show(step.name, step.instruction, line);
            }
            SetInstruction($"{SignSight.Relax.RelaxMode.WhyLine}\n<size=70%><color={UIStyle.Amber}>{SignSight.Relax.RelaxMode.SafetyLine}</color></size>");

            bool stepping = st != R;
            Show(tryAgainButton, stepping);
            SetLabel(tryAgainButton, "SKIP");
            SetIcon(tryAgainButton, "next");
            Show(replayButton, !stepping);
            SetLabel(replayButton, "DONE");
            SetIcon(replayButton, "check");
            if (nextButton != null)
            {
                // MENU top-left, as in the lesson list: leaves Relax for Start (and starts no lesson).
                nextButton.SetActive(true);
                nextButton.transform.localPosition = MenuCornerPosition;
            }
            if (nextLabel != null) nextLabel.text = "MENU";
            SetIcon(nextButton, "menu");
            if (speedButton != null) speedButton.SetActive(false);
            if (faceButton != null) faceButton.SetActive(false);
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
