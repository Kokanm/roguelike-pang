using System.Collections.Generic;
using NUnit.Framework;

namespace Bubblewright.Tests
{
    /// <summary>Each normal stage draws one recipe, and that recipe limits how the budget is spent.</summary>
    public class RecipeTests
    {
        [Test]
        public void TheRecipeTableMatchesTheAgreedRows()
        {
            var tuning = Tuning.I;
            Assert.AreEqual(3, tuning.giantsMaxLate);
            Assert.AreEqual(4, tuning.giantsLateFromWorld);
            Assert.NotNull(tuning.recipes);
            Assert.AreEqual(8, tuning.recipes.Length);

            Expect(tuning.recipes[0], RecipeId.Mixed, "Mixed", 1, 0, 3, 1f, 0, 0f, 0, -1f, false);
            Expect(tuning.recipes[1], RecipeId.Giants, "Giants", 1, 0, 0, 1f, 2, 0f, 0, -1f, false);
            Expect(tuning.recipes[2], RecipeId.Swarm, "Swarm", 1, 2, 3, 2f, 0, 0f, 0, -1f, false);
            Expect(tuning.recipes[3], RecipeId.RollingFloor, "Rolling floor", 2, 0, 3, 1f, 3, 0.6f, 4, -1f, false);
            Expect(tuning.recipes[4], RecipeId.HardShell, "Hard shell", 3, 0, 3, 1f, 0, 0f, 0, 0.7f, true);
            Expect(tuning.recipes[5], RecipeId.Reinforcements, "Reinforcements", 2, 0, 3, 1f, 0, 0f, 0, -1f, false);
            Assert.AreEqual(2, tuning.recipes[5].waves);
            Assert.AreEqual(0.5f, tuning.recipes[5].startShare);
            Assert.AreEqual(1.15f, tuning.recipes[5].timeScale);
            Assert.AreEqual(RecipeId.HomeTurf, tuning.recipes[6].id);
            Assert.AreEqual(RecipeId.Introduction, tuning.recipes[7].id);
            foreach (var row in tuning.recipes)
                Assert.AreEqual(row.id != RecipeId.Introduction, row.inPool, row.name);
            Assert.AreSame(tuning.recipes[0], tuning.Recipe(RecipeId.Mixed));
            Assert.AreSame(tuning.recipes[0], tuning.Recipe((RecipeId)99), "an unknown id falls back to the first row");
        }

