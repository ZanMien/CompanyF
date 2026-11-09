using CompanyF.Track;
using CompanyF.Train;
using CompanyF.UI;
using UnityEngine;

namespace CompanyF.Gameplay
{
    /// <summary>
    /// Plays the game's sound effects by listening to the gameplay events: furnace door, coal, track lever,
    /// result jingles and the scene change. The fire loops while the train runs and gets louder as the boiler heats up.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        [Header("Furnace")]
        public AudioClip doorOpen;
        public AudioClip doorClose;
        public AudioClip shovel;
        public AudioClip fireUp;
        public AudioClip fireLoop;

        [Header("Track")]
        public AudioClip lever;

        [Header("Result")]
        public AudioClip clear;
        public AudioClip gameOver;
        public AudioClip sceneChange;

        [Header("Volume")]
        [Range(0f, 1f)] public float effectVolume = 1f;
        [Tooltip("Fire loop volume at the lowest boiler temperature.")]
        [Range(0f, 1f)] public float fireVolumeCold = 0.2f;
        [Tooltip("Fire loop volume just before the boiler overheats.")]
        [Range(0f, 1f)] public float fireVolumeHot = 0.8f;

        TrainFollower train;
        BoilerTemperature boiler;
        FurnaceController furnace;
        TrackSwitchControl trackSwitch;
        GameResultUI result;
        AudioSource effects;
        AudioSource fire;

        void Awake()
        {
            effects = gameObject.AddComponent<AudioSource>();
            effects.playOnAwake = false;
            fire = gameObject.AddComponent<AudioSource>();
            fire.playOnAwake = false;
            fire.loop = true;
            fire.clip = fireLoop;
        }

        void Start()
        {
            train = FindFirstObjectByType<TrainFollower>();
            boiler = FindFirstObjectByType<BoilerTemperature>();
            furnace = FindFirstObjectByType<FurnaceController>();
            trackSwitch = FindFirstObjectByType<TrackSwitchControl>();
            result = FindFirstObjectByType<GameResultUI>();

            if (furnace != null)
            {
                furnace.StateEntered += OnDoorState;
                furnace.CoalAdded += OnCoalAdded;
            }
            if (trackSwitch != null) trackSwitch.Switched += OnSwitched;
            if (result != null)
            {
                result.Shown += OnResultShown;
                result.Leaving += OnLeaving;
            }
        }

        void OnDestroy()
        {
            if (furnace != null)
            {
                furnace.StateEntered -= OnDoorState;
                furnace.CoalAdded -= OnCoalAdded;
            }
            if (trackSwitch != null) trackSwitch.Switched -= OnSwitched;
            if (result != null)
            {
                result.Shown -= OnResultShown;
                result.Leaving -= OnLeaving;
            }
        }

        void Update()
        {
            bool burning = train != null && !train.Stopped;
            if (burning && !fire.isPlaying && fire.clip != null) fire.Play();
            else if (!burning && fire.isPlaying) fire.Stop();

            if (boiler != null)
                fire.volume = Mathf.Lerp(fireVolumeCold, fireVolumeHot,
                    Mathf.InverseLerp(boiler.minTemperature, boiler.overheatTemperature, boiler.Temperature));
        }

        void OnDoorState(DoorState state)
        {
            switch (state)
            {
                case DoorState.Opening: Play(doorOpen); break;
                case DoorState.Closing: Play(doorClose); break;
                case DoorState.Loading: Play(shovel); break;
            }
        }

        void OnCoalAdded(float heat) => Play(fireUp);
        void OnSwitched() => Play(lever);
        void OnResultShown(bool cleared) => Play(cleared ? clear : gameOver);

        /// <summary>The scene is about to be replaced, so this sound plays on an object that outlives it.</summary>
        void OnLeaving()
        {
            if (sceneChange == null) return;
            var go = new GameObject("SceneChangeSound");
            DontDestroyOnLoad(go);
            go.AddComponent<AudioSource>().PlayOneShot(sceneChange, effectVolume);
            Destroy(go, sceneChange.length);
        }

        void Play(AudioClip clip)
        {
            if (clip != null) effects.PlayOneShot(clip, effectVolume);
        }
    }
}
