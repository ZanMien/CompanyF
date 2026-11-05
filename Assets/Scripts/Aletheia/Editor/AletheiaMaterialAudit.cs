using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CompanyF.Aletheia.EditorTools
{
    /// <summary>
    /// Read-only: lists every material under Assets/Aletheia/Models (.mat files and materials embedded in models)
    /// with its shader, whether that shader works in URP, and the textures it holds. Written to Temp/.
    /// </summary>
    public static class AletheiaMaterialAudit
    {
        const string Root = "Assets/Aletheia/Models";
        const string ReportPath = "Temp/AletheiaMaterialAudit.txt";

        [MenuItem("Tools/Aletheia/Audit Model Materials")]
        public static void Run()
        {
            var report = new StringBuilder();
            int pink = 0, ok = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(asset is Material material)) continue;
                    bool bad = IsPink(material);
                    if (bad) pink++; else ok++;
                    string kind = path.EndsWith(".mat") ? "MAT" : "EMBEDDED";
                    report.AppendLine($"{(bad ? "PINK" : "OK  ")} {kind} {path} :: {material.name} shader={(material.shader != null ? material.shader.name : "NULL")} color={ColorOf(material)} tex=[{Textures(material)}]");
                }
            }

            report.AppendLine("== model files");
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;
                var names = new List<string>();
                foreach (var remap in importer.GetExternalObjectMap())
                    if (remap.Value is Material m)
                        names.Add($"{remap.Key.name}->{AssetDatabase.GetAssetPath(m)}({m.shader.name})");
                var used = new HashSet<string>();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                    foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                        foreach (var m in renderer.sharedMaterials)
                            used.Add(m == null ? "NULL" : $"{m.name}[{m.shader.name}|{AssetDatabase.GetAssetPath(m)}]");
                report.AppendLine($"MODEL {path} importMode={importer.materialImportMode} location={importer.materialLocation} remaps=[{string.Join("; ", names)}] uses=[{string.Join("; ", used)}]");
            }

            File.WriteAllText(ReportPath, report.ToString());
            Debug.Log($"[MaterialAudit] pink={pink} ok={ok} report={Path.GetFullPath(ReportPath)}");
        }

        public static bool IsPink(Material material)
        {
            var shader = material.shader;
            if (shader == null || !shader.isSupported || shader.name == "Hidden/InternalErrorShader") return true;
            // Built-in surface shaders have no pass URP can draw.
            return shader.name == "Standard" || shader.name == "Standard (Specular setup)"
                || shader.name.StartsWith("Legacy Shaders/") || shader.name.StartsWith("Autodesk") || shader.name.StartsWith("Mobile/")
                || shader.name.StartsWith("Nature/");
        }

        static string ColorOf(Material m)
        {
            string prop = m.HasProperty("_BaseColor") ? "_BaseColor" : m.HasProperty("_Color") ? "_Color" : null;
            return prop == null ? "-" : ColorUtility.ToHtmlStringRGBA(m.GetColor(prop));
        }

        static string Textures(Material m)
        {
            var list = new List<string>();
            foreach (var name in m.GetTexturePropertyNames())
            {
                var texture = m.GetTexture(name);
                if (texture != null) list.Add($"{name}={AssetDatabase.GetAssetPath(texture)}");
            }
            return string.Join(", ", list);
        }
    }
}
