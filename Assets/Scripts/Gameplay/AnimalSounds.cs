using System.Collections.Generic;
using CompanyF.Train;
using UnityEngine;

namespace CompanyF.Gameplay
{
    /// <summary>
    /// Plays one call at a time, predicting the charge trigger one second ahead from current train motion.
    /// Each animal calls at most once; acceleration and curves can change the predicted lead time.
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

            var animal = PickNext(herd.Animals, herd.chargeTime, candidate => herd.WillChargeWithin(candidate, 1f));
            if (animal == null) return;

            var clip = ClipFor(animal);
            if (clip == null) return;
            animal.hasCalled = true;
            source.clip = clip;
            source.Play();
        }

        /// <summary>The first animal running onto the track that has not called on this run; null if none.</summary>
        public static AnimalHerd.Animal PickNext(IReadOnlyList<AnimalHerd.Animal> animals, float chargeTime,
            System.Func<AnimalHerd.Animal, bool> upcoming = null)
        {
            foreach (var animal in animals)
                if (!animal.hasCalled && ((animal.state == AnimalHerd.AnimalState.Charging && animal.timer < chargeTime)
                    || (animal.state == AnimalHerd.AnimalState.Waiting && upcoming != null && upcoming(animal))))
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
