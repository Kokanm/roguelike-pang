using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    public class WickMovementTests
    {
        static readonly string[] Tower =
        {
            "................................",
            "................................",
            "................................",
            "................................",
            "..........#########T##..........",
            "...................H............",
            "...................H............",
            "...................H............",
            "...................H............",
            "....##T##################T##....",
            "......H..................H......",
            "......H..................H......",
            "......H..................H......",
            "......H..................H......",
        };

        const float Dt = 1f / 60f;

        GameObject root;
        Effects fx;
        Wick wick;
        StageMap previousMap;
        StageMap map;

        [SetUp]
        public void SetUp()
        {
            previousMap = Game.Map;
            map = StageLayout.Parse(Tower);
            SetMap(map);
            root = new GameObject("WickMovementTestRoot");
            fx = Effects.Create(root.transform);
            wick = Wick.Create(root.transform, fx);
        }

        [TearDown]
        public void TearDown()
        {
            SetMap(previousMap);
            Object.DestroyImmediate(root);
        }

        static void SetMap(StageMap m)
        {
            var property = typeof(Game).GetProperty("Map", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(property, "Game.Map must exist.");
            property.SetValue(null, m);
        }

        void Step(float seconds, float move = 0f, float climb = 0f)
        {
            int ticks = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < ticks; i++)
                wick.Tick(move, climb, Dt, true);
        }

        [Test]
        public void StandingOnGroundKeepsFeetAtZero()
        {
            wick.ResetForStage(0f, 0f, 3);
            Step(1f);
            Assert.AreEqual(Mode.Ground, wick.Mode);
            Assert.AreEqual(0f, wick.FeetY, 1e-4f);
        }

        [Test]
        public void WalkingOffPlatformAEndsOnGroundWithAllHearts()
        {
            wick.ResetForStage(0f, 5f, 3);
            Step(3f, move: -1f);
            Assert.AreEqual(Mode.Ground, wick.Mode);
            Assert.AreEqual(0f, wick.FeetY, 1e-4f);
            Assert.AreEqual(3, wick.Hearts);
        }

        [Test]
        public void ClimbingUpLadderInColumn6ReachesPlatformA()
        {
            wick.ResetForStage(StageMap.CenterX(6), 0f, 3);
            Step(2f, climb: 1f);
            Assert.AreEqual(Mode.Ground, wick.Mode);
            Assert.AreEqual(5f, wick.FeetY, 1e-4f);
        }

        [Test]
        public void ClimbingDownLadderInColumn6ReturnsToGround()
        {
            wick.ResetForStage(StageMap.CenterX(6), 0f, 3);
            Step(2f, climb: 1f);
            Assert.AreEqual(5f, wick.FeetY, 1e-4f);

            Step(2f, climb: -1f);
            Assert.AreEqual(Mode.Ground, wick.Mode);
            Assert.AreEqual(0f, wick.FeetY, 1e-4f);
        }

        [Test]
        public void WalkingRightIntoSolidWallStopsAtTileEdge()
        {
            // Column 20 covers x from +4 to +5.
            map.Set(20, 0, new Cell { kind = TileKind.Solid });
            map.Set(20, 1, new Cell { kind = TileKind.Solid });

            wick.ResetForStage(0f, 0f, 3);
            Step(2f, move: 1f);

            float expectedX = StageMap.CellRect(20, 0).xMin - Tuning.I.hitbox.x * 0.5f;
            Assert.AreEqual(expectedX, wick.transform.position.x, 1e-4f);
            Assert.AreEqual(0f, wick.VelocityX, 1e-4f);
        }

        [Test]
        public void GrabbingLadderSlightlyOffCentreCentersXAndClimbs()
        {
            float offCenter = StageMap.CenterX(6) + 0.5f; // within ladderGrabWidth (0.6)
            wick.ResetForStage(offCenter, 0f, 3);
            Step(2f, climb: 1f);

            Assert.AreEqual(StageMap.CenterX(6), wick.transform.position.x, 1e-3f);
            Assert.AreEqual(5f, wick.FeetY, 1e-4f);
            Assert.AreEqual(Mode.Ground, wick.Mode);
        }

        [Test]
        public void MovingHorizontallyWithNoClimbLetsGoOfLadderAndFalls()
        {
            wick.ResetForStage(StageMap.CenterX(6), 0f, 3);
            Step(0.5f, climb: 1f);
            Assert.AreEqual(Mode.Climb, wick.Mode);
            Assert.Greater(wick.FeetY, 2f);

            wick.Tick(1f, 0f, Dt, true);
            Assert.AreEqual(Mode.Fall, wick.Mode);

            Step(1.5f, move: 1f, climb: 0f);
            Assert.AreEqual(Mode.Ground, wick.Mode);
            Assert.AreEqual(0f, wick.FeetY, 1e-4f);
        }

        [Test]
        public void HurtWhileClimbingCostsHeartWithoutKnockback()
        {
            wick.ResetForStage(StageMap.CenterX(6), 0f, 3);
            Step(0.4f, climb: 1f);
            Assert.AreEqual(Mode.Climb, wick.Mode);
            float xBefore = wick.transform.position.x;
            float yBefore = wick.FeetY;

            wick.Hurt(xBefore - 1f, 1);
            Step(0.2f, climb: 0f);

            Assert.AreEqual(2, wick.Hearts);
            Assert.AreEqual(Mode.Climb, wick.Mode);
            Assert.AreEqual(xBefore, wick.transform.position.x, 1e-4f);
            Assert.AreEqual(yBefore, wick.FeetY, 1e-4f);
        }

        [Test]
        public void NearLadderReflectsGrabRangeAndLevel()
        {
            wick.ResetForStage(StageMap.CenterX(6) + 0.5f, 0f, 3);
            Assert.IsTrue(wick.NearLadder);

            wick.ResetForStage(StageMap.CenterX(6), 5f, 3);
            Assert.IsTrue(wick.NearLadder, "Standing on top of a T tile is near the ladder below.");

            wick.ResetForStage(0f, 0f, 3);
            Assert.IsFalse(wick.NearLadder, "No ground ladder at x = 0.");
        }
    }
}
