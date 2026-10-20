using CompanyF.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CompanyF.UI
{
    /// <summary>
    /// Placeholder HUD: speedometer (bottom right), temperature bar (bottom), furnace status text (upper centre).
    /// Only reads the gameplay components, so it can be swapped for the final design.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [SerializeField] BoilerTemperature boiler;
        [SerializeField] TemperatureSpeed speed;
        [SerializeField] FurnaceController furnace;

        [Header("Speedometer")]
        [Tooltip("Rotates around its bottom pivot; points up at 0°.")]
        [SerializeField] RectTransform needle;
        [Tooltip("Radial-filled arc showing the current speed.")]
        [SerializeField] Image speedArc;
        [SerializeField] Text speedText;
        [Tooltip("Total angle of the dial, centred on straight up.")]
        public float dialSweep = 240f;

        [Header("Temperature bar")]
        [SerializeField] RawImage barGradient;
        [Tooltip("Child of the bar, positioned by its x anchors.")]
        [SerializeField] RectTransform temperaturePointer;
        [SerializeField] Text temperatureText;
        [SerializeField] Text minLabel;
        [SerializeField] Text maxLabel;
        public Color coldColor = Color.white;
        public Color hotColor = new Color(0.9f, 0.08f, 0.04f);

        [Header("Status")]
        [SerializeField] Text doorText;
        [SerializeField] Text flashText;
        [SerializeField] Text failText;
        [Min(0f)] public float flashTime = 0.5f;
        public Color coalFlashColor = new Color(1f, 0.6f, 0.15f);
        public Color coolFlashColor = new Color(0.45f, 0.8f, 1f);

        [Tooltip("OS fonts tried in order so Japanese text renders (the built-in font has no Japanese glyphs).")]
        public string[] fontNames = { "Yu Gothic UI", "Meiryo UI", "Meiryo", "MS Gothic", "Noto Sans CJK JP", "Hiragino Sans" };

        float flashLeft;
        Texture2D gradient;

        void Awake()
        {
            var font = Font.CreateDynamicFontFromOSFont(fontNames, 32);
            if (font != null)
                foreach (var label in GetComponentsInChildren<Text>(true))
                    label.font = font;

            gradient = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < gradient.width; x++)
                gradient.SetPixel(x, 0, Color.Lerp(coldColor, hotColor, x / (gradient.width - 1f)));
            gradient.Apply();
            if (barGradient != null) barGradient.texture = gradient;
        }

        void Start()
        {
            if (boiler == null) boiler = FindFirstObjectByType<BoilerTemperature>();
            if (speed == null) speed = FindFirstObjectByType<TemperatureSpeed>();
            if (furnace == null) furnace = FindFirstObjectByType<FurnaceController>();

            if (furnace != null)
            {
                furnace.CoalAdded += OnCoalAdded;
                furnace.Cooled += OnCooled;
            }
            if (boiler != null)
            {
                if (minLabel != null) minLabel.text = $"{boiler.minTemperature:0} °C";
                if (maxLabel != null) maxLabel.text = $"{boiler.overheatTemperature:0} °C";
            }
            if (flashText != null) flashText.text = "";
        }

        void OnDestroy()
        {
            if (furnace != null)
            {
                furnace.CoalAdded -= OnCoalAdded;
                furnace.Cooled -= OnCooled;
            }
            if (gradient != null) Destroy(gradient);
        }

        void OnCoalAdded(float heat) => Flash($"石炭投入！+{heat:0}°C", coalFlashColor);
        void OnCooled(float heat) => Flash($"-{heat:0}°C", coolFlashColor);

        void Flash(string message, Color color)
        {
            if (flashText == null) return;
            flashText.text = message;
            flashText.color = color;
            flashLeft = flashTime;
        }

        void Update()
        {
            if (speed != null)
            {
                float frac = Mathf.InverseLerp(speed.startDisplaySpeed, speed.maxDisplaySpeed, speed.DisplaySpeed);
                if (needle != null) needle.localEulerAngles = new Vector3(0f, 0f, dialSweep * 0.5f - frac * dialSweep);
                if (speedArc != null) speedArc.fillAmount = frac * dialSweep / 360f;
                Set(speedText, $"{Mathf.RoundToInt(speed.DisplaySpeed)} km/h");
            }

            if (boiler != null)
            {
                float t = Mathf.InverseLerp(boiler.minTemperature, boiler.overheatTemperature, boiler.Temperature);
                if (temperaturePointer != null)
                {
                    temperaturePointer.anchorMin = new Vector2(t, temperaturePointer.anchorMin.y);
                    temperaturePointer.anchorMax = new Vector2(t, temperaturePointer.anchorMax.y);
                }
                Set(temperatureText, $"{Mathf.RoundToInt(boiler.Temperature)} °C");
            }

            if (furnace != null)
            {
                Set(doorText, DoorLabel(furnace.State));
                if (failText != null && failText.gameObject.activeSelf != furnace.P2LockoutLeft > 0f)
                    failText.gameObject.SetActive(furnace.P2LockoutLeft > 0f);
            }

            if (flashLeft > 0f)
            {
                flashLeft -= Time.deltaTime;
                if (flashLeft <= 0f) Set(flashText, "");
            }
        }

        static string DoorLabel(DoorState state)
        {
            switch (state)
            {
                case DoorState.Opening: return "開く中…";
                case DoorState.Open: return "開";
                case DoorState.Loading: return "石炭投入中…";
                case DoorState.Closing: return "閉じる中…";
                default: return "閉";
            }
        }

        static void Set(Text label, string value)
        {
            if (label != null && label.text != value) label.text = value;
        }
    }
}
