using System;
using CompanyF.Train;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CompanyF.Gameplay
{
    public enum DoorState { Closed, Opening, Open, Loading, Closing }

    /// <summary>
    /// Furnace door and coal. P1 opens/closes the door, P2 shovels coal in while it is open.
    ///   Closed -F-> Opening -> Open -F-> Closing -> Closed
    ///   Open -Enter-> Loading -> (coal: +heat) -> Closing -> Closed
    /// P2 fails when shovelling at any other time, or when P1's door key and P2's coal key come within
    /// <see cref="simultaneousWindow"/> of each other (no coal goes in). After a failure P2 is locked out briefly.
    /// While the door is open (from fully open until fully closed) the boiler loses heat every <see cref="coolInterval"/>.
    ///
    /// Animation hook: <see cref="StateEntered"/> / <see cref="StateExited"/> fire on every change. Timed states
    /// (Opening, Loading, Closing) end after their time while <see cref="useTimers"/> is on; to drive them from
    /// animations instead, turn it off and call <see cref="CompleteState"/> when the clip finishes.
    /// </summary>
    public class FurnaceController : MonoBehaviour
    {
        const float Epsilon = 1e-4f;

        [SerializeField] BoilerTemperature boiler;
        [SerializeField] TrainFollower train;

        [Header("Animation times (s)")]
        [Min(0f)] public float openTime = 0.3f;
        [Min(0f)] public float loadTime = 0.3f;
        [Min(0f)] public float closeTime = 0.3f;
        [Tooltip("On: timed states end by themselves. Off: call CompleteState() (e.g. from an animation event).")]
        public bool useTimers = true;

        [Header("Coal")]
        [Tooltip("Added the moment the coal goes in (end of Loading).")]
        [Min(0f)] public float coalHeat = 100f;

        [Header("Cooling while the door is open")]
        [Min(0f)] public float coolAmount = 100f;
        [Tooltip("Every full interval of open door removes coolAmount. A partial interval when the door closes is dropped.")]
        [Min(0.01f)] public float coolInterval = 1.2f;

        [Header("P2 failure")]
        [Tooltip("P1's door key and P2's coal key within this many seconds of each other = P2 failure.")]
        [Min(0f)] public float simultaneousWindow = 0.3f;
        [Tooltip("After a failure P2's key does nothing for this long.")]
        [Min(0f)] public float p2LockoutTime = 0.5f;

        [Header("Input")]
        public Key p1DoorKey = Key.F;
        public Key p2CoalKey = Key.Enter;
        [Tooltip("Extra key for P2 (None to disable).")]
        public Key p2CoalAltKey = Key.NumpadEnter;

        public event Action<DoorState> StateEntered;
        public event Action<DoorState> StateExited;
        /// <summary>Coal went in: (heat added).</summary>
        public event Action<float> CoalAdded;
        /// <summary>The open door cooled the boiler: (heat removed).</summary>
        public event Action<float> Cooled;
        public event Action P2Failed;

        public DoorState State { get; private set; } = DoorState.Closed;
        public float StateTime { get; private set; }
        public float P2LockoutLeft { get; private set; }
        /// <summary>Door counts as open for cooling: from fully open until fully closed.</summary>
        public bool CoolingActive { get; private set; }

        float clock;
        float lastDoorPress = float.NegativeInfinity;
        float coolTimer;

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
            clock += dt;
            StateTime += dt;
            P2LockoutLeft = Mathf.Max(0f, P2LockoutLeft - dt);

            if (CoolingActive)
            {
                coolTimer += dt;
                while (coolTimer >= coolInterval - Epsilon)
                {
                    coolTimer -= coolInterval;
                    if (boiler != null) boiler.Change(-coolAmount);
                    Cooled?.Invoke(coolAmount);
                }
            }

            if (doorPressed) PressDoor();
            if (coalPressed) PressCoal();

            if (useTimers && IsTimed(State) && StateTime >= Duration(State) - Epsilon)
                CompleteState();
        }

        void PressDoor()
        {
            switch (State)
            {
                case DoorState.Closed:
                    lastDoorPress = clock;
                    Enter(DoorState.Opening);
                    break;
                case DoorState.Open:
                    lastDoorPress = clock;
                    Enter(DoorState.Closing);
                    break;
                case DoorState.Loading:
                    // P2 shovelled within the window: no coal, the door closes as P1 asked.
                    lastDoorPress = clock;
                    FailP2("P1 closed the door while the coal was going in");
                    Enter(DoorState.Closing);
                    break;
                // Opening / Closing: the door is moving, the key does nothing.
            }
        }

        void PressCoal()
        {
            if (P2LockoutLeft > 0f) return;

            if (State != DoorState.Open) FailP2($"door is {State}");
            else if (clock - lastDoorPress < simultaneousWindow - Epsilon) FailP2("pressed together with P1");
            else Enter(DoorState.Loading);
        }

        void FailP2(string reason)
        {
            P2LockoutLeft = p2LockoutTime;
            Debug.Log($"[Furnace] 2P 失敗（{reason}）", this);
            P2Failed?.Invoke();
        }

        /// <summary>Ends the current timed state (Opening, Loading, Closing). Call from animations when not using timers.</summary>
        public void CompleteState()
        {
            switch (State)
            {
                case DoorState.Opening:
                    Enter(DoorState.Open);
                    break;
                case DoorState.Loading:
                    if (boiler != null) boiler.Change(coalHeat);
                    Debug.Log($"[Furnace] 石炭投入 +{coalHeat:0}°C → {(boiler != null ? boiler.Temperature : 0f):0}°C", this);
                    CoalAdded?.Invoke(coalHeat);
                    Enter(DoorState.Closing);
                    break;
                case DoorState.Closing:
                    Enter(DoorState.Closed);
                    break;
            }
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
                coolTimer = 0f;
            }
            else if (next == DoorState.Closed)
            {
                CoolingActive = false;
                coolTimer = 0f;
            }

            StateEntered?.Invoke(next);
        }

        static bool IsTimed(DoorState s) => s == DoorState.Opening || s == DoorState.Loading || s == DoorState.Closing;

        float Duration(DoorState s) => s == DoorState.Opening ? openTime : s == DoorState.Loading ? loadTime : closeTime;
    }
}
