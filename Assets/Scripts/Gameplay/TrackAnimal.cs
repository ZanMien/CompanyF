using CompanyF.Train;
using UnityEngine;

namespace CompanyF.Gameplay
{
    public enum AnimalState { Waiting, Crossing, OnTrack, Returning, Done, Hit }

    /// <summary>
    /// An animal beside the line. When the train comes within <see cref="noticeDistance"/> it walks onto the track
    /// and stays there. The whistle sends it back to where it stood; if the train reaches it first, the train crashes.
    ///   Waiting -> Crossing -> OnTrack -> (whistle) Returning -> Done
    /// While the train is approaching it calls every <see cref="callInterval"/>.
    /// </summary>
    public class TrackAnimal : MonoBehaviour
    {
        [SerializeField] TrainFollower train;
        [SerializeField] WhistleBlast whistle;

        [Tooltip("Metres from the locomotive's nose at which it starts walking onto the track.")]
        [Min(0f)] public float noticeDistance = 200f;
        [Tooltip("Walking speed in m/s.")]
        [Min(0.1f)] public float walkSpeed = 3f;
        [Tooltip("Speed in m/s when the whistle sends it back. The walk animation speeds up to match.")]
        [Min(0.1f)] public float returnSpeed = 8f;
        [Tooltip("Where on the track it stops, in world X (the line runs along Z).")]
        public float trackX = 0f;

        [Header("Hit area around the nose (m)")]
        [Min(0f)] public float hitHalfWidth = 2f;
        [Min(0f)] public float hitLength = 3f;

        [Header("Call")]
        [Tooltip("Plays its clip every call interval. Assign the clip later.")]
        public AudioSource call;
        [Min(0.1f)] public float callInterval = 5f;

        public AnimalState State { get; private set; } = AnimalState.Waiting;

        Vector3 home;
        Vector3 onTrack;
        Animator animator;
        float callClock;

        void Start() => Begin();

        void Update() => Tick(Time.deltaTime);

        /// <summary>Takes its current place as home.</summary>
        public void Begin()
        {
            if (train == null) train = FindFirstObjectByType<TrainFollower>();
            if (whistle == null) whistle = FindFirstObjectByType<WhistleBlast>();
            animator = GetComponentInChildren<Animator>();
            home = transform.position;
            onTrack = new Vector3(trackX, home.y, home.z);
            Face(onTrack);
            Play("Idle");
        }

        /// <summary>Advances by <paramref name="dt"/> seconds.</summary>
        public void Tick(float dt)
        {
            if (train == null || train.Stopped || State == AnimalState.Hit) return;

            var nose = train.NosePosition;
            var offset = transform.position - nose;
            float ahead = Vector3.Dot(offset, train.transform.forward);
            float beside = Vector3.Dot(offset, train.transform.right);

            if (Mathf.Abs(beside) <= hitHalfWidth && ahead <= hitLength && ahead >= -hitLength)
            {
                SetAnimationSpeed(1f);
                Enter(AnimalState.Hit, "Death");
                train.Crash();
                return;
            }

            bool approaching = ahead > 0f && ahead <= noticeDistance;
            if (approaching) Call(dt);

            bool scared = whistle != null && whistle.Covers(transform.position);
            switch (State)
            {
                case AnimalState.Waiting:
                    if (approaching) Enter(AnimalState.Crossing, "Walk");
                    break;
                case AnimalState.Crossing:
                    if (scared) Turn();
                    else if (Walk(onTrack, walkSpeed, dt)) Enter(AnimalState.OnTrack, "Idle");
                    break;
                case AnimalState.OnTrack:
                    if (scared) Turn();
                    break;
                case AnimalState.Returning:
                    if (Walk(home, returnSpeed, dt))
                    {
                        SetAnimationSpeed(1f);
                        Enter(AnimalState.Done, "Idle");
                    }
                    break;
            }
        }

        void Turn()
        {
            Face(home);
            SetAnimationSpeed(returnSpeed / walkSpeed);
            Enter(AnimalState.Returning, "Walk");
        }

        void SetAnimationSpeed(float speed)
        {
            if (animator != null) animator.speed = speed;
        }

        /// <summary>Moves toward <paramref name="target"/>; true once there.</summary>
        bool Walk(Vector3 target, float speed, float dt)
        {
            Face(target);
            var position = Vector3.MoveTowards(transform.position, target, speed * dt);
            var terrain = Terrain.activeTerrain;
            if (terrain != null) position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
            transform.position = position;
            return Vector2.Distance(new Vector2(position.x, position.z), new Vector2(target.x, target.z)) < 0.01f;
        }

        void Face(Vector3 target)
        {
            var direction = target - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        void Call(float dt)
        {
            callClock -= dt;
            if (callClock > 0f) return;
            callClock = callInterval;
            if (call != null && call.clip != null) call.Play();
        }

        void Enter(AnimalState next, string animation)
        {
            State = next;
            Play(animation);
        }

        void Play(string animation)
        {
            if (animator != null && animator.runtimeAnimatorController != null) animator.CrossFade(animation, 0.15f);
        }
    }
}
