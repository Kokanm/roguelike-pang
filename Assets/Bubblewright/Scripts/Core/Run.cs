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

        public Run(int seed)
        {
            Seed = seed;
            MaxHearts = Hearts = Tuning.I.hearts;
        }

        public bool IsLastStage => Stage >= Tuning.I.RunLength;

        /// <summary>World 1 to 6.</summary>
        public int World => WorldOf(Stage);
        /// <summary>Stage inside its world, 1 to 6.</summary>
        public int StageInWorld => StageInWorldOf(Stage);
        /// <summary>The last stage of a world is its boss stage. Bosses come later; for now it is a bigger normal stage.</summary>
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
        public System.Random StageRng(int salt = 0) => new System.Random(unchecked(Seed * 7919 + Stage * 104729 + salt * 31));

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
                character = "wick",
                hardWorld = (bool[])hardWorld.Clone(),
                worldDebuff = (string[])worldDebuff.Clone(),
            };
        }

        public static Run FromSave(SaveData d)
        {
            if (d == null || d.version != 1) return null;
            var run = new Run(d.seed);
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
        public float wickX;
    }

    public static class StageGenerator
    {
        /// <summary>Bubble budget of a stage (1 to 36) from the world and the stage inside it.</summary>
        public static float Budget(int stage, bool hard = false)
        {
            var t = Tuning.I;
            float budget = t.budgetBase + t.budgetPerWorld * (Run.WorldOf(stage) - 1) + t.budgetPerStage * (Run.StageInWorldOf(stage) - 1);
            if (Run.IsBossStageNumber(stage)) budget *= t.bossPlaceholderBudgetScale;
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

            var choices = new List<LayoutDef>();
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

        public static StagePlan Generate(Run run) => Generate(run.Stage, run.StageRng(0), run.IsHard, null);

        public static StagePlan Generate(Run run, LayoutDef layout) =>
            Generate(run.Stage, run.StageRng(0), run.IsHard, layout);

        public static StagePlan Generate(int stage, System.Random rng, bool hard = false) =>
            Generate(stage, rng, hard, null);

        static StagePlan Generate(int stage, System.Random rng, bool hard, LayoutDef layoutOverride)
        {
            var t = Tuning.I;
            LayoutDef layout = layoutOverride ?? PickLayout(stage, rng);
            StageMap map = StageLayout.Parse(layout.rows);
            WorldRules.Apply(map, Run.WorldOf(stage), rng, Run.StageInWorldOf(stage), Run.IsBossStageNumber(stage));
            float bubbleMinY = layout.bubbleMinY > 0f ? layout.bubbleMinY : 5f;
            float budget = Budget(stage, hard);
            int maxBubbles = MaxStartBubbles(stage);
            var picks = new List<(BubbleKind kind, int tier)>();

            while (picks.Count < maxBubbles)
            {
                var kind = PickKind(stage, rng);
                int tier = BiggestAffordable(kind, budget);
                if (tier < 0 && kind != BubbleKind.Gloop)
                {
                    kind = BubbleKind.Gloop;
                    tier = BiggestAffordable(kind, budget);
                }
                if (tier < 0) break;
                // After the first bubble, sometimes go one size down so stages are not all XLs.
                if (picks.Count > 0 && tier < t.tiers.Length - 1 && rng.NextDouble() < 0.35) tier++;
                budget -= Cost(kind, tier);
                picks.Add((kind, tier));
            }
            if (stage > 1 && rng.NextDouble() < t.luckyChance) picks.Add((BubbleKind.Lucky, 2));

            // Biggest first. Flat stages assign the furthest random positions to the biggest bubbles.
            picks.Sort((a, b) => a.tier.CompareTo(b.tier));
            var specs = new List<SpawnSpec>();
            if (layout.levels == 1)
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
            return new StagePlan { map = map, spawns = specs, wickX = 0f };
        }

        static SpawnSpec Spec((BubbleKind kind, int tier) pick, Vector2 position, System.Random rng) =>
            new SpawnSpec
            {
                kind = pick.kind,
                tier = pick.tier,
                position = position,
                direction = rng.NextDouble() < 0.5 ? -1 : 1,
            };

        static bool TryPosition(StageMap map, float clearance, float minY, float wickX, System.Random rng, out Vector2 position)
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
                if ((resolved - candidate).sqrMagnitude <= 0.000001f &&
                    !contact.landed && !contact.bumped && contact.side == 0)
                {
                    position = candidate;
                    return true;
                }
            }
            position = default;
            return false;
        }

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

        static int BiggestAffordable(BubbleKind kind, float budget)
        {
            for (int tier = 0; tier < Tuning.I.tiers.Length; tier++)
                if (Cost(kind, tier) <= budget + 0.001f) return tier;
            return -1;
        }
    }
}
