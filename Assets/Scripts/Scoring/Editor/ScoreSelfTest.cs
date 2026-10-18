using System.Collections.Generic;
using CompanyF.Track;
using UnityEditor;
using UnityEngine;

namespace CompanyF.Scoring.EditorTools
{
    /// <summary>
    /// Feeds simulated pieces to a temporary ScoreManager and logs PASS/FAIL,
    /// then checks the piece data baked into the route in the open scene.
    /// </summary>
    public static class ScoreSelfTest
    {
        [MenuItem("Tools/Score/Run Self Test")]
        public static void Run()
        {
            var go = new GameObject("_ScoreSelfTest") { hideFlags = HideFlags.HideAndDontSave };
            var log = new List<string>();
            int failures = 0;

            void Check(string what, bool ok)
            {
                log.Add($"{(ok ? "PASS" : "FAIL")}  {what}");
                if (!ok) failures++;
            }

            try
            {
                var s = go.AddComponent<ScoreManager>();

                for (int i = 0; i < 3; i++) s.NotifyPiece("test");
                Check($"3 pieces: {s.Distance}m / {s.Score} (expect 300m / 3)", Mathf.Approximately(s.Distance, 300f) && s.Score == 3);

                while (s.Score < 99) s.NotifyPiece("fill");
                float d = s.Distance;
                s.NotifyPiece("to100");
                s.NotifyPiece("over");
                Check($"cap: score {s.Score} (expect 100), distance +200 ({s.Distance - d}m)", s.Score == 100 && Mathf.Approximately(s.Distance - d, 200f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            // Route data: pieces present, in travel order, inside each path; count along the whole line.
            var route = Object.FindFirstObjectByType<TrackRoute>();
            if (route != null)
            {
                bool ordered = true;
                int count = 0;
                for (var seg = route.start; seg != null; seg = seg.next)
                {
                    float prev = 0f;
                    foreach (var p in seg.Pieces)
                    {
                        if (p.model == null || p.end <= prev || p.end > seg.Length + 0.01f) ordered = false;
                        prev = p.end;
                    }
                    if (seg.Pieces.Count == 0) ordered = false;
                    count += seg.Pieces.Count;
                }
                Check("route pieces: present, in travel order, inside each path", ordered);
                Check($"pieces along the route: {count} (expect 104)", count == 104);
            }

            Debug.Log($"[Score SelfTest] {(failures == 0 ? "ALL PASSED" : failures + " FAILED")}\n" + string.Join("\n", log));
        }
    }
}
