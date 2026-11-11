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
        public string character = "wick"; id of the character playing; unknown ids load as Wick
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
            WriteData(run.ToSave());
        }

        /// <summary>
        /// Writes a mid-stage heart loss into the stage-start save without banking mid-stage score,
        /// so quitting to title or closing the app cannot restore lost hearts.
        /// </summary>
        public static void UpdateHearts(int hearts)
        {
            if (hearts <= 0)
            {
                Delete();
                return;
            }
            SaveData data = ReadData();
            if (data == null || hearts >= data.hearts) return;
            data.hearts = hearts;
            WriteData(data);
        }

        public static Run Load()
        {
            SaveData data = ReadData();
            return data == null ? null : Run.FromSave(data);
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

        static SaveData ReadData()
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
            return data;
        }

        static void WriteData(SaveData data)
        {
            string json = JsonUtility.ToJson(data);
            try
            {
                PlayerPrefs.SetString(Key, json);
                PlayerPrefs.Save();
            }
            catch { }
        }
    }
}
