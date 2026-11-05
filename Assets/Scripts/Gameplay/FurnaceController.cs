using System;
using CompanyF.Train;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CompanyF.Gameplay
{
    public enum DoorState { Closed, Opening, Open, Loading, Closing }

    /// <summary>What the shovel is currently doing.</summary>
    public enum ShovelAction
    {
        None,
        /// <summary>Coal thrown through the open door; it lands at <see cref="FurnaceController.loadTime"/>.</summary>
        Throw,
        /// <summary>P2 pressed while the door was not open: the coal is tipped onto the floor.</summary>
        FailDoorNotOpen,
        /// <summary>P1 shut the door before the coal reached the top of its arc: the coal hits the door.</summary>
        FailBlocked,
    }

    /// <summary>
    /// Furnace door and coal rules. P1 opens/closes the door, P2 shovels coal.
    ///   Door: Closed -F-> Opening -> Open -F-> Closing -> Closed (the key does nothing while the door is moving).
    ///   Open + Enter: throw. The coal lands at <see cref="loadTime"/> (+heat, cooling restarts) and the door stays open.
    ///   F before <see cref="apexTime"/> of a throw: the door closes and the coal is blocked (P2 failure, no heat).
    ///   F after <see cref="apexTime"/>: the door closes and the coal still lands.
    ///   Enter while the door is not open: P2 failure, the coal is tipped out.
    ///   Enter during any shovel action or during the lockout after a failure: ignored.
    /// While the door is open (from fully open until fully closed) the boiler loses heat: first after
    /// <see cref="firstCoolDelay"/>, then every <see cref="repeatCoolInterval"/>. Each coal restarts this timing.
    ///
    /// This class only keeps the rules and the clocks. <see cref="FurnaceDoorAnimator"/> and
    /// <see cref="ShovelAnimator"/> pose the models from <see cref="State"/>, <see cref="StateTime"/>,
    /// <see cref="Action"/> and <see cref="ActionTime"/>, so animation and rules cannot drift apart.
    /// </summary>
    public class FurnaceController : MonoBehaviour
    {
        const float Epsilon = 1e-4f;

        [SerializeField] BoilerTemperature boiler;
        [SerializeField] TrainFollower train;

        [Header("Door times (s)")]
        [Min(0f)] public float openTime = 0.3f;
        [Min(0f)] public float closeTime = 0.3f;
        [Tooltip("On: the door states end by themselves. Off: call CompleteDoorState() (e.g. from an animation event).")]
        public bool useTimers = true;

        [Header("Shovel times (s, from P2's key press)")]
        [Tooltip("The coal lands in the furnace at this time.")]
        [Min(0f)] public float loadTime = 0.5f;
        [Tooltip("The coal is at the top of its arc. F before this blocks the coal; F after it does not.")]
        [Min(0f)] public float apexTime = 0.42f;
        [Tooltip("Length of both failure animations.")]
        [Min(0f)] public float failAnimationTime = 0.75f;

        [Header("Coal")]
        [Tooltip("Added the moment the coal lands.")]
        [Min(0f)] public float coalHeat = 100f;

        [Header("Cooling while the door is open")]
        [Min(0f)] public float coolAmount = 100f;
        [Tooltip("Seconds from the door fully opening, or from the last coal landing, to the first cooling.")]
        [Min(0.01f)] public float firstCoolDelay = 1.2f;
        [Tooltip("Seconds between coolings after the first. A partial wait when the door closes is dropped.")]
        [Min(0.01f)] public float repeatCoolInterval = 0.6f;

        [Header("P2 failure")]
        [Tooltip("After a failure P2's key does nothing for this long, and pressing it does not restart the wait.")]
        [Min(0f)] public float p2LockoutTime = 0.75f;

        [Header("Input")]
        public Key p1DoorKey = Key.F;
        public Key p2CoalKey = Key.Enter;
        [Tooltip("Extra key for P2 (None to disable).")]
        public Key p2CoalAltKey = Key.NumpadEnter;

        public event Action<DoorState> StateEntered;
        public event Action<DoorState> StateExited;
        /// <summary>Coal landed: (heat added).</summary>
        public event Action<float> CoalAdded;
        /// <summary>The open door actually lowered the temperature: (heat removed).</summary>
        public event Action<float> Cooled;
        public event Action P2Failed;

        public DoorState State { get; private set; } = DoorState.Closed;
        public float StateTime { get; private set; }
        public float P2LockoutLeft { get; private set; }
        /// <summary>Door counts as open for cooling: from fully open until fully closed.</summary>
        public bool CoolingActive { get; private set; }
        public ShovelAction Action { get; private set; }
        /// <summary>Seconds since P2's key press started the current <see cref="Action"/>.</summary>
        public float ActionTime { get; private set; }

        float coolTimer;
        float nextCoolWait;

        void Start()
        {
            if (boiler == null) boiler = FindFirstObjectByType<BoilerTemperature>();
            if (train == null) train = FindFirstObjectByType<TrainFollower>();
        }

        void Update()
        {
            if (train != null && train.Stopped) return;
            var keyboard = Keyboard.current;
            bool door = keyboard != null && keyboard[p1DoorKey].wasPressedThisFrame;
            bool coal = keyboard != null && (keyboard[p2CoalKey].wasPressedThisFrame
                                             || (p2CoalAltKey != Key.None && keyboard[p2CoalAltKey].wasPressedThisFrame));
            Tick(Time.deltaTime, door, coal);
        }

        /// <summary>Advances by <paramref name="dt"/> seconds with this frame's key presses.</summary>
        public void Tick(float dt, bool doorPressed, bool coalPressed)
        {
            StateTime += dt;
            P2LockoutLeft = Mathf.Max(0f, P2LockoutLeft - dt);
            if (Action != ShovelAction.None) ActionTime += dt;

            if (CoolingActive)
            {
                coolTimer += dt;
                while (coolTimer >= nextCoolWait - Epsilon)
                {
                    coolTimer -= nextCoolWait;
                    nextCoolWait = repeatCoolInterval;
                    Cool();
                }
            }

            if (doorPressed) PressDoor();
            if (coalPressed) PressCoal();

            AdvanceAction();
            if (useTimers) AdvanceDoor();
        }

        void Cool()
        {
            if (boiler == null) return;
            float before = boiler.Temperature;
            boiler.Change(-coolAmount);
            if (boiler.Temperature < before) Cooled?.Invoke(before - boiler.Temperature);
        }

        void RestartCooling()
        {
            coolTimer = 0f;
            nextCoolWait = firstCoolDelay;
        }

        void PressDoor()
        {
            switch (State)
            {
                case DoorState.Closed:
                    Enter(DoorState.Opening);
                    break;
                case DoorState.Open:
                    Enter(DoorState.Closing);
                    break;
                case DoorState.Loading:
                    if (ActionTime < apexTime - Epsilon)
                    {
                        Action = ShovelAction.FailBlocked;
                        FailP2("P1 closed the door before the coal passed the top of its arc");
                    }
                    Enter(DoorState.Closing);
                    break;
                // Opening / Closing: the door is moving, the key does nothing.
            }
        }

        void PressCoal()
        {
            if (P2LockoutLeft > 0f || Action != ShovelAction.None) return;

            ActionTime = 0f;
            if (State == DoorState.Open)
            {
                Action = ShovelAction.Throw;
                Enter(DoorState.Loading);
            }
            else
            {
                Action = ShovelAction.FailDoorNotOpen;
                FailP2($"door is {State}");
            }
        }

        void FailP2(string reason)
        {
            P2LockoutLeft = p2LockoutTime;
            Debug.Log($"[Furnace] 2P 失敗（{reason}）", this);
            P2Failed?.Invoke();
        }

        void AdvanceAction()
        {
            switch (Action)
            {
                case ShovelAction.Throw:
                    if (ActionTime < loadTime - Epsilon) return;
                    Action = ShovelAction.None;
                    if (boiler != null) boiler.Change(coalHeat);
                    Debug.Log($"[Furnace] 石炭投入 +{coalHeat:0}°C → {(boiler != null ? boiler.Temperature : 0f):0}°C", this);
                    CoalAdded?.Invoke(coalHeat);
                    RestartCooling();
                    if (State == DoorState.Loading) Enter(DoorState.Open);
                    break;
                case ShovelAction.FailDoorNotOpen:
                case ShovelAction.FailBlocked:
                    if (ActionTime >= failAnimationTime - Epsilon) Action = ShovelAction.None;
                    break;
            }
        }

        void AdvanceDoor()
        {
            if ((State == DoorState.Opening && StateTime >= openTime - Epsilon)
                || (State == DoorState.Closing && StateTime >= closeTime - Epsilon))
                CompleteDoorState();
        }

        /// <summary>Ends Opening or Closing. Called by the timers, or by an animation when <see cref="useTimers"/> is off.</summary>
        public void CompleteDoorState()
        {
            if (State == DoorState.Opening) Enter(DoorState.Open);
            else if (State == DoorState.Closing) Enter(DoorState.Closed);
        }

        void Enter(DoorState next)
        {
            var previous = State;
            StateExited?.Invoke(previous);
            State = next;
            StateTime = 0f;

            if (next == DoorState.Open && previous == DoorState.Opening)
            {
                CoolingActive = true;
                RestartCooling();
            }
            else if (next == DoorState.Closed)
            {
                CoolingActive = false;
            }

            StateEntered?.Invoke(next);
        }
    }
}
