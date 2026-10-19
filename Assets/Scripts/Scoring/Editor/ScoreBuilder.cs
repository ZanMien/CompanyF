using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CompanyF.Scoring.EditorTools
{
    /// <summary>Creates the Score manager object and the results screen (replacing earlier ones).</summary>
    public static class ScoreBuilder
    {
        const string ManagerName = "Score";
        const string CanvasName = "ResultCanvas";
        const string BackgroundPath = "Assets/Image/ResultImage_1019.png";

        [MenuItem("Tools/Score/Create Score And Result UI")]
        public static void Build()
        {
            foreach (var oldName in new[] { ManagerName, CanvasName })
            {
                var old = GameObject.Find(oldName);
                if (old != null) Undo.DestroyObjectImmediate(old);
            }

            var managerGo = new GameObject(ManagerName);
            Undo.RegisterCreatedObjectUndo(managerGo, "Create Score");
            var manager = managerGo.AddComponent<ScoreManager>();

            var canvasGo = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Score");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;  // above the speed bar
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Full-screen container shown at game end; its colour fills the bars beside/above the picture.
            var overlayGo = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
            overlayGo.transform.SetParent(canvasGo.transform, false);
            var overlay = (RectTransform)overlayGo.transform;
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            var bars = overlayGo.GetComponent<Image>();
            bars.color = Color.black;
            bars.raycastTarget = false;

            // Background picture: always shown whole, at its own proportions, as large as the screen allows.
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
            if (sprite == null) Debug.LogWarning($"[Score] No sprite at {BackgroundPath}; set its Texture Type to Sprite (2D and UI).");
            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            bgGo.transform.SetParent(overlay, false);
            var bg = bgGo.GetComponent<Image>();
            bg.sprite = sprite;
            bg.color = Color.white;
            bg.raycastTarget = false;
            var fitter = bgGo.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = sprite != null ? sprite.rect.width / sprite.rect.height : 16f / 9f;
            var background = (RectTransform)bgGo.transform;

            // Text sits on the picture so it stays on the same planks whatever the screen shape.
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var title = NewLine(background, "TitleText", font, "オーバーヒート", 230f);
            title.color = new Color(0.9f, 0.12f, 0.05f, 1f);
            title.gameObject.SetActive(false);
            var distance = NewLine(background, "DistanceText", font, "距離：0m", 70f);
            var points = NewLine(background, "ScoreText", font, "スコア：0", -70f);

            var ui = canvasGo.AddComponent<ResultUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("score").objectReferenceValue = manager;
            so.FindProperty("overlay").objectReferenceValue = overlayGo;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("distanceText").objectReferenceValue = distance;
            so.FindProperty("scoreText").objectReferenceValue = points;
            so.ApplyModifiedPropertiesWithoutUndo();

            overlayGo.SetActive(false);
            Selection.activeGameObject = managerGo;
            EditorSceneManager.MarkSceneDirty(managerGo.scene);
        }

        static Text NewLine(Transform parent, string name, Font font, string text, float y)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(1600f, 130f);
            var label = go.GetComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = 96;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 0.95f, 0.84f, 1f);  // cream, readable on the wooden board
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.22f, 0.11f, 0.04f, 1f);
            outline.effectDistance = new Vector2(3f, -3f);
            return label;
        }
    }
}
