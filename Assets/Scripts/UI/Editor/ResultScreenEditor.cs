using UnityEditor;
using UnityEngine;

namespace CompanyF.UI.EditorTools
{
    /// <summary>Inspector buttons for testing the result screen.</summary>
    [CustomEditor(typeof(ResultScreen))]
    public class ResultScreenEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var screen = (ResultScreen)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("測試", EditorStyles.boldLabel);

            var times = ResultRanking.Load();
            EditorGUILayout.HelpBox(times.Count == 0
                ? "排行榜紀錄：無"
                : "排行榜紀錄：" + ResultRanking.FormatLines(times, Mathf.Min(times.Count, screen.rankingCount), -1, Color.white).Replace("\n", " / "),
                MessageType.None);

            if (GUILayout.Button("清除排行榜紀錄")
                && EditorUtility.DisplayDialog("清除排行榜紀錄", "這台電腦上的排行榜紀錄會全部刪除，無法復原。", "清除", "取消"))
            {
                ResultRanking.Clear();
                Debug.Log("[Result] ranking cleared");
            }

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("測試顯示：クリア 45.6秒（也會寫入排行榜）")) screen.Show(true, 45.6f);
                if (GUILayout.Button("測試顯示：オーバー")) screen.Show(false, 0f);
            }
            if (!Application.isPlaying) EditorGUILayout.HelpBox("測試顯示只能在 Play 中使用。", MessageType.Info);
        }
    }
}
