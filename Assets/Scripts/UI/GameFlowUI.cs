using System.Collections.Generic;
using System.Globalization;
using CompanyF.Gameplay;
using CompanyF.Train;
using UnityEngine;
using UnityEngine.UI;

namespace CompanyF.UI
{
    /// <summary>Placeholder round UI: centre messages, the countdown, the route progress line with its animal markers, and the whistle status.</summary>
    public class GameFlowUI : MonoBehaviour
    {
        [SerializeField] GameFlow flow;
        [SerializeField] TrainFollower train;
        [SerializeField] Text messageText;
        [SerializeField] Text timerText;
        [Tooltip("Moves along the progress line by its x anchors.")]
        [SerializeField] RectTransform progressMarker;
        [SerializeField] AnimalHerd herd;
        [SerializeField] TrainControls controls;
        [SerializeField] Text whistleText;

        [Header("Animal markers on the progress line")]
        public Color animalMarkerColor = new Color(0.45f, 0.25f, 0.1f);
        [Min(0f)] public float animalMarkerSize = 18f;

        readonly List<Image> animalMarkers = new List<Image>();
        int timerBaseSize;

        void Awake()
        {
            if (timerText != null) timerBaseSize = timerText.fontSize;
        }

        void Start()
        {
            if (flow == null) flow = FindFirstObjectByType<GameFlow>();
            if (train == null) train = FindFirstObjectByType<TrainFollower>();
            if (herd == null) herd = FindFirstObjectByType<AnimalHerd>();
            if (controls == null) controls = FindFirstObjectByType<TrainControls>();
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
                UpdateAnimalMarkers();
            }

            if (whistleText != null && controls != null) Set(whistleText, WhistleLabel());
        }

        /// <summary>One marker for each animal charging right now.</summary>
        void UpdateAnimalMarkers()
        {
            int shown = 0;
            if (herd != null)
            {
                foreach (var animal in herd.Animals)
                {
                    if (animal.state != AnimalHerd.AnimalState.Charging) continue;
                    if (shown == animalMarkers.Count) animalMarkers.Add(NewAnimalMarker());

                    var marker = animalMarkers[shown++];
                    float progress = train.RouteProgressAt(animal.transform.position);
                    var rect = marker.rectTransform;
                    rect.anchorMin = new Vector2(progress, rect.anchorMin.y);
                    rect.anchorMax = new Vector2(progress, rect.anchorMax.y);
                    rect.sizeDelta = new Vector2(animalMarkerSize, animalMarkerSize);
                    marker.color = animalMarkerColor;
                    marker.gameObject.SetActive(true);
                }
            }
            for (int i = shown; i < animalMarkers.Count; i++) animalMarkers[i].gameObject.SetActive(false);
        }

        /// <summary>Same round sprite and anchoring as the train's marker, drawn beneath it.</summary>
        Image NewAnimalMarker()
        {
            var go = new GameObject("AnimalMarker", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(progressMarker.parent, false);
            rect.SetSiblingIndex(progressMarker.GetSiblingIndex());
            rect.anchorMin = progressMarker.anchorMin;
            rect.anchorMax = progressMarker.anchorMax;
            rect.pivot = progressMarker.pivot;
            rect.anchoredPosition = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.sprite = progressMarker.GetComponent<Image>().sprite;
            image.raycastTarget = false;
            return image;
        }

        string WhistleLabel()
        {
            switch (controls.Whistle)
            {
                case TrainControls.WhistleState.Sounding: return "汽笛：鳴笛中";
                case TrainControls.WhistleState.Cooldown:
                    // Rounded up, so it never reads 0.0 while the whistle is still cooling.
                    float tenths = Mathf.Ceil(controls.WhistleCooldownLeft * 10f - 1e-3f) / 10f;
                    return "汽笛：CD " + tenths.ToString("F1", CultureInfo.InvariantCulture);
                default: return "汽笛：OK";
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
