using System;
using System.IO;
using CompanyF.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public static class TutorialVideoVerification
{
    const string Pending = "CompanyF.TutorialVideoVerification";
    static int stage;
    static double deadline, next;
    static TutorialVideoController tutorial;
    static VideoPlayer player;
    static Keyboard keyboard;
    static ResultScreen result;
    static long initialFrame;

    public static void RunBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/TitleScene.unity");
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]
    static void Resume()
    {
        if (!SessionState.GetBool(Pending, false)) return;
        stage = 0;
        deadline = EditorApplication.timeSinceStartup + 120;
        next = EditorApplication.timeSinceStartup + 2;
        EditorApplication.update += Step;
    }
    static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        Debug.Log("[Tutorial Verify] PASS: " + message);
    }
    static void Key(bool down)
    {
        InputSystem.QueueStateEvent(keyboard, down ? new KeyboardState(UnityEngine.InputSystem.Key.Enter) : new KeyboardState());
        InputSystem.Update();
    }
    static void ShowResult(bool cleared)
    {
        result = UnityEngine.Object.FindFirstObjectByType<ResultScreen>();
        Check(result != null && result.restartKey == UnityEngine.InputSystem.Key.Enter && result.restartScene == "TitleScene", "result uses Enter and TitleScene");
        bool existed = PlayerPrefs.HasKey(ResultRanking.PrefsKey);
        string saved = PlayerPrefs.GetString(ResultRanking.PrefsKey, "");
        try { result.Show(cleared, 9999f); }
        finally
        {
            if (existed) PlayerPrefs.SetString(ResultRanking.PrefsKey, saved);
            else PlayerPrefs.DeleteKey(ResultRanking.PrefsKey);
            PlayerPrefs.Save();
        }
        result.enabled = false;
        Check(result.Showing && result.Cleared == cleared, cleared ? "clear result displayed" : "game-over result displayed");
    }
    static void PressResultEnter()
    {
        Key(false); result.SendMessage("Update");
        Key(true); result.SendMessage("Update");
    }
    static void Step()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Tutorial integration verification timed out at stage " + stage);
            switch(stage)
            {
                case 0:
                    var title = UnityEngine.Object.FindFirstObjectByType<TitleSceneController>();
                    tutorial = title.GetComponent<TutorialVideoController>();
                    Check(tutorial != null && tutorial.Stage == TutorialVideoController.PlaybackStage.Idle, "tutorial waits for title animation");
                    title.SendMessage("StartGame");
                    Check(title.GetComponent<AudioSource>().isPlaying, "title transition still plays SE");
                    Check(tutorial.Stage == TutorialVideoController.PlaybackStage.Idle, "video does not interrupt train animation");
                    stage++; break;
                case 1:
                    if (tutorial.Stage != TutorialVideoController.PlaybackStage.Playing) return;
                    Check(SceneManager.GetActiveScene().name == "TitleScene", "video runs before game scene");
                    Check(tutorial.Hint == "3回ペダルを踏むとスキップ", "skip instructions displayed");
                    tutorial.enabled = false;
                    player = tutorial.GetComponentInChildren<VideoPlayer>();
                    Check(player != null && player.clip != null && player.clip.length > 47, "48-second tutorial clip assigned");
                    Check(player.audioOutputMode == VideoAudioOutputMode.AudioSource && player.GetTargetAudioSource(0) != null, "tutorial audio routed to source");
                    initialFrame = player.frame;
                    tutorial.TickInput(0f, false);
                    tutorial.TickInput(0.1f, true);
                    tutorial.TickInput(0.2f, true);
                    Check(tutorial.SkipPressCount == 1, "held Enter counts only once");
                    tutorial.TickInput(0.3f, false);
                    tutorial.TickInput(0.5f, true);
                    Check(tutorial.SkipPressCount == 2 && tutorial.Stage == TutorialVideoController.PlaybackStage.Playing, "two presses do not skip");
                    tutorial.TickInput(2f, false);
                    Check(tutorial.SkipPressCount == 0, "slow presses reset the triple-press gesture");
                    stage++; next = EditorApplication.timeSinceStartup + 1; break;
                case 2:
                    Check(player.frame > initialFrame + 5, "video frames advance");
                    Capture(tutorial.GetComponentInChildren<Canvas>(), "tutorial-playback.png");
                    stage++; break;
                case 3:
                    if (tutorial.Stage != TutorialVideoController.PlaybackStage.AwaitingStart) return;
                    Check(player.frame > 1000, "video reaches its real end");
                    Check(tutorial.Hint == "ペダルを1回踏むとスタート", "completion asks for one pedal press");
                    stage++; next = EditorApplication.timeSinceStartup + 0.5; break;
                case 4:
                    Check(SceneManager.GetActiveScene().name == "TitleScene", "finishing video does not automatically start game");
                    tutorial.TickInput(60f, true);
                    Check(tutorial.Stage == TutorialVideoController.PlaybackStage.AwaitingStart, "held key across ending cannot start game");
                    tutorial.TickInput(60.1f, false);
                    tutorial.TickInput(60.2f, true);
                    stage++; next = EditorApplication.timeSinceStartup + 2; break;
                case 5:
                    Check(SceneManager.GetActiveScene().name == "SampleScene", "one fresh press after playback starts game");
                    keyboard = InputSystem.AddDevice<Keyboard>("TutorialVerificationKeyboard");
                    ShowResult(true);
                    stage++; next = EditorApplication.timeSinceStartup + 1.2; break;
                case 6:
                    PressResultEnter();
                    stage++; next = EditorApplication.timeSinceStartup + 1; break;
                case 7:
                    Check(SceneManager.GetActiveScene().name == "TitleScene", "clear result Enter returns to title");
                    tutorial = UnityEngine.Object.FindFirstObjectByType<TutorialVideoController>();
                    Check(tutorial.Stage == TutorialVideoController.PlaybackStage.Idle, "returning title does not auto-start while Enter is held");
                    Key(false);
                    UnityEngine.Object.FindFirstObjectByType<TitleSceneController>().SendMessage("StartGame");
                    stage++; break;
                case 8:
                    if (tutorial.Stage != TutorialVideoController.PlaybackStage.Playing) return;
                    tutorial.enabled = false;
                    tutorial.TickInput(0f, false);
                    tutorial.TickInput(0.1f, true); tutorial.TickInput(0.2f, false);
                    tutorial.TickInput(0.3f, true); tutorial.TickInput(0.4f, false);
                    Check(tutorial.Stage == TutorialVideoController.PlaybackStage.Playing, "second run still waits after two presses");
                    tutorial.TickInput(0.5f, true);
                    stage++; next = EditorApplication.timeSinceStartup + 2; break;
                case 9:
                    Check(SceneManager.GetActiveScene().name == "SampleScene", "third rapid press skips to game");
                    ShowResult(false);
                    stage++; next = EditorApplication.timeSinceStartup + 1.2; break;
                case 10:
                    PressResultEnter();
                    stage++; next = EditorApplication.timeSinceStartup + 1; break;
                default:
                    Check(SceneManager.GetActiveScene().name == "TitleScene", "game-over result Enter returns to title");
                    Check(UnityEngine.Object.FindFirstObjectByType<TutorialVideoController>().Stage == TutorialVideoController.PlaybackStage.Idle, "next visitor starts from fresh title");
                    Debug.Log("[Tutorial Verify] ALL PASSED");
                    Finish(0); break;
            }
        }
        catch(Exception error) { Debug.LogException(error); Finish(1); }
    }
    static void Capture(Canvas canvas, string name)
    {
        var camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
        var mode = canvas.renderMode; var oldCamera = canvas.worldCamera; float distance = canvas.planeDistance;
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        var target = new RenderTexture(1280,720,24); var pixels = new Texture2D(1280,720,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=target; canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=camera.nearClipPlane+0.1f;
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,1280,720),0,0); pixels.Apply();
            var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../tutorial-review"));Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder,name),pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active=oldActive;camera.targetTexture=oldTarget;
            canvas.renderMode=mode;canvas.worldCamera=oldCamera;canvas.planeDistance=distance;
            UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(target);
        }
    }
    static void Finish(int code)
    {
        if(keyboard!=null) InputSystem.RemoveDevice(keyboard);
        SessionState.SetBool(Pending,false);EditorApplication.update-=Step;
        EditorApplication.Exit(code);
    }
}
