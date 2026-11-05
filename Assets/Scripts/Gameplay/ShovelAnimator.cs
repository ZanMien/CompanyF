using UnityEngine;

namespace CompanyF.Gameplay
{
    /// <summary>
    /// Poses the shovel and its coal from <see cref="FurnaceController.Action"/> and
    /// <see cref="FurnaceController.ActionTime"/>. Every frame the pose is computed from the time alone, in the
    /// Train's local space, so it stays on the moving train and snaps back to the rest pose when the action ends.
    /// </summary>
    public class ShovelAnimator : MonoBehaviour
    {
        [SerializeField] FurnaceController furnace;
        [SerializeField] Transform train;
        [SerializeField] Transform shovel;
        [SerializeField] Transform coal;
        [Tooltip("The handle end the shovel turns around when it tips the coal out.")]
        [SerializeField] Transform tipPivot;
        [Tooltip("Where the coal lands in the furnace.")]
        [SerializeField] Transform landingPoint;
        [Tooltip("Top of the coal's arc.")]
        [SerializeField] Transform apexPoint;
        [Tooltip("Where the coal lands when the door blocks it.")]
        [SerializeField] Transform blockedPoint;

        [Header("Step 1: move into place (Train-local)")]
        [Min(0f)] public float moveEndTime = 0.25f;
        public float targetLocalX = 0f;
        public float targetLocalY = 1.4877f;

        [Header("Step 2: turn (throw)")]
        [Min(0f)] public float turnEndTime = 0.35f;
        public float turnLocalY = 180f;

        [Header("Step 3: flick (throw); the coal leaves at turnEndTime")]
        public float flickLocalX = 65f;

        [Header("Tipping out (door not open)")]
        [Tooltip("Degrees around the Train's Y axis, through the tip pivot.")]
        public float tipAngle = -90f;
        [Tooltip("Height (Train-local) of the coal's centre once it is on the floor.")]
        public float floorLocalY = 1.375f;

        Vector3 shovelRestPosition;
        Vector3 shovelRestEuler;
        Vector3 coalRestPosition;
        Quaternion coalRestRotation;
        bool wasPosed;

        void Awake() => CaptureRestPose();

        /// <summary>Remembers the current shovel and coal pose as the one to return to.</summary>
        public void CaptureRestPose()
        {
            if (shovel == null || coal == null) return;
            shovelRestPosition = shovel.localPosition;
            shovelRestEuler = shovel.localEulerAngles;
            coalRestPosition = coal.localPosition;
            coalRestRotation = coal.localRotation;
        }

        void Start()
        {
            if (furnace == null) furnace = FindFirstObjectByType<FurnaceController>();
        }

        void LateUpdate()
        {
            if (furnace == null || shovel == null || coal == null || train == null) return;

            if (furnace.Action == ShovelAction.None)
            {
                if (wasPosed) ResetPose();
                wasPosed = false;
                return;
            }

            wasPosed = true;
            PoseAt(furnace.Action, furnace.ActionTime);
        }

        /// <summary>Poses the shovel and coal as they are <paramref name="t"/> seconds into <paramref name="action"/>.</summary>
        public void PoseAt(ShovelAction action, float t)
        {
            switch (action)
            {
                case ShovelAction.Throw: PoseThrow(t); break;
                case ShovelAction.FailBlocked: PoseBlocked(t); break;
                case ShovelAction.FailDoorNotOpen: PoseTipOut(t); break;
            }
        }

        public void ResetPose()
        {
            shovel.localPosition = shovelRestPosition;
            shovel.localEulerAngles = shovelRestEuler;
            coal.localPosition = coalRestPosition;
            coal.localRotation = coalRestRotation;
        }

        void PoseThrow(float t)
        {
            PoseShovelForThrow(t);
            if (t < turnEndTime) return;

            float apex = furnace.apexTime;
            if (t < apex) PlaceCoal(ArcUp(CoalLaunchPoint(), LocalOf(apexPoint), Ratio(t, turnEndTime, apex)));
            else PlaceCoal(ArcDown(LocalOf(apexPoint), LocalOf(landingPoint), Ratio(t, apex, furnace.loadTime)));
        }

