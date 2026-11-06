using CompanyF.Gameplay;
using CompanyF.Train;
using UnityEngine;

namespace CompanyF.UI
{
    /// <summary>
    /// End screens: CLEAR when the train reaches the end; GAMEOVER when the boiler overheats,
    /// the time runs out or the train hits an animal without the whistle.
    /// </summary>
    public class GameResultUI : MonoBehaviour
    {
        [SerializeField] TrainFollower train;
        [SerializeField] BoilerTemperature boiler;
        [SerializeField] GameFlow flow;
        [SerializeField] GameObject clearPanel;
        [SerializeField] GameObject gameOverPanel;

        void Start()
        {
            if (train == null) train = FindFirstObjectByType<TrainFollower>();
            if (boiler == null) boiler = FindFirstObjectByType<BoilerTemperature>();
            if (flow == null) flow = FindFirstObjectByType<GameFlow>();
            if (clearPanel != null) clearPanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);

            if (train != null) train.ReachedEnd += OnReachedEnd;
            if (boiler != null) boiler.Overheat += OnGameOver;
            if (flow != null) flow.GameOver += OnGameOver;
        }

        void OnDestroy()
        {
            if (train != null) train.ReachedEnd -= OnReachedEnd;
            if (boiler != null) boiler.Overheat -= OnGameOver;
            if (flow != null) flow.GameOver -= OnGameOver;
        }

        void OnReachedEnd() => Show(clearPanel);
        void OnGameOver() => Show(gameOverPanel);

        void Show(GameObject panel)
        {
            if (clearPanel != null && clearPanel.activeSelf) return;
            if (gameOverPanel != null && gameOverPanel.activeSelf) return;
            if (panel != null) panel.SetActive(true);
        }
    }
}
