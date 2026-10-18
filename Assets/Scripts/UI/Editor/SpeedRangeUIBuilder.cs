using CompanyF.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CompanyF.UI.EditorTools
{
    /// <summary>Creates the placeholder RANGE UI and the controller object in the open scene (replacing earlier ones).</summary>
    public static class SpeedRangeUIBuilder
    {
        const string ControllerName = "SpeedRange";
        const string CanvasName = "SpeedRangeCanvas";

        [MenuItem("Tools/Speed Range/Create UI")]
        public static void Build()
        {
            foreach (var oldName in new[] { ControllerName, CanvasName })
            {
                var old = GameObject.Find(oldName);
                if (old != null) Undo.DestroyObjectImmediate(old);
            }

            var controllerGo = new GameObject(ControllerName);
            Undo.RegisterCreatedObjectUndo(controllerGo, "Create Speed Range UI");
            var controller = controllerGo.AddComponent<SpeedRangeController>();

            var canvasGo = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Speed Range UI");
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root = canvasGo.transform;

            // Bar: bottom centre, 900 x 44.
            var bar = NewImage(root, "RangeBar", new Color(0.12f, 0.12f, 0.12f, 0.85f));
            Place(bar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(900f, 44f));

            var zone = NewImage(bar, "SuccessZone", new Color(0.25f, 0.8f, 0.35f, 0.9f));
            zone.anchorMin = new Vector2(0.4f, 0f);
            zone.anchorMax = new Vector2(0.6f, 1f);
            zone.offsetMin = zone.offsetMax = Vector2.zero;

            var pointer = NewImage(bar, "Pointer", Color.white);
            pointer.anchorMin = new Vector2(0f, 0f);
            pointer.anchorMax = new Vector2(0f, 1f);
            pointer.sizeDelta = new Vector2(6f, 20f);
            pointer.anchoredPosition = Vector2.zero;

            var zero = NewText(bar, "Label_0", font, "0", 24, TextAnchor.UpperCenter);
            Place(zero, new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(80f, 30f));
            var hundred = NewText(bar, "Label_100", font, "100", 24, TextAnchor.UpperCenter);
            Place(hundred, new Vector2(1f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(80f, 30f));

            var player = NewText(bar, "PlayerLabel", font, "P1", 48, TextAnchor.MiddleRight);
            player.GetComponent<Text>().fontStyle = FontStyle.Bold;
            Place(player, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(120f, 60f));

            var result = NewText(bar, "ResultLabel", font, "", 36, TextAnchor.LowerCenter);
            Place(result, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(600f, 50f));

            // Debug info: top left.
            var combo = NewText(root, "ComboLabel", font, "COMBO 0", 32, TextAnchor.UpperLeft);
            Place(combo, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(500f, 44f));

            var ui = canvasGo.AddComponent<SpeedRangeUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("controller").objectReferenceValue = controller;
            so.FindProperty("zone").objectReferenceValue = zone;
            so.FindProperty("pointer").objectReferenceValue = pointer;
            so.FindProperty("playerLabel").objectReferenceValue = player.GetComponent<Text>();
            so.FindProperty("comboLabel").objectReferenceValue = combo.GetComponent<Text>();
            so.FindProperty("resultLabel").objectReferenceValue = result.GetComponent<Text>();
            so.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = controllerGo;
            EditorSceneManager.MarkSceneDirty(controllerGo.scene);
        }

        static RectTransform NewImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return (RectTransform)go.transform;
        }

        static RectTransform NewText(Transform parent, string name, Font font, string text, int size, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.alignment = align;
            label.color = Color.white;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            return (RectTransform)go.transform;
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
