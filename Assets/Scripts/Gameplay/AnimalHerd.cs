using System;
using System.Collections.Generic;
using CompanyF.Train;
using UnityEngine;

namespace CompanyF.Gameplay
{
    /// <summary>
    /// Every object tagged Cow or Horse: once it is both near the train and inside camera point 2's view, it charges
    /// onto the track. A whistle wave reaching a charging animal turns it around (Rotation Y negated) and sends it back
    /// to where it started, where it stays. Waves do nothing to animals that have not started charging.
    /// Wave reach is measured against the animal's model bounds. Touching the train is judged by comparing colliders
    /// each frame (no Rigidbody involved): the animal vanishes if the whistle is sounding, otherwise the round is lost.
    /// </summary>
    public class AnimalHerd : MonoBehaviour
    {
        public enum AnimalState { Waiting, Charging, Fleeing, Done, Gone }

        public class Animal
        {
            public Transform transform;
            public Renderer[] renderers;
            public Collider collider;
            public AnimalState state;
            public float originalX;
            public float originalRotationY;
            public float startX;
            public float timer;
            /// <summary>Its call has been handled for this run (see <see cref="AnimalSounds"/>).</summary>
            public bool hasCalled;

            public Bounds Bounds
            {
                get
                {
                    if (renderers == null || renderers.Length == 0) return new Bounds(transform.position, Vector3.zero);
                    var bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                    return bounds;
                }
            }
        }

        [SerializeField] TrainFollower train;
        [SerializeField] WhistleWaves waves;
        [SerializeField] CameraPeek cameraPeek;
        [SerializeField] TrainControls controls;
        [Tooltip("The collider that counts as the train when it touches an animal.")]
        [SerializeField] Collider trainCollider;

        [Tooltip("Objects with these tags are treated as animals.")]
        public string[] animalTags = { "Cow", "Horse" };

        [Header("Charging onto the track")]
        [Tooltip("An animal starts charging within this distance of the front of the train (and inside camera point 2's view).")]
        [Min(0f)] public float chargeDistance = 80f;
        [Min(0.01f)] public float chargeTime = 0.5f;
        [Tooltip("World X of the track.")]
        public float trackX = 0f;

        [Header("Being driven off")]
        [Min(0.01f)] public float fleeTime = 0.3f;

        readonly List<Animal> animals = new List<Animal>();
        readonly Plane[] viewPlanes = new Plane[6];

        /// <summary>The train touched an animal while the whistle was not sounding.</summary>
        public event Action StruckWithoutWhistle;

        public IReadOnlyList<Animal> Animals => animals;

        void Awake()
        {
            foreach (var tag in animalTags)
                foreach (var go in GameObject.FindGameObjectsWithTag(tag))
                    Add(go.transform);
        }

        void Start()
        {
            if (train == null) train = GetComponent<TrainFollower>();
            if (waves == null) waves = GetComponent<WhistleWaves>();
            if (cameraPeek == null) cameraPeek = GetComponent<CameraPeek>();

        }

        /// <summary>Predicts the existing charge trigger without moving the animal or changing its timing.</summary>
        public bool WillChargeWithin(Animal animal, float seconds)
        {
            if (train == null || train.Stopped || cameraPeek == null || !enabled) return false;
            cameraPeek.GetPoint2ViewPlanes(viewPlanes);
            if (!GeometryUtility.TestPlanesAABB(viewPlanes, animal.Bounds)) return false;
            return EntersChargeRange(animal.transform.position, train.NosePosition,
                train.transform.forward * train.speed, chargeDistance, seconds);
        }

        public static bool EntersChargeRange(Vector3 animal, Vector3 nose, Vector3 velocity, float distance, float seconds)
        {
            var offset = new Vector2(animal.x - nose.x, animal.z - nose.z);
            var speed = new Vector2(velocity.x, velocity.z);
            float time = speed.sqrMagnitude > 1e-6f
                ? Mathf.Clamp(Vector2.Dot(offset, speed) / speed.sqrMagnitude, 0f, Mathf.Max(0f, seconds)) : 0f;
            return (offset - speed * time).sqrMagnitude <= distance * distance;
        }

