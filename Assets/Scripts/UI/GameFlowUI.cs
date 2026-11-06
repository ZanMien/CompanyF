using CompanyF.Gameplay;
using CompanyF.Train;
using UnityEngine;
using UnityEngine.UI;

namespace CompanyF.UI
{
    /// <summary>Placeholder round UI: centre messages, the countdown and the route progress line.</summary>
    public class GameFlowUI : MonoBehaviour
    {
        [SerializeField] GameFlow flow;
        [SerializeField] TrainFollower train;
        [SerializeField] Text messageText;
        [SerializeField] Text timerText;
        [Tooltip("Moves along the progress line by its x anchors.")]
        [SerializeField] RectTransform progressMarker;

        int timerBaseSize;

        void Awake()
        {
            if (timerText != null) timerBaseSize = timerText.fontSize;
        }

        void Start()
        {
            if (flow == null) flow = FindFirstObjectByType<GameFlow>();
            if (train == null) train = FindFirstObjectByType<TrainFollower>();
        }

        void Update()
        {
            if (flow == null) return;

            if (messageText != null) Set(messageText, Message());

            if (timerText != null)
            {
                Set(timerText, flow.SecondsShown.ToString());
                timerText.fontSize = flow.Emphasised ? Mathf.RoundToInt(timerBaseSize * flow.emphasisScale) : timerBaseSize;
            }

            if (progressMarker != null && train != null)
            {
                float progress = train.RouteProgress;
                progressMarker.anchorMin = new Vector2(progress, progressMarker.anchorMin.y);
                progressMarker.anchorMax = new Vector2(progress, progressMarker.anchorMax.y);
            }
        }

        string Message()
        {
            switch (flow.Phase)
            {
                case GamePhase.Ready: return "出発";
                case GamePhase.Finishing: return "フィニッシュ!!";
                default: return flow.ShowsGoMessage ? "進行！" : "";
            }
        }

        static void Set(Text label, string value)
        {
            if (label.text != value) label.text = value;
        }
    }
}
