#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using SignSight.Learn;
using SignSight.UI;
using UnityEngine;

namespace SignSight.DevTools
{
    /// <summary>
    /// Editor Play Mode only (v3.10 Task A): the room switch Start → Exam (classroom → café) frame by frame. Checks that the
    /// old room's props stay visible until the screen is black, that the new room appears only at black, and that the
    /// fade-out, black and fade-in captures are the expected ones. Writes Logs-independent files to the v3.10 screens folder:
    /// v310-fade-start.png (~0.1 s), v310-fade-black.png (the frame after the switch), v310-fade-end.png (fade finished).
    /// </summary>
    public class FadeCheck : MonoBehaviour
    {
        public bool Done { get; private set; }
        public readonly StringBuilder Log = new StringBuilder();
        private const string ScreenDir = "../workspace/goals/meta-vr-start-sign-language-entry/files/v310-screens";

        public static FadeCheck Begin()
        {
            var c = new GameObject("FadeCheck").AddComponent<FadeCheck>();
            c.StartCoroutine(c.Run());
            return c;
        }

        private static float Alpha(EnvironmentMode env)
        {
            var f = typeof(EnvironmentMode).GetField("_fadeMat", BindingFlags.NonPublic | BindingFlags.Instance);
            var m = f?.GetValue(env) as Material;
            return m == null ? -1f : m.GetColor("_BaseColor").a;
        }

        private static void Shot(string name)
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, ScreenDir));
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Application.dataPath, ScreenDir, name)));
        }

        private void Check(string name, bool ok, string detail = "") =>
            Log.AppendLine($"CHECK {name}: {(ok ? "PASS" : "FAIL")}{(detail != "" ? " (" + detail + ")" : "")}");

        private IEnumerator Run()
        {
            var lm = FindFirstObjectByType<LessonManager>();
            while (lm.Current == LessonManager.Phase.Loading) yield return null;
            var env = lm.GetComponent<EnvironmentMode>();
            var ui = GameObject.Find("LearnUI").transform;
            var classroom = ui.Find("ClassroomProps").gameObject;
            var cafe = ui.Find("CafeProps").gameObject;

            lm.GoStart();
            yield return new WaitForSecondsRealtime(1.5f);
            Check("start: classroom shown, café hidden", classroom.activeSelf && !cafe.activeSelf);

            float t0 = Time.realtimeSinceStartup;
            lm.StartExamMode();
            bool shotStart = false, shotBlack = false, oldRoomKeptUntilBlack = true, newRoomOnlyAtBlack = true;
            float alphaAtStart = -1f, alphaAtBlack = -1f;
            bool classroomAtStart = false;
            int frames = 0;
            while (Time.realtimeSinceStartup - t0 < 3f)
            {
                float el = Time.realtimeSinceStartup - t0;
                float a = Alpha(env);
                bool swapped = env.CurrentRoom == Room.Cafe;
                if (!swapped)
                {
                    // the old room must be fully there until the switch
                    if (!classroom.activeSelf) oldRoomKeptUntilBlack = false;
                }
                else if (cafe.activeSelf == false) newRoomOnlyAtBlack = false;
                frames++;
                Log.AppendLine($"  frame {frames,3} t={el:0.000}s alpha={a:0.00} room={env.CurrentRoom} classroom={classroom.activeSelf} cafe={cafe.activeSelf} fading={env.Fading}");

                // the start shot is taken on the fade's own progress (first frame with alpha >= 0.2 before the switch),
                // not on the wall clock: Play Mode's first frames stall, and a wall-clock 0.1 s can land after the swap
                if (!shotStart && !swapped && a >= 0.2f)
                {
                    Shot("v310-fade-start.png");
                    shotStart = true;
                    alphaAtStart = a;
                    classroomAtStart = classroom.activeSelf;
                }
                if (!shotBlack && swapped)
                {
                    Shot("v310-fade-black.png");
                    shotBlack = true;
                    alphaAtBlack = a;
                }
                if (shotBlack && !env.Fading) break;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.2f);
            Shot("v310-fade-end.png");

            Check("fade-start capture: old room (classroom) fully shown, screen dimming (alpha 0.1-0.6)", shotStart && alphaAtStart > 0.1f && alphaAtStart < 0.6f && classroomAtStart,
                  $"alpha {alphaAtStart:0.00}");
            Check("old room props stay until black (no empty frame)", oldRoomKeptUntilBlack);
            Check("new room props appear only at black", newRoomOnlyAtBlack && shotBlack, $"alpha at black {alphaAtBlack:0.00}");
            Check("black capture: screen near black, café shown, classroom gone", shotBlack && alphaAtBlack > 0.8f && cafe.activeSelf && !classroom.activeSelf,
                  $"alpha {alphaAtBlack:0.00}");
            Check("end: fade finished, café shown", !env.Fading && cafe.activeSelf && !classroom.activeSelf && Alpha(env) == 0f);

            Directory.CreateDirectory(Path.Combine(Application.dataPath, ScreenDir));
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, ScreenDir, "v310-fade-check.txt")), Log.ToString());
            lm.GoStart();
            Done = true;
        }
    }
}
#endif
