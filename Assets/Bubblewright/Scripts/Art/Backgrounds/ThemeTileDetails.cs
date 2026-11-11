using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Small per-theme details painted onto solid stage tiles by StageView, on top of the shared tile art. Catacombs:
    /// bone-stone with chipped corners and tiny skull marks. Lava: basalt with cracks and a faint red glow under
    /// platforms. Every position comes from the tile's column and row, so the same stage always looks the same.
    /// Breakable tiles are never touched: they keep their brick pattern.
    /// </summary>
    public static class ThemeTileDetails
    {
        const int TilePixels = 15;

        public static bool IsCatacombs(Theme theme) => theme == Themes.ForWorld(5);
        public static bool IsLava(Theme theme) => theme == Themes.ForWorld(6);

        /// <summary>(x0, y0) is the tile's bottom-left pixel in the buffer. Safe to call for every tile.</summary>
        public static void Paint(Theme theme, StageMap map, Color32[] buffer, int bufferWidth, int x0, int y0, int c, int r, Cell cell)
        {
            if (cell.kind != TileKind.Solid) return;
            int index = r * StageMap.Cols + c;
            if (IsCatacombs(theme)) Catacombs(theme, map, buffer, bufferWidth, x0, y0, c, r, index);
            else if (IsLava(theme)) Lava(theme, map, buffer, bufferWidth, x0, y0, c, r, index);
        }

        static void Catacombs(Theme theme, StageMap map, Color32[] buf, int bw, int x0, int y0, int c, int r, int index)
        {
            // Every third tile: a 1 px chip out of one top corner (left and right alternate).
            if (index % 3 == 0 && !map.IsSolid(c, r + 1))
                buf[(y0 + TilePixels - 1) * bw + x0 + ((index / 3) % 2 == 0 ? 0 : TilePixels - 1)] = theme.tileShade;

            // Every fifth tile: a 3 x 3 skull mark in the body, eyes left as stone.
            if (index % 5 == 0)
            {
                int sx = x0 + 6, sy = y0 + 3;
                for (int dx = 0; dx < 3; dx++) buf[(sy + 2) * bw + sx + dx] = theme.tileShade; // top of the skull
                buf[(sy + 1) * bw + sx + 1] = theme.tileShade;                                 // nose bridge; the sides are the eyes
                buf[(sy + 0) * bw + sx + 1] = theme.tileShade;                                 // jaw
            }
        }

        static void Lava(Theme theme, StageMap map, Color32[] buf, int bw, int x0, int y0, int c, int r, int index)
        {
            // Two 1 px vertical cracks, shifted a little from tile to tile.
            int a = 3 + index % 3, b = 9 + index % 3;
            for (int y = 2; y <= 9; y++) buf[(y0 + y) * bw + x0 + a] = theme.tileShade;
            for (int y = 1; y <= 7; y++) buf[(y0 + y) * bw + x0 + b] = theme.tileShade;

            // A faint red glow in the row just under a platform tile that has nothing beneath it.
            if (r > 0 && !map.IsSolid(c, r - 1))
            {
                Color32 glow = Palette.Shade(theme.accent, 0.7f);
                for (int x = 0; x < TilePixels; x++)
                {
                    int i = (y0 - 1) * bw + x0 + x;
                    if (buf[i].a == 0) buf[i] = glow;
                }
            }
        }
    }
}
