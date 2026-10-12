using CompanyF.Quiz;
using CompanyF.Track;
using CompanyF.Train;
using UnityEngine;

namespace CompanyF.Scoring
{
    /// <summary>
    /// Distance and score. Every track model the train leaves counts as one piece:
    /// +metersPerPiece distance and +pointsPerPiece score (score capped at maxScore).
    /// After a quiz, pieces passed during the bonus window score quizSuccessPointsPerPiece if the players agreed.
    /// The bonus window runs on real time. When it starts at the question appearing, the outcome is not known yet,
    /// so those pieces score the base points first and the difference is added when the quiz ends.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public enum BonusStart { QuizShown, QuizEnded }

        [SerializeField] TrainFollower train;
        [SerializeField] QuizManager quiz;

        [Header("Distance")]
        [Min(0f)] public float metersPerPiece = 100f;

        [Header("Score")]
        [Min(0)] public int maxScore = 100;
        [Min(0)] public int pointsPerPiece = 1;
        [Min(0)] public int quizSuccessPointsPerPiece = 2;

        [Header("Quiz bonus")]
        [Tooltip("Real seconds the quiz bonus window lasts.")]
        [Min(0f)] public float bonusDuration = 3f;
        [Tooltip("QuizShown: window opens when the question appears. QuizEnded: when the quiz screen closes.")]
        public BonusStart bonusStart = BonusStart.QuizShown;

        public float Distance { get; private set; }
        public int Score { get; private set; }
        public int PiecesPassed { get; private set; }
        public bool Finished { get; private set; }

        float windowStart = -1f;
        bool outcomeKnown;
        bool outcomeSuccess;
        int pendingPieces;

        bool InWindow(float now) => windowStart >= 0f && now - windowStart < bonusDuration;

        void Start()
        {
            if (train == null) train = FindFirstObjectByType<TrainFollower>();
            if (quiz == null) quiz = FindFirstObjectByType<QuizManager>();

            if (train != null)
            {
                train.PieceExited += OnPieceExited;
                train.ReachedEnd += OnReachedEnd;
            }
            else Debug.LogError("[Score] No TrainFollower in the scene.", this);

            if (quiz != null)
            {
                quiz.Started += OnQuizStarted;
                quiz.Ended += OnQuizEnded;
            }
        }

        void OnDestroy()
        {
            if (train != null)
            {
                train.PieceExited -= OnPieceExited;
                train.ReachedEnd -= OnReachedEnd;
            }
            if (quiz != null)
            {
                quiz.Started -= OnQuizStarted;
                quiz.Ended -= OnQuizEnded;
            }
        }

        void OnPieceExited(Transform piece, TrackSegment segment) => NotifyPiece($"{segment.name}/{piece.name}", Time.unscaledTime);
        void OnQuizStarted(TrackJunction junction) => NotifyQuizStarted(Time.unscaledTime);
        void OnQuizEnded(QuizResult result) => NotifyQuizEnded(result.success, Time.unscaledTime);

        void OnReachedEnd()
        {
            Finished = true;
            Debug.Log($"[Score] 終點：{PiecesPassed} 節，距離 {Distance:0}m，分數 {Score}", this);
        }

        /// <summary>One piece passed at real time <paramref name="now"/>.</summary>
        public void NotifyPiece(string label, float now)
        {
            PiecesPassed++;
            Distance += metersPerPiece;

            int points = pointsPerPiece;
            string note = "";
            if (InWindow(now))
            {
                if (!outcomeKnown)
                {
                    pendingPieces++;
                    note = "（問答加分時段，結果待定）";
                }
                else if (outcomeSuccess)
                {
                    points = quizSuccessPointsPerPiece;
                    note = "（問答成功加分）";
                }
            }

            int gained = AddScore(points);
            Debug.Log($"[Score] 通過 #{PiecesPassed} {label}：距離 {Distance:0}m，分數 {Score}（+{gained}）{note}", this);
        }

        public void NotifyQuizStarted(float now)
        {
            if (bonusStart != BonusStart.QuizShown) return;
            windowStart = now;
            outcomeKnown = false;
            pendingPieces = 0;
        }

        public void NotifyQuizEnded(bool success, float now)
        {
            if (bonusStart == BonusStart.QuizEnded)
            {
                windowStart = now;
                pendingPieces = 0;
            }
            else if (pendingPieces > 0)
            {
                int extra = success ? pendingPieces * (quizSuccessPointsPerPiece - pointsPerPiece) : 0;
                int gained = AddScore(extra);
                Debug.Log($"[Score] 問答{(success ? "成功" : "失敗")}：加分時段內通過 {pendingPieces} 節，補算 +{gained} → 分數 {Score}", this);
                pendingPieces = 0;
            }
            else
            {
                Debug.Log($"[Score] 問答{(success ? "成功" : "失敗")}：加分時段內沒有通過任何軌道，不補算", this);
            }

            outcomeKnown = true;
            outcomeSuccess = success;
        }

        int AddScore(int points)
        {
            int before = Score;
            Score = Mathf.Clamp(Score + points, 0, maxScore);
            return Score - before;
        }
    }
}
