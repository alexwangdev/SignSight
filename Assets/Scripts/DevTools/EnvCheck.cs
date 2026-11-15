#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using SignSight.Learn;
using SignSight.UI;
using UnityEngine;

namespace SignSight.DevTools
{
    /// <summary>
    /// Editor Play Mode only (v3.8): the mode-switch matrix for passthrough and the virtual rooms. Per transition it checks:
    /// no hard cut (the fade is running 0.1 s after the switch and passthrough is still on at that moment), the final
    /// passthrough state, the camera background alpha, and that only the right room is shown. Also counts the room budgets
    /// (shell + props, excluding teacher and UI). Writes Logs/v38-mode-matrix.txt and screenshots to the v3.8 folder.
    /// </summary>
    public class EnvCheck : MonoBehaviour
    {
        public bool Done { get; private set; }
        public readonly StringBuilder Log = new StringBuilder();
        private int _errors;
        private const string ScreenDir = "../workspace/goals/meta-vr-start-sign-language-entry/files/v38-screens";

        public static EnvCheck Begin()
        {
            var c = new GameObject("EnvCheck").AddComponent<EnvCheck>();
            c.StartCoroutine(c.Run());
            return c;
        }

        private void OnEnable() => Application.logMessageReceived += OnLog;
        private void OnDisable() => Application.logMessageReceived -= OnLog;

        private void OnLog(string msg, string stack, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && !msg.Contains("/api/exec"))
            {
                _errors++;
                Log.AppendLine($"  ERROR: {msg}");
            }
        }

        private void Check(string name, bool ok, string detail = "") =>
            Log.AppendLine($"CHECK {name}: {(ok ? "PASS" : "FAIL")}{(detail != "" ? " (" + detail + ")" : "")}");

