using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CompanyF.Gameplay.EditorTools
{
    /// <summary>
    /// Runs the furnace rules on temporary objects with simulated key presses and logs PASS/FAIL (failures first):
    /// door states and timing, coal, cooling, P2 failures and lockout, simultaneous presses, overheat,
    /// temperature-to-speed mapping and the animation hook.
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

                void Run(float seconds, bool door = false, bool coal = false)
                {
                    furnace.Tick(Dt, door, coal);
                    for (float t = Dt; t < seconds - 1e-4f; t += Dt) furnace.Tick(Dt, false, false);
                }

                Check($"start: {furnace.State}, {boiler.Temperature} °C, {speed.DisplaySpeed} km/h", furnace.State == DoorState.Closed && Mathf.Approximately(boiler.Temperature, 50f) && Mathf.Approximately(speed.DisplaySpeed, 15f));

                // Opening takes 0.3 s; F during it does nothing.
                Run(0.1f, door: true);
                Check($"F: {furnace.State}", furnace.State == DoorState.Opening);
                Run(0.1f, door: true);
                Check($"F while opening ignored: {furnace.State}", furnace.State == DoorState.Opening);
                Run(0.11f);
                Check($"open after 0.3 s: {furnace.State}", furnace.State == DoorState.Open);

                // Coal: Loading 0.3 s, +100 at the end, then auto close 0.3 s. Open for 0.6 s < 1.2 s: no cooling.
                Run(0.15f, coal: true);
                Check($"Enter while open: {furnace.State}, still {boiler.Temperature} °C", furnace.State == DoorState.Loading && Mathf.Approximately(boiler.Temperature, 50f));
                Run(0.16f);
                Check($"coal in: {boiler.Temperature} °C (expect 150), {furnace.State}", Mathf.Approximately(boiler.Temperature, 150f) && furnace.State == DoorState.Closing && coals == 1);
                Run(0.3f);
                Check($"auto closed: {furnace.State}, no cooling ({cools})", furnace.State == DoorState.Closed && cools == 0);

                // Load to 350, then keep the door open 2.5 s: cools at 1.2 s and 2.4 s; closing 0.3 s more (2.8 s total) adds none.
                for (int i = 0; i < 2; i++) LoadCoal(furnace);
                Check($"two more coals: {boiler.Temperature} (expect 350)", Mathf.Approximately(boiler.Temperature, 350f));
                cools = 0;
                Run(0.31f, door: true);
                Run(2.5f);
                Check($"open 2.5 s: cooled {cools}x, {boiler.Temperature} °C (expect 2x, 150)", cools == 2 && Mathf.Approximately(boiler.Temperature, 150f));
                Run(0.31f, door: true);
                Check($"closed: {furnace.State}, partial interval dropped ({cools}x)", furnace.State == DoorState.Closed && cools == 2);
                Run(3f);
                Check($"door closed for 3 s: still {boiler.Temperature} °C", Mathf.Approximately(boiler.Temperature, 150f));

                // P2 failure while closed, 0.5 s lockout: presses during it do nothing and do not restart it.
                fails = 0;
                Run(0.2f, coal: true);
                Check($"Enter while closed: fail ({fails}), lockout {furnace.P2LockoutLeft:0.00}", fails == 1 && furnace.P2LockoutLeft > 0f);
                Run(0.2f, coal: true);
                Check($"Enter during lockout ignored (fails {fails}, lockout left {furnace.P2LockoutLeft:0.00} not reset)", fails == 1 && furnace.P2LockoutLeft < 0.15f);
                Run(0.15f);
                Check($"lockout over after 0.5 s ({furnace.P2LockoutLeft})", furnace.P2LockoutLeft <= 0f);

                // Enter during Opening / Closing fails too.
                fails = 0;
                Run(0.1f, door: true);
                Run(0.6f, coal: true);
                Check($"Enter while opening: fail ({fails})", fails == 1);

                // Simultaneous: P2 first, P1 within 0.3 s -> no coal, door closes, P2 fails.
                fails = 0;
                float before = boiler.Temperature;
                Run(0.1f, coal: true);
                Run(0.2f, door: true);
                Check($"P2 then P1 within 0.3 s: fail ({fails}), {furnace.State}", fails == 1 && furnace.State == DoorState.Closing);
                Run(0.6f);
                Check($"... no coal: {boiler.Temperature} == {before}, door {furnace.State}", Mathf.Approximately(boiler.Temperature, before) && furnace.State == DoorState.Closed);

                // Simultaneous: P1 first (closing), P2 within 0.3 s -> fail, door keeps closing.
                Run(0.31f, door: true);  // open
                fails = 0;
                before = boiler.Temperature;
                Run(0.1f, door: true);
                Run(0.5f, coal: true);
                Check($"P1 then P2 within 0.3 s: fail ({fails}), {furnace.State}, {boiler.Temperature} == {before}", fails == 1 && furnace.State == DoorState.Closed && Mathf.Approximately(boiler.Temperature, before));

                // Temperature -> speed: 750 °C -> 85 km/h, train 82.5; 1450 °C -> 155 km/h, train 150.
                Check($"750 °C -> {speed.DisplayFor(750f)} km/h, train {speed.TrainSpeedFor(speed.DisplayFor(750f))}",
                      Mathf.Approximately(speed.DisplayFor(750f), 85f) && Mathf.Approximately(speed.TrainSpeedFor(85f), 82.5f));
                Check($"1450 °C -> {speed.DisplayFor(1450f)} km/h, train {speed.TrainSpeedFor(155f)}",
                      Mathf.Approximately(speed.DisplayFor(1450f), 155f) && Mathf.Approximately(speed.TrainSpeedFor(155f), 150f));

                // Overheat at 1500 or more.
                while (boiler.Temperature < 1450f - 1f) LoadCoal(furnace);
                Check($"at {boiler.Temperature} °C not overheated", !boiler.Overheated);
                LoadCoal(furnace);
                Check($"coal to {boiler.Temperature} °C: overheated", boiler.Overheated);

                // Animation hook: with timers off, states wait for CompleteState().
                var hook = new GameObject("_FurnaceHook") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<FurnaceController>();
                hook.useTimers = false;
                var entered = new List<DoorState>();
                hook.StateEntered += s => entered.Add(s);
                hook.Tick(Dt, true, false);
                for (int i = 0; i < 100; i++) hook.Tick(Dt, false, false);
                bool waited = hook.State == DoorState.Opening;
                hook.CompleteState();
                Check($"timers off: waited in Opening ({waited}), CompleteState -> {hook.State}; entered {string.Join(",", entered)}",
                      waited && hook.State == DoorState.Open && string.Join(",", entered) == "Opening,Open");
                Object.DestroyImmediate(hook.gameObject);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            log.Sort((a, b) => b.StartsWith("FAIL").CompareTo(a.StartsWith("FAIL")));
            Debug.Log($"[Furnace SelfTest] {(failures == 0 ? "ALL PASSED" : failures + " FAILED")}\n" + string.Join("\n", log));
        }

        /// <summary>Open, shovel, let it close by itself: one coal, the door open less than one cooling interval.</summary>
        static void LoadCoal(FurnaceController f)
        {
            f.Tick(Dt, true, false);
            for (int i = 0; i < 31; i++) f.Tick(Dt, false, false);
            f.Tick(Dt, false, true);
            for (int i = 0; i < 70; i++) f.Tick(Dt, false, false);
        }
    }
}
