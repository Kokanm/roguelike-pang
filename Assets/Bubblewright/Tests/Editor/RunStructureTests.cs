using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>The run is 6 worlds of 6 stages (36), the 6th stage of each world is a boss stage, and stages rebuild from the seed.</summary>
    public class RunStructureTests
    {
        static Run RunAt(int seed, int stage) => new Run(seed) { Stage = stage };

        [Test]
        public void StageNumbersMapToWorlds()
        {
            Assert.AreEqual(36, Tuning.I.RunLength);

            var first = RunAt(1, 1);
            Assert.AreEqual(1, first.World);
            Assert.AreEqual(1, first.StageInWorld);
            Assert.IsFalse(first.IsBossStage);

            var sixth = RunAt(1, 6);
            Assert.AreEqual(1, sixth.World);
            Assert.AreEqual(6, sixth.StageInWorld);
            Assert.IsTrue(sixth.IsBossStage);

            var seventh = RunAt(1, 7);
            Assert.AreEqual(2, seventh.World);
            Assert.AreEqual(1, seventh.StageInWorld);

            var last = RunAt(1, 36);
            Assert.AreEqual(6, last.World);
            Assert.AreEqual(6, last.StageInWorld);
            Assert.IsTrue(last.IsBossStage);
            Assert.IsTrue(last.IsLastStage);
            Assert.IsFalse(RunAt(1, 35).IsLastStage);
        }

        [Test]
        public void EveryWorldHasAName()
        {
            for (int w = 1; w <= Tuning.I.worlds; w++) Assert.IsFalse(string.IsNullOrEmpty(Run.WorldNameOf(w)), $"world {w}");
            Assert.AreEqual("Rooftops", Run.WorldNameOf(1));
            Assert.AreEqual("Lava", Run.WorldNameOf(6));
        }

        [Test]
        public void BudgetAndTimeFollowTheFormulas()
        {
            // budget = (8 + 6 * (world - 1) + 2 * (stage in world - 1)) * jump bonus; time = 60 + 8 * (world - 1) + 4 * (stage in world - 1)
            float j = Tuning.I.jumpBudgetBonus;
            Assert.AreEqual(1.1f, j, 0.0001f);
            Assert.AreEqual(8f * j, StageGenerator.Budget(1), 0.001f);
            Assert.AreEqual(60f, StageGenerator.StageTime(1), 0.001f);

            // stage 9 = world 2, stage 3 in the world
            Assert.AreEqual((8f + 6f + 4f) * j, StageGenerator.Budget(9), 0.001f);
            Assert.AreEqual(60f + 8f + 8f, StageGenerator.StageTime(9), 0.001f);

            // stage 31 = world 6, stage 1 in the world
            Assert.AreEqual((8f + 30f) * j, StageGenerator.Budget(31), 0.001f);
            Assert.AreEqual(60f + 40f, StageGenerator.StageTime(31), 0.001f);

            // the boss stage follows the same budget and time rules (the boss stage itself spawns no bubbles)
            Assert.AreEqual((8f + 10f) * j, StageGenerator.Budget(6), 0.001f);
            Assert.AreEqual(60f + 20f, StageGenerator.StageTime(6), 0.001f);
        }

        [Test]
        public void BudgetAndTimeGrowWithinAndAcrossWorlds()
        {
            for (int s = 1; s < Tuning.I.RunLength; s++)
            {
                if (Run.IsBossStageNumber(s) || Run.IsBossStageNumber(s + 1)) continue; // a world's last stage is followed by a new world, which starts lower
                Assert.Greater(StageGenerator.Budget(s + 1), StageGenerator.Budget(s), $"budget {s} -> {s + 1}");
                Assert.Greater(StageGenerator.StageTime(s + 1), StageGenerator.StageTime(s), $"time {s} -> {s + 1}");
            }
            Assert.Greater(StageGenerator.Budget(31), StageGenerator.Budget(1));
            Assert.Greater(StageGenerator.StageTime(31), StageGenerator.StageTime(1));
        }

        [Test]
        public void StartBubbleCapIs4_4_5_5_6_6()
        {
            var caps = new[] { 1, 7, 13, 19, 25, 31 }.Select(StageGenerator.MaxStartBubbles).ToArray();
            CollectionAssert.AreEqual(new[] { 4, 4, 5, 5, 6, 6 }, caps);
        }

        static string Describe(StagePlan plan)
        {
            var cells = new List<string>();
            for (int r = 0; r < StageMap.Rows; r++)
                for (int c = 0; c < StageMap.Cols; c++)
                    if (plan.map.IsSolid(c, r) || plan.map.Get(c, r).ladder)
                        cells.Add($"{c},{r},{plan.map.Get(c, r).ladder}");
            return string.Join("|", cells) + ":" +
                   string.Join(";", plan.spawns.Select(s => $"{s.kind}/{s.tier}/{s.position.x:F2}/{s.position.y:F2}/{s.direction}"));
        }

        [Test]
        public void SameSeedAndStageGiveTheSameStage()
        {
            var a = StageGenerator.Generate(RunAt(1234, 9));
            var b = StageGenerator.Generate(RunAt(1234, 9));
            Assert.AreEqual(Describe(a), Describe(b));
        }

        [Test]
        public void StagesDoNotDependOnWhatWasGeneratedBefore()
        {
            // A run that was restored straight to stage 9 must build the same stage as one that played up to it.
            var played = RunAt(77, 1);
            for (int s = 1; s < 9; s++) { played.Stage = s; StageGenerator.Generate(played); played.RollChoices(); }
            played.Stage = 9;
            var restored = RunAt(77, 9);
            Assert.AreEqual(Describe(StageGenerator.Generate(restored)), Describe(StageGenerator.Generate(played)));
        }

        [Test]
        public void SameSeedAndStageGiveTheSameUpgradeChoices()
        {
            var a = RunAt(55, 9).RollChoices().Select(u => u.Name).ToArray();
            var b = RunAt(55, 9).RollChoices().Select(u => u.Name).ToArray();
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void DifferentSeedsUsuallyGiveDifferentStages()
        {
            var baseline = Describe(StageGenerator.Generate(RunAt(1, 9)));
            Assert.IsTrue(new[] { 2, 3, 4, 5, 6 }.Any(seed => Describe(StageGenerator.Generate(RunAt(seed, 9))) != baseline));
        }

        [Test]
        public void EveryStageHasBubblesWithinTheCap()
        {
            for (int seed = 1; seed <= 5; seed++)
                for (int s = 1; s <= Tuning.I.RunLength; s++)
                {
                    var plan = StageGenerator.Generate(RunAt(seed, s));
                    var specs = plan.spawns;
                    if (Run.IsBossStageNumber(s))
                    {
                        Assert.AreEqual(0, specs.Count, $"seed {seed} boss stage {s} has no generated bubbles");
                        continue;
                    }
                    Assert.GreaterOrEqual(specs.Count, 1, $"seed {seed} stage {s}");
                    // the free Lucky bubble sits on top of the recipe's cap (Swarm raises it, Giants and Rolling floor lower it)
                    Assert.LessOrEqual(specs.Count, BubbleCap(plan, s) + 1, $"seed {seed} stage {s}");
                }
        }

        [Test]
        public void LeadsOnlyFromWorld2AndTrigloopsOnlyFromStage3()
        {
            for (int seed = 1; seed <= 30; seed++)
            {
                foreach (var spec in StageGenerator.Generate(RunAt(seed, 3)).spawns) // world 1
                    Assert.AreNotEqual(BubbleKind.Lead, spec.kind, $"seed {seed}");
                foreach (var spec in StageGenerator.Generate(RunAt(seed, 2)).spawns) // before trigloops
                    Assert.AreNotEqual(BubbleKind.Trigloop, spec.kind, $"seed {seed}");
            }
        }

        /// <summary>Bubbles a recipe may place before the free Lucky one.</summary>
        static int BubbleCap(StagePlan plan, int stage)
        {
            int cap = StageGenerator.MaxStartBubbles(stage);
            if (plan.recipeName == null || Tuning.I.recipes == null) return cap;
            RecipeDef recipe = null;
            foreach (var row in Tuning.I.recipes)
                if (row != null && row.name == plan.recipeName) { recipe = row; break; }
            if (recipe == null) return cap;
            if (recipe.id == RecipeId.Giants && Run.WorldOf(stage) >= Tuning.I.giantsLateFromWorld)
                return Tuning.I.giantsMaxLate;
            if (recipe.startCapMax > 0) return recipe.startCapMax;
            return Mathf.RoundToInt(cap * recipe.startCapScale);
        }
    }
}
