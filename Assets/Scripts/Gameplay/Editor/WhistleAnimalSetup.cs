using System.Collections.Generic;
using System.IO;
using CompanyF.Train;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CompanyF.Gameplay.EditorTools
{
    /// <summary>Adds the whistle waves and the animal herd to the Train, and tests their rules without key presses.</summary>
    public static class WhistleAnimalSetup
    {
        const string MaterialPath = "Assets/Materials/WhistleWave.mat";
        const string CallSourceName = "AnimalCallSource";
        static readonly Color RingColor = new Color(2.6f, 1.45f, 0.25f, 0.8f);  // HDR orange-yellow, picked up by Bloom

        [MenuItem("Tools/Game/Setup Whistle And Animals")]
        public static void Setup()
        {
            var train = GameObject.Find("Train");
            if (train == null) { Debug.LogError("[Whistle] No 'Train' object in the scene."); return; }

            var material = LoadOrCreateMaterial();

            var controls = train.GetComponent<TrainControls>();
            Undo.RecordObject(controls, "Setup Whistle");
            controls.whistleKey = UnityEngine.InputSystem.Key.W;
            if (controls.whistle == null) controls.whistle = train.GetComponent<AudioSource>();
            EditorUtility.SetDirty(controls);

            var waves = GetOrAdd<WhistleWaves>(train);
            Link(waves, "controls", controls);
            Link(waves, "ringMaterial", material);

            var herd = GetOrAdd<AnimalHerd>(train);
            Link(herd, "train", train.GetComponent<TrainFollower>());
            Link(herd, "waves", waves);
            Link(herd, "cameraPeek", train.GetComponent<CameraPeek>());

            // Animal calls: their own 2D source under the train, so the whistle never cuts them off.
            var sourceTransform = train.transform.Find(CallSourceName);
            if (sourceTransform == null)
            {
                var go = new GameObject(CallSourceName);
                Undo.RegisterCreatedObjectUndo(go, "Setup Animal Sounds");
                go.transform.SetParent(train.transform, false);
                sourceTransform = go.transform;
            }
            var source = GetOrAdd<AudioSource>(sourceTransform.gameObject);
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            EditorUtility.SetDirty(source);

            var sounds = GetOrAdd<AnimalSounds>(train);
            Link(sounds, "herd", herd);
            Link(sounds, "train", train.GetComponent<TrainFollower>());
            Link(sounds, "source", source);
            Undo.RecordObject(sounds, "Setup Animal Sounds");
            sounds.cowSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/CowSound.wav");
            sounds.horseSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/HorseSound.wav");
            if (sounds.cowSound == null || sounds.horseSound == null) Debug.LogError("[Whistle] Sound clips not found in Assets/Sound.");
            EditorUtility.SetDirty(sounds);

            EditorSceneManager.MarkSceneDirty(train.scene);
            Debug.Log("[Whistle] setup done");
        }

        static Material LoadOrCreateMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;

            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "WhistleWave" };
            material.SetColor("_BaseColor", RingColor);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        /// <summary>Draws two rings around the train from above and from the cab; nothing stays in the scene.</summary>
        [MenuItem("Tools/Game/Render Whistle Wave Preview")]
        public static void RenderPreview()
        {
            var train = GameObject.Find("Train").transform;
            var waves = train.GetComponent<WhistleWaves>();
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var rings = new List<LineRenderer>();
            foreach (var pair in new[] { new Vector2(7.5f, 0.75f), new Vector2(13f, 0.25f) })
            {
                var ring = WhistleWaves.CreateRing(train, material, waves.ringWidth, waves.ringSegments);
                ring.gameObject.hideFlags = HideFlags.HideAndDontSave;
                WhistleWaves.SetRing(ring, pair.x, pair.y, waves.ringSegments, waves.groundHeight);
                rings.Add(ring);
            }

            const int Width = 800, Height = 600;
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var sheet = new Texture2D(Width * 2, Height, TextureFormat.RGB24, false);
            var cameraGo = new GameObject("_WavePreviewCamera", typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var camera = cameraGo.GetComponent<Camera>();
                camera.targetTexture = target;
                camera.fieldOfView = 60f;
                camera.allowHDR = true;
                cameraGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
                Shoot(camera, train.TransformPoint(new Vector3(0f, 22f, -14f)), train.position, target, sheet, 0);
                Shoot(camera, train.TransformPoint(new Vector3(9f, 5f, -14f)), train.TransformPoint(new Vector3(0f, 0f, 2f)), target, sheet, Width);
                sheet.Apply();
                File.WriteAllBytes("Temp/WhistleWavePreview.png", sheet.EncodeToPNG());
                Debug.Log("[Whistle] preview done");
            }
            finally
            {
                foreach (var ring in rings) Object.DestroyImmediate(ring.gameObject);
                RenderTexture.active = null;
                Object.DestroyImmediate(cameraGo);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(sheet);
            }
        }

        static void Shoot(Camera camera, Vector3 from, Vector3 at, RenderTexture target, Texture2D sheet, int x)
        {
            camera.transform.position = from;
            camera.transform.rotation = Quaternion.LookRotation(at - from, Vector3.up);
            camera.Render();
            camera.Render();
            RenderTexture.active = target;
            sheet.ReadPixels(new Rect(0, 0, target.width, target.height), x, 0);
        }

        [MenuItem("Tools/Game/Run Whistle And Animal Self Test")]
        public static void SelfTest()
        {
            const float Dt = 0.01f;
            var log = new List<string>();
            int failures = 0;
            void Check(string what, bool ok)
            {
                log.Add($"{(ok ? "PASS" : "FAIL")}  {what}");
                if (!ok) failures++;
            }

            var root = new GameObject("_WhistleTest") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                // Waves: first at once, then every 0.5 s; each grows 0 to 30 m in 1 s and disappears.
                var waves = root.AddComponent<WhistleWaves>();
                float Largest() { float m = 0f; foreach (var r in waves.ActiveRadii) m = Mathf.Max(m, r); return m; }

                waves.Tick(Dt, false);
                Check($"no whistle, no waves ({waves.ActiveRadii.Count})", waves.ActiveRadii.Count == 0);
                waves.Tick(Dt, true);
                Check($"first wave at once ({waves.ActiveRadii.Count})", waves.ActiveRadii.Count == 1);
                for (int i = 0; i < 48; i++) waves.Tick(Dt, true);
                Check($"0.48 s: still one wave ({waves.ActiveRadii.Count}), radius {Largest():0.0} (expect 14.4)", waves.ActiveRadii.Count == 1 && Mathf.Abs(Largest() - 14.4f) < 0.05f);
                for (int i = 0; i < 3; i++) waves.Tick(Dt, true);
                Check($"0.51 s: second wave ({waves.ActiveRadii.Count})", waves.ActiveRadii.Count == 2);
                for (int i = 0; i < 48; i++) waves.Tick(Dt, true);
                Check($"0.99 s: largest wave about to finish, {Largest():0.0} m", Largest() > 29f && Largest() <= 30f);
                for (int i = 0; i < 3; i++) waves.Tick(Dt, true);
                Check($"1.02 s: first wave gone, new one out, {waves.ActiveRadii.Count} waves, largest {Largest():0.0} m", waves.ActiveRadii.Count == 2 && Largest() < 16f);
                for (int i = 0; i < 110; i++) waves.Tick(Dt, false);
                Check($"key released: no new waves, all gone ({waves.ActiveRadii.Count})", waves.ActiveRadii.Count == 0);
                waves.Tick(Dt, true);
                Check($"pressing again: wave at once ({waves.ActiveRadii.Count})", waves.ActiveRadii.Count == 1);

                // Animals. "In view" is switched by hand here; the frustum itself is checked in Play Mode.
                var herd = root.AddComponent<AnimalHerd>();
                Check($"default times: charge {herd.chargeTime} s, flee {herd.fleeTime} s", Mathf.Approximately(herd.chargeTime, 0.5f) && Mathf.Approximately(herd.fleeTime, 0.3f));
                herd.chargeTime = 2f;  // longer than the defaults, so the steps below can be checked part-way
                herd.fleeTime = 1f;
                AnimalHerd.Animal NewAnimal(float x, float z, float rotationY)
                {
                    var go = new GameObject("Animal") { hideFlags = HideFlags.HideAndDontSave };
                    go.transform.SetParent(root.transform, false);
                    go.transform.position = new Vector3(x, 0f, z);
                    go.transform.localEulerAngles = new Vector3(0f, rotationY, 0f);
                    return herd.Add(go.transform);
                }

                bool visible = false;
                var none = new List<float>();
                void Run(int ticks, Vector3 trainAt, IReadOnlyList<float> radii) { for (int i = 0; i < ticks; i++) herd.Tick(Dt, trainAt, trainAt, _ => visible, radii); }
                float X(AnimalHerd.Animal a) => a.transform.position.x;
                float Yaw(AnimalHerd.Animal a) => Mathf.DeltaAngle(0f, a.transform.localEulerAngles.y);

                var left = NewAnimal(-40f, 100f, 90f);
                var far = Vector3.zero;
                visible = true;
                Run(10, far, none);
                Check($"100 m away and in view: waits ({left.state})", left.state == AnimalHerd.AnimalState.Waiting);
                visible = false;
                var near = new Vector3(0f, 0f, 60f);
                Run(10, near, none);
                Check($"near but out of view: waits ({left.state})", left.state == AnimalHerd.AnimalState.Waiting);

                Run(10, near, new List<float> { 100f });
                Check($"a wave does nothing before it charges ({left.state}, x={X(left):0.0}, rot={Yaw(left):0})", left.state == AnimalHerd.AnimalState.Waiting && Mathf.Approximately(X(left), -40f) && Mathf.Approximately(Yaw(left), 90f));

                visible = true;
                Run(1, near, none);
                Check($"near and in view: charges ({left.state})", left.state == AnimalHerd.AnimalState.Charging);
                Run(100, near, none);
                Check($"halfway at 1 s: x={X(left):0.0} (expect -20)", Mathf.Abs(X(left) + 20f) < 0.5f);
                Run(101, near, none);
                Check($"on the track at 2 s: x={X(left):0.00}", Mathf.Abs(X(left)) < 0.01f);

                // A wave short of the animal does nothing; one that reaches it turns it around.
                var farAlong = new Vector3(0f, 0f, 90f);  // 10 m along the track
                Run(10, farAlong, new List<float> { 5f });
                Check($"wave stops short (5 m of 10 m): keeps going ({left.state})", left.state == AnimalHerd.AnimalState.Charging);
                Run(1, farAlong, new List<float> { 11f });
                Check($"wave reaches it: flees, Rotation Y {Yaw(left):0} (expect -90)", left.state == AnimalHerd.AnimalState.Fleeing && Mathf.Approximately(Yaw(left), -90f));
                Run(50, farAlong, none);
                Check($"halfway back at 0.5 s: x={X(left):0.0} (expect -20)", Mathf.Abs(X(left) + 20f) < 1f);
                Run(51, farAlong, none);
                Check($"back at the start after 1 s: x={X(left):0.00}, {left.state}", Mathf.Approximately(X(left), -40f) && left.state == AnimalHerd.AnimalState.Done);
                Run(300, near, new List<float> { 100f });
                Check($"stays there, never charges again: x={X(left):0.00}, {left.state}, rot={Yaw(left):0}", Mathf.Approximately(X(left), -40f) && left.state == AnimalHerd.AnimalState.Done && Mathf.Approximately(Yaw(left), -90f));

                // Mid-charge: the other side, -90 becomes 90, and it returns from where it is.
                var right = NewAnimal(30f, 200f, 270f);
                var nearRight = new Vector3(0f, 0f, 190f);
                Run(100, nearRight, none);
                float midX = X(right);
                Run(1, nearRight, new List<float> { 40f });
                Check($"hit mid-charge at x={midX:0.0}: turns from where it is, x={X(right):0.0}, Rotation Y {Yaw(right):0} (expect 90)",
                      right.state == AnimalHerd.AnimalState.Fleeing && Mathf.Abs(X(right) - midX) < 0.3f && Mathf.Approximately(Yaw(right), 90f));
                Run(101, nearRight, none);
                Check($"back at its own start: x={X(right):0.00}", Mathf.Approximately(X(right), 30f) && right.state == AnimalHerd.AnimalState.Done);

                // Charging is measured from the nose; the wave is still measured from the train's centre.
                var ahead = NewAnimal(0f, 500f, 90f);
                visible = true;
                herd.Tick(Dt, new Vector3(0f, 0f, 400f), new Vector3(0f, 0f, 425f), _ => visible, none);
                Check($"100 m from the nose: waits, even with the centre 75 m away ({ahead.state})", ahead.state == AnimalHerd.AnimalState.Waiting);
                herd.Tick(Dt, new Vector3(0f, 0f, 425f), new Vector3(0f, 0f, 400f), _ => visible, none);
                Check($"75 m from the nose: charges, even with the centre 100 m away ({ahead.state})", ahead.state == AnimalHerd.AnimalState.Charging);
                herd.Tick(Dt, new Vector3(0f, 0f, 495f), new Vector3(0f, 0f, 480f), _ => visible, new List<float> { 10f });
                Check($"wave reach counts from the centre: 20 m away, 10 m wave, no hit ({ahead.state})", ahead.state == AnimalHerd.AnimalState.Charging);

                // Calls: only an animal running right now, once per run.
                var calls = new List<AnimalHerd.Animal>
                {
                    new AnimalHerd.Animal { state = AnimalHerd.AnimalState.Waiting },
                    new AnimalHerd.Animal { state = AnimalHerd.AnimalState.Fleeing, timer = 0.1f },
                    new AnimalHerd.Animal { state = AnimalHerd.AnimalState.Done },
                    new AnimalHerd.Animal { state = AnimalHerd.AnimalState.Charging, timer = 0.6f },
                };
                Check("no call for waiting, fleeing, done or already-arrived animals", AnimalSounds.PickNext(calls, 0.5f) == null);
                var runner = new AnimalHerd.Animal { state = AnimalHerd.AnimalState.Charging, timer = 0.2f };
                calls.Add(runner);
                Check("a running animal gets a call", AnimalSounds.PickNext(calls, 0.5f) == runner);
                runner.hasCalled = true;
                Check("it does not call twice in one run", AnimalSounds.PickNext(calls, 0.5f) == null);
                runner.hasCalled = false;
                runner.timer = 0.5f;
                Check("an animal that arrived during another call is not called afterwards", AnimalSounds.PickNext(calls, 0.5f) == null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            log.Sort((a, b) => b.StartsWith("FAIL").CompareTo(a.StartsWith("FAIL")));
            Debug.Log($"[Whistle SelfTest] {(failures == 0 ? "ALL PASSED" : failures + " FAILED")}\n" + string.Join("\n", log));
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : Undo.AddComponent<T>(go);
        }

        static void Link(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }
    }
}
