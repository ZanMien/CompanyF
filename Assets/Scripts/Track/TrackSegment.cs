using System.Collections.Generic;
using UnityEngine;

namespace CompanyF.Track
{
    /// <summary>
    /// One continuous run of track the train follows. Points are the track centreline at rail-top height,
    /// stored in this transform's local space so the whole route can be moved with its parent.
    /// </summary>
    public class TrackSegment : MonoBehaviour
    {
        [SerializeField] List<Vector3> points = new List<Vector3>();

        [Tooltip("Segment that follows when this one does not end at a junction. Empty = end of the line.")]
        public TrackSegment next;

        [Tooltip("Junction at the end of this segment, where the player picks left or right.")]
        public TrackJunction endJunction;

        float[] cumulative;

        public IReadOnlyList<Vector3> Points => points;

        public float Length
        {
            get { EnsureCache(); return cumulative.Length == 0 ? 0f : cumulative[cumulative.Length - 1]; }
        }

        public void SetPoints(IList<Vector3> localPoints)
        {
            points.Clear();
            points.AddRange(localPoints);
            cumulative = null;
        }

        /// <summary>World position and forward direction at <paramref name="distance"/> metres along the segment (clamped).</summary>
        public void Evaluate(float distance, out Vector3 position, out Vector3 forward)
        {
            EnsureCache();
            if (points.Count < 2)
            {
                position = transform.position;
                forward = transform.forward;
                return;
            }

            distance = Mathf.Clamp(distance, 0f, Length);
            int i = 1;
            while (i < points.Count - 1 && cumulative[i] < distance) i++;

            float span = cumulative[i] - cumulative[i - 1];
            float t = span > 0f ? (distance - cumulative[i - 1]) / span : 0f;
            position = transform.TransformPoint(Vector3.Lerp(points[i - 1], points[i], t));
            forward = transform.TransformDirection(points[i] - points[i - 1]).normalized;
        }

        void EnsureCache()
        {
            if (cumulative != null && cumulative.Length == points.Count) return;
            cumulative = new float[points.Count];
            for (int i = 1; i < points.Count; i++)
                cumulative[i] = cumulative[i - 1] + Vector3.Distance(points[i - 1], points[i]);
        }

        void OnValidate() => cumulative = null;

        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            for (int i = 1; i < points.Count; i++)
                Gizmos.DrawLine(transform.TransformPoint(points[i - 1]), transform.TransformPoint(points[i]));
        }
    }
}
