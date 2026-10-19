using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CompanyF.Gameplay.EditorTools
{
    /// <summary>
    /// Runs the RANGE rules on a temporary controller with simulated key timing and logs PASS/FAIL per check.
    /// Covers judgement, turn order, combo speed table, caps, penalties, combo timeout and idle decay.
    /// (Which key belongs to which player is input wiring and is tested by hand.)
    /// </summary>
    public static class SpeedRangeSelfTest
    {
        const float Dt = 0.01f;

        [MenuItem("Tools/Speed Range/Run Self Test")]
        public static void Run()
        {
            var go = new GameObject("_SpeedRangeSelfTest") { hideFlags = HideFlags.HideAndDontSave };
            var log = new List<string>();
            int failures = 0;

            void Check(string what, bool ok)
            {
                log.Add($"{(ok ? "PASS" : "FAIL")}  {what}");
                if (!ok) failures++;
            }

            try
            {
                var c = go.AddComponent<SpeedRangeController>();
                c.gearCount = 1;  // the combo table checks below run without gear caps
                c.ResetGame();
                Check($"start: speed {c.Speed} == 15, P1, combo 0", Mathf.Approximately(c.Speed, 15f) && c.CurrentPlayer == 1 && c.Combo == 0);

                // Successes 1..10: speed follows the table and caps at 150; players alternate.
                float[] expected = { 18f, 22.5f, 30f, 45f, 60f, 82.5f, 105f, 127.5f, 150f, 150f };
                for (int i = 0; i < expected.Length; i++)
                {
                    int player = c.CurrentPlayer;
                    PressInsideZone(c);
                    Check($"success #{i + 1} by P{player}: combo {c.Combo}, speed {c.Speed} == {expected[i]}, next P{c.CurrentPlayer}",
                          c.Combo == i + 1 && Mathf.Abs(c.Speed - expected[i]) < 0.001f && c.CurrentPlayer == 3 - player && c.Pointer == 0f);
                }

                // Pressing before the zone.
                SkipToRound(c, () => c.ZoneStart > 5f);
                int p = c.CurrentPlayer;
                while (c.Pointer + 100f / c.cycleTime * Dt < c.ZoneStart - 1f) c.Tick(Dt, false);
                float before = c.Speed;
                c.Tick(Dt, true);
                Check($"early press: combo 0, speed {before} -> {c.Speed} (-10), turn passes", c.Combo == 0 && Mathf.Abs(c.Speed - Mathf.Max(15f, before - 10f)) < 0.01f && c.CurrentPlayer == 3 - p);

                // Pressing after the zone.
                SkipToRound(c, () => c.ZoneEnd < 95f);
                PressInsideZone(c);  // rebuild a combo first
                SkipToRound(c, () => c.ZoneEnd < 95f, keepCombo: true);
                before = c.Speed;
                while (c.Pointer <= c.ZoneEnd + 1f) c.Tick(Dt, false);
                c.Tick(Dt, true);
                Check($"late press: combo reset to 0, speed {before} -> {c.Speed}", c.Combo == 0 && Mathf.Abs(c.Speed - Mathf.Max(15f, before - 10f)) < 0.02f);

                // No press until 100.
                c.ResetGame();
                c.startSpeed = 100f; c.ResetGame();
                p = c.CurrentPlayer;
                float t = 0f;
                while (c.CurrentPlayer == p) { c.Tick(Dt, false); t += Dt; }
                Check($"no press: miss after {t:0.00}s (cycle 2.5s), speed {c.Speed:0.00} == 100 - 10 - idle decay", Mathf.Abs(t - 2.5f) < 0.02f && Mathf.Abs(c.Speed - (90f - 0.15f * t)) < 0.01f);

                // Floor: a miss at min speed stays at 15.
                c.startSpeed = 15f; c.ResetGame();
                while (c.CurrentPlayer == 1) c.Tick(Dt, false);
                Check($"miss at 15 stays 15 (got {c.Speed})", Mathf.Approximately(c.Speed, 15f));

                // Idle decay while combo is 0 (slow pointer so no miss happens).
                c.cycleTime = 100f; c.startSpeed = 50f; c.ResetGame();
                for (int i = 0; i < 1000; i++) c.Tick(Dt, false);
                Check($"idle 10s at combo 0: 50 -> {c.Speed:0.000} (expect 48.5)", Mathf.Abs(c.Speed - 48.5f) < 0.01f);

                // Combo timeout (only reachable when the pointer cycle is longer than the combo time).
                c.ResetGame();
                PressInsideZone(c);
                before = c.Speed;
                float waited = 0f;
                while (c.Combo > 0) { c.Tick(Dt, false); waited += Dt; }
                Check($"combo timeout after {waited:0.00}s (expect 3): speed {before} -> {c.Speed} (-30)", Mathf.Abs(waited - 3f) < 0.02f && Mathf.Abs(c.Speed - Mathf.Max(15f, before - 30f)) < 0.01f);

                // Gears: 15-60 / 60-105 / 105-150.
                c.gearCount = 3; c.cycleTime = 2.5f; c.startSpeed = 15f; c.ResetGame();
                Check($"gear tops: {c.GearTop(1)} / {c.GearTop(2)} / {c.GearTop(3)} (expect 60 / 105 / 150), start gear {c.Gear}",
                      Mathf.Approximately(c.GearTop(1), 60f) && Mathf.Approximately(c.GearTop(2), 105f) && Mathf.Approximately(c.GearTop(3), 150f) && c.Gear == 1);
                bool shiftedEarly = c.TryShiftUp();
                for (int i = 0; i < 8; i++) PressInsideZone(c);
                Check($"gear 1 caps at 60 (speed {c.Speed}), W below the top did nothing ({!shiftedEarly})", Mathf.Approximately(c.Speed, 60f) && !shiftedEarly && c.Gear == 1);
                Check($"W at the top: shift to {(c.TryShiftUp() ? c.Gear : -1)}, new top {c.GearCap}", c.Gear == 2 && Mathf.Approximately(c.GearCap, 105f));
                for (int i = 0; i < 6; i++) PressInsideZone(c);
                Check($"gear 2 caps at 105 (speed {c.Speed})", Mathf.Approximately(c.Speed, 105f));
                c.TryShiftUp();
                for (int i = 0; i < 6; i++) PressInsideZone(c);
                Check($"gear 3 caps at 150 (speed {c.Speed}), W at gear 3 does nothing ({!c.TryShiftUp()})", Mathf.Approximately(c.Speed, 150f) && c.Gear == 3 && !c.TryShiftUp());

                // Auto downshift on any slow-down: brake 150 -> 104 drops to gear 2, -> 59 drops to gear 1.
                c.Brake(46f / c.brakePerSecond);
                Check($"brake to {c.Speed:0.0}: gear {c.Gear} (expect 2)", c.Gear == 2);
                c.Brake(45f / c.brakePerSecond);
                Check($"brake to {c.Speed:0.0}: gear {c.Gear} (expect 1)", c.Gear == 1);
                c.Brake(10f);
                Check($"brake floor: {c.Speed} (expect 15)", Mathf.Approximately(c.Speed, 15f));

                // Miss penalty from gear 3 straight down two gears.
                c.ResetGame();
                for (int i = 0; i < 8; i++) PressInsideZone(c);
                c.TryShiftUp();
                for (int i = 0; i < 6; i++) PressInsideZone(c);
                c.TryShiftUp();
                for (int i = 0; i < 2; i++) PressInsideZone(c);
                before = c.Speed;
                int p0 = c.CurrentPlayer;
                while (c.CurrentPlayer == p0) c.Tick(Dt, false);
                Check($"miss at gear 3, {before:0.0} -> {c.Speed:0.0}: gear {c.Gear}", c.Gear == (c.Speed < 60f ? 1 : c.Speed < 105f ? 2 : 3));

                // Boiler: +20 per success (also at the gear top), -10 per miss, floor 50, overheat above 200.
                var boiler = go.AddComponent<BoilerTemperature>();
                boiler.Change(0f);
                Check($"boiler start {boiler.Temperature} (expect 50)", Mathf.Approximately(boiler.Temperature, 50f));
                boiler.Change(-boiler.failCool);
                Check($"miss at 50 stays {boiler.Temperature}", Mathf.Approximately(boiler.Temperature, 50f));
                for (int i = 0; i < 7; i++) boiler.Change(boiler.successHeat);
                Check($"7 successes: {boiler.Temperature} (expect 190), not overheated", Mathf.Approximately(boiler.Temperature, 190f) && !boiler.Overheated);
                boiler.Cool(1f);
                Check($"cool key 1s: {boiler.Temperature} (expect 170)", Mathf.Approximately(boiler.Temperature, 170f));
                boiler.Change(boiler.successHeat + 10f);
                Check($"exactly 200 is not an overheat ({boiler.Temperature}, {boiler.Overheated})", !boiler.Overheated);
                boiler.Change(boiler.successHeat);
                Check($"above 200 overheats ({boiler.Temperature})", boiler.Overheated);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            log.Sort((a, b) => a.StartsWith("FAIL").CompareTo(b.StartsWith("FAIL")) * -1);  // failures first
            Debug.Log($"[SpeedRange SelfTest] {(failures == 0 ? "ALL PASSED" : failures + " FAILED")}\n" + string.Join("\n", log));
        }

        static void PressInsideZone(SpeedRangeController c)
        {
            float mid = (c.ZoneStart + c.ZoneEnd) * 0.5f;
            while (c.Pointer + 100f / c.cycleTime * Dt < mid) c.Tick(Dt, false);
            c.Tick(Dt, true);
        }

        /// <summary>Lets rounds run out (as misses) until one matches; optionally keeps the current combo by succeeding instead.</summary>
        static void SkipToRound(SpeedRangeController c, System.Func<bool> wanted, bool keepCombo = false)
        {
            for (int guard = 0; guard < 200 && !wanted(); guard++)
            {
                if (keepCombo) PressInsideZone(c);
                else { int p = c.CurrentPlayer; while (c.CurrentPlayer == p) c.Tick(Dt, false); }
            }
        }
    }
}
