using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CompanyF.Gameplay.EditorTools
{
    /// <summary>
    /// Runs the furnace rules on temporary objects with simulated key presses and logs PASS/FAIL (failures first):
    /// door states and timing, coal throw, both failures and the lockout, cooling, overheat and the
    /// temperature-to-speed mapping.
    /// </summary>
    public static class FurnaceSelfTest
    {
        const float Dt = 0.01f;

        [MenuItem("Tools/Game/Run Furnace Self Test")]
        public static void Run()
        {
            var go = new GameObject("_FurnaceSelfTest") { hideFlags = HideFlags.HideAndDontSave };
            var log = new List<string>();
            int failures = 0;

            void Check(string what, bool ok)
            {
                log.Add($"{(ok ? "PASS" : "FAIL")}  {what}");
                if (!ok) failures++;
            }

            try
            {
                var boiler = go.AddComponent<BoilerTemperature>();
                boiler.Change(0f);  // Awake does not run in edit mode; this clamps to the 50 °C floor
                var furnace = go.AddComponent<FurnaceController>();
                var so = new SerializedObject(furnace);
                so.FindProperty("boiler").objectReferenceValue = boiler;
                so.ApplyModifiedPropertiesWithoutUndo();
                var speed = go.AddComponent<TemperatureSpeed>();
                var sso = new SerializedObject(speed);
                sso.FindProperty("boiler").objectReferenceValue = boiler;
                sso.ApplyModifiedPropertiesWithoutUndo();

                int fails = 0, coals = 0, cools = 0;
                furnace.P2Failed += () => fails++;
                furnace.CoalAdded += _ => coals++;
                furnace.Cooled += _ => cools++;

                // Press on the first tick, then idle: `ticks` ticks in total.
                void Run(int ticks, bool door = false, bool coal = false)
                {
                    furnace.Tick(Dt, door, coal);
                    for (int i = 1; i < ticks; i++) furnace.Tick(Dt, false, false);
                }

                void Close() { if (furnace.State == DoorState.Open) Run(31, door: true); }
                void Open() { if (furnace.State == DoorState.Closed) Run(31, door: true); }
                void Reset(float temperature)
                {
                    Run(100);
                    Close();
                    boiler.Change(temperature - boiler.Temperature);
                    fails = coals = cools = 0;
                }

                Check($"start: {furnace.State}, {boiler.Temperature} °C, {speed.DisplaySpeed} km/h", furnace.State == DoorState.Closed && Mathf.Approximately(boiler.Temperature, 50f) && Mathf.Approximately(speed.DisplaySpeed, 15f));

                // Door timing; F does nothing while the door moves.
                Run(10, door: true);
                Check($"F: {furnace.State}", furnace.State == DoorState.Opening);
                Run(10, door: true);
                Check($"F while opening ignored: {furnace.State}", furnace.State == DoorState.Opening);
                Run(11);
                Check($"open after 0.3 s: {furnace.State}", furnace.State == DoorState.Open);

                // Successful throw closes automatically; P1 can reopen after it closes.
                Run(49, coal: true);
                Check($"0.49 s into a throw: {furnace.State}, {furnace.Action}, still {boiler.Temperature}", furnace.State == DoorState.Loading && furnace.Action == ShovelAction.Throw && Mathf.Approximately(boiler.Temperature, 50f));
                Run(2);
                Check($"coal lands at 0.5 s: {boiler.Temperature} (expect 150), door {furnace.State}, action {furnace.Action}", Mathf.Approximately(boiler.Temperature, 150f) && furnace.State == DoorState.Closing && furnace.Action == ShovelAction.None && coals == 1);
                Run(31, door: true);
                Check($"automatic close finishes; F during closing ignored: {furnace.State}", furnace.State == DoorState.Closed);
                Open();
                Check($"P1 reopens after automatic closure: {furnace.State}", furnace.State == DoorState.Open);

                // Enter during a throw is ignored, not a failure; repeat throws work.
                fails = 0;
                Run(20, coal: true);
                Run(20, coal: true);
                Check($"Enter during a throw ignored: fails {fails}, action time {furnace.ActionTime:0.00} not restarted", fails == 0 && furnace.Action == ShovelAction.Throw && furnace.ActionTime > 0.35f);
                Run(12);
                Check($"second coal: {boiler.Temperature} (expect 250), {furnace.State}", Mathf.Approximately(boiler.Temperature, 250f) && furnace.State == DoorState.Closing);
                Run(31);
                Open();
                Run(51, coal: true);
                Check($"third coal: {boiler.Temperature} (expect 350), no cooling ({cools})", Mathf.Approximately(boiler.Temperature, 350f) && cools == 0);

                // Automatic closure prevents cooling; reopening starts a fresh timer.
                Run(331);
                Check("closed after coal: no cooling", furnace.State == DoorState.Closed && cools == 0 && Mathf.Approximately(boiler.Temperature, 350f));
                Open();
                Run(110);
                Check($"1.11 s after landing: no cooling ({cools}x)", cools == 0 && Mathf.Approximately(boiler.Temperature, 350f));
                Run(10);
                Check($"1.2 s: first cooling ({cools}x, {boiler.Temperature}, expect 250)", cools == 1 && Mathf.Approximately(boiler.Temperature, 250f));
                Run(58);
                Check($"just before 1.8 s: no second cooling ({cools}x)", cools == 1);
                Run(2);
                Check($"1.8 s: second cooling ({cools}x, {boiler.Temperature}, expect 150)", cools == 2 && Mathf.Approximately(boiler.Temperature, 150f));
                Run(60);
                Check($"2.4 s: third cooling ({cools}x, {boiler.Temperature}, expect 50)", cools == 3 && Mathf.Approximately(boiler.Temperature, 50f));
                Run(60);
                Check($"at the 50 °C floor no cooling is reported ({cools}x)", cools == 3);

                // A landing part-way through the wait restarts the full 1.2 s.
                Reset(350f);
                Open();
                Run(60);
                Run(51, coal: true);  // lands at about 1.1 s after opening
                Check($"landing before the first cooling: {boiler.Temperature} (expect 450), {cools}x", Mathf.Approximately(boiler.Temperature, 450f) && cools == 0);
                Run(110);
                Check($"1.1 s after that landing: no cooling ({cools}x)", cools == 0);
                Check("door closed after landing", furnace.State == DoorState.Closed);
                Open();
                Run(110);
                Check("reopening starts a fresh cooling wait", cools == 0);
                Run(10);
                Check($"1.2 s after that landing: cooled ({cools}x, {boiler.Temperature}, expect 350)", cools == 1 && Mathf.Approximately(boiler.Temperature, 350f));

                // Closed door: nothing changes; a partial wait is dropped; cooling counts until fully closed.
                Reset(350f);
                Open();
                Run(100);
                Run(31, door: true);  // 1.0 s + the 0.3 s close passes 1.2 s
                Check($"cooling due while closing still counts ({cools}x, {boiler.Temperature}, expect 250), {furnace.State}", cools == 1 && Mathf.Approximately(boiler.Temperature, 250f) && furnace.State == DoorState.Closed);
                Run(300);
                Check($"door closed 3 s: still {boiler.Temperature}", Mathf.Approximately(boiler.Temperature, 250f));

                // Failure A: Enter while the door is not open.
                foreach (var state in new[] { DoorState.Closed, DoorState.Opening, DoorState.Closing })
                {
                    Reset(250f);
                    if (state == DoorState.Opening) Run(5, door: true);
                    if (state == DoorState.Closing) { Open(); Run(5, door: true); }
                    var seen = furnace.State;
                    Run(10, coal: true);
                    Check($"Enter while {seen}: fail ({fails}), action {furnace.Action}, no heat ({boiler.Temperature})",
                          seen == state && fails == 1 && furnace.Action == ShovelAction.FailDoorNotOpen && Mathf.Approximately(boiler.Temperature, 250f));
                }

                // Lockout 0.75 s: presses during it do nothing and do not restart it, whatever the door does.
                Reset(250f);
                Run(40, coal: true);
                Run(30, coal: true);
                Check($"Enter during lockout ignored: fails {fails}, left {furnace.P2LockoutLeft:0.00}", fails == 1 && furnace.P2LockoutLeft < 0.07f && furnace.P2LockoutLeft > 0f);
                Check($"animation still running at 0.70 s ({furnace.Action})", furnace.Action == ShovelAction.FailDoorNotOpen);
                Run(7);
                Check($"at 0.76 s: lockout over ({furnace.P2LockoutLeft}), action {furnace.Action}", furnace.P2LockoutLeft <= 0f && furnace.Action == ShovelAction.None);
                Run(5, coal: true);
                Check($"Enter after the lockout fails again ({fails})", fails == 2);

                // Failure B: F before the top of the arc.
                Reset(250f);
                Open();
                Run(30, coal: true);
                Run(5, door: true);
                Check($"F at 0.30 s: fail ({fails}), door {furnace.State}, action {furnace.Action}", fails == 1 && furnace.State == DoorState.Closing && furnace.Action == ShovelAction.FailBlocked);
                Run(30);
                Check($"door closes on time: {furnace.State}", furnace.State == DoorState.Closed);
                Check($"blocked animation runs on ({furnace.Action}, {furnace.ActionTime:0.00} s)", furnace.Action == ShovelAction.FailBlocked);
                Run(20);
                Check($"animation ends by 0.75 s ({furnace.Action}), no heat ({boiler.Temperature}), no coal ({coals})", furnace.Action == ShovelAction.None && Mathf.Approximately(boiler.Temperature, 250f) && coals == 0);

                // F just before the top still fails; F after it succeeds and the door closes at once.
                Reset(250f);
                Open();
                Run(40, coal: true);
                Run(1, door: true);
                Check($"F at 0.41 s: blocked (fails {fails})", fails == 1 && furnace.Action == ShovelAction.FailBlocked);

                Reset(250f);
                Open();
                Run(44, coal: true);
                Run(1, door: true);
                Check($"F at 0.45 s: no failure ({fails}), door {furnace.State} at once, action {furnace.Action}", fails == 0 && furnace.State == DoorState.Closing && furnace.Action == ShovelAction.Throw);
                Run(7);
                Check($"... coal still lands: {boiler.Temperature} (expect 350), door {furnace.State}", Mathf.Approximately(boiler.Temperature, 350f) && furnace.State == DoorState.Closing && coals == 1);
                Run(30);
                Check($"... door closes: {furnace.State}", furnace.State == DoorState.Closed);

                // Failure B does not restart the cooling timer.
                Reset(350f);
                Open();
                Run(80);
                Run(10, coal: true);
                Run(40, door: true);  // 0.8 + 0.1 + 0.3 (door closed at 1.2 s): cooling is due as it closes
                Check($"blocked coal keeps the cooling timer ({cools}x, {boiler.Temperature})", cools == 1 && Mathf.Approximately(boiler.Temperature, 250f));

                // Flame trigger: only coal that will land, at the top of its arc.
                int apexes = 0;
                furnace.CoalReachedApex += () => apexes++;
                Reset(250f);
                Open();
                Run(41, coal: true);
                Check($"before the top of the arc: no flare ({apexes})", apexes == 0);
                Run(2);
                Check($"at the top of the arc: flare ({apexes})", apexes == 1);
                Run(10);
                Reset(250f);
                Open();
                apexes = 0;
                Run(30, coal: true);
                Run(50, door: true);
                Check($"blocked coal passes the top without a flare ({apexes})", apexes == 0);
                Reset(250f);
                apexes = 0;
                Run(80, coal: true);
                Check($"tipped-out coal: no flare ({apexes})", apexes == 0);
                Reset(250f);
                Open();
                apexes = 0;
                Run(45, coal: true);
                Run(10, door: true);
                Check($"F after the top: coal lands, flare fired once ({apexes})", apexes == 1);

                // Flare: 0.2 s up, 0.5 s hold, 0.5 s down; restarts from the current size.
                var flare = go.AddComponent<FurnaceFlameAnimator>();
                flare.ResetSize();
                void Flare(int ticks) { for (int i = 0; i < ticks; i++) flare.Tick(Dt); }
                Check($"flame at rest: {flare.Size}", Mathf.Approximately(flare.Size, 0.15f) && flare.Phase == FurnaceFlameAnimator.FlarePhase.Idle);
                flare.Flare();
                Flare(10);
                Check($"0.10 s: growing ({flare.Size:0.000})", flare.Size > 0.15f && flare.Size < 0.25f && flare.Phase == FurnaceFlameAnimator.FlarePhase.Grow);
                Flare(10);
                Check($"0.20 s: full size ({flare.Size:0.000})", Mathf.Approximately(flare.Size, 0.25f) && flare.Phase == FurnaceFlameAnimator.FlarePhase.Hold);
                Flare(49);
                Check($"0.69 s: still holding ({flare.Size:0.000})", Mathf.Approximately(flare.Size, 0.25f) && flare.Phase == FurnaceFlameAnimator.FlarePhase.Hold);
                Flare(26);
                Check($"0.95 s: shrinking ({flare.Size:0.000})", flare.Size > 0.15f && flare.Size < 0.25f && flare.Phase == FurnaceFlameAnimator.FlarePhase.Shrink);
                float midShrink = flare.Size;

                flare.Flare();
                Check($"retrigger while shrinking: grows from {midShrink:0.000}", flare.Phase == FurnaceFlameAnimator.FlarePhase.Grow && Mathf.Approximately(flare.Size, midShrink));
                Flare(20);
                Check($"... 0.2 s later full size ({flare.Size:0.000})", Mathf.Approximately(flare.Size, 0.25f));
                Flare(30);
                flare.Flare();
                Check($"retrigger at full size: goes straight to a new hold ({flare.Phase})", flare.Phase == FurnaceFlameAnimator.FlarePhase.Hold);
                Flare(49);
                Check($"... hold restarted: still full at 0.49 s ({flare.Phase})", flare.Phase == FurnaceFlameAnimator.FlarePhase.Hold);
                Flare(52);
                Check($"... back at rest after the shrink: {flare.Size:0.000}, {flare.Phase}", Mathf.Approximately(flare.Size, 0.15f) && flare.Phase == FurnaceFlameAnimator.FlarePhase.Idle);

                // Temperature -> speed.
                Check($"750 °C -> {speed.DisplayFor(750f)} km/h, train {speed.TrainSpeedFor(speed.DisplayFor(750f))}",
                      Mathf.Approximately(speed.DisplayFor(750f), 85f) && Mathf.Approximately(speed.TrainSpeedFor(85f), 82.5f));
                Check($"1450 °C -> {speed.DisplayFor(1450f)} km/h, train {speed.TrainSpeedFor(155f)}",
                      Mathf.Approximately(speed.DisplayFor(1450f), 155f) && Mathf.Approximately(speed.TrainSpeedFor(155f), 150f));

                // Overheat at 1500 or more.
                Reset(1450f);
                Check($"at {boiler.Temperature} °C not overheated", !boiler.Overheated);
                Open();
                Run(51, coal: true);
                Check($"coal to {boiler.Temperature} °C: overheated", boiler.Overheated);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            log.Sort((a, b) => b.StartsWith("FAIL").CompareTo(a.StartsWith("FAIL")));
            Debug.Log($"[Furnace SelfTest] {(failures == 0 ? "ALL PASSED" : failures + " FAILED")}\n" + string.Join("\n", log));
        }
    }
}
