using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CompanyF.Gameplay.EditorTools
{
    public static class GameSoundEffectsSetup
    {
        const string Folder = "Assets/Sound/SE/";
        static readonly string[] Fields = { "whistle", "shovel", "doorOpen", "doorClose", "fire", "fireFlare", "clear", "gameOver", "sceneTransition" };
        static readonly string[] Files = { "Whistle", "Shovel", "DoorOpen", "DoorClose", "Fire", "FireFlare", "GameClear", "GameOver", "SceneTransition" };

        public static void BatchSetup()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            Setup();
            Validate();
            FurnaceSelfTest.Run();
            GameFlowSetup.SelfTest();
        }

        [MenuItem("Tools/Game/Validate Sound Effects")]
        public static void Validate()
        {
            var sounds = UnityEngine.Object.FindFirstObjectByType<GameSoundEffects>();
            if (sounds == null) throw new InvalidOperationException("GameSoundEffects missing.");
            var so = new SerializedObject(sounds);
            for (int i = 0; i < Fields.Length; i++)
            {
                var clip = so.FindProperty(Fields[i]).objectReferenceValue as AudioClip;
                if (clip == null || clip.length <= 0f) throw new InvalidOperationException("Invalid clip: " + Fields[i]);
                Debug.Log($"[SE Verify] {Fields[i]}: {clip.length:F3}s, {clip.frequency}Hz, {clip.channels} channels");
            }
            foreach (var field in new[] { "effectsSource", "whistleSource", "fireSource", "resultSource" })
            {
                var source = so.FindProperty(field).objectReferenceValue as AudioSource;
                if (source == null || source.loop || source.playOnAwake) throw new InvalidOperationException("Invalid source: " + field);
            }
            var animal = UnityEngine.Object.FindFirstObjectByType<AnimalSounds>();
            if (animal == null || animal.cowSound == null || animal.horseSound == null) throw new InvalidOperationException("Animal clips missing.");
            Debug.Log("[SE Verify] PASS: all 9 clips, 4 non-looping sources and existing animal clips.");
        }

        [MenuItem("Tools/Game/Setup Sound Effects")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/SampleScene.unity") throw new InvalidOperationException("Open Assets/Scenes/SampleScene.unity first.");
            var train = GameObject.Find("Train");
            if (train == null || train.GetComponent<TrainControls>() == null) throw new InvalidOperationException("Train / TrainControls missing.");
            var clips = new AudioClip[Files.Length];
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + Files[i] + ".wav");
                if (clips[i] == null) throw new InvalidOperationException("Missing SE: " + Files[i]);
            }
            Undo.SetCurrentGroupName("Setup Sound Effects");
            int group = Undo.GetCurrentGroup();
            var sounds = train.GetComponent<GameSoundEffects>();
            if (sounds == null) sounds = Undo.AddComponent<GameSoundEffects>(train);
            var so = new SerializedObject(sounds);
            for (int i = 0; i < Fields.Length; i++) so.FindProperty(Fields[i]).objectReferenceValue = clips[i];
            foreach (var field in new[] { "effectsSource", "whistleSource", "fireSource", "resultSource" })
            {
                string name = "SE_" + field;
                var child = train.transform.Find(name);
                if (child == null)
                {
                    var go = new GameObject(name);
                    Undo.RegisterCreatedObjectUndo(go, "SE source");
                    go.transform.SetParent(train.transform, false);
                    child = go.transform;
                }
                var source = child.GetComponent<AudioSource>();
                if (source == null) source = Undo.AddComponent<AudioSource>(child.gameObject);
                Undo.RecordObject(source, "Configure SE source");
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.volume = 0.7f;
                so.FindProperty(field).objectReferenceValue = source;
            }
            so.ApplyModifiedProperties();
            // The SE controller plays the whole whistle clip. Keep the gameplay's one-second blast and cooldown.
            var controls = train.GetComponent<TrainControls>();
            Undo.RecordObject(controls, "Use SE whistle playback");
            controls.whistle = null;
            EditorUtility.SetDirty(controls);
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
            Debug.Log("[SE] Configured 9 clips and 4 non-looping sources. Scene saved. SceneTransition is available as a hook; no transition controller exists yet.");
        }
    }
}
