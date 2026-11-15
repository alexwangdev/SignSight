#if UNITY_EDITOR
using System.Collections;
using System.IO;
using SignSight.Hand;
using SignSight.Learn;
using UnityEngine;

namespace SignSight.DevTools
{
    /// <summary>
    /// Editor Play Mode only: renders a close-up of the teacher's face (and head) at the middle of each sign, to check
    /// the expressions by looking at them (v3.2 B6). Waits a frame after posing (skinned meshes apply blendshapes once
    /// per frame). Writes one PNG per word plus a contact sheet (Logs/v32-faces/sheet.png); Done when finished.
    /// </summary>
    public class FaceShots : MonoBehaviour
    {
        public bool Done { get; private set; }
        public static bool FaceOnly;
        public string SheetPath { get; private set; }

        public static FaceShots Begin(string[] intents, string outDir = "Logs/v32-faces", float u = 0.5f, float distance = 0.55f)
        {
            var r = new GameObject("FaceShots").AddComponent<FaceShots>();
            r.StartCoroutine(r.Run(intents, outDir, u, distance));
            return r;
        }

        private IEnumerator Run(string[] intents, string outDir, float u, float distance)
        {
            Directory.CreateDirectory(outDir);
            var teacher = FindFirstObjectByType<TeacherAvatarDriver>();
            var lessons = FindFirstObjectByType<LessonManager>();
            if (lessons != null) lessons.enabled = false;
            teacher.Setup();
            Transform le = null, re = null;
            foreach (var t in teacher.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Bip01 LEye") le = t;
                if (t.name == "Bip01 REye") re = t;
            }
            var eyes = (le.position + re.position) * 0.5f;
            var cam = new GameObject("FaceShotCam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.57f, 0.6f);
            cam.fieldOfView = 22f;
            cam.nearClipPlane = 0.05f;
            var look = eyes - teacher.transform.up * 0.04f;
            cam.transform.position = look + teacher.transform.forward * distance;
            cam.transform.rotation = Quaternion.LookRotation(look - cam.transform.position, teacher.transform.up);
            cam.enabled = false;
            const int S = 256;
            var rt = new RenderTexture(S, S, 24);
            cam.targetTexture = rt;
            var tex = new Texture2D(S, S, TextureFormat.RGB24, false);
            var sheet = new Texture2D(S * intents.Length, S, TextureFormat.RGB24, false);
            for (int i = 0; i < intents.Length; i++)
            {
                var tpl = TemplateStore.Canonical(intents[i]);
                if (tpl == null) continue;
                if (FaceOnly)
                {
                    // The FACE button's replay (face + head only, hands at rest), captured partway through.
                    teacher.DemonstrateFace(tpl, 0.5f);
                    yield return new WaitForSeconds(1.0f);
                    yield return new WaitForEndOfFrame();
                }
                else
                {
                    teacher.PoseAt(tpl, u);
                    yield return null;                  // let the skinned mesh pick up the blendshapes
                    teacher.PoseAt(tpl, u);             // and the bones (other scripts may have run this frame)
                    yield return new WaitForEndOfFrame();
                }
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, S, S), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(outDir, intents[i] + ".png"), tex.EncodeToPNG());
                sheet.SetPixels(i * S, 0, S, S, tex.GetPixels());
            }
            sheet.Apply();
            SheetPath = Path.Combine(outDir, "sheet.png");
            File.WriteAllBytes(SheetPath, sheet.EncodeToPNG());
            teacher.Rest();
            cam.targetTexture = null;
            Destroy(rt);
            Destroy(cam.gameObject);
            if (lessons != null) lessons.enabled = true;
            Done = true;
        }
    }
}
#endif
