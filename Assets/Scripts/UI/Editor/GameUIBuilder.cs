using CompanyF.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CompanyF.UI.EditorTools
{
    /// <summary>
    /// Sets up the furnace game in the open scene: gameplay components on the Train (boiler and controls are
    /// re-added with default values), the HUD and the end screens (both replaced if they exist).
    /// </summary>
    public static class GameUIBuilder
    {
        const string HudName = "HUDCanvas";
        const string ResultName = "ResultCanvas";
        const string ClearImagePath = "Assets/Image/ResultImage_1019.png";

        static Font font;
        static Sprite knob;

        [MenuItem("Tools/Game/Setup Furnace Game")]
        public static void Build()
        {
            var train = GameObject.Find("Train");
            if (train == null) { Debug.LogError("[Game] No 'Train' object in the scene."); return; }

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            // Gameplay on the train. Boiler and controls are replaced so old serialized values don't linger.
            Replace<BoilerTemperature>(train);
            var controls = Replace<TrainControls>(train);
            GetOrAdd<TemperatureSpeed>(train);
            GetOrAdd<FurnaceController>(train);
            var audio = GetOrAdd<AudioSource>(train);
            audio.playOnAwake = false;
            audio.loop = true;
            controls.whistle = audio;

            foreach (var oldName in new[] { HudName, ResultName })
            {
                var old = GameObject.Find(oldName);
                if (old != null) Undo.DestroyObjectImmediate(old);
            }

            BuildHud();
            BuildResults();
            EditorSceneManager.MarkSceneDirty(train.scene);
        }

        static void BuildHud()
        {
            var canvas = NewCanvas(HudName, 5);

            // Speedometer, bottom right.
            var meter = NewRect(canvas, "Speedometer", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(340f, 340f));
            Stretch(NewImage(meter, "Dial", knob, new Color(0.1f, 0.1f, 0.1f, 0.85f)), 0f);
            var track = Stretch(NewImage(meter, "ArcTrack", knob, new Color(1f, 1f, 1f, 0.15f)), 12f);
            MakeArc(track.GetComponent<Image>(), 240f / 360f);
            var arc = Stretch(NewImage(meter, "SpeedArc", knob, new Color(1f, 0.55f, 0.1f, 1f)), 12f);
            MakeArc(arc.GetComponent<Image>(), 0f);
            Stretch(NewImage(meter, "Inner", knob, new Color(0.07f, 0.07f, 0.07f, 1f)), 36f);

            for (int i = 0; i <= 7; i++)
            {
                float frac = i / 7f;
                float compass = (-120f + 240f * frac) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Sin(compass), Mathf.Cos(compass));
                var tick = NewRect(meter, $"Tick_{15 + i * 20}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), dir * 118f, new Vector2(4f, 16f));
                tick.localEulerAngles = new Vector3(0f, 0f, 120f - 240f * frac);
                tick.gameObject.AddComponent<Image>().color = Color.white;
                var number = NewText(meter, $"Label_{15 + i * 20}", (15 + i * 20).ToString(), 20, TextAnchor.MiddleCenter, new Color(0.85f, 0.85f, 0.85f));
                Place(number.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), dir * 92f, new Vector2(50f, 26f));
            }

            var needle = NewRect(meter, "Needle", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(6f, 112f));
            needle.gameObject.AddComponent<Image>().color = new Color(1f, 0.25f, 0.2f);
            needle.localEulerAngles = new Vector3(0f, 0f, 120f);
            var hub = NewImage(meter, "Hub", knob, new Color(0.85f, 0.85f, 0.85f));
            Place(hub, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));

            var speedText = NewText(meter, "SpeedText", "15 km/h", 40, TextAnchor.MiddleCenter, Color.white);
            speedText.fontStyle = FontStyle.Bold;
            Place(speedText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -82f), new Vector2(260f, 50f));
            var speedLabel = NewText(meter, "SpeedLabel", "速度", 24, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.8f));
            Place(speedLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(120f, 30f));

            // Temperature bar, bottom (left of the speedometer).
            var bar = NewRect(canvas, "TemperatureBar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-120f, 70f), new Vector2(1100f, 36f));
            var gradientGo = new GameObject("Gradient", typeof(RectTransform), typeof(RawImage));
            gradientGo.transform.SetParent(bar, false);
            Stretch((RectTransform)gradientGo.transform, 0f);
            var gradient = gradientGo.GetComponent<RawImage>();
            gradient.raycastTarget = false;

            var pointer = NewRect(bar, "Pointer", new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, 26f));
            pointer.anchorMin = new Vector2(0f, 0f);
            pointer.anchorMax = new Vector2(0f, 1f);
            pointer.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 1f);
            var temperatureText = NewText(pointer, "TemperatureText", "50 °C", 34, TextAnchor.LowerCenter, Color.white);
            temperatureText.fontStyle = FontStyle.Bold;
            Place(temperatureText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(220f, 44f));

            var minLabel = NewText(bar, "MinLabel", "50 °C", 28, TextAnchor.UpperCenter, Color.white);
            Place(minLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(180f, 36f));
            var maxLabel = NewText(bar, "MaxLabel", "1500 °C", 28, TextAnchor.UpperCenter, new Color(1f, 0.15f, 0.1f));
            Place(maxLabel.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(180f, 36f));

            // Furnace status, upper centre.
            var door = NewText(canvas, "DoorText", "閉", 80, TextAnchor.MiddleCenter, Color.white);
            door.fontStyle = FontStyle.Bold;
            Place(door.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(900f, 100f));
            var flash = NewText(canvas, "FlashText", "", 60, TextAnchor.MiddleCenter, Color.white);
            flash.fontStyle = FontStyle.Bold;
            Place(flash.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(900f, 80f));
            var fail = NewText(canvas, "FailText", "2P 失敗", 60, TextAnchor.MiddleCenter, new Color(1f, 0.2f, 0.15f));
            fail.fontStyle = FontStyle.Bold;
            Place(fail.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -350f), new Vector2(900f, 80f));
            fail.gameObject.SetActive(false);

            var hud = canvas.gameObject.AddComponent<GameHUD>();
            var so = new SerializedObject(hud);
            so.FindProperty("needle").objectReferenceValue = needle;
            so.FindProperty("speedArc").objectReferenceValue = arc.GetComponent<Image>();
            so.FindProperty("speedText").objectReferenceValue = speedText;
            so.FindProperty("barGradient").objectReferenceValue = gradient;
            so.FindProperty("temperaturePointer").objectReferenceValue = pointer;
            so.FindProperty("temperatureText").objectReferenceValue = temperatureText;
            so.FindProperty("minLabel").objectReferenceValue = minLabel;
            so.FindProperty("maxLabel").objectReferenceValue = maxLabel;
            so.FindProperty("doorText").objectReferenceValue = door;
            so.FindProperty("flashText").objectReferenceValue = flash;
            so.FindProperty("failText").objectReferenceValue = fail;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildResults()
        {
            var canvas = NewCanvas(ResultName, 20);

            // CLEAR: the result picture, whole and at its own proportions; black fills any bars.
            var clear = Stretch(NewImage(canvas, "ClearPanel", null, Color.black), 0f);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ClearImagePath);
            if (sprite == null) Debug.LogWarning($"[Game] No sprite at {ClearImagePath}; set its Texture Type to Sprite (2D and UI).");
            var background = NewImage(clear, "Background", sprite, Color.white);
            var fitter = background.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = sprite != null ? sprite.rect.width / sprite.rect.height : 16f / 9f;
            var clearText = NewText(background, "ClearText", "CLEAR", 180, TextAnchor.MiddleCenter, new Color(1f, 0.95f, 0.84f));
            clearText.fontStyle = FontStyle.Bold;
            Stretch(clearText.rectTransform, 0f);
            clear.gameObject.SetActive(false);

            // GAMEOVER: grey-white veil over the stopped scene.
            var over = Stretch(NewImage(canvas, "GameOverPanel", null, new Color(0.92f, 0.92f, 0.92f, 0.6f)), 0f);
            var overText = NewText(over, "GameOverText", "GAMEOVER", 180, TextAnchor.MiddleCenter, new Color(0.75f, 0.08f, 0.05f));
            overText.fontStyle = FontStyle.Bold;
            Stretch(overText.rectTransform, 0f);
            over.gameObject.SetActive(false);

            var ui = canvas.gameObject.AddComponent<GameResultUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("clearPanel").objectReferenceValue = clear.gameObject;
            so.FindProperty("gameOverPanel").objectReferenceValue = over.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : Undo.AddComponent<T>(go);
        }

        static T Replace<T>(GameObject go) where T : Component
        {
            var old = go.GetComponent<T>();
            if (old != null) Undo.DestroyObjectImmediate(old);
            return Undo.AddComponent<T>(go);
        }

        static RectTransform NewCanvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(go, "Setup Furnace Game");
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return (RectTransform)go.transform;
        }

        static RectTransform NewRect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Place(rect, anchor, pivot, position, size);
            return rect;
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

        static Text NewText(Transform parent, string name, string text, int size, TextAnchor align, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.alignment = align;
            label.color = color;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);
            return label;
        }

        /// <summary>Radial arc starting at the lower left (−120° from up) and running clockwise.</summary>
        static void MakeArc(Image image, float amount)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Bottom;
            image.fillClockwise = true;
            image.fillAmount = amount;
            image.rectTransform.localEulerAngles = new Vector3(0f, 0f, -60f);
        }

        static RectTransform Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
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
