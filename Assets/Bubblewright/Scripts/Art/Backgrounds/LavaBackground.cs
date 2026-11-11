using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// World 6: a volcano night. Sky in 6 bands, a notched volcano with a smoke column (far), jagged rock spires at
    /// both sides (near) and a low band of red lava along the bottom fifth of the arena. The red glow never goes above
    /// that band apart from a few single ember pixels, so coral bubbles are never seen against it for long.
    /// </summary>
    public static class LavaBackground
    {
        const int VolcanoHeight = 110, VolcanoBaseHalf = 170, VolcanoTopHalf = 18, NotchDepth = 8, NotchHalf = 7;
        const int LavaHeight = 32;           // inside the bottom fifth of the 14 unit arena (42 px)
        const int QuietHalfWidth = 150;      // no rock spires within this many pixels of the centre
        const int EmberCount = 24;

        public static void Paint(PixelCanvas c, Theme theme, System.Random rng)
        {
            var t = Tuning.I;
            int ppu = t.pixelsPerUnit;
            int w = c.Width, h = c.Height;
            int Px(float wx) => Mathf.RoundToInt((wx - ThemeBackgrounds.Left) * ppu);
            int Py(float wy) => Mathf.RoundToInt((wy - ThemeBackgrounds.Bottom) * ppu);
            int floorY = Py(0f);
            int centre = Px(0f);

            // Sky: 6 flat bands, deep red at the horizon up to near-black.
            int horizon = Py(2f), topY = Py(t.arenaHeight + 2f);
            for (int y = floorY; y < h; y++)
            {
                float k = Mathf.Clamp01((float)(y - horizon) / (topY - horizon));
                int band = Mathf.Min(5, Mathf.FloorToInt(k * 6f));
                c.Rect(0, y, w, 1, Palette.Mix(theme.skyBottom, theme.skyTop, band / 5f));
            }

            // Far layer: the volcano, slightly right of centre, with a notch in its top and a column of smoke.
            int vx = Px(3.5f);
            for (int dx = -VolcanoBaseHalf; dx <= VolcanoBaseHalf; dx++)
            {
                float u = Mathf.Clamp01((Mathf.Abs(dx) - VolcanoTopHalf) / (float)(VolcanoBaseHalf - VolcanoTopHalf));
                // Concave flanks: steep near the top, flattening toward the foot.
                int height = Mathf.RoundToInt(VolcanoHeight * (1f - Mathf.Pow(u, 0.7f)));
                if (Mathf.Abs(dx) < NotchHalf) height -= NotchDepth - Mathf.Abs(dx);
                if (height > 0) c.Rect(vx + dx, floorY, 1, height, theme.far);
            }
            Color32 smoke = Palette.Mix(theme.far, theme.skyTop, 0.4f);
            float sx = vx, sy = floorY + VolcanoHeight - 2;
            for (int i = 0; i < 9; i++)
            {
                int size = 6 + (i * 4) / 8;                 // 6 px at the crater, 10 px at the top
                c.Rect(Mathf.RoundToInt(sx) - size / 2, Mathf.RoundToInt(sy), size, size, smoke);
                sy += size - 1;
                sx -= 4f + (float)rng.NextDouble() * 3f;    // drifts left as it rises
            }

            // Lava band along the bottom fifth: darker red, a brighter line on top, a few 2 px bubbles.
            c.Rect(0, floorY, w, LavaHeight, Palette.Shade(theme.accent, 0.6f));
            c.Rect(0, floorY + LavaHeight - 1, w, 1, theme.accent);
            for (int i = 0; i < 6; i++)
            {
                int bx = centre - 130 + i * 52 + rng.Next(-10, 11);
                int by = floorY + 8 + rng.Next(0, 16);
                c.Rect(bx, by, 2, 2, theme.accent);
            }

            // Near layer: jagged spires from the bottom edge, in front of the lava, at both sides of the screen.
            for (int x = 0; x < w; )
            {
                int spireWidth = rng.Next(14, 27);
                int spireHeight = rng.Next(28, 76);
                if (Mathf.Abs(x + spireWidth / 2 - centre) >= QuietHalfWidth) Spire(c, theme.near, x, floorY, spireWidth, spireHeight, rng);
                x += spireWidth - rng.Next(0, 6);
            }

            // Embers: single pixels in the lower half of the arena. Never next to the lava band or each other.
            int placed = 0;
            for (int tries = 0; tries < 400 && placed < EmberCount; tries++)
            {
                int x = rng.Next(0, w);
                int y = rng.Next(floorY + LavaHeight + 4, Py(t.arenaHeight * 0.5f));
                if (!IsClear(c, x, y, theme.accent)) continue;
                c.Set(x, y, theme.accent);
                placed++;
            }

            ThemeBackgrounds.Frame(c, theme, Palette.Mix(theme.tileCap, theme.tileFill, 0.35f));
        }

        /// <summary>A tapering spire with a jagged right edge, base on the floor line.</summary>
        static void Spire(PixelCanvas c, Color32 color, int x, int floorY, int width, int height, System.Random rng)
        {
            int tip = width / 2 + rng.Next(-2, 3);
            for (int dx = 0; dx < width; dx++)
            {
                float u = dx <= tip ? dx / (float)Mathf.Max(1, tip) : (width - 1 - dx) / (float)Mathf.Max(1, width - 1 - tip);
                int column = Mathf.RoundToInt(height * Mathf.Clamp01(u)) + (dx % 3 == 0 ? 1 : 0);
                if (column > 0) c.Rect(x + dx, floorY, 1, column, color);
            }
        }

        /// <summary>True when no accent pixel is in the 3 x 3 block around (x, y).</summary>
        static bool IsClear(PixelCanvas c, int x, int y, Color32 accent)
        {
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    var p = c.Get(x + i, y + j);
                    if (p.r == accent.r && p.g == accent.g && p.b == accent.b) return false;
                }
            return true;
        }
    }
}
