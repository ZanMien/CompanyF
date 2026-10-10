using System.Collections.Generic;
using UnityEngine;

namespace CompanyF.Track
{
    /// <summary>Entry point of the route: where the train starts and the junctions in travel order.</summary>
    public class TrackRoute : MonoBehaviour
    {
        public TrackSegment start;
        public List<TrackJunction> junctions = new List<TrackJunction>();
    }
}
