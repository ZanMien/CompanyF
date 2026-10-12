using System;
using System.Collections.Generic;
using CompanyF.Track;
using UnityEngine;

namespace CompanyF.Train
{
    /// <summary>
    /// Moves the train along the <see cref="TrackRoute"/> at a constant speed, without physics.
    /// The heading follows the chord between the front and rear axle, so the body turns
    /// smoothly through turnouts instead of snapping at path points.
    /// Put this on the train root; the locomotive model is a child offset so its wheels sit on the rails.
    /// </summary>
    public class TrainFollower : MonoBehaviour
    {
        [SerializeField] TrackRoute route;

        [Tooltip("Travel speed in m/s. Can be changed while playing.")]
        [Min(0f)] public float speed = 15f;

        /// <summary>
        /// Raised once per junction when the nose comes within its trigger distance.
        /// A listener must answer with <see cref="SetJunctionChoice"/>; the train waits at the junction until it does.
        /// With no listener the train goes left.
        /// </summary>
        public event Action<TrackJunction> JunctionApproaching;

        /// <summary>Raised once per track model when the nose passes its far end: (model, segment it was travelled on).</summary>
        public event Action<Transform, TrackSegment> PieceExited;

        /// <summary>Raised once when the nose reaches the end of the line (after the last <see cref="PieceExited"/>).</summary>
        public event Action ReachedEnd;

        [Header("Locomotive geometry (m from the model pivot, along the track)")]
        [SerializeField] float frontAxle = 4.34f;
        [SerializeField] float rearAxle = 2.14f;
        [SerializeField] float frontBumper = 6.01f;
        [SerializeField] float rearBumper = 5.41f;

        // Segments actually travelled, with the route distance at which each one starts.
        readonly List<TrackSegment> segments = new List<TrackSegment>();
        readonly List<float> segmentStart = new List<float>();
        readonly HashSet<TrackJunction> announced = new HashSet<TrackJunction>();
        readonly HashSet<Transform> exitedPieces = new HashSet<Transform>();
        readonly Dictionary<TrackJunction, bool> choices = new Dictionary<TrackJunction, bool>();
        float endOfLine = -1f;
        float distance;

        public float Distance => distance;
        public bool Arrived { get; private set; }
        public TrackSegment CurrentSegment => segments.Count > 0 ? segments[SegmentIndexAt(distance)] : null;

        /// <summary>True while the nose is held at a junction whose direction is not decided yet.</summary>
        public bool WaitingAtJunction { get; private set; }

        /// <summary>Sets which branch the train takes at <paramref name="junction"/>. The first call wins.</summary>
        public void SetJunctionChoice(TrackJunction junction, bool goLeft)
        {
            if (junction != null && !choices.ContainsKey(junction)) choices[junction] = goLeft;
        }

        void Start()
        {
            if (route == null) route = FindFirstObjectByType<TrackRoute>();
            if (route == null || route.start == null)
            {
                Debug.LogError("[Train] No TrackRoute with a start segment in the scene.", this);
                enabled = false;
                return;
            }

            segments.Add(route.start);
            segmentStart.Add(0f);
            distance = rearBumper;  // whole locomotive on the first rail
            Advance(0f);
        }

        void Update()
        {
            if (Arrived) return;
            Advance(speed * Time.deltaTime);
        }

        void Advance(float step)
        {
            distance += step;
            float nose = distance + frontBumper;
            AnnounceJunctions(nose);
            Extend(nose);

            // Undecided junction ahead: hold the nose at the fork.
            int last = segments.Count - 1;
            float covered = segmentStart[last] + segments[last].Length;
            WaitingAtJunction = endOfLine < 0f && nose > covered;
            if (WaitingAtJunction)
            {
                distance = covered - frontBumper;
                nose = covered;
            }

            bool arriving = endOfLine >= 0f && nose >= endOfLine;
            if (arriving) nose = endOfLine;
            ExitPieces(nose);

            if (arriving)
            {
                distance = endOfLine - frontBumper;
                Arrived = true;
                Apply();
                Debug.Log("到達終點", this);
                ReachedEnd?.Invoke();
                return;
            }

            Apply();
        }

        /// <summary>Appends segments until the travelled path covers <paramref name="reach"/>. Junction choices are made here.</summary>
        void Extend(float reach)
        {
            while (endOfLine < 0f)
            {
                int last = segments.Count - 1;
                var seg = segments[last];
                float segEnd = segmentStart[last] + seg.Length;
                if (reach <= segEnd) return;

                var next = seg.next;
                if (seg.endJunction != null)
                {
                    if (!choices.TryGetValue(seg.endJunction, out bool goLeft)) return;  // not decided yet
                    next = seg.endJunction.Choose(goLeft);
                    Debug.Log($"[Train] {seg.endJunction.name}: {(goLeft ? "左" : "右")} -> {(next ? next.name : "none")}", this);
                }

                if (next == null)
                {
                    endOfLine = segEnd;
                    return;
                }

                segments.Add(next);
                segmentStart.Add(segEnd);
            }
        }

        void ExitPieces(float nose)
        {
            const float Epsilon = 0.001f;  // the line ends exactly on the last piece's end
            for (int i = 0; i < segments.Count; i++)
            {
                if (segmentStart[i] > nose) break;
                foreach (var piece in segments[i].Pieces)
                {
                    if (piece.model == null || exitedPieces.Contains(piece.model)) continue;
                    if (segmentStart[i] + piece.end > nose + Epsilon) break;
                    exitedPieces.Add(piece.model);
                    PieceExited?.Invoke(piece.model, segments[i]);
                }
            }
        }

        void AnnounceJunctions(float nose)
        {
            for (int i = 0; i < segments.Count; i++)
            {
                var junction = segments[i].endJunction;
                if (junction == null || announced.Contains(junction)) continue;
                float junctionAt = segmentStart[i] + segments[i].Length;
                if (nose < junctionAt - junction.triggerDistance) continue;

                announced.Add(junction);
                if (JunctionApproaching == null) SetJunctionChoice(junction, true);
                else JunctionApproaching.Invoke(junction);
            }
        }

        int SegmentIndexAt(float d)
        {
            int i = segments.Count - 1;
            while (i > 0 && d < segmentStart[i]) i--;
            return i;
        }

        Vector3 PointAt(float d)
        {
            int i = SegmentIndexAt(d);
            float local = d - segmentStart[i];
            float length = segments[i].Length;
            segments[i].Evaluate(local, out var p, out var fwd);
            // Past either end of the line, extend along the end tangent.
            if (local < 0f) p += fwd * local;
            else if (local > length) p += fwd * (local - length);
            return p;
        }

        void Apply()
        {
            var front = PointAt(distance + frontAxle);
            var rear = PointAt(distance - rearAxle);
            var forward = (front - rear).normalized;
            transform.SetPositionAndRotation(rear + forward * rearAxle, Quaternion.LookRotation(forward, Vector3.up));
        }
    }
}
