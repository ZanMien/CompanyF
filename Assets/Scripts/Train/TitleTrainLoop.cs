using CompanyF.Track;
using UnityEngine;

namespace CompanyF.Train
{
    /// <summary>Title screen background: runs the train along the route, waits at the end, then starts over.</summary>
    [RequireComponent(typeof(TrainFollower))]
    public class TitleTrainLoop : MonoBehaviour
    {
        [Tooltip("Travel speed in m/s.")]
        [Min(0f)] public float speed = 30f;
        [Tooltip("Seconds to wait at the end of the line before starting over.")]
        [Min(0f)] public float waitTime = 10f;

        TrainFollower train;
        float waited;

        void Awake()
        {
            train = GetComponent<TrainFollower>();

            // Junctions take whichever way does not end in a chasm.
            foreach (var segment in FindObjectsByType<TrackSegment>(FindObjectsSortMode.None))
                if (segment.branch != null && segment.Following.endsInChasm)
                    segment.branchSelected = !segment.branchSelected;
        }

        void Update()
        {
            train.speed = speed;
            if (!train.Stopped) return;

            waited += Time.deltaTime;
            if (waited < waitTime) return;

            waited = 0f;
            train.Restart();
        }
    }
}
