using CompanyF.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CompanyF.UI
{
    /// <summary>
    /// Placeholder view for <see cref="SpeedRangeController"/>: bar with success zone and pointer,
    /// current player, combo and speed. Only reads the controller, so it can be swapped for the final design.
    /// </summary>
    public class SpeedRangeUI : MonoBehaviour
    {
        [SerializeField] SpeedRangeController controller;

        [Header("Bar (children of the bar rect, positioned by anchors)")]
        [SerializeField] RectTransform zone;
        [SerializeField] RectTransform pointer;

        [Header("Labels")]
        [SerializeField] Text playerLabel;
        [SerializeField] Text comboLabel;
        [SerializeField] Text speedLabel;
        [SerializeField] Text resultLabel;

        [Min(0f)] public float resultShowTime = 0.6f;

        float resultTimer;

        void Start()
        {
            if (controller == null) controller = FindFirstObjectByType<SpeedRangeController>();
            if (controller != null) controller.Judged += OnJudged;
            if (resultLabel != null) resultLabel.text = "";
        }

        void OnDestroy()
        {
            if (controller != null) controller.Judged -= OnJudged;
        }

        void OnJudged(int player, bool success)
        {
            if (resultLabel == null) return;
            resultLabel.text = $"P{player} {(success ? "SUCCESS" : "MISS")}";
            resultLabel.color = success ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.4f, 0.4f);
            resultTimer = resultShowTime;
        }

        void Update()
        {
            if (controller == null) return;
            const float max = SpeedRangeController.BarMax;

            SetSpan(zone, controller.ZoneStart / max, controller.ZoneEnd / max);
            SetSpan(pointer, controller.Pointer / max, controller.Pointer / max);

            if (playerLabel != null) playerLabel.text = controller.Finished ? "--" : $"P{controller.CurrentPlayer}";
            if (comboLabel != null)
                comboLabel.text = controller.Combo > 0
                    ? $"COMBO {controller.Combo}  ({controller.ComboTimeLeft:0.0}s)"
                    : "COMBO 0";
            if (speedLabel != null) speedLabel.text = $"SPEED {controller.Speed:0.00}";

            if (resultLabel != null && resultTimer > 0f)
            {
                resultTimer -= Time.deltaTime;
                if (resultTimer <= 0f) resultLabel.text = "";
            }
        }

        /// <summary>Stretches <paramref name="rect"/> horizontally between two 0-1 positions of its parent; keeps its own width/height offsets.</summary>
        static void SetSpan(RectTransform rect, float from, float to)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(from, rect.anchorMin.y);
            rect.anchorMax = new Vector2(to, rect.anchorMax.y);
        }
    }
}
