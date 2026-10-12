using UnityEngine;

namespace CompanyF.Track
{
    /// <summary>
    /// A fork in the route. The transform sits on the centreline where the track splits (rail-top height).
    /// The train announces the junction once its nose is <see cref="triggerDistance"/> before it,
    /// and waits for a direction (<see cref="CompanyF.Train.TrainFollower.SetJunctionChoice"/>).
    /// </summary>
    public class TrackJunction : MonoBehaviour
    {
        [Tooltip("1-based order along the route; junction N uses question N.")]
        public int index;

        public TrackSegment left;
        public TrackSegment right;

        [Tooltip("Passing this junction ends the game.")]
        public bool isFinal;

        [Tooltip("Announce the junction when the train nose is this far (m) before it. Default = three rails_b pieces, i.e. on entering the third piece before the turnout.")]
        [Min(0f)] public float triggerDistance = 3f * 21.72999f;

        public Vector3 Position => transform.position;

        public TrackSegment Choose(bool goLeft) => goLeft ? left : right;
    }
}
