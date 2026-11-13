using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>The Ghost: ignores platform tiles, and alternates solid and faded (no hits, no harm while faded).</summary>
    public class GhostTests
    {
        const float Dt = 1f / 60f;

        // A row-4 platform from column 10 to 21 (x from -6 to 6).
        static readonly string[] PlatformMap =
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
            "..........############..........",
            "................................",
            "................................",
            "................................",
            "................................",
        };

        GameObject root;
        StageMap previousMap;
        Rect previousBox;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("GhostTests");
            previousMap = Game.Map;
            previousBox = Game.WickBox;
            Game.WickBox = new Rect(100f, 100f, 1f, 1f);
            SetMap(StageMap.Flat());
        }

        [TearDown]
        public void TearDown()
        {
            SetMap(previousMap);
            Game.WickBox = previousBox;
            Object.DestroyImmediate(root);
        }

        static void SetMap(StageMap map)
        {
            var property = typeof(Game).GetProperty("Map", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(property, "Game.Map must exist.");
            property.SetValue(null, map);
        }

        Bubble Make(BubbleKind kind, int tier, Vector2 at, bool ceramic = false) =>
            Bubble.Create(root.transform, kind, tier, at, new Vector2(Tuning.I.bubbleSpeedX, 0f), true, ceramic);

        [Test]
        public void AGhostFallsThroughAPlatformAndLandsOnTheGround()
        {
            SetMap(stage(PlatformMap));
            var ghost = Make(BubbleKind.Ghost, 2, new Vector2(-2f, 8f));
            bool wasInside = false, landed = false;
            for (int i = 0; i < 60 * 12; i++)
            {
                ghost.Tick(Dt);
                var p = ghost.Position;
                if (p.x > -6f && p.x < 6f && p.y > 4f && p.y < 5f) wasInside = true;
                if (wasInside && p.y <= ghost.Radius + 0.01f) landed = true;
            }
            Assert.IsTrue(wasInside, "the Ghost's centre went through the platform");
            Assert.IsTrue(landed, "and it later landed on the ground");
        }

        [Test]
        public void AGloopOnTheSameMapStaysAbovePlatform()
        {
            SetMap(stage(PlatformMap));
            var gloop = Make(BubbleKind.Gloop, 2, new Vector2(-2f, 8f));
            for (int i = 0; i < 60 * 2; i++)
            {
                gloop.Tick(Dt);
                var p = gloop.Position;
                if (p.x > -6f && p.x < 6f) Assert.GreaterOrEqual(p.y, 5f + gloop.Radius - 0.01f, $"tick {i}");
            }
        }

        [Test]
        public void AGhostBouncesToItsPeakAndStaysInTheArena()
        {
            var ghost = Make(BubbleKind.Ghost, 2, new Vector2(0f, 5f));
            float peak = Tuning.I.tiers[2].peakHeight;
            for (int i = 0; i < 60 * 20; i++)
            {
                ghost.Tick(Dt);
                var p = ghost.Position;
                Assert.LessOrEqual(p.y + ghost.Radius, peak + 0.05f, $"tick {i}");
                Assert.LessOrEqual(Mathf.Abs(p.x) + ghost.Radius, Tuning.I.HalfWidth + 0.01f, $"tick {i}");
                Assert.GreaterOrEqual(p.y - ghost.Radius, -0.01f, $"tick {i}");
            }
        }

        [Test]
        public void ItIsSolidThenFadedThenSolidAgain()
        {
            var ghost = Make(BubbleKind.Ghost, 2, new Vector2(0f, 5f));
            Run(ghost, 144); // 2.4 s
            Assert.IsTrue(ghost.CanBeHit && ghost.CanHurt, "solid before 2.5 s");
            Run(ghost, 12);  // 2.6 s
            Assert.IsFalse(ghost.CanBeHit, "faded from 2.5 s");
            Assert.IsFalse(ghost.CanHurt);
            Run(ghost, 78);  // 3.9 s
            Assert.IsFalse(ghost.CanBeHit && ghost.CanHurt, "still faded before 4.0 s");
            Run(ghost, 12);  // 4.1 s
            Assert.IsTrue(ghost.CanBeHit && ghost.CanHurt, "solid again after 4.0 s");
        }

        [Test]
        public void ItStaysFadedWhileItOverlapsWick()
        {
            var ghost = Make(BubbleKind.Ghost, 2, new Vector2(0f, 5f));
            Game.WickBox = new Rect(-20f, -1f, 40f, 30f); // covers the whole arena
            Run(ghost, 270); // 4.5 s
            Assert.IsFalse(ghost.CanBeHit, "stays faded around Wick");
            Game.WickBox = new Rect(100f, 100f, 1f, 1f);
            Run(ghost, 1);
            Assert.IsTrue(ghost.CanBeHit, "solid one tick after Wick is clear");
        }

        [Test]
        public void ASolidAgainCeramicGhostShowsItsCeramicSprite()
        {
            var ghost = Make(BubbleKind.Ghost, 2, new Vector2(0f, 5f), ceramic: true);
            var sr = ghost.GetComponent<SpriteRenderer>();
            Run(ghost, 156);
            Assert.AreEqual(PlaceholderArt.I.GhostFaded(2, false), sr.sprite);
            Run(ghost, 90);
            Assert.AreEqual(PlaceholderArt.I.CeramicBubble(BubbleKind.Ghost, 2, false), sr.sprite);
        }

        [Test]
        public void ThePlacedGhostsStartOutOfStep()
        {
            var a = Bubble.Create(root.transform, BubbleKind.Ghost, 2, new Vector2(-9f, 5f), Vector2.zero, false);
            var b = Bubble.Create(root.transform, BubbleKind.Ghost, 2, new Vector2(4f, 5f), Vector2.zero, false);
            // Ticking until the first of them fades must not fade the other one in the same tick.
            for (int i = 0; i < 60 * 4; i++)
            {
                a.Tick(Dt);
                b.Tick(Dt);
                if (!a.CanBeHit != !b.CanBeHit) return;
            }
            Assert.Fail("the two Ghosts faded in the same tick and came back in the same tick");
        }

        [Test]
        public void TheFadedSpritesExistAndAreSeeThrough()
        {
            for (int tier = 0; tier < Tuning.I.tiers.Length; tier++)
            {
                var faint = PlaceholderArt.I.GhostFaded(tier, false);
                var strong = PlaceholderArt.I.GhostFaded(tier, true);
                Assert.NotNull(faint);
                Assert.NotNull(strong);
                Assert.AreNotSame(faint, strong);
                int d = faint.texture.width;
                Assert.Less(faint.texture.GetPixel(d / 2, d - 2).a, 1f, "rim pixel is see-through");
                Assert.Less(strong.texture.GetPixel(d / 2, d - 2).a, 1f);
                Assert.Less(faint.texture.GetPixel(d / 2, d / 2).a, strong.texture.GetPixel(d / 2, d / 2).a, $"tier {tier}: strong frame is brighter");
            }
        }

        [Test]
        public void APoppedGhostCannotBeHit()
        {
            var ghost = Make(BubbleKind.Ghost, 2, new Vector2(0f, 5f));
            Assert.IsTrue(ghost.CanBeHit);
            ghost.Swell(1.2f);
            Assert.IsFalse(ghost.CanBeHit);
            Assert.IsFalse(ghost.CanHurt);
        }

        static StageMap stage(string[] rows) => StageLayout.Parse(rows);

        static void Run(Bubble b, int ticks)
        {
            for (int i = 0; i < ticks; i++) b.Tick(Dt);
        }
    }
}
