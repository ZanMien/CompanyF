using System;
using System.Collections.Generic;
using CompanyF.Gameplay;
using CompanyF.Track;
using CompanyF.Train;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CompanyF.Quiz
{
    public enum QuizChoice { None, Left, Right }

    /// <summary>Outcome of one junction question, kept for scoring and the results screen.</summary>
    [Serializable]
    public class QuizResult
    {
        public int questionNumber;
        public string question;
        public QuizChoice player1;
        public QuizChoice player2;
        public bool success;
        [Tooltip("Branch the train took.")] public QuizChoice direction;
    }

    /// <summary>
    /// Junction quiz. When the train announces a junction: game time slows down, the speed bar pauses,
    /// both players secretly pick left/right during the answer phase, then the picks are revealed.
    /// Matching picks send the train that way; otherwise the branch is random.
    /// The quiz itself runs on real (unscaled) time.
    /// </summary>
    public class QuizManager : MonoBehaviour
    {
        public enum QuizPhase { Idle, Answer, Reveal }

        [SerializeField] TrainFollower train;
        [SerializeField] SpeedRangeController speedRange;
        public QuizQuestionSet questionSet;

        [Header("Timing (real seconds)")]
        [Min(0.1f)] public float answerTime = 4f;
        [Min(0f)] public float revealTime = 1f;
        [Tooltip("Game time runs this many times slower during the quiz.")]
        [Min(1f)] public float slowFactor = 15f;

        [Header("Input")]
        public Key player1Left = Key.A;
        public Key player1Right = Key.D;
        public Key player2Left = Key.LeftArrow;
        public Key player2Right = Key.RightArrow;

        [Tooltip("One entry per answered junction, in order.")]
        [SerializeField] List<QuizResult> results = new List<QuizResult>();

        /// <summary>Raised the moment the direction is decided (both answered, or answer time ran out).</summary>
        public event Action<QuizResult> Decided;

        public IReadOnlyList<QuizResult> Results => results;
        public QuizPhase Phase { get; private set; }
        public QuizQuestionSet.Question Question { get; private set; }
        public QuizChoice Player1Choice { get; private set; }
        public QuizChoice Player2Choice { get; private set; }
        /// <summary>Result of the running question; null until decided.</summary>
        public QuizResult Current { get; private set; }
        public float PhaseElapsed { get; private set; }
        /// <summary>3, 2, 1, 0 — one number per second of the answer phase.</summary>
        public int CountdownNumber => Mathf.Max(0, Mathf.CeilToInt(answerTime - PhaseElapsed) - 1);

        TrackJunction junction;
        float savedTimeScale = 1f;

        void Start()
        {
            if (train == null) train = FindFirstObjectByType<TrainFollower>();
            if (speedRange == null) speedRange = FindFirstObjectByType<SpeedRangeController>();
            if (train != null) train.JunctionApproaching += Begin;
            else Debug.LogError("[Quiz] No TrainFollower in the scene.", this);
        }

        void OnDestroy()
        {
            if (train != null) train.JunctionApproaching -= Begin;
            if (Phase != QuizPhase.Idle) End();
        }

        void Update()
        {
            if (Phase == QuizPhase.Idle) return;
            Tick(Time.unscaledDeltaTime, Read(player1Left, player1Right), Read(player2Left, player2Right));
        }

        static QuizChoice Read(Key left, Key right)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return QuizChoice.None;
            if (keyboard[left].wasPressedThisFrame) return QuizChoice.Left;
            if (keyboard[right].wasPressedThisFrame) return QuizChoice.Right;
            return QuizChoice.None;
        }

        /// <summary>Starts the question for <paramref name="target"/> (junction N asks question N).</summary>
        public void Begin(TrackJunction target)
        {
            if (Phase != QuizPhase.Idle)
            {
                Debug.LogWarning($"[Quiz] {target.name} reached while a question is running; picking at random.", this);
                SetDirection(target, RandomSide());
                return;
            }

            var question = questionSet != null ? questionSet.Get(target.index - 1) : null;
            if (question == null)
            {
                Debug.LogWarning($"[Quiz] No question #{target.index}; {target.name} picks at random.", this);
                SetDirection(target, RandomSide());
                return;
            }

            junction = target;
            Question = question;
            Player1Choice = QuizChoice.None;
            Player2Choice = QuizChoice.None;
            Current = null;
            PhaseElapsed = 0f;
            Phase = QuizPhase.Answer;

            savedTimeScale = Time.timeScale;
            Time.timeScale = savedTimeScale / slowFactor;
            if (speedRange != null) speedRange.PointerPaused = true;
        }

        /// <summary>Advances the quiz by <paramref name="dt"/> real seconds with this frame's key presses.</summary>
        public void Tick(float dt, QuizChoice player1Press, QuizChoice player2Press)
        {
            if (Phase == QuizPhase.Idle) return;
            PhaseElapsed += dt;

            if (Phase == QuizPhase.Answer)
            {
                if (Player1Choice == QuizChoice.None) Player1Choice = player1Press;
                if (Player2Choice == QuizChoice.None) Player2Choice = player2Press;

                if (Current == null && Player1Choice != QuizChoice.None && Player2Choice != QuizChoice.None)
                    Decide();

                if (PhaseElapsed >= answerTime)
                {
                    if (Current == null) Decide();
                    Phase = QuizPhase.Reveal;
                    PhaseElapsed = 0f;
                }
            }
            else if (PhaseElapsed >= revealTime)
            {
                End();
            }
        }

        void Decide()
        {
            bool success = Player1Choice != QuizChoice.None && Player1Choice == Player2Choice;
            Current = new QuizResult
            {
                questionNumber = junction.index,
                question = Question.text,
                player1 = Player1Choice,
                player2 = Player2Choice,
                success = success,
                direction = success ? Player1Choice : RandomSide(),
            };
            results.Add(Current);
            SetDirection(junction, Current.direction);
            Debug.Log($"[Quiz] Q{Current.questionNumber}: P1={Current.player1} P2={Current.player2} -> " +
                      $"{(success ? "成功" : "失敗")} / {Current.direction}", this);
            Decided?.Invoke(Current);
        }

        void End()
        {
            Phase = QuizPhase.Idle;
            PhaseElapsed = 0f;
            junction = null;
            Time.timeScale = savedTimeScale;
            if (speedRange != null) speedRange.PointerPaused = false;
        }

        void SetDirection(TrackJunction target, QuizChoice side)
        {
            if (train != null) train.SetJunctionChoice(target, side == QuizChoice.Left);
        }

        static QuizChoice RandomSide() => UnityEngine.Random.value < 0.5f ? QuizChoice.Left : QuizChoice.Right;
    }
}
