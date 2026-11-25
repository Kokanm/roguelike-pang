using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>
    /// Well Shaft (World 1) has no ladder: Wick crouches under the well's lip and climbs the one-column well with chained
    /// wall jumps. His hitbox is exactly one tile wide, so this drives the real Wick through the real stage map.
    /// </summary>
    public class WellShaftClimbTests
    {
        const float Dt = 1f / 60f;

        GameObject root;
        Wick wick;
        StageMap previousMap;

        [SetUp]
        public void SetUp()
        {
            previousMap = Game.Map;
            root = new GameObject("WellShaftClimbTests");
            wick = Wick.Create(root.transform, Effects.Create(root.transform));
        }

        [TearDown]
        public void TearDown()
        {
            Game.InstallMap(previousMap);
            Object.DestroyImmediate(root);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WickCrouchesIntoTheWellAndWallJumpsOutOntoTheRoof(bool mirror)
        {
            var plan = StageSpec.ById("w1_well_shaft").Build(mirror);
            Game.InstallMap(plan.map);
            float side = mirror ? -1f : 1f;
            int wellCol = mirror ? StageMap.Cols - 1 - 26 : 26;
            float wellX = StageMap.CenterX(wellCol);
            float roofY = StageMap.CellRect(mirror ? 2 : 29, 8).yMax;

            wick.ResetForStage(wellX - side * 4f, 0f, 3);
            for (int i = 0; i < 6; i++) wick.Tick(0f, 0f, false, Dt, true);

            // Walk in under the lip holding Down.
            for (int i = 0; i < 240 && Mathf.Abs(wick.transform.position.x - wellX) > 0.05f; i++)
                wick.Tick(side, -1f, false, Dt, true);
            Assert.AreEqual(wellX, wick.transform.position.x, 0.1f, "he gets into the well under its lip");

            // Inside the well: jump, and kick off whichever wall he touches, always toward the other one.
            float dir = side;
            wick.Tick(0f, 0f, true, Dt, true);
            float best = 0f;
            for (int i = 0; i < 900 && wick.FeetY < roofY - 0.01f; i++)
            {
                bool jump = (wick.Mode == Mode.Fall && wick.WallSide != 0) || wick.Mode == Mode.Ground;
                if (wick.Mode == Mode.Fall && wick.WallSide != 0) dir = -wick.WallSide;
                wick.Tick(dir, 0f, jump, Dt, true);
                best = Mathf.Max(best, wick.FeetY);
            }

            // Then step out onto the roof.
            for (int i = 0; i < 120; i++) wick.Tick(side, 0f, false, Dt, true);
            Assert.GreaterOrEqual(wick.FeetY, roofY - 0.01f, $"highest feet {best:0.00}, roof at {roofY:0.00}");
            Assert.Greater(side * (wick.transform.position.x - wellX), 0.5f, "he ends on the roof beside the well");
        }
    }
}
