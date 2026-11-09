using System.Linq;
using CompanyF.Train;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CompanyF.Gameplay.EditorTools
{
    /// <summary>
    /// Puts cows and horses beside the line, and adds the whistle's sound wave to the train.
    /// Running it again replaces the animals.
    /// </summary>
    public static class AnimalPlacer
    {
        const string RootName = "Animals";
        const string ModelFolder = "Assets/Models/Animals/";
        const string CowCall = "Assets/Voice/牛の声 V.2.0.wav";
        const string HorseCall = "Assets/Voice/馬の声 V.2.0.wav";
        const string OutlineMaterialPath = ModelFolder + "AnimalOutline.mat";
        const string OutlineName = "Outline";

        // Distance along the line, kept clear of the junctions and chasms. Sides alternate.
        static readonly float[] Spots = { 200f, 620f, 1180f, 1750f, 2050f };
        const float BesideLine = 9f;

        const float CowHeight = 2.4f;
        const float HorseHeight = 2.7f;

        [MenuItem("Tools/Game/Place Animals")]
        public static void Place()
        {
            var old = GameObject.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Place Animals");

            var cow = Prepare("Cow");
            var horse = Prepare("Horse");
            var terrain = Object.FindFirstObjectByType<Terrain>();

            for (int i = 0; i < Spots.Length; i++)
            {
                bool isCow = i % 2 == 0;
                var position = new Vector3(i % 2 == 0 ? BesideLine : -BesideLine, 0f, Spots[i]);
                if (terrain != null) position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
                Spawn(isCow ? "Cow" : "Horse", isCow ? cow : horse, isCow ? CowHeight : HorseHeight, root.transform, position, i);
            }

            var train = Object.FindFirstObjectByType<TrainFollower>();
            if (train != null && train.GetComponent<WhistleBlast>() == null)
                Undo.AddComponent<WhistleBlast>(train.gameObject);

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        /// <summary>Adds the outline to the animals already in the scene, without replacing them.</summary>
        [MenuItem("Tools/Game/Add Animal Outline")]
        public static void AddOutlineToAnimals()
        {
            var root = GameObject.Find(RootName);
            if (root == null) { Debug.LogError("[Game] No 'Animals' object in the scene."); return; }

            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                AddOutline(renderer);
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        /// <summary>
        /// Adds a child renderer on the same mesh and bones that draws only the outline, one material per submesh
        /// (an extra material slot on the model itself would only cover its last submesh).
        /// </summary>
        static void AddOutline(SkinnedMeshRenderer source)
        {
            var outlineMaterial = OutlineMaterial();
            var kept = source.sharedMaterials.Where(m => m != outlineMaterial).ToArray();
            if (kept.Length != source.sharedMaterials.Length)
            {
                Undo.RecordObject(source, "Add Animal Outline");
                source.sharedMaterials = kept;
            }
            if (source.name == OutlineName || source.transform.Find(OutlineName) != null) return;

            var go = new GameObject(OutlineName);
            Undo.RegisterCreatedObjectUndo(go, "Add Animal Outline");
            go.transform.SetParent(source.transform, false);

            var outline = go.AddComponent<SkinnedMeshRenderer>();
            outline.sharedMesh = source.sharedMesh;
            outline.bones = source.bones;
            outline.rootBone = source.rootBone;
            outline.localBounds = source.localBounds;
            outline.sharedMaterials = Enumerable.Repeat(outlineMaterial, source.sharedMesh.subMeshCount).ToArray();
            outline.shadowCastingMode = ShadowCastingMode.Off;
            outline.receiveShadows = false;
        }

        static Material OutlineMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            if (material != null) return material;

            material = new Material(Shader.Find("CompanyF/Outline"));
            AssetDatabase.CreateAsset(material, OutlineMaterialPath);
            return material;
        }

        static void Spawn(string name, AnimatorController controller, float height, Transform parent, Vector3 position, int index)
        {
            var animal = new GameObject($"{name}_{index}");
            animal.transform.SetParent(parent, false);
            animal.transform.position = position;

            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + name + ".fbx"), animal.transform);
            model.transform.localPosition = Vector3.zero;
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var size = model.GetComponentsInChildren<Renderer>().Select(r => r.bounds.size.y).DefaultIfEmpty(0f).Max();
            if (size > 0f) model.transform.localScale *= height / size;
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                AddOutline(renderer);

            var sound = animal.AddComponent<AudioSource>();
            sound.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(name == "Cow" ? CowCall : HorseCall);
            sound.playOnAwake = false;
            sound.spatialBlend = 1f;
            sound.minDistance = 20f;
            sound.maxDistance = 300f;

            animal.AddComponent<TrackAnimal>().call = sound;
        }

        /// <summary>Loops the walk and idle clips and builds the controller. The FBX has each clip twice; it uses the fuller one.</summary>
        static AnimatorController Prepare(string name)
        {
            string path = ModelFolder + name + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
                clip.loopTime = Base(clip.name) == "Walk" || Base(clip.name) == "Idle";
            importer.clipAnimations = clips;
            importer.SaveAndReimport();

            string controllerPath = ModelFolder + name + ".controller";
            AssetDatabase.DeleteAsset(controllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            var imported = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();
            foreach (var state in new[] { "Idle", "Walk", "Death" })
            {
                var clip = imported.Where(c => Base(c.name) == state)
                    .OrderByDescending(c => AnimationUtility.GetCurveBindings(c).Length).FirstOrDefault();
                machine.AddState(state).motion = clip;
            }
            return controller;
        }

        static string Base(string clip) => clip.Substring(clip.LastIndexOf('|') + 1);
    }
}
