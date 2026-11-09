using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CompanyF.UI.EditorTools
{
    /// <summary>
    /// Builds the result screen prefab (camera + signboard UI) and puts one in the open game scene.
    /// Sizes are for the visible part of each picture: the pictures have transparent margins, so each image is
    /// scaled and shifted until its visible part fills the requested box. The picture files themselves are not changed.
    /// Safe to run again: the prefab is rebuilt in place (same file, so the scene instance stays linked).
    /// </summary>
    public static class ResultScreenBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/ResultScreen.prefab";
        public const string EmbossMaterialPath = "Assets/Fonts/Countdown/RoundedMplus1c-Bold SDF Emboss.mat";

        // Visible parts of the pictures, in pixels from the top-left corner (xMin, yMin, xMax, yMax).
        static readonly RectInt BoardPixels = new RectInt(392, 341, 1038, 624);      // the wooden board only, no chains
        static readonly RectInt HatPixels = new RectInt(272, 124, 1353, 805);
        static readonly RectInt PaperPixels = new RectInt(396, 480, 1717, 665);

        static readonly Color PaperTextColor = new Color32(0xfb, 0xf7, 0xd3, 255);
        static readonly Color EmbossColor = new Color32(0xb6, 0xb1, 0x92, 255);

        // Layout on a 1920x1080 canvas, centre origin. The board is 1700x900 in the middle of the screen.
        static readonly Vector2 BoardSize = new Vector2(1700f, 900f);
        static readonly Vector2 ResultTextCentre = new Vector2(0f, 270f);
        static readonly Vector2 ResultTextSize = new Vector2(700f, 300f);
        // The papers sit between the board's two iron straps.
        static readonly Vector2 ArrivalCentre = new Vector2(-449f, -180f);
        static readonly Vector2 ArrivalSize = new Vector2(500f, 500f);
        static readonly Vector2 RankingCentre = new Vector2(273f, -180f);
        static readonly Vector2 RankingSize = new Vector2(800f, 500f);
        static readonly Vector2 HatCentre = new Vector2(-735f, 425f);
        static readonly Vector2 HatSize = new Vector2(380f, 226f);
        const float HatTilt = 10f;

        // Background camera: an open stretch beside the line, looking down at 45 degrees.
        static readonly Vector3 CameraLookAt = new Vector3(-60f, 0f, 330f);
        const float CameraHeight = 60f;
        const float CameraYaw = 60f;

        [MenuItem("Tools/Result/Build Result Screen")]
        public static void Build()
        {
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ResultAssetSetup.FontAssetPath);
            if (fontAsset == null) { Debug.LogError("[Result] Run Tools/Result/Prepare Result Assets first."); return; }
            var board = AssetDatabase.LoadAssetAtPath<Sprite>(ResultAssetSetup.SignboardPath);
            var hat = AssetDatabase.LoadAssetAtPath<Sprite>(ResultAssetSetup.HatPath);
            var paper = AssetDatabase.LoadAssetAtPath<Sprite>(ResultAssetSetup.PaperPath);
            if (board == null || hat == null || paper == null) { Debug.LogError("[Result] Result pictures are not sprites yet."); return; }

            var emboss = GetOrCreateEmbossMaterial(fontAsset);
            var root = BuildHierarchy(fontAsset, emboss, board, hat, paper);
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            PlaceInScene(prefab);
            Debug.Log($"[Result] result screen built: {PrefabPath}");
        }

        static Material GetOrCreateEmbossMaterial(TMP_FontAsset fontAsset)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(EmbossMaterialPath);
            bool created = material == null;
            if (created) material = new Material(fontAsset.material);

            material.shader = Shader.Find("TextMeshPro/Distance Field");
            material.SetTexture(ShaderUtilities.ID_MainTex, fontAsset.atlasTexture);
            material.EnableKeyword("BEVEL_ON");
            material.SetFloat("_ShaderFlags", 0f);
            material.SetFloat("_Bevel", 0.8f);
            material.SetFloat("_BevelOffset", 0f);
            material.SetFloat("_BevelWidth", 0.1f);
            material.SetFloat("_BevelClamp", 0f);
            material.SetFloat("_BevelRoundness", 0.2f);
            material.SetFloat(ShaderUtilities.ID_LightAngle, 3.6f);
            material.SetColor("_SpecularColor", EmbossColor);
            material.SetFloat("_SpecularPower", 2f);
            material.SetFloat("_Reflectivity", 10f);
            // No diffuse shading, so flat parts keep exactly the text colour; the relief comes from the emboss (specular) colour.
            material.SetFloat("_Diffuse", 0f);
            material.SetFloat("_Ambient", 1f);

            if (created)
            {
                material.name = Path.GetFileNameWithoutExtension(EmbossMaterialPath);
                AssetDatabase.CreateAsset(material, EmbossMaterialPath);
            }
            else EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        static GameObject BuildHierarchy(TMP_FontAsset font, Material emboss, Sprite board, Sprite hat, Sprite paper)
        {
            var root = new GameObject("ResultScreen");
            var screen = root.AddComponent<ResultScreen>();

            // Background camera, off until the result screen appears.
            var cameraGo = new GameObject("ResultCamera");
            cameraGo.transform.SetParent(root.transform, false);
            var camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = 55f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1500f;
            camera.clearFlags = CameraClearFlags.Skybox;
            var cameraData = cameraGo.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null) cameraData = cameraGo.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraGo.AddComponent<AudioListener>();
            var rig = cameraGo.AddComponent<ResultCamera>();
            rig.lookAt = GroundPoint(CameraLookAt);
            rig.height = CameraHeight;
            rig.pitch = 45f;
            rig.yaw = CameraYaw;
            rig.Apply();

            // UI.
            var canvasGo = new GameObject("ResultScreenCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(root.transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var content = NewRect(canvasGo.transform, "Content", Vector2.zero, new Vector2(1920f, 1080f));

            var signboard = NewRect(content, "Signboard", Vector2.zero, BoardSize);
            FitVisible(signboard, board, BoardPixels);

            var resultText = NewText(content, "ResultText", font, emboss, ResultTextCentre, ResultTextSize, Color.white, 150f);
            resultText.text = "ゲームクリア";

            var arrival = NewRect(content, "ArrivalPaper", ArrivalCentre, ArrivalSize);
            FitVisible(arrival, paper, PaperPixels);
            var arrivalText = NewText(arrival, "ArrivalText", font, emboss, Vector2.zero, ArrivalSize - new Vector2(80f, 120f), PaperTextColor, 80f);
            arrivalText.text = "到着時間\n47.7秒";

            var ranking = NewRect(content, "RankingPaper", RankingCentre, RankingSize);
            FitVisible(ranking, paper, PaperPixels);
            var rankingTitle = NewText(ranking, "RankingTitle", font, emboss, new Vector2(0f, 150f), new Vector2(600f, 80f), PaperTextColor, 64f);
            rankingTitle.text = "ランキング";
            var rankingText = NewText(ranking, "RankingText", font, emboss, new Vector2(0f, -45f), new Vector2(600f, 300f), PaperTextColor, 46f);
            rankingText.alignment = TextAlignmentOptions.Center;
            rankingText.text = "1位 32.4秒\n2位 38.0秒\n3位 ---\n4位 ---\n5位 ---";

            // Hung on the board's top-left corner, sticking out past its edges; drawn over the board.
            var hatRect = NewRect(content, "Hat", HatCentre, HatSize);
            hatRect.localRotation = Quaternion.Euler(0f, 0f, HatTilt);
            FitVisible(hatRect, hat, HatPixels);

            var so = new SerializedObject(screen);
            so.FindProperty("resultCamera").objectReferenceValue = camera;
            so.FindProperty("content").objectReferenceValue = content.gameObject;
            so.FindProperty("resultText").objectReferenceValue = resultText;
            so.FindProperty("arrivalPaper").objectReferenceValue = arrival.gameObject;
            so.FindProperty("arrivalText").objectReferenceValue = arrivalText;
            so.FindProperty("rankingText").objectReferenceValue = rankingText;
            so.ApplyModifiedPropertiesWithoutUndo();

            content.gameObject.SetActive(false);
            cameraGo.SetActive(false);
            return root;
        }

        static void PlaceInScene(GameObject prefab)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "SampleScene") { Debug.LogWarning($"[Result] Open SampleScene to place the result screen (open: {scene.name})."); return; }
            foreach (var existing in Object.FindObjectsByType<ResultScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) == prefab) return;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(instance, "Add Result Screen");
            EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>The point on the terrain under <paramref name="point"/>.</summary>
        static Vector3 GroundPoint(Vector3 point)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain != null) point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
            return point;
        }

        /// <summary>
        /// Adds the picture under <paramref name="box"/>, scaled and shifted so that its visible part
        /// (<paramref name="visible"/>, in pixels from the top-left) exactly fills the box.
        /// </summary>
        static void FitVisible(RectTransform box, Sprite sprite, RectInt visible)
        {
            var go = new GameObject("Picture", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(box, false);
            go.transform.SetAsFirstSibling();
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;

            // Measured on the picture file; the imported texture may be smaller (max size), so use the file's size.
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            var texture = new Vector2(width, height);
            var scale = new Vector2(box.sizeDelta.x / visible.width, box.sizeDelta.y / visible.height);
            var visibleCentre = new Vector2(visible.x + visible.width * 0.5f, visible.y + visible.height * 0.5f);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.Scale(texture, scale);
            // Texture centre relative to the visible centre; picture y runs downwards.
            rect.anchoredPosition = new Vector2((texture.x * 0.5f - visibleCentre.x) * scale.x, (visibleCentre.y - texture.y * 0.5f) * scale.y);
        }

        static RectTransform NewRect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static TextMeshProUGUI NewText(Transform parent, string name, TMP_FontAsset font, Material material,
                                       Vector2 position, Vector2 size, Color color, float maxSize)
        {
            var rect = NewRect(parent, name, position, size);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSharedMaterial = material;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMin = 20f;
            text.fontSizeMax = maxSize;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>
        /// Renders the result screen of the open scene, background and UI, for both endings with sample data, into
        /// Temp/ResultScreenPreview_Clear.png and _Over.png. Works on a hidden copy; the scene is not changed.
        /// </summary>
        [MenuItem("Tools/Result/Render Result Screen Preview")]
        public static void RenderPreview()
        {
            var screen = Object.FindFirstObjectByType<ResultScreen>(FindObjectsInactive.Include);
            if (screen == null) { Debug.LogError("[Result] No result screen in the open scene."); return; }

            var copy = Object.Instantiate(screen.gameObject);
            foreach (var t in copy.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave;
            var target = new RenderTexture(1920, 1080, 24);
            try
            {
                var rig = copy.GetComponentInChildren<ResultCamera>(true);
                rig.gameObject.SetActive(true);
                rig.Apply();
                var camera = rig.GetComponent<Camera>();
                camera.enabled = false;
                camera.targetTexture = target;
                // In the game the overlay UI skips post-processing; turned off here so the UI colours match the screen.
                // (The background then lacks the post effects.)
                rig.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;

                // An overlay canvas is not seen by cameras, so the copy draws through the camera instead.
                var canvas = copy.GetComponentInChildren<Canvas>(true);
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                var content = canvas.transform.Find("Content");
                content.gameObject.SetActive(true);

                var sample = new List<float> { 32.4f, 38.0f, 45.6f };
                foreach (bool cleared in new[] { true, false })
                {
                    var result = content.Find("ResultText").GetComponent<TMP_Text>();
                    result.text = cleared ? "ゲームクリア" : "ゲームオーバー";
                    result.color = cleared ? screen.clearColor : screen.gameOverColor;
                    content.Find("ArrivalPaper").gameObject.SetActive(cleared);
                    content.Find("ArrivalPaper/ArrivalText").GetComponent<TMP_Text>().text = "到着時間\n45.6秒";
                    content.Find("RankingPaper/RankingText").GetComponent<TMP_Text>().text =
                        ResultRanking.FormatLines(sample, screen.rankingCount, cleared ? 2 : -1, screen.highlightColor);
                    foreach (var text in content.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
                    Canvas.ForceUpdateCanvases();

                    camera.Render();
                    RenderTexture.active = target;
                    var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                    RenderTexture.active = null;
                    string path = $"Temp/ResultScreenPreview_{(cleared ? "Clear" : "Over")}.png";
                    File.WriteAllBytes(path, image.EncodeToPNG());
                    Object.DestroyImmediate(image);
                }
                Debug.Log($"[Result] preview: Temp/ResultScreenPreview_Clear.png, Temp/ResultScreenPreview_Over.png (camera at {rig.transform.position})");
            }
            finally
            {
                RenderTexture.active = null;
                Object.DestroyImmediate(copy);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        [MenuItem("Tools/Result/Run Result Self Test")]
        public static void SelfTest()
        {
            var log = new List<string>();
            int failures = 0;
            void Check(string what, bool ok)
            {
                log.Add($"{(ok ? "PASS" : "FAIL")}  {what}");
                if (!ok) failures++;
            }

            // Ranking rules (no PlayerPrefs involved).
            var times = new List<float> { 32.4f, 38f };
            int place = ResultRanking.Insert(times, 47.66f);
            Check($"47.66 s goes third, rounded to 47.7 ({place}, {times[2]})", place == 2 && Mathf.Approximately(times[2], 47.7f));
            place = ResultRanking.Insert(times, 32.4f);
            Check($"a tie goes after the earlier record ({place})", place == 1);
            place = ResultRanking.Insert(times, 10f);
            Check($"a new best goes first ({place})", place == 0);
            var lines = ResultRanking.FormatLines(new List<float> { 32.4f, 38f }, 5, 1, Color.yellow).Split('\n');
            Check($"5 lines with --- for empty ranks: {string.Join(" / ", lines)}",
                  lines.Length == 5 && lines[0] == "1位 32.4秒" && lines[2] == "3位 ---" && lines[4] == "5位 ---");
            Check($"this run's line is coloured ({lines[1]})", lines[1] == "<color=#FFEB04FF>2位 38.0秒</color>");
            var many = new List<float>();
            for (int i = 0; i < ResultRanking.MaxStored; i++) many.Add(i);
            Check($"too slow for the stored list: not kept ({ResultRanking.Insert(many, 999f)})", many.Count == ResultRanking.MaxStored && many[many.Count - 1] < 999f);
            Check($"clear time 60 - 12.3 = {ResultRanking.FormatSeconds(60f - 12.3f)}", ResultRanking.FormatSeconds(60f - 12.3f) == "47.7");

            // Layout of the built prefab: both papers inside the board.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) Check("prefab exists", false);
            else
            {
                var content = prefab.transform.Find("ResultScreenCanvas/Content");
                Rect Box(string name)
                {
                    var r = (RectTransform)content.Find(name);
                    return new Rect(r.anchoredPosition - r.sizeDelta * 0.5f, r.sizeDelta);
                }
                var boardBox = Box("Signboard");
                foreach (var name in new[] { "ArrivalPaper", "RankingPaper" })
                {
                    var paperBox = Box(name);
                    Check($"{name} {paperBox} inside the board {boardBox}",
                          boardBox.Contains(paperBox.min) && boardBox.Contains(paperBox.max));
                }
                var result = Box("ResultText");
                Check($"result text {result} in the board's upper middle", boardBox.Contains(result.min) && boardBox.Contains(result.max) && Mathf.Approximately(result.center.x, 0f) && result.center.y > 0f);
            }

            log.Sort((a, b) => b.StartsWith("FAIL").CompareTo(a.StartsWith("FAIL")));
            Debug.Log($"[Result SelfTest] {(failures == 0 ? "ALL PASSED" : failures + " FAILED")}\n" + string.Join("\n", log));
        }
    }
}
