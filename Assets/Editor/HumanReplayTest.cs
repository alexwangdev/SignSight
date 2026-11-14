using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SignSight.Hand;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace SignSight.EditorTools
{
    /// <summary>
    /// Editor-only: replays real human signing (WLASL clips -> MediaPipe 3D hands, export_hands.py) through the
    /// recogniser. Every scored template is evaluated on the whole clip, as in SignSelfTest. A clip counts as recognised
    /// when its own sign is Top-1 among the scored templates, its movement completed and its total is within tolerance
    /// (the same rule as ScoreManager.IsCorrect). Runs in edit mode or Play Mode; writes Logs/v31-human-replay.txt.
    /// </summary>
    public static class HumanReplayTest
    {
        public const string Dir = "workspace/goals/meta-vr-start-sign-language-entry/files/teacher-verification/human_hands_flat";

        [Serializable] private class FrameJ { public float[] R; public float[] L; }
        [Serializable] private class ClipJ
        {
            public string clip; public float fps; public bool left_handed_mirrored; public float shoulder_width_m;
            public FrameJ[] frames;
        }

        /// <summary>Plays back stored joints; positions are already in the signer frame (head at the origin).</summary>
        private class ReplayProvider : IHandProvider
        {
            private readonly Transform _head;
            public FrameJ Current;
            private readonly Vector3[][] _pos = { new Vector3[HandFrame.JointCount], new Vector3[HandFrame.JointCount] };
            private readonly Quaternion[] _rot = Enumerable.Repeat(Quaternion.identity, HandFrame.JointCount).ToArray();
            public Transform Head => _head;
            public ReplayProvider(Transform head) { _head = head; }

            private static readonly XRHandJointID[] Thumb =
                { XRHandJointID.ThumbMetacarpal, XRHandJointID.ThumbProximal, XRHandJointID.ThumbDistal, XRHandJointID.ThumbTip };
            private static readonly XRHandJointID[][] Fingers =
            {
                new[] { XRHandJointID.IndexMetacarpal, XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, XRHandJointID.IndexTip },
                new[] { XRHandJointID.MiddleMetacarpal, XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip },
                new[] { XRHandJointID.RingMetacarpal, XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, XRHandJointID.RingTip },
                new[] { XRHandJointID.LittleMetacarpal, XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, XRHandJointID.LittleTip },
            };

            public bool TryGetHand(HandSide side, out HandFrame frame)
            {
                frame = default;
                var a = side == HandSide.Right ? Current?.R : Current?.L;
                if (a == null || a.Length < 63) return false;
                Vector3 M(int i) => new Vector3(a[i * 3], a[i * 3 + 1], a[i * 3 + 2]); // MediaPipe hand landmark i
                var p = _pos[side == HandSide.Right ? 1 : 0];
                // Same joint layout as AvatarHandProvider: MediaPipe 21 landmarks -> XR Hands 26 joints.
                p[XRHandJointID.Wrist.ToIndex()] = M(0);
                p[XRHandJointID.Palm.ToIndex()] = (M(0) + M(9)) * 0.5f;
                for (int j = 0; j < 4; j++) p[Thumb[j].ToIndex()] = M(1 + j);
                for (int f = 0; f < 4; f++)
                {
                    int mcp = 5 + f * 4;
                    p[Fingers[f][0].ToIndex()] = Vector3.Lerp(M(0), M(mcp), 0.3f);
                    for (int j = 0; j < 4; j++) p[Fingers[f][j + 1].ToIndex()] = M(mcp + j);
                }
                frame = new HandFrame { Side = side, Positions = p, Rotations = _rot };
                return true;
            }
        }

        public struct Row
        {
            public string Clip, Word, Top1, Top2, Hint;
            public bool Correct, Completed, Mirrored;
            public int Rank;
            public float Total, H, L, M, Top1Total;
        }

        /// <summary>Time between the first completed movement and the fire (SignRecognizer settles 0.25-1.0 s).</summary>
        public const float SettleSeconds = 0.5f;

        public static List<Row> Run(string only = null, string outPath = "Logs/v31-human-replay.txt", bool simulatorValues = false,
                                    bool fireLikeApp = true)
        {
            // Spec (device) values by default: MediaPipe from a clean dictionary video is closer to headset tracking than
            // to the live webcam; simulatorValues = true uses the editor's webcam allowances instead.
            if (simulatorValues) SignScorer.ConfigureForPlatform(); else SignScorer.ConfigureForPlatform(1f, 0f, 0f);
            var store = TemplateStore.FromDefaults();
            var scored = store.Templates.Where(t => t.scored).ToList();
            var head = new GameObject("HumanReplayHead").transform;
            var prov = new ReplayProvider(head);
            var rows = new List<Row>();
            foreach (var file in Directory.GetFiles(Dir, "*.json").OrderBy(f => f))
            {
                var clip = JsonUtility.FromJson<ClipJ>(File.ReadAllText(file));
                string word = Path.GetFileNameWithoutExtension(file);
                word = word.Substring(0, word.LastIndexOf('_'));
                if (only != null && word != only) continue;
                var target = store.Get(word);
                if (target == null) continue;
                var tracker = new TrajectoryTracker(30f);
                float dt = 1f / Mathf.Max(1f, clip.fps);
                for (int i = 0; i < clip.frames.Length; i++)
                {
                    prov.Current = clip.frames[i];
                    tracker.Push(prov, i * dt);
                }
                // Fire like SignRecognizer: at the first completed movement (any scored sign) plus a settle time, then
                // judge only what was signed up to then (the hand dropping back to rest afterwards is not judged).
                var full = scored.Select(s => SignScorer.Evaluate(s, tracker.Frames, 0)).ToList();
                var done = full.Where(e => e.Performed).Select(e => e.CompletedTime).DefaultIfEmpty(float.NaN).Min();
                IReadOnlyList<TrajFrame> judged = tracker.Frames;
                if (fireLikeApp && !float.IsNaN(done))
                {
                    float fireAt = done + SettleSeconds;
                    judged = tracker.Frames.Where(fr => fr.Time <= fireAt).ToList();
                }
                var evals = scored.Select(s => SignScorer.Evaluate(s, judged, 0)).OrderBy(e => e.Total).ToList();
                var own = SignScorer.Evaluate(target, judged, 0);
                if (!target.scored) own = SignScorer.Evaluate(target, tracker.Frames, 0);
                int rank = evals.FindIndex(e => e.Template.intent == word) + 1;
                bool correct = target.scored && evals[0].Template.intent == word && own.Completed &&
                               own.Total <= Mathf.Min(0.35f, target.tolerance);
                rows.Add(new Row
                {
                    Clip = clip.clip, Word = word, Top1 = evals[0].Template.intent, Top2 = evals.Count > 1 ? evals[1].Template.intent : "",
                    Correct = correct, Completed = own.Completed, Mirrored = clip.left_handed_mirrored, Rank = rank,
                    Total = own.Total, H = own.Handshape, L = own.Location, M = own.Movement, Top1Total = evals[0].Total, Hint = own.Hint,
                });
            }
            UnityEngine.Object.DestroyImmediate(head.gameObject);
            SignScorer.ConfigureForPlatform();

            var sb = new StringBuilder();
            sb.AppendLine("clip\tword\tcorrect\tcompleted\ttop1\ttop2\trank\ttotal\tH\tL\tM\ttop1Total\tmirrored\thint");
            foreach (var r in rows)
                sb.AppendLine($"{r.Clip}\t{r.Word}\t{(r.Correct ? "Y" : "N")}\t{r.Completed}\t{r.Top1}\t{r.Top2}\t{r.Rank}\t{r.Total:0.00}\t" +
                              $"{r.H:0.00}\t{r.L:0.00}\t{r.M:0.00}\t{r.Top1Total:0.00}\t{r.Mirrored}\t{r.Hint}");
            if (outPath != null) File.WriteAllText(outPath, sb.ToString());
            return rows;
        }

        /// <summary>
        /// Diagnostics for one word: per clip, the closest the contact point gets to the anchor (3D and ignoring depth),
        /// the palm speed there, and the largest palm travel.
        /// </summary>
        public static string Trace(string word)
        {
            SignScorer.ConfigureForPlatform(1f, 0f, 0f);
            var t = TemplateStore.FromDefaults().Get(word);
            var head = new GameObject("HumanReplayHead").transform;
            var prov = new ReplayProvider(head);
            var sb = new StringBuilder();
            foreach (var file in Directory.GetFiles(Dir, word + "_*.json"))
            {
                var clip = JsonUtility.FromJson<ClipJ>(File.ReadAllText(file));
                var tr = new TrajectoryTracker(30f);
                float dt = 1f / Mathf.Max(1f, clip.fps);
                for (int i = 0; i < clip.frames.Length; i++) { prov.Current = clip.frames[i]; tr.Push(prov, i * dt); }
                var f = tr.Frames;
                float best = 9f, bestXY = 9f, speedAt = 0f, travel = 0f;
                Vector3 first = Vector3.zero; bool hasFirst = false;
                for (int i = 0; i < f.Count; i++)
                {
                    if (!f[i].Right.Tracked || !SignAnchors.RightTarget(t, f[i], out var a)) continue;
                    var c = SignAnchors.Contact(f[i].Right, t.contact);
                    float d = Vector3.Distance(c, a), dxy = Vector2.Distance(c, a);
                    if (!hasFirst) { first = f[i].Right.Palm; hasFirst = true; }
                    travel = Mathf.Max(travel, Vector3.Distance(first, f[i].Right.Palm));
                    if (d < best)
                    {
                        best = d; bestXY = Mathf.Min(bestXY, dxy);
                        int j = Mathf.Min(f.Count - 1, i + 3), k = Mathf.Max(0, i - 3);
                        speedAt = f[j].Right.Tracked && f[k].Right.Tracked ? Vector3.Distance(f[j].Right.Palm, f[k].Right.Palm) / Mathf.Max(1e-3f, f[j].Time - f[k].Time) : -1f;
                    }
                    bestXY = Mathf.Min(bestXY, dxy);
                }
                sb.AppendLine($"{Path.GetFileName(file)}: min contact-anchor {best * 100f:0.0} cm (xy {bestXY * 100f:0.0} cm), speed there {speedAt:0.00} m/s, max palm travel {travel * 100f:0} cm");
            }
            UnityEngine.Object.DestroyImmediate(head.gameObject);
            SignScorer.ConfigureForPlatform();
            return sb.ToString();
        }

        /// <summary>One line per word: clips recognised / clips, and the most common failure.</summary>
        public static string Summary(List<Row> rows)
        {
            var sb = new StringBuilder();
            int ok = rows.Count(r => r.Correct);
            sb.AppendLine($"clips recognised {ok}/{rows.Count}; words with >=1 clip recognised {rows.GroupBy(r => r.Word).Count(g => g.Any(r => r.Correct))}/{rows.Select(r => r.Word).Distinct().Count()}");
            sb.AppendLine($"movement completed {rows.Count(r => r.Completed)}/{rows.Count}; target in top-3 {rows.Count(r => r.Rank <= 3)}/{rows.Count}");
            foreach (var g in rows.GroupBy(r => r.Word))
            {
                sb.AppendLine($"{g.Key}: {g.Count(r => r.Correct)}/{g.Count()} | " + string.Join(" ; ", g.Select(r =>
                    $"{(r.Correct ? "OK" : "x")} top1 {r.Top1} r{r.Rank} tot {r.Total:0.00} H{r.H:0.00} L{r.L:0.00} M{r.M:0.00}{(r.Completed ? "" : " " + r.Hint)}{(r.Mirrored ? " (mirrored)" : "")}")));
            }
            return sb.ToString();
        }
    }
}
