using System;
using System.Collections.Generic;
using System.Linq;
using CompanyF.Train;
using UnityEditor;
using UnityEngine;

public static class CameraPathPlanner
{
    const float Radius = 0.12f;
    const int Mask = 1 << 31;
    static readonly Vector3Int[] Directions = { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, new Vector3Int(0,0,1), new Vector3Int(0,0,-1) };

    public static void Configure(Transform train)
    {
        var peek = train.GetComponent<CameraPeek>();
        var so = new SerializedObject(peek);
        var first = (Transform)so.FindProperty("point1").objectReferenceValue;
        var last = (Transform)so.FindProperty("point2").objectReferenceValue;
        var camera = (Camera)so.FindProperty("peekCamera").objectReferenceValue;
        var temporary = new List<GameObject>();
        bool oldBackfaces = Physics.queriesHitBackfaces;
        try
        {
            foreach (var mesh in train.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mesh.sharedMesh == null || !mesh.gameObject.activeInHierarchy) continue;
                var go = new GameObject("_CameraPathGeometry") { hideFlags = HideFlags.HideAndDontSave, layer = 31 };
                go.transform.SetParent(mesh.transform, false);
                go.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
                temporary.Add(go);
            }
            Physics.queriesHitBackfaces = true;
            Physics.SyncTransforms();
            Vector3 start = first.localPosition, end = last.localPosition;
            Debug.Log($"[Camera Path] start={start:F3}, end={end:F3}, old duration={peek.moveTime}, geometry={temporary.Count}");
            bool Clear(Vector3 point) => !Physics.CheckSphere(train.TransformPoint(point), Radius, Mask, QueryTriggerInteraction.Ignore);
            bool Segment(Vector3 a, Vector3 b)
            {
                if (!Clear(a) || !Clear(b)) return false;
                var from = train.TransformPoint(a); var delta = train.TransformPoint(b) - from;
                return delta.sqrMagnitude < 1e-8f || !Physics.SphereCast(from, Radius, delta.normalized, out _, delta.magnitude, Mask, QueryTriggerInteraction.Ignore);
            }
            if (!Clear(start) || !Clear(end)) throw new InvalidOperationException("A camera endpoint intersects model geometry; cannot preserve framing safely.");
            var path = new List<Vector3> { start };
            if (!Segment(start, end))
            {
                const float step = 0.18f;
                var minimum = Vector3.Min(start, end) - new Vector3(1.8f, 1.5f, 1.8f);
                var maximum = Vector3.Max(start, end) + new Vector3(1.8f, 2f, 1.8f);
                Vector3 Position(Vector3Int key) => start + (Vector3)key * step;
                float Estimate(Vector3Int key) => Vector3.Distance(Position(key), end);
                var scores = new Dictionary<Vector3Int, float> { [Vector3Int.zero] = 0f };
                var previous = new Dictionary<Vector3Int, Vector3Int>();
                var comparer = Comparer<(float score, int serial, Vector3Int key)>.Create((a, b) =>
                {
                    int order = a.score.CompareTo(b.score);
                    return order != 0 ? order : a.serial.CompareTo(b.serial);
                });
                var open = new SortedSet<(float score, int serial, Vector3Int key)>(comparer);
                int serial = 0;
                open.Add((Estimate(Vector3Int.zero), serial++, Vector3Int.zero));
                var closed = new HashSet<Vector3Int>();
                Vector3Int reached = default;
                bool found = false;
                while (open.Count > 0 && closed.Count < 40000)
                {
                    var item = open.Min; open.Remove(item);
                    var key = item.key;
                    if (!closed.Add(key)) continue;
                    var p = Position(key);
                    if (Vector3.Distance(p, end) < 0.5f && Segment(p, end)) { reached = key; found = true; break; }
                    foreach (var direction in Directions)
                    {
                        var neighbour = key + direction;
                        if (closed.Contains(neighbour)) continue;
                        var q = Position(neighbour);
                        if (q.x < minimum.x || q.y < minimum.y || q.z < minimum.z || q.x > maximum.x || q.y > maximum.y || q.z > maximum.z) continue;
                        float cost = scores[key] + step;
                        if (scores.TryGetValue(neighbour, out float old) && cost >= old) continue;
                        if (!Segment(p, q)) continue;
                        scores[neighbour] = cost; previous[neighbour] = key;
                        open.Add((cost + Estimate(neighbour), serial++, neighbour));
                    }
                }
                if (!found) throw new InvalidOperationException("No collision-free camera route found.");
                var reversed = new List<Vector3> { end, Position(reached) };
                while (reached != Vector3Int.zero) { reached = previous[reached]; reversed.Add(Position(reached)); }
                reversed.Reverse();
                path.Clear();
                for (int i = 0; i < reversed.Count - 1;)
                {
                    path.Add(reversed[i]);
                    int next = reversed.Count - 1;
                    while (next > i + 1 && !Segment(reversed[i], reversed[next])) next--;
                    i = next;
                }
            }
            path.Add(end);
            var points = so.FindProperty("avoidancePath");
            points.arraySize = path.Count;
            for (int i = 0; i < path.Count; i++) points.GetArrayElementAtIndex(i).vector3Value = path[i];
            so.FindProperty("moveTime").floatValue = 0.7f;
            so.ApplyModifiedPropertiesWithoutUndo();
            camera.nearClipPlane = 0.05f;
            for (int i = 1; i <= 500; i++)
                if (!Segment(peek.PositionAt((i - 1) / 500f), peek.PositionAt(i / 500f)))
                    throw new InvalidOperationException("Camera route failed swept-sphere validation.");
            EditorUtility.SetDirty(peek); EditorUtility.SetDirty(camera);
            Debug.Log($"[Camera Path] PASS: {path.Count} waypoints, 0.7 seconds, 500 swept-sphere segments clear at radius {Radius}. " + string.Join(" -> ", path.Select(p => p.ToString("F2"))));
        }
        finally
        {
            Physics.queriesHitBackfaces = oldBackfaces;
            foreach (var go in temporary) UnityEngine.Object.DestroyImmediate(go);
            Physics.SyncTransforms();
        }
    }
}