        public Animal Add(Transform animal)
        {
            var entry = new Animal
            {
                transform = animal,
                renderers = animal.GetComponentsInChildren<Renderer>(),
                collider = animal.GetComponent<Collider>(),
                state = AnimalState.Waiting,
                originalX = animal.position.x,
                originalRotationY = animal.localEulerAngles.y,
            };
            animals.Add(entry);
            return entry;
        }

        void Update()
        {
            if (train == null || train.Stopped || cameraPeek == null) return;
            cameraPeek.GetPoint2ViewPlanes(viewPlanes);
            Tick(Time.deltaTime, train.NosePosition, train.transform.position,
                 bounds => GeometryUtility.TestPlanesAABB(viewPlanes, bounds),
                 waves != null ? waves.ActiveRadii : null);

            if (trainCollider != null)
            {
                // Colliders otherwise only follow their transforms at the next physics step.
                Physics.SyncTransforms();
                CheckCollisions(trainCollider.bounds, controls != null && controls.IsWhistling);
            }
        }

        /// <summary>
        /// Handles animals touching the train right now: with the whistle sounding they vanish; without it the
        /// round is lost. Only the state at this moment counts.
        /// </summary>
        public void CheckCollisions(Bounds trainBounds, bool whistling)
        {
            foreach (var animal in animals)
            {
                if (animal.state == AnimalState.Gone) continue;
                var bounds = animal.collider != null ? animal.collider.bounds : animal.Bounds;
                if (!bounds.Intersects(trainBounds)) continue;

                if (!whistling)
                {
                    StruckWithoutWhistle?.Invoke();
                    return;
                }
                animal.state = AnimalState.Gone;
                animal.transform.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Advances by <paramref name="dt"/> seconds. Charging is measured from <paramref name="nosePosition"/>;
        /// <paramref name="waveRadii"/> are the waves around <paramref name="trainPosition"/> right now.
        /// </summary>
        public void Tick(float dt, Vector3 nosePosition, Vector3 trainPosition, Func<Bounds, bool> inView, IReadOnlyList<float> waveRadii)
        {
            float reach = 0f;
            bool anyWave = false;
            if (waveRadii != null)
            {
                foreach (var radius in waveRadii)
                {
                    anyWave = true;
                    reach = Mathf.Max(reach, radius);
                }
            }

            foreach (var animal in animals)
            {
                switch (animal.state)
                {
                    case AnimalState.Waiting:
                        if (GroundDistance(animal.transform.position, nosePosition) <= chargeDistance && inView(animal.Bounds))
                        {
                            animal.state = AnimalState.Charging;
                            animal.startX = animal.transform.position.x;
                            animal.timer = 0f;
                        }
                        break;

                    case AnimalState.Charging:
                        animal.timer += dt;
                        SetX(animal, Mathf.Lerp(animal.startX, trackX, Eased(animal.timer / chargeTime)));
                        if (anyWave && DistanceToBounds(animal.Bounds, trainPosition) <= reach) StartFleeing(animal);
                        break;

                    case AnimalState.Fleeing:
                        animal.timer += dt;
                        SetX(animal, Mathf.Lerp(animal.startX, animal.originalX, Eased(animal.timer / fleeTime)));
                        if (animal.timer >= fleeTime)
                        {
                            SetX(animal, animal.originalX);
                            animal.state = AnimalState.Done;
                        }
                        break;
                }
            }
        }

        static void StartFleeing(Animal animal)
        {
            animal.state = AnimalState.Fleeing;
            animal.startX = animal.transform.position.x;
            animal.timer = 0f;
            var euler = animal.transform.localEulerAngles;
            euler.y = -euler.y;
            animal.transform.localEulerAngles = euler;
        }

        static void SetX(Animal animal, float x)
        {
            var position = animal.transform.position;
            position.x = x;
            animal.transform.position = position;
        }

        static float Eased(float progress) => Mathf.SmoothStep(0f, 1f, progress);

        static float GroundDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>Distance on the ground from the train's centre to the nearest edge of the model.</summary>
        static float DistanceToBounds(Bounds bounds, Vector3 point)
        {
            float dx = Mathf.Max(Mathf.Abs(point.x - bounds.center.x) - bounds.extents.x, 0f);
            float dz = Mathf.Max(Mathf.Abs(point.z - bounds.center.z) - bounds.extents.z, 0f);
            return new Vector2(dx, dz).magnitude;
        }
    }
}
