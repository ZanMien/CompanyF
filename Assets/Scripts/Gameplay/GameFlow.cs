using System;
using CompanyF.Train;
using UnityEngine;

namespace CompanyF.Gameplay
{
    public enum GamePhase
    {
        /// <summary>"Ready" message: nothing moves and no key works.</summary>
        Ready,
        /// <summary>The train runs and the time limit counts down.</summary>
        Running,
        /// <summary>Time ran out: the train is stopped and the finish message shows.</summary>
        Finishing,
        /// <summary>Cleared or failed; the results screen has taken over.</summary>
        Ended,
    }

    /// <summary>
    /// The round: opening messages, then the time limit. The gameplay components stay switched off until the round
    /// starts, so the train, the keys and the animals all begin at the same moment.
    /// Ends on reaching the end of the line, on overheating, on hitting an animal without the whistle, or on time.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        [SerializeField] TrainFollower train;
        [SerializeField] BoilerTemperature boiler;
        [SerializeField] AnimalHerd herd;
        [Tooltip("Switched off until the round starts.")]
        [SerializeField] Behaviour[] startsWithRound;

        [Header("Opening messages (s)")]
        [Min(0f)] public float readyTime = 1.5f;
        [Min(0f)] public float goTime = 0.5f;

        [Header("Time limit")]
        [Min(0f)] public float timeLimit = 60f;
        [Tooltip("The countdown number grows from this many seconds left.")]
        [Min(0f)] public float emphasisSeconds = 5f;
        [Min(0f)] public float emphasisScale = 2f;

        [Header("Time up")]
        [Min(0f)] public float finishMessageTime = 1.5f;

        /// <summary>The round is lost by running out of time or hitting an animal (overheating has its own event).</summary>
        public event Action GameOver;

        public GamePhase Phase { get; private set; } = GamePhase.Ready;
        public float TimeLeft { get; private set; }

        /// <summary>Whole seconds shown on the countdown.</summary>
        public int SecondsShown => Mathf.Max(0, Mathf.CeilToInt(TimeLeft - 1e-4f));
        public bool Emphasised => Phase == GamePhase.Running && SecondsShown <= emphasisSeconds;
        public bool ShowsGoMessage => Phase == GamePhase.Running && goLeft > 0f;

        float phaseTime;
        float goLeft;

        void Awake()
        {
            TimeLeft = timeLimit;
            SetGameplayEnabled(false);
        }

        void Start()
        {
            if (train == null) train = GetComponent<TrainFollower>();
            if (boiler == null) boiler = GetComponent<BoilerTemperature>();
            if (herd == null) herd = GetComponent<AnimalHerd>();

            if (train != null) train.ReachedEnd += End;
            if (boiler != null) boiler.Overheat += End;
            if (herd != null) herd.StruckWithoutWhistle += Fail;
        }

        void OnDestroy()
        {
            if (train != null) train.ReachedEnd -= End;
            if (boiler != null) boiler.Overheat -= End;
            if (herd != null) herd.StruckWithoutWhistle -= Fail;
        }

        void Update() => Tick(Time.deltaTime);

        public void Tick(float dt)
        {
            switch (Phase)
            {
                case GamePhase.Ready:
                    phaseTime += dt;
                    if (phaseTime >= readyTime - 1e-4f) StartRound();
                    break;

                case GamePhase.Running:
                    goLeft = Mathf.Max(0f, goLeft - dt);
                    TimeLeft = Mathf.Max(0f, TimeLeft - dt);
                    if (TimeLeft <= 1e-4f)
                    {
                        Phase = GamePhase.Finishing;
                        phaseTime = 0f;
                        if (train != null) train.Halt();
                    }
                    break;

                case GamePhase.Finishing:
                    phaseTime += dt;
                    if (phaseTime >= finishMessageTime - 1e-4f) Fail();
                    break;
            }
        }

        void StartRound()
        {
            Phase = GamePhase.Running;
            TimeLeft = timeLimit;
            goLeft = goTime;
            SetGameplayEnabled(true);
        }

        void End() => Phase = GamePhase.Ended;

        void Fail()
        {
            if (Phase == GamePhase.Ended) return;
            Phase = GamePhase.Ended;
            if (train != null) train.Halt();
            GameOver?.Invoke();
        }

        void SetGameplayEnabled(bool on)
        {
            if (startsWithRound == null) return;
            foreach (var behaviour in startsWithRound)
                if (behaviour != null) behaviour.enabled = on;
        }
    }
}
