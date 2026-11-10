using System.Linq;
using CompanyF.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CompanyF.UI.EditorTools
{
    /// <summary>
    /// Sets up the furnace game in the open scene: gameplay components on the Train (boiler and controls are
    /// re-added with default values), the HUD and the end screens (both replaced if they exist).
    /// </summary>
    public static class GameUIBuilder
    {
        const string HudName = "HUDCanvas";
        const string ResultName = "ResultCanvas";
        const string CountdownName = "CountdownCanvas";
        const string ResultCameraName = "ResultCamera";
        const string TitleScenePath = "Assets/Scenes/Title.unity";
        const string GameScenePath = "Assets/Scenes/SampleScene.unity";
        const string TitleCanvasName = "TitleCanvas";
        const string TitleTrainName = "Train";
        static readonly string[] StageRoots = { "Terrain", "Track", "Global Volume", "Directional Light" };
        const string SoundFolder = "Assets/Sound/";
        const string CowCallPath = "Assets/Voice/牛の声 V.2.0.wav";
        const string HorseCallPath = "Assets/Voice/馬の声 V.2.0.wav";
        const float ResultCameraDistance = 60f;
        static readonly Vector3 ResultCameraTarget = new Vector3(0f, 0f, 900f);
        static readonly Color WoodShadow = new Color32(0x89, 0x49, 0x14, 0xFF);
        static readonly Color Paper = new Color(0.4f, 0.26f, 0.12f);
        static readonly Color PaperText = new Color32(0xFB, 0xF7, 0xD3, 0xFF);

        static Font font;
        static Sprite knob;

        [MenuItem("Tools/Game/Setup Furnace Game")]
        public static void Build()
        {
            var train = GameObject.Find("Train");
            if (train == null) { Debug.LogError("[Game] No 'Train' object in the scene."); return; }

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            // Gameplay on the train. Boiler and controls are replaced so old serialized values don't linger.
            Replace<BoilerTemperature>(train);
            var controls = Replace<TrainControls>(train);
            GetOrAdd<TemperatureSpeed>(train);
            GetOrAdd<FurnaceController>(train);
            var audio = GetOrAdd<AudioSource>(train);
            audio.playOnAwake = false;
            audio.loop = true;
            controls.whistle = audio;

            foreach (var oldName in new[] { HudName, ResultName })
            {
                var old = GameObject.Find(oldName);
                if (old != null) Undo.DestroyObjectImmediate(old);
            }

            BuildHud();
            BuildResults();
            BuildCountdown();
            EditorSceneManager.MarkSceneDirty(train.scene);
        }

        /// <summary>Adds only the opening countdown, leaving the tuned gameplay values alone.</summary>
        [MenuItem("Tools/Game/Setup Start Countdown")]
        public static void SetupCountdown()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildCountdown();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        static void BuildHud()
        {
            var canvas = NewCanvas(HudName, 5);

            // Speedometer, bottom right.
            var meter = NewRect(canvas, "Speedometer", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(340f, 340f));
            Stretch(NewImage(meter, "Dial", knob, new Color(0.1f, 0.1f, 0.1f, 0.85f)), 0f);
            var track = Stretch(NewImage(meter, "ArcTrack", knob, new Color(1f, 1f, 1f, 0.15f)), 12f);
            MakeArc(track.GetComponent<Image>(), 240f / 360f);
            var arc = Stretch(NewImage(meter, "SpeedArc", knob, new Color(1f, 0.55f, 0.1f, 1f)), 12f);
            MakeArc(arc.GetComponent<Image>(), 0f);
            Stretch(NewImage(meter, "Inner", knob, new Color(0.07f, 0.07f, 0.07f, 1f)), 36f);

            for (int i = 0; i <= 7; i++)
            {
                float frac = i / 7f;
                float compass = (-120f + 240f * frac) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Sin(compass), Mathf.Cos(compass));
                var tick = NewRect(meter, $"Tick_{15 + i * 20}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), dir * 118f, new Vector2(4f, 16f));
                tick.localEulerAngles = new Vector3(0f, 0f, 120f - 240f * frac);
                tick.gameObject.AddComponent<Image>().color = Color.white;
                var number = NewText(meter, $"Label_{15 + i * 20}", (15 + i * 20).ToString(), 20, TextAnchor.MiddleCenter, new Color(0.85f, 0.85f, 0.85f));
                Place(number.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), dir * 92f, new Vector2(50f, 26f));
            }

            var needle = NewRect(meter, "Needle", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(6f, 112f));
            needle.gameObject.AddComponent<Image>().color = new Color(1f, 0.25f, 0.2f);
            needle.localEulerAngles = new Vector3(0f, 0f, 120f);
            var hub = NewImage(meter, "Hub", knob, new Color(0.85f, 0.85f, 0.85f));
            Place(hub, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));

            var speedText = NewText(meter, "SpeedText", "15 km/h", 40, TextAnchor.MiddleCenter, Color.white);
            speedText.fontStyle = FontStyle.Bold;
            Place(speedText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -82f), new Vector2(260f, 50f));
            var speedLabel = NewText(meter, "SpeedLabel", "速度", 24, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.8f));
            Place(speedLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(120f, 30f));

            // Temperature bar, bottom (left of the speedometer).
            var bar = NewRect(canvas, "TemperatureBar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-120f, 70f), new Vector2(1100f, 36f));
            var gradientGo = new GameObject("Gradient", typeof(RectTransform), typeof(RawImage));
            gradientGo.transform.SetParent(bar, false);
            Stretch((RectTransform)gradientGo.transform, 0f);
            var gradient = gradientGo.GetComponent<RawImage>();
            gradient.raycastTarget = false;

            var pointer = NewRect(bar, "Pointer", new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, 26f));
            pointer.anchorMin = new Vector2(0f, 0f);
            pointer.anchorMax = new Vector2(0f, 1f);
            pointer.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 1f);
            var temperatureText = NewText(pointer, "TemperatureText", "50 °C", 34, TextAnchor.LowerCenter, Color.white);
            temperatureText.fontStyle = FontStyle.Bold;
            Place(temperatureText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(220f, 44f));

            var minLabel = NewText(bar, "MinLabel", "50 °C", 28, TextAnchor.UpperCenter, Color.white);
            Place(minLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(180f, 36f));
            var maxLabel = NewText(bar, "MaxLabel", "1500 °C", 28, TextAnchor.UpperCenter, new Color(1f, 0.15f, 0.1f));
            Place(maxLabel.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(180f, 36f));

            // Furnace status, upper centre.
            var door = NewText(canvas, "DoorText", "閉", 80, TextAnchor.MiddleCenter, Color.white);
            door.fontStyle = FontStyle.Bold;
            Place(door.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(900f, 100f));
            var flash = NewText(canvas, "FlashText", "", 60, TextAnchor.MiddleCenter, Color.white);
            flash.fontStyle = FontStyle.Bold;
            Place(flash.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(900f, 80f));
            var fail = NewText(canvas, "FailText", "2P 失敗", 60, TextAnchor.MiddleCenter, new Color(1f, 0.2f, 0.15f));
            fail.fontStyle = FontStyle.Bold;
            Place(fail.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -350f), new Vector2(900f, 80f));
            fail.gameObject.SetActive(false);

            var hud = canvas.gameObject.AddComponent<GameHUD>();
            var so = new SerializedObject(hud);
            so.FindProperty("needle").objectReferenceValue = needle;
            so.FindProperty("speedArc").objectReferenceValue = arc.GetComponent<Image>();
            so.FindProperty("speedText").objectReferenceValue = speedText;
            so.FindProperty("barGradient").objectReferenceValue = gradient;
            so.FindProperty("temperaturePointer").objectReferenceValue = pointer;
            so.FindProperty("temperatureText").objectReferenceValue = temperatureText;
            so.FindProperty("minLabel").objectReferenceValue = minLabel;
            so.FindProperty("maxLabel").objectReferenceValue = maxLabel;
            so.FindProperty("doorText").objectReferenceValue = door;
            so.FindProperty("flashText").objectReferenceValue = flash;
            so.FindProperty("failText").objectReferenceValue = fail;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Rebuilds only the result screen, leaving the tuned gameplay values alone.</summary>
        [MenuItem("Tools/Game/Setup Result Screen")]
        public static void SetupResultScreen()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var old = GameObject.Find(ResultName);
            if (old != null) Undo.DestroyObjectImmediate(old);
            BuildResults();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        /// <summary>
        /// Assigns the sound effects and BGM and gives the animals their calls, leaving everything else alone.
        /// An existing GameAudio is kept, so its volume settings survive.
        /// </summary>
        [MenuItem("Tools/Game/Setup Game Audio")]
        public static void SetupGameAudio()
        {
            var audio = Object.FindFirstObjectByType<GameAudio>();
            if (audio == null)
            {
                var created = new GameObject("GameAudio");
                Undo.RegisterCreatedObjectUndo(created, "Setup Game Audio");
                audio = created.AddComponent<GameAudio>();
            }
            var go = audio.gameObject;
            Undo.RecordObject(audio, "Setup Game Audio");
            audio.mainBgm = StreamedClip("メインゲームBGM");
            audio.resultBgm = StreamedClip("結果画面BGM");
            audio.doorOpen = Clip("でかい扉が開いた音");
            audio.doorClose = Clip("でかい扉が閉まった音");
            audio.shovel = Clip("土をシャベルですくって入れる音");
            audio.fireUp = Clip("炎の勢いが強まった音 V.3.0");
            audio.fireLoop = Clip("炎が燃えている音 V3.0");
            audio.lever = Clip("進路レバーを倒した音 V.4.0");
            audio.clear = Clip("ゲームクリア音");
            audio.gameOver = Clip("ゲームオーバー音");
            audio.sceneChange = Clip("シーン遷移");

            foreach (var animal in Object.FindObjectsByType<TrackAnimal>(FindObjectsSortMode.None))
            {
                if (animal.call == null) continue;
                Undo.RecordObject(animal.call, "Setup Game Audio");
                animal.call.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(animal.name.StartsWith("Cow") ? CowCallPath : HorseCallPath);
            }
            EditorSceneManager.MarkSceneDirty(go.scene);
        }

        /// <summary>Long music: streamed from disk instead of decompressed into memory when the scene loads.</summary>
        static AudioClip StreamedClip(string name)
        {
            var importer = AssetImporter.GetAtPath($"{SoundFolder}{name}.wav") as AudioImporter;
            if (importer != null && importer.defaultSampleSettings.loadType != AudioClipLoadType.Streaming)
            {
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.Streaming;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
            return Clip(name);
        }

        static AudioClip Clip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SoundFolder}{name}.wav");
            if (clip == null) Debug.LogError($"[Game] No sound '{name}' in {SoundFolder}");
            return clip;
        }

        /// <summary>Run with the Title scene open: copies the stage from the game scene and builds the title screen.</summary>
        [MenuItem("Tools/Game/Setup Title Screen")]
        public static void SetupTitleScreen()
        {
            var title = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (title.path != TitleScenePath) { Debug.LogError($"[Game] Open {TitleScenePath} first."); return; }

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            CopyStage(title);
            AimTitleCamera();

            var old = GameObject.Find(TitleCanvasName);
            if (old != null) Undo.DestroyObjectImmediate(old);
            BuildTitleCanvas();

            MoveSceneToFront(TitleScenePath);
            EditorSceneManager.MarkSceneDirty(title);
        }

        /// <summary>Run with the Title scene open: adds a copy of the train with only its looks and the route loop.</summary>
        [MenuItem("Tools/Game/Setup Title Train")]
        public static void SetupTitleTrain()
        {
            var title = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (title.path != TitleScenePath) { Debug.LogError($"[Game] Open {TitleScenePath} first."); return; }

            var old = GameObject.Find(TitleTrainName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            var game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
            var source = game.GetRootGameObjects().FirstOrDefault(root => root.name == TitleTrainName);
            if (source == null)
            {
                EditorSceneManager.CloseScene(game, true);
                Debug.LogError($"[Game] No '{TitleTrainName}' in {GameScenePath}.");
                return;
            }
            var copy = Object.Instantiate(source);
            copy.name = TitleTrainName;
            Undo.RegisterCreatedObjectUndo(copy, "Setup Title Train");
            EditorSceneManager.CloseScene(game, true);

            foreach (var camera in copy.GetComponentsInChildren<Camera>(true))
                Object.DestroyImmediate(camera.gameObject);
            foreach (var script in copy.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(script is Train.TrainFollower))
                    Object.DestroyImmediate(script);
            foreach (var sound in copy.GetComponentsInChildren<AudioSource>(true))
                Object.DestroyImmediate(sound);

            copy.AddComponent<Train.TitleTrainLoop>();
            EditorSceneManager.MarkSceneDirty(title);
        }

        static void CopyStage(UnityEngine.SceneManagement.Scene title)
        {
            foreach (var root in title.GetRootGameObjects())
                if (System.Array.IndexOf(StageRoots, root.name) >= 0)
                    Undo.DestroyObjectImmediate(root);

            var game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

            // RenderSettings belong to the active scene, so read them while the game scene is active.
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(game);
            var sky = RenderSettings.skybox;
            var ambientMode = RenderSettings.ambientMode;
            float ambientIntensity = RenderSettings.ambientIntensity;
            var fog = (RenderSettings.fog, RenderSettings.fogMode, RenderSettings.fogColor, RenderSettings.fogDensity, RenderSettings.fogStartDistance, RenderSettings.fogEndDistance);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(title);

            Light sun = null;
            foreach (var root in game.GetRootGameObjects())
            {
                if (System.Array.IndexOf(StageRoots, root.name) < 0) continue;
                var copy = Object.Instantiate(root);
                copy.name = root.name;
                Undo.RegisterCreatedObjectUndo(copy, "Setup Title Screen");
                if (copy.TryGetComponent<Light>(out var light)) sun = light;
            }
            EditorSceneManager.CloseScene(game, true);

            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientIntensity = ambientIntensity;
            (RenderSettings.fog, RenderSettings.fogMode, RenderSettings.fogColor, RenderSettings.fogDensity, RenderSettings.fogStartDistance, RenderSettings.fogEndDistance) = fog;
            RenderSettings.sun = sun;
        }

        static void AimTitleCamera()
        {
            var camera = Camera.main;
            if (camera == null) { Debug.LogError("[Game] The Title scene has no Main Camera."); return; }

            Undo.RecordObject(camera.transform, "Setup Title Screen");
            Undo.RecordObject(camera, "Setup Title Screen");
            var rotation = Quaternion.Euler(45f, 45f, 0f);
            camera.transform.SetPositionAndRotation(ResultCameraTarget - rotation * Vector3.forward * ResultCameraDistance, rotation);
            camera.farClipPlane = 3000f;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        /// <summary>Layout and colours follow the design sheet; set each Image's Source Image when the art is ready.</summary>
        static void BuildTitleCanvas()
        {
            var canvas = NewCanvas(TitleCanvasName, 0);
            var logo = NewRect(canvas, "Logo", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(1500f, 700f));

            // The puffs come first so they are drawn behind everything else in the logo.
            var smokeColor = new Color32(0x72, 0x72, 0x72, 0xFF);
            var puffs = new Image[10];
            for (int i = 0; i < puffs.Length; i++)
            {
                var puff = NewImage(logo, $"Puff_{i}", knob, smokeColor);
                Place(puff, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170f, 170f));
                puffs[i] = puff.GetComponent<Image>();
            }

            var locomotiveColor = new Color32(0x28, 0x28, 0x28, 0xFF);
            var chimney = new Vector2(-440f, 60f);
            Place(NewImage(logo, "Chimney", null, locomotiveColor), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), chimney, new Vector2(80f, 90f));
            var body = NewImage(logo, "Locomotive", knob, locomotiveColor);
            Place(body, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(1320f, 330f));
            var name = TitleText(body, "NameText", "シュッポッポ!!", 150, new Color32(0xF2, 0x88, 0x86, 0xFF));
            Stretch(name.rectTransform, 0f);

            var cloud = NewImage(logo, "Cloud", knob, smokeColor);
            Place(cloud, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-440f, 180f), new Vector2(500f, 180f));
            cloud.localEulerAngles = new Vector3(0f, 0f, 8f);
            var futari = TitleText(cloud, "FutariText", "ふたりで", 100, new Color32(0xF6, 0xE8, 0x8C, 0xFF));
            Stretch(futari.rectTransform, 0f);

            var start = NewRect(canvas, "GameStart", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(600f, 250f));
            var group = start.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            foreach (float x in new[] { -130f, 130f })
                Place(NewImage(start, "Pedal", knob, Color.black), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(190f, 250f));
            var startText = NewText(start, "StartText", "ゲームスタート", 76, TextAnchor.MiddleCenter, new Color32(0xE5, 0xE5, 0xBC, 0xFF));
            startText.fontStyle = FontStyle.Bold;
            Object.DestroyImmediate(startText.GetComponent<Outline>());
            var shadow = startText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
            shadow.effectDistance = new Vector2(3.5f, -3.5f);
            Stretch(startText.rectTransform, 0f);

            var screen = canvas.gameObject.AddComponent<TitleScreen>();
            screen.smokeStart = chimney + new Vector2(0f, 90f);
            screen.smokeTop = 540f - 90f + 120f;
            screen.sceneChange = Clip("シーン遷移");
            var so = new SerializedObject(screen);
            so.FindProperty("startLabel").objectReferenceValue = group;
            var puffsProperty = so.FindProperty("puffs");
            puffsProperty.arraySize = puffs.Length;
            for (int i = 0; i < puffs.Length; i++)
                puffsProperty.GetArrayElementAtIndex(i).objectReferenceValue = puffs[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Text TitleText(Transform parent, string name, string text, int size, Color color)
        {
            var label = NewText(parent, name, text, size, TextAnchor.MiddleCenter, color);
            label.fontStyle = FontStyle.Bold;
            return label;
        }

        static void MoveSceneToFront(string path)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        [MenuItem("Tools/Game/Clear Ranking")]
        public static void ClearRanking() => ResultRanking.Clear();

        /// <summary>Layout and colours follow the design sheet; set each Image's Source Image when the art is ready.</summary>
        static void BuildResults()
        {
            var canvas = NewCanvas(ResultName, 20);
            var panel = Stretch(NewRect(canvas, "ResultPanel", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero), 0f);

            // Board, hanging from the top of the screen. Chains are its children so they move with it.
            var board = NewImage(panel, "Board", null, new Color32(0xC8, 0x61, 0x0D, 0xFF));
            Place(board, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(1700f, 900f));
            var shadow = board.gameObject.AddComponent<Shadow>();
            shadow.effectColor = WoodShadow;
            shadow.effectDistance = new Vector2(14f, -14f);
            foreach (float x in new[] { -650f, 650f })
                Place(NewImage(board, "Chain", null, new Color(0.15f, 0.12f, 0.1f)), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(x, 0f), new Vector2(14f, 200f));
            for (int i = 1; i < 5; i++)
                Place(NewImage(board, $"PlankLine_{i}", null, WoodShadow), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -180f * i), new Vector2(1700f, 6f));

            var title = Emboss(NewText(board, "TitleText", "ゲームクリア", 96, TextAnchor.MiddleCenter, Color.white));
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(700f, 300f));

            var timePaper = NewImage(board, "TimePaper", null, Paper);
            Place(timePaper, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-467f, -360f), new Vector2(500f, 500f));
            var timeLabel = Emboss(NewText(timePaper, "TimeLabel", "到着時間", 64, TextAnchor.MiddleCenter, PaperText));
            Place(timeLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(460f, 90f));
            var time = Emboss(NewText(timePaper, "TimeText", "00:00.00", 76, TextAnchor.MiddleCenter, PaperText));
            Place(time.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(460f, 120f));

            var rankingPaper = NewImage(board, "RankingPaper", null, Paper);
            Place(rankingPaper, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(316f, -360f), new Vector2(800f, 500f));
            var rankingLabel = Emboss(NewText(rankingPaper, "RankingLabel", "ランキング", 60, TextAnchor.MiddleCenter, PaperText));
            Place(rankingLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(760f, 80f));
            var rows = new Text[ResultRanking.Size];
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = Emboss(NewText(rankingPaper, $"Row_{i + 1}", "", 54, TextAnchor.MiddleCenter, PaperText));
                Place(rows[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -116f - i * 72f), new Vector2(760f, 68f));
            }

            var retry = NewButton(panel, "RetryButton", "もう一回", -210f);
            var toTitle = NewButton(panel, "TitleButton", "タイトル画面へ", 210f);
            EnsureEventSystem();

            var ui = canvas.gameObject.AddComponent<GameResultUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("board").objectReferenceValue = board;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("timeText").objectReferenceValue = time;
            so.FindProperty("resultCamera").objectReferenceValue = BuildResultCamera();
            so.FindProperty("retryButton").objectReferenceValue = retry;
            so.FindProperty("titleButton").objectReferenceValue = toTitle;
            var rowsProperty = so.FindProperty("rankingRows");
            rowsProperty.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++)
                rowsProperty.GetArrayElementAtIndex(i).objectReferenceValue = rows[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);

            AddSceneToBuild(TitleScenePath);
        }

        /// <summary>A button in the strip below the board.</summary>
        static Button NewButton(Transform parent, string name, string text, float x)
        {
            var rect = NewImage(parent, name, null, Color.white);
            Place(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(x, 10f), new Vector2(380f, 70f));
            var image = rect.GetComponent<Image>();
            image.raycastTarget = true;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Paper;
            colors.highlightedColor = colors.selectedColor = new Color32(0xC8, 0x61, 0x0D, 0xFF);
            colors.pressedColor = WoodShadow;
            colors.disabledColor = new Color(Paper.r, Paper.g, Paper.b, 0.5f);
            button.colors = colors;

            var label = Emboss(NewText(rect, "Label", text, 42, TextAnchor.MiddleCenter, PaperText));
            Stretch(label.rectTransform, 0f);
            return button;
        }

        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(go, "Setup Result Screen");
        }

        /// <summary>Looks at a point on the stage floor from 45° up and at an angle.</summary>
        static Camera BuildResultCamera()
        {
            var old = GameObject.Find(ResultCameraName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            var go = new GameObject(ResultCameraName, typeof(Camera));
            Undo.RegisterCreatedObjectUndo(go, "Setup Result Screen");
            var rotation = Quaternion.Euler(45f, 45f, 0f);
            go.transform.SetPositionAndRotation(ResultCameraTarget - rotation * Vector3.forward * ResultCameraDistance, rotation);

            var camera = go.GetComponent<Camera>();
            camera.depth = 10f;
            camera.farClipPlane = 3000f;
            camera.enabled = false;
            go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            return camera;
        }

        static void AddSceneToBuild(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static Text Emboss(Text label)
        {
            label.fontStyle = FontStyle.Bold;
            var outline = label.GetComponent<Outline>();
            outline.effectColor = new Color32(0xB6, 0xB1, 0x92, 0xFF);
            outline.effectDistance = new Vector2(3f, -3f);
            return label;
        }

        static void BuildCountdown()
        {
            var old = GameObject.Find(CountdownName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            var canvas = NewCanvas(CountdownName, 15);
            var veil = Stretch(NewImage(canvas, "Veil", null, new Color(0f, 0f, 0f, 0.35f)), 0f);
            var label = NewText(canvas, "CountdownText", "3", 320, TextAnchor.MiddleCenter, Color.white);
            label.fontStyle = FontStyle.Bold;
            label.GetComponent<Outline>().effectDistance = new Vector2(6f, -6f);
            Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 480f));

            var countdown = canvas.gameObject.AddComponent<StartCountdown>();
            var so = new SerializedObject(countdown);
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("veil").objectReferenceValue = veil.GetComponent<Image>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : Undo.AddComponent<T>(go);
        }

        static T Replace<T>(GameObject go) where T : Component
        {
            var old = go.GetComponent<T>();
            if (old != null) Undo.DestroyObjectImmediate(old);
            return Undo.AddComponent<T>(go);
        }

        static RectTransform NewCanvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(go, "Setup Furnace Game");
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return (RectTransform)go.transform;
        }

        static RectTransform NewRect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Place(rect, anchor, pivot, position, size);
            return rect;
        }

        static RectTransform NewImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return (RectTransform)go.transform;
        }

        static Text NewText(Transform parent, string name, string text, int size, TextAnchor align, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.alignment = align;
            label.color = color;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);
            return label;
        }

        /// <summary>Radial arc starting at the lower left (−120° from up) and running clockwise.</summary>
        static void MakeArc(Image image, float amount)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Bottom;
            image.fillClockwise = true;
            image.fillAmount = amount;
            image.rectTransform.localEulerAngles = new Vector3(0f, 0f, -60f);
        }

        static RectTransform Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
