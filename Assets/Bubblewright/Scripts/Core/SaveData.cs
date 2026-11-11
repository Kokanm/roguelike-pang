using System;
using UnityEngine;

namespace Bubblewright
{
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public int seed, stage, score, hearts, maxHearts;
        public string[] upgrades;        // names, in the order taken
        public string character = "wick"; // used from step 16
        public bool[] hardWorld;         // length 7, index = world
        public string[] worldDebuff;     // length 7
    }

    public static class SaveGame
    {
        public const string Key = "Bubblewright.Run";

        public static bool Exists => Load() != null;

        public static void Save(Run run)
        {
            if (run == null) return;
            string json = JsonUtility.ToJson(run.ToSave());
            try
            {
                PlayerPrefs.SetString(Key, json);
                PlayerPrefs.Save();
            }
            catch { }
        }

        public static Run Load()
        {
            string json;
            try
            {
                if (!PlayerPrefs.HasKey(Key)) return null;
                json = PlayerPrefs.GetString(Key, null);
            }
            catch
            {
                return null;
            }
            if (string.IsNullOrEmpty(json)) return null;

            SaveData data;
            try { data = JsonUtility.FromJson<SaveData>(json); }
            catch { return null; }

            if (data == null || data.version != 1 || data.stage < 1 || data.hearts < 1 || data.maxHearts < 1)
                return null;
            return Run.FromSave(data);
        }

        public static void Delete()
        {
            try
            {
                PlayerPrefs.DeleteKey(Key);
                PlayerPrefs.Save();
            }
            catch { }
        }
    }
}
