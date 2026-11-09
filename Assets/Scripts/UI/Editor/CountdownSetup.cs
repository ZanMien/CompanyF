using System;
using CompanyF.Gameplay;
using CompanyF.Gameplay.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CompanyF.UI.EditorTools
{
    public static class CountdownSetup
    {
        [MenuItem("Tools/Game/Setup Spec Countdowns")]
        public static void Setup()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/SampleScene.unity") throw new InvalidOperationException("Open SampleScene first.");
            var ui = UnityEngine.Object.FindFirstObjectByType<GameFlowUI>();
            var flow = UnityEngine.Object.FindFirstObjectByType<GameFlow>();
            if (ui == null || flow == null) throw new InvalidOperationException("Game flow UI missing.");
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Countdown/RoundedMplus1c-Bold.ttf");
            if (font == null) throw new InvalidOperationException("Specified font missing.");
            Undo.RecordObject(flow, "Countdown durations");
            flow.readyTime = 1f; flow.goTime = 1.2f; flow.finishMessageTime = 1f;
            EditorUtility.SetDirty(flow);
            var uiSo = new SerializedObject(ui);
            var legacy = uiSo.FindProperty("messageText").objectReferenceValue as UnityEngine.UI.Text;
            if (legacy != null) { Undo.RecordObject(legacy, "Use countdown overlay"); legacy.enabled = false; EditorUtility.SetDirty(legacy); }

            var root = Rect(ui.transform, "CountdownOverlay", Vector2.zero, Vector2.zero);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            root.SetAsLastSibling();
            var leverRoot = Rect(root, "Levers", new Vector2(0, 0), new Vector2(1000, 300));
            var leverGroup = Group(leverRoot);
            var circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            for (int side = -1; side <= 1; side += 2)
            {
                string name = side < 0 ? "Left" : "Right";
                var lever = Rect(leverRoot, name, new Vector2(side * 340, 0), new Vector2(160, 160));
                Image(lever, new Color32(166, 134, 190, 210), circle);
                var stem = Rect(lever, "Stem", new Vector2(0, -2), new Vector2(26, 115));
                stem.localRotation = Quaternion.Euler(0, 0, side * -15);
                Image(stem, new Color32(210, 88, 40, 255));
                Image(Rect(stem, "Grip", new Vector2(0, 47), new Vector2(62, 30)), new Color32(237, 112, 69, 255));
                Image(Rect(lever, "Base", new Vector2(0, -55), new Vector2(100, 20)), new Color32(133, 60, 40, 255));
            }
            var railRoot = Rect(root, "Rails", new Vector2(0, -120), new Vector2(900, 100));
            var railGroup = Group(railRoot);
            for (int i = 0; i < 13; i++) Image(Rect(railRoot, "Tie" + i, new Vector2(-420 + i * 70, 0), new Vector2(24, 95)), new Color32(133, 60, 40, 255));
            Image(Rect(railRoot, "UpperRail", new Vector2(0, 27), new Vector2(900, 12)), new Color32(237, 174, 69, 255));
            Image(Rect(railRoot, "LowerRail", new Vector2(0, -27), new Vector2(900, 12)), new Color32(237, 174, 69, 255));
            var textRect = Rect(root, "CountdownText", Vector2.zero, new Vector2(1700, 360));
            var text = GetOrAdd<UnityEngine.UI.Text>(textRect.gameObject);
            text.font = font; text.fontStyle = FontStyle.Normal; text.fontSize = 150;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = "";
            var border = GetOrAdd<UnityEngine.UI.Outline>(text.gameObject);
            border.effectDistance = new Vector2(4, -4);
            var bevel = text.GetComponents<UnityEngine.UI.Shadow>();
            UnityEngine.UI.Shadow highlight = null;
            foreach (var shadow in bevel) if (!(shadow is UnityEngine.UI.Outline)) highlight = shadow;
            if (highlight == null) highlight = Undo.AddComponent<UnityEngine.UI.Shadow>(text.gameObject);
            highlight.effectDistance = new Vector2(-2, 2);
            var presentation = GetOrAdd<CountdownPresentation>(ui.gameObject);
            var so = new SerializedObject(presentation);
            so.FindProperty("flow").objectReferenceValue = flow;
            so.FindProperty("text").objectReferenceValue = text;
            so.FindProperty("textGroup").objectReferenceValue = Group(textRect);
            so.FindProperty("levers").objectReferenceValue = leverGroup;
            so.FindProperty("rails").objectReferenceValue = railGroup;
            so.FindProperty("border").objectReferenceValue = border;
            so.FindProperty("bevel").objectReferenceValue = highlight;
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
            Debug.Log("[Countdown Setup] configured 1s ready, 1.2s go, 5-to-1 countdown and 1s finish.");
        }

        static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var found = parent.Find(name) as RectTransform;
            if (found == null)
            {
                var go = new GameObject(name, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "Countdown UI");
                found = (RectTransform)go.transform; found.SetParent(parent, false);
            }
            found.anchorMin = found.anchorMax = found.pivot = new Vector2(0.5f, 0.5f);
            found.anchoredPosition = position; found.sizeDelta = size;
            return found;
        }
        static void Image(RectTransform rect, Color colour, Sprite sprite = null)
        {
            var image = GetOrAdd<UnityEngine.UI.Image>(rect.gameObject);
            image.color = colour; image.sprite = sprite; image.raycastTarget = false;
        }
        static CanvasGroup Group(RectTransform rect)
        {
            var group = GetOrAdd<CanvasGroup>(rect.gameObject);
            group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false;
            return group;
        }
        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            if (component == null) component = Undo.AddComponent<T>(go);
            return component;
        }

        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            Setup();
            GameFlowSetup.SelfTest();
            CountdownSelfTest.Run();
            CountdownSelfTest.StartPlayVerification();
        }
    }
}
