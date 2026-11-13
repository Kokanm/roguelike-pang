using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>Rolling balloons: turning at walls, platform ends, gaps; splitting; jump clearance; the generator; the clear rule.</summary>
    public class RollerTests
    {
        const float Dt = 1f / 60f;

        GameObject root;
        StageMap previousMap;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("RollerTests");
            previousMap = Game.Map;
            SetMap(StageMap.Flat());
        }

        [TearDown]
        public void TearDown()
        {
            SetMap(previousMap);
            Object.DestroyImmediate(root);
        }

        static void SetMap(StageMap map)
        {
            var property = typeof(Game).GetProperty("Map", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(property, "Game.Map must exist.");
            property.SetValue(null, map);
        }

        Roller Make(int tier, float x, float surfaceY, int dir) => Roller.Create(root.transform, tier, x, surfaceY, dir);

        static float Half => StageMap.Cols * 0.5f;

        [Test]
        public void BigRollerIsEighteenPixelsAndSmallTwelve()
        {
            var t = Tuning.I;
            Assert.AreEqual(18f, t.rollerDiameters[0] * t.pixelsPerUnit, 0.01f);
            Assert.AreEqual(12f, t.rollerDiameters[1] * t.pixelsPerUnit, 0.01f);
        }

        [Test]
        public void GroundRollerTurnsAtBothWallsAndNeverLeavesTheArena()
        {
            var r = Make(0, 0f, 0f, 1);
            var all = new List<Roller> { r };
            float rad = r.Radius, minX = float.MaxValue, maxX = float.MinValue;
            int turns = 0, dir = r.Direction;
            for (int i = 0; i < 60 * 60; i++)
            {
                r.Tick(Dt, all);
                float x = r.Position.x;
                Assert.GreaterOrEqual(x - rad, -Half - 1e-4f, $"left wall at {i * Dt:0.00} s");
                Assert.LessOrEqual(x + rad, Half + 1e-4f, $"right wall at {i * Dt:0.00} s");
                Assert.AreEqual(rad, r.Position.y, 1e-4f, "stays on the ground");
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                if (r.Direction != dir) { turns++; dir = r.Direction; }
            }
            Assert.Greater(maxX + rad, Half - 0.2f, "reaches the right wall");
            Assert.Less(minX - rad, -Half + 0.2f, "reaches the left wall");
            Assert.GreaterOrEqual(turns, 4);
        }

        /// <summary>Distance from p to the nearest solid tile of the map.</summary>
        static float DistanceToTiles(StageMap map, Vector2 p)
        {
            float best = float.MaxValue;
            for (int c = 0; c < StageMap.Cols; c++)
                for (int row = 0; row < StageMap.Rows; row++)
                {
                    if (!map.IsSolid(c, row)) continue;
                    Rect rect = StageMap.CellRect(c, row);
                    var near = new Vector2(Mathf.Clamp(p.x, rect.xMin, rect.xMax), Mathf.Clamp(p.y, rect.yMin, rect.yMax));
                    best = Mathf.Min(best, (p - near).magnitude);
                }
            return best;
        }

        [Test]
        public void PlatformRollerRollsAroundTheWholePlatformTouchingItAllTheWay()
        {
            var map = StageMap.Flat();
            for (int c = 8; c <= 23; c++) map.Set(c, 4, new Cell { kind = TileKind.Solid }); // row 4: y 4 to 5
            SetMap(map);
            float left = StageMap.CellRect(8, 4).xMin, right = StageMap.CellRect(23, 4).xMax;

            foreach (int startDir in new[] { 1, -1 })
            {
                var r = Make(0, 0f, 5f, startDir);
                var all = new List<Roller> { r };
                float rad = r.Radius;
                bool top = false, underside = false, leftEnd = false, rightEnd = false;
                for (int i = 0; i < 60 * 60; i++)
                {
                    r.Tick(Dt, all);
                    Assert.IsFalse(r.Falling, $"never falls ({i * Dt:0.00} s)");
                    Assert.IsFalse(r.OnGround, "stays on the platform");
                    Vector2 p = r.Position;
                    Assert.GreaterOrEqual(p.x - rad, -Half - 1e-3f);
                    Assert.LessOrEqual(p.x + rad, Half + 1e-3f);
                    float gap = DistanceToTiles(map, p);
                    Assert.AreEqual(rad, gap, 0.03f, $"touches the platform at {i * Dt:0.00} s, position {p}");
                    if (p.y > 5f + rad - 0.05f) top = true;
                    if (p.y < 4f - rad + 0.05f) underside = true;
                    if (p.x < left - rad + 0.1f && p.y > 4.2f && p.y < 4.8f) leftEnd = true;
                    if (p.x > right + rad - 0.1f && p.y > 4.2f && p.y < 4.8f) rightEnd = true;
                }
                Assert.IsTrue(top, "rolls along the top");
                Assert.IsTrue(underside, "rolls along the underside");
                Assert.IsTrue(leftEnd, "rolls around the left end");
                Assert.IsTrue(rightEnd, "rolls around the right end");
                Object.DestroyImmediate(r.gameObject);
            }
        }

        [Test]
        public void PlatformRollerGoesClockwiseOverTheTopWhenRollingRight()
        {
            var map = StageMap.Flat();
            for (int c = 8; c <= 23; c++) map.Set(c, 4, new Cell { kind = TileKind.Solid });
            SetMap(map);
            var r = Make(0, 0f, 5f, 1);
            var all = new List<Roller> { r };
            float x = r.Position.x;
            for (int i = 0; i < 30; i++) r.Tick(Dt, all);
            Assert.Greater(r.Position.x, x, "right along the top");
            Assert.AreEqual(Vector2.up.y, r.Normal.y, 1e-3f);
        }

        [Test]
        public void PlatformRollerTurnsAtAnArenaWall()
        {
            var map = StageMap.Flat();
            for (int c = 0; c <= 12; c++) map.Set(c, 4, new Cell { kind = TileKind.Solid }); // touches the left wall
            SetMap(map);
            var r = Make(0, -12f, 5f, -1);
            var all = new List<Roller> { r };
            float rad = r.Radius;
            for (int i = 0; i < 60 * 30; i++)
            {
                r.Tick(Dt, all);
                Assert.GreaterOrEqual(r.Position.x - rad, -Half - 1e-3f, "never leaves the arena");
                Assert.IsFalse(r.Falling);
            }
        }

        [Test]
        public void RollerTurnsAtAGapEdge()
        {
            var map = StageMap.Flat();
            map.SetGap(20, true);
            map.SetGap(21, true);
            SetMap(map);
            float edge = StageMap.CellRect(20, 0).xMin;

            var r = Make(0, StageMap.CenterX(14), 0f, 1);
            var all = new List<Roller> { r };
            bool turned = false;
            for (int i = 0; i < 60 * 5 && !turned; i++)
            {
                r.Tick(Dt, all);
                Assert.LessOrEqual(r.Position.x + r.Radius, edge + 1e-4f, "never rolls over the gap");
                if (r.Direction < 0)
                {
                    turned = true;
                    Assert.Greater(r.Position.x + r.Radius, edge - 0.2f, "turns right at the edge");
                }
            }
            Assert.IsTrue(turned);
        }

        [Test]
        public void RollerFallsWhenItsTilesAreDestroyedAndCarriesOn()
        {
            var map = StageMap.Flat();
            for (int c = 10; c <= 20; c++) map.Set(c, 4, new Cell { kind = TileKind.Solid });
            SetMap(map);
            var r = Make(0, StageMap.CenterX(15), 5f, 1);
            var all = new List<Roller> { r };
            // Two tiles are wider than the ball, so it has nothing to rest on and drops through.
            map.Set(14, 4, new Cell { kind = TileKind.Empty });
            map.Set(15, 4, new Cell { kind = TileKind.Empty });
            map.Set(16, 4, new Cell { kind = TileKind.Empty });
            r.Tick(Dt, all);
            Assert.IsTrue(r.Falling);
            for (int i = 0; i < 120 && r.Falling; i++) r.Tick(Dt, all);
            Assert.IsFalse(r.Falling);
            Assert.IsTrue(r.OnGround);
            Assert.AreEqual(r.Radius, r.Position.y, 1e-4f, "lands on the ground");
            float x = r.Position.x;
            r.Tick(Dt, all);
            Assert.AreNotEqual(x, r.Position.x, "rolls on");
        }

        [Test]
        public void TwoRollersTurnWhenTheyMeet()
        {
            var a = Make(0, -3f, 0f, 1);
            var b = Make(0, 3f, 0f, -1);
            var all = new List<Roller> { a, b };
            for (int i = 0; i < 120; i++)
            {
                a.Tick(Dt, all);
                b.Tick(Dt, all);
                Assert.GreaterOrEqual(b.Position.x - a.Position.x, a.Radius + b.Radius - 0.1f, "they never pass through each other");
            }
            Assert.AreEqual(-1, a.Direction);
            Assert.AreEqual(1, b.Direction);
        }

        [Test]
        public void PoppingABigRollerLeavesTwoSmallOnesRollingApart()
        {
            var big = Make(0, 2f, 0f, 1);
            var kids = Roller.Split(big, root.transform);
            Assert.AreEqual(2, kids.Count);
            foreach (var k in kids)
            {
                Assert.AreEqual(1, k.Tier);
                Assert.AreEqual(2f, k.Position.x, 1e-4f, "from the same spot");
                Assert.AreEqual(k.Radius, k.Position.y, 1e-4f, "on the same surface");
            }
            Assert.AreEqual(-1, kids[0].Direction * kids[1].Direction, "opposite directions");

            float x0 = kids[0].Position.x, x1 = kids[1].Position.x;
            for (int i = 0; i < 30; i++)
                foreach (var k in kids) k.Tick(Dt, kids);
            Assert.AreEqual(Mathf.Sign(kids[0].Direction), Mathf.Sign(kids[0].Position.x - x0));
            Assert.AreEqual(Mathf.Sign(kids[1].Direction), Mathf.Sign(kids[1].Position.x - x1));

            Assert.AreEqual(0, Roller.Split(kids[0], root.transform).Count, "a small one leaves nothing");
        }

        // ------------------------------------------------------------------ jump clearance

        /// <summary>
        /// Wick runs right and a big roller rolls left toward him. He presses jump jumpAt seconds in. True when the roller
        /// passes under him without ever touching his hitbox.
        /// </summary>
        bool ClearsWhenJumpingAt(Wick wick, float jumpAt)
        {
            wick.ResetForStage(-8f, 0f, 3);
            var r = Make(0, 8f, 0f, -1);
            var all = new List<Roller> { r };
            bool jumped = false, pressed = false, clear = true;
            for (int i = 0; i < 60 * 3; i++)
            {
                float t = i * Dt;
                bool press = !pressed && t >= jumpAt;
                pressed |= press;
                wick.Tick(1f, 0f, press, Dt, true);
                jumped |= wick.Jumping;
                r.Tick(Dt, all);
                if (r.Overlaps(wick.Hitbox)) { clear = false; break; }
                if (r.Position.x + r.Radius < wick.Hitbox.xMin && wick.Mode == Mode.Ground) break; // passed under him
            }
            Object.DestroyImmediate(r.gameObject);
            return clear && jumped;
        }

        [Test]
        public void AJumpClearsABigRollerWithATimingWindowOfAtLeastATenthOfASecond()
        {
            var fx = Effects.Create(root.transform);
            var wick = Wick.Create(root.transform, fx);
            Assert.Greater(Tuning.I.jumpHeight, Tuning.I.rollerDiameters[0], "a jump is higher than a big roller");

            int run = 0, longest = 0;
            for (int i = 0; i <= 80; i++)
            {
                if (ClearsWhenJumpingAt(wick, i * 0.02f)) run++;
                else run = 0;
                longest = Mathf.Max(longest, run);
            }
            Assert.GreaterOrEqual(longest, 5, $"at least 5 consecutive jump start times (0.02 s apart) clear it; longest run {longest}");
        }

        // ------------------------------------------------------------------ generator

        [Test]
        public void GeneratorPlacesRollersOnlyWhereAllowed()
        {
            var t = Tuning.I;
            int placed = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                for (int stage = 1; stage <= t.RunLength; stage++)
                {
                    var plan = StageGenerator.Generate(stage, new System.Random(seed * 31 + stage));
                    int world = Run.WorldOf(stage);
                    string where = $"seed {seed} stage {stage}";
                    int pearl = 0, steel = 0;
                    foreach (var ro in plan.rollers) { if (ro.steel) steel++; else pearl++; }
                    if (world < t.rollerFromWorld || world == 1) Assert.AreEqual(0, pearl, $"{where}: no rollers in world 1");
                    if (world < t.steelFromWorld) Assert.AreEqual(0, steel, $"{where}: no steel before world {t.steelFromWorld}");
                    if (Run.IsBossStageNumber(stage)) Assert.AreEqual(0, plan.rollers.Count, $"{where}: no rollers on a boss stage");
                    Assert.LessOrEqual(pearl, t.rollerMaxPerStage, where);
                    Assert.LessOrEqual(steel, t.steelMaxPerStage, where);

                    foreach (var spec in plan.rollers)
                    {
                        placed++;
                        Assert.AreEqual(0, spec.tier, $"{where}: starts big");
                        Assert.GreaterOrEqual(Mathf.Abs(spec.x - plan.wickX), 6f - 1e-4f, $"{where}: at least 6 units from Wick's start");
                        int c = StageMap.Col(spec.x);
                        int row = spec.surfaceY <= 0.01f ? -1 : Mathf.RoundToInt(spec.surfaceY) - 1;
                        if (row < 0) Assert.IsFalse(plan.map.IsGap(c), $"{where}: on solid ground");
                        else Assert.IsTrue(plan.map.IsSolid(c, row), $"{where}: on a platform");
                        foreach (var strip in plan.map.spikes)
                            if (strip.row == row)
                                Assert.IsFalse(c >= strip.col && c < strip.col + strip.width, $"{where}: not on a spike strip");
                    }
                }
            }
            Assert.Greater(placed, 0, "rollers do appear from world 2 on");
        }

        [Test]
        public void RollerSpotsAvoidSpikesAndWickAndShortStretches()
        {
            var map = StageMap.Flat();
            map.spikes.Add(new StageMap.SpikeStrip { col = 2, row = -1, width = 4 });
            for (int c = 26; c <= 28; c++) map.Set(c, 4, new Cell { kind = TileKind.Solid }); // only 3 tiles long
            foreach (var spot in StageGenerator.RollerSpots(map, 0f))
            {
                int c = StageMap.Col(spot.x);
                Assert.GreaterOrEqual(Mathf.Abs(spot.x), 6f);
                Assert.AreEqual(0f, spot.y, 1e-4f, "the short platform is never used");
                Assert.IsFalse(c >= 1 && c <= 6, "not on the spike strip or right next to it");
            }
        }

        // ------------------------------------------------------------------ clear rule

        [Test]
        public void AStageWithRollersIsNotClearedWhileARollerIsAlive()
        {
            Assert.IsFalse(Game.StageCleared(0, 1), "a roller is left");
            Assert.IsFalse(Game.StageCleared(1, 0), "a bubble is left");
            Assert.IsTrue(Game.StageCleared(0, 0));

            var alive = new List<Roller> { Make(0, 4f, 0f, 1) };
            var popped = alive[0];
            alive.Remove(popped);
            alive.AddRange(Roller.Split(popped, root.transform));
            Assert.IsFalse(Game.StageCleared(0, alive.Count), "the two small ones keep the stage going");
            foreach (var small in alive.ToArray())
            {
                alive.Remove(small);
                alive.AddRange(Roller.Split(small, root.transform));
            }
            Assert.IsTrue(Game.StageCleared(0, alive.Count));
        }
    }
}
