using System;
using System.IO;
using CompanyF.Train;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CompanyF.Gameplay.EditorTools
{
    /// <summary>Opt-in playback smoke test. Only changes the Play Mode copy of the scene.</summary>
    public static class GameSoundEffectsVerification
    {
        const string Pending = "CompanyF.SEVerification";
        static int stage;
        static double next;
        static float firstTime;
        static int firstFrame;
        static GameSoundEffects sounds;
        static FurnaceController furnace;
        static TrainControls controls;
        static AudioSource effects, whistle, fire, result;
        static AudioSource other;

        [InitializeOnLoadMethod]
        static void Resume()
        {
            if (!SessionState.GetBool(Pending, false)) return;
            stage = 0;
            next = EditorApplication.timeSinceStartup + 3;
            EditorApplication.update -= Step;
            EditorApplication.update += Step;
        }

        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }

        static void Check(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException(message);
            Debug.Log("[SE Play Verify] PASS: " + message);
        }

        static AudioSource Source(string field) =>
            new SerializedObject(sounds).FindProperty(field).objectReferenceValue as AudioSource;

        static void Step()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
            try
            {
                switch (stage++)
                {
                    case 0:
                        sounds = UnityEngine.Object.FindFirstObjectByType<GameSoundEffects>();
                        Check(sounds != null, "scene SE controller exists");
                        furnace = sounds.GetComponent<FurnaceController>();
                        controls = sounds.GetComponent<TrainControls>();
                        foreach (var behaviour in sounds.GetComponents<Behaviour>())
                            if (behaviour != sounds) behaviour.enabled = false;
                        effects = Source("effectsSource"); whistle = Source("whistleSource");
                        fire = Source("fireSource"); result = Source("resultSource");
                        effects.Stop(); whistle.Stop(); fire.Stop(); result.Stop();
                        firstTime = Time.time; firstFrame = Time.frameCount;
                        var otherObject = new GameObject("SE_Verify_OtherAudio");
                        other = otherObject.AddComponent<AudioSource>();
                        other.clip = sounds.fire;
                        other.loop = true;
                        other.Play();
                        furnace.Tick(0f, true, false);
                        break;
                    case 1:
                        Check(effects.isPlaying, "door opening event plays audio");
                        effects.Stop();
                        furnace.Tick(furnace.openTime, false, false);
                        furnace.Tick(0f, false, true);
                        break;
                    case 2:
                        Check(effects.isPlaying, "shovel start event plays audio");
                        furnace.Tick(furnace.apexTime, false, false);
                        break;
                    case 3:
                        Check(fire.isPlaying, "coal apex event plays flare audio");
                        furnace.Tick(furnace.loadTime, false, false);
                        Check(furnace.State == DoorState.Open, "coal landing keeps door open for P1");
                        effects.Stop();
                        furnace.Tick(0f, true, false);
                        break;
                    case 4:
                        Check(effects.isPlaying, "door closing event plays audio");
                        var peek = sounds.GetComponent<CameraPeek>();
                        if (peek != null) peek.Tick(peek.moveTime, false, true);
                        controls.enabled = true;
                        controls.TickWhistle(0f, true);
                        sounds.SendMessage("LateUpdate");
                        controls.TickWhistle(controls.whistleMaxTime, false);
                        controls.enabled = false;
                        next = EditorApplication.timeSinceStartup + 1.2;
                        return;
                    case 5:
                        Check(whistle.isPlaying, "7.2 second whistle continues past the gameplay blast timer");
                        Check(Time.frameCount > firstFrame + 2 && Time.time > firstTime, "Play Mode frames and time advance");
                        sounds.GetComponent<BoilerTemperature>().Change(100000f);
                        break;
                    case 6:
                        Check(result.isPlaying, "overheat event plays game-over audio");
                        Check(!effects.isPlaying && !whistle.isPlaying && !fire.isPlaying, "end of round stops gameplay SE");
                        Check(other.mute && !other.isPlaying, "result also stops and mutes unrelated looping game audio");
                        other.Play();
                        break;
                    case 7:
                        Check(other.mute && !other.isPlaying, "audio restarted after the result stays silent");
                        sounds.enabled = false;
                        Check(!other.mute, "original mute state restored when controller is disabled");
                        UnityEngine.Object.DestroyImmediate(other.gameObject);
                        // Reset only the test round's end latch; keep the scene's real audio references.
                        typeof(GameSoundEffects).GetField("resultPlayed",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                            .SetValue(sounds, false);
                        sounds.enabled = true;
                        sounds.SendMessage("OnClear");
                        break;
                    case 8:
                        Check(result.isPlaying, "clear handler plays result audio");
                        Check(effects.mute && whistle.mute && fire.mute, "clear also mutes all other game audio");
                        result.Stop();
                        result.PlayOneShot(sounds.clear);
                        break;
                    case 9:
                        Check(result.isPlaying, "clear clip can be played");
                        result.Stop();
                        sounds.PlaySceneTransition();
                        break;
                    case 10:
                        Check(result.isPlaying, "scene transition hook plays audio");
                        result.PlayOneShot(sounds.fire);
                        break;
                    case 11:
                        Check(result.isPlaying, "fire clip can be played");
                        Finish(true, "Playback smoke test passed. Human listening and the future scene-transition integration remain separate checks.");
                        return;
                }
                next = EditorApplication.timeSinceStartup + 0.1;
            }
            catch (Exception ex) { Debug.LogException(ex); Finish(false, ex.ToString()); }
        }

        static void Finish(bool success, string message)
        {
            EditorApplication.update -= Step;
            SessionState.SetBool(Pending, false);
            var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "SEPlayVerification.txt"), (success ? "PASS\n" : "FAIL\n") + message);
            Debug.Log("[SE Play Verify] " + (success ? "ALL PASSED: " : "FAILED: ") + message);
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
