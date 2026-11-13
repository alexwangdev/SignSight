using System;
using System.Collections;
using System.Collections.Generic;
using SignSight.Hand;
using UnityEngine;

namespace SignSight.Learn
{
    /// <summary>
    /// Scoring (SPEC §7): +10 on the first try, +5 on the second, +0 and move on after two wrong tries; no penalties.
    /// Streak counts first-try correct answers only; every 3 in a row earns +5. Stars from the total.
    /// Practice runs are not scored.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public const int FirstTryPoints = 10, SecondTryPoints = 5, StreakBonus = 5, StreakLength = 3, MaxAttempts = 2;
        public const float MinConfidence = 0.7f, MaxDistance = 0.35f;

        public enum Outcome { CorrectFirstTry, CorrectSecondTry, WrongCanRetry, WrongMovedOn }

        public struct Verdict
        {
            public LessonManager.Word Word;
            public RecognitionResult Attempt;
            public Outcome Outcome;
            public int PointsAwarded;   // including any streak bonus
            public bool StreakBonusAwarded;
            public bool IsCorrect => Outcome == Outcome.CorrectFirstTry || Outcome == Outcome.CorrectSecondTry;
        }

        [SerializeField] private LessonManager lessons;
        [Tooltip("After a correct answer, move to the next word on its own after this many seconds (Next also works).")]
        [SerializeField] private float autoNextAfterCorrect = 2.5f;

        public int Score { get; private set; }
        public int Streak { get; private set; }
        public int AttemptsOnWord { get; private set; }
        public bool CanTryAgain { get; private set; }
        public Verdict? Last { get; private set; }
        /// <summary>Words not correct on the first try (for "Practice the words you missed").</summary>
        public IReadOnlyList<LessonManager.Word> Missed => _missed;

        public event Action<Verdict> Scored;
        public event Action Changed;

        private readonly List<LessonManager.Word> _missed = new List<LessonManager.Word>();
        private Coroutine _autoNext;

        private void OnEnable()
        {
            if (lessons == null) lessons = FindFirstObjectByType<LessonManager>();
            lessons.Attempted += OnAttempted;
            lessons.WordStarted += OnWordStarted;
            lessons.Skipped += OnSkipped;
            lessons.PhaseChanged += OnPhase;
        }

        private void OnDisable()
        {
            if (lessons == null) return;
            lessons.Attempted -= OnAttempted;
            lessons.WordStarted -= OnWordStarted;
            lessons.Skipped -= OnSkipped;
            lessons.PhaseChanged -= OnPhase;
        }

        public static int Stars(int total) => total >= 70 ? 3 : total >= 50 ? 2 : total >= 30 ? 1 : 0;

        /// <summary>The §7 rule.</summary>
        public static bool IsCorrect(LessonManager.Word target, RecognitionResult r) =>
            r.Top1 != null && r.Top1.intent == target.intent && r.Confidence >= MinConfidence && r.Distance <= MaxDistance;

        public void ResetAll()
        {
            Score = 0;
            Streak = 0;
            _missed.Clear();
            Last = null;
            Changed?.Invoke();
        }

        private void OnPhase(LessonManager.Phase p)
        {
            // A fresh run from the menu starts a fresh score.
            if (p == LessonManager.Phase.Menu) ResetAll();
            if (p != LessonManager.Phase.Feedback && _autoNext != null)
            {
                StopCoroutine(_autoNext);
                _autoNext = null;
            }
        }

        private void OnWordStarted(LessonManager.Word w)
        {
            AttemptsOnWord = 0;
            CanTryAgain = false;
            Last = null;
        }

        private void OnSkipped(LessonManager.Word w)
        {
            if (lessons.IsPractice || AttemptsOnWord > 0) return;
            Streak = 0;
            AddMissed(w);
            Changed?.Invoke();
        }

        private void OnAttempted(LessonManager.Word word, RecognitionResult r)
        {
            AttemptsOnWord++;
            bool correct = IsCorrect(word, r);
            var v = new Verdict { Word = word, Attempt = r };

            if (correct)
            {
                v.Outcome = AttemptsOnWord == 1 ? Outcome.CorrectFirstTry : Outcome.CorrectSecondTry;
                if (!lessons.IsPractice)
                {
                    if (v.Outcome == Outcome.CorrectFirstTry)
                    {
                        v.PointsAwarded = FirstTryPoints;
                        Streak++;
                        if (Streak % StreakLength == 0)
                        {
                            v.PointsAwarded += StreakBonus;
                            v.StreakBonusAwarded = true;
                        }
                    }
                    else
                    {
                        v.PointsAwarded = SecondTryPoints; // streak is not reset
                        AddMissed(word);
                    }
                    Score += v.PointsAwarded;
                }
                CanTryAgain = false;
            }
            else if (AttemptsOnWord < MaxAttempts)
            {
                v.Outcome = Outcome.WrongCanRetry;
                CanTryAgain = true;
            }
            else
            {
                v.Outcome = Outcome.WrongMovedOn;
                CanTryAgain = false;
                if (!lessons.IsPractice)
                {
                    Streak = 0;
                    AddMissed(word);
                }
            }

            Last = v;
            Changed?.Invoke();
            Scored?.Invoke(v);
            lessons.ShowFeedback();
            if (v.IsCorrect) _autoNext = StartCoroutine(AutoNext());
        }

        private IEnumerator AutoNext()
        {
            yield return new WaitForSeconds(autoNextAfterCorrect);
            _autoNext = null;
            if (lessons.Current == LessonManager.Phase.Feedback) lessons.Next();
        }

        private void AddMissed(LessonManager.Word w)
        {
            if (!_missed.Exists(x => x.intent == w.intent)) _missed.Add(w);
        }
    }
}
