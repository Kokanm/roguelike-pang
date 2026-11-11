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
                yield return new TestCaseData(layout.name, layout.levels, layout.rows)
                    .SetName($"{layout.name}_follows_layout_rules");
                yield return new TestCaseData(layout.name + " (mirror)", layout.levels, StageLayouts.Mirror(layout.rows))
                    .SetName($"{layout.name}_mirror_follows_layout_rules");
            }
        }

        [Test]
        public void LibraryContainsFourteenNamedLayouts()
        {
            Assert.AreEqual(14, StageLayouts.All.Length);
            var names = new HashSet<string>();
            foreach (var layout in StageLayouts.All)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(layout.name));
                Assert.IsTrue(names.Add(layout.name), $"duplicate layout name {layout.name}");
            }
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
        public void EveryLayoutParsesAndFollowsEveryRule(string name, int levels, string[] rows)
        {
            StageMap map = null;
            Assert.DoesNotThrow(() => map = StageLayout.Parse(rows), $"{name}: parses");

            Assert.AreEqual(StageMap.Rows, rows.Length, $"{name}: has 14 rows");
            for (int i = 0; i < rows.Length; i++)
            {
                Assert.AreEqual(StageMap.Cols, rows[i].Length, $"{name}: row {13 - i} has 32 columns");
                foreach (char ch in rows[i])
                    Assert.Contains(ch, new[] { '.', '#', 'H', 'T' }, $"{name}: uses only ., #, H and T");
            }

            for (int r = 0; r < StageMap.Rows; r++)
                for (int c = 0; c < StageMap.Cols; c++)
                    if (map.IsSolid(c, r))
                        Assert.Contains(r, new[] { 4, 9 }, $"{name}: solid at ({c}, {r}) is on a platform row");

            int expectedLevels = HasSolid(map, 9) ? 3 : HasSolid(map, 4) ? 2 : 1;
            Assert.AreEqual(expectedLevels, levels, $"{name}: levels matches rows used");

            for (int c = 0; c < StageMap.Cols; c++)
                for (int r = 0; r < StageMap.Rows; r++)
                    if (map.Get(c, r).ladder)
                        Assert.IsTrue(IsCompleteLadder(map, c, r), $"{name}: ladder at ({c}, {r}) belongs to a complete ladder");

            AssertPlatformsHaveValidRunsAndLadders(name, map, 4);
            AssertPlatformsHaveValidRunsAndLadders(name, map, 9);

            for (int c = 14; c <= 17; c++)
                for (int r = 0; r <= 3; r++)
                {
                    Assert.IsFalse(map.IsSolid(c, r), $"{name}: Wick start column {c}, row {r} is empty");
                    Assert.IsFalse(map.Get(c, r).ladder, $"{name}: Wick start column {c}, row {r} has no ladder");
                }

            AssertWallGaps(name, map, 4);
            AssertWallGaps(name, map, 9);
            AssertEveryPlatformReachable(name, map);
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
                    Assert.GreaterOrEqual(plan.spawns.Count, 1, $"seed {seed}, stage {stage}: at least one bubble");

                    foreach (var spawn in plan.spawns)
                    {
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

        static void AssertPlatformsHaveValidRunsAndLadders(string name, StageMap map, int row)
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
                while (c < StageMap.Cols && map.IsSolid(c, row))
                {
                    hasLadder |= map.Get(c, row).ladder;
                    c++;
                }

                Assert.GreaterOrEqual(c - start, 4, $"{name}: platform row {row}, columns {start}-{c - 1} is at least 4 tiles");
                Assert.IsTrue(hasLadder, $"{name}: platform row {row}, columns {start}-{c - 1} has a ladder");
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

        static void AssertEveryPlatformReachable(string name, StageMap map)
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
            }

            for (int c = 0; c < StageMap.Cols; c++)
            {
                if (map.IsSolid(c, 4)) Assert.IsTrue(reached[c, 1], $"{name}: row A platform column {c} is reachable");
                if (map.IsSolid(c, 9)) Assert.IsTrue(reached[c, 2], $"{name}: row B platform column {c} is reachable");
            }

            void TryVisit(int column, int level)
            {
                if (column < 0 || column >= StageMap.Cols || level < 0 || level > 2 || reached[column, level] || !HasSurface(map, column, level)) return;
                reached[column, level] = true;
                queue.Enqueue((column, level));
            }
        }

        static bool HasSurface(StageMap map, int c, int level) =>
            c >= 0 && c < StageMap.Cols && (level == 0 || map.IsSolid(c, level == 1 ? 4 : 9));

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