        [Test]
        public void RecipesHoldForEverySeedAndStage()
        {
            var tuning = Tuning.I;
            string mixed = tuning.Recipe(RecipeId.Mixed).name;
            string giants = tuning.Recipe(RecipeId.Giants).name;
            string swarm = tuning.Recipe(RecipeId.Swarm).name;
            string rolling = tuning.Recipe(RecipeId.RollingFloor).name;
            string shell = tuning.Recipe(RecipeId.HardShell).name;
            var rollingRow = tuning.Recipe(RecipeId.RollingFloor);
            int giantsSeen = 0, swarmSeen = 0, rollingSeen = 0, shellSeen = 0;

            for (int seed = 1; seed <= 200; seed++)
            {
                var run = new Run(seed);
                var counts = new Dictionary<int, Dictionary<string, int>>();
                for (int world = 1; world <= 6; world++) counts[world] = new Dictionary<string, int>();

                for (int stage = 1; stage <= 36; stage++)
                {
                    RecipeDef drawn = StageGenerator.RecipeFor(seed, stage);
                    Assert.AreSame(drawn, StageGenerator.RecipeFor(seed, stage), $"recipe row seed {seed} stage {stage}");

                    StagePlan plan = StageGenerator.GenerateFor(run, stage);
                    StagePlan again = StageGenerator.GenerateFor(run, stage);
                    AssertSameSpawns(plan, again, seed, stage);

                    int world = Run.WorldOf(stage);
                    if (Run.IsBossStageNumber(stage))
                    {
                        Assert.IsNull(plan.recipeName, $"boss seed {seed} stage {stage}");
                        Assert.IsNull(drawn, $"boss recipe seed {seed} stage {stage}");
                        continue;
                    }

                    Assert.NotNull(drawn, $"seed {seed} stage {stage}");
                    Assert.LessOrEqual(drawn.fromWorld, world, $"seed {seed} stage {stage} drew {drawn.name}");
                    Assert.IsTrue(drawn.inPool || drawn.id == RecipeId.Introduction, drawn.name);
                    Assert.AreEqual(world > 1 && inWorldOf(stage) == 1, drawn.id == RecipeId.Introduction, $"seed {seed} stage {stage} introduction");

                    int inWorld = Run.StageInWorldOf(stage);
                    if (world == 1 && inWorld <= 2)
                    {
                        Assert.AreEqual(RecipeId.Mixed, drawn.id, $"seed {seed} stage {stage}");
                        Assert.AreEqual(mixed, plan.recipeName, $"seed {seed} stage {stage}");
                    }

                    // A Drifter's first visit replaces the drawn recipe with a plain stage (see GuestTests).
                    if (plan.recipeName == StageGenerator.DebutRecipeName) continue;
                    string counted = plan.recipeName;
                    if (drawn.id == RecipeId.RollingFloor && counted == mixed)
                        counted = drawn.name;
                    else
                        Assert.AreEqual(drawn.name, plan.recipeName, $"seed {seed} stage {stage}");

                    RecipeDef shown = tuning.Recipe(RecipeId.Mixed);
                    foreach (var row in tuning.recipes)
                        if (row.name == plan.recipeName) { shown = row; break; }
                    Assert.LessOrEqual(shown.fromWorld, world, $"seed {seed} stage {stage} shows {plan.recipeName}");

                    bool slot = world == 1 ? inWorld >= 3 : inWorld >= 2;
                    if (slot)
                    {
                        counts[world].TryGetValue(counted, out int n);
                        counts[world][counted] = n + 1;
                    }

                    if (plan.recipeName == giants) { giantsSeen++; AssertGiants(plan, world, seed, stage); }
                    if (plan.recipeName == swarm) { swarmSeen++; AssertSwarm(plan, stage, seed); }
                    if (plan.recipeName == rolling) { rollingSeen++; AssertRollingFloor(plan, rollingRow, seed, stage); }
                    if (plan.recipeName == shell) { shellSeen++; AssertHardShell(plan, world, seed, stage); }

                    var report = StageReport.From(stage, plan, run.IsHardWorld(world));
                    Assert.LessOrEqual(report.bubbleSpend + report.rollerSpend, report.budget + 0.01f,
                        $"seed {seed} stage {stage} overspent");
                }

                for (int world = 1; world <= 6; world++)
                {
                    int slots = world == 1 ? 3 : 4;
                    int pool = 0;
                    foreach (var row in tuning.recipes)
                        if (row.inPool && row.fromWorld <= world) pool++;
                    Assert.Greater(pool, 0, $"world {world} pool");
                    int limit = pool >= slots ? 1 : (slots + pool - 1) / pool;
                    foreach (var kv in counts[world])
                        Assert.LessOrEqual(kv.Value, limit, $"seed {seed} world {world} {kv.Key} x{kv.Value} limit {limit}");
                }
            }

            Assert.Greater(giantsSeen, 0, "Giants was drawn");
            Assert.Greater(swarmSeen, 0, "Swarm was drawn");
            Assert.Greater(rollingSeen, 0, "Rolling floor kept its rollers");
            Assert.Greater(shellSeen, 0, "Hard shell was drawn");
        }

        [Test]
        public void GenerateWithoutARecipeLeavesTheNameEmpty()
        {
            for (int stage = 1; stage <= 36; stage++)
            {
                var plan = StageGenerator.Generate(stage, new System.Random(5), false);
                Assert.IsNull(plan.recipeName, $"stage {stage}");
            }
        }

        [Test]
        public void TheReportPutsTheRecipeAfterTheLayout()
        {
            var plan = StageGenerator.GenerateFor(new Run(1), 1);
            var report = StageReport.From(1, plan, false);
            Assert.AreEqual("Mixed", report.recipe);
            string header = StageReport.Header();
            Assert.IsTrue(header.Contains("layout\trecipe\tpattern\tguest\troad"), header);
            Assert.IsTrue(report.Line().Contains("\tMixed\t"), report.Line());

            var boss = StageGenerator.GenerateFor(new Run(1), 6);
            Assert.IsNull(StageReport.From(6, boss, false).recipe);
            string log = StageReport.LogRun(new Run(3));
            Assert.IsTrue(log.Contains("layout\trecipe\tpattern\tguest\troad"), log);
            Assert.IsTrue(log.Contains("\tMixed\t"), "stage 1 of the log names Mixed");
        }

        static int inWorldOf(int stage) => Run.StageInWorldOf(stage);

