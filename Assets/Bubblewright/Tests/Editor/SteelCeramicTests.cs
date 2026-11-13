using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>Steel rolling balls (indestructible, not needed to clear a stage) and ceramic bubbles and rolling balls (several hits).</summary>
    public class SteelCeramicTests
    {
        const float Dt = 1f / 60f;

        GameObject root;
        StageMap previousMap;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("SteelCeramicTests");
            previousMap = Game.Map;
            SetMap(StageMap.Flat());
        }

        [TearDown]
        public void TearDown()
        {
            SetMap(previousMap);
            Object.DestroyImmediate(root);
        }

        static void SetMap(StageMap map)
        {
            var property = typeof(Game).GetProperty("Map", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(property, "Game.Map must exist.");
            property.SetValue(null, map);
        }

        [Test]
        public void ASteelRollerAbsorbsEveryHitAndNeverBreaks()
        {
            var steel = Roller.Create(root.transform, 0, 0f, 0f, 1, steel: true);
            Assert.IsTrue(steel.Steel);
            for (int i = 0; i < 20; i++) Assert.IsTrue(steel.AbsorbHit(), $"hit {i + 1}");
            Assert.IsFalse(steel.Popped);
        }

        [Test]
        public void SteelRollersDoNotCountForClearingAStage()
        {
            Assert.IsTrue(Game.StageCleared(0, 0), "a stage with only steel balls left is cleared (the game counts only the others)");
        }

        [Test]
        public void ASteelRollerRollsLikeAnyOther()
        {
            var steel = Roller.Create(root.transform, 0, 0f, 0f, 1, steel: true);
            var all = new List<Roller> { steel };
            float x = steel.Position.x;
            for (int i = 0; i < 30; i++) steel.Tick(Dt, all);
            Assert.Greater(steel.Position.x, x);
            Assert.AreEqual(steel.Radius, steel.Position.y, 1e-4f);
        }

        [Test]
        public void ACeramicRollerBreaksOnTheSecondHit()
        {
            Assert.GreaterOrEqual(Tuning.I.ceramicHits, 2);
            var ball = Roller.Create(root.transform, 0, 0f, 0f, 1, ceramic: true);
            Assert.IsTrue(ball.Ceramic);
            for (int i = 1; i < Tuning.I.ceramicHits; i++) Assert.IsTrue(ball.AbsorbHit(), $"hit {i} is absorbed");
            Assert.IsFalse(ball.AbsorbHit(), "the last hit breaks it");
        }

        [Test]
        public void ACeramicBubbleTakesCeramicHitsHitsAndOrdinaryBubblesOne()
        {
            var plain = Bubble.Create(root.transform, BubbleKind.Gloop, 0, new Vector2(0f, 8f), Vector2.zero, false);
            var ceramic = Bubble.Create(root.transform, BubbleKind.Gloop, 0, new Vector2(4f, 8f), Vector2.zero, false, true);
            Assert.IsFalse(plain.AbsorbHit(), "an ordinary bubble breaks at once");
            Assert.IsTrue(ceramic.Ceramic);
            for (int i = 1; i < Tuning.I.ceramicHits; i++) Assert.IsTrue(ceramic.AbsorbHit());
            Assert.IsFalse(ceramic.AbsorbHit());
        }

        [Test]
        public void CeramicAndSteelLooksAreDistinctFromThePlainOnes()
        {
            var art = PlaceholderArt.I;
            Color32 Centre(Sprite s) => s.texture.GetPixel(s.texture.width / 2, s.texture.height / 2);
            Assert.AreNotSame(art.Bubbles[0, 0], art.CeramicBubble(BubbleKind.Gloop, 0, false));
            Assert.AreNotSame(art.CeramicBubble(BubbleKind.Gloop, 0, false), art.CeramicBubble(BubbleKind.Gloop, 0, true));
            Assert.AreNotSame(art.RollerFrame(0, 0, 0), art.RollerFrame(0, 0, 1));
            Assert.AreNotSame(art.RollerFrame(0, 0, 1), art.RollerFrame(0, 0, 2));
            Assert.AreNotSame(art.RollerFrame(0, 0, 0), art.RollerFrame(0, 0, 3));
            Assert.AreEqual(18, art.RollerFrame(0, 0, 3).texture.width);
            Assert.AreNotEqual((Color)Palette.Pearl, (Color)Centre(art.RollerFrame(0, 1, 3)), "steel is not pearl");
        }

        [Test]
        public void GeneratorUsesSteelAndCeramicOnlyFromTheirWorlds()
        {
            var t = Tuning.I;
            bool steel = false, ceramicBubble = false, ceramicRoller = false;
            for (int seed = 1; seed <= 40; seed++)
                for (int stage = 1; stage <= t.RunLength; stage++)
                {
                    var plan = StageGenerator.Generate(stage, new System.Random(seed * 17 + stage));
                    int world = Run.WorldOf(stage);
                    foreach (var r in plan.rollers)
                    {
                        if (r.steel) { steel = true; Assert.GreaterOrEqual(world, t.steelFromWorld); }
                        if (r.ceramic) { ceramicRoller = true; Assert.GreaterOrEqual(world, t.ceramicFromWorld); Assert.IsFalse(r.steel); }
                    }
                    foreach (var s in plan.spawns)
                        if (s.ceramic)
                        {
                            ceramicBubble = true;
                            Assert.GreaterOrEqual(world, t.ceramicFromWorld, $"stage {stage}");
                            Assert.LessOrEqual(s.tier, t.ceramicMaxTier);
                            Assert.AreNotEqual(BubbleKind.Lucky, s.kind);
                        }
                    // the budget still holds with the ceramic surcharge
                    var report = StageReport.From(stage, plan, false);
                    if (!report.boss) Assert.LessOrEqual(report.bubbleSpend + report.rollerSpend, report.budget + 0.01f, $"seed {seed} stage {stage}");
                }
            Assert.IsTrue(steel, "steel rollers appear");
            Assert.IsTrue(ceramicBubble, "ceramic bubbles appear");
            Assert.IsTrue(ceramicRoller, "ceramic rollers appear");
        }
    }
}
