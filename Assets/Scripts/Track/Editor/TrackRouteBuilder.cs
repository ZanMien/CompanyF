using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CompanyF.Track.EditorTools
{
    /// <summary>
    /// Builds the route: straight section -> fork -> left/right branches -> merge, three times;
    /// the third fork has no merge because passing it ends the game.
    /// Every piece runs toward +Z. A fork's straight leg is "left", its diverging leg is "right".
    /// Geometry constants are measured from rails.fbx, in prefab-local metres.
    /// </summary>
    public static class TrackRouteBuilder
    {
        const string PrefabDir = "Assets/Train station - Western/Models/Prefabs/";

        // In every rails_* prefab the pivot is at one end and the track runs toward local -Z;
        // the centreline sits at local x = -CentreX.
        const float CentreX = 0.80775f;
        const float RailTop = 0.1123f;

        const float LenB = 21.72999f;  // rails_b, straight
        const float LenC = 35.31830f;  // rails_c, straight leg
        const float LenD = 29.70430f;  // rails_d, straight leg
        const float DivergeHeadingD = 25.641f;  // rails_d diverging leg end heading (rails_c ends parallel)

        const int SectionPieces = 10;  // 217.3 m, the fork is the first piece after the 200 m mark
        const int BranchPieces = 7;    // 152.1 m between fork and merge
        const int FinalPieces = 3;     // run-out after the last fork

        static readonly string[] ForkPrefabs = { "rails_c", "rails_c", "rails_d" };

        // Diverging-leg centreline (x, z), prefab-local, from the pivot end to the far end.
        static readonly Vector2[] DivergeC =
        {
            new Vector2(-0.80775f, 0f), new Vector2(-0.83217f, -0.60046f), new Vector2(-0.88814f, -2.12022f), new Vector2(-1.00023f, -3.63801f),
            new Vector2(-1.09242f, -5.15715f), new Vector2(-1.18462f, -6.67629f), new Vector2(-1.27689f, -8.19542f), new Vector2(-1.37365f, -9.7142f),
            new Vector2(-1.51942f, -11.22913f), new Vector2(-1.67162f, -12.74324f), new Vector2(-1.91504f, -14.24548f), new Vector2(-2.16201f, -15.74723f),
            new Vector2(-2.46936f, -17.2377f), new Vector2(-2.78167f, -18.72724f), new Vector2(-3.09338f, -20.21691f), new Vector2(-3.40505f, -21.70659f),
            new Vector2(-3.66905f, -23.20539f), new Vector2(-3.92936f, -24.70489f), new Vector2(-4.12396f, -26.21416f), new Vector2(-4.3112f, -27.72453f),
            new Vector2(-4.45215f, -29.23982f), new Vector2(-4.58656f, -30.75581f), new Vector2(-4.6577f, -32.27583f), new Vector2(-4.71707f, -33.7966f),
            new Vector2(-4.73059f, -35.3183f),
        };

        static readonly Vector2[] DivergeD =
        {
            new Vector2(-0.80775f, 0f), new Vector2(-0.83219f, -0.60048f), new Vector2(-0.87432f, -1.93137f), new Vector2(-0.97349f, -3.26023f),
            new Vector2(-1.09936f, -4.58671f), new Vector2(-1.237f, -5.91214f), new Vector2(-1.39654f, -7.23477f), new Vector2(-1.59645f, -8.55225f),
            new Vector2(-1.80191f, -9.86875f), new Vector2(-2.06424f, -11.17523f), new Vector2(-2.32657f, -12.48171f), new Vector2(-2.64162f, -13.77619f),
            new Vector2(-2.9708f, -15.06745f), new Vector2(-3.31994f, -16.35328f), new Vector2(-3.69067f, -17.63323f), new Vector2(-4.0678f, -18.9112f),
            new Vector2(-4.48405f, -20.17708f), new Vector2(-4.90031f, -21.44295f), new Vector2(-5.35072f, -22.69699f), new Vector2(-5.80681f, -23.94907f),
            new Vector2(-6.29953f, -25.18679f), new Vector2(-6.81542f, -26.41543f), new Vector2(-7.34989f, -27.63573f), new Vector2(-7.92628f, -28.83718f),
            new Vector2(-8.50267f, -30.03863f),
        };

        [MenuItem("Tools/Track/Build Route")]
        public static void Build()
        {
            var old = GameObject.Find("Track");
            if (old != null) Undo.DestroyObjectImmediate(old);

            var root = new GameObject("Track");
            Undo.RegisterCreatedObjectUndo(root, "Build Track Route");
            var route = root.AddComponent<TrackRoute>();

            var cursor = Vector3.zero;  // centreline at ground level, travelling +Z
            var waitingForNext = new List<TrackSegment>();

            for (int f = 0; f < ForkPrefabs.Length; f++)
            {
                int n = f + 1;
                bool final = f == ForkPrefabs.Length - 1;

                // Straight section up to the fork.
                var section = NewChild<TrackSegment>(root.transform, $"Section_{n}");
                foreach (var s in waitingForNext) s.next = section;
                if (f == 0) route.start = section;
                var sectionStart = cursor;
                cursor = PlaceRun(section.transform, cursor, 0f, SectionPieces);
                section.SetPoints(ToLocal(section.transform, new List<Vector3> { sectionStart + Vector3.up * RailTop, cursor + Vector3.up * RailTop }));

                // Fork.
                string forkPrefab = ForkPrefabs[f];
                bool isD = forkPrefab == "rails_d";
                var junction = NewChild<TrackJunction>(root.transform, $"Junction_{n}");
                junction.transform.position = cursor + Vector3.up * RailTop;
                junction.index = n;
                junction.isFinal = final;
                section.endJunction = junction;
                route.junctions.Add(junction);
                var fork = PlaceForward(junction.transform, forkPrefab, $"Turnout_{forkPrefab}", cursor, 0f);

                var left = NewChild<TrackSegment>(root.transform, $"Branch_{n}_Left");
                var right = NewChild<TrackSegment>(root.transform, $"Branch_{n}_Right");
                junction.left = left;
                junction.right = right;

                var leftPts = new List<Vector3>();
                var rightPts = new List<Vector3>();
                AddLeg(leftPts, fork.transform, Straight(isD ? LenD : LenC), false);
                AddLeg(rightPts, fork.transform, isD ? DivergeD : DivergeC, false);

                var leftEnd = PlaceRun(left.transform, Ground(leftPts[leftPts.Count - 1]), 0f, final ? FinalPieces : BranchPieces);
                var rightEnd = PlaceRun(right.transform, Ground(rightPts[rightPts.Count - 1]), isD ? DivergeHeadingD : 0f, final ? FinalPieces : BranchPieces);
                AddPoint(leftPts, leftEnd + Vector3.up * RailTop);
                AddPoint(rightPts, rightEnd + Vector3.up * RailTop);

                if (!final)
                {
                    // Merge: rails_c turned around. Its straight leg takes the right branch, its diverging leg the left one.
                    var outCentre = rightEnd + Vector3.forward * LenC;
                    var mergeRoot = new GameObject($"Merge_{n}");
                    mergeRoot.transform.SetParent(root.transform, false);
                    mergeRoot.transform.position = outCentre + Vector3.up * RailTop;
                    var merge = PlaceMerge(mergeRoot.transform, "rails_c", "Turnout_rails_c", outCentre);
                    AddLeg(leftPts, merge.transform, DivergeC, true);
                    AddLeg(rightPts, merge.transform, Straight(LenC), true);
                    cursor = outCentre;
                    waitingForNext = new List<TrackSegment> { left, right };
                }

                left.SetPoints(ToLocal(left.transform, leftPts));
                right.SetPoints(ToLocal(right.transform, rightPts));
            }

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        /// <summary>
        /// Checks every joint: consecutive rails_b inside a segment, turnout legs against the branch runs,
        /// and path endpoints against the junctions / next segments. Logs the worst mismatch.
        /// </summary>
        [MenuItem("Tools/Track/Validate Route")]
        public static void Validate()
        {
            var route = Object.FindFirstObjectByType<TrackRoute>();
            if (route == null) { Debug.LogError("[TrackRoute] No TrackRoute in scene."); return; }

            float worstPiece = 0f, worstPath = 0f, worstHeading = 0f;
            int joints = 0;
            string worstPieceAt = "", worstPathAt = "";

            void Gap(Vector3 a, Vector3 b, string where, bool path)
            {
                joints++;
                float d = Vector3.Distance(a, b);
                if (path && d > worstPath) { worstPath = d; worstPathAt = where; }
                if (!path && d > worstPiece) { worstPiece = d; worstPieceAt = where; }
            }

            // Centreline start/end of every rails_b: start = pivot end, end = far end.
            foreach (var seg in route.GetComponentsInChildren<TrackSegment>())
            {
                Vector3? prevEnd = null;
                Vector3 prevDir = Vector3.zero;
                foreach (Transform piece in seg.transform)
                {
                    var s = piece.TransformPoint(new Vector3(-CentreX, 0f, 0f));
                    var e = piece.TransformPoint(new Vector3(-CentreX, 0f, -LenB));
                    var dir = (e - s).normalized;
                    if (prevEnd.HasValue)
                    {
                        Gap(prevEnd.Value, s, $"{seg.name}/{piece.name}", false);
                        worstHeading = Mathf.Max(worstHeading, Vector3.Angle(prevDir, dir));
                    }
                    prevEnd = e;
                    prevDir = dir;
                }

                // The path must pass exactly through the first and last rail of its own run.
                if (seg.transform.childCount > 0 && seg.Points.Count >= 2)
                {
                    var first = seg.transform.GetChild(0).TransformPoint(new Vector3(-CentreX, RailTop, 0f));
                    var last = seg.transform.GetChild(seg.transform.childCount - 1).TransformPoint(new Vector3(-CentreX, RailTop, -LenB));
                    bool startsAtFirstRail = seg.name.StartsWith("Section");
                    var pts = seg.Points;
                    float dFirst = float.MaxValue, dLast = float.MaxValue;
                    foreach (var p in pts)
                    {
                        var w = seg.transform.TransformPoint(p);
                        dFirst = Mathf.Min(dFirst, Vector3.Distance(w, first));
                        dLast = Mathf.Min(dLast, Vector3.Distance(w, last));
                    }
                    if (startsAtFirstRail) Gap(seg.transform.TransformPoint(pts[0]), first, $"{seg.name} path start", true);
                    else Gap(first, first + Vector3.right * dFirst, $"{seg.name} turnout->first rail", false);
                    if (seg.next == null && seg.endJunction == null) Gap(seg.transform.TransformPoint(pts[pts.Count - 1]), last, $"{seg.name} path end", true);
                    else Gap(last, last + Vector3.right * dLast, $"{seg.name} last rail->turnout", false);
                }

                var end = seg.transform.TransformPoint(seg.Points[seg.Points.Count - 1]);
                if (seg.endJunction != null)
                {
                    Gap(end, seg.endJunction.Position, $"{seg.name} -> {seg.endJunction.name}", true);
                    Gap(seg.endJunction.Position, seg.endJunction.left.transform.TransformPoint(seg.endJunction.left.Points[0]), $"{seg.endJunction.name} -> left", true);
                    Gap(seg.endJunction.Position, seg.endJunction.right.transform.TransformPoint(seg.endJunction.right.Points[0]), $"{seg.endJunction.name} -> right", true);
                }
                if (seg.next != null)
                    Gap(end, seg.next.transform.TransformPoint(seg.next.Points[0]), $"{seg.name} -> {seg.next.name}", true);
            }

            // Merge turnouts: their single-track end must meet the next section's first rail.
            foreach (Transform child in route.transform)
            {
                if (!child.name.StartsWith("Merge_")) continue;
                var turnout = child.GetChild(0);
                var outEnd = turnout.TransformPoint(new Vector3(-CentreX, 0f, 0f));
                var section = route.transform.Find("Section_" + (int.Parse(child.name.Substring(6)) + 1));
                Gap(outEnd, section.GetChild(0).TransformPoint(new Vector3(-CentreX, 0f, 0f)), $"{child.name} -> {section.name}", false);
            }

            Debug.Log($"[TrackRoute] joints={joints} worstRailGap={worstPiece * 1000f:0.###}mm ({worstPieceAt}) " +
                      $"worstPathGap={worstPath * 1000f:0.###}mm ({worstPathAt}) worstStraightKink={worstHeading:0.####}deg");
            foreach (var seg in route.GetComponentsInChildren<TrackSegment>())
                Debug.Log($"[TrackRoute] {seg.name}: length={seg.Length:0.00}m points={seg.Points.Count} " +
                          $"start={seg.transform.TransformPoint(seg.Points[0])} end={seg.transform.TransformPoint(seg.Points[seg.Points.Count - 1])} " +
                          $"next={(seg.next ? seg.next.name : "-")} junction={(seg.endJunction ? seg.endJunction.name : "-")}");
            foreach (var j in route.junctions)
                Debug.Log($"[TrackRoute] {j.name}: pos={j.Position} left={j.left.name} right={j.right.name} final={j.isFinal}");
        }

        static T NewChild<T>(Transform parent, string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<T>();
        }

        /// <summary>Places a piece so its pivot end starts at <paramref name="centre"/>, running at <paramref name="heading"/> degrees from +Z toward +X.</summary>
        static GameObject PlaceForward(Transform parent, string prefab, string name, Vector3 centre, float heading)
        {
            float a = heading * Mathf.Deg2Rad;
            var pivot = centre + new Vector3(-CentreX * Mathf.Cos(a), 0f, CentreX * Mathf.Sin(a));
            return Spawn(parent, prefab, name, pivot, Quaternion.Euler(0f, 180f + heading, 0f));
        }

        /// <summary>Places a turnout unrotated so its single-track (pivot) end finishes at <paramref name="outCentre"/>.</summary>
        static GameObject PlaceMerge(Transform parent, string prefab, string name, Vector3 outCentre)
        {
            return Spawn(parent, prefab, name, outCentre + new Vector3(CentreX, 0f, 0f), Quaternion.identity);
        }

        static Vector3 PlaceRun(Transform parent, Vector3 start, float heading, int count)
        {
            float a = heading * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            var c = start;
            for (int i = 0; i < count; i++)
            {
                PlaceForward(parent, "rails_b", $"Rail_{i:00}", c, heading);
                c += dir * LenB;
            }
            return c;
        }

        static GameObject Spawn(Transform parent, string prefab, string name, Vector3 position, Quaternion rotation)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + prefab + ".prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.name = name;
            go.transform.SetPositionAndRotation(position, rotation);
            return go;
        }

        static Vector2[] Straight(float length) => new[] { new Vector2(-CentreX, 0f), new Vector2(-CentreX, -length) };

        static void AddLeg(List<Vector3> pts, Transform piece, Vector2[] leg, bool reversed)
        {
            for (int i = 0; i < leg.Length; i++)
            {
                var p = leg[reversed ? leg.Length - 1 - i : i];
                AddPoint(pts, piece.TransformPoint(new Vector3(p.x, RailTop, p.y)));
            }
        }

        static void AddPoint(List<Vector3> pts, Vector3 p)
        {
            if (pts.Count == 0 || Vector3.Distance(pts[pts.Count - 1], p) > 0.001f) pts.Add(p);
        }

        static Vector3 Ground(Vector3 p) => new Vector3(p.x, 0f, p.z);

        static List<Vector3> ToLocal(Transform t, List<Vector3> world)
        {
            var local = new List<Vector3>(world.Count);
            foreach (var w in world) local.Add(t.InverseTransformPoint(w));
            return local;
        }
    }
}
