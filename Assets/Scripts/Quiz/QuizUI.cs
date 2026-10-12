using UnityEngine;
using UnityEngine.UI;

namespace CompanyF.Quiz
{
    /// <summary>
    /// Placeholder view for <see cref="QuizManager"/>: full-screen veil, question, two options, who has answered,
    /// countdown, and the reveal. Only reads the manager, so it can be swapped for the final design.
    /// </summary>
    public class QuizUI : MonoBehaviour
    {
        [SerializeField] QuizManager quiz;
        [Tooltip("Veil and everything on it; shown only while a question runs.")]
        [SerializeField] GameObject overlay;

        [SerializeField] Text questionText;
        [SerializeField] Text leftText;
        [SerializeField] Text rightText;
        [SerializeField] Text leftPlayers;
        [SerializeField] Text rightPlayers;
        [SerializeField] Text statusText;
        [SerializeField] Text countdownText;
        [SerializeField] Text resultText;

        [Header("Look")]
        public Color answeringColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        public Color chosenColor = Color.white;
        public Color notChosenColor = new Color(0.55f, 0.55f, 0.55f, 0.45f);

        [Tooltip("OS fonts tried in order so Japanese text renders (the built-in font has no Japanese glyphs).")]
        public string[] fontNames = { "Yu Gothic UI", "Meiryo UI", "Meiryo", "MS Gothic", "Noto Sans CJK JP", "Hiragino Sans" };

        void Awake()
        {
            var font = Font.CreateDynamicFontFromOSFont(fontNames, 32);
            if (font != null)
                foreach (var label in GetComponentsInChildren<Text>(true))
                    label.font = font;
        }

        void Start()
        {
            if (quiz == null) quiz = FindFirstObjectByType<QuizManager>();
            if (overlay != null) overlay.SetActive(false);
        }

        void Update()
        {
            bool active = quiz != null && quiz.Phase != QuizManager.QuizPhase.Idle && quiz.Question != null;
            if (overlay != null && overlay.activeSelf != active) overlay.SetActive(active);
            if (!active) return;

            var q = quiz.Question;
            Set(questionText, q.text);
            Set(leftText, q.left);
            Set(rightText, q.right);

            if (quiz.Phase == QuizManager.QuizPhase.Answer)
            {
                Paint(leftText, answeringColor);
                Paint(rightText, answeringColor);
                Set(leftPlayers, "");
                Set(rightPlayers, "");
                Set(statusText, $"{Answered(1, quiz.Player1Choice)}　　　{Answered(2, quiz.Player2Choice)}");
                Set(countdownText, quiz.CountdownNumber.ToString());
                Set(resultText, "");
                return;
            }

            // Reveal.
            var p1 = quiz.Player1Choice;
            var p2 = quiz.Player2Choice;
            Paint(leftText, p1 == QuizChoice.Left || p2 == QuizChoice.Left ? chosenColor : notChosenColor);
            Paint(rightText, p1 == QuizChoice.Right || p2 == QuizChoice.Right ? chosenColor : notChosenColor);
            Set(leftPlayers, Pickers(QuizChoice.Left, p1, p2));
            Set(rightPlayers, Pickers(QuizChoice.Right, p1, p2));
            Set(statusText, Unanswered(p1, p2));
            Set(countdownText, "");

            var result = quiz.Current;
            if (result != null)
            {
                string side = result.direction == QuizChoice.Left ? "左" : "右";
                Set(resultText, result.success ? $"一致！ → {side}へ"
                    : p1 == QuizChoice.None || p2 == QuizChoice.None ? $"時間切れ… ランダムで{side}へ"
                    : $"不一致… ランダムで{side}へ");
            }
        }

        static string Answered(int player, QuizChoice choice) => choice == QuizChoice.None ? $"P{player} ―" : $"P{player} 選択済み";

        static string Pickers(QuizChoice side, QuizChoice p1, QuizChoice p2)
        {
            if (p1 == side && p2 == side) return "P1　P2";
            if (p1 == side) return "P1";
            if (p2 == side) return "P2";
            return "";
        }

        static string Unanswered(QuizChoice p1, QuizChoice p2)
        {
            if (p1 == QuizChoice.None && p2 == QuizChoice.None) return "P1 未回答　　　P2 未回答";
            if (p1 == QuizChoice.None) return "P1 未回答";
            if (p2 == QuizChoice.None) return "P2 未回答";
            return "";
        }

        static void Set(Text label, string value)
        {
            if (label != null && label.text != value) label.text = value;
        }

        static void Paint(Text label, Color color)
        {
            if (label != null) label.color = color;
        }
    }
}
