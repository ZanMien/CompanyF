using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CompanyF.UI
{
    /// <summary>
    /// Title screen: smoke rising from the locomotive to the top of the screen, the looping "game start" label,
    /// and the input that starts the game.
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        [Header("Smoke")]
        [SerializeField] Image[] puffs;
        [Tooltip("Where the puffs leave the chimney, in the parent of the puffs.")]
        public Vector2 smokeStart;
        [Tooltip("Height at which the puffs are gone (the top of the screen), in the parent of the puffs.")]
        public float smokeTop = 420f;
        [Min(0.1f)] public float smokeRiseTime = 3f;
        public float smokeSway = 50f;
        public float smokeStartScale = 0.5f;
        public float smokeEndScale = 1.7f;
        [Range(0f, 1f)] public float smokeOpacity = 0.85f;
        [Tooltip("Part of the rise left for fading out.")]
        [Range(0.05f, 1f)] public float smokeFadePart = 0.35f;

        [Header("Game start label")]
        [SerializeField] CanvasGroup startLabel;
        [Min(0.1f)] public float labelCycle = 3f;
        [Tooltip("Opacity over one cycle, in seconds.")]
        public AnimationCurve labelOpacity = Linear(0f, 0f, 1f, 0.8f, 1.3f, 0.5f, 1.6f, 0.25f, 2f, 0f, 3f, 0f);
        [Tooltip("Height over one cycle, in seconds; multiplied by Label Rise Unit.")]
        public AnimationCurve labelRise = Linear(0f, 0f, 1f, 0f, 1.3f, 1f, 1.6f, 1.5f, 2f, 2f, 3f, 2f);
        [Tooltip("UI pixels for 1 on the Label Rise curve.")]
        public float labelRiseUnit = 20f;

        [Header("Starting the game")]
        public string gameScene = "SampleScene";
        public Key[] startKeys = { Key.Enter, Key.NumpadEnter, Key.Space };
        [Tooltip("Seconds before input works, so a key still held from the result screen does not skip the title.")]
        [Min(0f)] public float startDelay = 0.5f;
        public AudioClip sceneChange;

        [Header("Text")]
        [Tooltip("Rounded M+ 1c bold goes here. Empty: the OS fonts below are used.")]
        public Font font;
        public string[] fontNames = { "Yu Gothic UI", "Meiryo UI", "Meiryo", "MS Gothic", "Noto Sans CJK JP", "Hiragino Sans" };

        float clock;
        bool starting;
        Vector2 labelHome;

        void Awake()
        {
            var usedFont = font != null ? font : Font.CreateDynamicFontFromOSFont(fontNames, 32);
            if (usedFont != null)
                foreach (var label in GetComponentsInChildren<Text>(true))
                    label.font = usedFont;

            labelHome = ((RectTransform)startLabel.transform).anchoredPosition;
        }

        void Update()
        {
            clock += Time.deltaTime;
            AnimateSmoke();
            AnimateLabel();

            if (clock >= startDelay && StartPressed()) StartGame();
        }

        void AnimateSmoke()
        {
            for (int i = 0; i < puffs.Length; i++)
            {
                float t = Mathf.Repeat(clock / smokeRiseTime + i / (float)puffs.Length, 1f);
                var rect = puffs[i].rectTransform;
                float sway = Mathf.Sin(t * Mathf.PI * 2f + i) * smokeSway * t;
                rect.anchoredPosition = new Vector2(smokeStart.x + sway, Mathf.Lerp(smokeStart.y, smokeTop, t));
                float scale = Mathf.Lerp(smokeStartScale, smokeEndScale, t);
                rect.localScale = new Vector3(scale, scale, 1f);

                var color = puffs[i].color;
                color.a = smokeOpacity * Mathf.Clamp01(t / 0.08f) * Mathf.Clamp01((1f - t) / smokeFadePart);
                puffs[i].color = color;
            }
        }

        void AnimateLabel()
        {
            float t = Mathf.Repeat(clock, labelCycle);
            startLabel.alpha = labelOpacity.Evaluate(t);
            ((RectTransform)startLabel.transform).anchoredPosition = labelHome + Vector2.up * (labelRise.Evaluate(t) * labelRiseUnit);
        }

        bool StartPressed()
        {
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;

            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            foreach (var key in startKeys)
                if (keyboard[key].wasPressedThisFrame) return true;
            return false;
        }

        public void StartGame()
        {
            if (starting) return;
            starting = true;

            if (sceneChange != null)
            {
                var go = new GameObject("SceneChangeSound");
                DontDestroyOnLoad(go);
                go.AddComponent<AudioSource>().PlayOneShot(sceneChange);
                Destroy(go, sceneChange.length);
            }
            SceneManager.LoadScene(gameScene);
        }

        /// <summary>A curve with straight lines through (time, value) pairs.</summary>
        static AnimationCurve Linear(params float[] points)
        {
            var keys = new Keyframe[points.Length / 2];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new Keyframe(points[i * 2], points[i * 2 + 1]);
            for (int i = 0; i < keys.Length; i++)
            {
                if (i > 0) keys[i].inTangent = (keys[i].value - keys[i - 1].value) / (keys[i].time - keys[i - 1].time);
                if (i < keys.Length - 1) keys[i].outTangent = (keys[i + 1].value - keys[i].value) / (keys[i + 1].time - keys[i].time);
            }
            return new AnimationCurve(keys);
        }
    }
}
