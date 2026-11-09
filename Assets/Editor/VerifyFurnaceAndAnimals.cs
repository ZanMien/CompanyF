using System;
using System.Linq;
using CompanyF.Gameplay;
using CompanyF.Gameplay.EditorTools;
using CompanyF.UI;
using UnityEditor;
using UnityEngine;

public static class VerifyFurnaceAndAnimals
{
    const string Key = "CompanyF.VerifyFurnaceAndAnimals";
    static double next;
    static int stage;

    [MenuItem("Tools/Game/Verify Furnace And Animals")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        AnimalWarningSelfTest.Run();
        FurnaceSelfTest.Run();
        WhistleAnimalSetup.SelfTest();
        GameFlowSetup.SelfTest();
        ViewInputSelfTest.Run();
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    static void Resume()
    {
        if (!SessionState.GetBool(Key, false)) return;
        next = EditorApplication.timeSinceStartup + 3;
        stage = 0;
        EditorApplication.update -= Step;
        EditorApplication.update += Step;
    }

    static void Step()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
        try
        {
            var herd = UnityEngine.Object.FindFirstObjectByType<AnimalHerd>();
            var ui = UnityEngine.Object.FindFirstObjectByType<GameFlowUI>();
            if (herd == null || ui == null || herd.Animals.Count == 0) throw new InvalidOperationException("Missing herd/UI/animals.");
            var markers = ui.GetComponentsInChildren<UnityEngine.UI.Image>(true).Where(i => i.name == "AnimalMarker").ToArray();
            if (markers.Length != herd.Animals.Count || markers.Any(i => !i.gameObject.activeSelf))
                throw new InvalidOperationException("All animals must have visible markers.");
            if (stage == 0)
            {
                Debug.Log($"[Latest Gameplay] PASS: {markers.Length} animal markers visible, including waiting animals.");
                herd.Animals[0].state = AnimalHerd.AnimalState.Gone;
                herd.Animals[0].transform.gameObject.SetActive(false);
                stage = 1;
                next = EditorApplication.timeSinceStartup + 0.2;
                return;
            }
            Debug.Log("[Latest Gameplay] ALL PASSED: markers remain after an animal disappears; furnace and early warning tests passed.");
            Finish();
        }
        catch (Exception ex) { Debug.LogException(ex); Finish(); }
    }

    static void Finish()
    {
        EditorApplication.update -= Step;
        SessionState.SetBool(Key, false);
        EditorApplication.ExitPlaymode();
    }
}
