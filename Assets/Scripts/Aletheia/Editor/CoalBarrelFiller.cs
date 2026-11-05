using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CompanyF.Aletheia.EditorTools
{
    /// <summary>
    /// Fills Barrel_Empty1 with copies of coal/Coal01 under a "Coal Fill" child (replaced on each run).
    /// Coal is dropped in one piece at a time with a random turn and size: each piece settles at the lowest height
    /// where its real vertices stay inside the barrel wall and it does not sink too far into pieces already placed.
    /// The barrel mesh is not read-write enabled, so its inner profile is a table measured from Blacksmith Tools 5.fbx.
    /// </summary>
    public static class CoalBarrelFiller
    {
        const string BarrelPath = "Barrel_Empty1";
        const string BarrelMeshPath = "Barrel_Empty1/Empty_Barrel1/Barrel1/Mesh17";
        const string CoalPath = "coal/Coal01";
        const string CoalObjPath = "Assets/Aletheia/Models/source/coal.obj";
        const string FillName = "Coal Fill";
        const string PreviewPath = "Temp/CoalFillPreview.png";

        const int Seed = 20261105;
        const int MaxPieces = 60;
        const int CandidatesPerPiece = 40;
        const int FailuresToStop = 3;
        const float MinScale = 0.6f, MaxScale = 0.95f;   // relative to Coal01's size
        const float WallClearance = 0.01f;               // barrel-mesh units
        const float HeapAboveRim = 0.05f;                // barrel-mesh units
        const float SoftRadius = 0.72f;                  // collision sphere, fraction of the piece's mean half size

        // Inner wall of the staves: height -> radius, barrel-mesh units (metres; rim at 0.89).
        static readonly Vector2[] InnerProfile =
        {
            new Vector2(0f, 0.26f), new Vector2(0.19f, 0.289f), new Vector2(0.36f, 0.3183f),
            new Vector2(0.53f, 0.3183f), new Vector2(0.70f, 0.289f), new Vector2(0.89f, 0.26f),
        };
        const float RimHeight = 0.89f;

        struct Placed
        {
            public Vector3 position;
            public float radius;
        }

        [MenuItem("Tools/Aletheia/Fill Barrel With Coal")]
        public static void Fill()
        {
            var barrel = GameObject.Find(BarrelPath);
            var barrelMesh = GameObject.Find(BarrelMeshPath);
            var coal = GameObject.Find(CoalPath);
            if (barrel == null || barrelMesh == null || coal == null)
            {
                Debug.LogError("[CoalFill] Barrel_Empty1 or coal/Coal01 not found in the scene.");
                return;
            }

            var mesh = coal.GetComponent<MeshFilter>().sharedMesh;
            var vertices = LoadCoalVertices(mesh);
            if (vertices == null) return;

            var old = barrel.transform.Find(FillName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var fill = new GameObject(FillName);
            Undo.RegisterCreatedObjectUndo(fill, "Fill Barrel With Coal");
            fill.transform.SetParent(barrel.transform, false);

            // Barrel frame: origin on the axis at the barrel's base, in barrel-mesh units.
            var meshRenderer = barrelMesh.GetComponent<MeshRenderer>();
            var meshBounds = barrelMesh.GetComponent<MeshFilter>().sharedMesh.bounds;
            var frame = barrelMesh.transform;
            var axisLocal = new Vector3(meshBounds.center.x, meshBounds.min.y, meshBounds.center.z);
            float frameScale = frame.lossyScale.x;
            var rng = new System.Random(Seed);
            float coalScale = coal.transform.lossyScale.x;

            var placed = new List<Placed>();
            int failures = 0;
            float worstWall = float.MaxValue, highest = 0f;
            while (placed.Count < MaxPieces && failures < FailuresToStop)
            {
                bool found = false;
                float bestHeight = float.MaxValue;
                Vector3 bestPosition = Vector3.zero;
                Quaternion bestRotation = Quaternion.identity;
                float bestScale = 1f, bestRadius = 0f;

                for (int c = 0; c < CandidatesPerPiece; c++)
                {
                    var rotation = RandomRotation(rng);
                    float scale = Mathf.Lerp(MinScale, MaxScale, (float)rng.NextDouble()) * coalScale / frameScale;
                    var points = Transformed(vertices, rotation, scale);
                    float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float distance = Mathf.Sqrt((float)rng.NextDouble()) * 0.32f;
                    var xz = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                    float radius = SoftRadius * MeanHalfSize(vertices) * scale;

                    if (!Settle(points, xz, radius, placed, out float pivotY, out float top)) continue;
                    if (top < bestHeight)
                    {
                        found = true;
                        bestHeight = top;
                        bestPosition = new Vector3(xz.x, pivotY, xz.y);
                        bestRotation = rotation;
                        bestScale = scale;
                        bestRadius = radius;
                    }
                }

                if (!found) { failures++; continue; }
                failures = 0;

                placed.Add(new Placed { position = bestPosition + SphereOffset(vertices, bestRotation, bestScale), radius = bestRadius });
                var go = new GameObject($"Coal_{placed.Count:00}", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(fill.transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterials = coal.GetComponent<MeshRenderer>().sharedMaterials;
                go.transform.SetPositionAndRotation(
                    frame.TransformPoint(axisLocal + bestPosition),
                    frame.rotation * bestRotation);
                float worldScale = bestScale * frameScale;
                var parentScale = fill.transform.lossyScale;
                go.transform.localScale = new Vector3(worldScale / parentScale.x, worldScale / parentScale.y, worldScale / parentScale.z);

                foreach (var p in Transformed(vertices, bestRotation, bestScale))
                {
                    float y = p.y + bestPosition.y;
                    float r = new Vector2(p.x + bestPosition.x, p.z + bestPosition.z).magnitude;
                    worstWall = Mathf.Min(worstWall, InnerRadius(y) - r);
                    highest = Mathf.Max(highest, y);
                }
            }

            EditorSceneManager.MarkSceneDirty(barrel.scene);
            Debug.Log($"[CoalFill] pieces={placed.Count} highest={highest:0.000}/{RimHeight} (mesh units) minWallGap={worstWall:0.000} barrelRenderer={meshRenderer.name}");
        }

        /// <summary>Lowest pivot height at this spot where the piece clears the floor, the wall and placed pieces.</summary>
        static bool Settle(List<Vector3> points, Vector2 xz, float radius, List<Placed> placed, out float pivotY, out float top)
        {
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in points)
            {
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }

            const float Step = 0.01f;
            float limit = RimHeight + HeapAboveRim;
            for (pivotY = 0.005f - minY; pivotY + maxY <= limit; pivotY += Step)
            {
                top = pivotY + maxY;
                if (!InsideWall(points, xz, pivotY)) continue;

                var center = new Vector3(xz.x, pivotY + (minY + maxY) * 0.5f, xz.y);
                bool clear = true;
                foreach (var other in placed)
                {
                    if ((center - other.position).sqrMagnitude < (radius + other.radius) * (radius + other.radius))
                    {
                        clear = false;
                        break;
                    }
                }
                if (clear) return true;
            }
            top = float.MaxValue;
            return false;
        }

        static bool InsideWall(List<Vector3> points, Vector2 xz, float pivotY)
        {
            foreach (var p in points)
            {
                float r = new Vector2(p.x + xz.x, p.z + xz.y).magnitude;
                if (r > InnerRadius(p.y + pivotY) - WallClearance) return false;
            }
            return true;
        }

        static float InnerRadius(float height)
        {
            if (height >= RimHeight) return InnerProfile[InnerProfile.Length - 1].y;
            for (int i = 1; i < InnerProfile.Length; i++)
                if (height <= InnerProfile[i].x)
                    return Mathf.Lerp(InnerProfile[i - 1].y, InnerProfile[i].y, Mathf.InverseLerp(InnerProfile[i - 1].x, InnerProfile[i].x, height));
            return InnerProfile[0].y;
        }

        /// <summary>Vertices of coal.obj in Unity's mesh space (mirrored X), thinned out; null if they don't match the mesh.</summary>
        static List<Vector3> LoadCoalVertices(Mesh mesh)
        {
            var result = new List<Vector3>();
            var bounds = new Bounds();
            bool first = true;
            int index = 0;
            foreach (var line in File.ReadLines(CoalObjPath))
            {
                if (!line.StartsWith("v ")) continue;
                var parts = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                var v = new Vector3(
                    -float.Parse(parts[1], CultureInfo.InvariantCulture),
                    float.Parse(parts[2], CultureInfo.InvariantCulture),
                    float.Parse(parts[3], CultureInfo.InvariantCulture));
                if (first) { bounds = new Bounds(v, Vector3.zero); first = false; }
                else bounds.Encapsulate(v);
                if (index++ % 4 == 0) result.Add(v);
            }

            float scale = mesh.bounds.size.x / Mathf.Max(bounds.size.x, 1e-6f);
            for (int i = 0; i < result.Count; i++) result[i] *= scale;
            bounds.center *= scale;
            bounds.size *= scale;
            if (Vector3.Distance(bounds.center, mesh.bounds.center) > 0.02f || Vector3.Distance(bounds.size, mesh.bounds.size) > 0.02f)
            {
                Debug.LogError($"[CoalFill] coal.obj ({bounds.center}, {bounds.size}) does not match the mesh ({mesh.bounds.center}, {mesh.bounds.size}).");
                return null;
            }
            return result;
        }

        static List<Vector3> Transformed(List<Vector3> vertices, Quaternion rotation, float scale)
        {
            var points = new List<Vector3>(vertices.Count);
            foreach (var v in vertices) points.Add(rotation * (v * scale));
            return points;
        }

        static float MeanHalfSize(List<Vector3> vertices)
        {
            var b = new Bounds(vertices[0], Vector3.zero);
            foreach (var v in vertices) b.Encapsulate(v);
            return (b.extents.x + b.extents.y + b.extents.z) / 3f;
        }

        static Vector3 SphereOffset(List<Vector3> vertices, Quaternion rotation, float scale)
        {
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in Transformed(vertices, rotation, scale))
            {
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }
            return new Vector3(0f, (minY + maxY) * 0.5f, 0f);
        }

        static Quaternion RandomRotation(System.Random rng) => Quaternion.Euler(
            (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f);

        /// <summary>Three views to Temp/: the barrel from the side, from above, and the coal alone. Nothing stays in the scene.</summary>
        [MenuItem("Tools/Aletheia/Render Coal Fill Preview")]
        public static void RenderPreview()
        {
            var barrel = GameObject.Find(BarrelPath);
            var fill = barrel != null ? barrel.transform.Find(FillName) : null;
            if (fill == null) { Debug.LogError("[CoalFill] Run Fill Barrel With Coal first."); return; }

            var barrelRenderers = new List<Renderer>();
            foreach (var r in barrel.GetComponentsInChildren<Renderer>())
                if (!r.transform.IsChildOf(fill)) barrelRenderers.Add(r);
            var bounds = barrelRenderers[0].bounds;
            foreach (var r in barrelRenderers) bounds.Encapsulate(r.bounds);

            const int Cell = 600;
            var target = new RenderTexture(Cell, Cell, 24, RenderTextureFormat.ARGB32);
            var sheet = new Texture2D(Cell * 3, Cell, TextureFormat.RGB24, false);
            var cameraGo = new GameObject("_CoalPreviewCamera", typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var camera = cameraGo.GetComponent<Camera>();
                camera.targetTexture = target;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.45f, 0.5f, 0.55f);
                camera.nearClipPlane = 0.02f;
                camera.fieldOfView = 35f;
                float distance = bounds.extents.magnitude * 3.2f;

                Shoot(camera, bounds, distance, new Vector3(25f, 210f, 0f), target, sheet, 0);
                Shoot(camera, bounds, distance, new Vector3(80f, 210f, 0f), target, sheet, Cell);
                foreach (var r in barrelRenderers) r.enabled = false;
                try { Shoot(camera, bounds, distance, new Vector3(15f, 210f, 0f), target, sheet, Cell * 2); }
                finally { foreach (var r in barrelRenderers) r.enabled = true; }

                sheet.Apply();
                File.WriteAllBytes(PreviewPath, sheet.EncodeToPNG());
                Debug.Log($"[CoalFill] preview saved: {Path.GetFullPath(PreviewPath)}");
            }
            finally
            {
                RenderTexture.active = null;
                Object.DestroyImmediate(cameraGo);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(sheet);
            }
        }

        static void Shoot(Camera camera, Bounds bounds, float distance, Vector3 euler, RenderTexture target, Texture2D sheet, int x)
        {
            camera.transform.rotation = Quaternion.Euler(euler);
            camera.transform.position = bounds.center - camera.transform.forward * distance;
            camera.Render();
            camera.Render();
            RenderTexture.active = target;
            sheet.ReadPixels(new Rect(0, 0, target.width, target.height), x, 0);
        }
    }
}
