using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>The six world themes: palette contrast, ordering and the unchanged Rooftops background.</summary>
    public class ThemeTests
    {
        const float MinBubbleContrast = 2.7f;
        const float MinPlatformContrast = 2.9f;

        static IEnumerable<TestCaseData> AllThemes()
        {
            foreach (var theme in Themes.All) yield return new TestCaseData(theme).SetName($"Theme {theme.name}");
        }

        static IEnumerable<TestCaseData> PaintedThemes()
        {
            for (int world = 2; world <= 4; world++)
            {
                Theme theme = Themes.ForWorld(world);
                yield return new TestCaseData(theme, world).SetName($"{theme.name} painted background");
            }
        }

        [Test]
        public void ThereAreSixThemesInWorldOrder()
        {
            Assert.AreEqual(6, Themes.All.Length);
            string[] names = { "Rooftops", "Forest", "Desert", "Winter", "Catacombs", "Lava" };
            for (int world = 1; world <= 6; world++)
            {
                Assert.AreSame(Themes.All[world - 1], Themes.ForWorld(world));
                Assert.AreEqual(names[world - 1], Themes.ForWorld(world).name);
            }
            Assert.AreSame(Themes.All[0], Themes.ForWorld(0), "below range clamps to world 1");
            Assert.AreSame(Themes.All[5], Themes.ForWorld(99), "above range clamps to world 6");
        }

        [TestCaseSource(nameof(AllThemes))]
        public void CreamTextReadsOnSkyFarLayerAndStrip(Theme t)
        {
            foreach (var (label, bg) in new[] { ("skyBottom", t.skyBottom), ("far", t.far), ("strip", t.strip) })
                Assert.GreaterOrEqual(UiText.Contrast(Palette.Cream, bg), UiText.MinTextContrast, $"{t.name}: cream on {label}");
        }

        [TestCaseSource(nameof(AllThemes))]
        public void BubbleColoursStandOutFromTheBackground(Theme t)
        {
            var bubbles = new[] { ("coral", Palette.Coral), ("azure", Palette.Azure), ("plum", Palette.Plum), ("lemon", Palette.Lemon) };
            var backgrounds = new[] { ("skyTop", t.skyTop), ("skyBottom", t.skyBottom), ("far", t.far), ("near", t.near) };
            foreach (var (bubbleName, bubble) in bubbles)
                foreach (var (bgName, bg) in backgrounds)
                    Assert.GreaterOrEqual(UiText.Contrast(bubble, bg), MinBubbleContrast, $"{t.name}: {bubbleName} on {bgName}");
        }

        [TestCaseSource(nameof(AllThemes))]
        public void PlatformsCanBeSeenAgainstTheSky(Theme t)
        {
            Assert.GreaterOrEqual(UiText.Contrast(t.tileCap, t.skyBottom), MinPlatformContrast, t.name);
        }

        [TestCaseSource(nameof(AllThemes))]
        public void BreakLineIsTheTileShadeAndNoSceneryUsesReservedColours(Theme t)
        {
            Assert.AreEqual(t.tileShade, t.breakLine);
            var reserved = new[] { Palette.Saffron, Palette.Mint, Palette.Coral, Palette.Azure, Palette.Plum, Palette.Lemon };
            var scenery = new[] { t.skyTop, t.skyBottom, t.far, t.near, t.tileFill, t.tileCap, t.tileShade, t.tileLine, t.breakFill, t.accent, t.strip };
            foreach (var c in scenery)
                foreach (var r in reserved)
                    Assert.AreNotEqual(r, c, $"{t.name} uses a reserved colour");
        }

        [Test]
        public void BackgroundIsBuiltOncePerThemeAndEveryThemeLooksDifferent()
        {
            var art = PlaceholderArt.I;
            var seen = new List<string>();
            foreach (var t in Themes.All)
            {
                var sprite = art.BackgroundFor(t);
                Assert.AreSame(sprite, art.BackgroundFor(t), $"{t.name} is cached");
                Assert.AreEqual(960, sprite.texture.width);
                Assert.AreEqual(510, sprite.texture.height);
                Assert.AreEqual(480f, sprite.pivot.x, 0.01f, "pivot is the world origin");
                Assert.AreEqual(150f, sprite.pivot.y, 0.01f, "pivot is the world origin");
                seen.Add(Checksum(sprite.texture.GetPixels32()));
            }
            CollectionAssert.AllItemsAreUnique(seen);
        }

        [Test]
        public void ThemeBackgroundIsTheSameEveryTime()
        {
            // The shared painter is seeded by world number, so a theme never changes between builds.
            var t = Themes.ForWorld(3);
            Assert.AreEqual(Paint(t, 3), Paint(t, 3));
        }

        [TestCaseSource(nameof(PaintedThemes))]
        public void PaintedBackgroundMatchesRooftopsSizeIsOpaqueAndUsesAtLeastSixColours(Theme theme, int world)
        {
            Texture2D rooftops = PlaceholderArt.I.BackgroundFor(Themes.ForWorld(1)).texture;
            Texture2D painted = PaintTexture(theme, world);
            Assert.AreEqual(rooftops.width, painted.width);
            Assert.AreEqual(rooftops.height, painted.height);

            var colours = new HashSet<Color32>();
            foreach (Color32 pixel in painted.GetPixels32())
            {
                Assert.AreEqual(255, pixel.a, $"{theme.name} has a transparent pixel");
                colours.Add(pixel);
            }
            Assert.GreaterOrEqual(colours.Count, 6, theme.name);
        }

        [TestCaseSource(nameof(PaintedThemes))]
        public void PaintedBackgroundNeverExceedsTileCapLuminanceExceptForAccent(Theme theme, int world)
        {
            float maximum = UiText.RelativeLuminance(theme.tileCap);
            foreach (Color32 pixel in PaintTexture(theme, world).GetPixels32())
                if (!pixel.Equals(theme.accent))
                    Assert.LessOrEqual(UiText.RelativeLuminance(pixel), maximum + 0.0001f,
                        $"{theme.name} pixel {pixel} is brighter than tileCap");
        }

        [TestCaseSource(nameof(PaintedThemes))]
        public void PaintedBackgroundIsDeterministic(Theme theme, int world)
        {
            CollectionAssert.AreEqual(
                PaintTexture(theme, world).GetPixels32(),
                PaintTexture(theme, world).GetPixels32(),
                theme.name);
        }

        [Test]
        public void PaintedThemesProvideTileDetailsWithoutTouchingTheOutline()
        {
            var field = typeof(Theme).GetField("paintTileDetail");
            Assert.NotNull(field, "Theme needs a tile-detail hook");

            foreach (Theme theme in new[] { Themes.ForWorld(2), Themes.ForWorld(3), Themes.ForWorld(4) })
            {
                var painter = (System.Action<PixelCanvas, int, int, Cell>)field.GetValue(theme);
                Assert.NotNull(painter, theme.name);
                var canvas = new PixelCanvas(15, 15);
                canvas.Rect(0, 0, 15, 15, theme.tileFill);
                for (int i = 0; i < 15; i++)
                {
                    canvas.Set(i, 0, theme.tileLine);
                    canvas.Set(i, 14, theme.tileLine);
                    canvas.Set(0, i, theme.tileLine);
                    canvas.Set(14, i, theme.tileLine);
                }

                painter(canvas, 0, 0, new Cell { kind = TileKind.Solid, surface = Surface.Normal });

                for (int i = 0; i < 15; i++)
                {
                    Assert.AreEqual(theme.tileLine, canvas.Get(i, 0), $"{theme.name} bottom outline");
                    Assert.AreEqual(theme.tileLine, canvas.Get(i, 14), $"{theme.name} top outline");
                    Assert.AreEqual(theme.tileLine, canvas.Get(0, i), $"{theme.name} left outline");
                    Assert.AreEqual(theme.tileLine, canvas.Get(14, i), $"{theme.name} right outline");
                }
            }
        }

        [Test]
        public void RooftopsBackgroundIsPixelForPixelTheOriginal()
        {
            // LegacyRooftops is the pre-theme BuildBackground, frozen here as the reference.
            var expected = LegacyRooftops(Tuning.I).GetPixels32();
            var actual = PlaceholderArt.I.BackgroundFor(Themes.ForWorld(1)).texture.GetPixels32();
            Assert.AreEqual(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++)
                if (!expected[i].Equals(actual[i]))
                    Assert.Fail($"First difference at pixel {i % 960},{i / 960}: expected {expected[i]}, got {actual[i]}");
        }

        static string Paint(Theme t, int world)
        {
            return Checksum(PaintTexture(t, world).GetPixels32());
        }

        static Texture2D PaintTexture(Theme t, int world)
        {
            var c = new PixelCanvas(960, 510);
            t.paintBackground(c, t, new System.Random(world));
            return c.ToTexture("theme test");
        }

        static string Checksum(Color32[] pixels)
        {
            ulong hash = 1469598103934665603UL;
            foreach (var p in pixels)
            {
                hash = (hash ^ p.r) * 1099511628211UL;
                hash = (hash ^ p.g) * 1099511628211UL;
                hash = (hash ^ p.b) * 1099511628211UL;
                hash = (hash ^ p.a) * 1099511628211UL;
            }
            return hash.ToString("X");
        }

        static Texture2D LegacyRooftops(Tuning t)
        {
            int ppu = t.pixelsPerUnit;
            const float left = -32f, bottom = -10f, top = 24f, right = 32f;
            int w = Mathf.RoundToInt((right - left) * ppu);
            int h = Mathf.RoundToInt((top - bottom) * ppu);
            var c = new PixelCanvas(w, h);
            int Px(float wx) => Mathf.RoundToInt((wx - left) * ppu);
            int Py(float wy) => Mathf.RoundToInt((wy - bottom) * ppu);

            // Sky: deep at the top, dusk at the horizon, ordered dither between bands.
            int[] bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
            int horizon = Py(2f), skyTop = Py(t.arenaHeight + 2f);
            for (int y = Py(0f); y < h; y++)
            {
                float k = Mathf.Clamp01((float)(y - horizon) / (skyTop - horizon));
                for (int x = 0; x < w; x++)
                {
                    float threshold = (bayer[(y % 4) * 4 + (x % 4)] + 0.5f) / 16f;
                    c.Set(x, y, k > threshold ? Palette.Deep : Palette.Dusk);
                }
            }

            var rng = new System.Random(7);
            // Stars
            for (int i = 0; i < 260; i++)
            {
                int x = rng.Next(w), y = rng.Next(Py(4f), h);
                c.Set(x, y, rng.NextDouble() < 0.3 ? Palette.Cream : Palette.Lilac);
            }
            // Moon
            c.Circle(Px(10.5f), Py(11.2f), 17f, Palette.Cream);
            c.Circle(Px(10.5f) + 5, Py(11.2f) - 4, 3.5f, Palette.Mix(Palette.Cream, Palette.Lilac, 0.35f));
            c.Circle(Px(10.5f) - 6, Py(11.2f) + 5, 2.2f, Palette.Mix(Palette.Cream, Palette.Lilac, 0.35f));

            // Rooftop silhouettes (deep, darker than the horizon)
            var roofColor = Palette.Mix(Palette.Deep, Palette.Ink, 0.35f);
            int bx = 0;
            while (bx < w)
            {
                int bw = rng.Next(30, 75);
                int bh = Mathf.RoundToInt((1.2f + (float)rng.NextDouble() * 3.8f) * ppu);
                int y0 = Py(0f);
                c.Rect(bx, y0, bw, bh, roofColor);
                if (rng.NextDouble() < 0.5)
                    for (int i = 0; i < bw / 2; i++) c.Rect(bx + i, y0 + bh + i / 2 - 1, bw - 2 * i, 1, roofColor); // pitched roof
                if (rng.NextDouble() < 0.6) c.Rect(bx + rng.Next(4, Mathf.Max(5, bw - 8)), y0 + bh, 4, 7, roofColor); // chimney
                for (int wy = y0 + 5; wy < y0 + bh - 4; wy += 9)
                    for (int wx = bx + 4; wx < bx + bw - 4; wx += 8)
                        if (rng.NextDouble() < 0.18) c.Rect(wx, wy, 2, 3, Palette.Mix(roofColor, Palette.Lilac, 0.6f));
                bx += bw + rng.Next(0, 6);
            }

            // Arena walls and ceiling beam (only visible on screens wider or taller than 16:9)
            int wallW = 6;
            int ceilY = Py(t.arenaHeight);
            for (int side = -1; side <= 1; side += 2)
            {
                int wx = side < 0 ? Px(-t.HalfWidth) - wallW : Px(t.HalfWidth);
                c.Rect(wx, Py(0f), wallW, ceilY - Py(0f) + 4, Palette.Lilac);
                c.Rect(side < 0 ? wx + wallW - 1 : wx, Py(0f), 1, ceilY - Py(0f) + 4, Palette.Mix(Palette.Lilac, Palette.Cream, 0.4f));
                c.Rect(side < 0 ? wx - 1 : wx + wallW, Py(0f), 1, ceilY - Py(0f) + 4, Palette.Ink);
            }
            c.Rect(Px(-t.HalfWidth) - wallW, ceilY, Px(t.HalfWidth) - Px(-t.HalfWidth) + wallW * 2, 4, Palette.Lilac);
            c.Rect(Px(-t.HalfWidth), ceilY, Px(t.HalfWidth) - Px(-t.HalfWidth), 1, Palette.Ink);

            // Floor: a lilac ledge, then the ground strip that holds the thumb controls
            int floorTop = Py(0f);
            c.Rect(0, floorTop - 6, w, 6, Palette.Lilac);
            c.Rect(0, floorTop - 1, w, 1, Palette.Mix(Palette.Lilac, Palette.Cream, 0.45f));
            c.Rect(0, floorTop - 6, w, 1, Palette.Shade(Palette.Lilac, 0.7f));
            c.Rect(0, floorTop - 7, w, 1, Palette.Ink);
            c.Rect(0, 0, w, floorTop - 7, Palette.Ink);
            // Faint brick courses in the ground strip
            var brick = Palette.Mix(Palette.Ink, Palette.Deep, 0.55f);
            for (int y = floorTop - 15, row = 0; y > 0; y -= 8, row++)
            {
                c.Rect(0, y, w, 1, brick);
                for (int x = (row % 2) * 12; x < w; x += 24) c.Rect(x, y - 7, 1, 7, brick);
            }
            return c.ToTexture("Legacy Rooftops");
        }
    }
}
