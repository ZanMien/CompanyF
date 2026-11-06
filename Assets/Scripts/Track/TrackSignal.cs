using UnityEngine;

namespace CompanyF.Track
{
    /// <summary>
    /// Shows which way a junction is set: the lamp on that side is lit, red when the way ends in a chasm
    /// and green when it is safe. The other lamp stays dark.
    /// </summary>
    public class TrackSignal : MonoBehaviour
    {
        public TrackSegment junction;
        public Renderer leftLamp;
        public Renderer rightLamp;

        public Color safe = Color.green;
        public Color danger = Color.red;
        public Color dark = new Color(0.1f, 0.1f, 0.1f);

        void Update()
        {
            if (junction == null || junction.Following == null) return;

            bool left = junction.branchSelected == junction.branchOnLeft;
            var lit = junction.Following.endsInChasm ? danger : safe;
            if (leftLamp != null) leftLamp.material.color = left ? lit : dark;
            if (rightLamp != null) rightLamp.material.color = left ? dark : lit;
        }
    }
}
