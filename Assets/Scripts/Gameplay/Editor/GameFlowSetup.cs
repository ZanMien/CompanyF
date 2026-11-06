using System.Collections.Generic;
using CompanyF.Train;
using CompanyF.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CompanyF.Gameplay.EditorTools
{
    /// <summary>
    /// Adds the round flow to the Train, its UI to the HUD (replaced if it exists) and a trigger BoxCollider to every
    /// animal that has none. Safe to run again. Also tests the round rules without key presses.
    /// </summary>
    public static class GameFlowSetup
    {
        const string UiRootName = "RoundUI";

        [MenuItem("Tools/Game/Setup Game Flow")]
        public static void Setup()
        {
            var train = GameObject.Find("Train");
            if (train == null) { Debug.LogError("[GameFlow] No 'Train' object in the scene."); return; }

            int added = AddAnimalColliders();

            var flow = GetOrAdd<GameFlow>(train);
            var so = new SerializedObject(flow);
            var list = so.FindProperty("startsWithRound");
            var gated = new Behaviour[]
            {
                train.GetComponent<TrainFollower>(), train.GetComponent<FurnaceController>(), train.GetComponent<TrainControls>(),
                train.GetComponent<CameraPeek>(), train.GetComponent<AnimalHerd>(), train.GetComponent<WhistleWaves>(), train.GetComponent<AnimalSounds>(),
            };
            list.arraySize = gated.Length;
            for (int i = 0; i < gated.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = gated[i];
            so.FindProperty("train").objectReferenceValue = train.GetComponent<TrainFollower>();
            so.FindProperty("boiler").objectReferenceValue = train.GetComponent<BoilerTemperature>();
            so.FindProperty("herd").objectReferenceValue = train.GetComponent<AnimalHerd>();
            so.ApplyModifiedProperties();

            var herd = new SerializedObject(train.GetComponent<AnimalHerd>());
            herd.FindProperty("controls").objectReferenceValue = train.GetComponent<TrainControls>();
            herd.FindProperty("trainCollider").objectReferenceValue = train.transform.Find("Steam_Locomotive").GetComponent<Collider>();
            herd.ApplyModifiedProperties();

            BuildUi(flow, train.GetComponent<TrainFollower>(), train.GetComponent<AnimalHerd>(), train.GetComponent<TrainControls>());

            var result = new SerializedObject(Object.FindFirstObjectByType<GameResultUI>());
            result.FindProperty("flow").objectReferenceValue = flow;
            result.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(train.scene);
            Debug.Log($"[GameFlow] setup done, colliders added to {added} animals");
        }

        static int AddAnimalColliders()
        {
            int added = 0;
            foreach (var tag in new[] { "Cow", "Horse" })
            {
                foreach (var animal in GameObject.FindGameObjectsWithTag(tag))
                {
                    if (animal.GetComponent<Collider>() != null) continue;
                    var box = Undo.AddComponent<BoxCollider>(animal);
                    box.isTrigger = true;
                    FitToModel(box, animal.transform);
                    added++;
                }
            }
            return added;
        }

        /// <summary>Sizes the box around the animal's models, in its own rotated space.</summary>
        static void FitToModel(BoxCollider box, Transform animal)
        {
            bool first = true;
            var local = new Bounds();
            foreach (var renderer in animal.GetComponentsInChildren<Renderer>())
            {
                var b = renderer.bounds;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    var corner = animal.InverseTransformPoint(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z)));
                    if (first) { local = new Bounds(corner, Vector3.zero); first = false; }
                    else local.Encapsulate(corner);
                }
            }
            box.center = local.center;
            box.size = local.size;
        }

        static void BuildUi(GameFlow flow, TrainFollower train, AnimalHerd herd, TrainControls controls)
        {
            var canvas = GameObject.Find("HUDCanvas");
            if (canvas == null) { Debug.LogError("[GameFlow] No HUDCanvas in the scene."); return; }

            var old = canvas.transform.Find(UiRootName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var oldUi = canvas.GetComponent<GameFlowUI>();
            if (oldUi != null) Undo.DestroyObjectImmediate(oldUi);

            var rootGo = new GameObject(UiRootName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(rootGo, "Setup Game Flow");
            rootGo.transform.SetParent(canvas.transform, false);
            var root = (RectTransform)rootGo.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            var message = NewText(root, "MessageText", font, "", 200);
            Place(message.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1600f, 300f));

            var timer = NewText(root, "TimerText", font, "60", 72);
            Place(timer.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(400f, 100f));

            // Progress line just under the countdown.
            var line = NewImage(root, "ProgressLine", null, new Color(0.45f, 0.03f, 0.03f));
            Place(line, new Vector2(0.5f, 1f), new Vector2(0f, -135f), new Vector2(700f, 6f));
            Place(NewImage(line, "StartDot", knob, new Color(0.35f, 0.8f, 1f)), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(26f, 26f));
            Place(NewImage(line, "EndDot", knob, new Color(0.95f, 0.1f, 0.08f)), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(36f, 36f));
            var marker = NewImage(line, "Marker", knob, new Color(1f, 0.55f, 0.1f));
            Place(marker, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(18f, 18f));

            var whistle = NewText(root, "WhistleText", font, "汽笛：OK", 40);
            whistle.alignment = TextAnchor.MiddleLeft;
            Place(whistle.rectTransform, new Vector2(0f, 1f), new Vector2(240f, -50f), new Vector2(400f, 60f));

            var ui = canvas.AddComponent<GameFlowUI>();
            Undo.RegisterCreatedObjectUndo(ui, "Setup Game Flow");
            var so = new SerializedObject(ui);
            so.FindProperty("flow").objectReferenceValue = flow;
            so.FindProperty("train").objectReferenceValue = train;
            so.FindProperty("messageText").objectReferenceValue = message;
            so.FindProperty("timerText").objectReferenceValue = timer;
            so.FindProperty("progressMarker").objectReferenceValue = marker;
            so.FindProperty("herd").objectReferenceValue = herd;
            so.FindProperty("controls").objectReferenceValue = controls;
            so.FindProperty("whistleText").objectReferenceValue = whistle;
            so.ApplyModifiedProperties();
        }

        [MenuItem("Tools/Game/Run Game Flow Self Test")]
        public static void SelfTest()
        {
            const float Dt = 0.01f;
            var log = new List<string>();
            int failures = 0;
            void Check(string what, bool ok)
            {
                log.Add($"{(ok ? "PASS" : "FAIL")}  {what}");
                if (!ok) failures++;
            }

            var root = new GameObject("_GameFlowTest") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var gated = root.AddComponent<WhistleWaves>();
                var flow = root.AddComponent<GameFlow>();
                var so = new SerializedObject(flow);
                so.FindProperty("startsWithRound").arraySize = 1;
                so.FindProperty("startsWithRound").GetArrayElementAtIndex(0).objectReferenceValue = gated;
                so.ApplyModifiedPropertiesWithoutUndo();
                int overs = 0;
                flow.GameOver += () => overs++;
                void Run(int ticks) { for (int i = 0; i < ticks; i++) flow.Tick(Dt); }

                gated.enabled = false;
                Check($"starts on the ready message ({flow.Phase})", flow.Phase == GamePhase.Ready);
                Run(149);
                Check($"1.49 s: still ready, gameplay off ({flow.Phase}, {gated.enabled})", flow.Phase == GamePhase.Ready && !gated.enabled);
                Run(1);
                Check($"1.5 s: running, gameplay on, go message, {flow.SecondsShown} s", flow.Phase == GamePhase.Running && gated.enabled && flow.ShowsGoMessage && flow.SecondsShown == 60);
                Run(49);
                Check("go message still up at 0.49 s", flow.ShowsGoMessage);
                Run(2);
                Check("go message gone after 0.5 s", !flow.ShowsGoMessage);

                Run(100);
                Check($"about 1.5 s in: shows {flow.SecondsShown} (expect 59)", flow.SecondsShown == 59);
                while (flow.SecondsShown > 6) flow.Tick(Dt);
                Check($"6 left: not enlarged ({flow.Emphasised})", !flow.Emphasised);
                while (flow.SecondsShown > 5) flow.Tick(Dt);
                Check($"5 left: enlarged ({flow.Emphasised}), scale {flow.emphasisScale}", flow.Emphasised && Mathf.Approximately(flow.emphasisScale, 2f));

                while (flow.Phase == GamePhase.Running) flow.Tick(Dt);
                Check($"0 left: finish message ({flow.Phase}), {flow.SecondsShown} s, no game over yet ({overs})", flow.Phase == GamePhase.Finishing && flow.SecondsShown == 0 && overs == 0);
                Run(149);
                Check($"1.49 s later: still finish message, game over {overs}", flow.Phase == GamePhase.Finishing && overs == 0);
                Run(2);
                Check($"after 1.5 s: game over once ({overs}), {flow.Phase}", flow.Phase == GamePhase.Ended && overs == 1);

                // Touching an animal.
                var herd = root.AddComponent<AnimalHerd>();
                AnimalHerd.Animal NewAnimal(float z)
                {
                    var go = new GameObject("Animal") { hideFlags = HideFlags.HideAndDontSave };
                    go.transform.SetParent(root.transform, false);
                    go.transform.position = new Vector3(0f, 0f, z);
                    var box = go.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                    box.size = new Vector3(1f, 2f, 3f);
                    return herd.Add(go.transform);
                }
                var first = NewAnimal(50f);
                var second = NewAnimal(100f);
                int struck = 0;
                herd.StruckWithoutWhistle += () => struck++;
                Physics.SyncTransforms();
                var trainBox = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(2.8f, 4f, 11f));
                herd.CheckCollisions(trainBox, false);
                Check($"not touching: nothing happens ({struck})", struck == 0 && first.state == AnimalHerd.AnimalState.Waiting);
                trainBox.center = new Vector3(0f, 1f, 50f);
                herd.CheckCollisions(trainBox, true);
                Check($"touching with the whistle: no failure ({struck}), animal gone ({first.state}, active {first.transform.gameObject.activeSelf})",
                      struck == 0 && first.state == AnimalHerd.AnimalState.Gone && !first.transform.gameObject.activeSelf);
                herd.CheckCollisions(trainBox, false);
                Check($"a vanished animal cannot be hit later ({struck})", struck == 0);
                trainBox.center = new Vector3(0f, 1f, 100f);
                herd.CheckCollisions(trainBox, false);
                Check($"touching without the whistle: failure ({struck}), animal stays ({second.state})", struck == 1 && second.state != AnimalHerd.AnimalState.Gone);

                // The whistle's time limit and cooldown.
                var controls = root.AddComponent<TrainControls>();
                var ringSource = root.AddComponent<WhistleWaves>();
                ringSource.expandTime = 100f;  // rings outlive the blast, so counting them counts emissions
                int rings = 0;
                void Whistle(int ticks, bool held)
                {
                    for (int i = 0; i < ticks; i++)
                    {
                        controls.TickWhistle(Dt, held);
                        ringSource.Tick(Dt, controls.IsWhistling);
                        rings = ringSource.ActiveRadii.Count;
                    }
                }
                Whistle(1, false);
                Check($"key up: ready ({controls.Whistle})", controls.Whistle == TrainControls.WhistleState.Ready);
                Whistle(1, true);
                Check($"press: sounds, first ring ({controls.Whistle}, {rings})", controls.IsWhistling && rings == 1);
                Whistle(98, true);
                Check($"0.98 s held: still sounding, 2 rings ({controls.Whistle}, {rings})", controls.IsWhistling && rings == 2);
                Whistle(2, true);
                Check($"1 s held: stops by itself, cooldown about 5 s ({controls.Whistle}, {controls.WhistleCooldownLeft})", controls.Whistle == TrainControls.WhistleState.Cooldown && controls.WhistleCooldownLeft > 4.98f);
                Whistle(400, true);
                Check($"key still held at 4 s of cooldown: cooling ({controls.Whistle}), never more than 2 rings ({rings})", controls.Whistle == TrainControls.WhistleState.Cooldown && rings == 2);
                Whistle(102, true);
                Check($"cooldown over, key still held: no blast ({controls.Whistle})", controls.Whistle == TrainControls.WhistleState.Ready);
                Whistle(50, true);
                Check($"held on: still no blast ({controls.Whistle})", controls.Whistle == TrainControls.WhistleState.Ready);
                Whistle(1, false);
                Whistle(1, true);
                Check($"release and press again: sounds ({controls.Whistle})", controls.IsWhistling);
                Whistle(30, true);
                Whistle(1, false);
                Check($"released early: cooldown 5 s ({controls.Whistle}, {controls.WhistleCooldownLeft})", controls.Whistle == TrainControls.WhistleState.Cooldown && Mathf.Approximately(controls.WhistleCooldownLeft, 5f));
                Whistle(100, false);
                Whistle(10, true);
                Whistle(10, false);
                Check($"press during cooldown: nothing ({controls.Whistle}, {controls.WhistleCooldownLeft:F2} left)", controls.Whistle == TrainControls.WhistleState.Cooldown && Mathf.Abs(controls.WhistleCooldownLeft - 3.8f) < 0.02f);
                Whistle(400, true);
                Check($"pressed during cooldown and held past its end: no blast ({controls.Whistle})", controls.Whistle == TrainControls.WhistleState.Ready);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            log.Sort((a, b) => b.StartsWith("FAIL").CompareTo(a.StartsWith("FAIL")));
            Debug.Log($"[GameFlow SelfTest] {(failures == 0 ? "ALL PASSED" : failures + " FAILED")}\n" + string.Join("\n", log));
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : Undo.AddComponent<T>(go);
        }

        static Text NewText(Transform parent, string name, Font font, string text, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);
            return label;
        }

        static RectTransform NewImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return (RectTransform)go.transform;
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
