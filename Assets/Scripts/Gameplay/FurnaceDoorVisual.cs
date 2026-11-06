using UnityEngine;

namespace CompanyF.Gameplay
{
    /// <summary>
    /// Swings the two furnace door leaves with <see cref="FurnaceController"/>'s door state.
    /// Each leaf turns around its own origin, which is the pin at its lower outer corner, so the two
    /// open away from each other. The swing lasts as long as the controller's Opening and Closing states.
    /// </summary>
    public class FurnaceDoorVisual : MonoBehaviour
    {
        [SerializeField] FurnaceController furnace;
        public Transform leftDoor;
        public Transform rightDoor;

        [Tooltip("How far each leaf turns when fully open (degrees).")]
        [Range(0f, 120f)] public float openAngle = 75f;
        [Tooltip("Local axis the leaves turn around. Z: sideways in the door's plane. Y: outward like a house door.")]
        public Vector3 hingeAxis = Vector3.forward;

        Quaternion leftClosed = Quaternion.identity;
        Quaternion rightClosed = Quaternion.identity;
        bool captured;

        void Start()
        {
            if (furnace == null) furnace = FindFirstObjectByType<FurnaceController>();
        }

        void LateUpdate() => Apply();

        /// <summary>Poses both leaves for the controller's current state.</summary>
        public void Apply()
        {
            if (furnace == null) return;
            if (!captured)
            {
                if (leftDoor != null) leftClosed = leftDoor.localRotation;
                if (rightDoor != null) rightClosed = rightDoor.localRotation;
                captured = true;
            }

            float angle = openAngle * Mathf.SmoothStep(0f, 1f, OpenAmount());
            if (leftDoor != null) leftDoor.localRotation = leftClosed * Quaternion.AngleAxis(angle, hingeAxis);
            if (rightDoor != null) rightDoor.localRotation = rightClosed * Quaternion.AngleAxis(-angle, hingeAxis);
        }

        /// <summary>0 closed, 1 fully open.</summary>
        float OpenAmount()
        {
            switch (furnace.State)
            {
                case DoorState.Opening:
                    return furnace.openTime > 0f ? Mathf.Clamp01(furnace.StateTime / furnace.openTime) : 1f;
                case DoorState.Closing:
                    return furnace.closeTime > 0f ? 1f - Mathf.Clamp01(furnace.StateTime / furnace.closeTime) : 0f;
                case DoorState.Closed:
                    return 0f;
                default:
                    return 1f;
            }
        }
    }
}
