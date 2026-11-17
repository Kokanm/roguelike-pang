using System.Linq;
using NUnit.Framework;

namespace Bubblewright.Tests
{
    /// <summary>
    /// World 3 (Desert) stage set. Its own file so the five world threads of Task 7 never edit the same test file.
    /// EveryDiscoveredStageAndMirrorPassesValidation in StageValidatorTests runs the game rules on each stage.
    /// </summary>
    public class World3StageTests
    {
        [Test]
        public void World3HasOneIntroThreeMediumFourHardAndFourHardestStages()
        {
            var world = StageSpec.All.Where(s => s.World == 3).ToList();
            string ids = string.Join(", ", world.Select(s => s.Id));
            Assert.AreEqual(12, world.Count, ids);
            Assert.AreEqual(1, world.Count(s => s.Difficulty == 1 && s.MaxStageInWorld == 1), ids);
            Assert.AreEqual(3, world.Count(s => s.Difficulty == 2 && s.MinStageInWorld >= 2), ids);
            Assert.AreEqual(4, world.Count(s => s.Difficulty == 3 && s.MinStageInWorld >= 2), ids);
            Assert.AreEqual(4, world.Count(s => s.Difficulty == 4 && s.MinStageInWorld >= 3), ids);
            Assert.AreEqual("w3_sinking_dunes", world.Single(s => s.Difficulty == 1).Id);
        }

        [Test]
        public void World3HasAWallJumpWellAndAMultiStoreyClimb()
        {
            var world = StageSpec.All.Where(s => s.World == 3).ToList();
            Assert.IsNotNull(StageSpec.ById("w3_twin_cisterns"), "the wall-jump well stage");
            Assert.IsTrue(world.Any(s => s.Storeys == 3), "a three-storey tower");
        }
    }
}
