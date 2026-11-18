using System.Linq;
using NUnit.Framework;

namespace Bubblewright.Tests
{
    /// <summary>World 5 (Catacombs) stage pool. Each world's thread pins its own world in its own file.</summary>
    public class World5StageTests
    {
        [Test]
        public void World5HasOneIntroThreeMediumFourHardAndFourHardestStages()
        {
            var world = StageSpec.All.Where(s => s.World == 5).ToList();
            string ids = string.Join(", ", world.Select(s => s.Id));
            Assert.AreEqual(12, world.Count, ids);
            Assert.AreEqual(1, world.Count(s => s.Difficulty == 1 && s.MaxStageInWorld == 1), ids);
            Assert.AreEqual(3, world.Count(s => s.Difficulty == 2 && s.MinStageInWorld >= 2), ids);
            Assert.AreEqual(4, world.Count(s => s.Difficulty == 3 && s.MinStageInWorld >= 2), ids);
            Assert.AreEqual(4, world.Count(s => s.Difficulty == 4 && s.MinStageInWorld >= 3), ids);
            foreach (var id in new[]
            {
                "w5_haunted_nave", "w5_sealed_ossuary", "w5_crypt_crawl", "w5_divided_tombs",
                "w5_phantom_bunker", "w5_deep_catacomb", "w5_bone_sanctum",
            })
                Assert.IsNotNull(StageSpec.ById(id), id);
        }
    }
}
