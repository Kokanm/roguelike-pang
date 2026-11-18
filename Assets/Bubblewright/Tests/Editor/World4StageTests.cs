using System.Linq;
using NUnit.Framework;

namespace Bubblewright.Tests
{
    /// <summary>World 4 (Winter) stage pool. Kept in its own file so the five world threads of Task 7 never conflict.</summary>
    public class World4StageTests
    {
        [Test]
        public void World4HasOneIntroThreeMediumFourHardAndFourHardestStages()
        {
            var world = StageSpec.All.Where(s => s.World == 4).ToList();
            string ids = string.Join(", ", world.Select(s => s.Id));
            Assert.AreEqual(12, world.Count, ids);
            Assert.AreEqual(1, world.Count(s => s.Difficulty == 1 && s.MaxStageInWorld == 1), ids);
            Assert.AreEqual(3, world.Count(s => s.Difficulty == 2 && s.MinStageInWorld >= 2), ids);
            Assert.AreEqual(4, world.Count(s => s.Difficulty == 3 && s.MinStageInWorld >= 2), ids);
            Assert.AreEqual(4, world.Count(s => s.Difficulty == 4 && s.MinStageInWorld >= 3), ids);
            Assert.IsNotNull(StageSpec.ById("w4_first_freeze"));
            Assert.IsNotNull(StageSpec.ById("w4_whiteout_citadel"));
        }

        [Test]
        public void World4StagesAndMirrorsPassValidation()
        {
            foreach (var spec in StageSpec.All.Where(s => s.World == 4))
                foreach (bool mirror in new[] { false, true })
                {
                    var errors = StageValidator.Validate(spec, mirror);
                    Assert.IsEmpty(errors, $"{spec.Id} (mirror {mirror}):\n" + string.Join("\n", errors));
                }
        }
    }
}
