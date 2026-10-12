using System.Collections.Generic;
using CompanyF.Gameplay;
using CompanyF.Track;
using UnityEditor;
using UnityEngine;

namespace CompanyF.Quiz.EditorTools
{
    /// <summary>
    /// Runs the quiz rules on temporary objects with simulated key presses and logs PASS/FAIL per check:
    /// slow-down and pointer pause, countdown, locked answers, match / mismatch / timeout, timing, restore.
    /// </summary>
    public static class QuizSelfTest
    {
        const float Dt = 0.01f;

        [MenuItem("Tools/Quiz/Run Self Test")]
        public static void Run()
        {
            var go = new GameObject("_QuizSelfTest") { hideFlags = HideFlags.HideAndDontSave };
            var set = ScriptableObject.CreateInstance<QuizQuestionSet>();
            var log = new List<string>();
            int failures = 0;
            float originalTimeScale = Time.timeScale;

            void Check(string what, bool ok)
            {
                log.Add($"{(ok ? "PASS" : "FAIL")}  {what}");
                if (!ok) failures++;
            }

            try
            {
                set.questions.Add(new QuizQuestionSet.Question { text = "Q1", left = "L", right = "R" });
                var speedRange = go.AddComponent<SpeedRangeController>();
                var quiz = go.AddComponent<QuizManager>();
                quiz.questionSet = set;
                var so = new SerializedObject(quiz);
                so.FindProperty("speedRange").objectReferenceValue = speedRange;
                so.ApplyModifiedPropertiesWithoutUndo();
                var junction = go.AddComponent<TrackJunction>();
                junction.index = 1;

                // 1. Both pick right at 1.2 s.
                Time.timeScale = 1f;
                quiz.Begin(junction);
                Check($"begin: timeScale {Time.timeScale:0.0000} == 1/15, pointer paused, answer phase",
                      Mathf.Abs(Time.timeScale - 1f / 15f) < 1e-5f && speedRange.PointerPaused && quiz.Phase == QuizManager.QuizPhase.Answer);

                var seen = new List<int>();
                float t = 0f;
                bool decidedEarly = false;
                while (quiz.Phase != QuizManager.QuizPhase.Idle)
                {
                    bool pressNow = t >= 1.2f && t < 1.2f + Dt;
                    var press = pressNow ? QuizChoice.Right : QuizChoice.None;
                    if (quiz.Phase == QuizManager.QuizPhase.Answer && (seen.Count == 0 || seen[seen.Count - 1] != quiz.CountdownNumber))
                        seen.Add(quiz.CountdownNumber);
                    quiz.Tick(Dt, press, press);
                    t += Dt;
                    if (pressNow) decidedEarly = quiz.Current != null && quiz.Phase == QuizManager.QuizPhase.Answer;
                    if (t > 20f) break;
                }
                var r = quiz.Results[0];
                Check($"countdown shown: {string.Join(",", seen)} (expect 3,2,1,0)", string.Join(",", seen) == "3,2,1,0");
                Check("both answered: decided at once but still in answer phase (slow-mo kept)", decidedEarly);
                Check($"same pick: success={r.success}, direction={r.direction} (expect true, Right)", r.success && r.direction == QuizChoice.Right);
                Check($"quiz lasted {t:0.00}s real (expect 4 + 1)", Mathf.Abs(t - 5f) < 0.03f);
                Check($"end: timeScale {Time.timeScale} restored to 1, pointer resumed", Mathf.Approximately(Time.timeScale, 1f) && !speedRange.PointerPaused);

                // 2. Different picks; P1 tries to change their answer.
                quiz.Begin(junction);
                quiz.Tick(Dt, QuizChoice.Left, QuizChoice.None);
                quiz.Tick(Dt, QuizChoice.Right, QuizChoice.None);   // ignored: answers are final
                quiz.Tick(Dt, QuizChoice.None, QuizChoice.Right);
                r = quiz.Current;
                Check($"P1 answer locked: {quiz.Player1Choice} (expect Left)", quiz.Player1Choice == QuizChoice.Left);
                Check($"different picks: success={r?.success}, random direction={r?.direction}", r != null && !r.success && r.direction != QuizChoice.None);
                Finish(quiz);

                // 3. Only P2 answers: decided as a failure when time runs out, not before.
                quiz.Begin(junction);
                quiz.Tick(Dt, QuizChoice.None, QuizChoice.Left);
                bool undecidedBeforeTimeout = true;
                while (quiz.Phase == QuizManager.QuizPhase.Answer)
                {
                    if (quiz.Current != null) undecidedBeforeTimeout = false;
                    quiz.Tick(Dt, QuizChoice.None, QuizChoice.None);
                }
                r = quiz.Current;
                Check($"P1 no answer: undecided until 4s = {undecidedBeforeTimeout}, success={r?.success}, direction={r?.direction}",
                      undecidedBeforeTimeout && r != null && !r.success && r.direction != QuizChoice.None && r.player1 == QuizChoice.None);
                Finish(quiz);

                // 4. Random split across many mismatches (sanity check that both sides happen).
                int lefts = 0;
                for (int i = 0; i < 200; i++)
                {
                    quiz.Begin(junction);
                    quiz.Tick(Dt, QuizChoice.Left, QuizChoice.Right);
                    if (quiz.Current.direction == QuizChoice.Left) lefts++;
                    Finish(quiz);
                }
                Check($"random direction on failure: {lefts}/200 left", lefts > 60 && lefts < 140);
                Check($"results recorded: {quiz.Results.Count} (expect 203)", quiz.Results.Count == 203);
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(set);
            }

            Debug.Log($"[Quiz SelfTest] {(failures == 0 ? "ALL PASSED" : failures + " FAILED")}\n" + string.Join("\n", log));
        }

        static void Finish(QuizManager quiz)
        {
            for (int guard = 0; guard < 10000 && quiz.Phase != QuizManager.QuizPhase.Idle; guard++)
                quiz.Tick(Dt, QuizChoice.None, QuizChoice.None);
        }
    }
}
