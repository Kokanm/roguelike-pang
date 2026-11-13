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
    public class Run
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

        // Hard worlds, indexed by world number 1 to 6. A hard world has more bubbles, less time, one debuff and richer rewards.
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

        /// <summary>"30% more bubbles, 25% less time", from the hard world numbers.</summary>
        public static string HardRoadNumbers() =>
            $"{Mathf.RoundToInt((Tuning.I.hardBudgetScale - 1f) * 100f)}% more bubbles, {Mathf.RoundToInt((1f - Tuning.I.hardTimeScale) * 100f)}% less time";

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

        public SaveData ToSave()
        {
            var names = new string[taken.Count];
            for (int i = 0; i < taken.Count; i++) names[i] = taken[i].Name;
            return new SaveData
            {
                version = 1,
                seed = Seed,
                stage = Stage,
                score = Score,
                hearts = Hearts,
                maxHearts = MaxHearts,
                upgrades = names,
                character = Character.id,
                hardWorld = (bool[])hardWorld.Clone(),
                worldDebuff = (string[])worldDebuff.Clone(),
            };
        }

        public static Run FromSave(SaveData d)
        {
            if (d == null || d.version != 1) return null;
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
            new Upgrade { Name = "Spare Wick", Text = "One more heart, and it starts lit.", Max = 2, Rarity = Rarity.Rare, Apply = r => { r.MaxHearts++; r.Hearts++; } },
            new Upgrade { Name = "Relight", Text = "Get back one lost heart.", Max = 0, Extra = r => r.Hearts < r.MaxHearts && !(r.CurrentDebuff?.noRelight ?? false), Apply = r => r.Hearts++ },
        };

        public static Upgrade ByName(string name)
        {
            foreach (var u in All) if (u.Name == name) return u;
            return null;
        }
    }

    /// <summary>
    /// Builds a stage from the sheet's bubble budget: each stage gets a budget that grows by stage, and spends it
    /// on bubbles (XL 8, L 4, M 2, S 1, times a cost per kind). Trigloops and Leads join in later stages.
    /// </summary>
    public class StagePlan
    {
        public StageMap map;
        public List<SpawnSpec> spawns;
        public List<RollerSpec> rollers = new List<RollerSpec>();
        public float wickX;
        /// <summary>The layout's name, for the balance page.</summary>
        public string layoutName;
        /// <summary>The recipe that shaped this stage. Null on a boss stage and when no recipe was used.</summary>
        public string recipeName;
        /// <summary>The placement pattern that put the bubbles down. Null when no recipe was used.</summary>
        public string patternName;
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

    public static class StageGenerator
    {
        /// <summary>Bubble budget of a stage (1 to 36) from the world and the stage inside it.</summary>
        public static float Budget(int stage, bool hard = false)
        {
            var t = Tuning.I;
            float budget = t.budgetBase + t.budgetPerWorld * (Run.WorldOf(stage) - 1) + t.budgetPerStage * (Run.StageInWorldOf(stage) - 1);
            budget *= t.jumpBudgetBonus; // jumping makes dodging easier
            return hard ? budget * t.hardBudgetScale : budget;
        }

        /// <summary>Most bubbles at the start: 4, 4, 5, 5, 6, 6 for worlds 1 to 6.</summary>
        public static int MaxStartBubbles(int stage) => Tuning.I.startBubblesBase + (Run.WorldOf(stage) + 1) / 2;

        /// <summary>Seconds on the storm clock for a stage (1 to 36).</summary>
        public static float StageTime(int stage, bool hard = false)
        {
            var t = Tuning.I;
            float time = t.timeBase + t.timePerWorld * (Run.WorldOf(stage) - 1) + t.timePerStage * (Run.StageInWorldOf(stage) - 1);
            if (Run.WorldOf(stage) == 3) time *= t.desertTimeScale; // Desert: everything is slower on sand
            return hard ? time * t.hardTimeScale : time;
        }

        /// <summary>Seconds on the storm clock of a boss stage. A hard world still scales it.</summary>
        public static float BossTime(bool hard = false) => hard ? Tuning.I.bossTime * Tuning.I.hardTimeScale : Tuning.I.bossTime;

        public static LayoutDef PickLayout(Run run, System.Random rng) => PickLayout(run.Stage, rng);

        static LayoutDef PickLayout(int stage, System.Random rng)
        {
            int world = Run.WorldOf(stage);
            int stageInWorld = Run.StageInWorldOf(stage);
            if (Run.IsBossStageNumber(stage) || world == 1 && stageInWorld <= 2)
                return Mirrored(StageLayouts.All[0], rng);

            if (world == 5)
            {
                var catacombs = new List<LayoutDef>();
                foreach (var layout in StageLayouts.All)
                    if (layout.onlyWorld == 5)
                        catacombs.Add(layout);
                return Mirrored(catacombs[rng.Next(catacombs.Count)], rng);
            }

            int wantedLevels = 2;
            if (world >= 2 && stageInWorld >= 2)
                wantedLevels = rng.Next(3) == 0 ? 2 : 3;

            // From world 2 on, about one stage in three has gaps in the ground (never a boss stage: those returned above).
            bool wantGaps = world >= 2 && rng.NextDouble() < Tuning.I.gapLayoutChance;

            var choices = new List<LayoutDef>();
            foreach (var layout in StageLayouts.All)
                if (layout.levels == wantedLevels && layout.minWorld <= world && layout.HasGaps == wantGaps &&
                    (layout.onlyWorld == 0 || layout.onlyWorld == world))
                    choices.Add(layout);
            if (choices.Count == 0)
                foreach (var layout in StageLayouts.All)
                    if (layout.levels == wantedLevels && layout.minWorld <= world &&
                        (layout.onlyWorld == 0 || layout.onlyWorld == world))
                        choices.Add(layout);

            // minWorld can remove a whole tier from a future data set; fall back to any eligible non-flat layout.
            if (choices.Count == 0)
                foreach (var layout in StageLayouts.All)
                    if (layout.levels > 1 && layout.minWorld <= world &&
                        (layout.onlyWorld == 0 || layout.onlyWorld == world))
                        choices.Add(layout);

            return Mirrored(choices[rng.Next(choices.Count)], rng);
        }

        static LayoutDef Mirrored(LayoutDef layout, System.Random rng)
        {
            if (rng.NextDouble() >= 0.5) return layout;
            return new LayoutDef
            {
                name = layout.name,
                levels = layout.levels,
                minWorld = layout.minWorld,
                onlyWorld = layout.onlyWorld,
                needsBreaking = layout.needsBreaking,
                bubbleMinY = layout.bubbleMinY,
                rows = StageLayouts.Mirror(layout.rows),
            };
        }

        public static StagePlan Generate(Run run) => GenerateFor(run, run.Stage);

        /// <summary>
        /// The stage with only its layout re-rolled (debug NEXT LAYOUT): the stage's own random numbers still choose
        /// the bubbles, a separate stream chooses the layout. layoutReroll 0 is the stage as normally generated.
        /// </summary>
        public static StagePlan Generate(Run run, int layoutReroll) => GenerateFor(run, run.Stage, layoutReroll);

        public static StagePlan Generate(Run run, LayoutDef layout) =>
            Generate(run.Stage, run.StageRng(0), run.IsHard, layout, null, RecipeFor(run.Seed, run.Stage));

        public static StagePlan Generate(int stage, System.Random rng, bool hard = false) =>
            Generate(stage, rng, hard, null);

        /// <summary>
        /// The stage a run would build, including the recipe drawn for this seed and stage. layoutReroll above 0
        /// re-rolls only the layout. forced, when set, is used instead of the drawn recipe. A boss stage has no recipe.
        /// </summary>
        public static StagePlan GenerateFor(Run run, int stage, int layoutReroll = 0, RecipeDef forced = null)
        {
            RecipeDef recipe = forced ?? RecipeFor(run.Seed, stage);
            bool hard = run.IsHardWorld(Run.WorldOf(stage));
            System.Random layoutRng = layoutReroll <= 0 ? null : run.StageRngFor(stage, 100 + layoutReroll);
            return Generate(stage, run.StageRngFor(stage, 0), hard, null, layoutRng, recipe);
        }

        /// <summary>
        /// The recipe drawn for this seed and stage. Boss stages return null. World 1 stages 1 and 2 are Mixed.
        /// Every other normal stage takes the next row of that world's pool, shuffled from the seed, and a row
        /// repeats only after the pool is used up.
        /// </summary>
        public static RecipeDef RecipeFor(int seed, int stage)
        {
            if (Run.IsBossStageNumber(stage)) return null;
            var tuning = Tuning.I;
            int world = Run.WorldOf(stage);
            int stageInWorld = Run.StageInWorldOf(stage);
            if (world == 1 && stageInWorld <= 2) return tuning.Recipe(RecipeId.Mixed);

            var pool = new List<RecipeDef>();
            if (tuning.recipes != null)
                foreach (var row in tuning.recipes)
                    if (row != null && row.inPool && row.fromWorld <= world)
                        pool.Add(row);
            if (pool.Count == 0) return tuning.Recipe(RecipeId.Mixed);

            var draw = new System.Random(unchecked(seed * 7919 + world * 15485863 + 977));
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = draw.Next(i + 1);
                RecipeDef swap = pool[i];
                pool[i] = pool[j];
                pool[j] = swap;
            }
            int slot = world == 1 ? stageInWorld - 3 : stageInWorld - 1;
            return pool[slot % pool.Count];
        }

        static StagePlan Generate(int stage, System.Random rng, bool hard, LayoutDef layoutOverride, System.Random layoutRng = null, RecipeDef recipe = null)
        {
            var t = Tuning.I;
            LayoutDef layout = layoutOverride ?? PickLayout(stage, rng);
            if (layoutOverride == null && layoutRng != null)
            {
                // Try a few times for a different layout; small pools may only offer the same one mirrored.
                string first = layout.name;
                for (int i = 0; i < 8; i++)
                {
                    layout = PickLayout(stage, layoutRng);
                    if (layout.name != first) break;
                }
            }
            StageMap map = StageLayout.Parse(layout.rows);
            WorldRules.Apply(map, Run.WorldOf(stage), rng, Run.StageInWorldOf(stage), Run.IsBossStageNumber(stage));
            if (Run.IsBossStageNumber(stage))
                return new StagePlan { map = map, spawns = new List<SpawnSpec>(), wickX = 0f, layoutName = layout.name }; // the boss is the whole stage
            float bubbleMinY = layout.bubbleMinY > 0f ? layout.bubbleMinY : 5f;
            float budget = Budget(stage, hard);
            float fullBudget = budget;
            // A rolling-floor recipe needs room for several balls. Without it, the stage is Mixed.
            if (recipe != null && recipe.rollerShare > 0f && RollerSpots(map, 0f).Count < 2)
                recipe = t.Recipe(RecipeId.Mixed);
            var rollers = new List<RollerSpec>();
            float rollerSpend = AddRollers(map, stage, rng, ref budget, rollers, 0f, recipe);
            int maxBubbles = StartCap(stage, recipe);
            bool hasWaves = recipe != null && recipe.waves > 0;
            // With waves, pick for the whole budget: the start cap is raised so there is something left to send later.
            if (hasWaves) maxBubbles = Mathf.RoundToInt(maxBubbles / Mathf.Max(0.05f, recipe.startShare));
            var picks = new List<(BubbleKind kind, int tier, bool ceramic)>();
            bool ceramicWorld = Run.WorldOf(stage) >= t.ceramicFromWorld;

            while (picks.Count < maxBubbles)
            {
                var kind = PickKind(stage, rng);
                int tier = BiggestAffordable(kind, budget);
                // Outside the recipe's tiers counts as unaffordable, so the Gloop fallback still applies.
                if (recipe != null) tier = TierInRange(recipe, tier);
                if (tier < 0 && kind != BubbleKind.Gloop)
                {
                    kind = BubbleKind.Gloop;
                    tier = BiggestAffordable(kind, budget);
                    if (recipe != null) tier = TierInRange(recipe, tier);
                }
                if (tier < 0) break;
                // After the first bubble, sometimes go one size down so stages are not all XLs.
                // A recipe keeps that step inside its tier range, and does not roll when it cannot.
                bool canShrink = tier < t.tiers.Length - 1 && (recipe == null || tier < recipe.maxTier);
                if (picks.Count > 0 && canShrink && rng.NextDouble() < 0.35) tier++;
                // From ceramicFromWorld on some bubbles are ceramic: they take more hits, so they cost more budget.
                // A recipe with ceramicChance at 0 or above uses that chance in every world.
                bool ceramic;
                if (recipe != null && recipe.ceramicChance >= 0f)
                    ceramic = kind != BubbleKind.Lucky && tier <= t.ceramicMaxTier && rng.NextDouble() < recipe.ceramicChance;
                else
                    ceramic = ceramicWorld && kind != BubbleKind.Lucky && tier <= t.ceramicMaxTier && rng.NextDouble() < t.ceramicChance;
                float cost = Cost(kind, tier) * (ceramic ? t.ceramicCostScale : 1f);
                if (ceramic && cost > budget + 0.001f)
                {
                    ceramic = false;
                    cost = Cost(kind, tier);
                }
                budget -= cost;
                picks.Add((kind, tier, ceramic));
            }
            var waveLists = new List<List<SpawnSpec>>();
            if (hasWaves) picks = SplitWaves(picks, recipe, rng, waveLists);
            if (stage > 1 && rng.NextDouble() < t.luckyChance) picks.Add((BubbleKind.Lucky, 2, false));

            // Biggest first. Flat stages assign the furthest random positions to the biggest bubbles.
            picks.Sort((a, b) => a.tier.CompareTo(b.tier));
            var specs = new List<SpawnSpec>();
            // A recipe names the placement patterns it may use. One is drawn only when there is a choice, so a
            // recipe with just Scattered draws the same random numbers as before patterns existed.
            PlacementPattern pattern = PlacementPattern.Scattered;
            if (recipe != null && recipe.patterns != null && recipe.patterns.Length > 0)
                pattern = recipe.patterns.Length == 1 ? recipe.patterns[0] : recipe.patterns[rng.Next(recipe.patterns.Length)];
            List<SpawnSpec> patterned = pattern == PlacementPattern.Scattered ? null : PlacementPatterns.Place(pattern, map, picks, bubbleMinY, rng);
            if (patterned != null)
            {
                specs.AddRange(patterned);
            }
            else if (layout.levels == 1)
            {
                var positions = new List<Vector2>();
                float largestClearance = t.tiers[0].diameter * 0.5f + 0.25f;
                for (int i = 0; i < picks.Count; i++)
                    if (TryPosition(map, largestClearance, bubbleMinY, 0f, rng, out Vector2 position))
                        positions.Add(position);
                positions.Sort((a, b) => Mathf.Abs(b.x).CompareTo(Mathf.Abs(a.x)));
                int count = Mathf.Min(picks.Count, positions.Count);
                for (int i = 0; i < count; i++)
                    specs.Add(Spec(picks[i], positions[i], rng));
            }
            else
            {
                foreach (var pick in picks)
                {
                    float clearance = t.tiers[pick.tier].diameter * 0.5f + 0.25f;
                    if (TryPosition(map, clearance, bubbleMinY, 0f, rng, out Vector2 position))
                        specs.Add(Spec(pick, position, rng));
                }
            }
            return new StagePlan
            {
                map = map, spawns = specs, rollers = rollers, wickX = 0f,
                layoutName = layout.name, recipeName = recipe == null ? null : recipe.name,
                patternName = recipe == null ? null : PlacementPatterns.NameOf(pattern),
                budget = fullBudget, rollerSpend = rollerSpend,
                waves = waveLists, timeScale = recipe == null ? 1f : recipe.timeScale,
            };
        }

        /// <summary>
        /// Splits the picks of a Reinforcements stage. The biggest bubbles stay at the start until they cost half of
        /// everything, but never more than 65% (a bubble that would push the start past that waits for a wave, unless it
        /// is the only way to have a start group at all). The rest is dealt to the waves in turn, wave 1 first. An empty
        /// wave is dropped, so a stage with few bubbles has fewer waves. Returns the start picks; fills waves.
        /// </summary>
        static List<(BubbleKind kind, int tier, bool ceramic)> SplitWaves(
            List<(BubbleKind kind, int tier, bool ceramic)> picks, RecipeDef recipe, System.Random rng, List<List<SpawnSpec>> waves)
        {
            var t = Tuning.I;
            var sorted = new List<(BubbleKind kind, int tier, bool ceramic)>(picks);
            sorted.Sort((a, b) => a.tier.CompareTo(b.tier)); // biggest first
            float total = 0f;
            foreach (var p in sorted) total += PickCost(p);

            var start = new List<(BubbleKind kind, int tier, bool ceramic)>();
            var later = new List<(BubbleKind kind, int tier, bool ceramic)>();
            float startCost = 0f;
            foreach (var p in sorted)
            {
                float cost = PickCost(p);
                bool enough = startCost >= recipe.startShare * total - 0.001f;
                bool fits = start.Count == 0 || startCost + cost <= total * 0.65f + 0.001f;
                if (!enough && fits)
                {
                    start.Add(p);
                    startCost += cost;
                }
                else later.Add(p);
            }

            // Skipping big bubbles can leave the start too thin: bring the biggest waiting one that still fits back.
            while (startCost < total * 0.35f && later.Count > 0)
            {
                int best = -1;
                for (int i = 0; i < later.Count; i++)
                {
                    float cost = PickCost(later[i]);
                    if (startCost + cost > total * 0.65f + 0.001f) continue;
                    if (best < 0 || cost > PickCost(later[best])) best = i;
                }
                if (best < 0) break;
                startCost += PickCost(later[best]);
                start.Add(later[best]);
                later.RemoveAt(best);
            }

            int count = Mathf.Max(1, recipe.waves);
            var lists = new List<SpawnSpec>[count];
            for (int i = 0; i < count; i++) lists[i] = new List<SpawnSpec>();
            for (int i = 0; i < later.Count; i++)
            {
                var p = later[i];
                float radius = t.tiers[p.tier].diameter * 0.5f;
                float x = -t.HalfWidth + radius + (float)rng.NextDouble() * (2f * t.HalfWidth - 2f * radius);
                lists[i % count].Add(new SpawnSpec
                {
                    kind = p.kind, tier = p.tier, ceramic = p.ceramic,
                    position = new Vector2(x, 0f),
                    direction = rng.NextDouble() < 0.5 ? -1 : 1,
                });
            }
            foreach (var list in lists)
                if (list.Count > 0) waves.Add(list);
            return start;
        }

        static float PickCost((BubbleKind kind, int tier, bool ceramic) p) =>
            Cost(p.kind, p.tier) * (p.ceramic ? Tuning.I.ceramicCostScale : 1f);

        /// <summary>Most bubbles placed before the free Lucky one. A null recipe keeps the usual cap.</summary>
        static int StartCap(int stage, RecipeDef recipe)
        {
            if (recipe == null) return MaxStartBubbles(stage);
            var tuning = Tuning.I;
            if (recipe.id == RecipeId.Giants && Run.WorldOf(stage) >= tuning.giantsLateFromWorld)
                return tuning.giantsMaxLate;
            if (recipe.startCapMax > 0) return recipe.startCapMax;
            return Mathf.RoundToInt(MaxStartBubbles(stage) * recipe.startCapScale);
        }

        /// <summary>
        /// The affordable tier clamped into the recipe's range, or -1 when nothing in that range is affordable.
        /// A tier below minTier is a bigger bubble than the recipe allows; the biggest allowed tier is cheaper, so it still fits.
        /// </summary>
        static int TierInRange(RecipeDef recipe, int tier)
        {
            int last = Tuning.I.tiers.Length - 1;
            int minTier = Mathf.Clamp(recipe.minTier, 0, last);
            int maxTier = Mathf.Clamp(recipe.maxTier, minTier, last);
            if (tier < 0 || tier > maxTier) return -1;
            return tier < minTier ? minTier : tier;
        }

        /// <summary>Most rollers a stage of this world starts with: 0 before rollerFromWorld, 2 from rollerTwoFromWorld, capped.</summary>
        public static int RollersFor(int stage)
        {
            var t = Tuning.I;
            int world = Run.WorldOf(stage);
            if (Run.IsBossStageNumber(stage) || world < t.rollerFromWorld) return 0;
            return Mathf.Clamp(world >= t.rollerTwoFromWorld ? 2 : 1, 0, t.rollerMaxPerStage);
        }

        /// <summary>
        /// From rollerFromWorld on, on normal stages, with rollerChance a stage swaps some budget for big rollers. Each
        /// costs rollerCost from the budget before bubbles are picked, and at least one budget point is left for bubbles.
        /// A recipe with rollerShare above 0 spends up to that share instead, up to rollerMax balls. Giants and Swarm
        /// skip the budget rollers. Steel balls are still added by today's rule. A null recipe draws the same random
        /// numbers, in the same order, as before recipes existed. Returns the budget spent.
        /// </summary>
        static float AddRollers(StageMap map, int stage, System.Random rng, ref float budget, List<RollerSpec> into, float wickX, RecipeDef recipe)
        {
            var t = Tuning.I;
            if (Run.IsBossStageNumber(stage)) return 0f;
            int world = Run.WorldOf(stage);
            float spent = 0f;
            List<Vector2> spots = null;
            bool skipBudget = recipe != null && (recipe.id == RecipeId.Giants || recipe.id == RecipeId.Swarm);
            bool share = recipe != null && recipe.rollerShare > 0f && !skipBudget;

            if (share)
            {
                spots = RollerSpots(map, wickX);
                float cap = recipe.rollerShare * budget;
                for (int i = 0; i < recipe.rollerMax && spots.Count > 0; i++)
                {
                    bool forceCeramic = recipe.allRollersCeramic;
                    bool ceramic = forceCeramic || (world >= t.ceramicFromWorld && rng.NextDouble() < t.ceramicRollerChance);
                    float cost = t.rollerCost * (ceramic ? t.ceramicCostScale : 1f);
                    if (spent + cost > cap + 0.001f || cost > budget + 0.001f)
                    {
                        if (!ceramic || forceCeramic) break;
                        ceramic = false;
                        cost = t.rollerCost;
                        if (spent + cost > cap + 0.001f || cost > budget + 0.001f) break;
                    }
                    Vector2 spot = spots[rng.Next(spots.Count)];
                    into.Add(new RollerSpec { tier = 0, x = spot.x, surfaceY = spot.y, direction = rng.NextDouble() < 0.5 ? -1 : 1, ceramic = ceramic });
                    budget -= cost;
                    spent += cost;
                    spots.RemoveAll(sp => Mathf.Approximately(sp.y, spot.y) && Mathf.Abs(sp.x - spot.x) < 3f);
                }
            }
            else if (!skipBudget)
            {
                int count = RollersFor(stage);
                if (count > 0 && rng.NextDouble() < t.rollerChance)
                {
                    spots = RollerSpots(map, wickX);
                    for (int i = 0; i < count && spots.Count > 0; i++)
                    {
                        if (budget - t.rollerCost < 1f - 0.001f) break;
                        Vector2 spot = spots[rng.Next(spots.Count)];
                        bool ceramic;
                        float cost;
                        if (recipe != null && recipe.allRollersCeramic)
                        {
                            ceramic = true;
                            cost = t.rollerCost * t.ceramicCostScale;
                            if (budget - cost < 1f - 0.001f) break;
                        }
                        else
                        {
                            ceramic = world >= t.ceramicFromWorld && rng.NextDouble() < t.ceramicRollerChance;
                            cost = t.rollerCost * (ceramic ? t.ceramicCostScale : 1f);
                            if (ceramic && budget - cost < 1f - 0.001f)
                            {
                                ceramic = false;
                                cost = t.rollerCost;
                            }
                        }
                        into.Add(new RollerSpec { tier = 0, x = spot.x, surfaceY = spot.y, direction = rng.NextDouble() < 0.5 ? -1 : 1, ceramic = ceramic });
                        budget -= cost;
                        spent += cost;
                        // Keep the next one away from this one on the same surface, so they do not start touching.
                        spots.RemoveAll(sp => Mathf.Approximately(sp.y, spot.y) && Mathf.Abs(sp.x - spot.x) < 3f);
                    }
                }
            }

            // Steel balls cost no budget: they cannot be popped and are not needed to clear the stage.
            if (world >= t.steelFromWorld && t.steelMaxPerStage > 0 && rng.NextDouble() < t.steelChance)
            {
                if (spots == null) spots = RollerSpots(map, wickX);
                for (int i = 0; i < t.steelMaxPerStage && spots.Count > 0; i++)
                {
                    Vector2 spot = spots[rng.Next(spots.Count)];
                    into.Add(new RollerSpec { tier = 0, x = spot.x, surfaceY = spot.y, direction = rng.NextDouble() < 0.5 ? -1 : 1, steel = true });
                    spots.RemoveAll(sp => Mathf.Approximately(sp.y, spot.y) && Mathf.Abs(sp.x - spot.x) < 3f);
                }
            }
            return spent;
        }

        /// <summary>Shortest stretch of surface, in tiles, a roller is placed on.</summary>
        public const int RollerMinStretch = 5;
        /// <summary>A roller starts at least this far (horizontally) from Wick's start.</summary>
        public const float RollerMinWickDistance = 6f;

        /// <summary>
        /// Where a big roller may start: (centre x, surface y) at a column centre on the ground or a platform top, on a
        /// stretch of at least RollerMinStretch tiles it can roll along, not at either end of it, at least
        /// RollerMinWickDistance from wickX, and with no spike strip under it or its neighbours.
        /// </summary>
        public static List<Vector2> RollerSpots(StageMap map, float wickX)
        {
            var spots = new List<Vector2>();
            float diameter = Roller.RadiusOf(0) * 2f;
            // row -1 is the ground; any other row is a tile row whose top is a surface.
            for (int row = -1; row < StageMap.Rows - 1; row++)
            {
                float surface = row + 1f;
                if (surface + diameter > StageMap.Rows) break;
                int start = -1;
                for (int c = 0; c <= StageMap.Cols; c++)
                {
                    bool ok = c < StageMap.Cols && Rollable(map, c, row, surface, diameter);
                    if (ok && start < 0) start = c;
                    if (ok || start < 0) continue;
                    int end = c - 1;
                    if (end - start + 1 >= RollerMinStretch)
                    {
                        for (int k = start + 1; k <= end - 1; k++)
                        {
                            float x = StageMap.CenterX(k);
                            if (Mathf.Abs(x - wickX) < RollerMinWickDistance) continue;
                            if (SpikeNear(map, k, row)) continue;
                            spots.Add(new Vector2(x, surface));
                        }
                    }
                    start = -1;
                }
            }
            return spots;
        }

        static bool Rollable(StageMap map, int c, int row, float surface, float diameter)
        {
            bool floor = row < 0 ? !map.IsGap(c) : map.IsSolid(c, row);
            if (!floor) return false;
            int rMin = StageMap.Row(surface + 0.0001f), rMax = StageMap.Row(surface + diameter - 0.0001f);
            for (int r = rMin; r <= rMax; r++)
                if (map.IsSolid(c, r)) return false;
            return true;
        }

        /// <summary>A spike strip on this surface under column c or a neighbour (the ball is a little wider than a tile).</summary>
        static bool SpikeNear(StageMap map, int c, int row)
        {
            foreach (var strip in map.spikes)
            {
                if (strip.row != row) continue;
                if (c + 1 >= strip.col && c - 1 <= strip.col + strip.width - 1) return true;
            }
            return false;
        }

        /// <summary>Budget cost of one bubble (kind and tier), as the generator spends it.</summary>
        public static float BubbleCost(BubbleKind kind, int tier) => Cost(kind, tier);

        static SpawnSpec Spec((BubbleKind kind, int tier, bool ceramic) pick, Vector2 position, System.Random rng) =>
            new SpawnSpec
            {
                kind = pick.kind,
                tier = pick.tier,
                ceramic = pick.ceramic,
                position = position,
                direction = rng.NextDouble() < 0.5 ? -1 : 1,
            };

        internal static bool TryPosition(StageMap map, float clearance, float minY, float wickX, System.Random rng, out Vector2 position)
        {
            var t = Tuning.I;
            float minX = -t.HalfWidth + clearance;
            float maxX = t.HalfWidth - clearance;
            float maxY = Mathf.Min(12.5f, t.arenaHeight - clearance);
            for (int attempt = 0; attempt < 30; attempt++)
            {
                // The largest bubble plus its safety margin exactly fills a four-unit level, so explicitly
                // try the two level centres before sampling the rest of the vertical range.
                float y = attempt == 0 ? (minY > 5f ? 12f : 7f) :
                    attempt == 1 ? 12f : Mathf.Lerp(minY, maxY, (float)rng.NextDouble());
                var candidate = new Vector2(
                    Mathf.Lerp(minX, maxX, (float)rng.NextDouble()),
                    y);
                if (Mathf.Abs(candidate.x - wickX) < 5f) continue;

                Vector2 resolved = candidate;
                Contact contact = map.ResolveCircle(ref resolved, clearance);
                if (IsClear(candidate, resolved, contact))
                {
                    position = candidate;
                    return true;
                }
            }

            // The two tries above are the only heights where the largest bubble fits, and each one
            // draws a single x. When that x is on Wick, a later random y never lands on the centre
            // again and the bubble is dropped. Search those rows, then the rest of the open air.
            if (SearchClear(map, clearance, minY, minX, maxX, maxY, wickX, out position))
                return true;
            position = default;
            return false;
        }

        static bool SearchClear(StageMap map, float clearance, float minY, float minX, float maxX, float maxY, float wickX, out Vector2 position)
        {
            if (SearchRow(map, clearance, minY > 5f ? 12f : 7f, minY, minX, maxX, maxY, wickX, out position)) return true;
            if (SearchRow(map, clearance, 12f, minY, minX, maxX, maxY, wickX, out position)) return true;

            int rows = Mathf.Max(1, Mathf.CeilToInt((maxY - minY) / 0.25f));
            for (int i = 0; i <= rows; i++)
            {
                float y = Mathf.Lerp(maxY, minY, i / (float)rows);
                if (SearchRow(map, clearance, y, minY, minX, maxX, maxY, wickX, out position)) return true;
            }
            position = default;
            return false;
        }

        static bool SearchRow(StageMap map, float clearance, float y, float minY, float minX, float maxX, float maxY, float wickX, out Vector2 position)
        {
            position = default;
            if (y < minY || y > maxY) return false;
            int steps = Mathf.Max(1, Mathf.CeilToInt((maxX - minX) / 0.25f));
            for (int i = 0; i <= steps; i++)
            {
                float x = Mathf.Lerp(maxX, minX, i / (float)steps);
                if (Mathf.Abs(x - wickX) < 5f) continue;
                var candidate = new Vector2(x, y);
                Vector2 resolved = candidate;
                Contact contact = map.ResolveCircle(ref resolved, clearance);
                if (!IsClear(candidate, resolved, contact)) continue;
                position = candidate;
                return true;
            }
            return false;
        }

        internal static bool IsClear(Vector2 candidate, Vector2 resolved, Contact contact) =>
            (resolved - candidate).sqrMagnitude <= 0.000001f &&
            !contact.landed && !contact.bumped && contact.side == 0;

        static BubbleKind PickKind(int stage, System.Random rng)
        {
            var t = Tuning.I;
            double roll = rng.NextDouble();
            if (Run.WorldOf(stage) >= t.leadFromWorld && roll < 0.25) return BubbleKind.Lead;
            if (stage >= t.trigloopFromStage && roll > 0.65) return BubbleKind.Trigloop;
            return BubbleKind.Gloop;
        }

        static float Cost(BubbleKind kind, int tier)
        {
            var t = Tuning.I;
            float scale = (int)kind < t.kindCostScale.Length ? t.kindCostScale[(int)kind] : 1f;
            return t.tierCost[Mathf.Min(tier, t.tierCost.Length - 1)] * scale;
        }

        /// <summary>Biggest tier this kind is allowed to be: 0 XL, 1 L.</summary>
        public static int LargestTier(BubbleKind kind) => Tuning.I.Kind(kind).largestTier;

        public static int BiggestAffordable(BubbleKind kind, float budget)
        {
            for (int tier = LargestTier(kind); tier < Tuning.I.tiers.Length; tier++)
                if (Cost(kind, tier) <= budget + 0.001f) return tier;
            return -1;
        }
    }
}
