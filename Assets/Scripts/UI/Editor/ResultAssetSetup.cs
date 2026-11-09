using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace CompanyF.UI.EditorTools
{
    /// <summary>
    /// Prepares the result screen's assets: the signboard, hat and paper become UI sprites, and a TextMeshPro font
    /// asset is made from M PLUS Rounded 1c Bold (RoundedMplus1c-Bold.ttf). The font asset is dynamic, so it renders any Japanese glyph the
    /// source font has. Safe to run again; an existing font asset is kept.
    /// </summary>
    public static class ResultAssetSetup
    {
        public const string SignboardPath = "Assets/ResultPicture/リザルトの看板.png";
        public const string HatPath = "Assets/ResultPicture/リザルトのハット.png";
        public const string PaperPath = "Assets/ResultPicture/リザルトの紙.png";
        public const string FontPath = "Assets/Fonts/Countdown/RoundedMplus1c-Bold.ttf";
        public const string FontAssetPath = "Assets/Fonts/Countdown/RoundedMplus1c-Bold SDF.asset";

        /// <summary>Every character the result screen shows, checked against the font.</summary>
        const string ResultCharacters = "ゲームクリアオーバー到着時間秒ランキング位---0123456789.";

        [MenuItem("Tools/Result/Prepare Result Assets")]
        public static void Prepare()
        {
            foreach (var path in new[] { SignboardPath, HatPath, PaperPath }) MakeSprite(path);
            var fontAsset = GetOrCreateFontAsset();
            if (fontAsset == null) return;

            bool allFound = fontAsset.TryAddCharacters(ResultCharacters, out string missing);
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            Debug.Log(allFound
                ? $"[Result] assets ready; font asset shows all result characters: {FontAssetPath}"
                : $"[Result] font asset is missing characters: {missing}");
        }

        static void MakeSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { Debug.LogError($"[Result] Not a texture: {path}"); return; }
            if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            Debug.Log($"[Result] sprite: {path}");
        }

        static TMP_FontAsset GetOrCreateFontAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null) { Debug.LogError($"[Result] Font not found: {FontPath}"); return null; }

            // Dynamic with multi-atlas: Japanese glyphs are added as text needs them, without baking thousands upfront.
            var fontAsset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048,
                                                          AtlasPopulationMode.Dynamic, true);
            fontAsset.name = Path.GetFileNameWithoutExtension(FontAssetPath);
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

            var atlas = fontAsset.atlasTextures[0];
            atlas.name = fontAsset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, fontAsset);
            fontAsset.material.name = fontAsset.name + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Result] font asset created: {FontAssetPath}");
            return fontAsset;
        }
    }
}
