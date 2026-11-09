using CompanyF.Train;
using UnityEngine;

namespace CompanyF.Gameplay
{
    public class WhistleSmoke : MonoBehaviour
    {
        [SerializeField] TrainControls controls;
        [SerializeField] TrainFollower train;
        [SerializeField] ParticleSystem smoke;

        void Awake()
        {
            if (controls == null) controls = GetComponent<TrainControls>();
            if (train == null) train = GetComponent<TrainFollower>();
        }

        void LateUpdate() => Tick(controls != null && controls.enabled && controls.CanWhistle
            && controls.IsWhistling && (train == null || !train.Stopped));

        public void Tick(bool sounding)
        {
            if (smoke == null) return;
            var emission = smoke.emission;
            emission.enabled = sounding;
            if (sounding && !smoke.isEmitting) smoke.Play();
            else if (!sounding && smoke.isEmitting)
                smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        void OnDisable()
        {
            if (smoke == null) return;
            var emission = smoke.emission;
            emission.enabled = false;
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
