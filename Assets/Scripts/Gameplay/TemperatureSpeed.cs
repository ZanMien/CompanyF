using CompanyF.Train;
using UnityEngine;

namespace CompanyF.Gameplay
{
    /// <summary>
    /// The speed follows the boiler temperature: every <see cref="degreesPerStep"/> above the start temperature
    /// adds <see cref="speedPerStep"/> to the displayed km/h. The displayed range maps linearly onto the
    /// train's actual movement range (start display speed = slowest, max display speed = fastest).
    /// </summary>
    public class TemperatureSpeed : MonoBehaviour
    {
        [SerializeField] BoilerTemperature boiler;
        [SerializeField] TrainFollower train;

        [Header("Displayed speed (km/h)")]
        [Min(0.01f)] public float degreesPerStep = 50f;
        public float speedPerStep = 5f;
        [Tooltip("Shown at the start temperature.")]
        public float startDisplaySpeed = 15f;
        [Tooltip("Highest speed shown; the train moves at maxTrainSpeed here.")]
        public float maxDisplaySpeed = 155f;

        [Header("Actual train movement (TrainFollower speed)")]
        public float minTrainSpeed = 15f;
        public float maxTrainSpeed = 150f;

        public float DisplaySpeed => boiler == null ? startDisplaySpeed : DisplayFor(boiler.Temperature);
        public float TrainSpeed => TrainSpeedFor(DisplaySpeed);

        public float DisplayFor(float temperature)
        {
            float reference = boiler != null ? boiler.startTemperature : 50f;
            float display = startDisplaySpeed + (temperature - reference) / degreesPerStep * speedPerStep;
            return Mathf.Clamp(display, startDisplaySpeed, maxDisplaySpeed);
        }

        public float TrainSpeedFor(float display) =>
            Mathf.Lerp(minTrainSpeed, maxTrainSpeed, Mathf.InverseLerp(startDisplaySpeed, maxDisplaySpeed, display));

        void Start()
        {
            if (boiler == null) boiler = FindFirstObjectByType<BoilerTemperature>();
            if (train == null) train = FindFirstObjectByType<TrainFollower>();
            Apply();
        }

        void Update() => Apply();

        void Apply()
        {
            if (train != null) train.speed = TrainSpeed;
        }
    }
}
