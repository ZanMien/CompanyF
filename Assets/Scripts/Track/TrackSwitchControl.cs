using System;
using CompanyF.Train;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CompanyF.Track
{
    /// <summary>
    /// P1's left lever, pushed left or right, sets the junction ahead to that side.
    /// Works once the nose is within <see cref="switchRange"/> of the junction and until it reaches it.
    /// </summary>
    public class TrackSwitchControl : MonoBehaviour
    {
        [SerializeField] TrainFollower train;

        [Tooltip("Metres before a junction from which it can be switched.")]
        [Min(0f)] public float switchRange = 120f;

        [Header("Input")]
        public Key leftKey = Key.A;
        public Key rightKey = Key.D;

        /// <summary>The lever was pushed while a junction was in range.</summary>
        public event Action Switched;

        void Start()
        {
            if (train == null) train = FindFirstObjectByType<TrainFollower>();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || train == null) return;

            var junction = train.UpcomingJunction(switchRange);
            if (junction == null) return;

            bool left = keyboard[leftKey].wasPressedThisFrame;
            if (!left && !keyboard[rightKey].wasPressedThisFrame) return;

            junction.branchSelected = left == junction.branchOnLeft;
            Switched?.Invoke();
        }
    }
}
