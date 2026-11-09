using System.Collections.Generic;
using UnityEngine;

namespace CompanyF.UI
{
    /// <summary>Best clear times, fastest first, kept in PlayerPrefs.</summary>
    public static class ResultRanking
    {
        public const int Size = 5;
        const string Key = "ResultRanking";

        public static List<float> Load()
        {
            var times = new List<float>();
            for (int i = 0; i < Size && PlayerPrefs.HasKey(Key + i); i++)
                times.Add(PlayerPrefs.GetFloat(Key + i));
            return times;
        }

        /// <summary>Records a clear time; returns its place (0 = best), or -1 if it did not make the ranking.</summary>
        public static int Add(float time)
        {
            var times = Load();
            int place = times.FindIndex(t => time < t);
            if (place < 0) place = times.Count;
            if (place >= Size) return -1;

            times.Insert(place, time);
            for (int i = 0; i < times.Count && i < Size; i++)
                PlayerPrefs.SetFloat(Key + i, times[i]);
            PlayerPrefs.Save();
            return place;
        }

        public static void Clear()
        {
            for (int i = 0; i < Size; i++)
                PlayerPrefs.DeleteKey(Key + i);
            PlayerPrefs.Save();
        }
    }
}
