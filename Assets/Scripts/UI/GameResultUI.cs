using CompanyF.Gameplay;
using CompanyF.Train;
using UnityEngine;

namespace CompanyF.UI
{
    /// <summary>End screens: CLEAR when the train reaches the end, GAMEOVER when the boiler overheats, the train falls into a chasm or it hits an animal.</summary>
    public class GameResultUI : MonoBehaviour
    {
        [SerializeField] TrainFollower train;
        [SerializeField] BoilerTemperature boiler;
        [SerializeField] GameObject clearPanel;
        [SerializeField] GameObject gameOverPanel;

        void Start()
        {
            if (train == null) train = FindFirstObjectByType<TrainFollower>();
            if (boiler == null) boiler = FindFirstObjectByType<BoilerTemperature>();
            if (clearPanel != null) clearPanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);

            if (train != null)
            {
                train.ReachedEnd += OnReachedEnd;
                train.Fell += OnFell;
                train.Crashed += OnCrashed;
            }
            if (boiler != null) boiler.Overheat += OnOverheat;
        }

        void OnDestroy()
        {
            if (train != null)
            {
                train.ReachedEnd -= OnReachedEnd;
                train.Fell -= OnFell;
                train.Crashed -= OnCrashed;
            }
            if (boiler != null) boiler.Overheat -= OnOverheat;
        }

        void OnReachedEnd() => Show(clearPanel);
        void OnOverheat() => Show(gameOverPanel);
        void OnFell() => Show(gameOverPanel);
        void OnCrashed() => Show(gameOverPanel);

        void Show(GameObject panel)
        {
            if (clearPanel != null && clearPanel.activeSelf) return;
            if (gameOverPanel != null && gameOverPanel.activeSelf) return;
            if (panel != null) panel.SetActive(true);
        }
    }
}
