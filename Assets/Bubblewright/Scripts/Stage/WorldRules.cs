using System;

namespace Bubblewright
{
    /// <summary>
    /// What each world does to a freshly parsed stage map. Sand covers all of Desert, patchy ice covers Winter.
    /// Every other world is left alone, because each hazard belongs to one world only.
    /// </summary>
    public static class WorldRules
    {
        public const int DesertWorld = 3;
        public const int WinterWorld = 4;
        const int SafeFirstCol = 14, SafeLastCol = 17;
        const int MinStretch = 4, MaxStretch = 8;

        public static void Apply(StageMap map, Run run, Random rng) => Apply(map, run.World, rng);

        public static void Apply(StageMap map, int world, Random rng)
        {
            if (world == DesertWorld) PaintAll(map, Surface.Sand);
            else if (world == WinterWorld) PatchyIce(map, rng);
        }

        /// <summary>Every ground column and every solid tile gets this surface.</summary>
        public static void PaintAll(StageMap map, Surface surface)
        {
            for (int c = 0; c < StageMap.Cols; c++) map.SetGround(c, surface);
            for (int r = 0; r < StageMap.Rows; r++)
                for (int c = 0; c < StageMap.Cols; c++)
                    SetTileSurface(map, c, r, surface);
        }

        static void PatchyIce(StageMap map, Random rng)
        {
            // Ground: stretches of 4 to 8 columns, alternating normal and ice, starting with either.
            bool ice = rng.Next(2) == 0;
            int col = 0;
            while (col < StageMap.Cols)
            {
                int length = MinStretch + rng.Next(MaxStretch - MinStretch + 1);
                for (int i = 0; i < length && col < StageMap.Cols; i++, col++)
                    map.SetGround(col, ice ? Surface.Ice : Surface.Normal);
                ice = !ice;
            }
            // Wick starts around the middle, so never on ice.
            for (int c = SafeFirstCol; c <= SafeLastCol; c++) map.SetGround(c, Surface.Normal);

            // Platforms (a run of solid tiles in one row): each is ice with a 50% chance.
            for (int r = 0; r < StageMap.Rows; r++)
            {
                int c = 0;
                while (c < StageMap.Cols)
                {
                    if (!map.IsSolid(c, r)) { c++; continue; }
                    int start = c;
                    while (c < StageMap.Cols && map.IsSolid(c, r)) c++;
                    if (rng.Next(2) != 0) continue;
                    for (int i = start; i < c; i++) SetTileSurface(map, i, r, Surface.Ice);
                }
            }
        }

        static void SetTileSurface(StageMap map, int c, int r, Surface surface)
        {
            if (!map.IsSolid(c, r)) return;
            Cell cell = map.Get(c, r);
            cell.surface = surface;
            map.Set(c, r, cell);
        }
    }
}
