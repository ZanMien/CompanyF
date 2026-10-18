using UnityEngine;
using UnityEngine.UI;

namespace CompanyF.Scoring
{
    /// <summary>
    /// Placeholder results screen: grey-white veil with distance and score, shown when the game ends.
    /// On overheat a title line is shown above them.
    /// </summary>
    public class ResultUI : MonoBehaviour
    {
        [SerializeField] ScoreManager score;
        [SerializeField] GameObject overlay;
        [Tooltip("Shown only when the game ended by overheating.")]
        [SerializeField] Text titleText;
        [SerializeField] Text distanceText;
        [SerializeField] Text scoreText;

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
            if (score == null) score = FindFirstObjectByType<ScoreManager>();
            if (overlay != null) overlay.SetActive(false);
        }

        void Update()
        {
            if (score == null || !score.Finished || overlay == null || overlay.activeSelf) return;
            if (titleText != null)
            {
                titleText.text = "オーバーヒート";
                titleText.gameObject.SetActive(score.Overheated);
            }
            if (distanceText != null) distanceText.text = $"距離：{score.Distance:0}m";
            if (scoreText != null) scoreText.text = $"スコア：{score.Score}";
            overlay.SetActive(true);
        }
    }
}
