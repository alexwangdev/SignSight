#if UNITY_EDITOR
using System.Collections;
using System.IO;
using SignSight.Learn;
using UnityEngine;

namespace SignSight.DevTools
{
    /// <summary>
    /// Editor Play Mode only (v3.5 §6): runs the exam check and the v3.3 / v3.4 regression checks one after another
    /// (Lesson 1 smoke run, Explore virtual objects, Explore YOLO mock camera, Relax Task A + B). The learner profile is
    /// backed up first and restored at the end (the smoke runs reset it so every word is played).
    /// </summary>
    public class V35Regression : MonoBehaviour
    {
        public bool Done { get; private set; }
        public string Stage { get; private set; } = "";

        public static V35Regression Begin()
        {
            var r = new GameObject("V35Regression").AddComponent<V35Regression>();
            r.StartCoroutine(r.Run());
            return r;
        }

        private IEnumerator Run()
        {
            var lm = FindFirstObjectByType<LessonManager>();
            while (lm.Current == LessonManager.Phase.Loading) yield return null;
            string profile = LearnerProfile.PathOnDisk, backup = "Logs/learner-profile-backup-v35.json";
            if (File.Exists(profile)) File.Copy(profile, backup, true);

            Stage = "exam";
            var ex = ExamCheck.Begin();
            while (!ex.Done) yield return null;

            Stage = "lesson1";
            lm.GoStart();
            lm.Tutor.ResetProfile();
            lm.StartLearnMode();
            var smoke = LessonSmokeRun.Begin(0);
            while (!smoke.Done) yield return null;
            File.Copy("Logs/v31-lesson-smoke.txt", "Logs/v35-lesson1-smoke.txt", true);

            Stage = "explore";
            lm.GoStart();
            lm.Explore.PreferCamera = false; // virtual objects (the headset path)
            var e1 = ExploreCheck.Begin();
            while (!e1.Done) yield return null;
            lm.GoStart();
            lm.Explore.PreferCamera = true;
            Stage = "explore-camera";
            var e2 = ExploreCameraCheck.Begin();
            while (!e2.Done) yield return null;

            Stage = "relaxA";
            lm.GoStart();
            var ra = RelaxCheck.Begin("taskA");
            while (!ra.Done) yield return null;
            Stage = "relaxB";
            lm.Tutor.ResetProfile();
            var rb = RelaxCheck.Begin("taskB");
            while (!rb.Done) yield return null;

            lm.GoStart();
            if (File.Exists(backup)) File.Copy(backup, profile, true);
            Stage = "done";
            File.WriteAllText("Logs/v35-regression-done.txt", "done\n");
            Done = true;
        }
    }
}
#endif
