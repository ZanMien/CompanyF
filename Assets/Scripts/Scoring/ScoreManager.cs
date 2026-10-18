using CompanyF.Gameplay;
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
        [SerializeField] BoilerTemperature boiler;

        [Header("Distance")]
        [Min(0f)] public float metersPerPiece = 100f;

        [Header("Score")]
        [Min(0)] public int maxScore = 100;
        [Min(0)] public int pointsPerPiece = 1;

        public float Distance { get; private set; }
        public int Score { get; private set; }
        public int PiecesPassed { get; private set; }
        /// <summary>The game is over (reached the end, or overheated).</summary>
        public bool Finished { get; private set; }
        /// <summary>The game ended because the boiler overheated.</summary>
        public bool Overheated { get; private set; }

        void Start()
        {
            if (boiler == null) boiler = FindFirstObjectByType<BoilerTemperature>();
            if (boiler != null) boiler.Overheat += OnOverheat;

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
            if (boiler != null) boiler.Overheat -= OnOverheat;
            if (train == null) return;
            train.PieceExited -= OnPieceExited;
            train.ReachedEnd -= OnReachedEnd;
        }

        void OnOverheat()
        {
            if (Finished) return;
            Finished = true;
            Overheated = true;
            Debug.Log($"[Score] オーバーヒート：{PiecesPassed} 節，距離 {Distance:0}m，分數 {Score}", this);
        }

        void OnPieceExited(Transform piece, TrackSegment segment) => NotifyPiece($"{segment.name}/{piece.name}");

        void OnReachedEnd()
        {
            if (Finished) return;
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
