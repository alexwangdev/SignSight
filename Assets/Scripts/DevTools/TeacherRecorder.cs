#if UNITY_EDITOR
using System.Collections;
using System.IO;
using SignSight.Hand;
using SignSight.Learn;
using UnityEngine;

namespace SignSight.DevTools
{
    /// <summary>
    /// Editor Play Mode only (not in builds): records the teacher demonstrating each sign, front view, plain
    /// background, 30 fps, as PNG frames (Python turns them into MP4 for the offline model check, SPEC v3.1 §3.2).
    /// Uses Time.captureFramerate so the recording runs at exactly 30 fps of sign time whatever the editor frame rate.
    /// Only the teacher is rendered (its own layer), never the learner's hands.
    /// </summary>
    public class TeacherRecorder : MonoBehaviour
    {
        public const int Fps = 30;
        public int Size = 512;
        public float Speed = 1f;
        public string OutDir = "workspace/goals/meta-vr-start-sign-language-entry/files/teacher-verification/teacher_frames";
        public bool Done { get; private set; }
        public string Log { get; private set; } = "";

        private const int TeacherLayer = 31;

        public static TeacherRecorder Begin(string[] intents, float speed = 1f)
        {
            var go = new GameObject("TeacherRecorder");
            var r = go.AddComponent<TeacherRecorder>();
            r.Speed = speed;
            r.StartCoroutine(r.Run(intents));
            return r;
        }

        private IEnumerator Run(string[] intents)
        {
            var teacher = FindFirstObjectByType<TeacherAvatarDriver>();
            var lessons = FindFirstObjectByType<LessonManager>();
            if (lessons != null) lessons.enabled = false;
            var store = TemplateStore.FromDefaults();

            // Teacher on its own layer; a camera that sees only it, against a plain grey background.
            var layers = new System.Collections.Generic.Dictionary<Transform, int>();
            foreach (var t in teacher.GetComponentsInChildren<Transform>(true))
            {
                layers[t] = t.gameObject.layer;
                t.gameObject.layer = TeacherLayer;
            }
            Transform le = null, re = null;
            foreach (var t in teacher.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Bip01 LEye") le = t;
                if (t.name == "Bip01 REye") re = t;
            }
            Vector3 eyes = (le.position + re.position) * 0.5f;
            var camGo = new GameObject("TeacherRecorderCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.57f, 0.6f);
            cam.cullingMask = 1 << TeacherLayer;
            cam.fieldOfView = 42f;
            cam.nearClipPlane = 0.05f;
            var fwd = teacher.transform.forward;
            var up = teacher.transform.up;
            Vector3 look = eyes - up * 0.30f;
            cam.transform.position = look + fwd * 1.45f;
            cam.transform.rotation = Quaternion.LookRotation(look - cam.transform.position, up);
            cam.enabled = false;
            var rt = new RenderTexture(Size, Size, 24);
            var tex = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            cam.targetTexture = rt;

            Time.captureFramerate = Fps;
            float oldSpeed = teacher.Speed;
            teacher.Speed = Speed;
            foreach (var intent in intents)
            {
                var t = store.Get(intent);
                if (t == null) continue;
                string dir = Path.Combine(OutDir, intent);
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                Directory.CreateDirectory(dir);
                teacher.Rest();
                int frame = 0;
                // 0.3 s at rest, the full demonstration, 0.2 s after.
                for (int i = 0; i < 9; i++) { yield return new WaitForEndOfFrame(); Save(cam, rt, tex, dir, frame++); }
                bool finished = false;
                void OnDone() => finished = true;
                teacher.DemonstrationFinished += OnDone;
                teacher.Demonstrate(t);
                for (int guard = 0; !finished && guard < Fps * 20; guard++)
                {
                    yield return new WaitForEndOfFrame();
                    Save(cam, rt, tex, dir, frame++);
                }
                teacher.DemonstrationFinished -= OnDone;
                for (int i = 0; i < 6; i++) { yield return new WaitForEndOfFrame(); Save(cam, rt, tex, dir, frame++); }
                Log += $"{intent}\t{frame} frames\n";
            }
            teacher.Speed = oldSpeed;
            Time.captureFramerate = 0;
            foreach (var kv in layers) if (kv.Key != null) kv.Key.gameObject.layer = kv.Value;
            cam.targetTexture = null;
            Destroy(rt);
            Destroy(camGo);
            if (lessons != null) lessons.enabled = true;
            File.WriteAllText(Path.Combine(OutDir, "recording.txt"),
                $"fps {Fps}, size {Size}, teacher speed {Speed}x\n" + Log);
            Done = true;
        }

        private static void Save(Camera cam, RenderTexture rt, Texture2D tex, string dir, int frame)
        {
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(dir, $"{frame:0000}.png"), tex.EncodeToPNG());
        }
    }
}
#endif
