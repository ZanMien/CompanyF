using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CompanyF.Gameplay.EditorTools
{
    /// <summary>
    /// Checks the shovel and door animations without key presses: poses them at key moments in edit mode, compares
    /// the coal with the ColePoints, draws a filmstrip to Temp/ and puts every transform back afterwards.
    /// </summary>
    public static class FurnaceAnimationPreview
    {
        const string SheetPath = "Temp/FurnaceAnimationPreview.png";
        const float PointTolerance = 0.002f;

        struct Shot
        {
            public string label;
            public ShovelAction action;
            public float time;
            public float doorOpen;
            public Transform expected;

            public Shot(string label, ShovelAction action, float time, float doorOpen, Transform expected = null)
            {
                this.label = label;
                this.action = action;
                this.time = time;
                this.doorOpen = doorOpen;
                this.expected = expected;
            }
        }

        [MenuItem("Tools/Game/Render Furnace Animation Preview")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[FurnaceAnim] Run this outside Play Mode."); return; }

            var train = GameObject.Find("Train").transform;
            var animator = train.GetComponent<ShovelAnimator>();
            var doorAnimator = train.GetComponent<FurnaceDoorAnimator>();
            var so = new SerializedObject(animator);
            var shovel = (Transform)so.FindProperty("shovel").objectReferenceValue;
            var coal = (Transform)so.FindProperty("coal").objectReferenceValue;
            var landing = (Transform)so.FindProperty("landingPoint").objectReferenceValue;
            var apex = (Transform)so.FindProperty("apexPoint").objectReferenceValue;
            var blocked = (Transform)so.FindProperty("blockedPoint").objectReferenceValue;
            var doorSo = new SerializedObject(doorAnimator);
            var door1 = (Transform)doorSo.FindProperty("door1").objectReferenceValue;
            var door2 = (Transform)doorSo.FindProperty("door2").objectReferenceValue;

            var shovelPos = shovel.localPosition;
            var shovelRot = shovel.localRotation;
            var coalPos = coal.localPosition;
            var coalRot = coal.localRotation;
            var door1Rot = door1.localRotation;
            var door2Rot = door2.localRotation;

            var shots = new[]
            {
                new Shot("rest", ShovelAction.None, 0f, 0f),
                new Shot("throw 0.25", ShovelAction.Throw, 0.25f, 1f),
                new Shot("throw 0.35", ShovelAction.Throw, 0.35f, 1f),
                new Shot("throw 0.42 apex", ShovelAction.Throw, 0.42f, 1f, apex),
                new Shot("throw 0.50 land", ShovelAction.Throw, 0.4999f, 1f, landing),
                new Shot("blocked 0.42 apex", ShovelAction.FailBlocked, 0.42f, 0.5f, apex),
                new Shot("blocked 0.50", ShovelAction.FailBlocked, 0.5f, 0.1f, blocked),
                new Shot("blocked 0.74", ShovelAction.FailBlocked, 0.74f, 0f, blocked),
                new Shot("tip 0.25", ShovelAction.FailDoorNotOpen, 0.25f, 0f),
                new Shot("tip 0.50", ShovelAction.FailDoorNotOpen, 0.5f, 0f),
                new Shot("tip 0.74", ShovelAction.FailDoorNotOpen, 0.74f, 0f),
                new Shot("door half", ShovelAction.None, 0f, 0.5f),
            };

            const int Cell = 480, Columns = 4;
            int rows = Mathf.CeilToInt(shots.Length / (float)Columns);
            var target = new RenderTexture(Cell, Cell, 24, RenderTextureFormat.ARGB32);
            var sheet = new Texture2D(Cell * Columns, Cell * rows, TextureFormat.RGB24, false);
            var cameraGo = new GameObject("_FurnacePreviewCamera", typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave };
            var report = new StringBuilder("[FurnaceAnim] preview\n");
            int bad = 0;
            try
            {
                var camera = cameraGo.GetComponent<Camera>();
                camera.targetTexture = target;
                camera.nearClipPlane = 0.02f;
                camera.fieldOfView = 60f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.45f, 0.5f, 0.55f);
                cameraGo.transform.position = train.TransformPoint(new Vector3(1.05f, 2.45f, -4.75f));
                cameraGo.transform.rotation = Quaternion.LookRotation(train.TransformPoint(new Vector3(0f, 1.6f, -3.3f)) - cameraGo.transform.position, Vector3.up);

                animator.CaptureRestPose();
                for (int i = 0; i < shots.Length; i++)
                {
                    var shot = shots[i];
                    animator.ResetPose();
                    if (shot.action != ShovelAction.None) animator.PoseAt(shot.action, shot.time);
                    door1.localEulerAngles = new Vector3(0f, Mathf.LerpAngle(0f, doorAnimator.door1OpenY, shot.doorOpen), 0f);
                    door2.localEulerAngles = new Vector3(0f, Mathf.LerpAngle(0f, doorAnimator.door2OpenY, shot.doorOpen), 0f);

                    var coalLocal = train.InverseTransformPoint(coal.position);
                    string note = "";
                    if (shot.expected != null)
                    {
                        float gap = Vector3.Distance(coal.position, shot.expected.position);
                        note = $" gap to {shot.expected.name}={gap:0.0000}";
                        if (gap > PointTolerance) { note += " BAD"; bad++; }
                    }
                    report.AppendLine($"{shot.label}: shovel pos={shovel.localPosition:F3} rot={shovel.localEulerAngles:F1} coal(train)={coalLocal:F3}{note}");

                    camera.Render();
                    camera.Render();
                    RenderTexture.active = target;
                    sheet.ReadPixels(new Rect(0, 0, Cell, Cell), i % Columns * Cell, (rows - 1 - i / Columns) * Cell);
                }
                sheet.Apply();
                File.WriteAllBytes(SheetPath, sheet.EncodeToPNG());
            }
            finally
            {
                shovel.localPosition = shovelPos;
                shovel.localRotation = shovelRot;
                coal.localPosition = coalPos;
                coal.localRotation = coalRot;
                door1.localRotation = door1Rot;
                door2.localRotation = door2Rot;
                RenderTexture.active = null;
                Object.DestroyImmediate(cameraGo);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(sheet);
            }

            report.Insert(0, bad == 0 ? "POINTS OK\n" : $"{bad} POINTS OFF\n");
            File.WriteAllText("Temp/FurnaceAnimationPreview.txt", report.ToString());
            Debug.Log($"[FurnaceAnim] preview {(bad == 0 ? "points ok" : bad + " points off")}");
        }
    }
}
