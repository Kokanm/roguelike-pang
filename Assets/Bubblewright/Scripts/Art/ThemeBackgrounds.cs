using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Background painters, one per theme. Each fills a 64 x 34 unit canvas (pivot at the arena's floor centre) that sits
    /// behind the stage: sky, scenery, the arena walls and ceiling beam (seen on wide screens) and the floor ledge and
    /// ground strip. See PlaceholderArt.BackgroundFor.
    /// </summary>
    public static class ThemeBackgrounds
    {
        /// <summary>Canvas extent in world units. The sprite pivot is the world origin.</summary>
        public const float Left = -32f, Bottom = -10f, Top = 24f, Right = 32f;

        /// <summary>
        /// World 1: the original night rooftops, moved here unchanged. It keeps its own seed (7) so the pixels stay
        /// exactly as they were; the random generator passed in is not used.
        /// </summary>
        public static void Rooftops(PixelCanvas c, Theme theme, System.Random unused)
        {
            var t = Tuning.I;
            int ppu = t.pixelsPerUnit;
            const float left = -32f, bottom = -10f;
            int w = c.Width, h = c.Height;
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
        }

        /// <summary>Fills the sky above the floor with six hard-edged horizontal bands.</summary>
        internal static void PaintSky(PixelCanvas c, Theme theme)
        {
            var t = Tuning.I;
            int ppu = t.pixelsPerUnit;
            int Py(float wy) => Mathf.RoundToInt((wy - Bottom) * ppu);
            int floorY = Py(0f);
            int horizon = Py(2f), skyTopY = Py(t.arenaHeight + 2f);
            for (int y = floorY; y < c.Height; y++)
            {
                float k = Mathf.Clamp01((float)(y - horizon) / (skyTopY - horizon));
                int band = Mathf.Min(5, Mathf.FloorToInt(k * 6f));
                c.Rect(0, y, c.Width, 1, Palette.Mix(theme.skyBottom, theme.skyTop, band / 5f));
            }
        }

        /// <summary>Arena walls, ceiling beam, floor ledge and the ground strip, in the theme's tile colours.</summary>
        /// <summary>Arena frame for painted worlds, restricted to that world's colours and tile-cap luminance.</summary>
        internal static void PaintedFrame(PixelCanvas c, Theme theme) => Frame(c, theme, true);

        static void Frame(PixelCanvas c, Theme theme, bool paintedWorld)
        {
            var t = Tuning.I;
            int ppu = t.pixelsPerUnit;
            int w = c.Width;
            int Px(float wx) => Mathf.RoundToInt((wx - Left) * ppu);
            int Py(float wy) => Mathf.RoundToInt((wy - Bottom) * ppu);
            Color32 lit = paintedWorld
                ? Palette.Shade(theme.tileCap, 0.92f)
                : Palette.Mix(theme.tileCap, theme.accent, 0.4f);
            Color32 outline = paintedWorld ? theme.tileLine : Palette.Ink;

            int wallW = 6;
            int ceilY = Py(t.arenaHeight);
            for (int side = -1; side <= 1; side += 2)
            {
                int wx = side < 0 ? Px(-t.HalfWidth) - wallW : Px(t.HalfWidth);
                c.Rect(wx, Py(0f), wallW, ceilY - Py(0f) + 4, theme.tileCap);
                c.Rect(side < 0 ? wx + wallW - 1 : wx, Py(0f), 1, ceilY - Py(0f) + 4, lit);
                c.Rect(side < 0 ? wx - 1 : wx + wallW, Py(0f), 1, ceilY - Py(0f) + 4, outline);
            }
            c.Rect(Px(-t.HalfWidth) - wallW, ceilY, Px(t.HalfWidth) - Px(-t.HalfWidth) + wallW * 2, 4, theme.tileCap);
            c.Rect(Px(-t.HalfWidth), ceilY, Px(t.HalfWidth) - Px(-t.HalfWidth), 1, outline);

            // Floor ledge, then the flat ground strip that holds the thumb controls.
            int floorTop = Py(0f);
            c.Rect(0, floorTop - 6, w, 6, theme.tileCap);
            c.Rect(0, floorTop - 1, w, 1, lit);
            c.Rect(0, floorTop - 6, w, 1, Palette.Shade(theme.tileCap, 0.7f));
            c.Rect(0, floorTop - 7, w, 1, outline);
            c.Rect(0, 0, w, floorTop - 7, theme.strip);
        }
    }
}
