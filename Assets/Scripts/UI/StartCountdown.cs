using CompanyF.Train;
using UnityEngine;
using UnityEngine.UI;

namespace CompanyF.UI
{
    /// <summary>
    /// Opening animation: 3, 2, 1, then the go text. Holds the train at the start until the go text appears,
    /// and removes itself when it has faded out.
    /// </summary>
    public class StartCountdown : MonoBehaviour
    {
        [SerializeField] TrainFollower train;
        [SerializeField] Text label;
        [Tooltip("Dims the scene during the count; fades out on the go text.")]
        [SerializeField] Image veil;

        [Header("Timing (s)")]
        [Min(0.1f)] public float beatTime = 1f;
        [Min(0.1f)] public float goTime = 0.9f;
        [Tooltip("Part of each beat the number takes to slam in.")]
        [Range(0.05f, 0.9f)] public float slamPart = 0.3f;
        [Tooltip("Part of each beat left at the end for the number to fade.")]
        [Range(0.05f, 0.9f)] public float fadePart = 0.25f;

        [Header("Look")]
        [Min(1f)] public float slamScale = 2.4f;
        [Min(1f)] public float goEndScale = 1.7f;
        public string goText = "出發！";
        public Color[] numberColors =
        {
            new Color(1f, 0.25f, 0.2f),
            new Color(1f, 0.6f, 0.15f),
            new Color(1f, 0.88f, 0.25f),
        };
        public Color goColor = new Color(0.45f, 1f, 0.45f);

        [Tooltip("OS fonts tried in order so the go text renders (the built-in font has no CJK glyphs).")]
        public string[] fontNames = { "Yu Gothic UI", "Meiryo UI", "Meiryo", "MS Gothic", "Noto Sans CJK JP", "Hiragino Sans" };

        float clock;
        float veilAlpha;

        void Awake()
        {
            if (train == null) train = FindFirstObjectByType<TrainFollower>();
            if (train != null) train.Hold();  // in Awake so no Update sees the train running

            var font = Font.CreateDynamicFontFromOSFont(fontNames, 32);
            if (font != null && label != null) label.font = font;
            if (veil != null) veilAlpha = veil.color.a;
        }

        void Update()
        {
            clock += Time.deltaTime;

            int count = numberColors.Length;
            float goStart = count * beatTime;
            if (clock < goStart)
            {
                int beat = Mathf.FloorToInt(clock / beatTime);
                ShowNumber(count - beat, numberColors[beat], clock / beatTime - beat);
                return;
            }

            if (train != null) train.Depart();

            float t = (clock - goStart) / goTime;
            if (t >= 1f)
            {
                gameObject.SetActive(false);
                return;
            }
            ShowGo(t);
        }

        void ShowNumber(int number, Color color, float t)
        {
            label.text = number.ToString();
            float slam = Mathf.Clamp01(t / slamPart);
            float scale = Mathf.LerpUnclamped(slamScale, 1f, EaseOutBack(slam));
            float fade = Mathf.Clamp01((1f - t) / fadePart);
            Apply(color, scale, Mathf.Min(slam * 3f, 1f) * fade);
        }

        void ShowGo(float t)
        {
            label.text = goText;
            float pop = Mathf.Clamp01(t / 0.25f);
            float scale = t < 0.25f
                ? Mathf.LerpUnclamped(0.5f, 1f, EaseOutBack(pop))
                : Mathf.Lerp(1f, goEndScale, (t - 0.25f) / 0.75f);
            Apply(goColor, scale, Mathf.Clamp01((1f - t) / 0.5f));

            if (veil != null) SetAlpha(veil, veilAlpha * (1f - Mathf.Clamp01(t / 0.3f)));
        }

        void Apply(Color color, float scale, float alpha)
        {
            color.a = alpha;
            label.color = color;
            label.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        static void SetAlpha(Graphic graphic, float alpha)
        {
            var color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }

        static float EaseOutBack(float t)
        {
            const float Overshoot = 1.70158f;
            t -= 1f;
            return 1f + (Overshoot + 1f) * t * t * t + Overshoot * t * t;
        }
    }
}
