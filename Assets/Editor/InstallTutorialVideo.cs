using CompanyF.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Video;

public static class InstallTutorialVideo
{
    public static void Run()
    {
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/TitleScene.unity");
        var title = Object.FindFirstObjectByType<TitleSceneController>();
        var tutorial = title.GetComponent<TutorialVideoController>();
        if (tutorial == null) tutorial = title.gameObject.AddComponent<TutorialVideoController>();
        var settings = new SerializedObject(tutorial);
        settings.FindProperty("tutorialClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/Video/HowToPlay.mp4");
        settings.FindProperty("instructionFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Countdown/RoundedMplus1c-Bold.ttf");
        settings.ApplyModifiedPropertiesWithoutUndo();
        if (settings.FindProperty("tutorialClip").objectReferenceValue == null) throw new System.InvalidOperationException("Tutorial video did not import");
        var controller = new SerializedObject(title);
        controller.FindProperty("tutorialVideo").objectReferenceValue = tutorial;
        controller.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Tutorial Install] PASS: title animation -> video -> game is connected");
    }
}
