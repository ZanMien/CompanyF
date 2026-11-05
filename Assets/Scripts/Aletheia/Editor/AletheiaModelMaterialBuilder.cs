using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CompanyF.Aletheia.EditorTools
{
    /// <summary>
    /// Builds URP Lit materials for model.dae from its PBR textures and remaps the model to them.
    /// Roughness is not used directly: each metallic + (1 - roughness) pair is baked into a new
    /// metallic/smoothness PNG (R = metallic, A = smoothness), as URP Lit expects.
    /// Source images and the .dae are only read; just the texture import settings change.
    /// </summary>
    public static class AletheiaModelMaterialBuilder
    {
        const string ModelPath = "Assets/Aletheia/Models/model/model.dae";
        const string TextureDir = "Assets/Aletheia/Models/model/textures";
        const string OutputDir = "Assets/Aletheia/Models/model/Materials";
        const string PreviewPath = "Temp/AletheiaModelPreview.png";

        static readonly string[] MaterialNames = { "Bolts_1001", "Box_1001", "Gears_1001", "Lever_handle_1001", "Lever_rod_1001" };

        [MenuItem("Tools/Aletheia/Build Model Materials")]
        public static void Build()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { Debug.LogError("[ModelMaterials] URP Lit shader not found."); return; }

            if (!AssetDatabase.IsValidFolder(OutputDir))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(OutputDir).Replace('\\', '/'), Path.GetFileName(OutputDir));

            var materials = new Dictionary<string, Material>();
            foreach (var name in MaterialNames)
            {
                ConfigureSourceTextures(name);
                string maskPath = BakeMetallicSmoothness(name);
                materials[name] = BuildMaterial(name, shader, maskPath);
            }
            AssetDatabase.SaveAssets();

            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            foreach (var pair in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            importer.SaveAndReimport();

            Report();
        }

        static void ConfigureSourceTextures(string name)
        {
            var normal = (TextureImporter)AssetImporter.GetAtPath($"{TextureDir}/{name}_normal.png");
            if (normal.textureType != TextureImporterType.NormalMap)
            {
                normal.textureType = TextureImporterType.NormalMap;
                normal.SaveAndReimport();
            }

            var ao = (TextureImporter)AssetImporter.GetAtPath($"{TextureDir}/{name}_AO.jpg");
            if (ao.sRGBTexture)
            {
                ao.sRGBTexture = false;
                ao.SaveAndReimport();
            }
        }

        /// <summary>Combines the source images' stored values directly, so import settings can't alter them.</summary>
        static string BakeMetallicSmoothness(string name)
        {
            var metallic = LoadRaw($"{TextureDir}/{name}_metallic.jpg");
            var roughness = LoadRaw($"{TextureDir}/{name}_roughness.jpg");
            var m = metallic.GetPixels32();
            var r = roughness.GetPixels32();
            var result = new Color32[m.Length];
            for (int i = 0; i < m.Length; i++)
                result[i] = new Color32(m[i].r, m[i].r, m[i].r, (byte)(255 - r[i].r));

            var combined = new Texture2D(metallic.width, metallic.height, TextureFormat.RGBA32, false, true);
            combined.SetPixels32(result);
            combined.Apply();
            string path = $"{OutputDir}/{name}_MetallicSmoothness.png";
            File.WriteAllBytes(path, combined.EncodeToPNG());
            Object.DestroyImmediate(metallic);
            Object.DestroyImmediate(roughness);
            Object.DestroyImmediate(combined);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.SaveAndReimport();
            return path;
        }

        static Texture2D LoadRaw(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            texture.LoadImage(File.ReadAllBytes(path));
            return texture;
        }

        static Material BuildMaterial(string name, Shader shader, string maskPath)
        {
            string path = $"{OutputDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;

            var albedo = Load($"{TextureDir}/{name}_albedo.jpg");
            material.SetTexture("_BaseMap", albedo);
            material.mainTexture = albedo;
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", Load($"{TextureDir}/{name}_normal.png"));
            material.SetFloat("_BumpScale", 1f);
            material.SetTexture("_MetallicGlossMap", Load(maskPath));
            material.SetFloat("_WorkflowMode", 1f);          // metallic
            material.SetFloat("_SmoothnessTextureChannel", 0f);  // metallic map alpha
            material.SetFloat("_Smoothness", 1f);            // multiplies the map's alpha
            material.SetTexture("_OcclusionMap", Load($"{TextureDir}/{name}_AO.jpg"));
            material.SetFloat("_OcclusionStrength", 1f);

            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.EnableKeyword("_OCCLUSIONMAP");
            material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            EditorUtility.SetDirty(material);
            return material;
        }

        static Texture Load(string path) => AssetDatabase.LoadAssetAtPath<Texture>(path);

        static void Report()
        {
            var log = new StringBuilder("[ModelMaterials] done\n");
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            foreach (var pair in importer.GetExternalObjectMap())
                log.AppendLine($"remap {pair.Key.name} -> {AssetDatabase.GetAssetPath(pair.Value)}");

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var used = new HashSet<string>();
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                foreach (var m in renderer.sharedMaterials)
                    used.Add(m == null ? "NULL" : $"{m.name} [{m.shader.name}]");
            log.AppendLine("model uses: " + string.Join(", ", used));
            Debug.Log(log.ToString());
        }

        /// <summary>Renders the model in an isolated preview scene to Temp/; the open scene is not touched.</summary>
        [MenuItem("Tools/Aletheia/Render Model Preview")]
        public static void RenderPreview() => RenderPreview(null);

        /// <summary>Same preview with the handle swapped to the blue material, on the preview instance only.</summary>
        [MenuItem("Tools/Aletheia/Render Blue Handle Preview")]
        public static void RenderBlueHandlePreview() => RenderPreview($"{OutputDir}/Lever_handle_blue.mat");

        static void RenderPreview(string handleMaterialPath)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(800, 800, 24, RenderTextureFormat.ARGB32);
            GameObject cameraGo = null;
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), scene);
                var bounds = new Bounds(model.transform.position, Vector3.zero);
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                {
                    bounds.Encapsulate(renderer.bounds);
                    if (handleMaterialPath == null) continue;
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                        if (materials[i] != null && materials[i].name.StartsWith("Lever_handle"))
                            materials[i] = AssetDatabase.LoadAssetAtPath<Material>(handleMaterialPath);
                    renderer.sharedMaterials = materials;
                }

                var lightGo = new GameObject("Light", typeof(Light));
                SceneManager_Move(lightGo, scene);
                var light = lightGo.GetComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.5f;
                lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

                cameraGo = new GameObject("Camera", typeof(Camera));
                SceneManager_Move(cameraGo, scene);
                var camera = cameraGo.GetComponent<Camera>();
                camera.scene = scene;
                camera.targetTexture = target;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.45f, 0.5f, 0.55f);
                camera.nearClipPlane = bounds.size.magnitude * 0.01f;
                camera.farClipPlane = bounds.size.magnitude * 10f;
                float distance = bounds.extents.magnitude * 1.6f;

                var output = new Texture2D(1600, 800, TextureFormat.RGB24, false);
                RenderSide(camera, bounds, distance, new Vector3(25f, -35f, 0f), target, output, 0);
                RenderSide(camera, bounds, distance, new Vector3(25f, 145f, 0f), target, output, 800);
                File.WriteAllBytes(PreviewPath, output.EncodeToPNG());
                Object.DestroyImmediate(output);
                Debug.Log($"[ModelMaterials] preview saved: {Path.GetFullPath(PreviewPath)} bounds={bounds.size}");
            }
            finally
            {
                RenderTexture.active = null;
                EditorSceneManager.ClosePreviewScene(scene);
                Object.DestroyImmediate(target);
            }
        }

        static void SceneManager_Move(GameObject go, UnityEngine.SceneManagement.Scene scene) =>
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);

        static void RenderSide(Camera camera, Bounds bounds, float distance, Vector3 euler, RenderTexture target, Texture2D output, int x)
        {
            camera.transform.rotation = Quaternion.Euler(euler);
            camera.transform.position = bounds.center - camera.transform.forward * distance;
            camera.Render();
            camera.Render();  // the first render in a fresh preview scene can come out empty
            RenderTexture.active = target;
            output.ReadPixels(new Rect(0, 0, 800, 800), x, 0);
            output.Apply();
        }
    }
}
