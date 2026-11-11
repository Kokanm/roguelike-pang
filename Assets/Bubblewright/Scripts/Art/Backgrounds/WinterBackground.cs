using UnityEngine;

namespace Bubblewright
{
    /// <summary>World 4: aurora over snow-capped mountains, low hills and snowy pines.</summary>
    public static class WinterBackground
    {
        public static void Paint(PixelCanvas c, Theme theme, System.Random rng)
        {
            int ppu = Tuning.I.pixelsPerUnit;
            int Py(float worldY) => Mathf.RoundToInt((worldY - ThemeBackgrounds.Bottom) * ppu);
            int floorY = Py(0f);

            ThemeBackgrounds.PaintSky(c, theme);

            Color32 aurora = Palette.Mix(theme.accent, theme.skyTop, 0.8f);
            for (int ribbon = 0; ribbon < 3; ribbon++)
            {
                int baseY = Py(10.4f + ribbon * 1.35f);
                double phase = rng.NextDouble() * System.Math.PI * 2.0;
                for (int x = 0; x < c.Width; x++)
                {
                    int y = baseY + Mathf.RoundToInt((float)System.Math.Sin(x * (0.013 + ribbon * 0.002) + phase) * 3f);
                    c.Rect(x, y, 1, 2, aurora);
                }
            }

            for (int i = 0; i < 40; i++)
                c.Set(rng.Next(c.Width), rng.Next(floorY + 8, c.Height), theme.accent);

            PaintMountains(c, theme, rng, floorY);
            PaintHills(c, theme.near, Palette.Mix(theme.near, theme.accent, 0.5f), rng, floorY);

            Color32 branchSnow = Palette.Mix(theme.near, theme.accent, 0.4f);
            for (int i = 0; i < 6; i++)
            {
                int x = (i + 1) * c.Width / 7 + rng.Next(-18, 19);
                Pine(c, x, floorY + rng.Next(10, 19), rng.Next(22, 34), theme.near, branchSnow);
            }

            ThemeBackgrounds.PaintedFrame(c, theme);
        }

        public static void PaintTileDetail(PixelCanvas c, int x, int y, Cell cell)
        {
            if (cell.kind != TileKind.Solid || cell.surface != Surface.Normal) return;
            Color32 snow = Themes.ForWorld(4).tileCap;
            for (int px = 1; px < 14; px += 2)
                c.Set(x + px, y + 11, snow);
            foreach (int icicleX in new[] { 4, 10 })
                c.Rect(x + icicleX, y + 9, 1, 2, snow);
        }

        static void PaintMountains(PixelCanvas c, Theme theme, System.Random rng, int floorY)
        {
            Color32 snow = Palette.Mix(theme.far, theme.accent, 0.5f);
            int segmentStart = -20;
            while (segmentStart < c.Width)
            {
                int width = rng.Next(75, 141);
                int peakX = segmentStart + rng.Next(width / 3, width * 2 / 3);
                int peakY = floorY + rng.Next(48, 80);
                int leftY = floorY + rng.Next(20, 34);
                int rightY = floorY + rng.Next(20, 34);
                for (int x = segmentStart; x <= segmentStart + width; x++)
                {
                    float k = x <= peakX
                        ? Mathf.InverseLerp(segmentStart, peakX, x)
                        : (float)(segmentStart + width - x) / (segmentStart + width - peakX);
                    int edgeY = x <= peakX ? leftY : rightY;
                    int top = Mathf.RoundToInt(Mathf.Lerp(edgeY, peakY, k));
                    c.Rect(x, floorY, 1, top - floorY + 1, theme.far);
                    c.Rect(x, top - 5, 1, 6, snow);
                }
                segmentStart += width;
            }
        }

        static void PaintHills(PixelCanvas c, Color32 color, Color32 snow, System.Random rng, int floorY)
        {
            double phaseA = rng.NextDouble() * System.Math.PI * 2.0;
            double phaseB = rng.NextDouble() * System.Math.PI * 2.0;
            for (int x = 0; x < c.Width; x++)
            {
                float wave = (float)(System.Math.Sin(x * 0.009 + phaseA) * 7.0 +
                                     System.Math.Sin(x * 0.021 + phaseB) * 3.0);
                int top = floorY + 20 + Mathf.RoundToInt(wave);
                c.Rect(x, floorY, 1, top - floorY + 1, color);
                c.Rect(x, top - 1, 1, 2, snow);
            }
        }

        static void Pine(PixelCanvas c, int x, int y, int height, Color32 tree, Color32 snow)
        {
            c.Rect(x - 1, y, 2, height / 3, tree);
            for (int tier = 0; tier < 3; tier++)
            {
                int bottom = y + tier * height / 5;
                int tierHeight = height * 2 / 5;
                int halfWidth = 8 - tier * 2;
                for (int row = 0; row < tierHeight; row++)
                {
                    int half = Mathf.Max(0, halfWidth - row * halfWidth / tierHeight);
                    c.Rect(x - half, bottom + row, half * 2 + 1, 1, tree);
                }
                c.Rect(x - halfWidth, bottom, halfWidth * 2 + 1, 1, snow);
            }
        }
    }
}
