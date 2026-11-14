using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>Enemies return as guests after their home world; the Drifter only ever visits, and its first visit is gentle.</summary>
    public class GuestTests
    {
        const int Seeds = 300;

        static Run MakeRun(int seed, bool hard)
        {
            var run = new Run(seed);
            if (hard)
                for (int w = 2; w <= 6; w++) run.SetHard(w, Debuff.All[0]);
            return run;
        }

        static bool IsHomeOrClassic(BubbleKind kind, int world) =>
            kind == BubbleKind.Gloop || kind == BubbleKind.Trigloop || kind == BubbleKind.Lead || kind == BubbleKind.Lucky ||
            StageGenerator.SignatureKind(world) == kind;

        [Test]
        public void GuestsFollowTheirRules([Values(false, true)] bool hard)
        {
            var t = Tuning.I;
            int eligible = 0, withGuest = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var run = MakeRun(seed, hard);
                for (int stage = 1; stage <= 36; stage++)
                {
                    int world = Run.WorldOf(stage);
                    string where = $"seed {seed} stage {stage} hard {hard}";
                    StagePlan plan = StageGenerator.GenerateFor(run, stage);
                    BubbleKind? guest = StageGenerator.GuestFor(seed, stage, hard);
                    var recipe = StageGenerator.RecipeFor(seed, stage);

                    if (world == 1 || Run.IsBossStageNumber(stage) || recipe.id == RecipeId.Introduction || recipe.classicKindsOnly)
                    {
                        Assert.IsNull(guest, $"{where} guest where none is allowed");
                        Assert.IsNull(plan.guestName, $"{where} plan guest");
                        continue;
                    }
                    eligible++;
                    if (guest.HasValue) withGuest++;

                    // Kinds that are neither home nor classic: at most one, and it is the guest.
                    var foreign = new HashSet<BubbleKind>();
                    int guestBubbles = 0;
                    float guestCost = 0f;
                    foreach (var spawn in plan.spawns)
                    {
                        if (IsHomeOrClassic(spawn.kind, world)) continue;
                        foreign.Add(spawn.kind);
                        guestBubbles++;
                        guestCost += StageGenerator.BubbleCost(spawn.kind, spawn.tier);
                        Assert.IsFalse(spawn.ceramic, $"{where} guest ceramic");
                        Assert.AreNotEqual(BubbleKind.Scorch, spawn.kind, $"{where} scorch guest");
                        if (spawn.kind == BubbleKind.Drifter)
                        {
                            Assert.GreaterOrEqual(world, t.drifterFromWorld, $"{where} early drifter");
                            Assert.Greater(spawn.tier, 0, $"{where} xl drifter");
                        }
                        else
                        {
                            bool earlier = false;
                            for (int w = 2; w < world; w++) earlier |= StageGenerator.SignatureKind(w) == spawn.kind;
                            Assert.IsTrue(earlier, $"{where} {spawn.kind} is not from an earlier world");
                        }
                    }
                    Assert.LessOrEqual(foreign.Count, 1, $"{where} more than one guest kind");
                    if (foreign.Count == 1)
                    {
                        Assert.IsTrue(guest.HasValue, $"{where} guest without GuestFor");
                        Assert.AreEqual(guest.Value.ToString(), plan.guestName, $"{where} guest name");
                        Assert.IsTrue(foreign.Contains(guest.Value), where);
                    }
                    else Assert.IsNull(plan.guestName, $"{where} guest name without guest bubbles");
                    Assert.LessOrEqual(guestBubbles, t.guestMaxBubbles, $"{where} guest count");
                    Assert.LessOrEqual(guestCost, t.guestBudgetShare * plan.budget + 0.01f, $"{where} guest cost");

                    var report = StageReport.From(stage, plan, hard);
                    Assert.LessOrEqual(report.bubbleSpend + report.rollerSpend, report.budget + 0.01f, $"{where} overspent");
                    Assert.AreEqual(plan.guestName ?? "", report.guest ?? "", where);
                }
            }
            float share = withGuest / (float)eligible;
            if (hard) Assert.That(share, Is.InRange(0.31f, 0.49f), $"hard guest share {share}");
            else Assert.That(share, Is.InRange(0.22f, 0.38f), $"calm guest share {share}");
        }

        [Test]
        public void TheDrifterDebutsAmongGloops([Values(false, true)] bool hard)
        {
            int seedsWithDrifter = 0, laterNonGloop = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var run = MakeRun(seed, hard);
                bool seen = false;
                for (int stage = 1; stage <= 36; stage++)
                {
                    string where = $"seed {seed} stage {stage} hard {hard}";
                    StagePlan plan = StageGenerator.GenerateFor(run, stage);
                    bool hasDrifter = plan.spawns.Exists(s => s.kind == BubbleKind.Drifter);
                    if (!hasDrifter) { Assert.IsFalse(StageGenerator.IsDrifterDebut(run, stage) && plan.guestName != null, where); continue; }
                    foreach (var spawn in plan.spawns)
                    {
                        Assert.IsTrue(DrifterBehavior.StartIsFair(spawn.position, DriftVelocity(spawn), Vector2.zero) || spawn.kind != BubbleKind.Drifter, $"{where} unfair drifter");
                        if (spawn.kind == BubbleKind.Drifter) Assert.Greater(spawn.tier, 0, where);
                    }
                    if (!seen)
                    {
                        seen = true;
                        seedsWithDrifter++;
                        Assert.IsTrue(StageGenerator.IsDrifterDebut(run, stage), $"{where} first drifter is not the debut");
                        Assert.AreEqual("Drifter arrives", plan.recipeName, where);
                        foreach (var spawn in plan.spawns)
                        {
                            if (spawn.kind == BubbleKind.Drifter || spawn.kind == BubbleKind.Lucky) continue;
                            Assert.AreEqual(BubbleKind.Gloop, spawn.kind, $"{where} debut kind");
                            Assert.IsFalse(spawn.ceramic, $"{where} debut ceramic");
                        }
                        foreach (var spawn in plan.spawns) Assert.IsFalse(spawn.ceramic, where);
                        foreach (var roller in plan.rollers) Assert.IsTrue(roller.steel, $"{where} debut roller");
                        Assert.AreEqual(0, plan.waves.Count, $"{where} debut waves");
                    }
                    else
                    {
                        Assert.IsFalse(StageGenerator.IsDrifterDebut(run, stage), where);
                        if (plan.spawns.Exists(s => s.kind != BubbleKind.Gloop && s.kind != BubbleKind.Drifter && s.kind != BubbleKind.Lucky))
                            laterNonGloop++;
                    }
                }
            }
            Assert.Greater(seedsWithDrifter, Seeds / 2, "most seeds meet a Drifter");
            Assert.Greater(laterNonGloop, 0, "later Drifter stages are not all plain");
        }

        static Vector2 DriftVelocity(SpawnSpec spec)
        {
            float v = Tuning.I.drifterSpeed / Mathf.Sqrt(2f);
            return new Vector2(spec.direction * v, spec.position.y >= 7f ? -v : v);
        }

        [Test]
        public void GenerationIsRepeatable()
        {
            for (int seed = 1; seed <= 60; seed++)
            {
                var run = MakeRun(seed, seed % 2 == 0);
                for (int stage = 1; stage <= 36; stage++)
                {
                    bool hard = run.IsHardWorld(Run.WorldOf(stage));
                    Assert.AreEqual(StageGenerator.GuestFor(seed, stage, hard), StageGenerator.GuestFor(seed, stage, hard));
                    var a = StageGenerator.GenerateFor(run, stage);
                    var b = StageGenerator.GenerateFor(run, stage);
                    Assert.AreEqual(a.guestName, b.guestName);
                    Assert.AreEqual(a.spawns.Count, b.spawns.Count);
                    for (int i = 0; i < a.spawns.Count; i++)
                    {
                        Assert.AreEqual(a.spawns[i].kind, b.spawns[i].kind);
                        Assert.AreEqual(a.spawns[i].tier, b.spawns[i].tier);
                        Assert.AreEqual(a.spawns[i].position, b.spawns[i].position);
                        Assert.AreEqual(a.spawns[i].direction, b.spawns[i].direction);
                    }
                }
            }
        }

        [Test]
        public void AStageWithoutARunNeverHasAGuest()
        {
            for (int stage = 1; stage <= 36; stage++)
            {
                var plan = StageGenerator.Generate(stage, new System.Random(5), false);
                Assert.IsNull(plan.guestName);
                foreach (var spawn in plan.spawns)
                    Assert.AreNotEqual(BubbleKind.Drifter, spawn.kind);
            }
        }

        [Test]
        public void TheReportHasAGuestColumn()
        {
            string[] header = StageReport.Header().Split('\t');
            int guest = System.Array.IndexOf(header, "guest");
            Assert.Greater(guest, 0);
            Assert.AreEqual("pattern", header[guest - 1]);
            var run = new Run(1);
            var line = StageReport.From(1, StageGenerator.GenerateFor(run, 1), false).Line().Split('\t');
            Assert.AreEqual(header.Length, line.Length);
        }
    }
}
