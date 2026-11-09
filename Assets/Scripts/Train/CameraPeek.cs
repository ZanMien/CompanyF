using UnityEngine;
using UnityEngine.InputSystem;

namespace CompanyF.Train
{
    /// <summary>
    /// One camera that moves smoothly between two points on the train, like leaning out to look ahead.
    /// → moves toward point 2, ← toward point 1; pressing the other way mid-move turns back from where the camera is,
    /// taking the time in proportion to the way already covered.
    /// The camera is a child of the Train and only its local pose is set, so it stays locked to the train.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class CameraPeek : MonoBehaviour
    {
        [SerializeField] Camera peekCamera;
        [SerializeField] Transform point1;
        [SerializeField] Transform point2;
        [Tooltip("Optional: the path curves through this point on the way, to keep the camera clear of the cab. Only its position is used.")]
        [SerializeField] Transform viaPoint;
        [Tooltip("Optional collision-checked path in train-local coordinates. Takes priority over the via point.")]
        [SerializeField] Vector3[] avoidancePath;

        [Header("Field of view at each point")]
        public float point1FieldOfView = 55f;
        public float point2FieldOfView = 85f;

        [Header("Movement")]
        [Tooltip("Seconds for a full move from one point to the other.")]
        [Min(0.01f)] public float moveTime = 1f;
        [Tooltip("Progress along the move: 0 = point 1, 1 = point 2. Default eases in and out.")]
        public AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Input")]
        public Key toPoint2Key = Key.RightArrow;
        public Key toPoint1Key = Key.LeftArrow;

        /// <summary>0 at point 1, 1 at point 2, linear in time; <see cref="moveCurve"/> shapes it into the pose.</summary>
        public float Progress { get; private set; }
        /// <summary>The point the camera is at or moving to: 1 or 2.</summary>
        public int TargetPoint => target >= 1f ? 2 : 1;
        /// <summary>Operations are allowed only after the camera reaches the corresponding view.</summary>
        public bool IsFurnaceView => TargetPoint == 1 && Progress <= 1e-4f;
        public bool IsOutsideView => TargetPoint == 2 && Progress >= 1f - 1e-4f;

        float target;

        void Start() => ApplyPose();

        void Update()
        {
            var keyboard = Keyboard.current;
            bool toPoint2 = keyboard != null && keyboard[toPoint2Key].wasPressedThisFrame;
            bool toPoint1 = keyboard != null && keyboard[toPoint1Key].wasPressedThisFrame;
            Tick(Time.deltaTime, toPoint1, toPoint2);
        }

        /// <summary>Advances by <paramref name="dt"/> seconds with this frame's key presses.</summary>
        public void Tick(float dt, bool toPoint1, bool toPoint2)
        {
            if (toPoint2) target = 1f;
            else if (toPoint1) target = 0f;

            Progress = Mathf.MoveTowards(Progress, target, dt / moveTime);
            ApplyPose();
        }

        /// <summary>Sets the camera to where it belongs at the current <see cref="Progress"/>.</summary>
        public void ApplyPose()
        {
            if (peekCamera == null || point1 == null || point2 == null) return;

            float eased = moveCurve.Evaluate(Progress);
            var pose = peekCamera.transform;
            pose.localPosition = PositionAt(eased);
            pose.localRotation = Quaternion.SlerpUnclamped(point1.localRotation, point2.localRotation, eased);
            peekCamera.fieldOfView = Mathf.LerpUnclamped(point1FieldOfView, point2FieldOfView, eased);
        }

        /// <summary>
        /// The view frustum at point 2, wherever the camera is right now. <paramref name="planes"/> must hold 6.
        /// </summary>
        public void GetPoint2ViewPlanes(Plane[] planes)
        {
            var projection = Matrix4x4.Perspective(point2FieldOfView, peekCamera.aspect, peekCamera.nearClipPlane, peekCamera.farClipPlane);
            var view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * point2.worldToLocalMatrix;
            GeometryUtility.CalculateFrustumPlanes(projection * view, planes);
        }

        /// <summary>
        /// Local position along the path (0 = point 1, 1 = point 2). Uses the collision-checked polyline when assigned;
        /// otherwise uses the via-point curve or a straight line.
        /// </summary>
        public Vector3 PositionAt(float amount)
        {
            if (avoidancePath != null && avoidancePath.Length >= 2)
            {
                float length = 0f;
                for (int i = 1; i < avoidancePath.Length; i++) length += Vector3.Distance(avoidancePath[i - 1], avoidancePath[i]);
                float remaining = Mathf.Clamp01(amount) * length;
                for (int i = 1; i < avoidancePath.Length; i++)
                {
                    float segment = Vector3.Distance(avoidancePath[i - 1], avoidancePath[i]);
                    if (remaining <= segment || i == avoidancePath.Length - 1)
                        return Vector3.Lerp(avoidancePath[i - 1], avoidancePath[i], segment > 0f ? remaining / segment : 0f);
                    remaining -= segment;
                }
            }
            var from = point1.localPosition;
            var to = point2.localPosition;
            if (viaPoint == null) return Vector3.LerpUnclamped(from, to, amount);

            var control = 2f * viaPoint.localPosition - 0.5f * (from + to);
            float rest = 1f - amount;
            return rest * rest * from + 2f * rest * amount * control + amount * amount * to;
        }
    }
}
