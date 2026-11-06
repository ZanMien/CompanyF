using System.Collections.Generic;
using CompanyF.Track;
using CompanyF.Train;
using UnityEditor;
using UnityEngine;

namespace CompanyF.Gameplay.EditorTools
{
    /// <summary>
    /// Runs a temporary train and animal on the scene's route and logs PASS/FAIL (failures first):
    /// without the whistle the animal blocks the line and the train crashes, with it the animal goes
    /// back and the train passes.
    /// </summary>
    public static class AnimalSelfTest
    {
        const float Dt = 0.02f;
        const float SpotZ = 250f;

        [MenuItem("Tools/Game/Run Animal Self Test")]
        public static void Run()
        {
            var route = Object.FindFirstObjectByType<TrackRoute>();
            if (route == null || route.start == null) { Debug.LogError("[Animal SelfTest] No TrackRoute in scene."); return; }

            var log = new List<string>();
            int failures = 0;
            void Check(string what, bool ok)
            {
                log.Add($"{(ok ? "PASS" : "FAIL")}  {what}");
                if (!ok) failures++;
            }

            // No whistle: it walks onto the track and the train hits it.
            int crashes = 0;
            var train = NewTrain(route, out var whistle);
            train.Crashed += () => crashes++;
            var animal = NewAnimal(train, whistle);
            Check($"far away: {animal.State}", animal.State == AnimalState.Waiting);
            for (int i = 0; i < 20000 && !train.Stopped; i++) Step(train, animal);
            Check($"no whistle: {animal.State}, Crashed x{crashes}, train stopped ({train.Halted}) at x {animal.transform.position.x:0.0}",
                  animal.State == AnimalState.Hit && crashes == 1 && train.Halted && Mathf.Abs(animal.transform.position.x) < 2f);
            Object.DestroyImmediate(animal.gameObject);
            Object.DestroyImmediate(train.gameObject);

            // Whistle: on the track it goes back home, and the train passes.
            crashes = 0;
            train = NewTrain(route, out whistle);
            train.Crashed += () => crashes++;
            animal = NewAnimal(train, whistle);
            var home = animal.transform.position;
            for (int i = 0; i < 20000 && animal.State != AnimalState.OnTrack; i++) Step(train, animal);
            Check($"reached the track: {animal.State}", animal.State == AnimalState.OnTrack);
            for (int i = 0; i < 20000 && !whistle.Covers(animal.transform.position); i++)
            {
                whistle.Sounding = true;
                Step(train, animal);
            }
            Step(train, animal);
            Check($"whistled: {animal.State}", animal.State == AnimalState.Returning);
            whistle.Sounding = false;
            for (int i = 0; i < 20000 && train.NosePosition.z < SpotZ + 10f && !train.Stopped; i++) Step(train, animal);
            Check($"train passed, Crashed x{crashes}", crashes == 0 && !train.Stopped);
            for (int i = 0; i < 2000 && animal.State != AnimalState.Done; i++) Step(train, animal);
            Check($"back home: {animal.State}, {Vector3.Distance(animal.transform.position, home):0.00} m away",
                  animal.State == AnimalState.Done && Vector3.Distance(animal.transform.position, home) < 0.1f);
            Object.DestroyImmediate(animal.gameObject);
            Object.DestroyImmediate(train.gameObject);

            log.Sort((a, b) => b.StartsWith("FAIL").CompareTo(a.StartsWith("FAIL")));
            Debug.Log($"[Animal SelfTest] {(failures == 0 ? "ALL PASSED" : failures + " FAILED")}\n" + string.Join("\n", log));
        }

        static void Step(TrainFollower train, TrackAnimal animal)
        {
            train.Tick(Dt);
            animal.Tick(Dt);
        }

        static TrainFollower NewTrain(TrackRoute route, out WhistleBlast whistle)
        {
            var go = new GameObject("_AnimalTrain") { hideFlags = HideFlags.HideAndDontSave };
            var train = go.AddComponent<TrainFollower>();
            var so = new SerializedObject(train);
            so.FindProperty("route").objectReferenceValue = route;
            so.ApplyModifiedPropertiesWithoutUndo();
            train.speed = 20f;
            train.Begin();

            whistle = go.AddComponent<WhistleBlast>();
            var wso = new SerializedObject(whistle);
            wso.FindProperty("train").objectReferenceValue = train;
            wso.ApplyModifiedPropertiesWithoutUndo();
            return train;
        }

        static TrackAnimal NewAnimal(TrainFollower train, WhistleBlast whistle)
        {
            var go = new GameObject("_Animal") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.position = new Vector3(9f, 0f, SpotZ);
            var animal = go.AddComponent<TrackAnimal>();
            var so = new SerializedObject(animal);
            so.FindProperty("train").objectReferenceValue = train;
            so.FindProperty("whistle").objectReferenceValue = whistle;
            so.ApplyModifiedPropertiesWithoutUndo();
            animal.Begin();
            return animal;
        }
    }
}
