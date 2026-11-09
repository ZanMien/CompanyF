using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CompanyF.Gameplay.EditorTools
{
    public static class AnimalWarningSelfTest
    {
        static void Check(bool result, string message)
        {
            if (!result) throw new InvalidOperationException(message);
            Debug.Log("[Animal Warning] PASS: " + message);
        }
        [MenuItem("Tools/Game/Run Animal Warning Self Test")]
        public static void Run()
        {
            var velocity = Vector3.forward * 10f;
            Check(!AnimalHerd.EntersChargeRange(new Vector3(0,0,91), Vector3.zero, velocity, 80f, 1f), "more than one second away is silent");
            Check(AnimalHerd.EntersChargeRange(new Vector3(0,0,90), Vector3.zero, velocity, 80f, 1f), "one second before charge distance is eligible");
            Check(!AnimalHerd.EntersChargeRange(new Vector3(0,0,90), Vector3.zero, Vector3.zero, 80f, 1f), "stopped train does not predict movement");
            Check(!AnimalHerd.EntersChargeRange(new Vector3(0,0,90), Vector3.zero, -velocity, 80f, 1f), "moving away does not trigger early warning");
            var animal = new AnimalHerd.Animal { state = AnimalHerd.AnimalState.Waiting };
            var list = new List<AnimalHerd.Animal> { animal };
            Check(AnimalSounds.PickNext(list, 0.5f, _ => true) == animal, "waiting animal can call before charging");
            Check(animal.state == AnimalHerd.AnimalState.Waiting, "prediction does not change animal movement");
            animal.hasCalled = true;
            animal.state = AnimalHerd.AnimalState.Charging;
            Check(AnimalSounds.PickNext(list, 0.5f, _ => true) == null, "early warning is not played again on charge");
            Debug.Log("[Animal Warning] ALL PASSED");
        }
        public static void RunBatch()
        {
            Run();
            WhistleAnimalSetup.SelfTest();
            ViewInputSelfTest.RunBatch();
        }
    }
}
