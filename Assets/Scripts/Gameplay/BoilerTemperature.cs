using System;
using CompanyF.Train;
using UnityEngine;

namespace CompanyF.Gameplay
{
    /// <summary>
    /// Boiler temperature. Coal heats it, an open furnace door cools it (see <see cref="FurnaceController"/>).
    /// Reaching <see cref="overheatTemperature"/> halts the train and ends the game.
    /// </summary>
    public class BoilerTemperature : MonoBehaviour
    {
        [SerializeField] TrainFollower train;

        [Header("Temperature (°C)")]
        public float startTemperature = 50f;
        public float minTemperature = 50f;
        [Tooltip("Reaching this (or more) ends the game.")]
        public float overheatTemperature = 1500f;

        [Header("Cooling key (currently disabled, see TrainControls)")]
        [Min(0f)] public float coolPerSecond = 20f;

        /// <summary>Raised once when the boiler overheats (after the train has been halted).</summary>
        public event Action Overheat;

        public float Temperature { get; private set; }
        public bool Overheated { get; private set; }

        void Awake() => Temperature = Mathf.Max(startTemperature, minTemperature);

        void Start()
        {
            if (train == null) train = FindFirstObjectByType<TrainFollower>();
        }

        /// <summary>Cooling key held for <paramref name="dt"/> seconds.</summary>
        public void Cool(float dt) => Change(-coolPerSecond * dt);

        public void Change(float delta)
        {
            if (Overheated || (train != null && train.Stopped)) return;

            Temperature = Mathf.Max(minTemperature, Temperature + delta);
            if (Temperature < overheatTemperature) return;

            Overheated = true;
            if (train != null) train.Halt();
            Debug.Log($"[Boiler] GAMEOVER：{Temperature:0} °C", this);
            Overheat?.Invoke();
        }
    }
}
