using UnityEngine;

namespace CompanyF.UI
{
    /// <summary>
    /// Places the result screen's background camera: high above the ground, looking down at <see cref="lookAt"/>
    /// at <see cref="pitch"/> degrees. Changing the values in the Inspector moves the camera straight away.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ResultCamera : MonoBehaviour
    {
        [Tooltip("World point on the ground the camera looks at.")]
        public Vector3 lookAt = Vector3.zero;
        [Tooltip("How far above the look-at point the camera is (m).")]
        [Min(1f)] public float height = 60f;
        [Tooltip("Downward angle (degrees).")]
        [Range(1f, 89f)] public float pitch = 45f;
        [Tooltip("Compass direction the camera faces (degrees, 0 = +Z).")]
        public float yaw = 0f;

        void Awake() => Apply();
        void OnValidate() => Apply();

        public void Apply()
        {
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            float distance = height / Mathf.Sin(pitch * Mathf.Deg2Rad);
            transform.SetPositionAndRotation(lookAt - rotation * Vector3.forward * distance, rotation);
        }
    }
}
