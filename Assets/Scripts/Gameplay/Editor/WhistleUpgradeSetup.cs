using System;
using CompanyF.Train;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CompanyF.Gameplay.EditorTools
{
    public static class WhistleUpgradeSetup
    {
        [MenuItem("Tools/Game/Setup Whistle Smoke And Range")]
        public static void Setup()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/SampleScene.unity" || EditorApplication.isPlaying)
                throw new InvalidOperationException("Open SampleScene in Edit Mode first.");
            var train = GameObject.Find("Train");
            var controls = train.GetComponent<TrainControls>();
            var waves = train.GetComponent<WhistleWaves>();
            Undo.RecordObject(controls, "Whistle cooldown");
            Undo.RecordObject(waves, "Whistle range");
            controls.whistleCooldown = 3f;
            waves.maxRadius = 30f;
            EditorUtility.SetDirty(controls);
            EditorUtility.SetDirty(waves);

            var model = train.transform.Find("Steam_Locomotive").GetComponent<MeshFilter>();
            Vector3 chimney = FindChimney(model, train.transform);
            var child = train.transform.Find("WhistleSmoke");
            if (child == null)
            {
                var go = new GameObject("WhistleSmoke");
                Undo.RegisterCreatedObjectUndo(go, "Whistle smoke");
                go.transform.SetParent(train.transform, false);
                child = go.transform;
            }
            child.localPosition = chimney + Vector3.up * 0.05f;
            child.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var particles = child.GetComponent<ParticleSystem>();
            if (particles == null) particles = Undo.AddComponent<ParticleSystem>(child.gameObject);
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false; main.loop = true; main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.startColor = new Color(0.78f, 0.8f, 0.82f, 0.7f);
            main.maxParticles = 180;
            var emission = particles.emission; emission.enabled = true; emission.rateOverTime = 35f;
            var shape = particles.shape; shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 12f; shape.radius = 0.18f;
            var size = particles.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 2.5f));
            var colour = particles.colorOverLifetime; colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0.5f, 0.3f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
            var noise = particles.noise; noise.enabled = true; noise.strength = 0.35f; noise.frequency = 0.6f;
            var sheet = particles.textureSheetAnimation;
            sheet.enabled = true; sheet.numTilesX = 6; sheet.numTilesY = 6;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            sheet.cycleCount = 1;

            const string materialPath = "Assets/Materials/WhistleSmoke.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UnityTechnologies/ParticlePack/EffectExamples/Misc Effects/Textures/SmokePuff.png");
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f); material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent"); material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            var smoke = train.GetComponent<WhistleSmoke>();
            if (smoke == null) smoke = Undo.AddComponent<WhistleSmoke>(train);
            var so = new SerializedObject(smoke);
            so.FindProperty("controls").objectReferenceValue = controls;
            so.FindProperty("train").objectReferenceValue = train.GetComponent<TrainFollower>();
            so.FindProperty("smoke").objectReferenceValue = particles;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save scene.");
            Debug.Log($"[Whistle Upgrade] cooldown=3, radius=30, chimney local={chimney:F3}");
        }

        static Vector3 FindChimney(MeshFilter model, Transform train)
        {
            using (var data = MeshUtility.AcquireReadOnlyMeshData(model.sharedMesh))
            using (var vertices = new NativeArray<Vector3>(data[0].vertexCount, Allocator.Temp))
            {
                data[0].GetVertices(vertices);
                float top = float.NegativeInfinity;
                foreach (var vertex in vertices)
                {
                    var p = train.InverseTransformPoint(model.transform.TransformPoint(vertex));
                    if (p.z > 3f && Mathf.Abs(p.x) < 0.7f) top = Mathf.Max(top, p.y);
                }
                Vector3 sum = Vector3.zero; int count = 0;
                foreach (var vertex in vertices)
                {
                    var p = train.InverseTransformPoint(model.transform.TransformPoint(vertex));
                    if (p.z > 3f && Mathf.Abs(p.x) < 0.7f && p.y >= top - 0.025f) { sum += p; count++; }
                }
                if (count == 0) throw new InvalidOperationException("Could not locate chimney top.");
                return sum / count;
            }
        }

        public static void BatchSetup()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            Setup();
            VerifySmoke();
            GameFlowSetup.SelfTest();
            WhistleAnimalSetup.SelfTest();
            ViewInputSelfTest.Run();
            GameSoundEffectsVerification.RunBatch();
        }

        public static void VerifySmoke()
        {
            var train = GameObject.Find("Train");
            var smoke = train.GetComponent<WhistleSmoke>();
            var ps = train.transform.Find("WhistleSmoke").GetComponent<ParticleSystem>();
            if (ps.main.playOnAwake || ps.GetComponent<ParticleSystemRenderer>().sharedMaterial.mainTexture == null)
                throw new InvalidOperationException("Smoke must start disabled with a texture.");
            smoke.Tick(true);
            ps.Simulate(0.5f, true, false, true);
            if (ps.particleCount == 0) throw new InvalidOperationException("Smoke did not emit.");
            smoke.Tick(false);
            if (ps.isEmitting) throw new InvalidOperationException("Smoke did not stop emitting.");
            ps.Simulate(3f, true, false, true);
            if (ps.particleCount != 0) throw new InvalidOperationException("Smoke did not dissipate.");
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Debug.Log("[Whistle Upgrade] PASS: smoke emits on whistle, stops emitting and dissipates afterwards.");
        }
    }
}
