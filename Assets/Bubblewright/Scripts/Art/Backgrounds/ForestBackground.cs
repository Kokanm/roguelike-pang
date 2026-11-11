using UnityEngine;

namespace Bubblewright
{
    /// <summary>World 2: a moonlit pine forest with edge trunks, bushes and fireflies.</summary>
    public static class ForestBackground
    {
        public static void Paint(PixelCanvas c, Theme theme, System.Random rng)
        {
            int ppu = Tuning.I.pixelsPerUnit;
            int Px(float worldX) => Mathf.RoundToInt((worldX - ThemeBackgrounds.Left) * ppu);
            int Py(float worldY) => Mathf.RoundToInt((worldY - ThemeBackgrounds.Bottom) * ppu);
            int floorY = Py(0f);

            ThemeBackgrounds.PaintSky(c, theme);

            Color32 moon = Palette.Mix(theme.accent, theme.skyBottom, 0.5f);
            c.Circle(Px(-14.7f), Py(10.8f), 7f, moon);

            int x = -12;
            while (x < c.Width + 12)
            {
                int height = rng.Next(30, 71);
                int width = Mathf.Max(13, height / 2);
                int baseY = floorY - rng.Next(2, 9);
                Pine(c, x, baseY, width, height, theme.far);
                x += rng.Next(20, 39);
            }

            int[] trunks = { 8, Px(-15.2f), Px(15.2f), c.Width - 18 };
            foreach (int trunkX in trunks)
            {
                int width = rng.Next(6, 11);
                c.Rect(trunkX, floorY - 4, width, c.Height - floorY + 4, theme.near);
                int direction = trunkX < c.Width / 2 ? 1 : -1;
                for (int branch = 0; branch < 3; branch++)
                {
                    int by = floorY + 58 + branch * 53 + rng.Next(-7, 8);
                    int length = rng.Next(9, 18);
                    c.Rect(direction > 0 ? trunkX + width : trunkX - length, by, length, 3, theme.near);
                    c.Rect(direction > 0 ? trunkX + width + length - 4 : trunkX - length, by + 3, 4, 3, theme.near);
                }
            }

            for (int bx = -10; bx < c.Width + 12; bx += rng.Next(12, 22))
            {
                int radius = rng.Next(7, 13);
                c.Circle(bx, floorY + rng.Next(0, 5), radius, theme.near);
            }
            c.Rect(0, floorY - 3, c.Width, 4, theme.near);

            Color32 dimFirefly = Palette.Mix(theme.far, theme.accent, 0.35f);
            for (int i = 0; i < 18; i++)
            {
                int fx = rng.Next(5, c.Width - 6);
                int fy = rng.Next(floorY + 8, Py(7f));
                c.Set(fx, fy, theme.accent);
                c.Set(fx + (rng.Next(2) == 0 ? -1 : 1), fy, dimFirefly);
            }

            ThemeBackgrounds.PaintedFrame(c, theme);
        }

        public static void PaintTileDetail(PixelCanvas c, int x, int y, Cell cell)
        {
            if (cell.kind != TileKind.Solid || cell.surface != Surface.Normal) return;
            int[] positions = { 3, 8, 12 };
            for (int i = 0; i < positions.Length; i++)
                c.Rect(x + positions[i], y + 11 - i, 1, i + 1, Themes.ForWorld(2).tileCap);
        }

        static void Pine(PixelCanvas c, int cx, int baseY, int width, int height, Color32 color)
        {
            c.Rect(cx - 1, baseY, 2, Mathf.Max(5, height / 4), color);
            int tiers = height > 48 ? 4 : 3;
            for (int tier = 0; tier < tiers; tier++)
            {
                int tierBottom = baseY + height * tier / (tiers + 2);
                int tierHeight = height * 2 / (tiers + 2);
                int halfWidth = Mathf.Max(3, width * (tiers - tier) / (tiers * 2));
                for (int row = 0; row < tierHeight; row++)
                {
                    int half = Mathf.Max(0, halfWidth - row * halfWidth / tierHeight);
                    c.Rect(cx - half, tierBottom + row, half * 2 + 1, 1, color);
                }
            }
        }
    }
}
