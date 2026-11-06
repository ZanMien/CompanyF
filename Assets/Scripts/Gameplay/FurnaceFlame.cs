using UnityEngine;

namespace CompanyF.Gameplay
{
    /// <summary>
    /// The fire inside the furnace. The hotter the <see cref="BoilerTemperature"/>, the bigger, taller, denser
    /// and brighter the flames. Put this on an empty object at the furnace floor, under the Train.
    /// </summary>
    public class FurnaceFlame : MonoBehaviour
    {
        [SerializeField] BoilerTemperature boiler;

        [Header("At the lowest (x) and highest (y) temperature")]
        [Tooltip("Particles per second.")]
        public Vector2 emission = new Vector2(40f, 220f);
        [Tooltip("Particle size (m).")]
        public Vector2 size = new Vector2(0.2f, 0.42f);
        [Tooltip("Rising speed (m/s).")]
        public Vector2 riseSpeed = new Vector2(0.4f, 0.75f);
        [Tooltip("Particle lifetime (s).")]
        public Vector2 lifetime = new Vector2(0.4f, 0.65f);
        [Tooltip("Width of the fire across the furnace (m).")]
        public Vector2 width = new Vector2(0.45f, 0.75f);

        [Header("Colour")]
        public Color coolColor = new Color(0.9f, 0.25f, 0.05f);
        public Color hotColor = new Color(1f, 0.85f, 0.45f);

        [Tooltip("How fast the fire follows the temperature (0 to 1 per second).")]
        [Min(0.01f)] public float response = 2f;

        /// <summary>0 at the lowest temperature, 1 at the overheat temperature.</summary>
        public float Heat { get; private set; }

        ParticleSystem flame;

        void Awake() => Begin();

        void Start()
        {
            if (boiler == null) boiler = FindFirstObjectByType<BoilerTemperature>();
        }

        void Update()
        {
            if (boiler == null) return;
            float target = Mathf.InverseLerp(boiler.minTemperature, boiler.overheatTemperature, boiler.Temperature);
            SetHeat(Mathf.MoveTowards(Heat, target, response * Time.deltaTime));
        }

        /// <summary>Builds the particle system.</summary>
        public void Begin()
        {
            flame = gameObject.AddComponent<ParticleSystem>();
            flame.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = flame.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 400;

            var shape = flame.shape;
            shape.shapeType = ParticleSystemShapeType.Box;

            var velocity = flame.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;

            var noise = flame.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 1.5f;

            var fade = flame.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.5f, 0.15f), 0.5f), new GradientColorKey(new Color(0.7f, 0.1f, 0.02f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0.5f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var shrink = flame.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

            var material = new Material(Shader.Find("Sprites/Default")) { mainTexture = SoftDot() };
            GetComponent<ParticleSystemRenderer>().sharedMaterial = material;

            SetHeat(0f);
            flame.Play();
        }

        /// <summary>Sets how strong the fire is, from 0 to 1.</summary>
        public void SetHeat(float heat)
        {
            Heat = Mathf.Clamp01(heat);

            var main = flame.main;
            main.startLifetime = Mathf.Lerp(lifetime.x, lifetime.y, Heat);
            main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Lerp(size.x, size.y, Heat) * 0.7f, Mathf.Lerp(size.x, size.y, Heat));
            main.startSpeed = 0f;
            main.startColor = Color.Lerp(coolColor, hotColor, Heat);

            var emit = flame.emission;
            emit.rateOverTime = Mathf.Lerp(emission.x, emission.y, Heat);

            var shape = flame.shape;
            shape.scale = new Vector3(Mathf.Lerp(width.x, width.y, Heat), 0.02f, 0.2f);

            var velocity = flame.velocityOverLifetime;
            float rise = Mathf.Lerp(riseSpeed.x, riseSpeed.y, Heat);
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(rise * 0.7f, rise * 1.2f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        }

        static Texture2D SoftDot()
        {
            const int Size = 64;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), Vector2.one * (Size - 1) / 2f) / (Size / 2f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(1f, 0f, d)));
            }
            texture.Apply();
            return texture;
        }
    }
}
