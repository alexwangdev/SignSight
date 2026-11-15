using SignSight.Exam;
using SignSight.Explore;
using SignSight.Learn;
using SignSight.Relax;
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
    /// v3.4 adds the third card (Finger Relax), lays the three cards out (CardLayout, re-applied on every run) and
    /// wires RelaxMode. Menu: SignSight / Apply v3.3 modes (v3.4 Relax).
    /// </summary>
    public static class SignSightModesSetup
    {
        public static readonly Vector3 LearnCardPos = new Vector3(0.045f, 0.05f, 0.9f);
        public static readonly Vector3 ExploreCardPos = new Vector3(0.36f, 0.05f, 0.9f);
        public const float CardDisc = 0.20f; // >= 1.5x the 0.13 lesson disc
        public static readonly Vector3 TablePos = new Vector3(0f, -0.34f, 0.80f);
        public static readonly float[] ObjectX = { -0.28f, 0f, 0.28f };

        /// <summary>v3.4: three cards in one row across the right half (positions found in the Simulator view).</summary>
        public static readonly Vector3[] Card3Pos =
        {
            new Vector3(-0.035f, 0.05f, 0.9f), new Vector3(0.20f, 0.05f, 0.9f), new Vector3(0.435f, 0.05f, 0.9f),
        };
        public const float Card3Disc = 0.165f, Card3Title = 0.21f, Card3Icon = 0.048f, Card3Sub = 0.15f;
        public static readonly Vector2 Card3Panel = new Vector2(0.215f, 0.31f);
        public const float Card3PanelY = -0.06f;
        public static readonly Vector2 Card3SubRect = new Vector2(0.195f, 0.10f);
        public static readonly string[] CardNames = { "LearnCard", "ExploreCard", "RelaxCard", "ExamCard" };

        /// <summary>v3.5: four cards as a 2 x 2 grid on the right half (Learn + Explore on top, Relax + Exam below).</summary>
        public static readonly Vector3[] Card4Pos =
        {
            new Vector3(0.06f, 0.075f, 0.9f), new Vector3(0.31f, 0.075f, 0.9f),
            new Vector3(0.06f, -0.235f, 0.9f), new Vector3(0.31f, -0.235f, 0.9f),
        };
        public static readonly Vector2 Card4Panel = new Vector2(0.23f, 0.28f);
        public const float Card4PanelY = -0.055f;
        public static readonly Vector2 Card4SubRect = new Vector2(0.21f, 0.10f);

        // v3.5 café props (LearnUI local; the teacher stands behind the counter at ExamMode.TeacherCafePosition)
        public static readonly Vector3 CounterPos = new Vector3(-0.30f, -0.78f, 1.25f);   // top at y -0.53
        public static readonly Vector3 CounterSize = new Vector3(0.80f, 0.50f, 0.25f);
        public static readonly Vector3 MenuBoardPos = new Vector3(-0.95f, -0.22f, 1.45f); // lower left, clear of MENU
        public static readonly Vector2 MenuBoardSize = new Vector2(0.34f, 0.30f);
        public static readonly Vector3 CafeCupPos = new Vector3(-0.08f, -0.53f, 1.16f);   // on the counter

        // v3.6: real models from no-login sources (KayKit, CC0). Positions are LearnUI local; the floor is where the
        // teacher stands (y -1.67). Sizes are metres (each model is scaled to it from its import bounds).
        public const string CounterFbx = "Assets/Models/Cafe/kitchencounter_straight_A.fbx";
        public const string CafeTableFbx = "Assets/Models/Cafe/table_round_A.fbx";
        public const string CafeChairFbx = "Assets/Models/Cafe/chair_A.fbx";
        public static readonly Vector3 CafeTablePos = new Vector3(1.50f, -1.67f, 2.45f);
        public static readonly Vector3 CafeTableSize = new Vector3(0.80f, 0.72f, 0.80f);
        public static readonly Vector3[] CafeChairPos = { new Vector3(1.07f, -1.67f, 2.45f), new Vector3(1.93f, -1.67f, 2.45f) };
        public static readonly float[] CafeChairYaw = { 90f, -90f };
        public const float ChairHeight = 0.85f;

        public const string DeskFbx = "Assets/Models/Classroom/table_medium.fbx";
        public const string ClassChairFbx = "Assets/Models/Classroom/chair_A.fbx";
        public const string ShelfFbx = "Assets/Models/Classroom/shelf_A_small.fbx";
        public const string BooksFbx = "Assets/Models/Classroom/book_set.fbx";
        public static readonly Vector3 TeacherDeskPos = new Vector3(-0.95f, -1.67f, 2.05f);
        public static readonly Vector3 TeacherDeskSize = new Vector3(1.10f, 0.75f, 0.55f);
        public static readonly Vector3 BooksPos = new Vector3(-1.15f, -0.92f, 2.05f);   // on the teacher desk
        public const float BooksWidth = 0.30f;
        public static readonly Vector3[] StudentDeskPos = { new Vector3(1.55f, -1.67f, 2.70f), new Vector3(2.20f, -1.67f, 2.70f) };
        public static readonly Vector3 StudentDeskSize = new Vector3(0.60f, 0.72f, 0.45f);
        public static readonly Vector3[] StudentChairPos = { new Vector3(1.55f, -1.67f, 2.35f), new Vector3(2.20f, -1.67f, 2.35f) };
        public static readonly Vector3 ShelfPos = new Vector3(-1.55f, -0.45f, 2.30f);
        public const float ShelfWidth = 0.70f;
        public static readonly Vector3 BlackboardPos = new Vector3(-2.05f, 0.70f, 2.60f); // above the Tutor panel
        public static readonly Vector2 BlackboardSize = new Vector2(1.00f, 0.55f);

        [MenuItem("SignSight/Apply v3.3 modes (v3.4 Relax, v3.5 Exam)")]
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
            MakeCard(cards, lesson1, "RelaxCard", Card3Pos[2], "hand", "Finger Relax",
                     "Stretch your fingers · Shake it out", "About 1 minute · No score", null, learn.StartRelaxMode, instruction, log);
            MakeCard(cards, lesson1, "ExamCard", Card4Pos[3], "cup", "Final Exam",
                     "Order a coffee · Talk to the clerk", "7 answers · 70 points", green, learn.StartExamMode, instruction, log);
            CardLayout(cards);
            var learnSub = cards.Find("LearnCard/Subtitle")?.GetComponent<TMP_Text>();
            if (learnSub != null) learnSub.text = learnSub.text.Replace("50 words · 5 lessons", "62 words · 6 lessons");

            // ---- v3.5 café props: primitives (counter, menu board, cup), inactive outside the exam ----
            var cafe = root.Find("CafeProps");
            if (cafe == null)
            {
                cafe = new GameObject("CafeProps").transform;
                cafe.SetParent(root, false);
                var counter = GameObject.CreatePrimitive(PrimitiveType.Cube);
                counter.name = "Counter";
                Object.DestroyImmediate(counter.GetComponent<Collider>());
                counter.transform.SetParent(cafe, false);
                counter.transform.localPosition = CounterPos;
                counter.transform.localScale = CounterSize;
                counter.GetComponent<Renderer>().sharedMaterial = Mat("CafeCounter", new Color(0.36f, 0.25f, 0.17f));

                var board = GameObject.CreatePrimitive(PrimitiveType.Quad);
                board.name = "MenuBoard";
                Object.DestroyImmediate(board.GetComponent<Collider>());
                board.transform.SetParent(cafe, false);
                board.transform.localPosition = MenuBoardPos;
                board.transform.localScale = new Vector3(MenuBoardSize.x, MenuBoardSize.y, 1f);
                board.GetComponent<Renderer>().sharedMaterial = UIStyle.PanelMaterial != null
                    ? UIStyle.PanelMaterial : AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/UI/UIPanel.mat");
                board.AddComponent<RoundedPanel>();
                var menuText = NewText(cafe, "MenuText", instruction, 0.30f, new Vector2(MenuBoardSize.x - 0.02f, MenuBoardSize.y - 0.02f));
                menuText.transform.localPosition = MenuBoardPos + new Vector3(0f, 0f, -0.003f);
                menuText.alignment = TextAlignmentOptions.Center;
                menuText.text = "<color=#9DB7FF><size=70%>MENU</size></color>\nCOFFEE\nMILK\nWATER";

                var cupGo = new GameObject("CafeCup");
                cupGo.transform.SetParent(cafe, false);
                cupGo.transform.localPosition = CafeCupPos;
                Part(cupGo, PrimitiveType.Cylinder, new Vector3(0, 0.045f, 0), new Vector3(0.075f, 0.045f, 0.075f), Mat("ExploreCup", new Color(0.92f, 0.92f, 0.9f)));
                Part(cupGo, PrimitiveType.Cylinder, new Vector3(0, 0.091f, 0), new Vector3(0.062f, 0.002f, 0.062f), Mat("CafeCoffee", new Color(0.23f, 0.13f, 0.07f)));
                foreach (var c in cupGo.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                cupGo.SetActive(false);
                cafe.gameObject.SetActive(false);
                log.AppendLine("created CafeProps");
            }
            // v3.6: the primitive counter becomes the KayKit counter (same name, same footprint: top at y -0.53)
            var oldCounter = cafe.Find("Counter");
            if (oldCounter != null && oldCounter.GetComponent<MeshFilter>() != null &&
                oldCounter.GetComponent<MeshFilter>().sharedMesh != null && oldCounter.GetComponent<MeshFilter>().sharedMesh.name == "Cube")
            {
                Object.DestroyImmediate(oldCounter.gameObject);
                log.AppendLine("removed primitive counter");
            }
            var counterT = Model(cafe, "Counter", CounterFbx, log);
            FitBox(counterT, new Vector3(CounterPos.x, CounterPos.y - CounterSize.y * 0.5f, CounterPos.z), CounterSize, 0f);
            var cTable = Model(cafe, "CafeTable", CafeTableFbx, log);
            FitBox(cTable, CafeTablePos, CafeTableSize, 0f);
            for (int i = 0; i < 2; i++)
                FitHeight(Model(cafe, "CafeChair" + (i + 1), CafeChairFbx, log), CafeChairPos[i], ChairHeight, CafeChairYaw[i]);

            // ---- v3.6 classroom props (Learn mode only; LessonUI shows / hides them) ----
            var room = root.Find("ClassroomProps");
            if (room == null)
            {
                room = new GameObject("ClassroomProps").transform;
                room.SetParent(root, false);
                var bb = GameObject.CreatePrimitive(PrimitiveType.Quad);
                bb.name = "Blackboard";
                Object.DestroyImmediate(bb.GetComponent<Collider>());
                bb.transform.SetParent(room, false);
                bb.GetComponent<Renderer>().sharedMaterial = UIStyle.PanelMaterial != null
                    ? UIStyle.PanelMaterial : AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/UI/UIPanel.mat");
                bb.AddComponent<RoundedPanel>();
                room.gameObject.SetActive(false);
                log.AppendLine("created ClassroomProps");
            }
            var board2 = room.Find("Blackboard");
            board2.localPosition = BlackboardPos;
            board2.localScale = new Vector3(BlackboardSize.x, BlackboardSize.y, 1f);
            FitBox(Model(room, "TeacherDesk", DeskFbx, log), TeacherDeskPos, TeacherDeskSize, 0f);
            FitWidth(Model(room, "Books", BooksFbx, log), BooksPos, BooksWidth, 0f);
            for (int i = 0; i < 2; i++)
            {
                FitBox(Model(room, "StudentDesk" + (i + 1), DeskFbx, log), StudentDeskPos[i], StudentDeskSize, 0f);
                FitHeight(Model(room, "StudentChair" + (i + 1), ClassChairFbx, log), StudentChairPos[i], ChairHeight, 180f);
            }
            FitWidth(Model(room, "Shelf", ShelfFbx, log), ShelfPos, ShelfWidth, 0f);

            // re-applied on every run, so tuned positions reach the scene
            cafe.Find("MenuBoard").localPosition = MenuBoardPos;
            cafe.Find("MenuText").localPosition = MenuBoardPos + new Vector3(0f, 0f, -0.003f);
            cafe.Find("CafeCup").localPosition = CafeCupPos;

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
            var relax = learn.GetComponent<RelaxMode>();
            if (relax == null) relax = learn.gameObject.AddComponent<RelaxMode>();
            var rso = new SerializedObject(relax);
            rso.FindProperty("lessons").objectReferenceValue = learn;
            GhostHand target = null;
            foreach (var g in Object.FindObjectsByType<GhostHand>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (g.name == "TargetHand") target = g;
            rso.FindProperty("ghostTemplate").objectReferenceValue = target;
            rso.ApplyModifiedPropertiesWithoutUndo();
            var exam = learn.GetComponent<ExamMode>();
            if (exam == null) exam = learn.gameObject.AddComponent<ExamMode>();
            var xso = new SerializedObject(exam);
            xso.FindProperty("lessons").objectReferenceValue = learn;
            xso.FindProperty("cafeRoot").objectReferenceValue = cafe.gameObject;
            xso.FindProperty("cup").objectReferenceValue = cafe.Find("CafeCup").gameObject;
            xso.ApplyModifiedPropertiesWithoutUndo();
            var lso = new SerializedObject(learn);
            lso.FindProperty("explore").objectReferenceValue = explore;
            lso.FindProperty("relax").objectReferenceValue = relax;
            lso.FindProperty("exam").objectReferenceValue = exam;
            lso.ApplyModifiedPropertiesWithoutUndo();
            var uso = new SerializedObject(ui);
            uso.FindProperty("startCards").objectReferenceValue = cards.gameObject;
            uso.FindProperty("explore").objectReferenceValue = explore;
            uso.FindProperty("relax").objectReferenceValue = relax;
            uso.FindProperty("exam").objectReferenceValue = exam;
            uso.FindProperty("classroomProps").objectReferenceValue = room.gameObject;
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

        /// <summary>
        /// v3.4: the three cards side by side on the right half with smaller discs and narrower panels (the teacher
        /// stands left of centre). Safe to run again; also usable at runtime to try positions.
        /// </summary>
        public static void CardLayout(Transform cards)
        {
            for (int i = 0; i < CardNames.Length; i++)
            {
                var go = cards.Find(CardNames[i]);
                if (go == null) continue;
                go.localPosition = Card4Pos[i];
                var d = go.Find("Disc");
                if (d != null) d.localScale = new Vector3(Card3Disc, d.localScale.y, Card3Disc);
                var label = go.Find("Label")?.GetComponent<TMP_Text>();
                if (label != null)
                {
                    label.fontSize = Card3Title;
                    label.rectTransform.sizeDelta = new Vector2(Card3Panel.x - 0.02f, 0.05f);
                }
                var bi = go.GetComponent<ButtonIcon>();
                if (bi != null)
                {
                    bi.Size = Card3Icon;
                    var icon = go.Find("Icon");
                    if (icon != null) icon.localScale = Vector3.one * Card3Icon;
                }
                var panel = go.Find("Panel");
                if (panel != null)
                {
                    panel.localScale = new Vector3(Card4Panel.x, Card4Panel.y, 1f);
                    panel.localPosition = new Vector3(0f, Card4PanelY, panel.localPosition.z);
                }
                var sub = go.Find("Subtitle")?.GetComponent<TMP_Text>();
                if (sub != null)
                {
                    sub.fontSize = Card3Sub;
                    sub.rectTransform.sizeDelta = Card4SubRect;
                }
            }
        }

        // ---------------- v3.6 model placement ----------------
        // Each model is an empty, unrotated holder named 'name' with the FBX instance as its child, so the FBX keeps
        // its own import transform (axis conversion, unit scale); the holder is scaled, turned and placed.

        /// <summary>A holder with an imported FBX under 'parent' (created once, found by name afterwards).</summary>
        private static Transform Model(Transform parent, string name, string fbx, System.Text.StringBuilder log)
        {
            var t = parent.Find(name);
            if (t != null && (PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) || t.childCount == 0))
            {
                Object.DestroyImmediate(t.gameObject); // a v3.6 first-try instance placed without a holder
                t = null;
            }
            if (t != null) return t;
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
            go.name = System.IO.Path.GetFileNameWithoutExtension(fbx);
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            log.AppendLine($"placed {name} ({System.IO.Path.GetFileName(fbx)})");
            return holder;
        }

        /// <summary>Bounds of the holder's meshes in the holder's own space (holder at scale 1, no rotation).</summary>
        private static Bounds RawBounds(Transform holder)
        {
            var b = new Bounds();
            bool first = true;
            foreach (var mf in holder.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = mf.sharedMesh.bounds;
                // mesh -> holder space through the child chain only (the holder's own transform is left out)
                var toHolder = holder.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                foreach (var c in new[] { new Vector3(-1, -1, -1), new Vector3(1, 1, 1), new Vector3(-1, 1, 1), new Vector3(1, -1, 1),
                                          new Vector3(1, 1, -1), new Vector3(-1, -1, 1), new Vector3(1, -1, -1), new Vector3(-1, 1, -1) })
                {
                    var p = toHolder.MultiplyPoint3x4(m.center + Vector3.Scale(m.extents, c));
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        /// <summary>Scales the model to 'size' metres (non-uniform) and puts its bottom-centre at 'bottom'.</summary>
        private static void FitBox(Transform t, Vector3 bottom, Vector3 size, float yaw)
        {
            var b = RawBounds(t);
            Place(t, bottom, new Vector3(size.x / b.size.x, size.y / b.size.y, size.z / b.size.z), b, yaw);
        }

        /// <summary>Uniform scale so the model is 'height' metres tall; bottom-centre at 'bottom'.</summary>
        private static void FitHeight(Transform t, Vector3 bottom, float height, float yaw)
        {
            var b = RawBounds(t);
            float k = height / b.size.y;
            Place(t, bottom, new Vector3(k, k, k), b, yaw);
        }

        /// <summary>Uniform scale so the model is 'width' metres wide; bottom-centre at 'bottom'.</summary>
        private static void FitWidth(Transform t, Vector3 bottom, float width, float yaw)
        {
            var b = RawBounds(t);
            float k = width / b.size.x;
            Place(t, bottom, new Vector3(k, k, k), b, yaw);
        }

        private static void Place(Transform t, Vector3 bottom, Vector3 k, Bounds b, float yaw)
        {
            t.localScale = k;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            // bottom-centre of the bounds, after scaling and turning, goes to 'bottom'
            var off = new Vector3(b.center.x * k.x, b.min.y * k.y, b.center.z * k.z);
            t.localPosition = bottom - t.localRotation * off;
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
