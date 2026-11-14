using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SignSight.Hand;
using SignSight.Learn;
using UnityEngine;

namespace SignSight.EditorTools
{
    /// <summary>
    /// Editor-only self-test (v3.1): the teacher performs each sign frame by frame (PoseAt), its own hands are read
    /// back through AvatarHandProvider into a TrajectoryTracker, and every scored template is evaluated on that
    /// trajectory. A sign passes when its own template is Top1 among the scored templates and its movement completed.
    /// This checks that the teacher, the definitions and the recogniser agree; it does not replace testing with a
    /// real learner's hands. Run in Play Mode: SignSelfTest.Run() (writes Logs/v31-selftest.txt).
    /// </summary>
    public static class SignSelfTest
    {
        public const float Fps = 30f;

        public struct Row
        {
            public string Intent;
            public bool Scored, Completed, Top1Ok;
            public string Top1, Top2;
            public float Total, Top1Total, H, L, M;
            public string Hint;
        }

        public static List<Row> Run(string only = null, string outPath = "Logs/v31-selftest.txt")
        {
            var teacher = Object.FindFirstObjectByType<TeacherAvatarDriver>();
            var anim = teacher.GetComponentInChildren<Animator>();
            teacher.Setup();
            SignScorer.ConfigureForPlatform(1f, 0f, 0f); // spec values: the avatar has no webcam jitter
            var head = new GameObject("SelfTestHead").transform;
            var le = FindDeep(teacher.transform, "Bip01 LEye");
            var re = FindDeep(teacher.transform, "Bip01 REye");
            head.SetPositionAndRotation((le.position + re.position) * 0.5f, teacher.transform.rotation);
            var provider = new AvatarHandProvider(anim, head);
            var store = TemplateStore.FromDefaults();
            var scored = store.Templates.Where(t => t.scored).ToList();
            var rows = new List<Row>();
            foreach (var t in store.Templates)
            {
                if (only != null && t.intent != only) continue;
                var tracker = new TrajectoryTracker(6f);
                float time = 0f;
                void Push(float u)
                {
                    teacher.PoseAt(t, u);
                    tracker.Push(provider, time);
                    time += 1f / Fps;
                }
                // Hold the start pose, perform the movement once, hold the end pose.
                for (int i = 0; i < 9; i++) Push(0f);
                int n = Mathf.Max(4, Mathf.RoundToInt(t.movement.durationSec * Fps));
                for (int i = 0; i <= n; i++) Push((float)i / n);
                for (int i = 0; i < 12; i++) Push(1f);

                var evals = scored.Select(s => SignScorer.Evaluate(s, tracker.Frames, 0)).OrderBy(e => e.Total).ToList();
                var own = SignScorer.Evaluate(t, tracker.Frames, 0);
                rows.Add(new Row
                {
                    Intent = t.intent,
                    Scored = t.scored,
                    Completed = own.Completed,
                    Top1 = evals[0].Template.intent,
                    Top2 = evals.Count > 1 ? evals[1].Template.intent : "",
                    Top1Ok = evals[0].Template.intent == t.intent,
                    Total = own.Total,
                    Top1Total = evals[0].Total,
                    H = own.Handshape, L = own.Location, M = own.Movement,
                    Hint = own.Hint,
                });
            }
            teacher.Rest();
            Object.Destroy(head.gameObject);
            SignScorer.ConfigureForPlatform();

            var sb = new StringBuilder();
            sb.AppendLine("intent\tscored\tcompleted\ttop1\ttop2\tpass\ttotal\tH\tL\tM\ttop1Total\thint");
            foreach (var r in rows)
            {
                bool pass = r.Top1Ok && r.Completed && r.Total <= 0.35f;
                sb.AppendLine($"{r.Intent}\t{r.Scored}\t{r.Completed}\t{r.Top1}\t{r.Top2}\t{(pass ? "PASS" : "FAIL")}\t" +
                              $"{r.Total:0.00}\t{r.H:0.00}\t{r.L:0.00}\t{r.M:0.00}\t{r.Top1Total:0.00}\t{r.Hint}");
            }
            if (outPath != null) File.WriteAllText(outPath, sb.ToString());
            Debug.Log("[SelfTest]\n" + sb);
            return rows;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
            {
                if (tr.name == name) return tr;
            }
            return root;
        }
    }
}
