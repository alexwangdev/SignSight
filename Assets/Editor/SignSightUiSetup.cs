using SignSight.Learn;
using SignSight.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SignSight.EditorTools
{
    /// <summary>
    /// v3.2 UI polish, applied to Main.unity once (idempotent) and saved: shared panel / icon / confetti materials,
    /// 1.3x larger pinch discs with icons and 0.19 labels, real SPEED and FACE buttons, a real Tutor panel with a TUTOR
    /// title, dark backings behind the top bar and instruction line. Menu: SignSight / Apply v3.2 UI.
    /// </summary>
    public static class SignSightUiSetup
    {
        private const string ResDir = "Assets/Resources/UI";

        [MenuItem("SignSight/Apply v3.2 UI")]
        public static string Apply()
        {
            var log = new System.Text.StringBuilder();
            Materials(log);
            var ui = Object.FindFirstObjectByType<LessonUI>();
            var root = GameObject.Find("LearnUI").transform; // LessonUI sits on the "Learn" object
            var buttons = root.Find("Buttons");
            var so = new SerializedObject(ui);
            var instruction = (TMP_Text)so.FindProperty("instruction").objectReferenceValue;
            var topBar = (TMP_Text)so.FindProperty("topBar").objectReferenceValue;

            // Buttons: larger discs, larger labels, an icon each.
            foreach (Transform b in buttons)
            {
                var disc = b.Find("Disc");
                if (disc != null && disc.localScale.x < 0.125f) disc.localScale = new Vector3(0.13f, disc.localScale.y, 0.13f);
                var label = b.Find("Label")?.GetComponent<TMP_Text>();
                if (label != null)
                {
                    label.fontSize = 0.19f;
                    label.rectTransform.sizeDelta = new Vector2(0.13f, 0.07f);
                }
                string icon = b.name.StartsWith("Lesson") ? "lesson" : b.name.StartsWith("Replay") ? "replay"
                    : b.name.StartsWith("TryAgain") ? "tryagain" : b.name.StartsWith("Next") ? "next"
                    : b.name.StartsWith("Practice") ? "practice" : b.name.StartsWith("Speed") ? "speed"
                    : b.name.StartsWith("Face") ? "face" : "next";
                var bi = b.GetComponent<ButtonIcon>();
                if (bi == null) bi = b.gameObject.AddComponent<ButtonIcon>();
                bi.Icon = icon;
                EditorUtility.SetDirty(b.gameObject);
            }

            // SPEED and FACE: real buttons in the scene (from the Replay button), wired to LessonUI.
            var replay = buttons.Find("ReplayButton").gameObject;
            var speed = MakeButton(buttons, replay, "SpeedButton", "speed", "1×\n<size=60%>SPEED</size>", ui.ToggleSpeed, log);
            var face = MakeButton(buttons, replay, "FaceButton", "face", "FACE", ui.ReplayFace, log);
            if (face.transform.Find("Caption") == null)
            {
                var cap = NewText(face.transform, "Caption", instruction, 0.085f, new Vector2(0.20f, 0.03f), TextAlignmentOptions.Center);
                cap.transform.localPosition = new Vector3(0f, -0.085f, -0.012f);
                cap.text = $"<color={UIStyle.Amber}>{WordCard.NotScored}</color>";
                TextBacking.For(cap.gameObject, cap);
            }

            // Tutor panel: real panel, TUTOR title, same font size as the instruction line.
            var tutor = root.Find("TutorPanel");
            if (tutor == null)
            {
                tutor = new GameObject("TutorPanel").transform;
                tutor.SetParent(root, false);
                tutor.localPosition = new Vector3(-0.72f, 0.08f, 1.35f);
                var title = NewText(tutor, "Title", instruction, 0.2f, new Vector2(0.48f, 0.04f), TextAlignmentOptions.TopLeft);
                title.transform.localPosition = new Vector3(0f, 0.07f, 0f);
                title.fontStyle = FontStyles.SmallCaps | FontStyles.Bold;
                title.text = "<color=#9DB7FF>TUTOR</color>";
                var body = NewText(tutor, "Body", instruction, instruction.fontSize, new Vector2(0.48f, 0.12f), TextAlignmentOptions.TopLeft);
                body.transform.localPosition = new Vector3(0f, -0.02f, 0f);
                var tb = tutor.gameObject.AddComponent<TextBacking>();
                tb.Texts.Add(title);
                tb.Texts.Add(body);
                tb.MinSize = new Vector2(0.50f, 0.10f);
                log.AppendLine("created TutorPanel");
            }
            so.FindProperty("tutorRoot").objectReferenceValue = tutor.gameObject;
            so.FindProperty("tutorPanel").objectReferenceValue = tutor.Find("Body").GetComponent<TMP_Text>();
            so.FindProperty("speedButton").objectReferenceValue = speed;
            so.FindProperty("faceButton").objectReferenceValue = face;
            so.FindProperty("teacher").objectReferenceValue = Object.FindFirstObjectByType<TeacherAvatarDriver>();
            so.FindProperty("buttonSpacing").floatValue = 0.175f;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Backings behind the bare texts; rounded backings for the existing quads.
            foreach (var t in new[] { topBar, instruction })
            {
                if (t != null && t.GetComponent<TextBacking>() == null) TextBacking.For(t.gameObject, t);
            }
            foreach (var path in new[] { "FeedbackPanel/Hands/Backing" })
            {
                var q = root.Find(path);
                if (q != null && q.GetComponent<RoundedPanel>() == null) q.gameObject.AddComponent<RoundedPanel>();
            }

            EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
            EditorSceneManager.SaveScene(ui.gameObject.scene);
            log.AppendLine("scene saved");
            return log.ToString();
        }

        private static GameObject MakeButton(Transform parent, GameObject from, string name, string icon, string label,
                                             UnityEngine.Events.UnityAction action, System.Text.StringBuilder log)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            var go = Object.Instantiate(from, parent);
            go.name = name;
            var wrap = go.GetComponentInChildren<Oculus.Interaction.InteractableUnityEventWrapper>(true);
            var ev = wrap.WhenSelect;
            while (ev.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(ev, 0);
            UnityEventTools.AddVoidPersistentListener(ev, action);
            EditorUtility.SetDirty(wrap);
            go.transform.Find("Label").GetComponent<TMP_Text>().text = label;
            var bi = go.GetComponent<ButtonIcon>();
            if (bi == null) bi = go.AddComponent<ButtonIcon>();
            bi.Icon = icon;
            log.AppendLine("created " + name);
            return go;
        }

        private static TMP_Text NewText(Transform parent, string name, TMP_Text style, float size, Vector2 rect, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshPro>();
            t.font = style.font;
            t.fontSharedMaterial = style.fontSharedMaterial;
            t.fontSize = size;
            t.color = Color.white;
            t.alignment = align;
            t.enableWordWrapping = true;
            t.rectTransform.sizeDelta = rect;
            return t;
        }

        private static void Materials(System.Text.StringBuilder log)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(ResDir)) AssetDatabase.CreateFolder("Assets/Resources", "UI");
            var dark = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/DarkPanel.mat");
            dark.SetColor("_BaseColor", UIStyle.PanelColor); // one shared panel colour (75 %)
            EditorUtility.SetDirty(dark);

            Material Copy(string name, Color c)
            {
                string path = $"{ResDir}/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(dark) { name = name };
                    AssetDatabase.CreateAsset(m, path);
                    log.AppendLine("created " + path);
                }
                m.SetColor("_BaseColor", c);
                EditorUtility.SetDirty(m);
                return m;
            }
            var panel = Copy("UIPanel", UIStyle.PanelColor);
            panel.renderQueue = 2999; // before the text
            Copy("UIIcon", Color.white);

            string cpath = $"{ResDir}/Confetti.mat";
            var conf = AssetDatabase.LoadAssetAtPath<Material>(cpath);
            if (conf == null)
            {
                conf = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "Confetti" };
                AssetDatabase.CreateAsset(conf, cpath);
                log.AppendLine("created " + cpath);
            }
            conf.SetFloat("_Surface", 1f);
            conf.SetFloat("_Blend", 0f);
            conf.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            conf.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            conf.SetFloat("_ZWrite", 0f);
            conf.SetOverrideTag("RenderType", "Transparent");
            conf.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            conf.renderQueue = 3002;
            EditorUtility.SetDirty(conf);
            AssetDatabase.SaveAssets();
        }
    }
}
