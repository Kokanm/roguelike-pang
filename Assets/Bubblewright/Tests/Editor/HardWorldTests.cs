using System.Linq;
using NUnit.Framework;

namespace Bubblewright.Tests
{
    /// <summary>Hard worlds: more bubbles, less time, one debuff for the whole world, double score, rarity and the richer pick.</summary>
    public class HardWorldTests
    {
        static Run RunAt(int stage, int seed = 1) => new Run(seed) { Stage = stage };

        [Test]
        public void HardWorldScalesBudgetAndTime()
        {
            var run = RunAt(8); // world 2, stage 2
            run.SetHard(2, Debuff.All[0]);
            Assert.IsTrue(run.IsHard);
            float calmBudget = StageGenerator.Budget(8), calmTime = StageGenerator.StageTime(8);
            Assert.AreEqual(calmBudget * 1.3f, StageGenerator.Budget(8, run.IsHard), 0.001f);
            Assert.AreEqual(calmTime * 0.75f, StageGenerator.StageTime(8, run.IsHard), 0.001f);
            // the boss stage of a hard world gets the shorter clock too
            Assert.AreEqual(StageGenerator.StageTime(12) * 0.75f, StageGenerator.StageTime(12, true), 0.001f);
        }

        [Test]
        public void HardWorldIsOnlyThatWorld()
        {
            var run = RunAt(8);
            run.SetHard(2, Debuff.All[0]);
            run.Stage = 6; // world 1
            Assert.IsFalse(run.IsHard);
            Assert.IsNull(run.CurrentDebuff);
            run.Stage = 13; // world 3
            Assert.IsFalse(run.IsHard);
            Assert.IsNull(run.CurrentDebuff);
            run.Stage = 12; // last stage of world 2
            Assert.IsTrue(run.IsHard);
            Assert.AreEqual("Heavy Boots", run.CurrentDebuff.Name);
        }

        [Test]
        public void EachDebuffChangesOnlyTheNumberItNames()
        {
            var changed = Debuff.All.Select(d => new[]
            {
                d.speedScale != 1f, d.invulnerableScale != 1f, d.harpoonSpeedScale != 1f, d.noRelight,
            }.Count(x => x)).ToArray();
            CollectionAssert.AreEqual(new[] { 1, 1, 1, 1 }, changed);

            Assert.AreEqual(0.85f, Debuff.ByName("Heavy Boots").speedScale, 0.0001f);
            Assert.AreEqual(0.5f, Debuff.ByName("Short Glow").invulnerableScale, 0.0001f);
            Assert.AreEqual(0.8f, Debuff.ByName("Frayed Cable").harpoonSpeedScale, 0.0001f);
            Assert.IsTrue(Debuff.ByName("Cold Wick").noRelight);
            Assert.AreEqual(4, Debuff.All.Length);
        }

        [Test]
        public void ColdWickStopsRelightCards()
        {
            var run = RunAt(1);
            run.Hearts = 1; // a heart is missing, so Relight could be offered
            Assert.IsTrue(Enumerable.Range(1, 80).Any(stage =>
            {
                run.Stage = stage;
                return run.RollChoices().Any(u => u.Name == "Relight");
            }), "sanity: Relight is offered in a calm world");

            run.SetHard(1, Debuff.ByName("Cold Wick"));
            for (int stage = 1; stage <= 6; stage++)
            {
                for (int seed = 1; seed <= 10; seed++)
                {
                    var r = RunAt(stage, seed);
                    r.Hearts = 1;
                    r.SetHard(1, Debuff.ByName("Cold Wick"));
                    CollectionAssert.DoesNotContain(r.RollChoices().Select(u => u.Name).ToArray(), "Relight", $"seed {seed} stage {stage}");
                }
            }
        }

        [Test]
        public void HardWorldDoublesScore()
        {
            var calm = RunAt(8);
            calm.AddScore(100);
            Assert.AreEqual(100, calm.Score);

            var hard = RunAt(8);
            hard.SetHard(2, Debuff.All[1]);
            hard.AddScore(100);
            Assert.AreEqual(200, hard.Score);
        }

        [Test]
        public void RareUpgradesAreTheThreeInThePlan()
        {
            var rare = Upgrade.All.Where(u => u.Rarity == Rarity.Rare).Select(u => u.Name).OrderBy(n => n).ToArray();
            CollectionAssert.AreEqual(new[] { "Anchor Tip", "Spare Wick", "Twin Cable" }, rare);
        }

        [Test]
        public void RichPickOffersFourDifferentCardsWithARareOne()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var run = RunAt(6, seed); // boss stage of world 1
                run.SetHard(1, Debuff.All[2]);
                Assert.IsTrue(run.IsRichPick);
                var cards = run.RollChoices(4, true);
                Assert.AreEqual(4, cards.Count, $"seed {seed}");
                Assert.AreEqual(4, cards.Distinct().Count(), $"seed {seed}: duplicate card");
                Assert.IsTrue(cards.Any(c => c.Rarity == Rarity.Rare), $"seed {seed}: no rare card");
            }
            Assert.IsFalse(RunAt(6).IsRichPick, "a calm boss stage gives the normal pick");
        }

        [Test]
        public void TwoCardsFromARichPickAreBothTaken()
        {
            var run = RunAt(6);
            run.SetHard(1, Debuff.All[0]);
            var cards = run.RollChoices(4, true);
            var first = cards[0];
            var second = cards[1];
            run.Take(first);
            cards.Remove(first);
            Assert.AreEqual(3, cards.Count);
            run.Take(second);
            Assert.AreEqual(1, run.TimesTaken(first));
            Assert.AreEqual(1, run.TimesTaken(second));
        }

        [Test]
        public void NormalPickStillOffersThreeCards()
        {
            for (int seed = 1; seed <= 10; seed++)
                Assert.AreEqual(3, RunAt(3, seed).RollChoices().Count);
        }
    }
}
