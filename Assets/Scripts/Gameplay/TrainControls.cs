using CompanyF.Train;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CompanyF.Gameplay
{
    /// <summary>
    /// Hold controls. P1's whistle is live, one blast per key press: it stops at <see cref="whistleMaxTime"/> or when the
    /// key is released, then cools down for <see cref="whistleCooldown"/>. P1's brake (S) and P2's cooling (↓) are kept for
    /// later and stay off until <see cref="otherInputEnabled"/> is turned on. Braking has no effect yet,
    /// since the speed currently follows the boiler temperature.
    /// </summary>
    public class TrainControls : MonoBehaviour
    {
        public enum WhistleState { Ready, Sounding, Cooldown }

        [SerializeField] BoilerTemperature boiler;
        [SerializeField] TrainFollower train;

        [Tooltip("Off for now: S and ↓ do nothing until these controls are used again. The whistle is not affected.")]
        public bool otherInputEnabled = false;

        [Header("P1")]
        public Key whistleKey = Key.W;
        public Key brakeKey = Key.S;

        [Header("P2")]
        public Key coolKey = Key.DownArrow;

        [Header("Whistle")]
        [Tooltip("Plays (looped) while the whistle sounds. Works without a clip; assign one later.")]
        public AudioSource whistle;
        [Tooltip("Longest a single blast can last (s), even with the key still held.")]
        [Min(0.01f)] public float whistleMaxTime = 1f;
        [Tooltip("Seconds after each blast before the whistle can be used again.")]
        [Min(0f)] public float whistleCooldown = 5f;

        public bool IsBraking { get; private set; }
        public bool IsCooling { get; private set; }
        public WhistleState Whistle { get; private set; } = WhistleState.Ready;
        public bool IsWhistling => Whistle == WhistleState.Sounding;
        public float WhistleCooldownLeft { get; private set; }

        float blastTime;
        bool whistleKeyWasHeld;

        // A key already down when the controls come on (held through the opening) counts as an old press.
        void OnEnable() => whistleKeyWasHeld = Keyboard.current != null && Keyboard.current[whistleKey].isPressed;

        void Start()
        {
            if (boiler == null) boiler = FindFirstObjectByType<BoilerTemperature>();
            if (train == null) train = FindFirstObjectByType<TrainFollower>();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            bool running = keyboard != null && (train == null || !train.Stopped);

            TickWhistle(Time.deltaTime, running && keyboard[whistleKey].isPressed);
            IsBraking = running && otherInputEnabled && keyboard[brakeKey].isPressed;
            IsCooling = running && otherInputEnabled && keyboard[coolKey].isPressed;

            if (IsCooling && boiler != null) boiler.Cool(Time.deltaTime);
            UpdateWhistle();
        }

        /// <summary>Advances the whistle by <paramref name="dt"/> seconds. Only a fresh press starts a blast.</summary>
        public void TickWhistle(float dt, bool keyHeld)
        {
            bool pressed = keyHeld && !whistleKeyWasHeld;
            whistleKeyWasHeld = keyHeld;

            if (Whistle == WhistleState.Sounding)
            {
                blastTime += dt;
                if (!keyHeld || blastTime >= whistleMaxTime - 1e-4f)
                {
                    Whistle = WhistleState.Cooldown;
                    WhistleCooldownLeft = whistleCooldown;
                }
                return;
            }

            if (Whistle == WhistleState.Cooldown)
            {
                WhistleCooldownLeft = Mathf.Max(0f, WhistleCooldownLeft - dt);
                if (WhistleCooldownLeft <= 1e-4f) Whistle = WhistleState.Ready;
            }

            if (Whistle == WhistleState.Ready && pressed)
            {
                Whistle = WhistleState.Sounding;
                blastTime = 0f;
            }
        }

        void UpdateWhistle()
        {
            if (whistle == null || whistle.clip == null) return;
            if (IsWhistling && !whistle.isPlaying) whistle.Play();
            else if (!IsWhistling && whistle.isPlaying) whistle.Stop();
        }
    }
}
