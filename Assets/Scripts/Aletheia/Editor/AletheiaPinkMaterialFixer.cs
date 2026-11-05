using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CompanyF.Aletheia.EditorTools
{
    /// <summary>
    /// Converts the pink materials under Assets/Aletheia/Models to URP Lit, in place (file names and GUIDs stay).
    ///  - Blacksmith Tools: HDRP Lit materials whose texture links are also broken. Textures are re-linked by file
    ///    name from the material's own folder. The HDRP mask map (R metallic, G AO, A smoothness) serves URP's
    ///    metallic map and occlusion map directly.
    ///  - model/textures: unused Built-in copies, set to match their URP namesakes in model/Materials.
    /// Source images and model files are only read; mask map import settings change (sRGB off).
    /// </summary>
    public static class AletheiaPinkMaterialFixer
    {
        const string LitShader = "Universal Render Pipeline/Lit";
        const string Prefix = "Black Smith Tools 5_";
        const string PreviewPath = "Temp/AletheiaPinkFixPreview.png";

        static readonly string[] BlacksmithFolders =
        {
            "Assets/Aletheia/Models/Blacksmith tools 5 textures/1024",
            "Assets/Aletheia/Models/Barrel - Buckets Textures/Blacksmith tools 5 textures/1024",
        };

        static readonly string[] BlacksmithParts =
            { "Barrel_Lid_Wood", "Barrel_Metal", "Barrel_Wood", "Bucket_Metal", "Bucket_Wood", "Coalpile", "Metal", "Nails", "Water" };

        // These masks store metallic = 1 everywhere, an export default rather than real metal, so the metallic map is
        // skipped; the value is the mask's average smoothness.
        static readonly Dictionary<string, float> NonMetalSmoothness = new Dictionary<string, float> { { "Coalpile", 0.44f }, { "Water", 0.90f } };

        const float WaterAlpha = 0.46666667f;

        static readonly string[] OldModelMaterials = { "Bolts_1001", "Box_1001", "Gears_1001", "Lever_handle_1001", "Lever_rod_1001" };

        [MenuItem("Tools/Aletheia/Fix Pink Materials")]
        public static void Fix()
        {
            var lit = Shader.Find(LitShader);
            if (lit == null) { Debug.LogError("[PinkFix] URP Lit shader not found."); return; }

            int converted = 0;
            foreach (var folder in BlacksmithFolders)
                foreach (var part in BlacksmithParts)
                    if (ConvertBlacksmith(folder, part, lit)) converted++;

            foreach (var name in OldModelMaterials)
            {
                var old = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Aletheia/Models/model/textures/{name}.mat");
                var source = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Aletheia/Models/model/Materials/{name}.mat");
                if (old == null || source == null) { Debug.LogError($"[PinkFix] Missing material pair for {name}."); continue; }
                EditorUtility.CopySerialized(source, old);
                old.name = name;
                EditorUtility.SetDirty(old);
                converted++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[PinkFix] converted {converted} materials");
        }

        static bool ConvertBlacksmith(string folder, string part, Shader lit)
        {
            string stem = $"{folder}/{Prefix}{part}";
            var material = AssetDatabase.LoadAssetAtPath<Material>(stem + ".mat");
            var baseMap = AssetDatabase.LoadAssetAtPath<Texture>(stem + "_BaseMap.png");
            var normal = AssetDatabase.LoadAssetAtPath<Texture>(stem + "_Normal.png");
            string maskPath = stem + "_MaskMap.png";
            if (material == null || baseMap == null || normal == null || !File.Exists(maskPath))
            {
                Debug.LogError($"[PinkFix] Missing files for {stem}.");
                return false;
            }

            var maskImporter = (TextureImporter)AssetImporter.GetAtPath(maskPath);
            if (maskImporter.sRGBTexture)
            {
                maskImporter.sRGBTexture = false;
                maskImporter.SaveAndReimport();
            }
            var mask = AssetDatabase.LoadAssetAtPath<Texture>(maskPath);

            // Start from a clean URP Lit so no HDRP keywords, passes or broken links remain.
            string assetName = material.name;
            var fresh = new Material(lit);
            EditorUtility.CopySerialized(fresh, material);
            Object.DestroyImmediate(fresh);
            material.name = assetName;

            material.SetTexture("_BaseMap", baseMap);
            material.SetTexture("_MainTex", baseMap);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_OcclusionMap", mask);
            material.SetFloat("_OcclusionStrength", 1f);
            material.EnableKeyword("_OCCLUSIONMAP");
            material.SetFloat("_WorkflowMode", 1f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);

            if (NonMetalSmoothness.TryGetValue(part, out float smoothness))
            {
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Smoothness", smoothness);
            }
            else
            {
                material.SetTexture("_MetallicGlossMap", mask);
                material.SetFloat("_Smoothness", 1f);  // multiplies the map's alpha
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }

            if (part == "Water") MakeTransparent(material);
            EditorUtility.SetDirty(material);
            return true;
        }

        static void MakeTransparent(Material material)
        {
            material.SetColor("_BaseColor", new Color(1f, 1f, 1f, WaterAlpha));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetShaderPassEnabled("DepthOnly", false);
        }

        /// <summary>Draws each converted material with Unity's own material preview into one sheet, to Temp/.</summary>
        [MenuItem("Tools/Aletheia/Render Pink Fix Preview")]
        public static void RenderPreview()
        {
            var paths = new List<string>();
            foreach (var part in BlacksmithParts) paths.Add($"{BlacksmithFolders[0]}/{Prefix}{part}.mat");
            foreach (var name in OldModelMaterials) paths.Add($"Assets/Aletheia/Models/model/textures/{name}.mat");

            const int columns = 5, cell = 300;
            int rows = Mathf.CeilToInt(paths.Count / (float)columns);
            var sheet = new Texture2D(columns * cell, rows * cell, TextureFormat.RGB24, false);
            var cellTarget = new RenderTexture(cell, cell, 0, RenderTextureFormat.ARGB32);
            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    var editor = Editor.CreateEditor(AssetDatabase.LoadAssetAtPath<Material>(paths[i]));
                    var preview = editor.RenderStaticPreview(paths[i], null, cell, cell);
                    Graphics.Blit(preview, cellTarget);
                    RenderTexture.active = cellTarget;
                    sheet.ReadPixels(new Rect(0, 0, cell, cell), i % columns * cell, (rows - 1 - i / columns) * cell);
                    Object.DestroyImmediate(editor);
                }
                sheet.Apply();
                File.WriteAllBytes(PreviewPath, sheet.EncodeToPNG());
                Debug.Log($"[PinkFix] preview saved: {Path.GetFullPath(PreviewPath)}");
            }
            finally
            {
                RenderTexture.active = null;
                Object.DestroyImmediate(cellTarget);
                Object.DestroyImmediate(sheet);
            }
        }
    }
}
