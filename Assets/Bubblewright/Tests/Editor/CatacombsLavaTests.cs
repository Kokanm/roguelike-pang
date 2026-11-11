using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>Catacombs and Lava: background painters, tile details and the iron spikes.</summary>
    public class CatacombsLavaTests
    {
        const int Width = 960, Height = 510;

        static IEnumerable<TestCaseData> BothThemes()
        {
            yield return new TestCaseData(Themes.ForWorld(5)).SetName("Catacombs");
            yield return new TestCaseData(Themes.ForWorld(6)).SetName("Lava");
        }

        static Texture2D Paint(Theme t)
        {
            var c = new PixelCanvas(Width, Height);
            t.paintBackground(c, t, new System.Random(System.Array.IndexOf(Themes.All, t) + 1));
            return c.ToTexture("test");
        }

        [TestCaseSource(nameof(BothThemes))]
        public void BackgroundCoversTheCanvasAndUsesManyColours(Theme t)
        {
            var sprite = PlaceholderArt.I.BackgroundFor(t);
            Assert.AreEqual(PlaceholderArt.I.BackgroundFor(Themes.ForWorld(1)).texture.width, sprite.texture.width);
            Assert.AreEqual(PlaceholderArt.I.BackgroundFor(Themes.ForWorld(1)).texture.height, sprite.texture.height);
            var colours = new HashSet<Color32>();
            foreach (var p in sprite.texture.GetPixels32())
            {
                Assert.AreEqual(255, p.a, $"{t.name}: every pixel is painted");
                colours.Add(p);
            }
            Assert.GreaterOrEqual(colours.Count, 6, t.name);
        }

        [TestCaseSource(nameof(BothThemes))]
        public void NothingIsBrighterThanTheTileCapExceptAccent(Theme t)
        {
            float limit = UiText.RelativeLuminance(t.tileCap);
            foreach (var p in Paint(t).GetPixels32())
            {
                if (SameRgb(p, t.accent)) continue;
                Assert.LessOrEqual(UiText.RelativeLuminance(p), limit + 0.0001f, $"{t.name}: pixel {p}");
            }
        }

        [TestCaseSource(nameof(BothThemes))]
        public void BuildingTheBackgroundTwiceGivesTheSamePixels(Theme t)
        {
            CollectionAssert.AreEqual(Paint(t).GetPixels32(), Paint(t).GetPixels32());
        }

        [Test]
        public void LavaGlowStaysInTheBottomFifthExceptSingleEmbers()
        {
            var t = Themes.ForWorld(6);
            var pixels = Paint(t).GetPixels32();
            int ppu = Tuning.I.pixelsPerUnit;
            int floorY = Mathf.RoundToInt(-ThemeBackgrounds.Bottom * ppu);
            int arenaLeft = Mathf.RoundToInt((-Tuning.I.HalfWidth - ThemeBackgrounds.Left) * ppu);
            int arenaRight = Mathf.RoundToInt((Tuning.I.HalfWidth - ThemeBackgrounds.Left) * ppu);
            int arenaHeight = Mathf.RoundToInt(Tuning.I.arenaHeight * ppu);
            int fromY = floorY + Mathf.RoundToInt(arenaHeight * 0.25f);

            bool Accent(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && SameRgb(pixels[y * Width + x], t.accent);
            int embers = 0;
            for (int y = fromY; y < floorY + arenaHeight; y++)
                for (int x = arenaLeft; x < arenaRight; x++)
                {
                    if (!Accent(x, y)) continue;
                    embers++;
                    Assert.IsFalse(Accent(x - 1, y) || Accent(x + 1, y) || Accent(x, y - 1) || Accent(x, y + 1),
                        $"accent at {x},{y} is not an isolated ember");
                }
            Assert.Less(embers, 40, "embers are a few single pixels");
        }

        [Test]
        public void CatacombsAndLavaHaveTheirOwnPainters()
        {
            var catacombs = Themes.ForWorld(5).paintBackground;
            var lava = Themes.ForWorld(6).paintBackground;
            Assert.AreNotEqual(catacombs, lava);
            Assert.AreNotEqual(Themes.ForWorld(1).paintBackground, catacombs);
            Assert.AreNotEqual(Themes.ForWorld(1).paintBackground, lava);
        }

        [Test]
        public void LavaSpikesAreTintedIronAndOtherWorldsKeepSteel()
        {
            var art = PlaceholderArt.I;
            var lava = Themes.ForWorld(6);
            Color32 iron = Palette.Mix(Palette.Steel, lava.far, 0.25f);
            Assert.AreEqual(iron, PlaceholderArt.SpikeFill(lava));
            Assert.AreNotEqual(Palette.Steel, iron);
            Assert.AreEqual(Palette.Steel, PlaceholderArt.SpikeFill(Themes.ForWorld(1)));
            Assert.AreEqual(Palette.Steel, PlaceholderArt.SpikeFill(Themes.ForWorld(5)));

                        var raisedLava = art.SpikeRaisedFor(lava).texture.GetPixels32();
            var raisedCrypt = art.SpikeRaisedFor(Themes.ForWorld(5)).texture.GetPixels32();
            const int fillIndex = 3 * 15 + 2, litIndex = 3 * 15 + 1; // row 3 from the bottom is "ILS": ink, light, fill
            Assert.AreEqual(iron, raisedLava[fillIndex]);
            Assert.AreEqual(Palette.Steel, raisedCrypt[fillIndex]);
            Assert.AreSame(art.SpikeRaised, art.SpikeRaisedFor(Themes.ForWorld(1)));
            Assert.AreSame(art.SpikeRaisedFor(lava), art.SpikeRaisedFor(lava), "built once");
            Assert.AreEqual(Palette.SteelLight, raisedLava[litIndex], "the lit edge keeps its colour");
        }

        [Test]
        public void CatacombsTilesGetChipsAndSkullMarksButBreakablesKeepTheirBricks()
        {
            var t = Themes.ForWorld(5);
            var map = StageMap.Flat();
            // Tile index = row * 32 + column, and all of these sit in row 1.
            map.Set(0, 1, new Cell { kind = TileKind.Solid });   // 32: neither a multiple of 3 nor of 5
            map.Set(10, 1, new Cell { kind = TileKind.Solid });  // 42: a multiple of 3, chip (42 / 3 = 14 is even: left corner)
            map.Set(13, 1, new Cell { kind = TileKind.Solid });  // 45: chip (15 is odd: right corner) and a skull mark
            map.Set(23, 1, new Cell { kind = TileKind.Breakable, hp = 2 }); // 55: a multiple of 5, but breakables are left alone
            var pixels = PaintStage(map, t, out int width);
            Color32 Pixel(int c, int x, int y) => pixels[(15 + y) * width + c * 15 + x];

            Assert.AreEqual(t.tileLine, Pixel(0, 0, 14), "plain tile keeps its outline corner");
            Assert.AreEqual(t.tileShade, Pixel(10, 0, 14), "chip, left corner");
            Assert.AreEqual(t.tileLine, Pixel(10, 14, 14), "the other corner is untouched");
            Assert.AreEqual(t.tileShade, Pixel(13, 14, 14), "chip, right corner");
            for (int x = 6; x <= 8; x++) Assert.AreEqual(t.tileShade, Pixel(13, x, 6), "skull top");
            Assert.AreEqual(t.tileShade, Pixel(13, 7, 5), "nose bridge");
            Assert.AreEqual(t.tileShade, Pixel(13, 7, 4), "jaw");
            Assert.AreEqual(t.tileFill, Pixel(13, 6, 4), "left eye is stone");
            Assert.AreEqual(t.tileFill, Pixel(13, 8, 4), "right eye is stone");
            Assert.AreEqual(t.tileFill, Pixel(0, 7, 4), "no skull on a plain tile");
            Assert.AreEqual(t.breakFill, Pixel(23, 7, 7), "breakable keeps its fill and brick pattern");
            Assert.AreEqual(t.breakFill, Pixel(23, 7, 4), "breakable has no skull mark");
        }

        [Test]
        public void LavaTilesGetCracksAndOnlyOpenUndersidesGlow()
        {
            var t = Themes.ForWorld(6);
            var map = StageMap.Flat();
            map.Set(2, 4, new Cell { kind = TileKind.Solid });   // a platform with nothing under it
            map.Set(5, 4, new Cell { kind = TileKind.Solid });   // this one has a tile under it
            map.Set(5, 3, new Cell { kind = TileKind.Solid });   // ...and this one has nothing under it
            var pixels = PaintStage(map, t, out int width);
            Color32 Pixel(int c, int r, int x, int y) => pixels[(r * 15 + y) * width + c * 15 + x];
            Color32 glow = Palette.Shade(t.accent, 0.7f);

            int index = 4 * StageMap.Cols + 2;
            Assert.AreEqual(t.tileShade, Pixel(2, 4, 3 + index % 3, 5), "first crack");
            Assert.AreEqual(t.tileShade, Pixel(2, 4, 9 + index % 3, 4), "second crack");
            for (int x = 0; x < 15; x++) Assert.AreEqual(glow, Pixel(2, 4, x, 0), $"glow along the open underside, x {x}");
            for (int x = 0; x < 15; x++) Assert.AreNotEqual(glow, Pixel(5, 4, x, 0), "a tile with a tile under it does not glow");
            for (int x = 0; x < 15; x++) Assert.AreEqual(glow, Pixel(5, 3, x, 0), "the lower of the pair glows");
        }

        // Paints a map with StageView and returns the tile layer's pixels.
        static Color32[] PaintStage(StageMap map, Theme theme, out int width)
        {
            typeof(Game).GetProperty("Map", BindingFlags.Public | BindingFlags.Static).SetValue(null, map);
            Game.CurrentTheme = theme;
            var view = StageView.Create(null);
            try
            {
                view.Tick();
                var texture = view.GetComponent<SpriteRenderer>().sprite.texture;
                width = texture.width;
                return texture.GetPixels32();
            }
            finally
            {
                Game.CurrentTheme = Themes.ForWorld(1);
                Object.DestroyImmediate(view.gameObject);
            }
        }

        static bool SameRgb(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;
    }
}