        void PoseBlocked(float t)
        {
            // The shovel follows the throw up to the top of the arc, then holds still.
            float apex = furnace.apexTime;
            PoseShovelForThrow(Mathf.Min(t, apex));
            if (t < turnEndTime) return;

            if (t < apex) PlaceCoal(ArcUp(CoalLaunchPoint(), LocalOf(apexPoint), Ratio(t, turnEndTime, apex)));
            else PlaceCoal(ArcDown(LocalOf(apexPoint), LocalOf(blockedPoint), Ratio(t, apex, furnace.loadTime)));
        }

        void PoseShovelForThrow(float t)
        {
            coal.localPosition = coalRestPosition;
            coal.localRotation = coalRestRotation;
            shovel.localPosition = MovedPosition(t);
            float turn = Smooth(Ratio(t, moveEndTime, turnEndTime));
            float flick = Smooth(Ratio(t, turnEndTime, furnace.loadTime));
            shovel.localEulerAngles = new Vector3(
                Mathf.LerpAngle(shovelRestEuler.x, flickLocalX, flick),
                Mathf.LerpAngle(shovelRestEuler.y, turnLocalY, turn),
                shovelRestEuler.z);
        }

        void PoseTipOut(float t)
        {
            coal.localPosition = coalRestPosition;
            coal.localRotation = coalRestRotation;

            // Start of the tip: moved into place, not yet turned. Find the pivot in that pose.
            var startPosition = MovedPosition(moveEndTime);
            var startRotation = Quaternion.Euler(shovelRestEuler);
            shovel.localPosition = startPosition;
            shovel.localRotation = startRotation;
            var pivot = train.InverseTransformPoint(tipPivot.position);
            float coalStartY = train.InverseTransformPoint(coal.position).y;

            if (t < moveEndTime)
            {
                shovel.localPosition = MovedPosition(t);
                return;
            }

            float progress = Ratio(t, moveEndTime, furnace.failAnimationTime);
            var turn = Quaternion.AngleAxis(tipAngle * Smooth(progress), Vector3.up);
            shovel.localPosition = pivot + turn * (startPosition - pivot);
            shovel.localRotation = turn * startRotation;

            // The coal turns with the shovel but falls straight down, accelerating, to the floor.
            var coalLocal = train.InverseTransformPoint(coal.position);
            coalLocal.y = Mathf.Lerp(coalStartY, floorLocalY, progress * progress);
            coal.position = train.TransformPoint(coalLocal);
        }

        Vector3 MovedPosition(float t)
        {
            float move = Smooth(Ratio(t, 0f, moveEndTime));
            return new Vector3(
                Mathf.Lerp(shovelRestPosition.x, targetLocalX, move),
                Mathf.Lerp(shovelRestPosition.y, targetLocalY, move),
                shovelRestPosition.z);
        }

        /// <summary>Where the coal sits on the shovel at the moment it leaves, in Train-local space.</summary>
        Vector3 CoalLaunchPoint()
        {
            var position = shovel.localPosition;
            var rotation = shovel.localRotation;
            var coalPosition = coal.localPosition;
            var coalRotation = coal.localRotation;

            PoseShovelForThrow(turnEndTime);
            var launch = train.InverseTransformPoint(coal.position);

            shovel.localPosition = position;
            shovel.localRotation = rotation;
            coal.localPosition = coalPosition;
            coal.localRotation = coalRotation;
            return launch;
        }

        void PlaceCoal(Vector3 trainLocal) => coal.position = train.TransformPoint(trainLocal);

        Vector3 LocalOf(Transform point) => train.InverseTransformPoint(point.position);

        // Together these make one arc: steady sideways, slowing as it rises and speeding up as it falls.
        static Vector3 ArcUp(Vector3 from, Vector3 to, float p)
        {
            var position = Vector3.Lerp(from, to, p);
            position.y = Mathf.Lerp(from.y, to.y, 1f - (1f - p) * (1f - p));
            return position;
        }

        static Vector3 ArcDown(Vector3 from, Vector3 to, float p)
        {
            var position = Vector3.Lerp(from, to, p);
            position.y = Mathf.Lerp(from.y, to.y, p * p);
            return position;
        }

        static float Ratio(float t, float from, float to) => to > from ? Mathf.Clamp01((t - from) / (to - from)) : 1f;

        static float Smooth(float p) => Mathf.SmoothStep(0f, 1f, p);
    }
}
