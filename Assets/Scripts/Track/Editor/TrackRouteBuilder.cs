using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CompanyF.Track.EditorTools
{
    /// <summary>
    /// Builds the route: one straight line of rails_b running toward +Z from the origin, centreline on x = 0.
    /// Geometry constants are measured from rails.fbx, in prefab-local metres.
    /// </summary>
    public static class TrackRouteBuilder
    {
        const string PrefabPath = "Assets/Train station - Western/Models/Prefabs/rails_b.prefab";

        // In rails_b the pivot is at one end and the track runs toward local -Z;
        // the centreline sits at local x = -CentreX.
        const float CentreX = 0.80775f;
        const float RailTop = 0.1123f;
        const float LenB = 21.72999f;

        const int PieceCount = 104;

        [MenuItem("Tools/Track/Build Route")]
        public static void Build()
        {
            var old = GameObject.Find("Track");
            if (old != null) Undo.DestroyObjectImmediate(old);

            var root = new GameObject("Track");
            Undo.RegisterCreatedObjectUndo(root, "Build Track Route");
            var route = root.AddComponent<TrackRoute>();

            var line = new GameObject("Line").AddComponent<TrackSegment>();
            line.transform.SetParent(root.transform, false);
            route.start = line;

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            for (int i = 0; i < PieceCount; i++)
            {
                // Rotated 180° so the piece runs from its pivot toward +Z; pivot offset puts the centreline on x = 0.
                var rail = (GameObject)PrefabUtility.InstantiatePrefab(asset, line.transform);
                rail.name = $"Rail_{i:000}";
                rail.transform.SetPositionAndRotation(new Vector3(-CentreX, 0f, i * LenB), Quaternion.Euler(0f, 180f, 0f));
            }

            float length = PieceCount * LenB;
            line.SetPoints(new List<Vector3> { new Vector3(0f, RailTop, 0f), new Vector3(0f, RailTop, length) });

            var pieces = new List<TrackSegment.Piece>(PieceCount);
            foreach (Transform rail in line.transform)
                pieces.Add(new TrackSegment.Piece { model = rail, end = line.DistanceAlong(RailExit(rail)) });
            line.SetPieces(pieces);

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
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

            for (var seg = route.start; seg != null; seg = seg.next)
            {
                foreach (Transform rail in seg.transform)
                {
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
            var line = route.start;
            float pathStart = Vector3.Distance(line.transform.TransformPoint(line.Points[0]), first);
            float pathEnd = Vector3.Distance(line.transform.TransformPoint(line.Points[line.Points.Count - 1]), prevEnd ?? first);

            Debug.Log($"[TrackRoute] rails={rails} joints={joints} worstGap={worstGap * 1000f:0.###}mm ({worstAt}) worstKink={worstKink:0.####}deg " +
                      $"pathStartOffset={pathStart * 1000f:0.###}mm pathEndOffset={pathEnd * 1000f:0.###}mm length={line.Length:0.00}m");
        }

        static Vector3 RailExit(Transform rail) => rail.TransformPoint(new Vector3(-CentreX, RailTop, -LenB));
    }
}
