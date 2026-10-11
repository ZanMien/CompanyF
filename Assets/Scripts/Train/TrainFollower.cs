using System.Collections.Generic;
using CompanyF.Track;
using UnityEngine;
using UnityEngine.InputSystem;

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

        [Tooltip("Hold this key when the train nose reaches a junction to take the right branch; otherwise it goes left.")]
        public Key rightKey = Key.R;

        [Header("Locomotive geometry (m from the model pivot, along the track)")]
        [SerializeField] float frontAxle = 4.34f;
        [SerializeField] float rearAxle = 2.14f;
        [SerializeField] float frontBumper = 6.01f;
        [SerializeField] float rearBumper = 5.41f;

        // Segments actually travelled, with the route distance at which each one starts.
        readonly List<TrackSegment> segments = new List<TrackSegment>();
        readonly List<float> segmentStart = new List<float>();
        float endOfLine = -1f;
        float distance;

        public float Distance => distance;
        public bool Arrived { get; private set; }
        public TrackSegment CurrentSegment => segments.Count > 0 ? segments[SegmentIndexAt(distance)] : null;

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
            Extend(distance + frontBumper);
            Apply();
        }

        void Update()
        {
            if (Arrived) return;

            distance += speed * Time.deltaTime;
            float nose = distance + frontBumper;
            Extend(nose);

            if (endOfLine >= 0f && nose >= endOfLine)
            {
                distance = endOfLine - frontBumper;
                Arrived = true;
                Apply();
                Debug.Log("到達終點", this);
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
                    bool goRight = Keyboard.current != null && Keyboard.current[rightKey].isPressed;
                    next = seg.endJunction.Choose(!goRight);
                    Debug.Log($"[Train] {seg.endJunction.name}: {(goRight ? "右" : "左")} -> {(next ? next.name : "none")}", this);
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
