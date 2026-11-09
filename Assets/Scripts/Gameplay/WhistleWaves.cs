using System.Collections.Generic;
using UnityEngine;

namespace CompanyF.Gameplay
{
    /// <summary>
    /// While the whistle sounds, sends out ground rings from the train at a fixed interval. Each ring grows from
    /// radius 0 to <see cref="maxRadius"/> and disappears; the rings are children of the train, so they move with it.
    /// <see cref="ActiveRadii"/> tells <see cref="AnimalHerd"/> how far the sound currently reaches.
    /// </summary>
    public class WhistleWaves : MonoBehaviour
    {
        class Wave
        {
            public LineRenderer ring;
            public float age;
        }

        [SerializeField] TrainControls controls;

        [Header("Waves")]
        [Tooltip("Seconds between waves while the whistle sounds.")]
        [Min(0.01f)] public float emitInterval = 0.5f;
        [Tooltip("Seconds for a wave to grow to its full radius.")]
        [Min(0.01f)] public float expandTime = 1f;
        [Min(0f)] public float maxRadius = 30f;

        [Header("Look")]
        [Tooltip("Transparent unlit material; its HDR colour gives the glow.")]
        [SerializeField] Material ringMaterial;
        [Min(0f)] public float ringWidth = 0.5f;
        [Tooltip("World height of the ring, just above the ground.")]
        public float groundHeight = 0.05f;
        [Min(8)] public int ringSegments = 64;

        readonly List<Wave> waves = new List<Wave>();
        readonly Stack<LineRenderer> idleRings = new Stack<LineRenderer>();
        readonly List<float> activeRadii = new List<float>();
        float emitTimer;
        bool wasWhistling;

        /// <summary>Current radius of every wave in the air.</summary>
        public IReadOnlyList<float> ActiveRadii => activeRadii;

        void Start()
        {
            if (controls == null) controls = GetComponent<TrainControls>();
        }

        void Update() => Tick(Time.deltaTime, controls != null && controls.IsWhistling);

        /// <summary>Advances by <paramref name="dt"/> seconds; the first wave leaves the moment the whistle starts.</summary>
        public void Tick(float dt, bool whistling)
        {
            activeRadii.Clear();
            for (int i = waves.Count - 1; i >= 0; i--)
            {
                waves[i].age += dt;
                if (waves[i].age >= expandTime)
                {
                    Retire(waves[i]);
                    waves.RemoveAt(i);
                }
            }

            if (whistling)
            {
                if (!wasWhistling) { Emit(); emitTimer = 0f; }
                else
                {
                    emitTimer += dt;
                    while (emitTimer >= emitInterval - 1e-4f)
                    {
                        emitTimer -= emitInterval;
                        Emit();
                    }
                }
            }
            wasWhistling = whistling;

            foreach (var wave in waves)
            {
                float progress = wave.age / expandTime;
                float radius = maxRadius * progress;
                activeRadii.Add(radius);
                SetRing(wave.ring, radius, 1f - progress * progress, ringSegments, groundHeight);
            }
        }

        void Emit()
        {
            var ring = idleRings.Count > 0 ? idleRings.Pop() : CreateRing(transform, ringMaterial, ringWidth, ringSegments);
            ring.gameObject.SetActive(true);
            waves.Add(new Wave { ring = ring });
        }

        void Retire(Wave wave)
        {
            wave.ring.gameObject.SetActive(false);
            idleRings.Push(wave.ring);
        }

        /// <summary>Makes a flat closed ring under <paramref name="parent"/>.</summary>
        public static LineRenderer CreateRing(Transform parent, Material material, float width, int segments)
        {
            var go = new GameObject("WhistleWave");
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);  // the line lies in its local XY plane: flat on the ground
            var ring = go.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.alignment = LineAlignment.TransformZ;
            ring.loop = true;
            ring.positionCount = segments;
            ring.widthMultiplier = width;
            ring.numCornerVertices = 2;
            ring.sharedMaterial = material;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            return ring;
        }

        /// <summary>Sizes and fades a ring; its opacity goes on the line colour, tinted by the material.</summary>
        public static void SetRing(LineRenderer ring, float radius, float opacity, int segments, float groundHeight)
        {
            var parent = ring.transform.parent;
            var local = ring.transform.localPosition;
            local.y = groundHeight - parent.position.y;
            ring.transform.localPosition = local;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }
            var color = new Color(1f, 1f, 1f, Mathf.Clamp01(opacity));
            ring.startColor = color;
            ring.endColor = color;
        }
    }
}
