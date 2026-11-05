using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CompanyF.Train.EditorTools
{
    /// <summary>
    /// Read-only: reports, for the objects that should ride on the Train and everything under them, the parent,
    /// local and world transforms, static flags, rigidbodies, colliders, scripts and animators. Written to Temp/.
    /// </summary>
    public static class TrainAttachmentAudit
    {
        static readonly string[] Roots = { "Lever(L)", "Lever(R)", "Barrel_Empty1", "shovel" };
        const string ReportPath = "Temp/TrainAttachmentAudit.txt";

        [MenuItem("Tools/Train/Audit Attachments")]
        public static void Run()
        {
            var report = new StringBuilder();
            report.AppendLine($"playing={EditorApplication.isPlaying}");
            foreach (var mono in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (mono != null && mono.GetType().Namespace != null && mono.GetType().Namespace.StartsWith("CompanyF"))
                    report.AppendLine($"SCRIPT {mono.GetType().Name} on {PathOf(mono.transform)} enabled={mono.enabled}");

            foreach (var name in Roots)
            {
                var root = FindAnywhere(name);
                if (root == null) { report.AppendLine($"MISSING {name}"); continue; }
                int total = 0, staticCount = 0;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    total++;
                    var flags = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
                    if (flags != 0) staticCount++;
                    bool batched = false;
                    var renderer = t.GetComponent<Renderer>();
                    if (renderer != null) batched = renderer.isPartOfStaticBatch;
                    var extras = new StringBuilder();
                    foreach (var c in t.GetComponents<Component>())
                    {
                        if (c == null) { extras.Append(" MISSING-SCRIPT"); continue; }
                        if (c is Transform || c is MeshFilter || c is MeshRenderer) continue;
                        extras.Append(" ").Append(c.GetType().Name);
                        if (c is Rigidbody rb) extras.Append($"(kinematic={rb.isKinematic})");
                    }
                    if (t == root.transform || flags != 0 || batched || extras.Length > 0)
                        report.AppendLine($"{PathOf(t)} active={t.gameObject.activeInHierarchy} localPos={Fmt(t.localPosition)} localRot={Fmt(t.localEulerAngles)} localScale={Fmt(t.localScale)} worldPos={Fmt(t.position)} static={(int)flags} batched={batched} prefabInstance={PrefabUtility.IsPartOfPrefabInstance(t.gameObject)} comps=[{extras}]");
                }
                report.AppendLine($"== {name}: objects={total} withStaticFlags={staticCount}");
            }

            var train = GameObject.Find("Train");
            if (train != null) report.AppendLine($"Train {PathOf(train.transform)} worldPos={Fmt(train.transform.position)} static={(int)GameObjectUtility.GetStaticEditorFlags(train)}");
            File.WriteAllText(ReportPath, report.ToString());
            Debug.Log($"[Attachments] report={Path.GetFullPath(ReportPath)}");
        }

        static GameObject FindAnywhere(string name)
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t.name == name && t.gameObject.scene.IsValid() && t.gameObject.hideFlags == HideFlags.None)
                    return t.gameObject;
            return null;
        }

        static string Fmt(Vector3 v) => $"({v.x:0.###},{v.y:0.###},{v.z:0.###})";

        static string PathOf(Transform t)
        {
            string path = t.name;
            for (var p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return path;
        }
    }
}
