using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SignSight.Learn
{
    /// <summary>What the Tutor Agent remembers about one word (SPEC v3.1 §4.1 "Remember").</summary>
    [Serializable]
    public class WordState
    {
        public string intent;
        public int attempts;                 // scored attempts, all visits
        public int visits;                   // times the word was shown
        public int correctFirstTry;
        public int correctSecondTry;
        public int wrongVisits;              // visits that ended with two wrong tries
        public int consecutiveFirstTry;      // correct on the first try, visits in a row
        public bool lastVisitFailedTwice;    // the last visit ended with two wrong tries
        public int practised;                // TEACH-ONLY words: times practised
        public List<int> worstFingerHistory = new List<int>();        // 0 thumb .. 4 pinky, red fingers only
        public List<string> movementErrorHistory = new List<string>(); // the movement hint of each wrong attempt
        public int lastSeenLesson = -1;      // LearnerProfile.lessonsStarted when last shown
        public string lastSeenUtc = "";
        public int streak;                   // correct visits in a row
        public float lastTimeTaken;          // seconds from "your turn" to the attempt

        /// <summary>The finger that was red in the last two wrong attempts, or -1.</summary>
        public int RepeatedWorstFinger
        {
            get
            {
                int n = worstFingerHistory.Count;
                if (n < 2) return -1;
                return worstFingerHistory[n - 1] == worstFingerHistory[n - 2] ? worstFingerHistory[n - 1] : -1;
            }
        }

        /// <summary>Correct on the first try twice in a row: shown less often.</summary>
        public bool Demoted => consecutiveFirstTry >= 2;
    }

    /// <summary>
    /// The learner's per-word history, stored on the headset only (JSON in Application.persistentDataPath).
    /// No account, no network. Delete the file to start over.
    /// </summary>
    [Serializable]
    public class LearnerProfile
    {
        public const string FileName = "learner_profile.json";
        public const int MaxHistory = 8;

        public int version = 1;
        public int lessonsStarted;           // lesson runs started, used for "not seen for 2+ lessons"
        public List<WordState> words = new List<WordState>();

        public static string PathOnDisk => Path.Combine(Application.persistentDataPath, FileName);

        public static LearnerProfile Load()
        {
            try
            {
                if (File.Exists(PathOnDisk))
                {
                    var p = JsonUtility.FromJson<LearnerProfile>(File.ReadAllText(PathOnDisk));
                    if (p != null && p.words != null) return p;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LearnerProfile] Could not read {PathOnDisk}, starting fresh. {e.Message}");
            }
            return new LearnerProfile();
        }

        public void Save()
        {
            try
            {
                foreach (var w in words)
                {
                    Trim(w.worstFingerHistory);
                    Trim(w.movementErrorHistory);
                }
                File.WriteAllText(PathOnDisk, JsonUtility.ToJson(this, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LearnerProfile] Could not save {PathOnDisk}. {e.Message}");
            }
        }

        private static void Trim<T>(List<T> l)
        {
            while (l.Count > MaxHistory) l.RemoveAt(0);
        }

        public WordState Get(string intent)
        {
            var w = words.Find(x => x.intent == intent);
            if (w != null) return w;
            w = new WordState { intent = intent };
            words.Add(w);
            return w;
        }

        public WordState Find(string intent) => words.Find(x => x.intent == intent);
    }
}
