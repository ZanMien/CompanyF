using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace CompanyF.UI
{
    /// <summary>
    /// Fastest clear times, kept on this computer in <see cref="PlayerPrefs"/>. Only times are stored, fastest first.
    /// Times are kept to a tenth of a second, the precision the result screen shows.
    /// </summary>
    public static class ResultRanking
    {
        public const string PrefsKey = "CompanyF.Ranking.ClearTimes";
        /// <summary>More than are ever shown, so raising the number of ranks shown later still has records to show.</summary>
        public const int MaxStored = 100;

        public static List<float> Load()
        {
            var times = new List<float>();
            var saved = PlayerPrefs.GetString(PrefsKey, "");
            foreach (var part in saved.Split(';'))
                if (float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out float time))
                    times.Add(time);
            times.Sort();
            return times;
        }

        public static void Save(List<float> times)
        {
            var parts = new string[times.Count];
            for (int i = 0; i < times.Count; i++) parts[i] = times[i].ToString("0.0", CultureInfo.InvariantCulture);
            PlayerPrefs.SetString(PrefsKey, string.Join(";", parts));
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }

        /// <summary>Saves a clear time and returns its place (0 = fastest) among the saved times.</summary>
        public static int Record(float clearTime, out List<float> times)
        {
            times = Load();
            int place = Insert(times, clearTime);
            Save(times);
            return place;
        }

        /// <summary>
        /// Puts <paramref name="time"/> (rounded to a tenth) into the sorted list after any equal times, so an earlier
        /// record keeps its place. Returns its index, or -1 if it falls off the end of the stored list.
        /// </summary>
        public static int Insert(List<float> times, float time)
        {
            time = Round(time);
            int place = 0;
            while (place < times.Count && times[place] <= time) place++;
            times.Insert(place, time);
            if (times.Count > MaxStored) times.RemoveRange(MaxStored, times.Count - MaxStored);
            return place < MaxStored ? place : -1;
        }

        public static float Round(float seconds) => Mathf.Round(seconds * 10f) / 10f;

        /// <summary>"47.7" for 47.66 seconds.</summary>
        public static string FormatSeconds(float seconds) => Round(seconds).ToString("0.0", CultureInfo.InvariantCulture);

        /// <summary>
        /// One line per rank: "1位 32.4秒", or "4位 ---" when there is no record yet. The line at
        /// <paramref name="highlight"/> is coloured with <paramref name="highlightColor"/>.
        /// </summary>
        public static string FormatLines(IReadOnlyList<float> times, int count, int highlight, Color highlightColor)
        {
            var text = new StringBuilder();
            string colour = ColorUtility.ToHtmlStringRGBA(highlightColor);
            for (int i = 0; i < count; i++)
            {
                if (i > 0) text.Append('\n');
                string line = i < times.Count ? $"{i + 1}位 {FormatSeconds(times[i])}秒" : $"{i + 1}位 ---";
                text.Append(i == highlight ? $"<color=#{colour}>{line}</color>" : line);
            }
            return text.ToString();
        }
    }
}
