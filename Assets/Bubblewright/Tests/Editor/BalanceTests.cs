using NUnit.Framework;

namespace Bubblewright.Tests
{
    /// <summary>The many-seed balance summary and the pops-needed count behind it.</summary>
    public class BalanceTests
    {
        [Test]
        public void SummaryHasAHeaderAndThirtyEvenLines()
        {
            string[] lines = StageReport.Summary(5).Split('\n');
            Assert.AreEqual(31, lines.Length, "a header and one line per normal stage slot");
            Assert.That(lines[0], Does.StartWith("stage\t"));
            int fields = lines[0].Split('\t').Length;
            foreach (var line in lines)
            {
                var parts = line.Split('\t');
                Assert.AreEqual(fields, parts.Length, line);
                foreach (var p in parts)
                {
                    Assert.AreNotEqual("NaN", p, line);
                    Assert.That(p, Does.Not.Contain("Infinity"), line);
                }
            }
        }

        [Test]
        public void PopsNeededMatchesTheHandCount()
        {
            Assert.AreEqual(15, StageReport.PopsNeeded(BubbleKind.Gloop, 0));
            Assert.AreEqual(40, StageReport.PopsNeeded(BubbleKind.Trigloop, 0));
            Assert.AreEqual(4, StageReport.PopsNeeded(BubbleKind.Gloop, 2, ceramic: true));
            Assert.AreEqual(1, StageReport.PopsNeeded(BubbleKind.Gloop, 3));
            Assert.AreEqual(3, StageReport.PopsNeeded(new RollerSpec { tier = 0 }));
            Assert.AreEqual(4, StageReport.PopsNeeded(new RollerSpec { tier = 0, ceramic = true }));
            Assert.AreEqual(0, StageReport.PopsNeeded(new RollerSpec { tier = 0, steel = true }));
        }

        [Test]
        public void PopsNeededForAPlanSkipsLuckyAndSteel()
        {
            var plan = new StagePlan
            {
                spawns = new System.Collections.Generic.List<SpawnSpec>
                {
                    new SpawnSpec { kind = BubbleKind.Gloop, tier = 0 },
                    new SpawnSpec { kind = BubbleKind.Lucky, tier = 2 },
                },
            };
            plan.waves.Add(new System.Collections.Generic.List<SpawnSpec> { new SpawnSpec { kind = BubbleKind.Gloop, tier = 3 } });
            plan.rollers.Add(new RollerSpec { tier = 0 });
            plan.rollers.Add(new RollerSpec { tier = 0, steel = true });
            Assert.AreEqual(15 + 1 + 3, StageReport.PopsNeeded(plan));
        }
    }
}
