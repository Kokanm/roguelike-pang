using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>Worlds 2 to 6 each have a signature enemy: stage 1 introduces it, later stages use it a lot.</summary>
    public class SignatureTests
    {
        static readonly BubbleKind[] NewKinds =
            { BubbleKind.Clinger, BubbleKind.Sinker, BubbleKind.Frost, BubbleKind.Ghost, BubbleKind.Scorch, BubbleKind.Drifter };

        [Test]
        public void SignatureKindsFollowTheWorlds()
        {
            Assert.IsNull(StageGenerator.SignatureKind(0));
            Assert.IsNull(StageGenerator.SignatureKind(1));
            Assert.AreEqual(BubbleKind.Clinger, StageGenerator.SignatureKind(2));
            Assert.AreEqual(BubbleKind.Sinker, StageGenerator.SignatureKind(3));
            Assert.AreEqual(BubbleKind.Frost, StageGenerator.SignatureKind(4));
            Assert.AreEqual(BubbleKind.Ghost, StageGenerator.SignatureKind(5));
            Assert.AreEqual(BubbleKind.Scorch, StageGenerator.SignatureKind(6));
            Assert.IsNull(StageGenerator.SignatureKind(7));
        }

        [Test]
        public void SignaturesHoldForEverySeedAndStage()
        {
            var t = Tuning.I;
            int homeSig = 0, homeAll = 0, otherSig = 0, otherAll = 0, homeStages = 0;
            for (int seed = 1; seed <= 200; seed++)
            {
                var run = new Run(seed);
                for (int stage = 1; stage <= 36; stage++)
                {
                    int world = Run.WorldOf(stage);
                    int inWorld = Run.StageInWorldOf(stage);
                    string where = $"seed {seed} stage {stage}";
                    StagePlan plan = StageGenerator.GenerateFor(run, stage);
                    BubbleKind? signature = StageGenerator.SignatureKind(world);

                    foreach (var spawn in plan.spawns)
                    {
                        Assert.AreNotEqual(BubbleKind.Drifter, spawn.kind, $"{where} drifter");
                        if (world == 1) CollectionAssert.DoesNotContain(NewKinds, spawn.kind, $"{where} world 1");
                        if (Contains(NewKinds, spawn.kind) && (!signature.HasValue || spawn.kind != signature.Value))
                            Assert.Fail($"{where} has {spawn.kind} outside its home world");
                        if (spawn.kind == BubbleKind.Sinker) Assert.Greater(spawn.tier, 0, $"{where} sinker size");
                    }

                    var report = StageReport.From(stage, plan, run.IsHardWorld(world));
                    Assert.LessOrEqual(report.bubbleSpend + report.rollerSpend, report.budget + 0.01f, $"{where} overspent");
                    if (Run.IsBossStageNumber(stage)) continue;

                    if (world > 1 && inWorld == 1) AssertIntroduction(plan, signature.Value, stage, where);
                    if (plan.recipeName == "Giants")
                        foreach (var spawn in plan.spawns)
                            Assert.That(spawn.kind, Is.EqualTo(BubbleKind.Gloop).Or.EqualTo(BubbleKind.Trigloop)
                                .Or.EqualTo(BubbleKind.Lead).Or.EqualTo(BubbleKind.Lucky), $"{where} giants kind");
                    if (plan.recipeName == "Home turf")
                    {
                        Assert.Greater(world, 1, where);
                        homeStages++;
                        foreach (var spawn in plan.spawns)
                        {
                            if (spawn.kind == BubbleKind.Lucky) continue;
                            homeAll++;
                            if (spawn.kind == signature.Value) homeSig++;
                            else Assert.AreEqual(BubbleKind.Gloop, spawn.kind, $"{where} home turf fill");
                        }
                    }
                    else if (world > 1 && inWorld > 1 && plan.recipeName != "Giants" && plan.recipeName != "Introduction")
                    {
                        foreach (var spawn in plan.spawns)
                        {
                            if (spawn.kind == BubbleKind.Lucky) continue;
                            otherAll++;
                            if (spawn.kind == signature.Value) otherSig++;
                        }
                    }
                    AssertClingers(plan, where);
                    AssertClear(plan, where);
                }
            }
            Assert.Greater(homeStages, 0, "Home turf was drawn");
            float home = homeSig / (float)homeAll, other = otherSig / (float)otherAll;
            Assert.That(home, Is.InRange(0.55f, 0.85f), $"home turf signature share {home}");
            Assert.That(other, Is.InRange(0.20f, 0.45f), $"other stages signature share {other}");
        }

        [Test]
        public void OtherWorldsAreCoveredByTheSignatureTable()
        {
            Assert.AreEqual(6, Tuning.I.signatureKinds.Length);
            Assert.AreEqual(0.33f, Tuning.I.signatureShare);
            Assert.AreEqual(0.6f, Tuning.I.introBudgetScale);
            var home = Tuning.I.Recipe(RecipeId.HomeTurf);
            Assert.AreEqual(0.7f, home.signatureShare);
            Assert.IsTrue(home.gloopFill);
            var intro = Tuning.I.Recipe(RecipeId.Introduction);
            Assert.IsFalse(intro.inPool);
            Assert.IsTrue(intro.plain);
            Assert.IsTrue(Tuning.I.Recipe(RecipeId.Giants).classicKindsOnly);
        }

        static void AssertIntroduction(StagePlan plan, BubbleKind signature, int stage, string where)
        {
            Assert.AreEqual("Introduction", plan.recipeName, where);
            int sig = 0;
            foreach (var spawn in plan.spawns)
            {
                Assert.That(spawn.kind == signature || spawn.kind == BubbleKind.Gloop, $"{where} intro kind {spawn.kind}");
                Assert.IsFalse(spawn.ceramic, $"{where} intro ceramic");
                Assert.AreNotEqual(BubbleKind.Lucky, spawn.kind, $"{where} intro lucky");
                if (spawn.kind == signature) sig++;
            }
            Assert.GreaterOrEqual(sig * 2, plan.spawns.Count, $"{where} intro signature share");
            Assert.AreEqual(0, plan.rollers.Count, $"{where} intro rollers");
            Assert.AreEqual(0, plan.waves.Count, $"{where} intro waves");
            var report = StageReport.From(stage, plan, false);
            Assert.LessOrEqual(report.bubbleSpend, 0.6f * StageGenerator.Budget(stage, false) + 0.01f, $"{where} intro spend");
        }

        static void AssertClingers(StagePlan plan, string where)
        {
            var t = Tuning.I;
            var placed = new List<(Vector2 pos, float radius)>();
            foreach (var spawn in plan.spawns)
            {
                if (spawn.kind != BubbleKind.Clinger) continue;
                float radius = t.tiers[spawn.tier].diameter * 0.5f;
                Vector2 p = spawn.position;
                Assert.GreaterOrEqual(Mathf.Abs(p.x), 5f - 0.001f, $"{where} clinger beside Wick");
                bool ceiling = Mathf.Abs(p.y + radius - t.arenaHeight) <= 0.06f;
                bool underTile = plan.map.IsSolid(StageMap.Col(p.x), StageMap.Row(p.y + radius + 0.01f));
                Assert.IsTrue(ceiling || underTile, $"{where} clinger hangs from nothing at {p}");
                if (spawn.tier == 0) Assert.IsTrue(ceiling, $"{where} XL clinger off the ceiling");
                foreach (var other in placed)
                    Assert.GreaterOrEqual((other.pos - p).magnitude, other.radius + radius - 0.001f, $"{where} clingers overlap");
                placed.Add((p, radius));
            }
        }

        static void AssertClear(StagePlan plan, string where)
        {
            foreach (var spawn in plan.spawns)
            {
                float radius = Tuning.I.tiers[spawn.tier].diameter * 0.5f;
                Vector2 p = spawn.position;
                if (spawn.kind == BubbleKind.Clinger) p.y -= 0.05f;
                Vector2 resolved = p;
                plan.map.ResolveCircle(ref resolved, radius);
                Assert.LessOrEqual((resolved - p).sqrMagnitude, 0.000001f, $"{where} {spawn.kind} inside a tile at {spawn.position}");
            }
        }

        static bool Contains(BubbleKind[] list, BubbleKind kind)
        {
            foreach (var k in list) if (k == kind) return true;
            return false;
        }
    }
}
