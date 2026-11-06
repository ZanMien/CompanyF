using System.Collections.Generic;
using CompanyF.Train;
using UnityEngine;

namespace CompanyF.Gameplay
{
    /// <summary>
    /// Plays one animal call at a time: when an animal is running onto the track and nothing is playing, its call
    /// starts. Animals that start running during a call are not queued; when the call ends, only one still
    /// running at that moment gets its call. Each run calls at most once.
    /// </summary>
    public class AnimalSounds : MonoBehaviour
    {
        [SerializeField] AnimalHerd herd;
        [SerializeField] TrainFollower train;
        [Tooltip("Plays the calls; separate from the whistle's AudioSource.")]
        [SerializeField] AudioSource source;

        [Header("Calls")]
        public AudioClip cowSound;
        public AudioClip horseSound;

        void Start()
        {
            if (herd == null) herd = GetComponent<AnimalHerd>();
            if (train == null) train = GetComponent<TrainFollower>();

            // Load now so the first call starts without a delay.
            if (cowSound != null) cowSound.LoadAudioData();
            if (horseSound != null) horseSound.LoadAudioData();
        }

        void Update()
        {
            if (herd == null || source == null || source.isPlaying) return;
            if (train != null && train.Stopped) return;

            var animal = PickNext(herd.Animals, herd.chargeTime);
            if (animal == null) return;
            animal.hasCalled = true;

            var clip = ClipFor(animal);
            if (clip == null) return;
            source.clip = clip;
            source.Play();
        }

        /// <summary>The first animal running onto the track that has not called on this run; null if none.</summary>
        public static AnimalHerd.Animal PickNext(IReadOnlyList<AnimalHerd.Animal> animals, float chargeTime)
        {
            foreach (var animal in animals)
                if (animal.state == AnimalHerd.AnimalState.Charging && animal.timer < chargeTime && !animal.hasCalled)
                    return animal;
            return null;
        }

        AudioClip ClipFor(AnimalHerd.Animal animal)
        {
            if (animal.transform.CompareTag("Cow")) return cowSound;
            if (animal.transform.CompareTag("Horse")) return horseSound;
            return null;
        }
    }
}
