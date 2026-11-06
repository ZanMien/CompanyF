using UnityEngine;

namespace CompanyF.Gameplay
{
    /// <summary>
    /// Makes the furnace flame flare up when a coal that will land reaches the top of its arc:
    /// grow to <see cref="maxSize"/>, hold, shrink back to <see cref="normalSize"/>.
    /// Another coal during the flare restarts from the current size; already at full size, only the hold restarts.
    /// The flame's particle systems need Scaling Mode = Hierarchy for the Transform scale to change them.
    /// </summary>
    public class FurnaceFlameAnimator : MonoBehaviour
    {
        public enum FlarePhase { Idle, Grow, Hold, Shrink }

        const float Epsilon = 1e-4f;

        [SerializeField] FurnaceController furnace;
        [SerializeField] Transform flame;

        [Header("Size (uniform Scale)")]
        [Min(0f)] public float normalSize = 0.15f;
        [Min(0f)] public float maxSize = 0.25f;

        [Header("Times (s)")]
        [Min(0f)] public float growTime = 0.2f;
        [Min(0f)] public float holdTime = 0.5f;
        [Min(0f)] public float shrinkTime = 0.5f;

        public FlarePhase Phase { get; private set; }
        public float Size { get; private set; }

        float phaseTime;
        float growFrom;

        void Awake() => ResetSize();

        void Start()
        {
            if (furnace == null) furnace = FindFirstObjectByType<FurnaceController>();
            if (furnace != null) furnace.CoalReachedApex += Flare;
        }

        void OnDestroy()
        {
            if (furnace != null) furnace.CoalReachedApex -= Flare;
        }

        void Update() => Tick(Time.deltaTime);

        public void ResetSize()
        {
            Phase = FlarePhase.Idle;
            phaseTime = 0f;
            Size = normalSize;
            Apply();
        }

        /// <summary>Starts or restarts the flare from the current size.</summary>
        public void Flare()
        {
            phaseTime = 0f;
            if (Size >= maxSize - Epsilon)
            {
                Size = maxSize;
                Phase = FlarePhase.Hold;
            }
            else
            {
                growFrom = Size;
                Phase = FlarePhase.Grow;
            }
        }

        public void Tick(float dt)
        {
            if (Phase == FlarePhase.Idle) return;
            phaseTime += dt;

            if (Phase == FlarePhase.Grow)
            {
                if (phaseTime < growTime - Epsilon)
                {
                    Size = Mathf.Lerp(growFrom, maxSize, Mathf.SmoothStep(0f, 1f, phaseTime / growTime));
                    Apply();
                    return;
                }
                phaseTime -= growTime;
                Size = maxSize;
                Phase = FlarePhase.Hold;
            }

            if (Phase == FlarePhase.Hold)
            {
                if (phaseTime < holdTime - Epsilon)
                {
                    Apply();
                    return;
                }
                phaseTime -= holdTime;
                Phase = FlarePhase.Shrink;
            }

            if (phaseTime < shrinkTime - Epsilon)
            {
                Size = Mathf.Lerp(maxSize, normalSize, Mathf.SmoothStep(0f, 1f, phaseTime / shrinkTime));
            }
            else
            {
                Size = normalSize;
                Phase = FlarePhase.Idle;
            }
            Apply();
        }

        void Apply()
        {
            if (flame != null) flame.localScale = Vector3.one * Size;
        }
    }
}
