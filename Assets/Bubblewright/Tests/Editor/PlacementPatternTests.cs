using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>Placement patterns put a stage's bubbles down in a shape, and always keep the usual start rules.</summary>
    public class PlacementPatternTests
    {
        static List<(BubbleKind kind, int tier, bool ceramic)> Picks(params int[] tiers)
        {
            var list = new List<(BubbleKind kind, int tier, bool ceramic)>();
            foreach (int tier in tiers) list.Add((BubbleKind.Gloop, tier, false));
            return list;
        }

        static float Clearance(int tier) => Tuning.I.tiers[tier].diameter * 0.5f + 0.25f;

        [Test]
        public void EveryStartIsClearAndAwayFromWickWhateverThePattern()
        {
            var seen = new HashSet<string>();
            for (int seed = 1; seed <= 200; seed++)
            {
                var run = new Run(seed);
                for (int stage = 1; stage <= 36; stage++)
                {
                    if (Run.IsBossStageNumber(stage)) continue;
                    StagePlan plan = StageGenerator.GenerateFor(run, stage);
                    Assert.NotNull(plan.patternName, $"seed {seed} stage {stage}");
                    seen.Add(plan.patternName);
                    foreach (var s in plan.spawns)
                    {
                        // A Clinger hangs from a surface, so it has its own rule (see SignatureTests).
                        if (s.kind == BubbleKind.Clinger) continue;
                        Vector2 resolved = s.position;
                        plan.map.ResolveCircle(ref resolved, Clearance(s.tier));
                        Assert.AreEqual(0f, (resolved - s.position).magnitude, 0.001f,
                            $"seed {seed} stage {stage} {plan.patternName} starts inside a tile at {s.position}");
                        Assert.GreaterOrEqual(Mathf.Abs(s.position.x), 5f - 0.001f,
                            $"seed {seed} stage {stage} {plan.patternName} starts near Wick at {s.position}");
                    }
                    if (plan.patternName == "Top row" || plan.patternName == "Mirrored pair")
                        for (int i = 0; i < plan.spawns.Count; i++)
                            for (int j = i + 1; j < plan.spawns.Count; j++)
                            {
                                float gap = (Tuning.I.tiers[plan.spawns[i].tier].diameter + Tuning.I.tiers[plan.spawns[j].tier].diameter) * 0.5f;
                                Assert.GreaterOrEqual((plan.spawns[i].position - plan.spawns[j].position).magnitude, gap - 0.001f,
                                    $"seed {seed} stage {stage} {plan.patternName} overlap");
                            }
                }
            }
            foreach (string name in new[] { "Scattered", "Mirrored pair", "Top row", "One per floor" })
                Assert.IsTrue(seen.Contains(name), $"no stage used {name}");
        }

        [Test]
        public void AMirroredPairMovesApartAtTheSameHeight()
        {
            var spawns = PlacementPatterns.MirroredPair(StageMap.Flat(), Picks(1, 1, 2, 2), 5f, new System.Random(3));
            Assert.AreEqual(4, spawns.Count);
            for (int i = 0; i < 4; i += 2)
            {
                var a = spawns[i];
                var b = spawns[i + 1];
                Assert.AreEqual(a.position.y, b.position.y, 0.0001f, "pair height");
                Assert.AreEqual(-a.position.x, b.position.x, 0.0001f, "pair mirrored");
                Assert.GreaterOrEqual(Mathf.Abs(a.position.x), 5f);
                var left = a.position.x < 0 ? a : b;
                var right = a.position.x < 0 ? b : a;
                Assert.AreEqual(-1, left.direction);
                Assert.AreEqual(1, right.direction);
            }
        }

        [Test]
        public void ATopRowSharesOneHeightAndSpreadsAcrossTheWidth()
        {
            var spawns = PlacementPatterns.TopRow(StageMap.Flat(), Picks(1, 1, 2, 2), 5f, new System.Random(8));
            Assert.AreEqual(4, spawns.Count);
            float minX = float.MaxValue, maxX = float.MinValue;
            foreach (var s in spawns)
            {
                Assert.AreEqual(spawns[0].position.y, s.position.y, 0.0001f);
                Assert.GreaterOrEqual(Mathf.Abs(s.position.x), 5f - 0.001f);
                minX = Mathf.Min(minX, s.position.x);
                maxX = Mathf.Max(maxX, s.position.x);
            }
            Assert.Less(minX, -8f);
            Assert.Greater(maxX, 8f);
            for (int i = 1; i < spawns.Count; i++)
                Assert.AreNotEqual(spawns[i - 1].direction, spawns[i].direction, "directions alternate");
        }

        [Test]
        public void OnePerFloorUsesAHighAMiddleAndALowBand()
        {
            var spawns = PlacementPatterns.OnePerFloor(StageMap.Flat(), Picks(2, 2, 2), 5f, new System.Random(11));
            Assert.AreEqual(3, spawns.Count);
            Assert.GreaterOrEqual(spawns[0].position.y, 11f);
            Assert.LessOrEqual(spawns[0].position.y, 12.5f);
            Assert.GreaterOrEqual(spawns[1].position.y, 6f);
            Assert.LessOrEqual(spawns[1].position.y, 8.5f);
            Assert.GreaterOrEqual(spawns[2].position.y, 1.5f);
            Assert.LessOrEqual(spawns[2].position.y, 3.5f);
            Assert.GreaterOrEqual(Mathf.Abs(spawns[2].position.x), 7f - 0.001f, "low band stays 7 from Wick");
        }

        [Test]
        public void AnXlBubbleIsNeverPutInTheLowBand()
        {
            for (int seed = 1; seed <= 50; seed++)
            {
                var spawns = PlacementPatterns.OnePerFloor(StageMap.Flat(), Picks(0, 0, 0), 5f, new System.Random(seed));
                foreach (var s in spawns) Assert.GreaterOrEqual(s.position.y, 5f, $"seed {seed}"); // a fallback spot may sit lower than the middle band, never in the low band
            }
        }

        [Test]
        public void ThePatternOfAStageIsTheSameWhenGeneratedTwice()
        {
            for (int seed = 1; seed <= 200; seed++)
            {
                var run = new Run(seed);
                for (int stage = 1; stage <= 36; stage++)
                {
                    if (Run.IsBossStageNumber(stage)) continue;
                    var a = StageGenerator.GenerateFor(run, stage);
                    var b = StageGenerator.GenerateFor(run, stage);
                    Assert.AreEqual(a.patternName, b.patternName, $"seed {seed} stage {stage}");
                    Assert.AreEqual(a.spawns.Count, b.spawns.Count);
                    for (int i = 0; i < a.spawns.Count; i++)
                    {
                        Assert.AreEqual(a.spawns[i].position, b.spawns[i].position, $"seed {seed} stage {stage}");
                        Assert.AreEqual(a.spawns[i].direction, b.spawns[i].direction, $"seed {seed} stage {stage}");
                    }
                }
            }
        }

        [Test]
        public void WithoutARecipeThePlacementIsAsBefore()
        {
            for (int stage = 1; stage <= 36; stage++)
            {
                var a = StageGenerator.Generate(stage, new System.Random(5), false);
                var b = StageGenerator.Generate(stage, new System.Random(5), false);
                Assert.IsNull(a.patternName, $"stage {stage}");
                Assert.AreEqual(a.spawns.Count, b.spawns.Count);
                for (int i = 0; i < a.spawns.Count; i++)
                    Assert.AreEqual(a.spawns[i].position, b.spawns[i].position, $"stage {stage}");
            }
        }

        [Test]
        public void TheReportCarriesThePattern()
        {
            var plan = StageGenerator.GenerateFor(new Run(1), 1);
            var report = StageReport.From(1, plan, false);
            Assert.AreEqual("Scattered", report.pattern);
            Assert.IsTrue(report.Line().Contains("\tMixed\tScattered\t"), report.Line());
        }
    }
}
