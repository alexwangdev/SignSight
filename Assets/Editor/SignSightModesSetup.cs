using SignSight.Explore;
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
    /// v3.3 Task A, applied to Main.unity once (idempotent) and saved: the Start screen's two mode cards
    /// (Learn &amp; Test, Explore) and the Explore table with three primitive objects (apple, cup, bottle).
    /// Menu: SignSight / Apply v3.3 modes.
    /// </summary>
    public static class SignSightModesSetup
    {
        public static readonly Vector3 LearnCardPos = new Vector3(0.045f, 0.05f, 0.9f);
        public static readonly Vector3 ExploreCardPos = new Vector3(0.36f, 0.05f, 0.9f);
        public const float CardDisc = 0.20f; // >= 1.5x the 0.13 lesson disc
        public static readonly Vector3 TablePos = new Vector3(0f, -0.34f, 0.80f);
        public static readonly float[] ObjectX = { -0.28f, 0f, 0.28f };

        [MenuItem("SignSight/Apply v3.3 modes")]
        public static string Apply()
        {
            var log = new System.Text.StringBuilder();
            var root = GameObject.Find("LearnUI").transform;
            var learn = Object.FindFirstObjectByType<LessonManager>();
            var ui = Object.FindFirstObjectByType<LessonUI>();
            var instruction = (TMP_Text)new SerializedObject(ui).FindProperty("instruction").objectReferenceValue;

            // ---- Start cards ----
            var cards = root.Find("StartCards");
            if (cards == null)
            {
                cards = new GameObject("StartCards").transform;
                cards.SetParent(root, false);
                log.AppendLine("created StartCards");
            }
            var lesson1 = root.Find("Buttons/Lesson1Button").gameObject;
            var green = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/LearnButtonGreen.mat");
            MakeCard(cards, lesson1, "LearnCard", LearnCardPos, "lesson", "Learn & Test",
                     "Teacher shows a word · You copy it · Scored", "50 words · 5 lessons", null, learn.StartLearnMode, instruction, log);
            MakeCard(cards, lesson1, "ExploreCard", ExploreCardPos, "eye", "Explore",
                     "Point at something · See its word · Teacher shows its sign", "3 objects to try", green, learn.StartExploreMode, instruction, log);

            // ---- Explore table + objects (primitives only; no floating labels) ----
            var objs = root.Find("ExploreObjects");
            if (objs == null)
            {
                objs = new GameObject("ExploreObjects").transform;
                objs.SetParent(root, false);
                var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
                table.name = "Table";
                Object.DestroyImmediate(table.GetComponent<Collider>()); // the ray should only find the objects
                table.transform.SetParent(objs, false);
                table.transform.localPosition = TablePos;
                table.transform.localScale = new Vector3(0.9f, 0.025f, 0.35f);
                table.GetComponent<Renderer>().sharedMaterial = Mat("ExploreTable", new Color(0.42f, 0.33f, 0.25f));
                float top = TablePos.y + 0.0125f;

                var apple = Obj(objs, "Apple", "apple", new Vector3(ObjectX[0], top, TablePos.z));
                Part(apple, PrimitiveType.Sphere, new Vector3(0, 0.04f, 0), new Vector3(0.08f, 0.075f, 0.08f), Mat("ExploreApple", new Color(0.78f, 0.08f, 0.08f)));
                Part(apple, PrimitiveType.Cylinder, new Vector3(0, 0.085f, 0), new Vector3(0.006f, 0.012f, 0.006f), Mat("ExploreStem", new Color(0.35f, 0.22f, 0.1f)));

                var cup = Obj(objs, "Cup", "cup", new Vector3(ObjectX[1], top, TablePos.z));
                Part(cup, PrimitiveType.Cylinder, new Vector3(0, 0.045f, 0), new Vector3(0.075f, 0.045f, 0.075f), Mat("ExploreCup", new Color(0.92f, 0.92f, 0.9f)));

                var bottle = Obj(objs, "Bottle", "bottle", new Vector3(ObjectX[2], top, TablePos.z));
                Part(bottle, PrimitiveType.Cylinder, new Vector3(0, 0.09f, 0), new Vector3(0.06f, 0.09f, 0.06f), Mat("ExploreBottle", new Color(0.35f, 0.6f, 0.85f)));
                Part(bottle, PrimitiveType.Cylinder, new Vector3(0, 0.19f, 0), new Vector3(0.028f, 0.012f, 0.028f), Mat("ExploreCap", new Color(0.15f, 0.25f, 0.6f)));
                objs.gameObject.SetActive(false);
                log.AppendLine("created ExploreObjects");
            }

            // ---- wiring ----
            var explore = learn.GetComponent<ExploreMode>();
            if (explore == null) explore = learn.gameObject.AddComponent<ExploreMode>();
            var eso = new SerializedObject(explore);
            eso.FindProperty("objectsRoot").objectReferenceValue = objs.gameObject;
            eso.FindProperty("lessons").objectReferenceValue = learn;
            eso.ApplyModifiedPropertiesWithoutUndo();
            var lso = new SerializedObject(learn);
            lso.FindProperty("explore").objectReferenceValue = explore;
            lso.ApplyModifiedPropertiesWithoutUndo();
            var uso = new SerializedObject(ui);
            uso.FindProperty("startCards").objectReferenceValue = cards.gameObject;
            uso.FindProperty("explore").objectReferenceValue = explore;
            uso.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            EditorSceneManager.SaveScene(root.gameObject.scene);
            log.AppendLine("scene saved");
            return log.ToString();
        }

        private static void MakeCard(Transform parent, GameObject from, string name, Vector3 pos, string icon, string title,
                                     string subtitle, string small, Material disc, UnityEngine.Events.UnityAction action,
                                     TMP_Text style, System.Text.StringBuilder log)
        {
            if (parent.Find(name) != null) return;
            var go = Object.Instantiate(from, parent);
            go.name = name;
            go.transform.localPosition = pos;
            var wrap = go.GetComponentInChildren<Oculus.Interaction.InteractableUnityEventWrapper>(true);
            var ev = wrap.WhenSelect;
            while (ev.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(ev, 0);
            UnityEventTools.AddVoidPersistentListener(ev, action);
            EditorUtility.SetDirty(wrap);
            var d = go.transform.Find("Disc");
            d.localScale = new Vector3(CardDisc, d.localScale.y, CardDisc);
            if (disc != null) d.GetComponent<Renderer>().sharedMaterial = disc;
            var label = go.transform.Find("Label").GetComponent<TMP_Text>();
            label.text = title;
            label.fontSize = 0.24f;
            label.rectTransform.sizeDelta = new Vector2(0.19f, 0.05f);
            var bi = go.GetComponent<ButtonIcon>();
            if (bi == null) bi = go.AddComponent<ButtonIcon>();
            bi.Icon = icon;
            bi.Size = 0.055f;
            bi.IconY = 0.035f;
            bi.LabelY = -0.03f;

            // Card panel behind the disc, with the subtitle and the small line on it.
            var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panel.name = "Panel";
            Object.DestroyImmediate(panel.GetComponent<Collider>());
            panel.transform.SetParent(go.transform, false);
            panel.transform.localPosition = new Vector3(0f, -0.065f, 0.015f);
            panel.transform.localScale = new Vector3(0.28f, 0.36f, 1f);
            panel.GetComponent<Renderer>().sharedMaterial = UIStyle.PanelMaterial != null
                ? UIStyle.PanelMaterial : AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/UI/UIPanel.mat");
            panel.AddComponent<RoundedPanel>();

            var sub = NewText(go.transform, "Subtitle", style, 0.16f, new Vector2(0.25f, 0.08f));
            sub.transform.localPosition = new Vector3(0f, -0.15f, -0.002f);
            sub.text = $"{subtitle}\n<size=80%><color=#9DB7FF>{small}</color></size>";
            log.AppendLine("created " + name);
        }

        private static TMP_Text NewText(Transform parent, string name, TMP_Text style, float size, Vector2 rect)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshPro>();
            t.font = style.font;
            t.fontSharedMaterial = style.fontSharedMaterial;
            t.fontSize = size;
            t.color = Color.white;
            t.alignment = TextAlignmentOptions.Top;
            t.enableWordWrapping = true;
            t.rectTransform.sizeDelta = rect;
            return t;
        }

        private static GameObject Obj(Transform parent, string name, string label, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<ExploreObject>().Label = label;
            return go;
        }

        private static void Part(GameObject parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material m)
        {
            var p = GameObject.CreatePrimitive(type);
            p.transform.SetParent(parent.transform, false);
            p.transform.localPosition = pos;
            p.transform.localScale = scale;
            p.GetComponent<Renderer>().sharedMaterial = m;
            // keep the primitive's collider: the pointing ray hits it
        }

        private static Material Mat(string name, Color c)
        {
            string path = $"Assets/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
