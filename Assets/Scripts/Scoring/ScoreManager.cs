using CompanyF.Track;
using CompanyF.Train;
using UnityEngine;

namespace CompanyF.Scoring
{
    /// <summary>
    /// Distance and score. Every track model the train leaves counts as one piece:
    /// +metersPerPiece distance and +pointsPerPiece score (score capped at maxScore, distance keeps counting).
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        [SerializeField] TrainFollower train;

        [Header("Distance")]
        [Min(0f)] public float metersPerPiece = 100f;

        [Header("Score")]
        [Min(0)] public int maxScore = 100;
        [Min(0)] public int pointsPerPiece = 1;

        public float Distance { get; private set; }
        public int Score { get; private set; }
        public int PiecesPassed { get; private set; }
        public bool Finished { get; private set; }

        void Start()
        {
            if (train == null) train = FindFirstObjectByType<TrainFollower>();
            if (train == null)
            {
                Debug.LogError("[Score] No TrainFollower in the scene.", this);
                return;
            }
            train.PieceExited += OnPieceExited;
            train.ReachedEnd += OnReachedEnd;
        }

        void OnDestroy()
        {
            if (train == null) return;
            train.PieceExited -= OnPieceExited;
            train.ReachedEnd -= OnReachedEnd;
        }

        void OnPieceExited(Transform piece, TrackSegment segment) => NotifyPiece($"{segment.name}/{piece.name}");

        void OnReachedEnd()
        {
            Finished = true;
            Debug.Log($"[Score] 終點：{PiecesPassed} 節，距離 {Distance:0}m，分數 {Score}", this);
        }

        /// <summary>One piece passed.</summary>
        public void NotifyPiece(string label)
        {
            PiecesPassed++;
            Distance += metersPerPiece;
            int before = Score;
            Score = Mathf.Clamp(Score + pointsPerPiece, 0, maxScore);
            Debug.Log($"[Score] 通過 #{PiecesPassed} {label}：距離 {Distance:0}m，分數 {Score}（+{Score - before}）", this);
        }
    }
}
