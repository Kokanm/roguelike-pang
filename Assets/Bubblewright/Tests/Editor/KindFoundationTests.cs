using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>The six new kinds exist as data and art. Once a kind has its own behaviour, that behaviour's tests own its motion.</summary>
    public class KindFoundationTests
    {
        GameObject root;
        StageMap previousMap;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("KindFoundationTests");
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

        [Test]
        public void EveryKindHasATuningRowAndACost()
        {
            int count = System.Enum.GetValues(typeof(BubbleKind)).Length;
            Assert.AreEqual(count, Tuning.I.kindCostScale.Length);
            foreach (BubbleKind kind in System.Enum.GetValues(typeof(BubbleKind)))
                Assert.AreEqual(kind, Tuning.I.Kind(kind).kind);
        }

        [Test]
        public void TheTenKindColoursAreAllDifferent()
        {
            var seen = new HashSet<int>();
            foreach (BubbleKind kind in System.Enum.GetValues(typeof(BubbleKind)))
            {
                Color32 c = Tuning.I.Kind(kind).color;
                int key = (c.r << 16) | (c.g << 8) | c.b;
                Assert.IsTrue(seen.Add(key), $"{kind} shares a colour");
            }
            Assert.AreEqual(10, seen.Count);
        }

        [Test]
        public void EveryKindAndTierHasASpriteAndACeramicSprite()
        {
            var art = PlaceholderArt.I;
            int maxCeramic = Tuning.I.ceramicMaxTier;
            foreach (BubbleKind kind in System.Enum.GetValues(typeof(BubbleKind)))
                for (int tier = 0; tier < Tuning.I.tiers.Length; tier++)
                {
                    Assert.IsNotNull(art.Bubbles[(int)kind, tier], $"{kind} tier {tier}");
                    if (tier <= maxCeramic)
                        Assert.IsNotNull(art.CeramicBubble(kind, tier, false), $"{kind} ceramic tier {tier}");
                }
        }

        [Test]
        public void NewKindMarksAreMoreThanARecolour()
        {
            var art = PlaceholderArt.I;
            const int m = 2;
            foreach (BubbleKind kind in new[] { BubbleKind.Clinger, BubbleKind.Sinker, BubbleKind.Ghost, BubbleKind.Drifter })
                Assert.IsTrue(HasMarkColour(art.Bubbles[(int)kind, m], Tuning.I.Kind(kind).color), kind.ToString());

            AssertCentreIsNotBody(art.Bubbles[(int)BubbleKind.Frost, m], Tuning.I.Kind(BubbleKind.Frost).color);
            AssertCentreIsNotBody(art.Bubbles[(int)BubbleKind.Scorch, m], Tuning.I.Kind(BubbleKind.Scorch).color);
        }

        [Test]
        public void SinkerAndDrifterNeverComeBiggerThanL()
        {
            Assert.AreEqual(1, StageGenerator.LargestTier(BubbleKind.Sinker));
            Assert.AreEqual(1, StageGenerator.LargestTier(BubbleKind.Drifter));
            Assert.AreEqual(1, StageGenerator.BiggestAffordable(BubbleKind.Sinker, 1000f));
            Assert.AreNotEqual(0, StageGenerator.BiggestAffordable(BubbleKind.Sinker, 1f));
        }

        [Test]
        public void ResolveBoundsKeepsTheArenaAndIgnoresTiles()
        {
            var map = StageMap.Flat();
            int col = 10;
            map.Set(col, 4, new Cell { kind = TileKind.Solid });
            float radius = 0.4f;
            var start = new Vector2(StageMap.CenterX(col), 4.5f);

            var bounds = start;
            var circle = start;
            map.ResolveBounds(ref bounds, radius);
            var pushed = map.ResolveCircle(ref circle, radius);
            Assert.AreEqual(start.x, bounds.x, 1e-4f);
            Assert.AreEqual(start.y, bounds.y, 1e-4f);
            Assert.Greater((circle - start).sqrMagnitude, 0.0001f, "ResolveCircle pushes the circle out of the platform");
            Assert.IsTrue(pushed.landed || pushed.bumped || pushed.side != 0);

            var floor = new Vector2(0f, -2f);
            var floorContact = map.ResolveBounds(ref floor, 0.5f);
            Assert.IsTrue(floorContact.landed);
            Assert.AreEqual(0.5f, floor.y, 1e-4f);

            var left = new Vector2(-30f, 3f);
            map.ResolveBounds(ref left, 0.5f);
            Assert.AreEqual(-16f + 0.5f, left.x, 1e-4f);
            var right = new Vector2(30f, 3f);
            map.ResolveBounds(ref right, 0.5f);
            Assert.AreEqual(16f - 0.5f, right.x, 1e-4f);
        }

        [Test]
        public void APlainGloopCanBeHitAndHurtUntilItPops()
        {
            var b = Bubble.Create(root.transform, BubbleKind.Gloop, 1, new Vector2(0f, 4f), Vector2.zero, false);
            Assert.IsTrue(b.CanBeHit);
            Assert.IsTrue(b.CanHurt);
            b.Popped = true;
            Assert.IsFalse(b.CanBeHit);
            Assert.IsFalse(b.CanHurt);
        }

        static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        static bool HasMarkColour(Sprite sprite, Color bodyColor)
        {
            Color32 body = bodyColor;
            Color32 crescent = Palette.Shade(body, 0.78f);
            foreach (var p in sprite.texture.GetPixels32())
            {
                if (p.a == 0) continue;
                if (Same(p, body) || Same(p, crescent) || Same(p, Palette.Cream) || Same(p, Palette.Ink)) continue;
                return true;
            }
            return false;
        }

        static void AssertCentreIsNotBody(Sprite sprite, Color bodyColor)
        {
            var tex = sprite.texture;
            int x = Mathf.Clamp(Mathf.RoundToInt(sprite.pivot.x), 0, tex.width - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(sprite.pivot.y), 0, tex.height - 1);
            Color32 centre = tex.GetPixel(x, y);
            Assert.IsFalse(Same(centre, bodyColor), $"centre pixel is the body colour at ({x}, {y})");
        }
    }
}
