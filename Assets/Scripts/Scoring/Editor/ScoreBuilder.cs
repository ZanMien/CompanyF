using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CompanyF.Scoring.EditorTools
{
    /// <summary>Creates the Score manager object and the placeholder results screen (replacing earlier ones).</summary>
    public static class ScoreBuilder
    {
        const string ManagerName = "Score";
        const string CanvasName = "ResultCanvas";

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

            var overlayGo = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
            overlayGo.transform.SetParent(canvasGo.transform, false);
            var overlay = (RectTransform)overlayGo.transform;
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            var veil = overlayGo.GetComponent<Image>();
            veil.color = new Color(0.92f, 0.92f, 0.92f, 0.6f);
            veil.raycastTarget = false;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var title = NewLine(overlay, "TitleText", font, "オーバーヒート", 230f);
            title.color = new Color(0.8f, 0.1f, 0.05f, 1f);
            title.gameObject.SetActive(false);
            var distance = NewLine(overlay, "DistanceText", font, "距離：0m", 70f);
            var points = NewLine(overlay, "ScoreText", font, "スコア：0", -70f);

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
            label.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            return label;
        }
    }
}
