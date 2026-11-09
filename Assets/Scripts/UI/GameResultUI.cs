using System;
using CompanyF.Gameplay;
using CompanyF.Train;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CompanyF.UI
{
    /// <summary>
    /// Result screen: CLEAR when the train reaches the end, GAMEOVER when the boiler overheats, the train falls into
    /// a chasm or it hits an animal. A wooden board drops in over a view of the stage, without the train.
    /// </summary>
    public class GameResultUI : MonoBehaviour
    {
        const string NoTime = "--:--.--";

        /// <summary>Raised once when the result appears: (cleared).</summary>
        public event Action<bool> Shown;
        /// <summary>Raised just before a button loads another scene.</summary>
        public event Action Leaving;

        [SerializeField] TrainFollower train;
        [SerializeField] BoilerTemperature boiler;
        [SerializeField] GameObject panel;
        [Tooltip("Pivot at its top centre, so it swings like a hanging sign.")]
        [SerializeField] RectTransform board;
        [SerializeField] Text titleText;
        [SerializeField] Text timeText;
        [SerializeField] Text[] rankingRows;
        [Tooltip("Stage view shown behind the board. Off until the result appears.")]
        [SerializeField] Camera resultCamera;
        [SerializeField] Button retryButton;
        [SerializeField] Button titleButton;

        [Header("Text")]
        public string clearTitle = "ゲームクリア";
        public string gameOverTitle = "ゲームオーバー";
        public Color clearColor = new Color32(0xF9, 0x63, 0x63, 0xFF);
        public Color gameOverColor = new Color32(0x7B, 0x90, 0xFA, 0xFF);
        public Color rowColor = new Color32(0xFB, 0xF7, 0xD3, 0xFF);
        public Color newRecordColor = new Color(1f, 0.85f, 0.2f);
        [Tooltip("Rounded M+ 1c bold goes here. Empty: the OS fonts below are used.")]
        public Font font;
        public string[] fontNames = { "Yu Gothic UI", "Meiryo UI", "Meiryo", "MS Gothic", "Noto Sans CJK JP", "Hiragino Sans" };

        [Header("Board animation")]
        [Min(0.05f)] public float dropTime = 0.5f;
        [Tooltip("Degrees of the first swing after it lands.")]
        public float swingAngle = 4f;
        public float swingFrequency = 7f;
        public float swingDamping = 2.5f;

        [Header("Buttons")]
        [Tooltip("Scene loaded by the title button. It must be in the Build Settings.")]
        public string titleScene = "Title";
        [Tooltip("Seconds before the buttons work, so a player still shovelling coal does not skip the result.")]
        [Min(0f)] public float buttonDelay = 1.5f;

        float elapsed;
        float shownFor;
        bool shown;
        bool buttonsReady;
        Vector2 boardHome;

        void Start()
        {
            if (train == null) train = FindFirstObjectByType<TrainFollower>();
            if (boiler == null) boiler = FindFirstObjectByType<BoilerTemperature>();
            if (panel != null) panel.SetActive(false);
            if (resultCamera != null) resultCamera.enabled = false;
            if (board != null) boardHome = board.anchoredPosition;
            retryButton.onClick.AddListener(Retry);
            titleButton.onClick.AddListener(GoToTitle);
            retryButton.interactable = titleButton.interactable = false;

            var usedFont = font != null ? font : Font.CreateDynamicFontFromOSFont(fontNames, 32);
            if (usedFont != null)
                foreach (var label in GetComponentsInChildren<Text>(true))
                    label.font = usedFont;

            if (train != null)
            {
                train.ReachedEnd += OnReachedEnd;
                train.Fell += OnFailed;
                train.Crashed += OnFailed;
            }
            if (boiler != null) boiler.Overheat += OnFailed;
        }

        void OnDestroy()
        {
            if (train != null)
            {
                train.ReachedEnd -= OnReachedEnd;
                train.Fell -= OnFailed;
                train.Crashed -= OnFailed;
            }
            if (boiler != null) boiler.Overheat -= OnFailed;
        }

        void Update()
        {
            if (!shown)
            {
                if (train != null && !train.Stopped) elapsed += Time.deltaTime;
                return;
            }

            shownFor += Time.deltaTime;
            AnimateBoard();

            if (!buttonsReady && shownFor >= buttonDelay)
            {
                buttonsReady = true;
                retryButton.interactable = titleButton.interactable = true;
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(retryButton.gameObject);
            }
        }

        void Retry() => Leave(SceneManager.GetActiveScene().name);
        void GoToTitle() => Leave(titleScene);

        void Leave(string scene)
        {
            Leaving?.Invoke();
            SceneManager.LoadScene(scene);
        }

        void OnReachedEnd() => Show(true);
        void OnFailed() => Show(false);

        void Show(bool cleared)
        {
            if (shown) return;
            shown = true;

            int place = cleared ? ResultRanking.Add(elapsed) : -1;
            titleText.text = cleared ? clearTitle : gameOverTitle;
            titleText.color = cleared ? clearColor : gameOverColor;
            timeText.text = cleared ? FormatTime(elapsed) : NoTime;
            FillRanking(place);

            if (train != null)
                foreach (var part in train.GetComponentsInChildren<Renderer>())
                    part.enabled = false;
            var hud = FindFirstObjectByType<GameHUD>();
            if (hud != null) hud.gameObject.SetActive(false);
            if (resultCamera != null) resultCamera.enabled = true;

            AnimateBoard();
            panel.SetActive(true);
            Shown?.Invoke(cleared);
        }

        void FillRanking(int place)
        {
            var times = ResultRanking.Load();
            for (int i = 0; i < rankingRows.Length; i++)
            {
                rankingRows[i].text = $"{i + 1}位　{(i < times.Count ? FormatTime(times[i]) : NoTime)}";
                rankingRows[i].color = i == place ? newRecordColor : rowColor;
            }
        }

        void AnimateBoard()
        {
            float drop = Mathf.Clamp01(shownFor / dropTime);
            float fall = 1f - (1f - drop) * (1f - drop);
            board.anchoredPosition = boardHome + Vector2.up * ((1f - fall) * (board.rect.height - boardHome.y));

            float settle = Mathf.Max(0f, shownFor - dropTime);
            float angle = drop < 1f ? 0f : swingAngle * Mathf.Exp(-swingDamping * settle) * Mathf.Sin(swingFrequency * settle);
            board.localEulerAngles = new Vector3(0f, 0f, angle);
        }

        static string FormatTime(float seconds) => $"{Mathf.FloorToInt(seconds / 60f):00}:{seconds % 60f:00.00}";
    }
}
