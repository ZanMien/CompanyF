using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TitleTransitionVerification
{
    const string Pending = "CompanyF.TitleTransitionVerification";
    static int stage;
    static double deadline;
    static RectTransform train;
    static float startX;
    public static void RunBatch()
    {
        TutorialVideoVerification.RunBatch();
    }
    [InitializeOnLoadMethod]
    static void Resume()
    {
        if (!SessionState.GetBool(Pending, false)) return;
        stage = 0;
        deadline = EditorApplication.timeSinceStartup + 45;
        EditorApplication.update += Step;
    }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Debug.Log("[Title Transition] PASS: " + message);
    }
    static void Step()
    {
        if (!EditorApplication.isPlaying) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Title transition timed out");
            if (stage == 0)
            {
                var title = UnityEngine.Object.FindFirstObjectByType<TitleSceneController>();
                if (title == null || Time.frameCount < 3) return;
                var so = new SerializedObject(title);
                var audio = (AudioSource)so.FindProperty("transitionAudio").objectReferenceValue;
                train = (RectTransform)so.FindProperty("transitionTrain").objectReferenceValue;
                Check(audio != null && audio.clip != null && !audio.playOnAwake, "transition SE assigned, silent at startup");
                title.SendMessage("StartGame");
                Check(audio.isPlaying, "SE starts with title train animation");
                Check(train != null && train.gameObject.activeSelf, "transition train visible");
                startX = train.anchoredPosition.x;
                stage = 1;
            }
            else if (stage == 1 && train != null && train.anchoredPosition.x > startX + 20f)
            {
                Check(true, "title train moves while transition runs");
                stage = 2;
            }
            else if (stage == 2 && SceneManager.GetActiveScene().name == "SampleScene")
            {
                Check(true, "transition reaches game scene");
                SessionState.SetBool(Pending, false);
                EditorApplication.update -= Step;
                EditorApplication.Exit(0);
            }
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            SessionState.SetBool(Pending, false);
            EditorApplication.update -= Step;
            EditorApplication.Exit(1);
        }
    }
}
