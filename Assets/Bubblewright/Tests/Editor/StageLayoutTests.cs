using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    public class StageLayoutTests
    {
        static IEnumerable<TestCaseData> LayoutsAndMirrors()
        {
            foreach (var layout in StageLayouts.All)
            {
                yield return new TestCaseData(layout.name, layout.levels, layout.onlyWorld, layout.needsBreaking, layout.rows)
                    .SetName($"{layout.name}_follows_layout_rules");
                yield return new TestCaseData(layout.name + " (mirror)", layout.levels, layout.onlyWorld, layout.needsBreaking, StageLayouts.Mirror(layout.rows))
                    .SetName($"{layout.name}_mirror_follows_layout_rules");
            }
        }

        [Test]
        public void LibraryContainsTwentyFiveNamedLayoutsIncludingFiveCatacombsAndSixWithGaps()
        {
            Assert.AreEqual(25, StageLayouts.All.Length);
            int withGaps = 0;
            foreach (var layout in StageLayouts.All)
                if (layout.HasGaps)
                {
                    withGaps++;
                    Assert.AreEqual(2, layout.minWorld, $"{layout.name}: gap layouts start in world 2");
                    Assert.AreEqual(0, layout.onlyWorld, $"{layout.name}: gap layouts are not tied to one world");
                }
            Assert.AreEqual(6, withGaps);
            var names = new HashSet<string>();
            int catacombs = 0;
            foreach (var layout in StageLayouts.All)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(layout.name));
                Assert.IsTrue(names.Add(layout.name), $"duplicate layout name {layout.name}");
                if (layout.onlyWorld == 5)
                {
                    catacombs++;
                    Assert.IsTrue(Array.Exists(layout.rows, row => row.Contains("B")), $"{layout.name} contains breakables");
                }
            }
            Assert.AreEqual(5, catacombs);
        }

        [Test]
        public void MirrorReversesEveryRowWithoutChangingTheSource()
        {
            var rows = new[] { "....H#", "T#...." };
            var mirrored = StageLayouts.Mirror(rows);

            CollectionAssert.AreEqual(new[] { "#H....", "....#T" }, mirrored);
            CollectionAssert.AreEqual(new[] { "....H#", "T#...." }, rows);
        }

        [TestCaseSource(nameof(LayoutsAndMirrors))]
        public void EveryLayoutParsesAndFollowsEveryRule(string name, int levels, int onlyWorld, bool needsBreaking, string[] rows)
        {
            StageMap map = null;
            Assert.DoesNotThrow(() => map = StageLayout.Parse(rows), $"{name}: parses");

            Assert.IsTrue(rows.Length == StageMap.Rows || rows.Length == StageMap.Rows + 1, $"{name}: has 14 rows plus an optional ground row");
            if (rows.Length > StageMap.Rows)
            {
                Assert.AreEqual(StageMap.Cols, rows[StageMap.Rows].Length, $"{name}: ground row has 32 columns");
                foreach (char ch in rows[StageMap.Rows])
                    Assert.Contains(ch, new[] { '=', '_' }, $"{name}: ground uses only = and _");
            }
            for (int i = 0; i < StageMap.Rows; i++)
            {
                Assert.AreEqual(StageMap.Cols, rows[i].Length, $"{name}: row {13 - i} has 32 columns");
                foreach (char ch in rows[i])
                    Assert.Contains(ch, new[] { '.', '#', 'B', 'H', 'T' }, $"{name}: uses only ., #, B, H and T");
            }

            for (int r = 0; r < StageMap.Rows; r++)
                for (int c = 0; c < StageMap.Cols; c++)
                    if (map.IsSolid(c, r))
                        Assert.IsTrue(r == 4 || r == 9 || onlyWorld == 5 && map.Get(c, r).kind == TileKind.Breakable,
                            $"{name}: solid at ({c}, {r}) follows the platform or Catacombs breakable rule");

            int expectedLevels = HasSolid(map, 9) ? 3 : HasSolid(map, 4) ? 2 : 1;
            Assert.AreEqual(expectedLevels, levels, $"{name}: levels matches rows used");

            for (int c = 0; c < StageMap.Cols; c++)
                for (int r = 0; r < StageMap.Rows; r++)
                    if (map.Get(c, r).ladder)
                        Assert.IsTrue(IsCompleteLadder(map, c, r), $"{name}: ladder at ({c}, {r}) belongs to a complete ladder");

            bool allowBreakableWithoutLadder = onlyWorld == 5 && needsBreaking;
            AssertPlatformsHaveValidRunsAndLadders(name, map, 4, allowBreakableWithoutLadder);
            AssertPlatformsHaveValidRunsAndLadders(name, map, 9, allowBreakableWithoutLadder);

            for (int c = 14; c <= 17; c++)
                for (int r = 0; r <= 3; r++)
                {
                    Assert.IsFalse(map.IsSolid(c, r), $"{name}: Wick start column {c}, row {r} is empty");
                    Assert.IsFalse(map.Get(c, r).ladder, $"{name}: Wick start column {c}, row {r} has no ladder");
                }

            AssertWallGaps(name, map, 4);
            AssertWallGaps(name, map, 9);
            AssertGroundGapRules(name, map);
            if (!needsBreaking) AssertEveryPlatformReachable(name, map, WorldRules.MaxGap(1));
        }

        static IEnumerable<TestCaseData> GapLayoutsInEveryWorld()
        {
            foreach (var layout in StageLayouts.All)
            {
                if (!layout.HasGaps) continue;
                for (int world = layout.minWorld; world <= 6; world++)
                {
                    if (world == 5) continue; // Catacombs stages use only Catacombs layouts
                    yield return new TestCaseData(layout.name, world, layout.rows).SetName($"{layout.name}_is_crossable_in_world_{world}");
                    yield return new TestCaseData(layout.name + " (mirror)", world, StageLayouts.Mirror(layout.rows))
                        .SetName($"{layout.name}_mirror_is_crossable_in_world_{world}");
                }
            }
        }

        [TestCaseSource(nameof(GapLayoutsInEveryWorld))]
        public void GapLayoutsFollowTheRulesAndCanBeCrossedByJumping(string name, int world, string[] rows)
        {
            var map = StageLayout.Parse(rows);
            WorldRules.Apply(map, world, new System.Random(world), 2, false);
            int maxGap = WorldRules.MaxGap(world);
            AssertGroundGapRules(name, map, maxGap);
            AssertEveryPlatformReachable(name, map, maxGap);
            AssertEveryGroundColumnReachable(name, map, maxGap);
            Assert.IsFalse(IsGroundConnectedByWalking(map), $"{name}: walking alone cannot cross every gap");
        }

        [Test]
        public void DesertNarrowsThreeColumnGapsToTwo()
        {
            LayoutDef tower = Array.Find(StageLayouts.All, layout => layout.name == "Gap Tower");
            Assert.NotNull(tower);
            var map = StageLayout.Parse(tower.rows);
            WorldRules.Apply(map, WorldRules.DesertWorld, new System.Random(1), 2, false);
            Assert.IsTrue(map.IsGap(9) && map.IsGap(10) && !map.IsGap(11), "first gap narrowed to columns 9-10");
            Assert.IsTrue(map.IsGap(20) && map.IsGap(21) && !map.IsGap(22), "second gap narrowed to columns 20-21");
        }

        [Test]
        public void GapLayoutsAppearOnAboutOneStageInThreeFromWorldTwoAndNeverOnBosses()
        {
            int stages = 0, withGaps = 0;
            for (int seed = 1; seed <= 300; seed++)
            {
                for (int stage = 1; stage <= 36; stage++)
                {
                    LayoutDef layout = StageGenerator.PickLayout(RunAt(seed, stage), new System.Random(seed * 37 + stage));
                    int world = Run.WorldOf(stage);
                    if (world == 1 || world == 5 || Run.IsBossStageNumber(stage))
                    {
                        Assert.IsFalse(layout.HasGaps, $"seed {seed}, stage {stage}: no gaps here");
                        continue;
                    }
                    stages++;
                    if (layout.HasGaps) withGaps++;
                }
            }
            float share = withGaps / (float)stages;
            Assert.That(share, Is.InRange(0.25f, 0.42f), $"share of stages with gaps was {share:0.00}");
        }

        [Test]
        public void WorldFiveStagesOneToFiveUseOnlyCatacombsLayouts()
        {
            for (int seed = 1; seed <= 100; seed++)
            {
                for (int stage = 25; stage <= 29; stage++)
                    Assert.AreEqual(5, StageGenerator.PickLayout(RunAt(seed, stage), new System.Random(seed)).onlyWorld);

                for (int world = 1; world <= 6; world++)
                {
                    if (world == 5) continue;
                    int first = (world - 1) * 6 + 1;
                    for (int stage = first; stage < first + 5; stage++)
                        Assert.AreNotEqual(5, StageGenerator.PickLayout(RunAt(seed, stage), new System.Random(seed)).onlyWorld);
                }
            }
        }

        [Test]
        public void SealedVaultStartsEveryBubbleInsideTheBreakableBox()
        {
            LayoutDef vault = Array.Find(StageLayouts.All, layout => layout.name == "Sealed Vault");
            Assert.NotNull(vault);

            for (int seed = 1; seed <= 20; seed++)
            {
                StagePlan plan = StageGenerator.Generate(RunAt(seed, 25), vault);
                Assert.Greater(plan.spawns.Count, 0, $"seed {seed}");
                foreach (var spawn in plan.spawns)
                    Assert.Greater(spawn.position.y, 10f, $"seed {seed}: bubble is above the breakable floor");
            }
        }

        [Test]
        public void PickLayoutFollowsStageProgression()
        {
            for (int seed = 1; seed <= 100; seed++)
            {
                Assert.AreEqual("Flat", StageGenerator.PickLayout(RunAt(seed, 1), new System.Random(seed)).name, $"seed {seed}, stage 1");
                Assert.AreEqual("Flat", StageGenerator.PickLayout(RunAt(seed, 2), new System.Random(seed)).name, $"seed {seed}, stage 2");

                for (int stage = 3; stage <= 5; stage++)
                    Assert.AreEqual(2, StageGenerator.PickLayout(RunAt(seed, stage), new System.Random(seed)).levels, $"seed {seed}, stage {stage}");

                for (int world = 2; world <= 6; world++)
                {
                    int first = (world - 1) * 6 + 1;
                    if (world != 5)
                        Assert.AreEqual(2, StageGenerator.PickLayout(RunAt(seed, first), new System.Random(seed)).levels, $"seed {seed}, stage {first}");
                    Assert.AreEqual("Flat", StageGenerator.PickLayout(RunAt(seed, first + 5), new System.Random(seed)).name, $"seed {seed}, boss {first + 5}");
                }
            }
        }

        [Test]
        public void EveryGeneratedPlanHasSafeBubbles()
        {
            for (int seed = 1; seed <= 5; seed++)
            {
                for (int stage = 1; stage <= 36; stage++)
                {
                    var plan = StageGenerator.Generate(RunAt(seed, stage));
                    Assert.NotNull(plan.map, $"seed {seed}, stage {stage}: map");
                    Assert.AreEqual(0f, plan.wickX, 1e-5f, $"seed {seed}, stage {stage}: Wick starts in the middle");
                    if (Run.IsBossStageNumber(stage)) Assert.AreEqual(0, plan.spawns.Count, $"seed {seed}, stage {stage}: the boss is the whole stage");
                    else Assert.GreaterOrEqual(plan.spawns.Count, 1, $"seed {seed}, stage {stage}: at least one bubble");

                    foreach (var spawn in plan.spawns)
                    {
                        // A Clinger hangs from a surface, so it has its own rule (see SignatureTests).
                        if (spawn.kind == BubbleKind.Clinger) continue;
                        float radius = Tuning.I.tiers[spawn.tier].diameter * 0.5f + 0.25f;
                        Vector2 resolved = spawn.position;
                        Contact contact = plan.map.ResolveCircle(ref resolved, radius);
                        Assert.AreEqual(spawn.position.x, resolved.x, 1e-5f, $"seed {seed}, stage {stage}: bubble x is clear");
                        Assert.AreEqual(spawn.position.y, resolved.y, 1e-5f, $"seed {seed}, stage {stage}: bubble y is clear");
                        Assert.IsFalse(contact.landed || contact.bumped || contact.side != 0, $"seed {seed}, stage {stage}: bubble has no contact");
                        Assert.GreaterOrEqual(Mathf.Abs(spawn.position.x - plan.wickX), 5f, $"seed {seed}, stage {stage}: bubble is away from Wick");
                    }
                }
            }
        }

        [Test]
        public void EveryLayoutCanBuildASafeDebugPlan()
        {
            foreach (var layout in StageLayouts.All)
            {
                var plan = StageGenerator.Generate(RunAt(19, 20), layout);
                var expected = StageLayout.Parse(layout.rows);
                for (int r = 0; r < StageMap.Rows; r++)
                    for (int c = 0; c < StageMap.Cols; c++)
                    {
                        Assert.AreEqual(expected.Get(c, r).kind, plan.map.Get(c, r).kind, $"{layout.name}: tile ({c}, {r}) kind");
                        Assert.AreEqual(expected.Get(c, r).ladder, plan.map.Get(c, r).ladder, $"{layout.name}: tile ({c}, {r}) ladder");
                    }
                foreach (var spawn in plan.spawns)
                {
                    float radius = Tuning.I.tiers[spawn.tier].diameter * 0.5f + 0.25f;
                    Vector2 resolved = spawn.position;
                    plan.map.ResolveCircle(ref resolved, radius);
                    Assert.AreEqual(spawn.position, resolved, $"{layout.name}: bubble is clear");
                }
            }
        }

        static Run RunAt(int seed, int stage) => new Run(seed) { Stage = stage };

        static bool HasSolid(StageMap map, int row)
        {
            for (int c = 0; c < StageMap.Cols; c++)
                if (map.IsSolid(c, row)) return true;
            return false;
        }

        static bool IsCompleteLadder(StageMap map, int c, int r)
        {
            if (r <= 4)
            {
                for (int row = 0; row <= 4; row++)
                    if (!map.Get(c, row).ladder) return false;
                return map.IsSolid(c, 4);
            }

            for (int row = 5; row <= 9; row++)
                if (!map.Get(c, row).ladder) return false;
            return map.IsSolid(c, 4) && map.IsSolid(c, 9);
        }

        static void AssertPlatformsHaveValidRunsAndLadders(string name, StageMap map, int row, bool allowBreakableWithoutLadder)
        {
            int c = 0;
            while (c < StageMap.Cols)
            {
                if (!map.IsSolid(c, row))
                {
                    c++;
                    continue;
                }

                int start = c;
                bool hasLadder = false;
                bool allBreakable = true;
                while (c < StageMap.Cols && map.IsSolid(c, row))
                {
                    hasLadder |= map.Get(c, row).ladder;
                    allBreakable &= map.Get(c, row).kind == TileKind.Breakable;
                    c++;
                }

                Assert.GreaterOrEqual(c - start, 4, $"{name}: platform row {row}, columns {start}-{c - 1} is at least 4 tiles");
                Assert.IsTrue(hasLadder || allowBreakableWithoutLadder && allBreakable,
                    $"{name}: platform row {row}, columns {start}-{c - 1} has a ladder or requires breaking");
            }
        }

        static void AssertWallGaps(string name, StageMap map, int row)
        {
            int c = 0;
            while (c < StageMap.Cols)
            {
                if (!map.IsSolid(c, row))
                {
                    c++;
                    continue;
                }

                int start = c;
                while (c < StageMap.Cols && map.IsSolid(c, row)) c++;
                int end = c - 1;
                int leftGap = start;
                int rightGap = StageMap.Cols - 1 - end;
                Assert.IsTrue(leftGap == 0 || leftGap >= 3, $"{name}: platform row {row}, columns {start}-{end} leaves a valid left wall gap");
                Assert.IsTrue(rightGap == 0 || rightGap >= 3, $"{name}: platform row {row}, columns {start}-{end} leaves a valid right wall gap");
            }
        }

        /// <summary>Gaps are 2 or 3 columns (at most maxGap), never under columns 13-18, never against a wall,
        /// never at or next to a ladder's foot, and at least 4 columns of ground lie between two gaps.</summary>
        static void AssertGroundGapRules(string name, StageMap map, int maxGap = 3)
        {
            int previousEnd = -100;
            int c = 0;
            while (c < StageMap.Cols)
            {
                if (!map.IsGap(c)) { c++; continue; }
                int start = c;
                while (c < StageMap.Cols && map.IsGap(c)) c++;
                int end = c - 1, width = c - start;
                Assert.That(width, Is.InRange(2, maxGap), $"{name}: gap {start}-{end} is 2 to {maxGap} columns wide");
                Assert.IsTrue(start > 0 && end < StageMap.Cols - 1, $"{name}: gap {start}-{end} is not against a wall");
                Assert.IsTrue(end < 13 || start > 18, $"{name}: gap {start}-{end} is clear of columns 13-18");
                Assert.GreaterOrEqual(start - previousEnd - 1, 4, $"{name}: at least 4 ground columns before gap {start}-{end}");
                for (int g = start - 1; g <= end + 1; g++)
                    Assert.IsFalse(g >= 0 && g < StageMap.Cols && map.Get(g, 0).ladder, $"{name}: gap {start}-{end} is not at a ladder foot");
                previousEnd = end;
            }
        }

        static bool IsGroundConnectedByWalking(StageMap map)
        {
            for (int c = 0; c < StageMap.Cols; c++)
                if (map.IsGap(c)) return false;
            return true;
        }

        static void AssertEveryGroundColumnReachable(string name, StageMap map, int maxJumpGap)
        {
            bool[,] reached = Reach(map, maxJumpGap);
            for (int c = 0; c < StageMap.Cols; c++)
                if (!map.IsGap(c)) Assert.IsTrue(reached[c, 0], $"{name}: ground column {c} is reachable");
        }

        static void AssertEveryPlatformReachable(string name, StageMap map, int maxJumpGap = 0)
        {
            bool[,] reached = Reach(map, maxJumpGap);
            for (int c = 0; c < StageMap.Cols; c++)
            {
                if (map.IsSolid(c, 4)) Assert.IsTrue(reached[c, 1], $"{name}: row A platform column {c} is reachable");
                if (map.IsSolid(c, 9)) Assert.IsTrue(reached[c, 2], $"{name}: row B platform column {c} is reachable");
            }
        }

        /// <summary>Walk, climb, fall off ends, and on the ground jump over up to maxJumpGap gap columns.</summary>
        static bool[,] Reach(StageMap map, int maxJumpGap)
        {
            var reached = new bool[StageMap.Cols, 3];
            var queue = new Queue<(int c, int level)>();
            reached[15, 0] = true;
            queue.Enqueue((15, 0));

            while (queue.Count > 0)
            {
                var (c, level) = queue.Dequeue();
                TryVisit(c - 1, level);
                TryVisit(c + 1, level);

                if (level == 0 && IsLadder(map, c, 0, 4)) TryVisit(c, 1);
                if (level == 1 && IsLadder(map, c, 0, 4)) TryVisit(c, 0);
                if (level == 1 && IsLadder(map, c, 5, 9)) TryVisit(c, 2);
                if (level == 2 && IsLadder(map, c, 5, 9)) TryVisit(c, 1);

                // Walking off an end falls to the highest surface below that column.
                if (level > 0 && !HasSurface(map, c - 1, level)) TryVisit(c - 1, HighestSurfaceBelow(map, c - 1, level));
                if (level > 0 && !HasSurface(map, c + 1, level)) TryVisit(c + 1, HighestSurfaceBelow(map, c + 1, level));

                // A jump on the ground clears a run of up to maxJumpGap gap columns.
                if (level == 0)
                {
                    for (int dir = -1; dir <= 1; dir += 2)
                    {
                        int n = c + dir, crossed = 0;
                        while (n >= 0 && n < StageMap.Cols && map.IsGap(n)) { n += dir; crossed++; }
                        if (crossed > 0 && crossed <= maxJumpGap) TryVisit(n, 0);
                    }
                }
            }

            return reached;

            void TryVisit(int column, int level)
            {
                if (column < 0 || column >= StageMap.Cols || level < 0 || level > 2 || reached[column, level] || !HasSurface(map, column, level)) return;
                reached[column, level] = true;
                queue.Enqueue((column, level));
            }
        }

        static bool HasSurface(StageMap map, int c, int level) =>
            c >= 0 && c < StageMap.Cols && (level == 0 ? !map.IsGap(c) : map.IsSolid(c, level == 1 ? 4 : 9));

        static int HighestSurfaceBelow(StageMap map, int c, int level)
        {
            if (level > 2 && map.IsSolid(c, 9)) return 2;
            if (level > 1 && map.IsSolid(c, 4)) return 1;
            return 0;
        }

        static bool IsLadder(StageMap map, int c, int from, int to)
        {
            for (int r = from; r <= to; r++)
                if (!map.Get(c, r).ladder) return false;
            return true;
        }
    }
}
