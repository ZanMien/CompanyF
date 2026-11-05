using UnityEngine;

namespace CompanyF.Gameplay
{
    /// <summary>
    /// Swings the two furnace doors from the <see cref="FurnaceController"/> door state,
    /// so the animation always lasts exactly as long as Opening / Closing.
    /// </summary>
    public class FurnaceDoorAnimator : MonoBehaviour
    {
        [SerializeField] FurnaceController furnace;
        [SerializeField] Transform door1;
        [SerializeField] Transform door2;

        [Header("Open angle (local Y, degrees)")]
        public float door1OpenY = 145f;
        public float door2OpenY = -145f;

        Vector3 door1Closed;
        Vector3 door2Closed;

        void Awake()
        {
            if (door1 != null) door1Closed = door1.localEulerAngles;
            if (door2 != null) door2Closed = door2.localEulerAngles;
        }

        void Start()
        {
            if (furnace == null) furnace = FindFirstObjectByType<FurnaceController>();
        }

        void LateUpdate()
        {
            if (furnace == null) return;
            float open = OpenAmount();
            Pose(door1, door1Closed, door1OpenY, open);
            Pose(door2, door2Closed, door2OpenY, open);
        }

        float OpenAmount()
        {
            switch (furnace.State)
            {
                case DoorState.Opening:
                    return Mathf.SmoothStep(0f, 1f, furnace.openTime > 0f ? furnace.StateTime / furnace.openTime : 1f);
                case DoorState.Closing:
                    return Mathf.SmoothStep(1f, 0f, furnace.closeTime > 0f ? furnace.StateTime / furnace.closeTime : 1f);
                case DoorState.Open:
                case DoorState.Loading:
                    return 1f;
                default:
                    return 0f;
            }
        }

        static void Pose(Transform door, Vector3 closed, float openY, float open)
        {
            if (door == null) return;
            door.localEulerAngles = new Vector3(closed.x, Mathf.LerpAngle(closed.y, openY, open), closed.z);
        }
    }
}
