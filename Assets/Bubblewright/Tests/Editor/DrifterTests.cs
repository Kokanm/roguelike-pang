using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>The Drifter: constant diagonal flight, reflection off everything, mirrored children, fair starts, arrow art.</summary>
    public class DrifterTests
    {
        const float Dt = 1f / 60f;

        // Row 4 platform (y 4..5) from column 10 to 21. Text rows run top (row 13) to bottom (row 0).
        static readonly string[] Shelf = BuildShelf();

        static string[] BuildShelf()
        {
            var rows = new string[14];
            for (int i = 0; i < 14; i++) rows[i] = new string('.', 32);
            rows[9] = new string('.', 10) + new string('#', 12) + new string('.', 10);
            return rows;
        }

        GameObject root;
        StageMap previousMap;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("DrifterTests");
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

        Bubble Make(int tier, Vector2 pos, int dir, bool child = false) =>
            Bubble.Create(root.transform, BubbleKind.Drifter, tier, pos, new Vector2(dir * Tuning.I.bubbleSpeedX, 0f), child);

        [Test]
        public void SpeedStaysConstantAndItNeverLeavesTheArena()
        {
            var b = Make(1, new Vector2(-3f, 3f), 1);
            float half = Tuning.I.HalfWidth, h = Tuning.I.arenaHeight;
            for (int i = 0; i < 30 * 60; i++)
            {
                b.Tick(Dt);
                Assert.AreEqual(Tuning.I.drifterSpeed, b.Velocity.magnitude, 0.01f, $"tick {i}");
                var p = b.Position;
                Assert.GreaterOrEqual(p.x - b.Radius, -half - 0.01f);
                Assert.LessOrEqual(p.x + b.Radius, half + 0.01f);
                Assert.GreaterOrEqual(p.y - b.Radius, -0.01f);
                Assert.LessOrEqual(p.y + b.Radius, h + 0.01f);
            }
        }

        [Test]
        public void ItReflectsOffFloorCeilingAndBothWalls()
        {
            float half = Tuning.I.HalfWidth, h = Tuning.I.arenaHeight;
            var floor = Make(1, new Vector2(0f, 2f), 1);
            floor.Velocity = new Vector2(1f, -1f).normalized * Tuning.I.drifterSpeed;
            Run(floor, 120);
            Assert.Greater(floor.Velocity.y, 0f, "floor");

            var ceiling = Make(1, new Vector2(0f, h - 2f), 1, true);
            ceiling.Velocity = new Vector2(1f, 1f);
            Run(ceiling, 120);
            Assert.Less(ceiling.Velocity.y, 0f, "ceiling");

            var right = Make(1, new Vector2(half - 2f, 7f), 1);
            Run(right, 120);
            Assert.Less(right.Velocity.x, 0f, "right wall");

            var left = Make(1, new Vector2(-half + 2f, 7f), -1);
            Run(left, 120);
            Assert.Greater(left.Velocity.x, 0f, "left wall");
        }

        static void Run(Bubble b, int ticks)
        {
            // Run until the first reflection, then stop, so the sign check is not undone by a second one.
            var start = b.Velocity;
            for (int i = 0; i < ticks; i++)
            {
                b.Tick(Dt);
                if (Mathf.Sign(b.Velocity.x) != Mathf.Sign(start.x) || Mathf.Sign(b.Velocity.y) != Mathf.Sign(start.y)) return;
            }
            Assert.Fail("no reflection happened");
        }

        [Test]
        public void PlatformsReflectItFromBelowAndAbove()
        {
            SetMap(StageLayout.Parse(Shelf));
            var below = Make(1, new Vector2(0f, 2.2f), 1, true); // child: starts upward
            for (int i = 0; i < 90; i++)
            {
                below.Tick(Dt);
                AssertOutsideTiles(below);
                if (below.Velocity.y < 0f) break;
            }
            Assert.Less(below.Velocity.y, 0f, "turned down under the platform");

            var above = Make(1, new Vector2(0f, 9f), 1); // placed high: starts downward
            Assert.Less(above.Velocity.y, 0f);
            for (int i = 0; i < 120; i++)
            {
                above.Tick(Dt);
                AssertOutsideTiles(above);
                if (above.Velocity.y > 0f) break;
            }
            Assert.Greater(above.Velocity.y, 0f, "turned up off the platform top");
        }

        static void AssertOutsideTiles(Bubble b)
        {
            var p = b.Position;
            bool inside = p.x > -6f && p.x < 6f && p.y > 4f && p.y < 5f; // centre inside the slab
            Assert.IsFalse(inside, $"centre inside the platform at {p}");
        }

        [Test]
        public void ChildrenLeaveUpAndMirrored()
        {
            var l = Make(2, new Vector2(0f, 3f), -1, true);
            var r = Make(2, new Vector2(0f, 3f), 1, true);
            Assert.Greater(l.Velocity.y, 0f);
            Assert.Greater(r.Velocity.y, 0f);
            Assert.Less(l.Velocity.x, 0f);
            Assert.Greater(r.Velocity.x, 0f);
        }

        [Test]
        public void StageBubblesStartTowardsTheMiddleHeight()
        {
            Assert.Less(Make(1, new Vector2(0f, 10f), 1).Velocity.y, 0f);
            Assert.Greater(Make(1, new Vector2(0f, 3f), 1).Velocity.y, 0f);
        }

        [Test]
        public void FairStartRule()
        {
            var wick = new Vector2(0f, 0f);
            Assert.IsFalse(DrifterBehavior.StartIsFair(new Vector2(-6f, 7f), new Vector2(6f, -6f), wick));
            Assert.IsTrue(DrifterBehavior.StartIsFair(new Vector2(-6f, 7f), new Vector2(-6f, 6f), wick));
            Assert.IsTrue(DrifterBehavior.StartIsFair(new Vector2(-14f, 10f), new Vector2(3.5f, 3.5f), wick));
        }

        [Test]
        public void ArrowSpritesDifferPerDirection()
        {
            var art = PlaceholderArt.I;
            for (int tier = 0; tier <= 1; tier++)
            {
                var all = new[]
                {
                    art.DrifterBubble(tier, 1, 1), art.DrifterBubble(tier, -1, 1),
                    art.DrifterBubble(tier, 1, -1), art.DrifterBubble(tier, -1, -1),
                };
                for (int i = 0; i < 4; i++)
                    for (int j = i + 1; j < 4; j++)
                        Assert.AreNotSame(all[i], all[j]);
                Assert.AreSame(all[0], art.DrifterBubble(tier, 1, 1), "cached");
                Assert.IsFalse(SamePixels(all[0], all[1]) || SamePixels(all[0], all[2]) || SamePixels(all[0], all[3]));
            }
        }

        static bool SamePixels(Sprite a, Sprite b)
        {
            var ra = a.rect; var rb = b.rect;
            var pa = a.texture.GetPixels((int)ra.x, (int)ra.y, (int)ra.width, (int)ra.height);
            var pb = b.texture.GetPixels((int)rb.x, (int)rb.y, (int)rb.width, (int)rb.height);
            if (pa.Length != pb.Length) return false;
            for (int i = 0; i < pa.Length; i++) if (pa[i] != pb[i]) return false;
            return true;
        }
    }
}
