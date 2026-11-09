using System;
using CompanyF.Train;
using UnityEditor;
using UnityEngine;

namespace CompanyF.Gameplay.EditorTools
{
    public static class ViewInputSelfTest
    {
        public static void RunBatch()
        {
            Run();
            FurnaceSelfTest.Run();
            GameFlowSetup.SelfTest();
            GameSoundEffectsVerification.RunBatch();
        }

        [MenuItem("Tools/Game/Run View Input Self Test")]
        public static void Run()
        {
            var root = new GameObject("_ViewInputTest") { hideFlags = HideFlags.HideAndDontSave };
            void Check(bool ok, string message)
            {
                if (!ok) throw new InvalidOperationException("[View Input] " + message);
                Debug.Log("[View Input] PASS: " + message);
            }
            try
            {
                var peek = root.AddComponent<CameraPeek>();
                var furnace = root.AddComponent<FurnaceController>();
                var controls = root.AddComponent<TrainControls>();
                int shovels = 0, failures = 0;
                furnace.ShovelStarted += () => shovels++;
                furnace.P2Failed += () => failures++;
                Check(peek.IsFurnaceView && furnace.CanOperate && !controls.CanWhistle, "initial furnace view permissions");
                controls.TickWhistle(0f, true);
                Check(!controls.IsWhistling, "furnace view rejects whistle");
                peek.Tick(peek.moveTime / 2f, false, true);
                furnace.Tick(0f, true, true);
                Check(!furnace.CanOperate && !controls.CanWhistle && furnace.State == DoorState.Closed
                    && shovels == 0 && failures == 0, "transition rejects both inputs without a shovel failure");
                peek.Tick(peek.moveTime, false, false);
                Check(peek.IsOutsideView && !furnace.CanOperate && controls.CanWhistle, "outside view permissions");
                furnace.Tick(0f, true, true);
                Check(furnace.State == DoorState.Closed && shovels == 0 && failures == 0, "outside rejects door and coal");
                controls.TickWhistle(0f, true);
                Check(!controls.IsWhistling, "held whistle does not trigger merely by changing view");
                controls.TickWhistle(0f, false);
                controls.TickWhistle(0f, true);
                Check(controls.IsWhistling, "fresh outside whistle press works");
                peek.Tick(0f, true, false);
                controls.TickWhistle(0f, true);
                Check(!controls.IsWhistling && controls.Whistle == TrainControls.WhistleState.Cooldown,
                    "leaving outside view ends whistle and starts cooldown");
                peek.Tick(peek.moveTime, false, false);
                furnace.Tick(0f, true, false);
                Check(furnace.State == DoorState.Opening, "furnace view accepts door");
                peek.Tick(peek.moveTime, false, true);
                furnace.Tick(furnace.openTime, true, true);
                Check(furnace.State == DoorState.Open && shovels == 0, "door animation continues outside without accepting extra input");
                peek.Tick(peek.moveTime, true, false);
                furnace.Tick(0f, false, true);
                Check(furnace.Action == ShovelAction.Throw && shovels == 1, "furnace view accepts coal");
                peek.Tick(peek.moveTime, false, true);
                furnace.Tick(furnace.loadTime, false, false);
                Check(furnace.Action == ShovelAction.None, "existing throw completes after switching view");
                Debug.Log("[View Input] ALL PASSED");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
