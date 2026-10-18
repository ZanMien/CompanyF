using System;
using CompanyF.Train;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CompanyF.Gameplay
{
    /// <summary>
    /// The RANGE timing game that drives the train speed.
    /// A pointer sweeps 0 -> 100; the current player presses their key while it is inside the success zone.
    /// Players alternate after every judgement and share one combo. Speed is pushed to the <see cref="TrainFollower"/>.
    /// </summary>
    public class SpeedRangeController : MonoBehaviour
    {
        public const float BarMax = 100f;

        [Header("Train")]
        [SerializeField] TrainFollower train;

        [Tooltip("Train m/s per 1 game speed unit. 1 = speed is m/s; 0.2778 = speed is km/h.")]
        [Min(0f)] public float metersPerSecondPerUnit = 1f;

        [Header("Pointer")]
        [Tooltip("Seconds for the pointer to travel 0 -> 100.")]
        [Min(0.1f)] public float cycleTime = 2.5f;

        [Tooltip("Width of the success zone on the 0-100 bar.")]
        [Range(1f, BarMax)] public float zoneLength = 20f;

        [Header("Combo")]
        [Tooltip("Seconds a combo survives without another success.")]
        [Min(0f)] public float comboDuration = 3f;

        [Tooltip("Speed added when the combo reaches 1, 2, 3, ... The last entry repeats for every higher combo.")]
        public float[] comboSpeedBonus = { 3f, 4.5f, 7.5f, 15f, 15f, 22.5f };

        [Header("Speed")]
        public float startSpeed = 15f;
        public float maxSpeed = 150f;
        public float minSpeed = 15f;

        [Tooltip("Speed lost on a miss, or when the combo times out.")]
        [Min(0f)] public float failPenalty = 30f;

        [Tooltip("Speed lost per second while the combo is 0.")]
        [Min(0f)] public float idleDecayPerSecond = 0.15f;

        [Header("Gears")]
        [Tooltip("The min-max speed range is split evenly into this many gears. Each gear caps the speed at its top.")]
        [Min(1)] public int gearCount = 3;

        [Tooltip("Speed lost per second while braking.")]
        [Min(0f)] public float brakePerSecond = 30f;

        [Header("Input")]
        public Key player1Key = Key.F;
        public Key player2Key = Key.Enter;
        [Tooltip("Extra key for player 2 (None to disable).")]
        public Key player2AltKey = Key.NumpadEnter;

        /// <summary>Raised after every judgement: (player 1 or 2, success).</summary>
        public event Action<int, bool> Judged;

        public float Pointer { get; private set; }
        public float ZoneStart { get; private set; }
        public float ZoneEnd => ZoneStart + zoneLength;
        public int CurrentPlayer { get; private set; } = 1;
        public int Combo { get; private set; }
        public float ComboTimeLeft { get; private set; }
        public float Speed { get; private set; }
        public int Gear { get; private set; } = 1;
        public float GearCap => GearTop(Gear);
        public bool CanShiftUp => !Finished && Gear < gearCount && Speed >= GearCap - 0.001f;
        public bool Finished => train != null && train.Stopped;

        /// <summary>Top speed of <paramref name="gear"/> (1-based).</summary>
        public float GearTop(int gear) => minSpeed + (maxSpeed - minSpeed) * Mathf.Clamp(gear, 1, gearCount) / gearCount;

        /// <summary>Shifts up one gear if the speed has reached the current gear's top. Returns whether it shifted.</summary>
        public bool TryShiftUp()
        {
            if (!CanShiftUp) return false;
            Gear++;
            return true;
        }

        /// <summary>Brakes for <paramref name="dt"/> seconds (down to the minimum speed).</summary>
        public void Brake(float dt)
        {
            if (Finished) return;
            ChangeSpeed(-brakePerSecond * dt);
            ApplySpeed();
        }

        void Start()
        {
            if (train == null) train = FindFirstObjectByType<TrainFollower>();
            if (train == null) Debug.LogError("[SpeedRange] No TrainFollower in the scene.", this);
            ResetGame();
        }

        void Update()
        {
            if (Finished) return;
            Tick(Time.deltaTime, CurrentPlayerPressed());
        }

        /// <summary>Back to the starting state: gear 1, start speed, no combo, player 1, new round.</summary>
        public void ResetGame()
        {
            Gear = 1;
            Speed = Mathf.Clamp(startSpeed, minSpeed, GearCap);
            Combo = 0;
            ComboTimeLeft = 0f;
            CurrentPlayer = 1;
            NewRound();
            ApplySpeed();
        }

        /// <summary>Advances the game by <paramref name="dt"/> seconds; <paramref name="pressed"/> = the current player pressed this frame.</summary>
        public void Tick(float dt, bool pressed)
        {
            if (Combo > 0)
            {
                ComboTimeLeft -= dt;
                if (ComboTimeLeft <= 0f)
                {
                    Combo = 0;
                    ComboTimeLeft = 0f;
                    ChangeSpeed(-failPenalty);
                }
            }
            else
            {
                ChangeSpeed(-idleDecayPerSecond * dt);
            }

            Pointer = Mathf.Min(Pointer + BarMax / cycleTime * dt, BarMax);

            if (pressed)
                Judge(Pointer >= ZoneStart && Pointer <= ZoneEnd);
            else if (Pointer >= BarMax)
                Judge(false);

            ApplySpeed();
        }

        bool CurrentPlayerPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            if (CurrentPlayer == 1) return keyboard[player1Key].wasPressedThisFrame;
            return keyboard[player2Key].wasPressedThisFrame
                   || (player2AltKey != Key.None && keyboard[player2AltKey].wasPressedThisFrame);
        }

        void Judge(bool success)
        {
            if (success)
            {
                Combo++;
                ComboTimeLeft = comboDuration;
                ChangeSpeed(ComboBonus(Combo));
            }
            else
            {
                Combo = 0;
                ComboTimeLeft = 0f;
                ChangeSpeed(-failPenalty);
            }

            Judged?.Invoke(CurrentPlayer, success);
            CurrentPlayer = CurrentPlayer == 1 ? 2 : 1;
            NewRound();
        }

        float ComboBonus(int combo)
        {
            if (comboSpeedBonus == null || comboSpeedBonus.Length == 0) return 0f;
            return comboSpeedBonus[Mathf.Min(combo, comboSpeedBonus.Length) - 1];
        }

        void NewRound()
        {
            Pointer = 0f;
            float length = Mathf.Clamp(zoneLength, 0f, BarMax);
            ZoneStart = UnityEngine.Random.Range(0f, BarMax - length);
        }

        /// <summary>
        /// Speed-ups stop at the current gear's top. Any slow-down shifts down while the speed
        /// is below the top of the gear underneath.
        /// </summary>
        void ChangeSpeed(float delta)
        {
            if (delta >= 0f)
            {
                Speed = Mathf.Clamp(Speed + delta, minSpeed, GearCap);
                return;
            }

            Speed = Mathf.Clamp(Speed + delta, minSpeed, maxSpeed);
            while (Gear > 1 && Speed < GearTop(Gear - 1)) Gear--;
        }

        void ApplySpeed()
        {
            if (train != null) train.speed = Speed * metersPerSecondPerUnit;
        }
    }
}
