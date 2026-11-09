using System;
using System.IO;
using CompanyF.Gameplay;
using CompanyF.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class InstallUserDesign
{
    const string Folder = "Assets/UI/ImportedDesign/";
    static Sprite Sprite(string name, Rect crop)
    {
        string png = Folder + name + ".png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(png);
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(Folder + name + "Sprite.asset");
        if (asset != null) return asset;
        var sprite = UnityEngine.Sprite.Create(texture, crop, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        sprite.name = name;
        AssetDatabase.CreateAsset(sprite, Folder + name + "Sprite.asset");
        return sprite;
    }
    static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var rect = parent.Find(name) as RectTransform;
        if (rect == null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            rect = (RectTransform)go.transform; rect.SetParent(parent, false);
        }
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = position; rect.sizeDelta = size;
        return rect;
    }
    static UnityEngine.UI.Image Image(RectTransform rect, Sprite sprite)
    {
        var image = rect.GetComponent<UnityEngine.UI.Image>();
        if (image == null) image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = sprite; image.preserveAspect = true; image.color = Color.white; image.raycastTarget = false;
        return image;
    }
    public static void Run()
    {
        AssetDatabase.Refresh();
        var departure = Sprite("Departure", new Rect(15, 178, 1890, 400));
        var go = Sprite("Go", new Rect(360, 0, 1244, 1076));
        var gauge = Sprite("ProgressTrack", new Rect(43, 241, 2114, 276));
        var cow = Sprite("Cow", new Rect(18, 75, 1214, 1052));
        var horse = Sprite("Horse", new Rect(250, 48, 752, 1123));

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        var ui = UnityEngine.Object.FindFirstObjectByType<GameFlowUI>();
        var follower = UnityEngine.Object.FindFirstObjectByType<CompanyF.Train.TrainFollower>();
        var followerSettings = new SerializedObject(follower);
        followerSettings.FindProperty("route").objectReferenceValue = UnityEngine.Object.FindFirstObjectByType<CompanyF.Track.TrackRoute>();
        followerSettings.ApplyModifiedPropertiesWithoutUndo();
        var presentation = UnityEngine.Object.FindFirstObjectByType<CountdownPresentation>();
        var overlay = ui.transform.Find("CountdownOverlay");
        var readyImage = Image(Rect(overlay, "DepartureArtwork", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1450, 307)), departure);
        var goImage = Image(Rect(overlay, "GoArtwork", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1050, 740)), go);
        readyImage.enabled = goImage.enabled = false;
        var pso = new SerializedObject(presentation);
        pso.FindProperty("departureArtwork").objectReferenceValue = readyImage;
        pso.FindProperty("goArtwork").objectReferenceValue = goImage;
        pso.ApplyModifiedPropertiesWithoutUndo();

        var uso = new SerializedObject(ui);
        var marker = (RectTransform)uso.FindProperty("progressMarker").objectReferenceValue;
        RectTransform oldLine = null;
        foreach (var candidate in ui.GetComponentsInChildren<RectTransform>(true))
            if (candidate.name == "ProgressLine") { oldLine = candidate; break; }
        if (oldLine == null) throw new InvalidOperationException("ProgressLine is missing");
        oldLine.GetComponent<UnityEngine.UI.Image>().enabled = false;
        foreach (var child in new[] { "StartDot", "EndDot" })
        { var t = oldLine.Find(child); if (t != null) t.gameObject.SetActive(false); }
        var parent = oldLine.parent;
        var trackRect = Rect(parent, "ProgressTrackArtwork", new Vector2(0.5f, 1f), new Vector2(0, -140), new Vector2(1120, 146));
        Image(trackRect, gauge);
        var positions = Rect(trackRect, "Positions", new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(960, 1));
        marker.SetParent(positions, false);
        marker.anchorMin = marker.anchorMax = new Vector2(0, 0.5f);
        marker.pivot = new Vector2(0.5f, 0.5f); marker.anchoredPosition = Vector2.zero;
        ConfigurePlayerMarker(marker);
        uso.FindProperty("cowMarkerSprite").objectReferenceValue = cow;
        uso.FindProperty("horseMarkerSprite").objectReferenceValue = horse;
        uso.FindProperty("animalMarkerSize").floatValue = 94f;
        uso.ApplyModifiedPropertiesWithoutUndo();
        CameraPathPlanner.Configure(GameObject.Find("Train").transform);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        scene = EditorSceneManager.OpenScene("Assets/Scenes/TitleScene.unity", OpenSceneMode.Single);
        var title = UnityEngine.Object.FindFirstObjectByType<TitleSceneController>();
        var source = title.GetComponent<AudioSource>();
        if (source == null) source = title.gameObject.AddComponent<AudioSource>();
        source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/SE/SceneTransition.wav");
        source.playOnAwake = false; source.loop = false; source.spatialBlend = 0f; source.volume = 0.7f;
        var tso = new SerializedObject(title);
        tso.FindProperty("transitionAudio").objectReferenceValue = source;
        tso.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[User Design] scenes configured, supplied art and animal sprites assigned, title transition audio connected.");
    }

    static void ConfigurePlayerMarker(RectTransform marker)
    {
        var sprite = Sprite("PlayerTrain", new Rect(233, 29, 1150, 995));
        marker.pivot = new Vector2(0.5f, 0f);
        marker.anchoredPosition = new Vector2(0f, -3f);
        marker.sizeDelta = new Vector2(112f * 1150f / 995f, 112f);
        Image(marker, sprite);
    }

    public static void InstallPlayerTrainAndPreview()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        InstallPlayerTrain();
        CompanyF.UI.EditorTools.CountdownSelfTest.PreviewBatch();
    }

    [MenuItem("Tools/Game/Install Player Train Marker")]
    public static void InstallPlayerTrain()
    {
        AssetDatabase.Refresh();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/SampleScene.unity")
            throw new InvalidOperationException("Open SampleScene before installing the player marker");
        var ui = UnityEngine.Object.FindFirstObjectByType<GameFlowUI>();
        var settings = new SerializedObject(ui);
        ConfigurePlayerMarker((RectTransform)settings.FindProperty("progressMarker").objectReferenceValue);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Player Train] supplied locomotive assigned to moving player marker");
    }
}
