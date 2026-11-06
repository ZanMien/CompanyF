using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CompanyF.Train.EditorTools
{
    /// <summary>
    /// Replaces the two switched cameras with one moving camera: CameraPoint_1 / CameraPoint_2 copy the cameras' exact
    /// local poses, Camera1 stays as the only camera, Camera2 and the old switcher are removed.
    /// Also verifies the views against renders taken before the change, and tests the movement rules.
    /// </summary>
    public static class CameraPeekSetup
    {
        const string BaselineDir = "Temp/CameraBaseline";
        const int Width = 960, Height = 540;

        /// <summary>Renders both existing cameras so the views can be compared after the change.</summary>
        [MenuItem("Tools/Train/Camera Peek/1 Save View Baseline")]
        public static void SaveBaseline()
        {
            var train = GameObject.Find("Train").transform;
            Directory.CreateDirectory(BaselineDir);
            File.WriteAllBytes($"{BaselineDir}/1.png", Shoot(train.Find("Camera1").GetComponent<Camera>()));
            File.WriteAllBytes($"{BaselineDir}/2.png", Shoot(train.Find("Camera2").GetComponent<Camera>()));
            Debug.Log("[CameraPeek] baseline saved");
        }

        [MenuItem("Tools/Train/Camera Peek/2 Setup")]
        public static void Setup()
        {
            var train = GameObject.Find("Train");
            var camera1 = train.transform.Find("Camera1");
            var camera2 = train.transform.Find("Camera2");
            if (camera1 == null || camera2 == null)
            {
                Debug.LogError("[CameraPeek] Train/Camera1 and Train/Camera2 are needed; setup has probably already run.");
                return;
            }

            var point1 = NewPoint(train.transform, "CameraPoint_1", camera1);
            var point2 = NewPoint(train.transform, "CameraPoint_2", camera2);

            var peek = Undo.AddComponent<CameraPeek>(train);
            var cameraComponent = camera1.GetComponent<Camera>();
            peek.point1FieldOfView = cameraComponent.fieldOfView;
            peek.point2FieldOfView = camera2.GetComponent<Camera>().fieldOfView;
            var so = new SerializedObject(peek);
            so.FindProperty("peekCamera").objectReferenceValue = cameraComponent;
            so.FindProperty("point1").objectReferenceValue = point1;
            so.FindProperty("point2").objectReferenceValue = point2;
            so.ApplyModifiedProperties();

            Undo.DestroyObjectImmediate(camera2.gameObject);
            cameraComponent.enabled = true;
            EditorUtility.SetDirty(cameraComponent);

            EditorSceneManager.MarkSceneDirty(train.scene);
            Debug.Log("[CameraPeek] setup done");
        }

        /// <summary>Compares the moving camera at both points with the baseline; everything is put back afterwards.</summary>
        [MenuItem("Tools/Train/Camera Peek/3 Verify Views")]
        public static void Verify()
        {
            var train = GameObject.Find("Train");
            var peek = train.GetComponent<CameraPeek>();
            var so = new SerializedObject(peek);
            var camera = (Camera)so.FindProperty("peekCamera").objectReferenceValue;
            var transform = camera.transform;
            var position = transform.localPosition;
            var rotation = transform.localRotation;
            float fov = camera.fieldOfView;
            try
            {
                for (int i = 1; i <= 2; i++)
                {
                    var point = (Transform)so.FindProperty($"point{i}").objectReferenceValue;
                    transform.localPosition = point.localPosition;
                    transform.localRotation = point.localRotation;
                    camera.fieldOfView = i == 1 ? peek.point1FieldOfView : peek.point2FieldOfView;
                    var png = Shoot(camera);
                    File.WriteAllBytes($"{BaselineDir}/{i}_after.png", png);
                    Debug.Log($"[CameraPeek] point {i}: local={point.localPosition:F4} rot={point.localEulerAngles:F3} fov={camera.fieldOfView} {Compare(File.ReadAllBytes($"{BaselineDir}/{i}.png"), png)}");
                }
            }
            finally
            {
                transform.localPosition = position;
                transform.localRotation = rotation;
                camera.fieldOfView = fov;
            }
        }

        /// <summary>
        /// Walks the path through Train/CameraPoint_3 and measures the distance to the locomotive's colliders. Only when
        /// it stays clear of them is the point linked to the camera. Also draws views along the path to Temp/.
        /// </summary>
        [MenuItem("Tools/Train/Camera Peek/4 Check And Link Via Point")]
        public static void CheckAndLinkVia()
        {
            var train = GameObject.Find("Train").transform;
            var via = train.Find("CameraPoint_3");
            if (via == null) { Debug.LogError("[CameraPeek] Train/CameraPoint_3 not found."); return; }

            var peek = train.GetComponent<CameraPeek>();
            var so = new SerializedObject(peek);
            var camera = (Camera)so.FindProperty("peekCamera").objectReferenceValue;
            var viaProperty = so.FindProperty("viaPoint");
            var previousVia = viaProperty.objectReferenceValue;
            viaProperty.objectReferenceValue = via;
            so.ApplyModifiedPropertiesWithoutUndo();

            var report = new System.Text.StringBuilder($"[CameraPeek] via local={via.localPosition:F3} rot={via.localEulerAngles:F2} scale={via.localScale:F2}\n");
            Physics.SyncTransforms();
            bool backfaces = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            float worst = float.MaxValue, worstAt = 0f;
            float length = 0f;
            var previous = train.TransformPoint(peek.PositionAt(0f));
            for (int i = 0; i <= 200; i++)
            {
                float u = i / 200f;
                var point = train.TransformPoint(peek.PositionAt(u));
                length += Vector3.Distance(previous, point);
                previous = point;
                float clearance = ClearanceAt(point, train);
                if (clearance < worst) { worst = clearance; worstAt = u; }
            }
            Physics.queriesHitBackfaces = backfaces;

            float near = camera.nearClipPlane;
            bool clear = worst > 0f;
            report.AppendLine($"path length={length:F2} smallest clearance={worst:0.00} at {worstAt:P0} (near clip {near}) {(clear ? "CLEAR" : "HITS THE CAB")}");
            report.AppendLine($"via rotation is not used; rotation and field of view blend between the two end points.");

            // Views along the path (the pose is put back afterwards).
            var pose = camera.transform;
            var position = pose.localPosition;
            var rotation = pose.localRotation;
            float fov = camera.fieldOfView;
            const int CellW = 640, CellH = 360;
            var amounts = new[] { 0.25f, 0.5f, 0.75f };
            var sheet = new Texture2D(CellW * amounts.Length, CellH, TextureFormat.RGB24, false);
            var target = new RenderTexture(CellW, CellH, 24, RenderTextureFormat.ARGB32);
            var previousTarget = camera.targetTexture;
            var point1 = (Transform)so.FindProperty("point1").objectReferenceValue;
            var point2 = (Transform)so.FindProperty("point2").objectReferenceValue;
            try
            {
                camera.targetTexture = target;
                for (int i = 0; i < amounts.Length; i++)
                {
                    pose.localPosition = peek.PositionAt(amounts[i]);
                    pose.localRotation = Quaternion.Slerp(point1.localRotation, point2.localRotation, amounts[i]);
                    camera.fieldOfView = Mathf.Lerp(peek.point1FieldOfView, peek.point2FieldOfView, amounts[i]);
                    camera.Render();
                    camera.Render();
                    RenderTexture.active = target;
                    sheet.ReadPixels(new Rect(0, 0, CellW, CellH), i * CellW, 0);
                }
                sheet.Apply();
                File.WriteAllBytes("Temp/CameraPathPreview.png", sheet.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = null;
                camera.targetTexture = previousTarget;
                pose.localPosition = position;
                pose.localRotation = rotation;
                camera.fieldOfView = fov;
                UnityEngine.Object.DestroyImmediate(sheet);
                UnityEngine.Object.DestroyImmediate(target);
            }

            if (clear)
            {
                EditorSceneManager.MarkSceneDirty(train.gameObject.scene);
                report.AppendLine("linked");
            }
            else
            {
                viaProperty.objectReferenceValue = previousVia;
                so.ApplyModifiedPropertiesWithoutUndo();
                report.AppendLine("not linked");
            }
            File.WriteAllText("Temp/CameraPathCheck.txt", report.ToString());
            Debug.Log(report.ToString());
        }

        static float ClearanceAt(Vector3 point, Transform train)
        {
            foreach (var radius in new[] { 0.02f, 0.05f, 0.1f, 0.15f, 0.2f, 0.25f, 0.3f, 0.4f, 0.5f })
                foreach (var collider in Physics.OverlapSphere(point, radius))
                    if (collider.transform.IsChildOf(train))
                        return radius <= 0.02f ? 0f : radius;
            return 1f;
        }

        [MenuItem("Tools/Train/Camera Peek/Run Self Test")]
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

            var root = new GameObject("_CameraPeekTest") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var cameraGo = new GameObject("Camera", typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave };
                cameraGo.transform.SetParent(root.transform, false);
                var a = new GameObject("A") { hideFlags = HideFlags.HideAndDontSave }.transform;
                var b = new GameObject("B") { hideFlags = HideFlags.HideAndDontSave }.transform;
                a.SetParent(root.transform, false);
                b.SetParent(root.transform, false);
                a.localPosition = Vector3.zero;
                b.localPosition = new Vector3(2f, 0f, 0f);
                b.localRotation = Quaternion.Euler(0f, 90f, 0f);

                var peek = root.AddComponent<CameraPeek>();
                var so = new SerializedObject(peek);
                so.FindProperty("peekCamera").objectReferenceValue = cameraGo.GetComponent<Camera>();
                so.FindProperty("point1").objectReferenceValue = a;
                so.FindProperty("point2").objectReferenceValue = b;
                so.ApplyModifiedPropertiesWithoutUndo();
                peek.moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
                peek.point1FieldOfView = 55f;
                peek.point2FieldOfView = 85f;
                var camera = cameraGo.GetComponent<Camera>();
                float X() => cameraGo.transform.localPosition.x;

                void Run(int ticks, bool left = false, bool right = false)
                {
                    peek.Tick(Dt, left, right);
                    for (int i = 1; i < ticks; i++) peek.Tick(Dt, false, false);
                }

                peek.ApplyPose();
                Check($"start at point 1: x={X():0.000}, fov={camera.fieldOfView}", Mathf.Approximately(X(), 0f) && Mathf.Approximately(camera.fieldOfView, 55f));
                Run(10, left: true);
                Check($"← at point 1 does nothing: x={X():0.000}", Mathf.Approximately(X(), 0f));

                Run(10, right: true);
                float early = X();
                Run(40);
                float half = X();
                Check($"ease in: 10% of the time covers little ({early:0.000} of 2)", early > 0f && early < 0.1f);
                Check($"halfway at 0.5 s: x={half:0.000}, fov={camera.fieldOfView:0.0}, rot={cameraGo.transform.localEulerAngles.y:0.0}",
                      Mathf.Abs(half - 1f) < 0.02f && Mathf.Abs(camera.fieldOfView - 70f) < 0.5f && Mathf.Abs(cameraGo.transform.localEulerAngles.y - 45f) < 1f);

                Run(1, left: true);
                Check($"← mid-move turns back from where it is: x={X():0.000}", Mathf.Abs(X() - half) < 0.05f);
                int ticksBack = 1;
                while (peek.Progress > 0f && ticksBack < 500) { peek.Tick(Dt, false, false); ticksBack++; }
                Check($"turning back at the halfway point takes 0.5 s ({ticksBack * Dt:0.00} s)", Mathf.Abs(ticksBack * Dt - 0.5f) < 0.03f);

                Run(100, right: true);
                Check($"full move takes 1 s: x={X():0.000}, fov={camera.fieldOfView}", Mathf.Approximately(X(), 2f) && Mathf.Approximately(camera.fieldOfView, 85f));
                Run(10, right: true);
                Check($"→ at point 2 does nothing: x={X():0.000}", Mathf.Approximately(X(), 2f));
                Run(100, left: true);
                Check($"← returns to point 1 in 1 s: x={X():0.000}", Mathf.Abs(X()) < 0.001f);

                // With a via point the path is one curve through it, with the ends unchanged.
                var via = new GameObject("Via") { hideFlags = HideFlags.HideAndDontSave }.transform;
                via.SetParent(root.transform, false);
                via.localPosition = new Vector3(1f, 1f, 0f);
                so.FindProperty("viaPoint").objectReferenceValue = via;
                so.ApplyModifiedPropertiesWithoutUndo();
                Check($"via: start {peek.PositionAt(0f):F2}, middle {peek.PositionAt(0.5f):F2}, end {peek.PositionAt(1f):F2}",
                      Vector3.Distance(peek.PositionAt(0f), a.localPosition) < 0.001f
                      && Vector3.Distance(peek.PositionAt(0.5f), via.localPosition) < 0.001f
                      && Vector3.Distance(peek.PositionAt(1f), b.localPosition) < 0.001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            log.Sort((x, y) => y.StartsWith("FAIL").CompareTo(x.StartsWith("FAIL")));
            Debug.Log($"[CameraPeek SelfTest] {(failures == 0 ? "ALL PASSED" : failures + " FAILED")}\n" + string.Join("\n", log));
        }

        static Transform NewPoint(Transform train, string name, Transform source)
        {
            var existing = train.Find(name);
            var point = existing != null ? existing : new GameObject(name).transform;
            if (existing == null)
            {
                Undo.RegisterCreatedObjectUndo(point.gameObject, "Setup Camera Peek");
                point.SetParent(train, false);
            }
            point.localPosition = source.localPosition;
            point.localRotation = source.localRotation;
            point.localScale = Vector3.one;
            return point;
        }

        static byte[] Shoot(Camera camera)
        {
            bool enabled = camera.enabled;
            var previous = camera.targetTexture;
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            try
            {
                camera.enabled = true;
                camera.targetTexture = target;
                camera.Render();
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();
                return image.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = null;
                camera.targetTexture = previous;
                camera.enabled = enabled;
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        static string Compare(byte[] before, byte[] after)
        {
            var a = new Texture2D(2, 2);
            var b = new Texture2D(2, 2);
            try
            {
                a.LoadImage(before);
                b.LoadImage(after);
                var pa = a.GetPixels32();
                var pb = b.GetPixels32();
                if (pa.Length != pb.Length) return "SIZE DIFFERS";
                int changed = 0, worst = 0;
                for (int i = 0; i < pa.Length; i++)
                {
                    int delta = Math.Max(Math.Abs(pa[i].r - pb[i].r), Math.Max(Math.Abs(pa[i].g - pb[i].g), Math.Abs(pa[i].b - pb[i].b)));
                    if (delta > 2) changed++;
                    worst = Math.Max(worst, delta);
                }
                return $"view vs baseline: {changed}/{pa.Length} pixels differ by more than 2, worst {worst}/255";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(a);
                UnityEngine.Object.DestroyImmediate(b);
            }
        }
    }
}
