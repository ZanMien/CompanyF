using CompanyF.Train;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CompanyF.Gameplay
{
    /// <summary>
    /// Hold controls kept for later use: P1 brake (S), P2 whistle (↑) and cooling (↓).
    /// Input is switched off for now (<see cref="inputEnabled"/>). Braking has no effect yet,
    /// since the speed currently follows the boiler temperature.
    /// </summary>
    public class TrainControls : MonoBehaviour
    {
        [SerializeField] BoilerTemperature boiler;
        [SerializeField] TrainFollower train;

        [Tooltip("Off for now: S / ↑ / ↓ do nothing until these controls are used again.")]
        public bool inputEnabled = false;

        [Header("P1")]
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
            if (boiler == null) boiler = FindFirstObjectByType<BoilerTemperature>();
            if (train == null) train = FindFirstObjectByType<TrainFollower>();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            bool active = inputEnabled && keyboard != null && (train == null || !train.Stopped);

            IsBraking = active && keyboard[brakeKey].isPressed;
            IsCooling = active && keyboard[coolKey].isPressed;
            IsWhistling = active && keyboard[whistleKey].isPressed;

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
