using UnityEngine;

namespace Bubblewright
{
    /// <summary>World 3: a starry dusk over layered dunes, cactus silhouettes and a ruined column.</summary>
    public static class DesertBackground
    {
        public static void Paint(PixelCanvas c, Theme theme, System.Random rng)
        {
            int ppu = Tuning.I.pixelsPerUnit;
            int Px(float worldX) => Mathf.RoundToInt((worldX - ThemeBackgrounds.Left) * ppu);
            int Py(float worldY) => Mathf.RoundToInt((worldY - ThemeBackgrounds.Bottom) * ppu);
            int floorY = Py(0f);

            ThemeBackgrounds.PaintSky(c, theme);

            Color32 star = Palette.Mix(theme.accent, theme.skyTop, 0.6f);
            for (int i = 0; i < 30; i++)
                c.Set(rng.Next(c.Width), rng.Next(Py(9.5f), Py(14f)), star);

            int sunX = Px(8f), sunY = floorY + 25;
            Color32 sun = Palette.Mix(theme.far, theme.accent, 0.3f);
            c.Circle(sunX, sunY, 20f, sun);
            c.Rect(sunX - 20, sunY + 2, 40, 1, theme.skyBottom);
            c.Rect(sunX - 19, sunY + 7, 38, 1, theme.skyBottom);

            Color32 crest = Palette.Mix(theme.far, theme.accent, 0.25f);
            Dunes(c, rng, theme.far, crest, floorY, 30, 12f, 0.010f);
            Dunes(c, rng, theme.near, theme.near, floorY, 17, 9f, 0.014f);

            int[] cactusX = { Px(-22f), Px(-9f), Px(11f), Px(24f) };
            foreach (int cx in cactusX)
                Cactus(c, cx, floorY + rng.Next(10, 18), rng.Next(20, 34), theme.near, rng);

            int columnX = Px(1.5f);
            c.Rect(columnX, floorY + 12, 8, 31, theme.near);
            c.Rect(columnX - 3, floorY + 39, 14, 5, theme.near);
            c.Rect(columnX - 1, floorY + 44, 10, 3, theme.near);
            c.Rect(columnX + 5, floorY + 44, 4, 2, theme.skyBottom);

            ThemeBackgrounds.PaintedFrame(c, theme);
        }

        public static void PaintTileDetail(PixelCanvas c, int x, int y, Cell cell)
        {
            if (cell.kind != TileKind.Solid) return;
            Color32 joint = Themes.ForWorld(3).tileShade;
            c.Rect(x + 1, y + 8, 13, 1, joint);
            if ((x / 15) % 2 == 0)
                c.Rect(x + 7, y + 1, 1, 11, joint);
        }

        static void Dunes(PixelCanvas c, System.Random rng, Color32 fill, Color32 crest, int floorY,
            int baseHeight, float amplitude, float frequency)
        {
            double phaseA = rng.NextDouble() * System.Math.PI * 2.0;
            double phaseB = rng.NextDouble() * System.Math.PI * 2.0;
            int previousTop = floorY;
            for (int x = 0; x < c.Width; x++)
            {
                float waveA = (float)System.Math.Sin(x * frequency + phaseA);
                float waveB = (float)System.Math.Sin(x * frequency * 0.47 + phaseB);
                int top = floorY + baseHeight + Mathf.RoundToInt(amplitude * (waveA * 0.65f + waveB * 0.35f));
                c.Rect(x, floorY, 1, Mathf.Max(1, top - floorY), fill);
                c.Set(x, top, crest);
                if (x > 0 && Mathf.Abs(top - previousTop) > 1)
                    c.Set(x, (top + previousTop) / 2, crest);
                previousTop = top;
            }
        }

        static void Cactus(PixelCanvas c, int x, int y, int height, Color32 color, System.Random rng)
        {
            c.Rect(x, y, 3, height, color);
            int armY = y + height / 2;
            int direction = rng.Next(2) == 0 ? -1 : 1;
            c.Rect(direction < 0 ? x - 7 : x + 3, armY, 7, 3, color);
            c.Rect(direction < 0 ? x - 7 : x + 7, armY, 3, 9, color);
            if (height > 26)
            {
                int otherY = y + height * 2 / 3;
                c.Rect(direction < 0 ? x + 3 : x - 5, otherY, 5, 3, color);
                c.Rect(direction < 0 ? x + 5 : x - 5, otherY, 3, 6, color);
            }
        }
    }
}