        static void AssertGiants(StagePlan plan, int world, int seed, int stage)
        {
            int xl = 0;
            foreach (var spawn in plan.spawns)
            {
                if (spawn.kind == BubbleKind.Lucky) continue;
                Assert.AreEqual(0, spawn.tier, $"seed {seed} stage {stage} giants tier");
                xl++;
            }
            int cap = world >= Tuning.I.giantsLateFromWorld ? Tuning.I.giantsMaxLate : Tuning.I.Recipe(RecipeId.Giants).startCapMax;
            Assert.LessOrEqual(xl, cap, $"seed {seed} stage {stage} giants count");
            foreach (var roller in plan.rollers)
                Assert.IsTrue(roller.steel, $"seed {seed} stage {stage} giants roller");
        }

        static void AssertSwarm(StagePlan plan, int stage, int seed)
        {
            int count = 0;
            foreach (var spawn in plan.spawns)
            {
                if (spawn.kind == BubbleKind.Lucky) continue;
                Assert.That(spawn.tier, Is.EqualTo(2).Or.EqualTo(3), $"seed {seed} stage {stage} swarm tier {spawn.tier}");
                count++;
            }
            Assert.LessOrEqual(count, 2 * StageGenerator.MaxStartBubbles(stage), $"seed {seed} stage {stage} swarm count");
            foreach (var roller in plan.rollers)
                Assert.IsTrue(roller.steel, $"seed {seed} stage {stage} swarm roller");
        }

        static void AssertRollingFloor(StagePlan plan, RecipeDef row, int seed, int stage)
        {
            int rollers = 0;
            float cost = 0f;
            foreach (var roller in plan.rollers)
            {
                if (roller.steel) continue;
                rollers++;
                cost += Tuning.I.rollerCost * (roller.ceramic ? Tuning.I.ceramicCostScale : 1f);
            }
            Assert.GreaterOrEqual(rollers, 1, $"seed {seed} stage {stage} rolling floor");
            Assert.LessOrEqual(rollers, row.rollerMax, $"seed {seed} stage {stage} rolling floor");
            Assert.LessOrEqual(cost, plan.budget * row.rollerShare + 0.01f, $"seed {seed} stage {stage} roller share");
            Assert.AreEqual(cost, plan.rollerSpend, 0.01f, $"seed {seed} stage {stage} roller spend");
            int bubbles = 0;
            foreach (var spawn in plan.spawns)
                if (spawn.kind != BubbleKind.Lucky) bubbles++;
            Assert.LessOrEqual(bubbles, row.startCapMax, $"seed {seed} stage {stage} rolling floor bubbles");
        }

        static void AssertHardShell(StagePlan plan, int world, int seed, int stage)
        {
            Assert.GreaterOrEqual(world, Tuning.I.Recipe(RecipeId.HardShell).fromWorld, $"seed {seed} stage {stage}");
            foreach (var roller in plan.rollers)
                if (!roller.steel) Assert.IsTrue(roller.ceramic, $"seed {seed} stage {stage} hard shell roller");
        }

        static void AssertSameSpawns(StagePlan a, StagePlan b, int seed, int stage)
        {
            Assert.AreEqual(a.spawns.Count, b.spawns.Count, $"seed {seed} stage {stage} spawn count");
            for (int i = 0; i < a.spawns.Count; i++)
            {
                Assert.AreEqual(a.spawns[i].kind, b.spawns[i].kind, $"seed {seed} stage {stage} kind");
                Assert.AreEqual(a.spawns[i].tier, b.spawns[i].tier, $"seed {seed} stage {stage} tier");
                Assert.AreEqual(a.spawns[i].position, b.spawns[i].position, $"seed {seed} stage {stage} position");
            }
        }

        static void Expect(RecipeDef row, RecipeId id, string name, int fromWorld, int minTier, int maxTier,
            float scale, int cap, float share, int rollerMax, float ceramic, bool allCeramic)
        {
            Assert.AreEqual(id, row.id);
            Assert.AreEqual(name, row.name);
            Assert.AreEqual(fromWorld, row.fromWorld);
            Assert.AreEqual(minTier, row.minTier);
            Assert.AreEqual(maxTier, row.maxTier);
            Assert.AreEqual(scale, row.startCapScale);
            Assert.AreEqual(cap, row.startCapMax);
            Assert.AreEqual(share, row.rollerShare);
            Assert.AreEqual(rollerMax, row.rollerMax);
            Assert.AreEqual(ceramic, row.ceramicChance);
            Assert.AreEqual(allCeramic, row.allRollersCeramic);
        }
    }
}
