using UnityEngine;

namespace CompanyF.Track
{
    /// <summary>
    /// A fork in the route. The transform sits on the centreline where the track splits (rail-top height);
    /// use it with <see cref="promptDistance"/> to decide when to show the left/right choice UI.
    /// </summary>
    public class TrackJunction : MonoBehaviour
    {
        [Tooltip("1-based order along the route.")]
        public int index;

        public TrackSegment left;
        public TrackSegment right;

        [Tooltip("Passing this junction ends the game.")]
        public bool isFinal;

        [Tooltip("Distance (m) before the junction at which the direction choice UI should appear.")]
        public float promptDistance = 80f;

        public Vector3 Position => transform.position;

        public TrackSegment Choose(bool goLeft) => goLeft ? left : right;

        void OnDrawGizmos()
        {
            Gizmos.color = isFinal ? Color.red : Color.yellow;
            Gizmos.DrawSphere(transform.position, 1.5f);
            Gizmos.DrawWireSphere(transform.position, promptDistance);
        }
    }
}
