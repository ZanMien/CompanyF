using CompanyF.Train;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CompanyF.Gameplay
{
    /// <summary>
    /// Hold controls that work at any time. P1's whistle is live; P1's brake (S) and P2's cooling (↓) are kept for later
    /// and stay off until <see cref="otherInputEnabled"/> is turned on. Braking has no effect yet,
    /// since the speed currently follows the boiler temperature.
    /// </summary>
    public class TrainControls : MonoBehaviour
    {
        [SerializeField] BoilerTemperature boiler;
        [SerializeField] TrainFollower train;

        [Tooltip("Off for now: S and ↓ do nothing until these controls are used again. The whistle always works.")]
        public bool otherInputEnabled = false;

        [Header("P1")]
        public Key whistleKey = Key.W;
        public Key brakeKey = Key.S;

        [Header("P2")]
        public Key coolKey = Key.DownArrow;

        [Header("Whistle")]
        [Tooltip("Plays (looped) while the whistle key is held. Works without a clip; assign one later.")]
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
            bool running = keyboard != null && (train == null || !train.Stopped);

            IsWhistling = running && keyboard[whistleKey].isPressed;
            IsBraking = running && otherInputEnabled && keyboard[brakeKey].isPressed;
            IsCooling = running && otherInputEnabled && keyboard[coolKey].isPressed;

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
