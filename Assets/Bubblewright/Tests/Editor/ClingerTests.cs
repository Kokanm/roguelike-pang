using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>The Clinger sticks to any surface it touches and lets go, after a wobble, when Wick crosses the lane straight out from that surface.</summary>
    public class ClingerTests
    {
        const float Dt = 1f / 60f;

        GameObject root;
        StageMap previousMap;
        Vector2 previousFeet;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("ClingerTests");
            previousMap = Game.Map;
            previousFeet = Game.WickFeet;
            SetMap(StageMap.Flat());
            Game.WickFeet = new Vector2(-10f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            SetMap(previousMap);
            Game.WickFeet = previousFeet;
            Object.DestroyImmediate(root);
        }

        static void SetMap(StageMap map)
        {
            var property = typeof(Game).GetProperty("Map", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(property, "Game.Map must exist.");
            property.SetValue(null, map);
        }

        static StageMap PlatformMap(TileKind kind = TileKind.Solid)
        {
            var map = StageMap.Flat();
            for (int c = 19; c <= 23; c++) map.Set(c, 9, new Cell { kind = kind, hp = 1 });
            return map;
        }

        Bubble Spawn(int tier, Vector2 at, bool child = false)
        {
            return Bubble.Create(root.transform, BubbleKind.Clinger, tier, at, new Vector2(1f, 0f), child);
        }

        static ClingerBehavior Of(Bubble b) => (ClingerBehavior)b.Behavior;

        static void Run(Bubble b, float seconds)
        {
            int n = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < n; i++) b.Tick(Dt);
        }

        [Test]
        public void AttachesToTheArenaCeilingAndStaysStillWhenWickIsFarAway()
        {
            var b = Spawn(2, new Vector2(5f, 9f));
            Assert.AreEqual(StageMap.StoreyRows - b.Radius, b.Position.y, 0.001f);
            Assert.AreEqual(ClingerBehavior.State.Stuck, Of(b).Current);
            var start = b.Position;
            Run(b, Tuning.I.clingerMaxStickTime - 0.5f);
            Assert.AreEqual(start.x, b.Position.x, 0.0001f);
            Assert.AreEqual(start.y, b.Position.y, 0.0001f);
        }

        [Test]
        public void AttachesUnderAPlatformUnlessItIsXl()
        {
            SetMap(PlatformMap());
            var m = Spawn(2, new Vector2(5.5f, 7f));
            Assert.AreEqual(9f - m.Radius, m.Position.y, 0.001f);
            var xl = Spawn(0, new Vector2(5.5f, 7f));
            Assert.AreEqual(StageMap.StoreyRows - xl.Radius, xl.Position.y, 0.001f);
        }

        [Test]
        public void WobblesFor0Point4SecondsThenDropsSidewaysAtFullSpeed()
        {
            SetMap(PlatformMap());
            var b = Spawn(2, new Vector2(5.5f, 7f));
            float hangY = b.Position.y;
            Game.WickFeet = new Vector2(5.5f + 1.4f, 0f);
            b.Tick(Dt);
            Assert.AreEqual(ClingerBehavior.State.Wobbling, Of(b).Current);
            Run(b, 0.38f);
            Assert.AreEqual(ClingerBehavior.State.Wobbling, Of(b).Current);
            Assert.AreEqual(hangY, b.Position.y, 0.0001f, "it must not move while it wobbles");
            Run(b, 0.1f);
            Assert.AreEqual(ClingerBehavior.State.Free, Of(b).Current);
            Assert.AreEqual(b.SpeedX * Tuning.I.clingerLetGoSpeedScale, b.Velocity.x, 0.001f);
            Run(b, 0.2f);
            Assert.Less(b.Position.y, hangY, "it falls once dropped");
        }

        [Test]
        public void DoesNotTriggerWhenWickIsTooFarSidewaysOrAbove()
        {
            SetMap(PlatformMap());
            var b = Spawn(2, new Vector2(5.5f, 7f));
            Game.WickFeet = new Vector2(5.5f + 1.6f, 0f);
            Run(b, 1f);
            Assert.AreEqual(ClingerBehavior.State.Stuck, Of(b).Current);
            Game.WickFeet = new Vector2(5.5f, b.Position.y + 1f); // standing above its bottom
            Run(b, 1f);
            Assert.AreEqual(ClingerBehavior.State.Stuck, Of(b).Current);
        }

        [Test]
        public void AfterDroppingItSticksToTheFloorItLandsOn()
        {
            var b = Spawn(2, new Vector2(5f, 9f));
            Game.WickFeet = new Vector2(5f, 0f);
            Run(b, 0.5f);
            Assert.AreEqual(ClingerBehavior.State.Free, Of(b).Current);
            Game.WickFeet = new Vector2(-10f, 0f);
            Run(b, 3f);
            Assert.AreEqual(ClingerBehavior.State.Stuck, Of(b).Current);
            Assert.AreEqual(Vector2.up, Of(b).Normal);
            Assert.AreEqual(b.Radius, b.Position.y, 0.01f);
        }

        Bubble StuckOnFloor(float x)
        {
            var b = Spawn(3, new Vector2(x, 2f), child: true);
            for (int i = 0; i < 5 * 60 && Of(b).Current != ClingerBehavior.State.Stuck; i++) b.Tick(Dt);
            Assert.AreEqual(ClingerBehavior.State.Stuck, Of(b).Current);
            Assert.AreEqual(Vector2.up, Of(b).Normal);
            return b;
        }

        [Test]
        public void OnTheFloorItIgnoresWickWalkingBesideIt()
        {
            var b = StuckOnFloor(5f);
            Game.WickFeet = new Vector2(b.Position.x + 1.2f, 0f);
            Run(b, 1f);
            Assert.AreEqual(ClingerBehavior.State.Stuck, Of(b).Current);
        }

        [Test]
        public void OnTheFloorItLetsGoWhenWickJumpsOverItAndBouncesUp()
        {
            var b = StuckOnFloor(5f);
            Game.WickFeet = new Vector2(b.Position.x, b.Position.y + b.Radius + 0.3f);
            b.Tick(Dt);
            Assert.AreEqual(ClingerBehavior.State.Wobbling, Of(b).Current);
            Run(b, 0.45f);
            Assert.AreEqual(ClingerBehavior.State.Free, Of(b).Current);
            Assert.Greater(b.Velocity.y, 0f, "it leaves the floor with a bounce");
        }

        [Test]
        public void ItLetsGoByItselfAfterTheMaxStickTime()
        {
            var b = StuckOnFloor(5f);
            Game.WickFeet = new Vector2(-10f, 0f);
            Run(b, Tuning.I.clingerMaxStickTime - 0.2f);
            Assert.AreEqual(ClingerBehavior.State.Stuck, Of(b).Current);
            Run(b, 0.3f);
            Assert.AreEqual(ClingerBehavior.State.Wobbling, Of(b).Current);
            Run(b, Tuning.I.clingerWobbleTime + 0.05f);
            Assert.AreEqual(ClingerBehavior.State.Free, Of(b).Current);
            Assert.Greater(b.Velocity.y, 0f, "it leaves the floor with a bounce");
        }

        Bubble StuckOnRightWall()
        {
            var b = Bubble.Create(root.transform, BubbleKind.Clinger, 2, new Vector2(14.8f, 6f), new Vector2(6f, 0f), true);
            for (int i = 0; i < 2 * 60 && Of(b).Current != ClingerBehavior.State.Stuck; i++) b.Tick(Dt);
            Assert.AreEqual(ClingerBehavior.State.Stuck, Of(b).Current);
            Assert.AreEqual(Vector2.left, Of(b).Normal);
            return b;
        }

        [Test]
        public void OnARightWallItLetsGoWhenWickPassesToItsLeft()
        {
            var b = StuckOnRightWall();
            Game.WickFeet = new Vector2(0f, b.Position.y - 0.7f);
            b.Tick(Dt);
            Assert.AreEqual(ClingerBehavior.State.Wobbling, Of(b).Current);
            Run(b, 0.45f);
            Assert.AreEqual(ClingerBehavior.State.Free, Of(b).Current);
            Assert.Less(b.Velocity.x, 0f, "it pushes off the wall, away from it");
        }

        [Test]
        public void OnARightWallItIgnoresWickFarBelowItsLane()
        {
            var b = StuckOnRightWall();
            Game.WickFeet = new Vector2(0f, 0f);
            Run(b, 1f);
            Assert.AreEqual(ClingerBehavior.State.Stuck, Of(b).Current);
        }

        [Test]
        public void ItDoesNotHangUnderAPlatformTooLowForWickToWalkUnder()
        {
            var map = StageMap.Flat();
            for (int c = 10; c <= 22; c++) map.Set(c, 4, new Cell { kind = TileKind.Solid });
            SetMap(map);
            var l = Spawn(1, new Vector2(0f, 2f)); // an L under row 4 would leave 1.5 units, less than the clearance
            Assert.AreEqual(StageMap.StoreyRows - l.Radius, l.Position.y, 0.001f);
            var m = Spawn(2, new Vector2(1f, 2f)); // an M leaves 2.4 units, enough
            Assert.AreEqual(4f - m.Radius, m.Position.y, 0.001f);
        }

        [Test]
        public void AfterTheStormItNeverSticksAgain()
        {
            var b = Spawn(2, new Vector2(5f, 9f));
            b.OnStorm();
            for (int i = 0; i < 5 * 60; i++)
            {
                b.Tick(Dt);
                Assert.AreEqual(ClingerBehavior.State.Free, Of(b).Current);
            }
        }

        [Test]
        public void TheStormDropsAStuckClingerAtOnce()
        {
            var b = Spawn(1, new Vector2(5f, 9f));
            Assert.AreEqual(ClingerBehavior.State.Stuck, Of(b).Current);
            b.OnStorm();
            Assert.AreEqual(ClingerBehavior.State.Free, Of(b).Current);
        }

        [Test]
        public void AChildIsFreeFromTheStartAndSticksToTheFirstSurfaceItTouches()
        {
            var b = Spawn(2, new Vector2(5f, 4f), child: true);
            Assert.AreEqual(ClingerBehavior.State.Free, Of(b).Current);
            Assert.AreEqual(4f, b.Position.y, 0.0001f);
            Run(b, 3f);
            Assert.AreEqual(ClingerBehavior.State.Stuck, Of(b).Current);
        }

        [Test]
        public void AStuckClingerCanBeShotAndHurts()
        {
            var b = Spawn(2, new Vector2(5f, 9f));
            Assert.IsTrue(b.CanBeHit);
            Assert.IsTrue(b.CanHurt);
        }

        [Test]
        public void DropsWhenItsBreakableTileIsShotAway()
        {
            var map = PlatformMap(TileKind.Breakable);
            SetMap(map);
            var b = Spawn(2, new Vector2(5.5f, 7f));
            b.Tick(Dt);
            Assert.AreEqual(ClingerBehavior.State.Stuck, Of(b).Current);
            map.Set(21, 9, new Cell { kind = TileKind.Empty });
            b.Tick(Dt);
            Assert.AreEqual(ClingerBehavior.State.Free, Of(b).Current);
        }

        Bubble Mounted(int tier, Vector2 at, ClingerMount mount, bool patient, bool pogo = false)
        {
            return Bubble.Create(root.transform, BubbleKind.Clinger, tier, at, new Vector2(1f, 0f), child: false, pogo: pogo,
                mount: mount, patient: patient);
        }

        [Test]
        public void PatientFloorClingerPogoWaitsPastMaxStickTimeThenLeapsWhenWickApproaches()
        {
            var b = Mounted(2, new Vector2(5f, 1f), ClingerMount.Floor, patient: true, pogo: true);
            Assert.AreEqual(b.Radius, b.Position.y, 0.001f);
            Assert.AreEqual(Vector2.up, Of(b).Normal);
            Assert.AreEqual(ClingerBehavior.State.Stuck, Of(b).Current);

            Game.WickFeet = new Vector2(-10f, 0f);
            Run(b, Tuning.I.clingerMaxStickTime + 3f);
            Assert.AreEqual(ClingerBehavior.State.Stuck, Of(b).Current, "a patient Clinger ignores the max stick time");

            Game.WickFeet = new Vector2(5f - 1.5f, 0f);
            b.Tick(Dt);
            Assert.AreEqual(ClingerBehavior.State.Wobbling, Of(b).Current);
            Run(b, Tuning.I.clingerWobbleTime + 0.05f);
            Assert.AreEqual(ClingerBehavior.State.Free, Of(b).Current);
            Assert.Greater(b.Velocity.y, 0f, "it leaps up");

            // After it lets go once, every later stick uses the normal rule.
            Game.WickFeet = new Vector2(-10f, 0f);
            for (int i = 0; i < 5 * 60 && Of(b).Current != ClingerBehavior.State.Stuck; i++) b.Tick(Dt);
            Assert.AreEqual(ClingerBehavior.State.Stuck, Of(b).Current);
            Run(b, Tuning.I.clingerMaxStickTime + 0.1f);
            Assert.AreNotEqual(ClingerBehavior.State.Stuck, Of(b).Current, "the second stick times out as usual");
        }

        [Test]
        public void WallMountedClingersAttachToLeftAndRightSurfacesAndWaitWhenPatient()
        {
            var map = StageMap.Flat();
            for (int r = 5; r <= 7; r++) map.Set(25, r, new Cell { kind = TileKind.Solid }); // a pillar whose left face is x = 9
            SetMap(map);

            var left = Mounted(2, new Vector2(-10f, 6f), ClingerMount.LeftWall, patient: true);
            Assert.AreEqual(-16f + left.Radius, left.Position.x, 0.001f, "flush against the arena's left wall");
            Assert.AreEqual(6f, left.Position.y, 0.001f);
            Assert.AreEqual(Vector2.right, Of(left).Normal);

            var right = Mounted(2, new Vector2(5f, 6f), ClingerMount.RightWall, patient: true);
            Assert.AreEqual(9f - right.Radius, right.Position.x, 0.001f, "flush against the pillar's left face");
            Assert.AreEqual(Vector2.left, Of(right).Normal);
            Assert.IsTrue(Of(right).OnTile);

            var arenaRight = Mounted(2, new Vector2(5f, 10f), ClingerMount.RightWall, patient: false);
            Assert.AreEqual(16f - arenaRight.Radius, arenaRight.Position.x, 0.001f, "no tile in its row: the arena wall");

            var tileLeft = Mounted(2, new Vector2(12f, 6f), ClingerMount.LeftWall, patient: false);
            Assert.AreEqual(10f + tileLeft.Radius, tileLeft.Position.x, 0.001f, "flush against the pillar's right face");
            Assert.AreEqual(Vector2.right, Of(tileLeft).Normal);

            Game.WickFeet = new Vector2(0f, 0f); // far below their lanes
            for (int i = 0; i < Mathf.RoundToInt((Tuning.I.clingerMaxStickTime + 1f) / Dt); i++)
            {
                left.Tick(Dt);
                right.Tick(Dt);
            }
            Assert.AreEqual(ClingerBehavior.State.Stuck, Of(left).Current);
            Assert.AreEqual(ClingerBehavior.State.Stuck, Of(right).Current);

            left.OnStorm();
            right.OnStorm();
            Assert.AreEqual(ClingerBehavior.State.Free, Of(left).Current);
            Assert.AreEqual(ClingerBehavior.State.Free, Of(right).Current);
        }

        [Test]
        public void ANonPatientMountedClingerStillLetsGoAfterTheMaxStickTime()
        {
            var floor = Mounted(2, new Vector2(5f, 1f), ClingerMount.Floor, patient: false);
            var wall = Mounted(2, new Vector2(-10f, 6f), ClingerMount.LeftWall, patient: false);
            Game.WickFeet = new Vector2(-10f, 0f);
            Run(floor, Tuning.I.clingerMaxStickTime + 0.1f);
            Run(wall, Tuning.I.clingerMaxStickTime + 0.1f);
            Assert.AreEqual(ClingerBehavior.State.Wobbling, Of(floor).Current);
            Assert.AreEqual(ClingerBehavior.State.Wobbling, Of(wall).Current);
        }

        [Test]
        public void APatientFloorClingerOnABreakableTileDropsWhenTheTileIsShotAway()
        {
            var map = PlatformMap(TileKind.Breakable);
            SetMap(map);
            var b = Mounted(2, new Vector2(5.5f, 11f), ClingerMount.Floor, patient: true);
            Assert.AreEqual(10f + b.Radius, b.Position.y, 0.001f);
            Assert.IsTrue(Of(b).OnTile);
            Run(b, Tuning.I.clingerMaxStickTime + 1f);
            Assert.AreEqual(ClingerBehavior.State.Stuck, Of(b).Current);
            map.Set(21, 9, new Cell { kind = TileKind.Empty });
            b.Tick(Dt);
            Assert.AreEqual(ClingerBehavior.State.Free, Of(b).Current);
        }
    }
}
