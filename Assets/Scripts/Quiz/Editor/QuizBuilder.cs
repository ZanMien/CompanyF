using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CompanyF.Quiz.EditorTools
{
    /// <summary>
    /// Creates the question asset (only if it does not exist, so edits are kept),
    /// the Quiz manager object and the placeholder quiz UI (replacing earlier ones).
    /// </summary>
    public static class QuizBuilder
    {
        const string AssetPath = "Assets/Data/Quiz/QuizQuestions.asset";
        const string ManagerName = "Quiz";
        const string CanvasName = "QuizCanvas";

        [MenuItem("Tools/Quiz/Create Quiz")]
        public static void Build()
        {
            var questions = LoadOrCreateQuestions();

            foreach (var oldName in new[] { ManagerName, CanvasName })
            {
                var old = GameObject.Find(oldName);
                if (old != null) Undo.DestroyObjectImmediate(old);
            }

            var managerGo = new GameObject(ManagerName);
            Undo.RegisterCreatedObjectUndo(managerGo, "Create Quiz");
            var manager = managerGo.AddComponent<QuizManager>();
            manager.questionSet = questions;

            var canvasGo = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Quiz");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;  // above the speed bar
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var dark = new Color(0.08f, 0.08f, 0.08f, 0.8f);
            var ink = new Color(0.1f, 0.1f, 0.1f, 1f);

            // Full-screen grey-white veil.
            var overlay = NewImage(canvasGo.transform, "Overlay", new Color(0.92f, 0.92f, 0.92f, 0.6f));
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;

            var questionPanel = NewImage(overlay, "QuestionPanel", dark);
            Place(questionPanel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1400f, 150f));
            var question = Fill(NewText(questionPanel, "QuestionText", font, "問題", 60, TextAnchor.MiddleCenter, Color.white));

            Text leftText, rightText, leftPlayers, rightPlayers;
            BuildOption(overlay, "Left", -400f, "P1: A　/　P2: ←", font, dark, out leftText, out leftPlayers);
            BuildOption(overlay, "Right", 400f, "P1: D　/　P2: →", font, dark, out rightText, out rightPlayers);

            var result = NewText(overlay, "ResultText", font, "", 48, TextAnchor.MiddleCenter, ink);
            Place(result, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(1200f, 70f));
            var status = NewText(overlay, "StatusText", font, "", 40, TextAnchor.MiddleCenter, ink);
            Place(status, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(1200f, 60f));
            var countdown = NewText(overlay, "CountdownText", font, "3", 120, TextAnchor.MiddleCenter, ink);
            countdown.GetComponent<Text>().fontStyle = FontStyle.Bold;
            Place(countdown, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(300f, 150f));

            var ui = canvasGo.AddComponent<QuizUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("quiz").objectReferenceValue = manager;
            so.FindProperty("overlay").objectReferenceValue = overlay.gameObject;
            so.FindProperty("questionText").objectReferenceValue = question.GetComponent<Text>();
            so.FindProperty("leftText").objectReferenceValue = leftText;
            so.FindProperty("rightText").objectReferenceValue = rightText;
            so.FindProperty("leftPlayers").objectReferenceValue = leftPlayers;
            so.FindProperty("rightPlayers").objectReferenceValue = rightPlayers;
            so.FindProperty("statusText").objectReferenceValue = status.GetComponent<Text>();
            so.FindProperty("countdownText").objectReferenceValue = countdown.GetComponent<Text>();
            so.FindProperty("resultText").objectReferenceValue = result.GetComponent<Text>();
            so.ApplyModifiedPropertiesWithoutUndo();

            overlay.gameObject.SetActive(false);
            Selection.activeGameObject = managerGo;
            EditorSceneManager.MarkSceneDirty(managerGo.scene);
        }

        static QuizQuestionSet LoadOrCreateQuestions()
        {
            var existing = AssetDatabase.LoadAssetAtPath<QuizQuestionSet>(AssetPath);
            if (existing != null) return existing;

            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            var set = ScriptableObject.CreateInstance<QuizQuestionSet>();
            set.questions.Add(new QuizQuestionSet.Question { text = "どっちをしたい？", left = "過去をやり直す", right = "未来を先に知る" });
            set.questions.Add(new QuizQuestionSet.Question { text = "どっちでいたい？", left = "安定した生活", right = "挑戦し続ける生活" });
            set.questions.Add(new QuizQuestionSet.Question { text = "どっちがいい？", left = "100人に好かれる", right = "1人に深く愛される" });
            AssetDatabase.CreateAsset(set, AssetPath);
            AssetDatabase.SaveAssets();
            return set;
        }

        static void BuildOption(Transform parent, string side, float x, string hint, Font font, Color panelColor, out Text option, out Text players)
        {
            var panel = NewImage(parent, side + "Option", panelColor);
            Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 40f), new Vector2(660f, 260f));

            var text = NewText(panel, side + "Text", font, side, 50, TextAnchor.MiddleCenter, Color.white);
            text.anchorMin = new Vector2(0f, 0.25f);
            text.anchorMax = Vector2.one;
            text.offsetMin = new Vector2(20f, 0f);
            text.offsetMax = new Vector2(-20f, -10f);
            option = text.GetComponent<Text>();
            option.horizontalOverflow = HorizontalWrapMode.Wrap;

            var hintText = NewText(panel, side + "Hint", font, hint, 26, TextAnchor.MiddleCenter, new Color(0.75f, 0.75f, 0.75f, 1f));
            hintText.anchorMin = Vector2.zero;
            hintText.anchorMax = new Vector2(1f, 0.25f);
            hintText.offsetMin = hintText.offsetMax = Vector2.zero;

            var who = NewText(parent, side + "Players", font, "", 52, TextAnchor.MiddleCenter, new Color(0.05f, 0.35f, 0.75f, 1f));
            who.GetComponent<Text>().fontStyle = FontStyle.Bold;
            Place(who, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -140f), new Vector2(660f, 70f));
            players = who.GetComponent<Text>();
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

        static RectTransform NewText(Transform parent, string name, Font font, string text, int size, TextAnchor align, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.alignment = align;
            label.color = color;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            return (RectTransform)go.transform;
        }

        static RectTransform Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
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
