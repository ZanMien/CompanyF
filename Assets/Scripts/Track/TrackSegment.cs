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
        /// <summary>One track model on this segment's path and the distance along the path where it ends.</summary>
        [System.Serializable]
        public struct Piece
        {
            public Transform model;
            public float end;
        }

        [SerializeField] List<Vector3> points = new List<Vector3>();

        [Tooltip("Track models this path runs over, in travel order. A turnout appears in both branches it serves.")]
        [SerializeField] List<Piece> pieces = new List<Piece>();

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

        public IReadOnlyList<Piece> Pieces => pieces;

        public void SetPoints(IList<Vector3> localPoints)
        {
            points.Clear();
            points.AddRange(localPoints);
            cumulative = null;
        }

        public void SetPieces(IList<Piece> list)
        {
            pieces.Clear();
            pieces.AddRange(list);
        }

        /// <summary>Distance along the path to the point closest to <paramref name="worldPoint"/>.</summary>
        public float DistanceAlong(Vector3 worldPoint)
        {
            EnsureCache();
            var p = transform.InverseTransformPoint(worldPoint);
            float best = float.MaxValue, at = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                var a = points[i - 1];
                var ab = points[i] - a;
                float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                float d = (a + ab * t - p).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    at = cumulative[i - 1] + ab.magnitude * t;
                }
            }
            return at;
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
