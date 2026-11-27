using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Picks the five stages of a world from StageSpec.All and builds the plan for one stage of a run.
    /// Boss stages stay the flat arena. A saved run keeps the ids it already rolled.
    /// </summary>
    public static class StagePicker
    {
        public const int Slots = 5;
        static readonly int[] CalmTargets = { 1, 2, 2, 3, 3 };
        static readonly int[] HardTargets = { 1, 3, 3, 4, 4 };

        public static float StageTime(int stage, bool hard = false)
        {
            var t = Tuning.I;
            float time = t.timeBase + t.timePerWorld * (Run.WorldOf(stage) - 1) + t.timePerStage * (Run.StageInWorldOf(stage) - 1);
            if (Run.WorldOf(stage) == 3) time *= t.desertTimeScale;
            return hard ? time * t.hardTimeScale : time;
        }

        /// <summary>The storm clock of a world's boss fight: its BossDef's clock, scaled on the hard road.</summary>
        public static float BossTime(int world, bool hard = false) => BossDef.ForWorld(world).ClockFor(hard);

        public static float BubbleCost(BubbleKind kind, int tier)
        {
            var t = Tuning.I;
            float scale = (int)kind < t.kindCostScale.Length ? t.kindCostScale[(int)kind] : 1f;
            return t.tierCost[Mathf.Min(tier, t.tierCost.Length - 1)] * scale;
        }

        /// <summary>Five stages for one world, slot 1 first. The same seed, world and road always return the same ids.</summary>
        public static StageSpec[] PickForWorld(int seed, int world, bool hard)
        {
            var targets = hard ? HardTargets : CalmTargets;
            var pool = new List<StageSpec>();
            foreach (var stage in StageSpec.All)
                if (stage.World == world) pool.Add(stage);
            Shuffle(pool, new System.Random(unchecked(seed * 7919 + world * 104729 + (hard ? 17 : 0))));

            bool reuse = pool.Count < Slots;
            var used = new HashSet<string>();
            var picked = new StageSpec[Slots];
            for (int i = 0; i < Slots; i++)
            {
                int slot = i + 1;
                var choice = Choose(pool, used, slot, targets[i], hard, unusedOnly: true, respectBounds: true)
                    ?? Choose(pool, used, slot, targets[i], hard, unusedOnly: true, respectBounds: false);
                if (choice == null && reuse)
                    choice = Choose(pool, used, slot, targets[i], hard, unusedOnly: false, respectBounds: true)
                        ?? Choose(pool, used, slot, targets[i], hard, unusedOnly: false, respectBounds: false);
                picked[i] = choice;
                if (choice != null) used.Add(choice.Id);
            }
            OrderLaterSlots(picked);
            return picked;
        }

        /// <summary>
        /// From stage 2, with Tuning.luckyChance, one plain starting Gloop, Trigloop or Lead becomes a Lucky
        /// of the same tier, position and direction. Pogos, ceramic bubbles and every other kind stay as they are.
        /// </summary>
        public static void ApplyLucky(StagePlan plan, Run run, int stage)
        {
            if (plan == null || plan.spawns == null || run == null || stage < 2) return;
            var rng = run.StageRngFor(stage, 2);
            if (rng.NextDouble() >= Tuning.I.luckyChance) return;
            var eligible = new List<int>();
            for (int i = 0; i < plan.spawns.Count; i++)
            {
                var spawn = plan.spawns[i];
                if (!IsPlain(spawn)) continue;
                eligible.Add(i);
            }
            if (eligible.Count == 0) return;
            int index = eligible[rng.Next(eligible.Count)];
            var chosen = plan.spawns[index];
            chosen.kind = BubbleKind.Lucky;
            plan.spawns[index] = chosen;
        }

        public static StagePlan Build(Run run, int stage, int debugOverrideIndex = -1)
        {
            if (debugOverrideIndex >= 0)
            {
                var all = StageSpec.All;
                if (all.Count == 0) return Arena("Debug");
                int index = Mathf.Clamp(debugOverrideIndex, 0, all.Count - 1);
                return all[index].Build(false);
            }
            if (run == null || Run.IsBossStageNumber(stage)) return Arena("Boss");

            int world = Run.WorldOf(stage);
            int slot = Run.StageInWorldOf(stage) - 1;
            bool hard = run.IsHardWorld(world);
            Ensure(run, world, hard);
            if (run.stageIds == null || slot < 0 || slot >= run.stageIds.Length) return Arena("Boss");

            var spec = StageSpec.ById(run.stageIds[slot]);
            if (spec == null || spec.World != world)
            {
                spec = PickForWorld(run.Seed, world, hard)[slot];
                if (spec == null) return Arena("Boss");
                run.stageIds[slot] = spec.Id;
                run.stageMirrors[slot] = spec.CanMirror && run.StageRngFor(stage, 1).NextDouble() < 0.5;
            }
            bool mirror = spec.CanMirror && run.stageMirrors != null && slot < run.stageMirrors.Length && run.stageMirrors[slot];
            var plan = spec.Build(mirror);
            ApplyLucky(plan, run, stage);
            return plan;
        }

        static void Ensure(Run run, int world, bool hard)
        {
            if (run.HasStagePick(world, hard)) return;
            var picked = PickForWorld(run.Seed, world, hard);
            var ids = new string[Slots];
            var mirrors = new bool[Slots];
            bool saved = run.SavedStagesMatch(world, hard);
            for (int i = 0; i < Slots; i++)
            {
                int stageNumber = (world - 1) * Tuning.I.stagesPerWorld + i + 1;
                var spec = picked[i];
                bool mirror = false;
                if (saved)
                {
                    var known = StageSpec.ById(run.SavedStageId(i));
                    if (known != null && known.World == world)
                    {
                        spec = known;
                        mirror = known.CanMirror && run.SavedStageMirror(i);
                    }
                    else if (spec != null)
                        mirror = spec.CanMirror && run.StageRngFor(stageNumber, 1).NextDouble() < 0.5;
                }
                else if (spec != null)
                    mirror = spec.CanMirror && run.StageRngFor(stageNumber, 1).NextDouble() < 0.5;
                ids[i] = spec != null ? spec.Id : null;
                mirrors[i] = mirror;
            }
            run.StoreStagePick(world, hard, ids, mirrors);
        }

        static StageSpec Choose(List<StageSpec> pool, HashSet<string> used, int slot, int target, bool hard, bool unusedOnly, bool respectBounds)
        {
            StageSpec best = null;
            int bestKey = int.MaxValue;
            int bestIndex = int.MaxValue;
            for (int i = 0; i < pool.Count; i++)
            {
                var stage = pool[i];
                if (unusedOnly && used.Contains(stage.Id)) continue;
                if (respectBounds && (slot < stage.MinStageInWorld || slot > stage.MaxStageInWorld)) continue;
                int distance = Mathf.Abs(stage.Difficulty - target);
                int tie = hard
                    ? (stage.Difficulty >= target ? 0 : 1) * 10 + (4 - stage.Difficulty)
                    : (stage.Difficulty <= target ? 0 : 1) * 10 + stage.Difficulty;
                int key = distance * 100 + tie;
                if (key < bestKey || (key == bestKey && i < bestIndex))
                {
                    best = stage;
                    bestKey = key;
                    bestIndex = i;
                }
            }
            return best;
        }

        static void OrderLaterSlots(StageSpec[] picked)
        {
            bool moved = true;
            while (moved)
            {
                moved = false;
                for (int i = 1; i < picked.Length - 1; i++)
                {
                    var left = picked[i];
                    var right = picked[i + 1];
                    if (left == null || right == null || left.Difficulty <= right.Difficulty) continue;
                    int slotLeft = i + 1;
                    int slotRight = i + 2;
                    if (Fits(right, slotLeft) && Fits(left, slotRight))
                    {
                        picked[i] = right;
                        picked[i + 1] = left;
                        moved = true;
                    }
                }
            }
        }

        static bool Fits(StageSpec stage, int slot) => stage.MinStageInWorld <= slot && slot <= stage.MaxStageInWorld;

        static void Shuffle(List<StageSpec> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var swap = list[i];
                list[i] = list[j];
                list[j] = swap;
            }
        }

        static bool IsPlain(SpawnSpec spawn) =>
            !spawn.pogo && !spawn.ceramic && !spawn.steel &&
            (spawn.kind == BubbleKind.Gloop || spawn.kind == BubbleKind.Trigloop || spawn.kind == BubbleKind.Lead);

        static StagePlan Arena(string name) => new StagePlan
        {
            map = StageMap.Flat(),
            spawns = new List<SpawnSpec>(),
            rollers = new List<RollerSpec>(),
            steelPogos = new List<SpawnSpec>(),
            waves = new List<List<SpawnSpec>>(),
            layoutName = name,
            timeScale = 1f,
        };
    }
}
