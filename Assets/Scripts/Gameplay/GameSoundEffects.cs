using System.Collections.Generic;
using CompanyF.Train;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CompanyF.Gameplay
{
    /// <summary>Scene-configured, non-looping sound effects. Gameplay timers remain independent of clip duration.</summary>
    public class GameSoundEffects : MonoBehaviour
    {
        [SerializeField] FurnaceController furnace;
        [SerializeField] TrainControls controls;
        [SerializeField] TrainFollower train;
        [SerializeField] BoilerTemperature boiler;
        [SerializeField] GameFlow flow;
        [SerializeField] AudioSource effectsSource;
        [SerializeField] AudioSource whistleSource;
        [SerializeField] AudioSource fireSource;
        [SerializeField] AudioSource resultSource;

        public AudioClip whistle;
        public AudioClip shovel;
        public AudioClip doorOpen;
        public AudioClip doorClose;
        public AudioClip fire;
        public AudioClip fireFlare;
        public AudioClip clear;
        public AudioClip gameOver;
        public AudioClip sceneTransition;
        [Tooltip("Hold the furnace door key this long to play the fire sound once per hold.")]
        [Min(0f)] public float fireHoldDelay = 1.5f;

        bool previousWhistle;
        bool firePlayed;
        bool resultPlayed;
        float fireHeld;
        readonly Dictionary<AudioSource, bool> mutedSources = new Dictionary<AudioSource, bool>();

        void OnEnable()
        {
            if (furnace == null) furnace = GetComponent<FurnaceController>();
            if (controls == null) controls = GetComponent<TrainControls>();
            if (train == null) train = GetComponent<TrainFollower>();
            if (boiler == null) boiler = GetComponent<BoilerTemperature>();
            if (flow == null) flow = GetComponent<GameFlow>();
            previousWhistle = controls != null && controls.IsWhistling;
            if (furnace != null)
            {
                furnace.StateEntered += OnDoor;
                furnace.ShovelStarted += OnShovel;
                furnace.CoalReachedApex += OnFlare;
            }
            if (train != null) train.ReachedEnd += OnClear;
            if (boiler != null) boiler.Overheat += OnGameOver;
            if (flow != null) flow.GameOver += OnGameOver;
        }

        void OnDisable()
        {
            if (furnace != null)
            {
                furnace.StateEntered -= OnDoor;
                furnace.ShovelStarted -= OnShovel;
                furnace.CoalReachedApex -= OnFlare;
            }
            if (train != null) train.ReachedEnd -= OnClear;
            if (boiler != null) boiler.Overheat -= OnGameOver;
            if (flow != null) flow.GameOver -= OnGameOver;
            StopSource(effectsSource);
            StopSource(whistleSource);
            StopSource(fireSource);
            StopSource(resultSource);
            foreach (var pair in mutedSources)
                if (pair.Key != null) pair.Key.mute = pair.Value;
            mutedSources.Clear();
            fireHeld = 0f;
            firePlayed = false;
        }

        void LateUpdate()
        {
            if (resultPlayed)
            {
                SilenceOtherAudio();
                return;
            }
            if (controls != null && !controls.CanWhistle) StopSource(whistleSource);
            bool sounding = controls != null && controls.enabled && controls.CanWhistle && controls.IsWhistling;
            if (sounding && !previousWhistle && !resultPlayed)
            {
                StopSource(whistleSource);
                Play(whistleSource, whistle);
            }
            previousWhistle = sounding;

            var keyboard = Keyboard.current;
            bool holding = !resultPlayed && furnace != null && furnace.enabled && furnace.CanOperate
                && (flow == null || flow.Phase == GamePhase.Running)
                && (train == null || !train.Stopped) && keyboard != null
                && keyboard[furnace.p1DoorKey].isPressed;
            if (!holding) { fireHeld = 0f; firePlayed = false; return; }
            fireHeld += Time.deltaTime;
            if (!firePlayed && fireHeld >= fireHoldDelay)
            {
                firePlayed = true;
                Play(fireSource, fire);
            }
        }

        void OnDoor(DoorState state)
        {
            if (resultPlayed) return;
            if (state == DoorState.Opening) Play(effectsSource, doorOpen);
            else if (state == DoorState.Closing) Play(effectsSource, doorClose);
        }

        void OnShovel() { if (!resultPlayed) Play(effectsSource, shovel); }
        void OnFlare() { if (!resultPlayed) Play(fireSource, fireFlare); }
        void OnClear() => PlayResult(clear);
        void OnGameOver() => PlayResult(gameOver);

        void PlayResult(AudioClip clip)
        {
            if (resultPlayed) return;
            resultPlayed = true;
            SilenceOtherAudio();
            StopSource(resultSource);
            Play(resultSource, clip);
        }

        // Include animal calls, ambient sounds and sources started after the result screen appears.
        // Restore their original mute settings when this controller leaves the scene.
        void SilenceOtherAudio()
        {
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (source == resultSource) continue;
                if (!mutedSources.ContainsKey(source)) mutedSources.Add(source, source.mute);
                source.mute = true;
                source.Stop();
            }
        }

        /// <summary>Call from a future scene-transition controller before changing scenes.</summary>
        public void PlaySceneTransition() => Play(resultSource, sceneTransition);

        static void StopSource(AudioSource source) { if (source != null) source.Stop(); }
        static void Play(AudioSource source, AudioClip clip)
        {
            if (source != null && clip != null) source.PlayOneShot(clip);
        }
    }
}
