using System;
using System.IO;
using System.Linq;
using CompanyF.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TitleAttractVerification
{
    const string Pending="CompanyF.TitleAttractVerification";
    static double next;
    static bool sampled;
    static Vector3 previous;
    static TitleAttractLoop director;
    public static void RunBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/TitleScene.unity");
        SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]
    static void Resume()
    {
        if(!SessionState.GetBool(Pending,false))return;
        next=EditorApplication.timeSinceStartup+2;sampled=false;
        EditorApplication.update+=Step;
    }
    static void Check(bool value,string message)
    {
        if(!value)throw new InvalidOperationException(message);
        Debug.Log("[Title Attract Verify] PASS: "+message);
    }
    static void Step()
    {
        if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup<next)return;
        try
        {
            if(!sampled)
            {
                director=UnityEngine.Object.FindFirstObjectByType<TitleAttractLoop>();
                Check(director!=null && director.enabled,"title train director is active");
                Check(director.Locomotive.gameObject.activeInHierarchy,"locomotive is visible");
                Check(!UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t=>t.CompareTag("Cow")||t.CompareTag("Horse")),"no active cows or horses in title");
                Check(!director.Locomotive.GetComponentsInChildren<MonoBehaviour>().Any(x=>x!=null && x.enabled),"gameplay behaviours do not run in title");
                var bounds=new Bounds(director.Locomotive.position,Vector3.zero);
                foreach(var r in director.Locomotive.GetComponentsInChildren<MeshRenderer>())bounds.Encapsulate(r.bounds);
                Debug.Log($"[Title Attract Verify] model bounds {bounds.center} / {bounds.size}, proxies={director.GeometryCount}");
                director.enabled=false;
                int samples=0,shots=0;
                for(float t=0;t<=252;t+=0.2f)
                {
                    director.RenderAt(t);
                    if(!director.CameraPositionClear(director.FilmingCamera.transform.position))
                        throw new InvalidOperationException($"Camera obstructed: time={t:F2}, distance={director.RouteDistance:F1}, shot={director.ShotIndex}, position={director.FilmingCamera.transform.position}");
                    var view=director.FilmingCamera.WorldToViewportPoint(director.Focus);
                    if(view.z<0 || view.x<0.1f||view.x>0.9f||view.y<0.1f||view.y>0.9f)
                        throw new InvalidOperationException("Train left framing at "+t);
                    shots|=1<<director.ShotIndex;samples++;
                }
                Check(director.LoopCount>=2,"train loops through entire route twice");
                Check(shots==15,"all four camera works used");
                Check(samples>1200,"full route camera clearance and train visibility sampled at 0.2 seconds");
                foreach(float t in new[]{0f,6.2f,12.2f,18.2f,35f,65f,95f,123.6f,124.1f,124.6f})
                {
                    director.RenderAt(t);
                    Capture("title-"+t.ToString("000.0",System.Globalization.CultureInfo.InvariantCulture)+".png");
                }
                director.RenderAt(0);director.enabled=true;previous=director.Locomotive.position;
                sampled=true;next=EditorApplication.timeSinceStartup+1;
            }
            else
            {
                Check(Vector3.Distance(previous,director.Locomotive.position)>1f,"live title train continues to move");
                Debug.Log("[Title Attract Verify] ALL PASSED");Finish(0);
            }
        }
        catch(Exception error){Debug.LogException(error);Finish(1);}
    }
    static void Capture(string name)
    {
        var camera=director.FilmingCamera;
        var canvas=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(x=>x.name=="TitleCanvas");
        var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;float distance=canvas.planeDistance;
        var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
        var target=new RenderTexture(1280,720,24);var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+0.1f;
            Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();
            var directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../title-attract-review"));Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory,name),pixels.EncodeToPNG());
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
        SessionState.SetBool(Pending,false);EditorApplication.update-=Step;EditorApplication.Exit(code);
    }
}
