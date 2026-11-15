#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SignSight.Hand;
using SignSight.Learn;
using UnityEngine;

namespace SignSight.DevTools
{
    /// <summary>
    /// Editor Play Mode only (v3.15 steps 3-4): the teacher demonstrates each word and the right hand bone is sampled
    /// every frame at a fixed 90 fps. Per word: demonstration time, peak speed, the largest frame-to-frame change of
    /// velocity (m/s: a jump) and of angular velocity (deg/s), and when each happened (seconds from the start).
    /// A smaller jump = a smoother phase change. Stills cannot show this; the numbers can.
    /// </summary>
    public class MotionTrace : MonoBehaviour
    {
        public bool Done { get; private set; }
        public readonly StringBuilder Log = new StringBuilder();

        public static MotionTrace Begin(string[] words, string outPath, string label)
        {
            var c = new GameObject("MotionTrace").AddComponent<MotionTrace>();
            c.StartCoroutine(c.Run(words, outPath, label));
            return c;
        }

        private IEnumerator Run(string[] words, string outPath, string label)
        {
            var lm = FindFirstObjectByType<LessonManager>();
            while (lm.Current == LessonManager.Phase.Loading) yield return null;
            var teacher = FindFirstObjectByType<TeacherAvatarDriver>();
            var hand = teacher.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightHand);
            lm.GoStart();
            int oldRate = Application.targetFrameRate, oldSync = QualitySettings.vSyncCount;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 90; // same frame time for every run, so the per-frame jumps compare
            yield return new WaitForSecondsRealtime(0.5f);
            Log.AppendLine($"[{label}] word\tseconds\tpeak m/s\tmax velocity jump m/s\tat s\tmax angular jump deg/s\tat s\tframes");
            float sumV = 0f, sumA = 0f;
            int n = 0;
            foreach (var word in words)
            {
                var t = TemplateStore.Canonical(word);
                if (t == null) continue;
                teacher.Rest();
                yield return null;
                var pos = new List<Vector3>();
                var rot = new List<Quaternion>();
                var dts = new List<float>();
                teacher.Demonstrate(t);
                float t0 = Time.time;
                while (teacher.IsDemonstrating && Time.time - t0 < 14f)
                {
                    yield return new WaitForEndOfFrame();
                    pos.Add(teacher.transform.InverseTransformPoint(hand.position));
                    rot.Add(Quaternion.Inverse(teacher.transform.rotation) * hand.rotation);
                    dts.Add(Mathf.Max(1e-4f, Time.deltaTime));
                }
                float peak = 0f, jumpV = 0f, jumpA = 0f, atV = 0f, atA = 0f, clock = 0f;
                Vector3 prevV = Vector3.zero;
                float prevW = 0f;
                for (int i = 1; i < pos.Count; i++)
                {
                    clock += dts[i];
                    Vector3 v = (pos[i] - pos[i - 1]) / dts[i];
                    float w = Quaternion.Angle(rot[i], rot[i - 1]) / dts[i];
                    peak = Mathf.Max(peak, v.magnitude);
                    if (i > 1)
                    {
                        if ((v - prevV).magnitude > jumpV) { jumpV = (v - prevV).magnitude; atV = clock; }
                        if (Mathf.Abs(w - prevW) > jumpA) { jumpA = Mathf.Abs(w - prevW); atA = clock; }
                    }
                    prevV = v;
                    prevW = w;
                }
                Log.AppendLine($"{word}\t{Time.time - t0:0.00}\t{peak:0.00}\t{jumpV:0.000}\t{atV:0.00}\t{jumpA:0}\t{atA:0.00}\t{pos.Count}");
                sumV += jumpV;
                sumA += jumpA;
                n++;
                yield return new WaitForSecondsRealtime(0.2f);
            }
            Application.targetFrameRate = oldRate;
            QualitySettings.vSyncCount = oldSync;
            if (n > 0) Log.AppendLine($"[{label}] mean\t\t\t{sumV / n:0.000}\t\t{sumA / n:0}");
            File.AppendAllText(outPath, Log.ToString() + "\n");
            File.WriteAllText("Logs/v315-trace-done.txt", label);
            Done = true;
        }
    }
}
#endif
