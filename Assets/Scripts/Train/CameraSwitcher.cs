using UnityEngine;
using UnityEngine.InputSystem;

namespace CompanyF.Train
{
    /// <summary>
    /// Shows one of two cameras at a time (starts on camera 1). The right-hand player switches:
    /// → on camera 1 goes to camera 2, ← on camera 2 goes back to camera 1.
    /// Only the Camera components are toggled, so an AudioListener on camera 1 keeps working.
    /// </summary>
    public class CameraSwitcher : MonoBehaviour
    {
        [SerializeField] Camera camera1;
        [SerializeField] Camera camera2;

        public Key toCamera2Key = Key.RightArrow;
        public Key toCamera1Key = Key.LeftArrow;

        /// <summary>1 or 2.</summary>
        public int Active { get; private set; } = 1;

        void Start() => Show(1);

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (Active == 1 && keyboard[toCamera2Key].wasPressedThisFrame) Show(2);
            else if (Active == 2 && keyboard[toCamera1Key].wasPressedThisFrame) Show(1);
        }

        public void Show(int which)
        {
            Active = which == 2 ? 2 : 1;
            if (camera1 != null) camera1.enabled = Active == 1;
            if (camera2 != null) camera2.enabled = Active == 2;
        }
    }
}
