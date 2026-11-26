using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// A playable hero. All stats live in the one table in All, so Kokan can redesign them in one place.
    /// Wick's row is read from Tuning (the Tuning fields stay as Wick's values, so live tuning still works);
    /// the other rows use their own numbers. Upgrades multiply the base numbers on top.
    /// </summary>
    public class Character
    {
        public string id, name, blurb;
        public int hearts;
        public float runSpeed;
        public int shots;
        public float harpoonSpeed;
        public float invulnerableTime;
        /// <summary>The cloak colours that replace saffron.</summary>
        public Color32 body, bodyDark, bodyLine;
        /// <summary>Id of the character whose finished run unlocks this one. Null means open from the start.</summary>
        public string unlockedByFinishingWith;

        public const string WickId = "wick";
        public bool IsWick => id == WickId;

        // Effective starting numbers: Wick follows Tuning so tuning the asset still changes him.
        public int StartHearts => IsWick ? Tuning.I.hearts : hearts;
        public float BaseRunSpeed => IsWick ? Tuning.I.runSpeed : runSpeed;
        public int StartShots => shots;
        public float BaseHarpoonSpeed => IsWick ? Tuning.I.harpoonSpeed : harpoonSpeed;
        public float BaseInvulnerable => IsWick ? Tuning.I.invulnerableTime : invulnerableTime;

        public static readonly Character[] All =
        {
            new Character
            {
                id = "wick", name = "Wick", blurb = "Steady all-rounder.",
                hearts = 3, runSpeed = 9f, shots = 1, harpoonSpeed = 20f, invulnerableTime = 1.5f,
                body = Palette.Hex(0xFFA53A), bodyDark = Palette.Hex(0xD9782A), bodyLine = Palette.Hex(0xB85A1C),
                unlockedByFinishingWith = null,
            },
            new Character
            {
                id = "ember", name = "Ember", blurb = "Fast and fragile.",
                hearts = 2, runSpeed = 10.5f, shots = 1, harpoonSpeed = 24f, invulnerableTime = 1.2f,
                body = Palette.Hex(0xFF8FB3), bodyDark = Palette.Hex(0xD9648C), bodyLine = Palette.Hex(0xA8456A),
                unlockedByFinishingWith = null,
            },
            new Character
            {
                id = "moss", name = "Moss", blurb = "Slow and sturdy.",
                hearts = 4, runSpeed = 7.5f, shots = 1, harpoonSpeed = 18f, invulnerableTime = 2.0f,
                body = Palette.Hex(0x8FCB6B), bodyDark = Palette.Hex(0x62A04A), bodyLine = Palette.Hex(0x3F7A33),
                unlockedByFinishingWith = "wick",
            },
            new Character
            {
                id = "flint", name = "Flint", blurb = "Two cables, slow shots.",
                hearts = 3, runSpeed = 8.5f, shots = 2, harpoonSpeed = 16f, invulnerableTime = 1.0f,
                body = Palette.Hex(0xB9C2E0), bodyDark = Palette.Hex(0x8892B8), bodyLine = Palette.Hex(0x5F688F),
                unlockedByFinishingWith = "ember",
            },
        };

        public static Character Wick => All[0];

        // Text limits the select cards are built for (CharacterTests checks them).
        public const int MaxNameLength = 8, MaxBlurbLength = 38, MaxUnlockHintLength = 56;

        public int SpeedPips => RankPips(BaseRunSpeed, c => c.BaseRunSpeed);
        public int ShotSpeedPips => RankPips(BaseHarpoonSpeed, c => c.BaseHarpoonSpeed);

        /// <summary>1 to 5 pips: where the value sits between the lowest and highest across all characters.</summary>
        static int RankPips(float value, System.Func<Character, float> read)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var c in All)
            {
                float v = read(c);
                lo = Mathf.Min(lo, v);
                hi = Mathf.Max(hi, v);
            }
            return hi - lo < 0.0001f ? 3 : 1 + Mathf.RoundToInt(4f * (value - lo) / (hi - lo));
        }

        /// <summary>"Finish a run with WICK to unlock", or null when the character is open from the start.</summary>
        public string UnlockHint => unlockedByFinishingWith == null ? null : $"Finish a run with {UnlockerName.ToUpperInvariant()} to unlock";

        /// <summary>Unknown or empty ids give Wick.</summary>
        public static Character ById(string id)
        {
            foreach (var c in All) if (c.id == id) return c;
            return Wick;
        }

        /// <summary>The name of the character that has to finish a run to open this one, or null when it is open.</summary>
        public string UnlockerName => unlockedByFinishingWith == null ? null : ById(unlockedByFinishingWith).name;
    }

    /// <summary>
    /// Which characters are open. Stored in PlayerPrefs as a comma separated list of ids. Wick and Ember are
    /// open from the start. DebugAll opens everything for this session only and is never written to PlayerPrefs.
    /// </summary>
    public static class Unlocks
    {
        public const string Key = "Bubblewright.Unlocks";
        public const string LastCharacterKey = "Bubblewright.LastCharacter";

        /// <summary>Debug panel cheat: treat every character as open.</summary>
        public static bool DebugAll;

        public static bool IsOpen(Character c)
        {
            if (c == null) return false;
            if (c.unlockedByFinishingWith == null || DebugAll) return true;
            return Contains(Read(), c.id);
        }

        /// <summary>Returns true if the character was not unlocked before.</summary>
        public static bool Unlock(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            string list = Read();
            if (Contains(list, id)) return false;
            list = string.IsNullOrEmpty(list) ? id : list + "," + id;
            try { PlayerPrefs.SetString(Key, list); PlayerPrefs.Save(); } catch { }
            return true;
        }

        public static void Reset()
        {
            try { PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); } catch { }
        }

        public static Character LastCharacter()
        {
            try { return Character.ById(PlayerPrefs.GetString(LastCharacterKey, Character.WickId)); }
            catch { return Character.Wick; }
        }

        public static void RememberCharacter(Character c)
        {
            try { PlayerPrefs.SetString(LastCharacterKey, c.id); PlayerPrefs.Save(); } catch { }
        }

        static string Read()
        {
            try { return PlayerPrefs.GetString(Key, ""); } catch { return ""; }
        }

        static bool Contains(string list, string id)
        {
            if (string.IsNullOrEmpty(list)) return false;
            foreach (var part in list.Split(',')) if (part.Trim() == id) return true;
            return false;
        }
    }
}
