using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// World 5: a crypt wall. No sky: a dark stone wall in 6 bands, a row of tall pointed arches (far), shelves of small
    /// dim skulls (near), six pale wall lamps on the pillars and a few cracks. Flat colours and hard edges, nothing
    /// bright in the middle of the screen. Every colour comes from the theme.
    /// </summary>
    public static class CatacombsBackground
    {
        const int TilePixels = 15;
        const int ArchWidth = 60, PillarWidth = 12, ArchPitch = ArchWidth + PillarWidth;
        const int ArchSpring = 58;           // straight sides up to here
        const int ArchHeight = 110;          // spring + the rise of the pointed top
        const int LintelHeight = 14;         // solid wall above the arches
        const int SkullSize = 5, SkullStep = 7;

        public static void Paint(PixelCanvas c, Theme theme, System.Random rng)
        {
            var t = Tuning.I;
            int ppu = t.pixelsPerUnit;
            int w = c.Width, h = c.Height;
            int Px(float wx) => Mathf.RoundToInt((wx - ThemeBackgrounds.Left) * ppu);
            int Py(float wy) => Mathf.RoundToInt((wy - ThemeBackgrounds.Bottom) * ppu);
            int floorY = Py(0f);
            int centre = Px(0f);

            // Wall: 6 flat bands from skyBottom at the floor to skyTop above the arena.
            int horizon = Py(2f), topY = Py(t.arenaHeight + 2f);
            for (int y = floorY; y < h; y++)
            {
                float k = Mathf.Clamp01((float)(y - horizon) / (topY - horizon));
                int band = Mathf.Min(5, Mathf.FloorToInt(k * 6f));
                c.Rect(0, y, w, 1, Palette.Mix(theme.skyBottom, theme.skyTop, band / 5f));
            }

            // Far layer: the arcade. Solid stone up to the top of the arches, then each arch is cut out in skyTop.
            c.Rect(0, floorY, w, ArchHeight + LintelHeight, theme.far);
            for (int x = 0; x < w; x++)
            {
                int dx = Mathf.Abs(x - centre) % ArchPitch; // 0 at an arch centre, ArchPitch / 2 at a pillar centre
                if (dx > ArchPitch - dx) dx = ArchPitch - dx;
                if (dx >= ArchWidth / 2) continue; // pillar
                int open = ArchOpeningHeight(dx);
                if (open > 0) c.Rect(x, floorY, 1, open, theme.skyTop);
            }

            // Cracks: thin wandering lines across the wall and the pillars.
            for (int i = 0; i < 7; i++)
            {
                int x = rng.Next(Px(-t.HalfWidth) - 40, Px(t.HalfWidth) + 40);
                int y = rng.Next(floorY + ArchHeight - 20, Py(t.arenaHeight));
                int length = rng.Next(20, 38);
                for (int s = 0; s < length; s++)
                {
                    c.Set(x, y, theme.skyTop);
                    y--;
                    int roll = rng.Next(4);
                    if (roll == 0) x--; else if (roll == 1) x++;
                    else c.Set(x, y, theme.skyTop);
                }
            }

            // Wall lamps: one on each of the six pillars nearest the middle.
            for (int i = -3; i < 3; i++)
                Lamp(c, theme, centre + ArchPitch / 2 + i * ArchPitch, floorY + 66);

            // Near layer: two shelves of skulls on each side of the arena. The middle of the screen stays empty.
            int quiet = 130;
            int[] shelfHeights = { 14, 40 };
            foreach (int rise in shelfHeights)
                for (int x = 0; x < w; x += SkullStep)
                {
                    if (Mathf.Abs(x - centre) < quiet) continue;
                    if (rng.NextDouble() < 0.12) continue; // a gap here and there
                    Skull(c, theme, x, floorY + rise);
                }
            foreach (int rise in shelfHeights)
            {
                // The shelf boards the skulls sit on (with the 3 px board under each row).
                c.Rect(0, floorY + rise - 3, centre - quiet, 3, theme.near);
                c.Rect(centre + quiet, floorY + rise - 3, w - centre - quiet, 3, theme.near);
            }

            ThemeBackgrounds.PaintedFrame(c, theme);
        }

        /// <summary>
        /// Tile detail: every third tile gets a 1 px chip out of one top corner, every fifth a 3 x 3 skull mark. Only
        /// solid platform tiles; breakables keep their brick pattern. (x, y) is the tile's bottom-left pixel, so the tile
        /// index is recovered from the column and row.
        /// </summary>
        public static void PaintTileDetail(PixelCanvas c, int x, int y, Cell cell, bool openBelow)
        {
            if (cell.kind != TileKind.Solid) return;
            int column = x / TilePixels, row = y / TilePixels;
            int index = row * StageMap.Cols + column;
            Color32 shade = Themes.ForWorld(5).tileShade;
            if (index % 3 == 0)
                c.Set(x + (index / 3 % 2 == 0 ? 0 : TilePixels - 1), y + TilePixels - 1, shade);
            if (index % 5 == 0)
            {
                // A 3 x 3 skull: flat top, a nose bridge, and a jaw. The eyes are left as stone.
                c.Rect(x + 6, y + 6, 3, 1, shade);
                c.Set(x + 7, y + 5, shade);
                c.Set(x + 7, y + 4, shade);
            }
        }

        /// <summary>Height of the arch opening at dx pixels from its centre (0 when outside). Two arcs of radius 60 form the point.</summary>
        static int ArchOpeningHeight(int dx)
        {
            if (dx >= ArchWidth / 2) return 0;
            // The right arc is centred on the left spring point (-30, 0) with radius 60, so x = -30 + sqrt(60^2 - rise^2).
            // Solve for the rise at this dx: rise = sqrt(60^2 - (dx + 30)^2).
            float run = dx + ArchWidth * 0.5f;
            float rise = Mathf.Sqrt(Mathf.Max(0f, ArchWidth * ArchWidth - run * run));
            return ArchSpring + Mathf.FloorToInt(rise);
        }

        static void Lamp(PixelCanvas c, Theme theme, int x, int y)
        {
            // A small bracket in the near colour, then a 3 x 4 pale flame with a brighter core.
            c.Rect(x - 2, y - 3, 5, 1, theme.near);
            c.Rect(x - 1, y - 2, 3, 1, theme.near);
            Color32 flame = Palette.Mix(theme.accent, theme.skyBottom, 0.3f);
            c.Rect(x - 1, y - 1, 3, 4, flame);
            c.Set(x, y, theme.accent);
        }

        /// <summary>A 5 x 5 rounded square with two 1 px eye dots, sitting on a shelf at y.</summary>
        static void Skull(PixelCanvas c, Theme theme, int x, int y)
        {
            Color32 bone = Palette.Mix(theme.far, theme.tileCap, 0.3f);
            c.Rect(x + 1, y, SkullSize - 2, SkullSize, bone);
            c.Rect(x, y + 1, SkullSize, SkullSize - 2, bone);
            c.Set(x + 1, y + 2, theme.skyTop);
            c.Set(x + 3, y + 2, theme.skyTop);
        }
    }
}
