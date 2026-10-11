using UnityEngine;

namespace CompanyF.Train
{
    /// <summary>
    /// Keeps the camera behind and above the target. Position is locked to the target every frame
    /// (no lag at any speed); only the rotation used for the offset is smoothed, so curves swing gently.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;

        [Tooltip("Camera position in the target's local space (x right, y up, z forward).")]
        public Vector3 offset = new Vector3(0f, 6f, -16f);

        [Tooltip("Point the camera looks at, in the target's local space.")]
        public Vector3 lookAt = new Vector3(0f, 2.5f, 6f);

        [Tooltip("How quickly the camera swings round to follow the train's heading. 0 = instantly.")]
        [Min(0f)] public float rotationSmoothing = 4f;

        Quaternion heading;

        void Start()
        {
            if (target == null)
            {
                var train = FindFirstObjectByType<TrainFollower>();
                if (train != null) target = train.transform;
            }
            if (target != null) heading = target.rotation;
        }

        void LateUpdate()
        {
            if (target == null) return;

            heading = rotationSmoothing > 0f
                ? Quaternion.Slerp(heading, target.rotation, 1f - Mathf.Exp(-rotationSmoothing * Time.deltaTime))
                : target.rotation;

            transform.position = target.position + heading * offset;
            transform.LookAt(target.position + heading * lookAt);
        }
    }
}