        private IEnumerator Step(string name, LessonManager lm, EnvironmentMode env, OVRPassthroughLayer pt, Camera eye,
                                 Transform room, Transform cafe, System.Action act, LessonManager.Mode to, bool expectFade, string shot)
        {
            bool wasOn = pt.enabled;
            act();
            yield return new WaitForSecondsRealtime(0.1f);
            bool fadingEarly = env.Fading;
            bool ptStillOn = pt.enabled == wasOn;
            float t0 = Time.realtimeSinceStartup;
            while (env.Fading && Time.realtimeSinceStartup - t0 < 5f) yield return null;
            yield return new WaitForSecondsRealtime(0.1f);

            bool wantVirtual = EnvironmentMode.Wants(to);
            bool bgOk = Mathf.Approximately(eye.backgroundColor.a, wantVirtual ? 1f : 0f);
            bool ok = lm.CurrentMode == to && pt.enabled == !wantVirtual && bgOk && env.VirtualRoom == wantVirtual &&
                      room.gameObject.activeSelf == (to == LessonManager.Mode.Learn) &&
                      cafe.gameObject.activeSelf == (to == LessonManager.Mode.Exam);
            bool noCut = !expectFade || (fadingEarly && ptStillOn);
            Check($"{name}: passthrough {(pt.enabled ? "on" : "off")}, room {(wantVirtual ? "virtual" : "real")}",
                  ok && noCut,
                  $"mode {lm.CurrentMode}, passthrough {pt.enabled}, bg a={eye.backgroundColor.a:0.0}, classroom {room.gameObject.activeSelf}, café {cafe.gameObject.activeSelf}, fade-at-0.1s {fadingEarly}, no-hard-cut {noCut}");
            if (!string.IsNullOrEmpty(shot))
            {
                Directory.CreateDirectory(Path.Combine(Application.dataPath, ScreenDir));
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Application.dataPath, ScreenDir, shot)));
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }

        private static (int tris, int draws) Budget(Transform root)
        {
            int tris = 0, draws = 0;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                draws++;
                var mf = r.GetComponent<MeshFilter>();
                // GetIndexCount works on non-readable meshes (the KayKit FBX meshes are not readable)
                if (mf != null && mf.sharedMesh != null)
                    for (int s = 0; s < mf.sharedMesh.subMeshCount; s++) tris += (int)mf.sharedMesh.GetIndexCount(s) / 3;
            }
            return (tris, draws);
        }

        private static (int tris, int draws) ShellBudget(Transform root)
        {
            var shell = root.Find("RoomShell");
            return shell == null ? (0, 0) : Budget(shell);
        }

        private IEnumerator Run()
        {
            var lm = FindFirstObjectByType<LessonManager>();
            while (lm.Current == LessonManager.Phase.Loading) yield return null;
            var env = lm.GetComponent<EnvironmentMode>();
            var pt = FindFirstObjectByType<OVRPassthroughLayer>();
            var eye = GameObject.Find("CenterEyeAnchor").GetComponent<Camera>();
            var ui = GameObject.Find("LearnUI").transform;
            var room = ui.Find("ClassroomProps");
            var cafe = ui.Find("CafeProps");

            lm.GoStart();
            yield return new WaitForSecondsRealtime(1f);
            Check("boot: Start shows the real room", pt.enabled && !env.VirtualRoom && !env.Fading && eye.backgroundColor.a == 0f,
                  $"passthrough {pt.enabled}, bg a={eye.backgroundColor.a:0.0}");
            yield return Step("Start->Learn", lm, env, pt, eye, room, cafe, () => lm.StartLearnMode(), LessonManager.Mode.Learn, true, "v38-classroom.png");
            yield return Step("Learn->Start", lm, env, pt, eye, room, cafe, () => lm.GoStart(), LessonManager.Mode.Start, true, "v38-start-passthrough.png");
            yield return Step("Start->Exam", lm, env, pt, eye, room, cafe, () => lm.StartExamMode(), LessonManager.Mode.Exam, true, "v38-cafe.png");
            yield return Step("Exam->Start", lm, env, pt, eye, room, cafe, () => lm.GoStart(), LessonManager.Mode.Start, true, "");
            yield return Step("Start->Explore", lm, env, pt, eye, room, cafe, () => lm.StartExploreMode(), LessonManager.Mode.Explore, false, "v38-explore-passthrough.png");
            yield return Step("Explore->Learn", lm, env, pt, eye, room, cafe, () => lm.StartLearnMode(), LessonManager.Mode.Learn, true, "");
            yield return Step("Learn->Relax", lm, env, pt, eye, room, cafe, () => lm.StartRelaxMode(), LessonManager.Mode.Relax, true, "");
            yield return Step("Relax->Learn", lm, env, pt, eye, room, cafe, () => lm.StartLearnMode(), LessonManager.Mode.Learn, true, "");
            yield return Step("Learn->Start (exit)", lm, env, pt, eye, room, cafe, () => lm.GoStart(), LessonManager.Mode.Start, true, "");

            // room budgets (shell + v3.6 props, excluding teacher and UI)
            var (cTris, cDraws) = Budget(cafe);
            var (sTris, sDraws) = ShellBudget(cafe);
            var (rTris, rDraws) = Budget(room);
            var (rsTris, rsDraws) = ShellBudget(room);
            Log.AppendLine($"BUDGET café room: {cTris} tris, {cDraws} draw calls (shell {sTris} tris / {sDraws} draws)");
            Log.AppendLine($"BUDGET classroom room: {rTris} tris, {rDraws} draw calls (shell {rsTris} tris / {rsDraws} draws)");
            Check("budget: shell < 2k tris and < 6 draw calls (both rooms)", sTris < 2000 && sDraws < 6 && rsTris < 2000 && rsDraws < 6);
            Check("budget: whole room < 22k tris and < 26 draw calls (both rooms)", cTris < 22000 && cDraws < 26 && rTris < 22000 && rDraws < 26);

            Check("no errors during the matrix", _errors == 0, $"{_errors} errors");
            Directory.CreateDirectory(Path.Combine(Application.dataPath, ScreenDir));
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, ScreenDir, "v38-mode-matrix.txt")), Log + $"Errors during the check: {_errors}\n");
            Done = true;
        }
    }
}
#endif
