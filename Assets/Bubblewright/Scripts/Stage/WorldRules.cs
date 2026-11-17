using System;
using System.Collections.Generic;

namespace Bubblewright
{
    /// <summary>
    /// Debug helpers for the hazards that belong to one world. PaintAll paints a whole map one surface.
    /// PlaceSpikes grows Lava's retracting strips. Authored stages set their own sand, ice and spikes.
    /// </summary>
    public static class WorldRules
    {
        public const int DesertWorld = 3;
        public const int WinterWorld = 4;
        public const int LavaWorld = 6;

        // Wick starts on columns 14 to 17. On the ground, keep three columns clear on each side of that.
        const int GroundClearFirst = 11, GroundClearLast = 20;

        struct Spot
        {
            public int col, row, width;
        }

        /// <summary>Every ground column and every solid tile gets this surface.</summary>
        public static void PaintAll(StageMap map, Surface surface)
        {
            for (int c = 0; c < StageMap.Cols; c++) map.SetGround(c, surface);
            for (int r = 0; r < map.Rows; r++)
                for (int c = 0; c < StageMap.Cols; c++)
                    SetTileSurface(map, c, r, surface);
        }

        /// <summary>
        /// Lava strips: 2 on stages 1 and 2, 3 on stages 3 and 4, 4 on stage 5. Each is 2 or 3 tiles,
        /// on the ground or a platform. Every legal spot is listed and one is picked at random, so a
        /// strip is skipped only when the rules leave nowhere to stand.
        /// </summary>
        public static void PlaceSpikes(StageMap map, int stageInWorld, Random rng)
        {
            int wanted = stageInWorld <= 2 ? 2 : stageInWorld <= 4 ? 3 : 4;
            var ground = new List<Spot>(64);
            var platform = new List<Spot>(64);
            int placed = 0;
            for (int n = 0; n < wanted; n++)
            {
                CollectSpots(map, ground, platform);
                List<Spot> pool = PickPool(rng, ground, platform);
                if (pool == null) continue;
                Spot spot = pool[rng.Next(pool.Count)];
                map.spikes.Add(new StageMap.SpikeStrip
                {
                    col = spot.col,
                    row = spot.row,
                    width = spot.width,
                    phase = placed * Tuning.I.spikePhaseStep,
                });
                placed++;
            }
        }

        static void SetTileSurface(StageMap map, int c, int r, Surface surface)
        {
            if (!map.IsSolid(c, r)) return;
            Cell cell = map.Get(c, r);
            cell.surface = surface;
            map.Set(c, r, cell);
        }

        /// <summary>Ground or platform, 50/50 when both have a legal spot. Null when neither does.</summary>
        static List<Spot> PickPool(Random rng, List<Spot> ground, List<Spot> platform)
        {
            if (ground.Count == 0 && platform.Count == 0) return null;
            bool wantGround = rng.Next(2) == 0;
            if (wantGround && ground.Count > 0) return ground;
            if (!wantGround && platform.Count > 0) return platform;
            return ground.Count > 0 ? ground : platform;
        }

        static void CollectSpots(StageMap map, List<Spot> ground, List<Spot> platform)
        {
            ground.Clear();
            platform.Clear();
            for (int width = 2; width <= 3; width++)
            {
                for (int col = 0; col <= StageMap.Cols - width; col++)
                    if (Valid(map, col, -1, width))
                        ground.Add(new Spot { col = col, row = -1, width = width });

                for (int row = 0; row < map.Rows; row++)
                {
                    int col = 0;
                    while (col < StageMap.Cols)
                    {
                        if (!map.IsSolid(col, row)) { col++; continue; }
                        int start = col;
                        while (col < StageMap.Cols && map.IsSolid(col, row)) col++;
                        for (int c = start; c <= col - width; c++)
                            if (Valid(map, c, row, width))
                                platform.Add(new Spot { col = c, row = row, width = width });
                    }
                }
            }
        }

        static bool Valid(StageMap map, int col, int row, int width)
        {
            for (int c = col; c < col + width; c++)
            {
                if (NearLadder(map, c)) return false;
                if (row < 0)
                {
                    if (c >= GroundClearFirst && c <= GroundClearLast) return false;
                    if (map.IsSolid(c, 0)) return false;
                    if (map.IsGap(c)) return false;
                }
                else if (!map.IsSolid(c, row) || map.IsSolid(c, row + 1))
                    return false;
            }
            if (Touches(map, col, row, width)) return false;
            if (row >= 0 && !LeavesPath(map, col, row, width)) return false;
            return true;
        }

        static bool NearLadder(StageMap map, int c)
        {
            for (int col = c - 1; col <= c + 1; col++)
            {
                if (col < 0 || col >= StageMap.Cols) continue;
                for (int r = 0; r < map.Rows; r++)
                    if (map.Get(col, r).ladder) return true;
            }
            return false;
        }

        /// <summary>Same surface, overlapping or sharing an edge. A one-column gap is enough.</summary>
        static bool Touches(StageMap map, int col, int row, int width)
        {
            foreach (var spike in map.spikes)
            {
                if (spike.row != row) continue;
                if (col <= spike.col + spike.width && spike.col <= col + width) return true;
            }
            return false;
        }

        /// <summary>A platform strip must leave at least 3 tiles of its platform free to walk.</summary>
        static bool LeavesPath(StageMap map, int col, int row, int width)
        {
            int start = col;
            while (start > 0 && map.IsSolid(start - 1, row)) start--;
            int end = col + width;
            while (end < StageMap.Cols && map.IsSolid(end, row)) end++;

            int covered = width;
            foreach (var spike in map.spikes)
            {
                if (spike.row != row) continue;
                int a = Math.Max(spike.col, start);
                int b = Math.Min(spike.col + spike.width, end);
                if (b > a) covered += b - a;
            }
            return end - start - covered >= 3;
        }
    }
}
