using CompanyF.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CompanyF.UI
{
    /// <summary>Placeholder status panel: gear, speed and boiler temperature, plus a whistle indicator.</summary>
    public class StatusUI : MonoBehaviour
    {
        [SerializeField] SpeedRangeController speedRange;
        [SerializeField] BoilerTemperature boiler;
        [SerializeField] TrainControls controls;

        [SerializeField] Text gearText;
        [SerializeField] Text speedText;
        [SerializeField] Text temperatureText;
        [SerializeField] Text whistleText;

        [Tooltip("Temperature text turns this colour from this fraction of the overheat temperature.")]
        [Range(0f, 1f)] public float warningFraction = 0.8f;
        public Color normalColor = Color.white;
        public Color warningColor = new Color(1f, 0.35f, 0.25f);

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
            if (speedRange == null) speedRange = FindFirstObjectByType<SpeedRangeController>();
            if (boiler == null) boiler = FindFirstObjectByType<BoilerTemperature>();
            if (controls == null) controls = FindFirstObjectByType<TrainControls>();
        }

        void Update()
        {
            if (speedRange != null)
            {
                Set(gearText, $"ギア：{speedRange.Gear}");
                Set(speedText, $"速度：{Mathf.RoundToInt(speedRange.Speed)} km/h");
            }

            if (boiler != null && temperatureText != null)
            {
                Set(temperatureText, $"温度：{Mathf.RoundToInt(boiler.Temperature)} °C");
                temperatureText.color = boiler.Temperature >= boiler.overheatTemperature * warningFraction ? warningColor : normalColor;
            }

            if (whistleText != null)
            {
                bool on = controls != null && controls.IsWhistling;
                if (whistleText.gameObject.activeSelf != on) whistleText.gameObject.SetActive(on);
            }
        }

        static void Set(Text label, string value)
        {
            if (label != null && label.text != value) label.text = value;
        }
    }
}
