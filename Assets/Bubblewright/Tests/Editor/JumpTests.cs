using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    public class JumpTests
    {
        const float Dt = 1f / 60f;

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

        GameObject root;
        Wick wick;
        StageMap previousMap;
        StageMap map;

        [SetUp]
        public void SetUp()
        {
            previousMap = Game.Map;
            map = StageMap.Flat();
            SetMap(map);
            root = new GameObject("JumpTestRoot");
            var fx = Effects.Create(root.transform);
            wick = Wick.Create(root.transform, fx);
        }

        [TearDown]
        public void TearDown()
        {
            SetMap(previousMap);
            Object.DestroyImmediate(root);
        }

        void UseMap(StageMap m)
        {
            map = m;
            SetMap(m);
        }

        static void SetMap(StageMap m)
        {
            var property = typeof(Game).GetProperty("Map", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(property, "Game.Map must exist.");
            property.SetValue(null, m);
        }

        float X => wick.transform.position.x;

        void Tick(float move = 0f, bool jump = false, float climb = 0f) => wick.Tick(move, climb, jump, Dt, true);

        void Step(float seconds, float move = 0f, float climb = 0f)
        {
            int ticks = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < ticks; i++) Tick(move, false, climb);
        }

        /// <summary>Ticks until he is back on the ground (or the limit runs out). Returns seconds taken.</summary>
        float StepUntilLanded(float move, ref float peak, float limit = 2f)
        {
            float t = 0f;
            while (t < limit)
            {
                Tick(move);
                t += Dt;
                peak = Mathf.Max(peak, wick.FeetY);
                if (wick.Mode == Mode.Ground) break;
            }
            return t;
        }

        static StageMap FlatWithGap(int firstCol, int width)
        {
            var m = StageMap.Flat();
            for (int c = firstCol; c < firstCol + width; c++) m.SetGap(c, true);
            return m;
        }

        [Test]
        public void StandingJumpPeaksNearOnePointNineAndLandsAfterAboutPointFiveSixSeconds()
        {
            wick.ResetForStage(0f, 0f, 3);
            Step(0.2f);

            Tick(jump: true);
            Assert.IsTrue(wick.Jumping, "the press starts a jump");
            float peak = wick.FeetY;
            float air = Dt + StepUntilLanded(0f, ref peak);

            Assert.That(peak, Is.InRange(1.85f, 1.95f), "peak height");
            Assert.AreEqual(0.56f, air, 0.04f, "time in the air");
            Assert.AreEqual(Mode.Ground, wick.Mode);
            Assert.IsFalse(wick.Jumping);
            Assert.AreEqual(0f, wick.FeetY, 1e-4f);
        }

        [Test]
        public void RunningJumpCoversAtLeastFourAndAHalfUnitsOnNormalGround()
        {
            Assert.GreaterOrEqual(RunningJumpDistance(), 4.5f);
        }

        [Test]
        public void RunningJumpCoversAtLeastThreePointOneUnitsOnSand()
        {
            WorldRules.PaintAll(map, Surface.Sand);
            Assert.GreaterOrEqual(RunningJumpDistance(), 3.1f);
        }

        float RunningJumpDistance()
        {
            wick.ResetForStage(-13f, 0f, 3);
            Step(1f, move: 1f);
            float start = X;
            Tick(1f, jump: true);
            Assert.IsTrue(wick.Jumping);
            float peak = 0f;
            StepUntilLanded(1f, ref peak);
            Assert.AreEqual(Mode.Ground, wick.Mode);
            return X - start;
        }

        [Test]
        public void JumpUnderPlatformRowANeverReachesIt()
        {
            for (int c = 0; c < StageMap.Cols; c++) map.Set(c, 4, new Cell { kind = TileKind.Solid });
            wick.ResetForStage(0f, 0f, 3);
            Step(0.2f);

            float highest = 0f;
            for (int i = 0; i < 180; i++) // keep pressing for three seconds
            {
                Tick(jump: true);
                highest = Mathf.Max(highest, wick.FeetY);
            }
            Step(1f);

            Assert.LessOrEqual(highest, 2.0f, "feet never above y = 2");
            Assert.AreEqual(Mode.Ground, wick.Mode);
            Assert.AreEqual(0f, wick.FeetY, 1e-4f, "he ends on the ground, not on the platform");
        }

        /// <summary>Platform A from the left wall to x = 0; he stands on it and walks right off its end.</summary>
        void WalkOffLedge()
        {
            for (int c = 0; c < 16; c++) map.Set(c, 4, new Cell { kind = TileKind.Solid });
            wick.ResetForStage(-3f, 5f, 3);
            Step(0.2f);
            for (int i = 0; i < 120 && wick.Mode == Mode.Ground; i++) Tick(1f);
            Assert.AreEqual(Mode.Fall, wick.Mode, "walked off the end");
            Assert.IsFalse(wick.Jumping);
        }

        [Test]
        public void PressJustAfterWalkingOffAnEdgeStillJumps()
        {
            WalkOffLedge();
            Step(0.05f - Dt, move: 1f);
            Tick(1f, jump: true);
            Assert.IsTrue(wick.Jumping, "coyote time");
            Assert.Greater(wick.VelocityY, 0f);
        }

        [Test]
        public void PressLongAfterWalkingOffAnEdgeDoesNotJump()
        {
            WalkOffLedge();
            Step(0.2f - Dt, move: 1f);
            Tick(1f, jump: true);
            Assert.IsFalse(wick.Jumping, "too late for coyote time");

            for (int i = 0; i < 120 && wick.Mode != Mode.Ground; i++)
            {
                Tick(1f);
                Assert.IsFalse(wick.Jumping);
            }
            Assert.AreEqual(Mode.Ground, wick.Mode);
            Step(0.1f, move: 1f);
            Assert.IsFalse(wick.Jumping, "the late press is not buffered into a jump on landing");
        }

        [Test]
        public void PressJustBeforeLandingJumpsOnLanding()
        {
            wick.ResetForStage(0f, 0f, 3);
            Step(0.2f);

            // Count the ticks of one jump, then replay it with a second press 5 ticks (0.083 s) before it lands.
            Tick(jump: true);
            int ticks = 1;
            while (wick.Mode != Mode.Ground && ticks < 120) { Tick(); ticks++; }
            Assert.AreEqual(Mode.Ground, wick.Mode);
            Step(0.3f);

            Tick(jump: true);
            for (int i = 1; i < ticks - 5; i++) Tick();
            Assert.AreEqual(Mode.Fall, wick.Mode, "still in the air when pressing");
            Assert.Less(wick.VelocityY, 0f, "falling when pressing");
            Tick(jump: true);

            bool landed = false, jumpedAgain = false;
            for (int i = 0; i < 12; i++)
            {
                Tick();
                if (wick.FeetY <= 1e-4f) landed = true;
                if (landed && wick.Jumping && wick.VelocityY > 0f) jumpedAgain = true;
            }
            Assert.IsTrue(landed, "he touched down");
            Assert.IsTrue(jumpedAgain, "the buffered press jumps again on landing");
        }

        [Test]
        public void CannotJumpFromALadder()
        {
            UseMap(StageLayout.Parse(Tower));
            wick.ResetForStage(StageMap.CenterX(6), 0f, 3);
            Step(0.4f, climb: 1f);
            Assert.AreEqual(Mode.Climb, wick.Mode);

            Tick(jump: true);
            Step(0.2f);
            Assert.AreEqual(Mode.Climb, wick.Mode);
            Assert.IsFalse(wick.Jumping);
        }

        [Test]
        public void WalkingIntoATwoColumnGapCostsOneHeartAndReturnsHimToTheSideHeCameFrom()
        {
            // Columns 20 and 21 cover x from 4 to 6.
            UseMap(FlatWithGap(20, 2));
            wick.ResetForStage(0f, 0f, 3);
            Step(0.5f);

            bool fell = false;
            for (int i = 0; i < 180 && !fell; i++)
            {
                Tick(1f);
                fell = wick.ConsumeGapFall();
            }

            Assert.IsTrue(fell, "he fell into the gap");
            Assert.AreEqual(2, wick.Hearts, "exactly one heart");
            Assert.AreEqual(Mode.Ground, wick.Mode);
            Assert.AreEqual(0f, wick.FeetY, 1e-4f);
            Assert.Less(X + Tuning.I.feetWidth * 0.5f, 4f, "both feet on solid ground on the left side");
            Assert.IsTrue(wick.Invulnerable, "normal safe time after the fall");
        }

        [Test]
        public void FallingIntoAGapCostsAHeartEvenWhileSafe()
        {
            UseMap(FlatWithGap(20, 2));
            wick.ResetForStage(0f, 0f, 3);
            Step(0.5f);
            wick.Hurt(-10f); // now safe, and knocked a little right
            Assert.AreEqual(2, wick.Hearts);
            Assert.IsTrue(wick.Invulnerable);

            bool fell = false;
            for (int i = 0; i < 180 && !fell; i++)
            {
                Tick(1f);
                fell = wick.ConsumeGapFall();
            }
            Assert.IsTrue(fell);
            Assert.AreEqual(1, wick.Hearts);
        }

        [Test]
        public void GapDoesNotHurtWhenGapDamageIsOff()
        {
            UseMap(FlatWithGap(20, 2));
            wick.GapDamageEnabled = false;
            wick.ResetForStage(0f, 0f, 3);
            Step(0.5f);
            for (int i = 0; i < 180 && X < 4.4f; i++) Tick(1f); // past the edge: both feet over the gap
            Assert.AreEqual(Mode.Fall, wick.Mode);
            Step(0.5f);
            Assert.AreEqual(3, wick.Hearts);
            Assert.AreEqual(0f, wick.FeetY, 1e-4f);
            Assert.Less(X, 4f);
        }

        [Test]
        public void RunningJumpOverATwoColumnGapCostsNothing()
        {
            UseMap(FlatWithGap(20, 2));
            wick.ResetForStage(-6f, 0f, 3);
            Step(0.2f);
            while (X < 3f) Tick(1f);
            Tick(1f, jump: true);
            Assert.IsTrue(wick.Jumping);
            float peak = 0f;
            StepUntilLanded(1f, ref peak);
            Step(0.2f, move: 1f);

            Assert.IsFalse(wick.ConsumeGapFall());
            Assert.AreEqual(3, wick.Hearts);
            Assert.AreEqual(0f, wick.FeetY, 1e-4f);
            Assert.Greater(X, 6f, "landed on the far side");
        }

        [Test]
        public void ABubbleCrossingAGapStillBouncesAtTheFloorLine()
        {
            UseMap(FlatWithGap(20, 2));
            var bubble = Bubble.Create(null, BubbleKind.Gloop, 3, new Vector2(StageMap.CenterX(20) + 0.5f, 1.5f), new Vector2(0f, -10f), false);
            try
            {
                bool bounced = false;
                float lowest = float.MaxValue;
                for (int i = 0; i < 60 && !bounced; i++)
                {
                    bubble.Tick(Dt);
                    lowest = Mathf.Min(lowest, bubble.Position.y);
                    bounced = bubble.Velocity.y > 0f;
                }
                Assert.IsTrue(bounced, "the bubble bounced");
                Assert.GreaterOrEqual(lowest, bubble.Radius - 1e-3f, "it bounced at y = 0, not inside the gap");

                var p = new Vector2(5f, 0.2f);
                Contact contact = map.ResolveCircle(ref p, 0.5f);
                Assert.IsTrue(contact.landed);
                Assert.AreEqual(0.5f, p.y, 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(bubble.gameObject);
            }
        }

        [Test]
        public void GapFloorIsFarBelowAndSurfaceIsUnchanged()
        {
            var m = FlatWithGap(20, 2);
            float x = StageMap.CenterX(20);
            Assert.Less(m.FloorBelow(x, 0f), -Tuning.I.gapFallDepth);
            Assert.AreEqual(0f, m.FloorBelow(StageMap.CenterX(19), 0f), 1e-5f);
            Assert.AreEqual(Surface.Normal, m.SurfaceUnder(x, 0f));
            m.Set(20, 4, new Cell { kind = TileKind.Solid });
            Assert.AreEqual(5f, m.FloorBelow(x, 6f), 1e-5f, "a platform above the gap is still a floor");
        }

        [Test]
        public void ParseReadsTheOptionalGroundRow()
        {
            var rows = new string[15];
            for (int i = 0; i < 14; i++) rows[i] = new string('.', 32);
            rows[14] = "====__====================___===";
            var m = StageLayout.Parse(rows);
            Assert.IsTrue(m.IsGap(4) && m.IsGap(5) && m.IsGap(26) && m.IsGap(28));
            Assert.IsFalse(m.IsGap(3) || m.IsGap(6) || m.IsGap(29));

            rows[14] = "====xx====================___===";
            Assert.Throws<System.ArgumentException>(() => StageLayout.Parse(rows));
        }

        [Test]
        public void PickupsNeverRestOverAGap()
        {
            UseMap(FlatWithGap(20, 2));
            var p = Pickup.Create(root.transform, new Vector2(StageMap.CenterX(20), 3f));
            for (int i = 0; i < 120; i++) p.Tick(Dt, map, new Rect(-100f, -100f, 0.1f, 0.1f));
            Assert.AreEqual(0f, p.Position.y, 1e-4f, "rests on the floor");
            Assert.IsFalse(map.IsGap(StageMap.Col(p.Position.x)), "over a solid column");
        }
    }
}
