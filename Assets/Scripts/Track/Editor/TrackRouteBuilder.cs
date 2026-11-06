using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CompanyF.Track.EditorTools
{
    /// <summary>
    /// Builds the route: a straight line of rails_b running toward +Z from the origin, centreline on x = 0, with
    /// rails_d junctions. Each junction is set to its branch by default, which runs off into a chasm carved in the terrain.
    /// Geometry constants are measured from rails.fbx, in prefab-local metres.
    /// </summary>
    public static class TrackRouteBuilder
    {
        const string PrefabPath = "Assets/Train station - Western/Models/Prefabs/rails_b.prefab";
        const string SwitchPrefabPath = "Assets/Train station - Western/Models/Prefabs/rails_d.prefab";

        // In rails_b the pivot is at one end and the track runs toward local -Z;
        // the centreline sits at local x = -CentreX.
        const float CentreX = 0.80775f;
        const float RailTop = 0.1123f;
        const float LenB = 21.72999f;

        const int PieceCount = 104;

        // rails_d: the straight track is LenSwitch long. The branch follows BranchPath
        // (x sideways from the centreline, y along it) and leaves at BranchAngle degrees.
        const float LenSwitch = 29.70f;
        const float BranchAngle = 25.9f;
        const int BranchRails = 2;

        static readonly Vector2[] BranchPath =
        {
            new Vector2(0f, 0f), new Vector2(0.20f, 3.65f), new Vector2(0.52f, 6.77f), new Vector2(0.97f, 9.75f),
            new Vector2(1.57f, 12.75f), new Vector2(2.33f, 15.73f), new Vector2(3.20f, 18.73f), new Vector2(4.15f, 21.62f),
            new Vector2(5.17f, 24.43f), new Vector2(6.36f, 27.27f), new Vector2(7.68f, 30.04f),
        };

        // Rail index each junction replaces, and the side its branch leaves to (+1 = +X).
        static readonly int[] JunctionAt = { 16, 42, 68 };
        static readonly int[] JunctionSide = { 1, -1, 1 };

        // Chasm: an ellipse beside the line, relative to the junction start.
        const float ChasmCentreX = 80f;
        const float ChasmCentreZ = 85f;
        const float ChasmRadiusX = 62f;
        const float ChasmRadiusZ = 55f;
        const float ChasmFloorY = -95f;

        // Signal post: sideways from the centreline, clear of the train with the lamps at full size.
        const float SignalOffset = 6f;
        const float SignalHeight = 8f;
        const float LampSize = 2f;
        const float LampSpacing = 1.3f;

        [MenuItem("Tools/Track/Build Route")]
        public static void Build()
        {
            var old = GameObject.Find("Track");
            if (old != null) Undo.DestroyObjectImmediate(old);

            var root = new GameObject("Track");
            Undo.RegisterCreatedObjectUndo(root, "Build Track Route");
            var route = root.AddComponent<TrackRoute>();

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var switchAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SwitchPrefabPath);

            var line = NewSegment(root.transform, "Line_0");
            route.start = line;
            float lineStart = 0f, z = 0f;
            int junction = 0;

            for (int i = 0; i < PieceCount; i++)
            {
                if (junction < JunctionAt.Length && i == JunctionAt[junction])
                {
                    int side = JunctionSide[junction];
                    var after = NewSegment(root.transform, $"Line_{junction + 1}");
                    Finish(line, new List<Vector3> { new Vector3(0f, RailTop, lineStart), new Vector3(0f, RailTop, z) });
                    line.next = after;
                    line.branch = BuildBranch(root.transform, asset, junction, z, side);
                    line.branchSelected = true;
                    line.branchOnLeft = side < 0;
                    BuildSignal(line, z, side);
                    CarveChasm(z, side);
                    line = after;
                    lineStart = z;

                    // Mirrored for branches toward -X; the straight track stays on x = 0 either way.
                    var points = (GameObject)PrefabUtility.InstantiatePrefab(switchAsset, line.transform);
                    points.name = $"Switch_{junction}";
                    points.transform.SetPositionAndRotation(new Vector3(-side * CentreX, 0f, z), Quaternion.Euler(0f, 180f, 0f));
                    points.transform.localScale = new Vector3(side, 1f, 1f);
                    z += LenSwitch;
                    junction++;
                    continue;
                }

                // Rotated 180° so the piece runs from its pivot toward +Z; pivot offset puts the centreline on x = 0.
                var rail = (GameObject)PrefabUtility.InstantiatePrefab(asset, line.transform);
                rail.name = $"Rail_{i:000}";
                rail.transform.SetPositionAndRotation(new Vector3(-CentreX, 0f, z), Quaternion.Euler(0f, 180f, 0f));
                z += LenB;
            }

            Finish(line, new List<Vector3> { new Vector3(0f, RailTop, lineStart), new Vector3(0f, RailTop, z) });

            var train = Object.FindFirstObjectByType<CompanyF.Train.TrainFollower>();
            if (train != null && train.GetComponent<TrackSwitchControl>() == null)
                Undo.AddComponent<TrackSwitchControl>(train.gameObject);

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        static TrackSegment NewSegment(Transform parent, string name)
        {
            var segment = new GameObject(name).AddComponent<TrackSegment>();
            segment.transform.SetParent(parent, false);
            return segment;
        }

        static bool IsTrack(Transform child) => child.name.StartsWith("Rail") || child.name.StartsWith("Switch");

        static void Finish(TrackSegment segment, IList<Vector3> points)
        {
            segment.SetPoints(points);
            var pieces = new List<TrackSegment.Piece>(segment.transform.childCount);
            foreach (Transform rail in segment.transform)
                if (IsTrack(rail))
                    pieces.Add(new TrackSegment.Piece { model = rail, end = segment.DistanceAlong(RailExit(rail)) });
            segment.SetPieces(pieces);
        }

        /// <summary>The line that leaves a junction: through the rails_d branch, then straight rails to a broken end.</summary>
        static TrackSegment BuildBranch(Transform parent, GameObject asset, int junction, float z, int side)
        {
            var branch = NewSegment(parent, $"Branch_{junction}");
            branch.endsInChasm = true;

            var points = new List<Vector3>();
            foreach (var p in BranchPath) points.Add(new Vector3(side * p.x, RailTop, z + p.y));

            var rotation = Quaternion.Euler(0f, 180f + side * BranchAngle, 0f);
            var along = Quaternion.Euler(0f, side * BranchAngle, 0f) * Vector3.forward;
            var end = points[points.Count - 1];
            for (int i = 0; i < BranchRails; i++)
            {
                var rail = (GameObject)PrefabUtility.InstantiatePrefab(asset, branch.transform);
                rail.name = $"Rail_{i:000}";
                var start = new Vector3(end.x, 0f, end.z);
                rail.transform.SetPositionAndRotation(start - rotation * new Vector3(-CentreX, 0f, 0f), rotation);
                end += along * LenB;
                points.Add(end);
            }

            Finish(branch, points);
            return branch;
        }

        /// <summary>A post beside the line ahead of the junction, with one lamp for each way.</summary>
        static void BuildSignal(TrackSegment line, float z, int side)
        {
            var post = Part(PrimitiveType.Cube, "Signal", line.transform, new Vector3(-side * SignalOffset, SignalHeight / 2f, z - 30f), new Vector3(0.4f, SignalHeight, 0.4f));
            var signal = post.AddComponent<TrackSignal>();
            signal.junction = line;
            var top = post.transform.position + Vector3.up * (SignalHeight / 2f);
            signal.leftLamp = Part(PrimitiveType.Sphere, "LeftLamp", line.transform, top + Vector3.left * LampSpacing, Vector3.one * LampSize).GetComponent<Renderer>();
            signal.rightLamp = Part(PrimitiveType.Sphere, "RightLamp", line.transform, top + Vector3.right * LampSpacing, Vector3.one * LampSize).GetComponent<Renderer>();
        }

        static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.position = position;
            part.transform.localScale = scale;
            return part;
        }

        /// <summary>Lowers the terrain into a steep-sided pit under the end of a branch. Running it again changes nothing.</summary>
        static void CarveChasm(float z, int side)
        {
            var terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain == null) return;

            var data = terrain.terrainData;
            var origin = terrain.transform.position;
            var size = data.size;
            int resolution = data.heightmapResolution;
            var centre = new Vector2(side * ChasmCentreX, z + ChasmCentreZ);

            int Index(float world, float start, float length) =>
                Mathf.Clamp(Mathf.RoundToInt((world - start) / length * (resolution - 1)), 0, resolution - 1);
            const float Margin = 1.2f;
            int x0 = Index(centre.x - ChasmRadiusX * Margin, origin.x, size.x), x1 = Index(centre.x + ChasmRadiusX * Margin, origin.x, size.x);
            int z0 = Index(centre.y - ChasmRadiusZ * Margin, origin.z, size.z), z1 = Index(centre.y + ChasmRadiusZ * Margin, origin.z, size.z);

            var heights = data.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
            float floor = Mathf.Clamp01((ChasmFloorY - origin.y) / size.y);
            for (int iz = 0; iz <= z1 - z0; iz++)
            for (int ix = 0; ix <= x1 - x0; ix++)
            {
                float worldX = origin.x + (x0 + ix) / (float)(resolution - 1) * size.x;
                float worldZ = origin.z + (z0 + iz) / (float)(resolution - 1) * size.z;
                float dx = (worldX - centre.x) / ChasmRadiusX, dz = (worldZ - centre.y) / ChasmRadiusZ;
                float reach = Mathf.Sqrt(dx * dx + dz * dz) + (Mathf.PerlinNoise(worldX * 0.03f, worldZ * 0.03f) - 0.5f) * 0.16f;
                float depth = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 0.86f, reach));
                heights[iz, ix] = Mathf.Min(heights[iz, ix], Mathf.Lerp(heights[iz, ix], floor, depth));
            }

            data.SetHeights(x0, z0, heights);
            EditorUtility.SetDirty(data);
        }

        /// <summary>Checks every joint between consecutive rails and the path against the rails; logs the worst mismatch.</summary>
        [MenuItem("Tools/Track/Validate Route")]
        public static void Validate()
        {
            var route = Object.FindFirstObjectByType<TrackRoute>();
            if (route == null || route.start == null) { Debug.LogError("[TrackRoute] No TrackRoute in scene."); return; }

            float worstGap = 0f, worstKink = 0f;
            string worstAt = "";
            int joints = 0, rails = 0;
            Vector3? prevEnd = null;
            Vector3 prevDir = Vector3.zero;

            var line = route.start;
            for (var seg = route.start; seg != null; seg = seg.next)
            {
                line = seg;
                foreach (Transform rail in seg.transform)
                {
                    if (!IsTrack(rail)) continue;
                    rails++;
                    var s = rail.TransformPoint(new Vector3(-CentreX, RailTop, 0f));
                    var e = RailExit(rail);
                    var dir = (e - s).normalized;
                    if (prevEnd.HasValue)
                    {
                        joints++;
                        float gap = Vector3.Distance(prevEnd.Value, s);
                        if (gap > worstGap) { worstGap = gap; worstAt = $"{seg.name}/{rail.name}"; }
                        worstKink = Mathf.Max(worstKink, Vector3.Angle(prevDir, dir));
                    }
                    prevEnd = e;
                    prevDir = dir;
                }
            }

            var first = route.start.transform.GetChild(0).TransformPoint(new Vector3(-CentreX, RailTop, 0f));
            float pathStart = Vector3.Distance(route.start.transform.TransformPoint(route.start.Points[0]), first);
            float pathEnd = Vector3.Distance(line.transform.TransformPoint(line.Points[line.Points.Count - 1]), prevEnd ?? first);

            Debug.Log($"[TrackRoute] rails={rails} joints={joints} worstGap={worstGap * 1000f:0.###}mm ({worstAt}) worstKink={worstKink:0.####}deg " +
                      $"pathStartOffset={pathStart * 1000f:0.###}mm pathEndOffset={pathEnd * 1000f:0.###}mm length={line.Length:0.00}m");
        }

        static Vector3 RailExit(Transform rail) =>
            rail.TransformPoint(new Vector3(-CentreX, RailTop, rail.name.StartsWith("Switch") ? -LenSwitch : -LenB));
    }
}
