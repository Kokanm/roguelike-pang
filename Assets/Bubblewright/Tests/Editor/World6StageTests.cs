using System.Linq;
using NUnit.Framework;

namespace Bubblewright.Tests
{
    /// <summary>World 6 (Lava) stage pool. Each world's Task 7 thread pins its own pool in its own file.</summary>
    public class World6StageTests
    {
        [Test]
        public void World6HasOneIntroThreeMediumFourHardAndFourHardestStages()
        {
            var world = StageSpec.All.Where(s => s.World == 6).ToList();
            Assert.AreEqual(12, world.Count, string.Join(", ", world.Select(s => s.Id)));
            Assert.AreEqual(1, world.Count(s => s.Difficulty == 1 && s.MaxStageInWorld == 1));
            Assert.AreEqual(3, world.Count(s => s.Difficulty == 2 && s.MinStageInWorld >= 2));
            Assert.AreEqual(4, world.Count(s => s.Difficulty == 3 && s.MinStageInWorld >= 2));
            Assert.AreEqual(4, world.Count(s => s.Difficulty == 4 && s.MinStageInWorld >= 3));
        }

        [Test]
        public void EveryLavaStageHasAScorch()
        {
            foreach (var spec in StageSpec.All.Where(s => s.World == 6))
                Assert.IsTrue(spec.Build().spawns.Any(s => s.kind == BubbleKind.Scorch), spec.Id + " has no Scorch");
        }
    }
}
