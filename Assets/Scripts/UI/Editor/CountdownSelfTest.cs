using System;
using System.IO;
using System.Reflection;
using CompanyF.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CompanyF.UI.EditorTools
{
    public static class CountdownSelfTest
    {
        const string Pending = "CompanyF.CountdownVerification";
        static int stage;
        static double next;
        static GameFlow flow;
        static CountdownPresentation presentation;
        static void Check(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException(message);
            Debug.Log("[Countdown Verify] PASS: " + message);
        }
        public static void Run()
        {
            Check(Mathf.Approximately(CountdownPresentation.Fade(0.1f, 1f, 0.2f, 0.8f), 0.5f), "0.2-second fade-in");
            Check(Mathf.Approximately(CountdownPresentation.Fade(0.6f, 1f, 0.2f, 0.8f), 0.5f), "0.8-second fade-out");
            Check(CountdownPresentation.Fade(1f, 1f, 0.2f, 0.8f) == 0f, "count disappears exactly at one second");
        }
        public static void StartPlayVerification()
        {
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }
        public static void PreviewBatch()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            StartPlayVerification();
        }
        [InitializeOnLoadMethod]
        static void Resume()
        {
            if (!SessionState.GetBool(Pending, false)) return;
            stage = 0; next = EditorApplication.timeSinceStartup + 2;
            EditorApplication.update -= Step; EditorApplication.update += Step;
        }
        static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        static void Pose(GamePhase phase, float elapsed = 0f, float timeLeft = 60f)
        {
            Set(flow, "<Phase>k__BackingField", phase);
            Set(flow, "<TimeLeft>k__BackingField", timeLeft);
            Set(flow, "phaseTime", elapsed);
            Set(flow, "goLeft", phase == GamePhase.Running && elapsed >= 0f ? flow.goTime - elapsed : 0f);
            presentation.Refresh();
        }
        static void Step()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
            try
            {
                if (stage == 0)
                {
                    flow = UnityEngine.Object.FindFirstObjectByType<GameFlow>();
                    presentation = UnityEngine.Object.FindFirstObjectByType<CountdownPresentation>();
                    Check(flow != null && presentation != null, "scene has countdown presentation");
                    flow.enabled = false;
                    foreach (var behaviour in flow.GetComponents<Behaviour>()) if (behaviour != flow) behaviour.enabled = false;
                    presentation.enabled = true;
                    Pose(GamePhase.Ready, 0.5f);
                    Check(presentation.DisplayedText == "出発", "ready text");
                }
                else if (stage == 1)
                {
                    Capture("countdown-ready.png");
                    Pose(GamePhase.Running, 0.4f);
                    Check(presentation.DisplayedText == "進行!!", "go text");
                }
                else if (stage == 2)
                {
                    Capture("countdown-go.png");
                    Pose(GamePhase.Running, 2f, 4.6f);
                    Check(presentation.DisplayedText == "5", "last-five countdown");
                }
                else if (stage == 3)
                {
                    Capture("countdown-five.png");
                    for (int number = 4; number >= 1; number--)
                    {
                        Pose(GamePhase.Running, 2f, number - 0.4f);
                        Check(presentation.DisplayedText == number.ToString(), "count " + number);
                    }
                    Pose(GamePhase.Finishing, 0.5f, 0f);
                    Check(presentation.DisplayedText == "フィニッシュ!!", "finish text");
                }
                else
                {
                    Capture("countdown-finish.png");
                    Pose(GamePhase.Finishing, 0.99f, 0f);
                    flow.Tick(0.02f);
                    Check(flow.Phase == GamePhase.Ended, "result opens after one-second finish");
                    Debug.Log("[Countdown Verify] ALL PASSED");
                    Finish(0); return;
                }
                stage++; next = EditorApplication.timeSinceStartup + 0.2;
            }
            catch (Exception ex) { Debug.LogException(ex); Finish(1); }
        }
        static void Capture(string name)
        {
            var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../countdown-review"));
            Directory.CreateDirectory(directory);
            var camera = Camera.main;
            if (camera == null) camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            var canvas = presentation.GetComponent<Canvas>();
            var oldMode = canvas.renderMode;
            var oldCamera = canvas.worldCamera;
            var oldDistance = canvas.planeDistance;
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var target = new RenderTexture(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = camera.nearClipPlane + 0.1f;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(directory, name), image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = oldActive;
                camera.targetTexture = oldTarget;
                canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldDistance;
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
        static void Finish(int exit)
        {
            SessionState.SetBool(Pending, false);
            EditorApplication.update -= Step;
            EditorApplication.Exit(exit);
        }
    }
}
