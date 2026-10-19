using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CompanyF.EditorTools
{
    public sealed class RailValleyTerrainGenerator : EditorWindow
    {
        private const string GeneratedFolder = "Assets/Generated/Terrain";
        private const string GeneratedAsset = GeneratedFolder + "/RailValleyTerrainData.asset";
        private const string OriginalTerrainDataAsset = "Assets/Train station - Western/New Terrain.asset";

        [SerializeField] private Terrain target;
        [SerializeField] private float terrainLength = 2400f;
        [SerializeField] private float routeCenterWorldX = -0.8f;
        [SerializeField] private float floorWorldY = -1f;
        [SerializeField] private float corridorHalfWidth = 30f;
        [SerializeField] private float wallTransitionWidth = 170f;
        [SerializeField] private float wallHeight = 210f;
        [SerializeField] private int seed = 2718;

        [MenuItem("Tools/CompanyF/Rail Valley Terrain")]
        private static void OpenWindow()
        {
            var window = GetWindow<RailValleyTerrainGenerator>("Rail Valley Terrain");
            window.minSize = new Vector2(360f, 300f);
            window.TryFindTerrain();
        }

        [MenuItem("Tools/CompanyF/Generate Rail Valley Terrain (Default)")]
        public static void GenerateDefault()
        {
            var terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>();
            if (terrain == null)
            {
                Debug.LogError("[Rail Valley] No Terrain was found in the active scene.");
                return;
            }

            Build(terrain, 2400f, -0.8f, -1f, 30f, 170f, 210f, 2718);
        }

        [MenuItem("Tools/CompanyF/Restore Original Terrain Surface")]
        public static void RestoreOriginalSurfaceDefault()
        {
            var terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>();
            if (terrain == null)
            {
                Debug.LogError("[Rail Valley] No Terrain was found in the active scene.");
                return;
            }

            RestoreOriginalSurface(terrain.terrainData);
            terrain.Flush();
            EditorUtility.SetDirty(terrain);
            EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
            AssetDatabase.SaveAssets();
        }

        private void OnEnable()
        {
            TryFindTerrain();
        }

        private void TryFindTerrain()
        {
            if (target == null)
                target = UnityEngine.Object.FindFirstObjectByType<Terrain>();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Straight Rail Valley", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Keeps a flat straight corridor around the rail line and raises natural canyon walls on both sides. Change Length and generate again whenever the track is extended.",
                MessageType.Info);

            target = (Terrain)EditorGUILayout.ObjectField("Terrain", target, typeof(Terrain), true);
            terrainLength = EditorGUILayout.FloatField("Length (Z)", terrainLength);
            routeCenterWorldX = EditorGUILayout.FloatField("Rail Center World X", routeCenterWorldX);
            floorWorldY = EditorGUILayout.FloatField("Valley Floor World Y", floorWorldY);
            corridorHalfWidth = EditorGUILayout.FloatField("Flat Half Width", corridorHalfWidth);
            wallTransitionWidth = EditorGUILayout.FloatField("Wall Transition Width", wallTransitionWidth);
            wallHeight = EditorGUILayout.FloatField("Wall Height", wallHeight);
            seed = EditorGUILayout.IntField("Seed", seed);

            GUILayout.Space(10f);
            using (new EditorGUI.DisabledScope(target == null))
            {
                if (GUILayout.Button("Generate / Update Valley", GUILayout.Height(36f)))
                {
                    Build(target, terrainLength, routeCenterWorldX, floorWorldY,
                        corridorHalfWidth, wallTransitionWidth, wallHeight, seed);
                }

                if (GUILayout.Button("Restore Original Terrain Surface"))
                {
                    RestoreOriginalSurface(target.terrainData);
                    target.Flush();
                    EditorUtility.SetDirty(target);
                    EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
                    AssetDatabase.SaveAssets();
                }
            }
        }

        private static void RestoreOriginalSurface(TerrainData destination)
        {
            TerrainData source = AssetDatabase.LoadAssetAtPath<TerrainData>(OriginalTerrainDataAsset);
            if (source == null)
                throw new System.IO.FileNotFoundException("Original TerrainData was not found.", OriginalTerrainDataAsset);
            if (destination == null)
                throw new System.InvalidOperationException("Target Terrain has no TerrainData.");

            Undo.RegisterCompleteObjectUndo(destination, "Restore Original Terrain Surface");

            destination.terrainLayers = source.terrainLayers;
            destination.alphamapResolution = source.alphamapResolution;
            destination.baseMapResolution = source.baseMapResolution;

            float[,,] originalWeights = source.GetAlphamaps(
                0, 0, source.alphamapWidth, source.alphamapHeight);
            destination.SetAlphamaps(0, 0, originalWeights);

            EditorUtility.SetDirty(destination);
            Debug.Log($"[Rail Valley] Restored {source.terrainLayers.Length} original Terrain Layers and painted surface weights.");
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Generated"))
                AssetDatabase.CreateFolder("Assets", "Generated");
            if (!AssetDatabase.IsValidFolder(GeneratedFolder))
                AssetDatabase.CreateFolder("Assets/Generated", "Terrain");
        }

        private static TerrainData EnsureSceneOwnedTerrainData(Terrain terrain)
        {
            var current = terrain.terrainData;
            if (current == null)
                throw new System.InvalidOperationException("Target Terrain has no TerrainData.");

            string currentPath = AssetDatabase.GetAssetPath(current).Replace('\\', '/');
            if (currentPath.StartsWith(GeneratedFolder + "/RailValleyTerrainData"))
                return current;

            EnsureFolder();
            var copy = Instantiate(current);
            copy.name = "RailValleyTerrainData";
            string path = AssetDatabase.GenerateUniqueAssetPath(GeneratedAsset);
            AssetDatabase.CreateAsset(copy, path);

            Undo.RecordObject(terrain, "Assign Rail Valley TerrainData");
            terrain.terrainData = copy;

            var collider = terrain.GetComponent<TerrainCollider>();
            if (collider != null)
            {
                Undo.RecordObject(collider, "Assign Rail Valley TerrainData");
                collider.terrainData = copy;
                EditorUtility.SetDirty(collider);
            }

            EditorUtility.SetDirty(terrain);
            return copy;
        }

        private static void Build(
            Terrain terrain,
            float length,
            float centerWorldX,
            float floorY,
            float flatHalfWidth,
            float transitionWidth,
            float mountainHeight,
            int noiseSeed)
        {
            if (terrain == null)
                return;

            length = Mathf.Max(200f, length);
            flatHalfWidth = Mathf.Max(5f, flatHalfWidth);
            transitionWidth = Mathf.Max(20f, transitionWidth);
            mountainHeight = Mathf.Max(10f, mountainHeight);

            TerrainData data = EnsureSceneOwnedTerrainData(terrain);
            Undo.RegisterCompleteObjectUndo(data, "Generate Rail Valley Terrain");

            Vector3 size = data.size;
            size.z = length;
            data.size = size;
            RestoreOriginalSurface(data);

            int resolution = data.heightmapResolution;
            float[,] heights = new float[resolution, resolution];
            Vector3 terrainPosition = terrain.transform.position;
            float floorNormalized = Mathf.Clamp01((floorY - terrainPosition.y) / size.y);
            float seedOffset = noiseSeed * 0.0137f;

            for (int z = 0; z < resolution; z++)
            {
                float nz = z / (float)(resolution - 1);
                float worldZ = terrainPosition.z + nz * size.z;

                for (int x = 0; x < resolution; x++)
                {
                    float nx = x / (float)(resolution - 1);
                    float worldX = terrainPosition.x + nx * size.x;
                    float distanceFromRail = Mathf.Abs(worldX - centerWorldX);
                    float wallT = Mathf.Clamp01((distanceFromRail - flatHalfWidth) / transitionWidth);
                    wallT = wallT * wallT * (3f - 2f * wallT);

                    float broadNoise = Mathf.PerlinNoise(
                        worldX * 0.0045f + seedOffset,
                        worldZ * 0.0032f + seedOffset * 0.7f);
                    float detailNoise = Mathf.PerlinNoise(
                        worldX * 0.014f + seedOffset * 1.9f,
                        worldZ * 0.011f + seedOffset * 1.3f);
                    float ridgeVariation = (broadNoise - 0.5f) * 70f + (detailNoise - 0.5f) * 22f;
                    float sideWorldY = floorY + mountainHeight + ridgeVariation;
                    float sideNormalized = Mathf.Clamp01((sideWorldY - terrainPosition.y) / size.y);

                    heights[z, x] = Mathf.Lerp(floorNormalized, sideNormalized, wallT);
                }
            }

            data.SetHeights(0, 0, heights);
            data.SyncHeightmap();
            terrain.Flush();

            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(terrain);
            EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Rail Valley] Generated {size.x:0} x {size.z:0} terrain. Flat corridor: +/-{flatHalfWidth:0.#}m around world X {centerWorldX:0.##}.");
        }
    }
}
