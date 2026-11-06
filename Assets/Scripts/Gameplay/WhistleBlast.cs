using CompanyF.Train;
using UnityEngine;

namespace CompanyF.Gameplay
{
    /// <summary>
    /// The whistle's sound wave: an area ahead of the train while the whistle sounds, shown by rings that
    /// travel forward from the nose and widen. <see cref="TrackAnimal"/>s inside it go back beside the line.
    /// </summary>
    public class WhistleBlast : MonoBehaviour
    {
        [SerializeField] TrainFollower train;
        [SerializeField] TrainControls controls;

        [Tooltip("How far ahead of the nose the wave reaches (m).")]
        [Min(0f)] public float range = 60f;
        [Tooltip("Half the width of the wave (m).")]
        [Min(0f)] public float halfWidth = 6f;

        [Header("Rings")]
        [Min(1)] public int ringCount = 4;
        [Tooltip("Seconds for a ring to travel the whole range.")]
        [Min(0.05f)] public float ringTime = 0.8f;
        public Color ringColor = new Color(0.6f, 0.9f, 1f, 0.8f);

        const int RingPoints = 40;

        /// <summary>True while the whistle sounds.</summary>
        public bool Sounding { get; set; }

        LineRenderer[] rings;
        float clock;

        void Start()
        {
            if (train == null) train = GetComponent<TrainFollower>();
            if (controls == null) controls = GetComponent<TrainControls>();

            rings = new LineRenderer[ringCount];
            var material = new Material(Shader.Find("Sprites/Default"));
            for (int i = 0; i < ringCount; i++)
            {
                var line = new GameObject($"WhistleRing_{i}").AddComponent<LineRenderer>();
                line.transform.SetParent(transform, false);
                line.sharedMaterial = material;
                line.useWorldSpace = false;
                line.loop = true;
                line.widthMultiplier = 0.25f;
                line.positionCount = RingPoints;
                for (int p = 0; p < RingPoints; p++)
                {
                    float angle = p * Mathf.PI * 2f / RingPoints;
                    line.SetPosition(p, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f));
                }
                line.enabled = false;
                rings[i] = line;
            }
        }

        void Update()
        {
            if (controls != null) Sounding = controls.IsWhistling;
            clock = Sounding ? clock + Time.deltaTime : 0f;

            for (int i = 0; i < rings.Length; i++)
            {
                var line = rings[i];
                line.enabled = Sounding;
                if (!Sounding) continue;

                float t = Mathf.Repeat(clock / ringTime + i / (float)rings.Length, 1f);
                line.transform.position = train.NosePosition + train.transform.forward * (range * t) + Vector3.up * 2f;
                line.transform.rotation = train.transform.rotation;
                line.transform.localScale = Vector3.one * Mathf.Lerp(1.5f, halfWidth, t);
                var color = ringColor;
                color.a *= 1f - t;
                line.startColor = line.endColor = color;
            }
        }

        /// <summary>True when the wave is sounding and reaches <paramref name="point"/>.</summary>
        public bool Covers(Vector3 point)
        {
            if (!Sounding || train == null) return false;
            var offset = point - train.NosePosition;
            float ahead = Vector3.Dot(offset, train.transform.forward);
            return ahead >= 0f && ahead <= range && Mathf.Abs(Vector3.Dot(offset, train.transform.right)) <= halfWidth;
        }
    }
}
