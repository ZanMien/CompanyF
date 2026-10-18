using CompanyF.Train;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CompanyF.Gameplay
{
    /// <summary>
    /// Hold/press controls that work at any time, independent of whose turn it is on the speed bar.
    /// P1: shift up, brake. P2: cool the boiler, whistle.
    /// </summary>
    public class TrainControls : MonoBehaviour
    {
        [SerializeField] SpeedRangeController speedRange;
        [SerializeField] BoilerTemperature boiler;
        [SerializeField] TrainFollower train;

        [Header("P1")]
        public Key shiftUpKey = Key.W;
        public Key brakeKey = Key.S;

        [Header("P2")]
        public Key whistleKey = Key.UpArrow;
        public Key coolKey = Key.DownArrow;

        [Header("Whistle")]
        [Tooltip("Plays (looped) while the whistle key is held. Assign the clip later.")]
        public AudioSource whistle;

        public bool IsBraking { get; private set; }
        public bool IsCooling { get; private set; }
        public bool IsWhistling { get; private set; }

        void Start()
        {
            if (speedRange == null) speedRange = FindFirstObjectByType<SpeedRangeController>();
            if (boiler == null) boiler = FindFirstObjectByType<BoilerTemperature>();
            if (train == null) train = FindFirstObjectByType<TrainFollower>();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            bool active = keyboard != null && (train == null || !train.Stopped);

            IsBraking = active && keyboard[brakeKey].isPressed;
            IsCooling = active && keyboard[coolKey].isPressed;
            IsWhistling = active && keyboard[whistleKey].isPressed;

            if (active && speedRange != null && keyboard[shiftUpKey].wasPressedThisFrame && speedRange.TryShiftUp())
                Debug.Log($"[Gear] シフトアップ → {speedRange.Gear}（上限 {speedRange.GearCap:0}）", this);

            if (IsBraking && speedRange != null) speedRange.Brake(Time.deltaTime);
            if (IsCooling && boiler != null) boiler.Cool(Time.deltaTime);
            UpdateWhistle();
        }

        void UpdateWhistle()
        {
            if (whistle == null || whistle.clip == null) return;
            if (IsWhistling && !whistle.isPlaying) whistle.Play();
            else if (!IsWhistling && whistle.isPlaying) whistle.Stop();
        }
    }
}
