using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>Crouching, wall jumps, the Updraft double jump, and harpoons fired in the air (up and down).</summary>
    public class AirMoveTests
    {
        const float Dt = 1f / 60f;

        // A platform 5 up across the middle, a breakable block on it, and a ladder down from it.
        static readonly string[] Stage =
        {
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "..........####B####T####........",
            "...................H............",
            "...................H............",
            "...................H............",
            "...................H............",
        };

        GameObject root;
        Wick wick;
        StageMap previousMap;
        Tuning T => Tuning.I;

        [SetUp]
        public void SetUp()
        {
            previousMap = Game.Map;
            SetMap(StageMap.Flat());
            root = new GameObject("AirMoveTestRoot");
            var fx = Effects.Create(root.transform);
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

        void Tick(float move = 0f, bool jump = false, float climb = 0f) => wick.Tick(move, climb, jump, Dt, true);

        void Step(float seconds, float move = 0f, float climb = 0f)
        {
            int ticks = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < ticks; i++) Tick(move, false, climb);
        }

        /// <summary>Ticks until he stops rising.</summary>
        void StepToApex(float move = 0f)
        {
            for (int i = 0; i < 120 && wick.VelocityY > 0f; i++) Tick(move);
        }

        /// <summary>Stages start with him facing the camera; play is with his back to it, where the wall pose shows.</summary>
        void FaceAway()
        {
            wick.Turn(false);
            Step(T.turnTime + 0.05f);
            Assert.IsFalse(wick.FacingFront);
        }

        float RightWallX => T.HalfWidth - 11.5f / T.pixelsPerUnit;

        // ---------------------------------------------------------------- Crouch

        [Test]
        public void DownOnTheGroundCrouchesWithAShorterHitboxAndALowerLauncher()
        {
            wick.ResetForStage(0f, 0f, 3);
            Step(0.1f);
            float standingHeight = wick.Hitbox.height;
            float standingTip = wick.LauncherTip.y;

            Step(0.1f, climb: -1f);
            Assert.IsTrue(wick.Crouching);
            Assert.AreEqual(standingHeight * T.crouchHeightScale, wick.Hitbox.height, 1e-4f);
            Assert.Less(wick.LauncherTip.y, standingTip);
            Assert.AreEqual(Mode.Ground, wick.Mode);

            Step(0.1f);
            Assert.IsFalse(wick.Crouching, "letting go of Down stands him up");
            Assert.AreEqual(standingHeight, wick.Hitbox.height, 1e-4f);
        }

        [Test]
        public void CrouchingShufflesSlowly()
        {
            wick.ResetForStage(0f, 0f, 3);
            Step(1f, move: 1f, climb: -1f);
            Assert.IsTrue(wick.Crouching);
            Assert.AreEqual(wick.BaseRunSpeed * T.crouchSpeedScale, wick.VelocityX, 0.01f);
        }

        [Test]
        public void DownAtTheTopOfALadderClimbsInsteadOfCrouching()
        {
            SetMap(StageLayout.Parse(Stage));
            wick.ResetForStage(StageMap.CenterX(19), 5f, 3);
            Step(0.1f);
            Assert.AreEqual(Mode.Ground, wick.Mode);
            Step(0.1f, climb: -1f);
            Assert.AreEqual(Mode.Climb, wick.Mode);
            Assert.IsFalse(wick.Crouching);
        }

        [Test]
        public void JumpingFromACrouchStandsHimUp()
        {
            wick.ResetForStage(0f, 0f, 3);
            Step(0.1f, climb: -1f);
            wick.Tick(0f, -1f, true, Dt, true);
            Assert.IsTrue(wick.Jumping);
            Assert.IsFalse(wick.Crouching);
            Assert.AreEqual(T.hitbox.y, wick.Hitbox.height, 1e-4f);
        }

        // ---------------------------------------------------------------- Wall jump

        [Test]
        public void JumpingAgainstTheArenaWallKicksHimUpAndAway()
        {
            wick.ResetForStage(RightWallX, 0f, 3);
            Step(0.1f, move: 1f);
            Tick(1f, jump: true);
            StepToApex(1f);
            Step(0.1f, move: 1f); // falling, against the wall
            Assert.AreEqual(1, wick.WallSide);
            float y = wick.FeetY;

            Tick(1f, jump: true);
            Assert.IsTrue(wick.Jumping);
            Assert.Greater(wick.VelocityY, 0f, "a wall jump goes up");
            Assert.Less(wick.VelocityX, 0f, "and away from the wall");

            Step(0.1f, move: 1f); // the stick still points at the wall, but he keeps going away for a moment
            Assert.Less(wick.transform.position.x, RightWallX - 0.5f);
            Assert.Greater(wick.FeetY, y);
        }

        [Test]
        public void FallingIntoAWallWhilePushingSlidesSlowly()
        {
            wick.ResetForStage(RightWallX, 0f, 3);
            Step(0.1f, move: 1f);
            Tick(1f, jump: true);
            StepToApex(1f);
            Step(0.25f, move: 1f);
            Assert.IsTrue(wick.WallSliding);
            Assert.GreaterOrEqual(wick.VelocityY, -T.wallSlideSpeed - 1e-3f);
        }

        [Test]
        public void ChainedWallJumpsClimbAShaft()
        {
            // In a shaft between the right arena wall and a column of blocks, he climbs by kicking from side to side.
            var rows = new string[14];
            for (int r = 0; r < 14; r++) rows[r] = "............................#...";
            SetMap(StageLayout.Parse(rows));
            float colLeft = StageMap.CellRect(28, 0).xMax;
            wick.ResetForStage((colLeft + RightWallX) * 0.5f, 0f, 3);
            Step(0.1f);
            Assert.AreEqual(Mode.Ground, wick.Mode);

            // Kick to and fro, always jumping the moment a wall is touched.
            float dir = 1f;
            Tick(dir, jump: true);
            for (int i = 0; i < 600 && wick.FeetY < 6f; i++)
            {
                if (wick.Mode == Mode.Fall && wick.WallSide != 0)
                {
                    dir = -wick.WallSide;
                    Tick(dir, jump: true);
                }
                else Tick(dir);
                if (wick.Mode == Mode.Ground && wick.FeetY < 0.01f) Tick(dir, jump: true);
            }
            Assert.Greater(wick.FeetY, 4.5f, "wall jumps let him climb higher than a jump alone");
        }

        // ---------------------------------------------------------------- Wall pose

        [Test]
        public void TheWallPoseTouchesTheWallOnlyWithAHandAndABoot()
        {
            var sprite = PlaceholderArt.I.SpritesFor(Character.Wick).Cling;
            var tex = sprite.texture;
            var rect = sprite.textureRect;
            int col = (int)rect.x + PlaceholderArt.ClingWallPixel;
            bool hand = false, boot = false;
            for (int y = 0; y < (int)rect.height; y++)
            {
                if (tex.GetPixel(col, (int)rect.y + y).a <= 0f) continue;
                if (y >= 18) hand = true;
                else if (y <= 11) boot = true;
                else Assert.Fail($"something other than the hand or boot reaches the wall at row {y}");
            }
            Assert.IsTrue(hand, "a hand on the wall");
            Assert.IsTrue(boot, "a boot on the wall");
            Assert.AreEqual(PlaceholderArt.ClingWallPixel + 1, (int)rect.width, "the wall column is the sprite's last column");
        }

        [Test]
        public void SlidingDownTheArenaWallShowsTheWallPoseAgainstIt()
        {
            wick.ResetForStage(RightWallX, 0f, 3);
            FaceAway();
            Step(0.1f, move: 1f);
            Tick(1f, jump: true);
            StepToApex(1f);
            Step(0.15f, move: 1f);
            Assert.IsTrue(wick.Clinging);
            Assert.IsTrue(wick.InWallPose);
            Assert.AreEqual(T.HalfWidth, wick.WallPoseContactX, 1.01f / T.pixelsPerUnit, "hand and boot meet the wall");

            Step(0.1f); // let go of the stick
            Assert.IsFalse(wick.InWallPose);
        }

        [Test]
        public void SlidingDownABlockShowsTheWallPoseAgainstIt()
        {
            var rows = new string[14];
            for (int r = 0; r < 14; r++) rows[r] = "............................#...";
            SetMap(StageLayout.Parse(rows));
            float blockRight = StageMap.CellRect(28, 0).xMax;
            wick.ResetForStage(blockRight + 1.5f, 0f, 3);
            FaceAway();
            Step(0.1f);
            Tick(-1f, jump: true);
            StepToApex(-1f);
            Step(0.15f, move: -1f);
            Assert.AreEqual(-1, wick.WallSide);
            Assert.IsTrue(wick.InWallPose);
            Assert.AreEqual(blockRight, wick.WallPoseContactX, 1.01f / T.pixelsPerUnit, "hand and boot meet the block's side");
        }

        [Test]
        public void AWallJumpPushesOffInTheWallPoseThenLetsGo()
        {
            wick.ResetForStage(RightWallX, 0f, 3);
            FaceAway();
            Step(0.1f, move: 1f);
            Tick(1f, jump: true);
            StepToApex(1f);
            Step(0.1f, move: 1f);
            Tick(1f, jump: true);
            Assert.IsTrue(wick.InWallPose, "the push-off starts from the wall");
            Assert.AreEqual(T.HalfWidth, wick.WallPoseContactX, 1.01f / T.pixelsPerUnit);
            Step(0.2f, move: 1f);
            Assert.IsFalse(wick.InWallPose, "then he flies off in the normal pose");
        }

        [Test]
        public void ASecondPressInTheOpenAirDoesNothingWithoutUpdraft()
        {
            wick.ResetForStage(0f, 0f, 3);
            wick.AirJumps = 0;
            Step(0.1f);
            Tick(jump: true);
            StepToApex();
            Step(0.05f);
            float vy = wick.VelocityY;
            Tick(jump: true);
            Assert.Less(wick.VelocityY, vy + 1e-4f, "no second jump");
        }

        // ---------------------------------------------------------------- Double jump

        [Test]
        public void UpdraftGivesOneJumpInTheAirThatComesBackOnLanding()
        {
            wick.AirJumps = 1;
            wick.ResetForStage(0f, 0f, 3);
            Step(0.1f);
            Tick(jump: true);
            StepToApex();
            Tick(jump: true);
            Assert.AreEqual(T.AirJumpSpeed, wick.VelocityY, 1f, "the double jump starts at full air jump speed");
            Assert.AreEqual(0, wick.AirJumpsLeft);

            float peak = wick.FeetY;
            for (int i = 0; i < 120 && wick.VelocityY > 0f; i++)
            {
                Tick();
                peak = Mathf.Max(peak, wick.FeetY);
            }
            Assert.That(peak, Is.InRange(T.jumpHeight + T.airJumpHeight - 0.15f, T.jumpHeight + T.airJumpHeight + 0.1f));
            Assert.Less(peak, 5f, "two jumps still do not reach a platform");

            float vy = wick.VelocityY;
            Tick(jump: true);
            Assert.Less(wick.VelocityY, vy + 1e-4f, "only one double jump");

            for (int i = 0; i < 240 && wick.Mode != Mode.Ground; i++) Tick();
            Step(0.2f); // past the buffer, so the press above does not count
            Assert.AreEqual(Mode.Ground, wick.Mode);
            Assert.AreEqual(1, wick.AirJumpsLeft);
        }

        [Test]
        public void UpdraftUpgradeSetsAirJumps()
        {
            var run = new Run(1);
            Assert.AreEqual(0, run.AirJumps);
            Upgrade.ByName("Updraft").Apply(run);
            Assert.AreEqual(1, run.AirJumps);
            Assert.AreEqual(Rarity.Rare, Upgrade.ByName("Updraft").Rarity);
        }

        // ---------------------------------------------------------------- Crouch tunnels

        static readonly string[] Tunnel =
        {
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "..............#####.............",
            "................................",
        };

        static readonly string[] Slot =
        {
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "..............##B.#.............",
            "................................",
        };

        [Test]
        public void CrouchingLetsWickShuffleThroughAOneRowTunnelAndLocksCrouchUntilClear()
        {
            SetMap(StageLayout.Parse(Tunnel));
            wick.ResetForStage(StageMap.CenterX(10), 0f, 3);
            Step(0.1f);
            for (int i = 0; i < 120; i++) Tick(1f);
            Assert.Less(wick.transform.position.x, StageMap.CellRect(14, 0).xMin, "standing, he is blocked at the entrance");
            Assert.IsFalse(wick.Crouching);

            for (int i = 0; i < 600 && wick.transform.position.x < StageMap.CenterX(15); i++) Tick(1f, climb: -1f);
            Assert.GreaterOrEqual(wick.transform.position.x, StageMap.CenterX(15), "crouched, he shuffles in");

            Tick(0f, climb: 0f);
            Assert.IsTrue(wick.Crouching, "releasing Down inside keeps him crouched");
            Tick(0f, jump: true);
            Assert.IsFalse(wick.Jumping, "no jumping into the roof");
            Assert.AreEqual(Mode.Ground, wick.Mode);

            for (int i = 0; i < 900 && wick.transform.position.x < StageMap.CenterX(20); i++) Tick(1f);
            Assert.Greater(wick.transform.position.x, StageMap.CellRect(18, 0).xMax);
            Tick(0f);
            Assert.IsFalse(wick.Crouching, "he stands up once clear");
        }

        [Test]
        public void CrouchedInAOneRowTunnelCanFireUpThroughASlotOrBreakCeilingTile()
        {
            SetMap(StageLayout.Parse(Slot));
            wick.ResetForStage(StageMap.CenterX(15), 0f, 3);
            Step(0.1f, climb: -1f);
            Assert.IsTrue(wick.Crouching);
            Assert.Less(wick.LauncherTip.y, 1f, "the launcher is inside the 1-row tunnel");

            int hitCol = -1, hitRow = -1;
            var h = Harpoon.Create(root.transform, (c, r) => { hitCol = c; hitRow = r; });
            h.HoldTime = 0f;
            h.Fire(new Vector2(StageMap.CenterX(16), wick.LauncherTip.y));
            for (int i = 0; i < 120 && h.Active; i++) h.Tick(Dt);
            Assert.AreEqual(16, hitCol);
            Assert.AreEqual(1, hitRow);

            h.Fire(new Vector2(StageMap.CenterX(17), wick.LauncherTip.y));
            float top = 0f;
            for (int i = 0; i < 120 && h.Active; i++)
            {
                h.Tick(Dt);
                top = Mathf.Max(top, h.TipRect.yMax);
            }
            Assert.Greater(top, 2f, "the cable goes up through the slot");
        }

        [Test]
        public void HurtInsideAOneRowTunnelStaysCrouchedAndNeverOverlapsTheRoof()
        {
            var map = StageLayout.Parse(Tunnel);
            SetMap(map);
            wick.ResetForStage(StageMap.CenterX(15), 0f, 3);
            Step(0.1f, climb: -1f);
            Assert.IsTrue(wick.Crouching);

            wick.Hurt(wick.transform.position.x - 1f, 1);
            for (int i = 0; i < 90; i++)
            {
                Tick(0f);
                Assert.IsTrue(wick.Crouching, "knockback cannot stand him up under the roof");
                Assert.IsFalse(map.OverlapsSolid(wick.Hitbox), "never inside a solid tile");
            }
        }

        // ---------------------------------------------------------------- Shots in the air

        [Test]
        public void AnUpShotInTheAirHangsOnlyAsFarAsHisFeet()
        {
            var h = Harpoon.Create(root.transform);
            var from = new Vector2(0f, 3f + T.launcherTip.y);
            h.Fire(from);
            Assert.AreEqual(3f, h.CableRect.yMin, 0.2f, "the cable starts near his feet, not on the floor");
        }

        [Test]
        public void ADownShotFliesToTheFloorAndHitsWhatIsBelow()
        {
            var h = Harpoon.Create(root.transform);
            h.HoldTime = 0f;
            h.FireDown(new Vector2(0f, 4f));
            Assert.IsTrue(h.Down);
            var below = new Rect(-0.3f, 1.5f, 0.6f, 0.6f);
            var above = new Rect(-0.3f, 5f, 0.6f, 0.6f);
            bool hitBelow = false;
            for (int i = 0; i < 60 && h.Active; i++)
            {
                h.Tick(Dt);
                hitBelow |= h.Hits(below);
                Assert.IsFalse(h.Hits(above), "a down shot never reaches above where it was fired");
                Assert.GreaterOrEqual(h.TipRect.yMin, -1e-4f, "never through the floor");
            }
            Assert.IsTrue(hitBelow);
            Assert.IsFalse(h.Active, "gone once it reaches the floor without Anchor Tip");
        }

        [Test]
        public void ADownShotStopsOnAPlatformAndBreaksABreakableTile()
        {
            SetMap(StageLayout.Parse(Stage));
            int hitCol = -1, hitRow = -1;
            var h = Harpoon.Create(root.transform, (c, r) => { hitCol = c; hitRow = r; });
            h.HoldTime = 0f;

            h.FireDown(new Vector2(StageMap.CenterX(12), 8f));
            for (int i = 0; i < 60 && h.Active; i++)
            {
                h.Tick(Dt);
                Assert.GreaterOrEqual(h.TipRect.yMin, 5f - 1e-4f, "stops on the platform top");
            }
            Assert.AreEqual(-1, hitCol, "solid tile: nothing broken");

            h.FireDown(new Vector2(StageMap.CenterX(14), 8f));
            for (int i = 0; i < 60 && h.Active; i++) h.Tick(Dt);
            Assert.AreEqual(14, hitCol);
            Assert.AreEqual(4, hitRow);
        }

        [Test]
        public void AnchorTipHooksADownShotIntoTheFloor()
        {
            var h = Harpoon.Create(root.transform);
            h.HoldTime = 1f;
            h.FireDown(new Vector2(0f, 3f));
            for (int i = 0; i < 30; i++) h.Tick(Dt);
            Assert.IsTrue(h.Active, "hooked");
            for (int i = 0; i < 70; i++) h.Tick(Dt);
            Assert.IsFalse(h.Active, "let go after the hold time");
        }

        [Test]
        public void UpdraftHasItsOwnIconAndFittingText()
        {
            var u = Upgrade.ByName("Updraft");
            Assert.IsNotNull(u);
            Assert.LessOrEqual(u.Text.Length, 66);
        }
    }
}
