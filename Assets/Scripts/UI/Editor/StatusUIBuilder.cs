using CompanyF.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CompanyF.UI.EditorTools
{
    /// <summary>
    /// Adds the boiler, controls and whistle AudioSource to the Train (only those missing),
    /// creates the status panel (replacing an earlier one) and removes the old speed label of the speed bar.
    /// </summary>
    public static class StatusUIBuilder
    {
        const string CanvasName = "StatusCanvas";

        [MenuItem("Tools/Train/Setup Controls And Status UI")]
        public static void Build()
        {
            var train = GameObject.Find("Train");
            if (train == null) { Debug.LogError("[Status] No 'Train' object in the scene."); return; }

            var boiler = GetOrAdd<BoilerTemperature>(train);
            var controls = GetOrAdd<TrainControls>(train);
            var audio = GetOrAdd<AudioSource>(train);
            audio.playOnAwake = false;
            audio.loop = true;
            if (controls.whistle == null)
            {
                Undo.RecordObject(controls, "Assign whistle");
                controls.whistle = audio;
            }

            var oldSpeed = GameObject.Find("SpeedRangeCanvas/SpeedLabel");
            if (oldSpeed != null) Undo.DestroyObjectImmediate(oldSpeed);

            var old = GameObject.Find(CanvasName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            var canvasGo = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Status UI");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;  // above the speed bar, below the results screen
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Top-right panel: away from the speed bar (bottom) and the combo text (top-left).
            var panelGo = new GameObject("StatusPanel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panel = (RectTransform)panelGo.transform;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1f, 1f);
            panel.anchoredPosition = new Vector2(-30f, -30f);
            panel.sizeDelta = new Vector2(480f, 220f);
            var bg = panelGo.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);
            bg.raycastTarget = false;

            var gear = NewLine(panel, "GearText", font, "ギア：1", 0);
            var speed = NewLine(panel, "SpeedText", font, "速度：15 km/h", 1);
            var temperature = NewLine(panel, "TemperatureText", font, "温度：50 °C", 2);

            var whistleGo = new GameObject("WhistleText", typeof(RectTransform), typeof(Text));
            whistleGo.transform.SetParent(canvasGo.transform, false);
            var whistleRect = (RectTransform)whistleGo.transform;
            whistleRect.anchorMin = whistleRect.anchorMax = whistleRect.pivot = new Vector2(0.5f, 1f);
            whistleRect.anchoredPosition = new Vector2(0f, -40f);
            whistleRect.sizeDelta = new Vector2(500f, 100f);
            var whistle = Style(whistleGo.GetComponent<Text>(), font, "汽笛", 72, TextAnchor.MiddleCenter);
            whistle.fontStyle = FontStyle.Bold;
            whistle.color = new Color(1f, 0.85f, 0.2f, 1f);
            whistleGo.SetActive(false);

            var ui = canvasGo.AddComponent<StatusUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("speedRange").objectReferenceValue = Object.FindFirstObjectByType<SpeedRangeController>();
            so.FindProperty("boiler").objectReferenceValue = boiler;
            so.FindProperty("controls").objectReferenceValue = controls;
            so.FindProperty("gearText").objectReferenceValue = gear;
            so.FindProperty("speedText").objectReferenceValue = speed;
            so.FindProperty("temperatureText").objectReferenceValue = temperature;
            so.FindProperty("whistleText").objectReferenceValue = whistle;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(canvasGo.scene);
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : Undo.AddComponent<T>(go);
        }

        static Text NewLine(RectTransform panel, string name, Font font, string text, int row)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(panel, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -15f - row * 64f);
            rect.sizeDelta = new Vector2(-50f, 64f);
            return Style(go.GetComponent<Text>(), font, text, 48, TextAnchor.MiddleLeft);
        }

        static Text Style(Text label, Font font, string text, int size, TextAnchor align)
        {
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.alignment = align;
            label.color = Color.white;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            return label;
        }
    }
}
