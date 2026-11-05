using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CompanyF.Gameplay.EditorTools
{
    /// <summary>
    /// Adds the door and shovel animators to the Train, links their scene objects, and writes the new
    /// furnace timings over the values saved in the scene. Safe to run again.
    /// </summary>
    public static class FurnaceAnimationSetup
    {
        [MenuItem("Tools/Game/Setup Furnace Animations")]
        public static void Run()
        {
            var train = GameObject.Find("Train");
            if (train == null) { Debug.LogError("[FurnaceAnim] No 'Train' object in the scene."); return; }

            var furnace = train.GetComponent<FurnaceController>();
            Undo.RecordObject(furnace, "Setup Furnace Animations");
            furnace.loadTime = 0.5f;
            furnace.apexTime = 0.42f;
            furnace.failAnimationTime = 0.75f;
            furnace.p2LockoutTime = 0.75f;
            EditorUtility.SetDirty(furnace);

            var door = GetOrAdd<FurnaceDoorAnimator>(train);
            Link(door, "furnace", furnace);
            Link(door, "door1", Find("Train/Steam_Locomotive/Steam_Locomotive_Controls_Burner_1"));
            Link(door, "door2", Find("Train/Steam_Locomotive/Steam_Locomotive_Controls_Burner_2"));

            var shovel = GetOrAdd<ShovelAnimator>(train);
            Link(shovel, "furnace", furnace);
            Link(shovel, "train", train.transform);
            Link(shovel, "shovel", Find("Train/shovel"));
            Link(shovel, "coal", Find("Train/shovel/Circle/Coal_48"));
            Link(shovel, "tipPivot", Find("Train/shovel/Circle.001"));
            Link(shovel, "landingPoint", Find("Train/ColePoint01"));
            Link(shovel, "apexPoint", Find("Train/ColePoint02"));
            Link(shovel, "blockedPoint", Find("Train/ColePoint03"));

            EditorSceneManager.MarkSceneDirty(train.scene);
            Debug.Log("[FurnaceAnim] setup done");
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : Undo.AddComponent<T>(go);
        }

        static Transform Find(string path)
        {
            var go = GameObject.Find(path);
            if (go == null) Debug.LogError($"[FurnaceAnim] Not found: {path}");
            return go != null ? go.transform : null;
        }

        static void Link(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }
    }
}
