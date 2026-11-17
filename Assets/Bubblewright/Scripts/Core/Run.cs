using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// One run, from stage 1 until the flame goes out or the last stage is cleared. Permadeath: nothing
    /// carries over to the next run except the best score. Hearts, score and upgrades live here, and the
    /// game copies the upgrade numbers onto Wick and the harpoons at the start of every stage.
    /// </summary>
    public partial class Run
    {
        public int Stage = 1;
        public int Score;
        public int Hearts;
        public int MaxHearts;
        public readonly int Seed;
        public Character Character;

        // Upgrade numbers, all neutral at the start of a run.
        public float SpeedMultiplier = 1f;
        public float HarpoonSpeedMultiplier = 1f;
        public float HarpoonWidthMultiplier = 1f;
        public float CableHold;
        public float ExtraInvulnerable;
        public int Shots = 1;
        /// <summary>Double jumps in the air before landing (the Updraft upgrade).</summary>
        public int AirJumps;

        // Hard worlds, indexed by world number 1 to 6. A hard world has less time, one debuff and richer rewards.
        public readonly bool[] hardWorld = new bool[7];
        public readonly string[] worldDebuff = new string[7];

        readonly List<Upgrade> taken = new List<Upgrade>();

        public Run(int seed, Character character = null)
        {
            Seed = seed;
            Character = character ?? Character.Wick;
            MaxHearts = Hearts = Character.StartHearts;
            Shots = Character.StartShots;
        }

        public bool IsLastStage => Stage >= Tuning.I.RunLength;

        /// <summary>World 1 to 6.</summary>
        public int World => WorldOf(Stage);
        /// <summary>Stage inside its world, 1 to 6.</summary>
        public int StageInWorld => StageInWorldOf(Stage);
        /// <summary>The last stage of a world is its boss stage: one flat floor, no generated bubbles, a boss.</summary>
        public bool IsBossStage => IsBossStageNumber(Stage);
        public string WorldName => WorldNameOf(World);

        public bool IsHard => World < hardWorld.Length && hardWorld[World];
        /// <summary>The debuff of the world being played, or null when it is a calm world.</summary>
        public Debuff CurrentDebuff => IsHard ? Debuff.ByName(worldDebuff[World]) : null;
        public float EffectiveSpeedMultiplier => SpeedMultiplier * (CurrentDebuff?.speedScale ?? 1f);

        public void SetHard(int world, Debuff d)
        {
            if (world < 1 || world >= hardWorld.Length) return;
            hardWorld[world] = d != null;
            worldDebuff[world] = d?.Name;
        }

        /// <summary>All scoring goes through here, so a hard world can count double.</summary>
        public void AddScore(int points) => Score += Mathf.RoundToInt(points * (IsHard ? Tuning.I.hardScoreScale : 1f));

        /// <summary>The pick after the boss of a hard world: more cards, and more than one can be taken.</summary>
        public bool IsRichPick => IsBossStage && IsHard;

        /// <summary>
        /// Whether the left or right choice comes after this stage: after the boss of worlds 1 to 5, never after the
        /// last boss and never after any other stage.
        /// </summary>
        public static bool PathFollows(int stage) => IsBossStageNumber(stage) && stage < Tuning.I.RunLength;

        /// <summary>One of the two doors is the hard road. It carries a debuff for the whole next world.</summary>
        public readonly struct RoadChoice
        {
            public readonly bool hardIsRight;
            public readonly Debuff debuff;
            public RoadChoice(bool hardIsRight, Debuff debuff) { this.hardIsRight = hardIsRight; this.debuff = debuff; }
        }

        /// <summary>
        /// Which door is hard (50/50) and which debuff it carries (any debuff, but not the one the world just finished
        /// had if that world was hard). Depends only on the seed and the stage, so a reopened screen shows the same doors.
        /// </summary>
        public RoadChoice RollRoad()
        {
            var rng = StageRng(2);
            bool hardIsRight = rng.Next(2) == 1;
            var finished = CurrentDebuff;
            var pool = new List<Debuff>();
            foreach (var d in Debuff.All) if (d != finished) pool.Add(d);
            return new RoadChoice(hardIsRight, pool[rng.Next(pool.Count)]);
        }

        /// <summary>The choice after this world's boss: the whole NEXT world is hard with this debuff, or calm when null.</summary>
        public void ChooseRoad(Debuff hardDebuff) => SetHard(World + 1, hardDebuff);

        /// <summary>The worlds played so far as "C H C" (calm or hard), the current world last.</summary>
        public string RoadSummary()
        {
            var parts = new List<string>();
            for (int w = 1; w <= World && w < hardWorld.Length; w++) parts.Add(hardWorld[w] ? "H" : "C");
            return string.Join(" ", parts);
        }

        /// <summary>"25% less time", from the hard world clock.</summary>
        public static string HardRoadNumbers() =>
            $"{Mathf.RoundToInt((1f - Tuning.I.hardTimeScale) * 100f)}% less time";

        /// <summary>"Reward: double score, and pick 2 of 4 upgrades after the boss".</summary>
        public static string HardRoadReward()
        {
            var t = Tuning.I;
            float s = t.hardScoreScale;
            string score = Mathf.Approximately(s, 2f) ? "double score" : Mathf.Approximately(s, 3f) ? "triple score" : $"score x{s:0.##}";
            return $"Reward: {score}, and pick {t.hardRewardPicks} of {t.hardRewardCards} upgrades after the boss";
        }

        public static int WorldOf(int stage) => (Mathf.Max(1, stage) - 1) / Tuning.I.stagesPerWorld + 1;
        public static int StageInWorldOf(int stage) => (Mathf.Max(1, stage) - 1) % Tuning.I.stagesPerWorld + 1;
        public static bool IsBossStageNumber(int stage) => StageInWorldOf(stage) == Tuning.I.stagesPerWorld;
        public static string WorldNameOf(int world)
        {
            var names = Tuning.I.worldNames;
            return names != null && world >= 1 && world <= names.Length ? names[world - 1] : $"World {world}";
        }

        /// <summary>
        /// Random numbers depend only on the run seed, the stage and a salt, so a stage (and its upgrade cards)
        /// can be rebuilt exactly later, for example from a saved run.
        /// </summary>
        public System.Random StageRng(int salt = 0) => StageRngFor(Stage, salt);

        /// <summary>The same random numbers StageRng gives when the run is on that stage (for the balance log).</summary>
        public System.Random StageRngFor(int stage, int salt = 0) => new System.Random(unchecked(Seed * 7919 + stage * 104729 + salt * 31));

        /// <summary>Whether a world (1 to 6) of this run is hard.</summary>
        public bool IsHardWorld(int world) => world >= 1 && world < hardWorld.Length && hardWorld[world];

        public int TimesTaken(Upgrade u)
        {
            int n = 0;
            foreach (var t in taken) if (t == u) n++;
            return n;
        }

        public void Take(Upgrade u)
        {
            u.Apply(this);
            taken.Add(u);
        }

        /// <summary>"Twin Cable, Swift Boots x2". Heals are not part of the build.</summary>
        public string BuildSummary()
        {
            var parts = new List<string>();
            foreach (var u in Upgrade.All)
            {
                if (u.Max == 0) continue;
                int n = TimesTaken(u);
                if (n > 0) parts.Add(n > 1 ? $"{u.Name} x{n}" : u.Name);
            }
            return parts.Count == 0 ? "nothing yet" : string.Join(", ", parts);
        }

        /// <summary>Ids and mirror flags of the five stages picked for the current world. Null until that world is built.</summary>
        public string[] stageIds;
        public bool[] stageMirrors;
        int pickedWorld = -1;
        bool pickedHard;
        bool hasPick;
        int savedWorld = -1;
        bool savedHard;
        bool hasSaved;
        string[] savedIds;
        bool[] savedMirrors;

        public bool HasStagePick(int world, bool hard) => hasPick && pickedWorld == world && pickedHard == hard;

        public void StoreStagePick(int world, bool hard, string[] ids, bool[] mirrors)
        {
            pickedWorld = world;
            pickedHard = hard;
            hasPick = true;
            stageIds = ids;
            stageMirrors = mirrors;
        }

        /// <summary>Remember a version 2 save. Call this after Stage and the hard-world flags are restored.</summary>
        public void NoteSavedStages(string[] ids, bool[] mirrors)
        {
            if (ids == null || ids.Length != StagePicker.Slots) return;
            hasSaved = true;
            savedWorld = World;
            savedHard = IsHard;
            savedIds = (string[])ids.Clone();
            savedMirrors = new bool[StagePicker.Slots];
            if (mirrors != null)
                for (int i = 0; i < Mathf.Min(StagePicker.Slots, mirrors.Length); i++)
                    savedMirrors[i] = mirrors[i];
        }

        public bool SavedStagesMatch(int world, bool hard) => hasSaved && savedWorld == world && savedHard == hard;
        public string SavedStageId(int slot) => savedIds != null && slot >= 0 && slot < savedIds.Length ? savedIds[slot] : null;
        public bool SavedStageMirror(int slot) => savedMirrors != null && slot >= 0 && slot < savedMirrors.Length && savedMirrors[slot];

        public SaveData ToSave()
        {
            var names = new string[taken.Count];
            for (int i = 0; i < taken.Count; i++) names[i] = taken[i].Name;
            var save = new SaveData
            {
                version = 2,
                seed = Seed,
                stage = Stage,
                score = Score,
                hearts = Hearts,
                maxHearts = MaxHearts,
                upgrades = names,
                character = Character.id,
                hardWorld = (bool[])hardWorld.Clone(),
                worldDebuff = (string[])worldDebuff.Clone(),
                stageIds = stageIds == null ? null : (string[])stageIds.Clone(),
                stageMirrors = stageMirrors == null ? null : (bool[])stageMirrors.Clone(),
            };
            SaveWeapons(save);
            return save;
        }

        public static Run FromSave(SaveData d)
        {
            if (d == null || (d.version != 1 && d.version != 2)) return null;
            var run = new Run(d.seed, Character.ById(d.character));
            if (d.upgrades != null)
            {
                foreach (var name in d.upgrades)
                {
                    var u = Upgrade.ByName(name);
                    if (u != null) run.Take(u);
                }
            }
            run.Stage = d.stage;
            run.Score = d.score;
            run.Hearts = d.hearts;
            run.MaxHearts = d.maxHearts;
            if (d.hardWorld != null)
                for (int i = 0; i < Mathf.Min(run.hardWorld.Length, d.hardWorld.Length); i++)
                    run.hardWorld[i] = d.hardWorld[i];
            if (d.worldDebuff != null)
                for (int i = 0; i < Mathf.Min(run.worldDebuff.Length, d.worldDebuff.Length); i++)
                    run.worldDebuff[i] = string.IsNullOrEmpty(d.worldDebuff[i]) ? null : d.worldDebuff[i];
            run.LoadWeapons(d);
            if (d.version >= 2) run.NoteSavedStages(d.stageIds, d.stageMirrors);
            return run;
        }

        /// <summary>
        /// Up to count different upgrades this run can still take, in random order. Each slot is rare with
        /// Tuning.rareChance; with ensureRare the first slot is always rare (when any rare can still be offered).
        /// </summary>
        public List<Upgrade> RollChoices(int count = 3, bool ensureRare = false)
        {
            var common = new List<Upgrade>();
            var rare = new List<Upgrade>();
            foreach (var u in Upgrade.All)
            {
                if (!u.CanOffer(this)) continue;
                (u.Rarity == Rarity.Rare ? rare : common).Add(u);
            }
            var picks = new List<Upgrade>();
            var rng = StageRng(1);
            for (int slot = 0; slot < count && (common.Count > 0 || rare.Count > 0); slot++)
            {
                bool wantRare = (ensureRare && slot == 0) || rng.NextDouble() < Tuning.I.rareChance;
                var pool = wantRare ? (rare.Count > 0 ? rare : common) : (common.Count > 0 ? common : rare);
                int i = rng.Next(pool.Count);
                picks.Add(pool[i]);
                pool.RemoveAt(i);
            }
            return picks;
        }
    }

    public enum Rarity { Common, Rare }

    /// <summary>
    /// What a hard world does to the character. Placeholder set that Kokan will redesign, so it is all data.
    /// The scales multiply the run's numbers for the whole world and stop when the next world starts.
    /// </summary>
    public class Debuff
    {
        public string Name;
        public string Text;
        public float speedScale = 1f;
        public float invulnerableScale = 1f;
        public float harpoonSpeedScale = 1f;
        public bool noRelight;

        public static readonly Debuff[] All =
        {
            new Debuff { Name = "Heavy Boots", Text = "Walk 15% slower.", speedScale = 0.85f },
            new Debuff { Name = "Short Glow", Text = "Half the safe time after a hit.", invulnerableScale = 0.5f },
            new Debuff { Name = "Frayed Cable", Text = "Harpoons fly 20% slower.", harpoonSpeedScale = 0.8f },
            new Debuff { Name = "Cold Wick", Text = "No Relight cards in this world.", noRelight = true },
        };

        public static Debuff ByName(string name)
        {
            foreach (var d in All) if (d.Name == name) return d;
            return null;
        }
    }

    /// <summary>
    /// A pick between stages. Placeholder set: every upgrade changes a number the prototype already has,
    /// so none of them needs new art. Max 0 means it can be taken any number of times and is not listed in the build.
    /// </summary>
    public class Upgrade
    {
        public string Name;
        public string Text;
        public int Max = 1;
        public Rarity Rarity;
        public Func<Run, bool> Extra = _ => true;
        public Action<Run> Apply;

        public bool CanOffer(Run r) => (Max == 0 || r.TimesTaken(this) < Max) && Extra(r);

        public static readonly Upgrade[] All =
        {
            new Upgrade { Name = "Twin Cable", Text = "One more harpoon on screen at a time.", Max = 2, Rarity = Rarity.Rare, Apply = r => r.Shots++ },
            new Upgrade { Name = "Anchor Tip", Text = "The tip hooks into the ceiling and the cable stays for 1.5 s.", Rarity = Rarity.Rare, Apply = r => r.CableHold = 1.5f },
            new Upgrade { Name = "Quick Winch", Text = "Harpoons fly 30% faster.", Max = 2, Apply = r => r.HarpoonSpeedMultiplier += 0.3f },
            new Upgrade { Name = "Wide Barbs", Text = "Harpoons hit bubbles from twice as far to the side.", Apply = r => r.HarpoonWidthMultiplier = 2f },
            new Upgrade { Name = "Swift Boots", Text = "Run 12% faster.", Max = 3, Apply = r => r.SpeedMultiplier += 0.12f },
            new Upgrade { Name = "Long Glow", Text = "Stay safe 0.75 s longer after a hit.", Max = 2, Apply = r => r.ExtraInvulnerable += 0.75f },
            new Upgrade { Name = "Updraft", Text = "Jump once more in mid-air. It comes back when you land.", Rarity = Rarity.Rare, Apply = r => r.AirJumps = 1 },
            new Upgrade { Name = "Spare Wick", Text = "One more heart, and it starts lit.", Max = 2, Rarity = Rarity.Rare, Apply = r => { r.MaxHearts++; r.Hearts++; } },
            new Upgrade { Name = "Relight", Text = "Get back one lost heart.", Max = 0, Extra = r => r.Hearts < r.MaxHearts && !(r.CurrentDebuff?.noRelight ?? false), Apply = r => r.Hearts++ },
        };

        public static Upgrade ByName(string name)
        {
            foreach (var u in All) if (u.Name == name) return u;
            return null;
        }
    }

    /// <summary>The geometry and the enemies of one stage.</summary>
    public class StagePlan
    {
        public StageMap map;
        public List<SpawnSpec> spawns;
        public List<RollerSpec> rollers = new List<RollerSpec>();
        /// <summary>Where Wick stands at the start. y is 0 on a one-storey stage.</summary>
        public Vector2 wickPosition;
        /// <summary>Horizontal start. Stages still assign this until they set wickPosition themselves.</summary>
        public float wickX
        {
            get => wickPosition.x;
            set => wickPosition.x = value;
        }
        /// <summary>Which storey the reinforcement waves belong to. Negative means the top storey.</summary>
        public int waveStorey = -1;
        /// <summary>The layout's name, for the balance page.</summary>
        public string layoutName;
        /// <summary>The authored stage, when this plan came from a StageSpec.</summary>
        public string stageId;
        public int difficulty;
        public string concept;
        /// <summary>The recipe that shaped this stage. Null on a boss stage and when no recipe was used.</summary>
        public string recipeName;
        /// <summary>The placement pattern that put the bubbles down. Null when no recipe was used.</summary>
        public string patternName;
        /// <summary>The kind that visits as a guest, or null.</summary>
        public string guestName;
        /// <summary>Steel Pogos: unbreakable, not needed to clear and outside the budget, so kept apart from spawns.</summary>
        public List<SpawnSpec> steelPogos = new List<SpawnSpec>();
        /// <summary>Bubbles that arrive after the start, one list per wave. Positions: only x is used (the wished entry point).</summary>
        public List<List<SpawnSpec>> waves = new List<List<SpawnSpec>>();
        /// <summary>Multiplies the stage's storm clock (1 unless the recipe says otherwise).</summary>
        public float timeScale = 1f;
        /// <summary>The whole budget of the stage (jump bonus and hard world included), and the part rollers took.</summary>
        public float budget, rollerSpend;
    }

    /// <summary>A rolling balloon at the start of a stage: resting on surfaceY (0 ground, 5 or 10 a platform), centred on x.</summary>
    [Serializable]
    public struct RollerSpec
    {
        public int tier;
        public float x;
        public float surfaceY;
        [Tooltip("-1 left, +1 right")] public int direction;
        /// <summary>Indestructible and not needed to clear the stage. Costs no budget.</summary>
        public bool steel;
        /// <summary>Takes Tuning.ceramicHits hits to break.</summary>
        public bool ceramic;
    }
}

