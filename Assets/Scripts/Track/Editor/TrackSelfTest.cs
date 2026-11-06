using System.Collections.Generic;
using CompanyF.Train;
using UnityEditor;
using UnityEngine;

namespace CompanyF.Track.EditorTools
{
    /// <summary>
    /// Runs a temporary train over the scene's route and logs PASS/FAIL (failures first):
    /// left alone it falls at the first junction, switched to the main line it reaches the end,
    /// and the terrain is flat under the line and deep past each branch end.
    /// </summary>
    public static class TrackSelfTest
    {
        const float Dt = 0.02f;

        [MenuItem("Tools/Track/Run Route Self Test")]
        public static void Run()
        {
            var route = Object.FindFirstObjectByType<TrackRoute>();
            if (route == null || route.start == null) { Debug.LogError("[Track SelfTest] No TrackRoute in scene."); return; }

            var log = new List<string>();
            int failures = 0;
            void Check(string what, bool ok)
            {
                log.Add($"{(ok ? "PASS" : "FAIL")}  {what}");
                if (!ok) failures++;
            }

            var junctions = new List<TrackSegment>();
            for (var seg = route.start; seg != null; seg = seg.next)
                if (seg.branch != null) junctions.Add(seg);
            Check($"{junctions.Count} junctions, all set to the branch", junctions.Count == 3 && junctions.TrueForAll(j => j.branchSelected));

            // Left alone: falls at the end of the first branch, and Fell comes once.
            int fell = 0, reached = 0;
            var first = NewTrain(route, () => fell++, () => reached++);
            for (int i = 0; i < 20000 && !first.Falling; i++) first.Tick(Dt);
            var edge = first.transform.position;
            for (int i = 0; i < 200; i++) first.Tick(Dt);
            Check($"no switching: fell on {first.CurrentSegment.name}, Fell x{fell}, ReachedEnd x{reached}",
                  first.CurrentSegment == junctions[0].branch && fell == 1 && reached == 0);
            Check($"falling: dropped {edge.y - first.transform.position.y:0.0} m, rules stopped",
                  first.transform.position.y < edge.y - 20f && first.Stopped);
            Object.DestroyImmediate(first.gameObject);

            // Only available within range of the junction.
            var near = NewTrain(route, null, null);
            Check("far from a junction: nothing to switch", near.UpcomingJunction(120f) == null);
            for (int i = 0; i < 20000 && near.UpcomingJunction(120f) == null; i++) near.Tick(Dt);
            Check("within range: first junction", near.UpcomingJunction(120f) == junctions[0]);
            Object.DestroyImmediate(near.gameObject);

            // All switched to the main line: reaches the end.
            foreach (var j in junctions) j.branchSelected = false;
            fell = reached = 0;
            var main = NewTrain(route, () => fell++, () => reached++);
            for (int i = 0; i < 20000 && !main.Stopped; i++) main.Tick(Dt);
            Check($"switched: arrived ({main.Arrived}), Fell x{fell}, ReachedEnd x{reached}", main.Arrived && fell == 0 && reached == 1);
            Object.DestroyImmediate(main.gameObject);
            foreach (var j in junctions) j.branchSelected = true;

            var terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain != null)
                foreach (var j in junctions)
                {
                    j.Evaluate(j.Length, out var at, out _);
                    j.branch.Evaluate(j.branch.Length, out var end, out var along);
                    float dip = 0f;
                    for (float z = 0f; z <= 160f; z += 2f)
                        dip = Mathf.Max(dip, Height(terrain, at) - Height(terrain, new Vector3(0f, 0f, at.z + z)));
                    float past = Height(terrain, end + along * 15f);
                    Check($"{j.branch.name}: line beside the chasm dips {dip:0.0} m, ground past the rail end at {past:0.0} m",
                          dip < 0.5f && past < -40f);
                }

            log.Sort((a, b) => b.StartsWith("FAIL").CompareTo(a.StartsWith("FAIL")));
            Debug.Log($"[Track SelfTest] {(failures == 0 ? "ALL PASSED" : failures + " FAILED")}\n" + string.Join("\n", log));
        }

        static float Height(Terrain terrain, Vector3 world) => terrain.SampleHeight(world) + terrain.transform.position.y;

        static TrainFollower NewTrain(TrackRoute route, System.Action fell, System.Action reached)
        {
            var train = new GameObject("_TrackTrain") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<TrainFollower>();
            var so = new SerializedObject(train);
            so.FindProperty("route").objectReferenceValue = route;
            so.ApplyModifiedPropertiesWithoutUndo();
            train.speed = 40f;
            train.Fell += fell;
            train.ReachedEnd += reached;
            train.Begin();
            return train;
        }
    }
}
