using System;
using System.IO;
using System.Linq;
using System.Text;
using CompanyF.Track;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TitleAttractSetup
{
    public static void Run()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/TitleScene.unity");
        var roots=scene.GetRootGameObjects();
        var train=roots.First(x=>x.name=="Train");
        var camera=roots.First(x=>x.name=="Camera").GetComponent<Camera>();
        var canvas=roots.First(x=>x.name=="TitleCanvas").GetComponent<RectTransform>();
        var manager=UnityEngine.Object.FindFirstObjectByType<TitleSceneController>();
        var director=manager.GetComponent<CompanyF.UI.TitleAttractLoop>();
        if(director==null)director=manager.gameObject.AddComponent<CompanyF.UI.TitleAttractLoop>();
        var settings=new SerializedObject(director);
        settings.FindProperty("locomotive").objectReferenceValue=train.transform;
        settings.FindProperty("filmingCamera").objectReferenceValue=camera;
        settings.FindProperty("route").objectReferenceValue=UnityEngine.Object.FindFirstObjectByType<TrackRoute>();
        settings.FindProperty("titleCanvas").objectReferenceValue=canvas;
        settings.FindProperty("tutorial").objectReferenceValue=manager.GetComponent<CompanyF.UI.TutorialVideoController>();
        settings.ApplyModifiedPropertiesWithoutUndo();
        foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(t.CompareTag("Cow") || t.CompareTag("Horse"))t.gameObject.SetActive(false);
        var background=canvas.Find("Background");
        if(background!=null && background.TryGetComponent<UnityEngine.UI.Image>(out var image))image.enabled=false;
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("[Title Attract Setup] PASS: four-shot train tour installed in title only");
    }

    public static void Inspect()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/TitleScene.unity");
        var report = new StringBuilder();
        foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            report.AppendLine($"ROOT {root.name}: {root.transform.position} active={root.activeSelf}");
        foreach(var behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(behaviour!=null)report.AppendLine($"SCRIPT {behaviour.name}: {behaviour.GetType().FullName} enabled={behaviour.enabled}");
        foreach(var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            report.AppendLine($"CAMERA {camera.name}: pos={camera.transform.position} rot={camera.transform.eulerAngles} enabled={camera.enabled} active={camera.gameObject.activeInHierarchy} fov={camera.fieldOfView}");
        foreach(var terrain in Terrain.activeTerrains)
            report.AppendLine($"TERRAIN {terrain.name}: pos={terrain.transform.position} size={terrain.terrainData.size}");
        var route=UnityEngine.Object.FindFirstObjectByType<TrackRoute>();
        for(var segment=route!=null?route.start:null;segment!=null;segment=segment.next)
        {
            segment.Evaluate(0,out var a,out _);segment.Evaluate(segment.Length,out var b,out _);
            report.AppendLine($"TRACK {segment.name}: length={segment.Length} from={a} to={b}");
        }
        var train=GameObject.Find("Train");
        if(train!=null)
        {
            var bounds=new Bounds(train.transform.position,Vector3.zero);
            foreach(var renderer in train.GetComponentsInChildren<Renderer>(true))bounds.Encapsulate(renderer.bounds);
            report.AppendLine($"TRAIN BOUNDS center={bounds.center} size={bounds.size}");
        }
        File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../title-attract-inspection.txt")),report.ToString());
    }
}
