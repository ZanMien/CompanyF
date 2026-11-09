using System.Collections;
using CompanyF.Gameplay;
using CompanyF.Train;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CompanyF.UI
{
    /// <summary>
    /// The result screen, shown in the game scene itself. When the round ends (after <see cref="switchDelay"/>, 0 by
    /// default), the train and every other UI are hidden, the background switches to <see cref="resultCamera"/>,
    /// and the signboard shows the result, the clear time and the ranking.
    /// After <see cref="inputLockTime"/>, the restart key loads the game scene again.
    /// </summary>
    public class ResultScreen : MonoBehaviour
    {
        [SerializeField] GameFlow flow;
        [SerializeField] TrainFollower train;
        [SerializeField] BoilerTemperature boiler;

        [Header("Timing (s)")]
        [Tooltip("Wait between the end of the round and the result screen.")]
        [Min(0f)] public float switchDelay = 0f;
        [Tooltip("Keys do nothing for this long after the result screen appears.")]
        [Min(0f)] public float inputLockTime = 1f;

        [Header("Ranking")]
        [Min(1)] public int rankingCount = 5;
        [Tooltip("Colour of this run's line when it makes the ranking.")]
        public Color highlightColor = new Color32(255, 225, 60, 255);

        [Header("Restart")]
        public Key restartKey = Key.Space;
        public string restartScene = "SampleScene";

        [Header("Result text")]
        public Color clearColor = new Color32(0xf9, 0x63, 0x63, 255);
        public Color gameOverColor = new Color32(0x7b, 0x90, 0xfa, 255);

        [Header("Parts")]
        [SerializeField] Camera resultCamera;
        [Tooltip("Everything on the signboard; hidden until the result screen appears.")]
        [SerializeField] GameObject content;
        [SerializeField] TMP_Text resultText;
        [SerializeField] GameObject arrivalPaper;
        [SerializeField] TMP_Text arrivalText;
        [SerializeField] TMP_Text rankingText;

        public bool Showing { get; private set; }
        public bool Cleared { get; private set; }
        public float ClearTime { get; private set; }
        /// <summary>This run's place in the ranking (0 = fastest), or -1.</summary>
        public int RankingPlace { get; private set; } = -1;

        bool decided;
        float shownAt;
        bool restartKeyWasDown = true;  // so a key already down when the screen appears needs pressing again

        void Start()
        {
            if (flow == null) flow = FindFirstObjectByType<GameFlow>();
            if (train == null) train = FindFirstObjectByType<TrainFollower>();
            if (boiler == null) boiler = FindFirstObjectByType<BoilerTemperature>();

            if (content != null) content.SetActive(false);
            if (resultCamera != null) resultCamera.gameObject.SetActive(false);

            if (train != null) train.ReachedEnd += OnCleared;
            if (boiler != null) boiler.Overheat += OnFailed;
            if (flow != null) flow.GameOver += OnFailed;
        }

        void OnDestroy()
        {
            if (train != null) train.ReachedEnd -= OnCleared;
            if (boiler != null) boiler.Overheat -= OnFailed;
            if (flow != null) flow.GameOver -= OnFailed;
        }

        // The first ending counts, as on the CLEAR / GAMEOVER screen.
        void OnCleared() => Decide(true);
        void OnFailed() => Decide(false);

        void Decide(bool cleared)
        {
            if (decided) return;
            decided = true;
            float time = cleared && flow != null ? flow.timeLimit - flow.TimeLeft : 0f;
            if (switchDelay <= 0f) Show(cleared, time);
            else StartCoroutine(ShowAfterDelay(cleared, time));
        }

        IEnumerator ShowAfterDelay(bool cleared, float clearTime)
        {
            yield return new WaitForSeconds(switchDelay);
            Show(cleared, clearTime);
        }

        /// <summary>Shows the result screen now. Also used by the Inspector's test buttons.</summary>
        public void Show(bool cleared, float clearTime)
        {
            if (Showing) return;
            decided = true;
            Showing = true;
            shownAt = Time.time;
            Cleared = cleared;
            ClearTime = ResultRanking.Round(clearTime);

            var times = ResultRanking.Load();
            RankingPlace = -1;
            if (cleared)
            {
                // Only clears are recorded.
                RankingPlace = ResultRanking.Record(ClearTime, out times);
            }

            HideGame();

            if (resultText != null)
            {
                resultText.text = cleared ? "ゲームクリア" : "ゲームオーバー";
                resultText.color = cleared ? clearColor : gameOverColor;
            }
            if (arrivalPaper != null) arrivalPaper.SetActive(cleared);
            if (arrivalText != null) arrivalText.text = $"到着時間\n{ResultRanking.FormatSeconds(ClearTime)}秒";
            if (rankingText != null)
            {
                int highlight = RankingPlace < rankingCount ? RankingPlace : -1;
                rankingText.text = ResultRanking.FormatLines(times, rankingCount, highlight, highlightColor);
            }

            if (resultCamera != null) resultCamera.gameObject.SetActive(true);
            if (content != null) content.SetActive(true);
        }

        /// <summary>
        /// Hides the train and every UI that is not this screen, and hands the view and hearing over to the result
        /// camera. The train object itself stays active: its sound effects (GameSoundEffects) play the clear /
        /// game over sound as this screen appears, and switching the train off would cut that sound.
        /// The game has already stopped, so its scripts no longer change anything.
        /// </summary>
        void HideGame()
        {
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.isRootCanvas && !canvas.transform.IsChildOf(transform)) canvas.gameObject.SetActive(false);
            foreach (var other in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (other != resultCamera) other.enabled = false;
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (!listener.transform.IsChildOf(transform)) listener.enabled = false;
            if (train != null)
                foreach (var part in train.GetComponentsInChildren<Renderer>(true)) part.enabled = false;
        }

        void Update()
        {
            if (!Showing) return;

            // Only a fresh press counts: a key held since before the screen appeared, or held through the lock, does not.
            var keyboard = Keyboard.current;
            bool down = keyboard != null && keyboard[restartKey].isPressed;
            bool pressed = down && !restartKeyWasDown;
            restartKeyWasDown = down;

            if (pressed && Time.time - shownAt >= inputLockTime) SceneManager.LoadScene(restartScene);
        }
    }
}
