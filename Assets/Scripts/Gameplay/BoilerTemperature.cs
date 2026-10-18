using System;
using CompanyF.Train;
using UnityEngine;

namespace CompanyF.Gameplay
{
    /// <summary>
    /// Boiler temperature. Speed-bar successes heat it, misses cool it, the cooling key cools it over time.
    /// Going above <see cref="overheatTemperature"/> halts the train and ends the game.
    /// </summary>
    public class BoilerTemperature : MonoBehaviour
    {
        [SerializeField] SpeedRangeController speedRange;
        [SerializeField] TrainFollower train;

        [Header("Temperature (°C)")]
        public float startTemperature = 50f;
        public float minTemperature = 50f;
        [Tooltip("Going above this ends the game.")]
        public float overheatTemperature = 200f;

        [Header("Changes")]
        [Tooltip("Added on every speed-bar success, even when the speed is already at the gear's top.")]
        [Min(0f)] public float successHeat = 20f;
        [Tooltip("Removed on every speed-bar miss.")]
        [Min(0f)] public float failCool = 10f;
        [Tooltip("Removed per second while the cooling key is held.")]
        [Min(0f)] public float coolPerSecond = 20f;

        /// <summary>Raised once when the boiler overheats (after the train has been halted).</summary>
        public event Action Overheat;

        public float Temperature { get; private set; }
        public bool Overheated { get; private set; }

        void Awake() => Temperature = Mathf.Max(startTemperature, minTemperature);

        void Start()
        {
            if (speedRange == null) speedRange = FindFirstObjectByType<SpeedRangeController>();
            if (train == null) train = FindFirstObjectByType<TrainFollower>();
            if (speedRange != null) speedRange.Judged += OnJudged;
            else Debug.LogError("[Boiler] No SpeedRangeController in the scene.", this);
        }

        void OnDestroy()
        {
            if (speedRange != null) speedRange.Judged -= OnJudged;
        }

        void OnJudged(int player, bool success) => Change(success ? successHeat : -failCool);

        /// <summary>Cooling key held for <paramref name="dt"/> seconds.</summary>
        public void Cool(float dt) => Change(-coolPerSecond * dt);

        public void Change(float delta)
        {
            if (Overheated || (train != null && train.Stopped)) return;

            Temperature = Mathf.Max(minTemperature, Temperature + delta);
            if (Temperature <= overheatTemperature) return;

            Overheated = true;
            if (train != null) train.Halt();
            Debug.Log($"[Boiler] オーバーヒート：{Temperature:0} °C", this);
            Overheat?.Invoke();
        }
    }
}
