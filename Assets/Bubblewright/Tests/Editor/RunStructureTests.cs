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
        public void TimeFollowsTheFormula()
        {
            // time = 60 + 8 * (world - 1) + 4 * (stage in world - 1). A boss stage number uses the same formula.
            Assert.AreEqual(60f, StagePicker.StageTime(1), 0.001f);
            Assert.AreEqual(60f + 8f + 8f, StagePicker.StageTime(9), 0.001f);
            Assert.AreEqual(60f + 40f, StagePicker.StageTime(31), 0.001f);
            Assert.AreEqual(60f + 20f, StagePicker.StageTime(6), 0.001f);
        }

        [Test]
        public void TimeGrowsWithinAndAcrossWorlds()
        {
            for (int s = 1; s < Tuning.I.RunLength; s++)
            {
                if (Run.IsBossStageNumber(s) || Run.IsBossStageNumber(s + 1)) continue;
                Assert.Greater(StagePicker.StageTime(s + 1), StagePicker.StageTime(s), $"time {s} -> {s + 1}");
            }
            Assert.Greater(StagePicker.StageTime(31), StagePicker.StageTime(1));
        }

        static string Describe(StagePlan plan)
        {
            var cells = new List<string>();
            for (int r = 0; r < plan.map.Rows; r++)
                for (int c = 0; c < StageMap.Cols; c++)
                    if (plan.map.IsSolid(c, r) || plan.map.Get(c, r).ladder)
                        cells.Add($"{c},{r},{plan.map.Get(c, r).ladder}");
            return string.Join("|", cells) + ":" +
                   string.Join(";", plan.spawns.Select(s => $"{s.kind}/{s.tier}/{s.position.x:F2}/{s.position.y:F2}/{s.direction}"));
        }

        [Test]
        public void SameSeedAndStageGiveTheSameStage()
        {
            var a = StagePicker.Build(RunAt(1234, 9), 9);
            var b = StagePicker.Build(RunAt(1234, 9), 9);
            Assert.AreEqual(Describe(a), Describe(b));
        }

        [Test]
        public void StagesDoNotDependOnWhatWasGeneratedBefore()
        {
            // A run that was restored straight to stage 9 must build the same stage as one that played up to it.
            var played = RunAt(77, 1);
            for (int s = 1; s < 9; s++) { played.Stage = s; StagePicker.Build(played, s); played.RollChoices(); }
            played.Stage = 9;
            var restored = RunAt(77, 9);
            Assert.AreEqual(Describe(StagePicker.Build(restored, 9)), Describe(StagePicker.Build(played, 9)));
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
            float plain = StageSpec.ById("w2_canopy_ambush").Build(false).spawns[0].position.x;
            float mirrored = StageSpec.ById("w2_canopy_ambush").Build(true).spawns[0].position.x;
            bool sawPlain = false, sawMirror = false;
            for (int seed = 1; seed <= 40; seed++)
            {
                float x = StagePicker.Build(RunAt(seed, 7), 7).spawns[0].position.x;
                if (Mathf.Approximately(x, plain)) sawPlain = true;
                if (Mathf.Approximately(x, mirrored)) sawMirror = true;
            }
            Assert.IsTrue(sawPlain && sawMirror, "the forest stage is mirrored for some seeds and not for others");
        }

        [Test]
        public void EveryStageHasBubblesAndBossesHaveNone()
        {
            for (int seed = 1; seed <= 5; seed++)
                for (int s = 1; s <= Tuning.I.RunLength; s++)
                {
                    var plan = StagePicker.Build(RunAt(seed, s), s);
                    if (Run.IsBossStageNumber(s))
                        Assert.AreEqual(0, plan.spawns.Count, $"seed {seed} boss stage {s}");
                    else
                        Assert.GreaterOrEqual(plan.spawns.Count, 1, $"seed {seed} stage {s}");
                }
        }

        [Test]
        public void LeadsOnlyFromWorld2AndTrigloopsOnlyFromStage3()
        {
            for (int seed = 1; seed <= 30; seed++)
            {
                foreach (var spec in StagePicker.Build(RunAt(seed, 3), 3).spawns)
                    Assert.AreNotEqual(BubbleKind.Lead, spec.kind, $"seed {seed}");
                foreach (var spec in StagePicker.Build(RunAt(seed, 2), 2).spawns)
                    Assert.AreNotEqual(BubbleKind.Trigloop, spec.kind, $"seed {seed}");
            }
        }
    }
}
