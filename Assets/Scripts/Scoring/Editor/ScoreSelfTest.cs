using System.Collections.Generic;
using CompanyF.Track;
using UnityEditor;
using UnityEngine;

namespace CompanyF.Scoring.EditorTools
{
    /// <summary>
    /// Feeds simulated pieces and quiz events (with real-time stamps) to a temporary ScoreManager and logs PASS/FAIL,
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

                for (int i = 0; i < 3; i++) s.NotifyPiece("test", i);
                Check($"3 pieces: {s.Distance}m / {s.Score} (expect 300m / 3)", Mathf.Approximately(s.Distance, 300f) && s.Score == 3);

                // Bonus window from the question appearing (t=10..13), quiz succeeds at t=15.
                s.NotifyQuizStarted(10f);
                s.NotifyPiece("in", 11f);
                s.NotifyPiece("in", 12.9f);
                s.NotifyPiece("after", 13.1f);
                Check($"window pieces score base first: {s.Score} (expect 6)", s.Score == 6);
                s.NotifyQuizEnded(true, 15f);
                Check($"success top-up for 2 pieces: {s.Score} (expect 8)", s.Score == 8);
                s.NotifyPiece("later", 16f);
                Check($"after window +1: {s.Score} (expect 9)", s.Score == 9);

                // Failed quiz: no top-up.
                s.NotifyQuizStarted(20f);
                s.NotifyPiece("in", 21f);
                s.NotifyQuizEnded(false, 25f);
                Check($"failed quiz: {s.Score} (expect 10)", s.Score == 10);

                // Window opening at quiz end, outcome known: success pieces score 2 straight away.
                s.bonusStart = ScoreManager.BonusStart.QuizEnded;
                s.NotifyQuizStarted(30f);
                s.NotifyQuizEnded(true, 35f);
                s.NotifyPiece("in", 36f);
                s.NotifyPiece("in", 37.9f);
                s.NotifyPiece("after", 38.1f);
                Check($"QuizEnded mode: {s.Score} (expect 10 + 2 + 2 + 1 = 15)", s.Score == 15);
                s.bonusStart = ScoreManager.BonusStart.QuizShown;

                // Cap at 100; distance keeps counting.
                while (s.Score < 99) s.NotifyPiece("fill", 100f);
                float d = s.Distance;
                s.NotifyPiece("to100", 100f);
                s.NotifyPiece("over", 100f);
                Check($"cap: score {s.Score} (expect 100), distance +200 ({s.Distance - d}m)", s.Score == 100 && Mathf.Approximately(s.Distance - d, 200f));
                s.NotifyQuizStarted(200f);
                s.NotifyPiece("in", 201f);
                s.NotifyQuizEnded(true, 205f);
                Check($"top-up never exceeds the cap: {s.Score}", s.Score == 100);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            // Route data: every path's pieces end in order, inside the path, and the route is 52 pieces long either way.
            var route = Object.FindFirstObjectByType<TrackRoute>();
            if (route != null)
            {
                bool ordered = true;
                foreach (var seg in route.GetComponentsInChildren<TrackSegment>())
                {
                    float prev = 0f;
                    foreach (var p in seg.Pieces)
                    {
                        if (p.model == null || p.end <= prev || p.end > seg.Length + 0.01f) ordered = false;
                        prev = p.end;
                    }
                    if (seg.Pieces.Count == 0) ordered = false;
                }
                Check("route pieces: present, in travel order, inside each path", ordered);

                int allLeft = 0, allRight = 0;
                foreach (bool goLeft in new[] { true, false })
                {
                    int count = 0;
                    var seg = route.start;
                    while (seg != null)
                    {
                        count += seg.Pieces.Count;
                        seg = seg.endJunction != null ? seg.endJunction.Choose(goLeft) : seg.next;
                    }
                    if (goLeft) allLeft = count; else allRight = count;
                }
                Check($"pieces along the route: all-left {allLeft}, all-right {allRight} (expect 52)", allLeft == 52 && allRight == 52);
            }

            Debug.Log($"[Score SelfTest] {(failures == 0 ? "ALL PASSED" : failures + " FAILED")}\n" + string.Join("\n", log));
        }
    }
}
