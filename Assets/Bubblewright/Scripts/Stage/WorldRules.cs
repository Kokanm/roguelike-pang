using System;
using System.Collections.Generic;

namespace Bubblewright
{
    /// <summary>
    /// What each world does to a freshly parsed stage map. Sand covers all of Desert, patchy ice covers Winter,
    /// and Lava (not its boss stage) grows retracting spike strips. Desert also narrows gaps in the ground to 2 columns. Every other world is left alone, because
    /// each hazard belongs to one world only.
    /// </summary>
    public static class WorldRules
    {
        public const int DesertWorld = 3;
        public const int WinterWorld = 4;
        public const int LavaWorld = 6;
        const int SafeFirstCol = 14, SafeLastCol = 17;
        const int MinStretch = 4, MaxStretch = 8;

        // Wick starts on columns 14 to 17. On the ground, keep three columns clear on each side of that.
        const int GroundClearFirst = 11, GroundClearLast = 20;

        struct Spot
        {
            public int col, row, width;
        }

        public static void Apply(StageMap map, Run run, Random rng) =>
            Apply(map, run.World, rng, run.StageInWorld, run.IsBossStage);

        public static void Apply(StageMap map, int world, Random rng, int stageInWorld = 0, bool boss = false)
        {
            NarrowGaps(map, MaxGap(world));
            if (world == DesertWorld) PaintAll(map, Surface.Sand);
            else if (world == WinterWorld) PatchyIce(map, rng);
            if (world == LavaWorld && !boss && stageInWorld >= 1 && stageInWorld <= 5)
                PlaceSpikes(map, stageInWorld, rng);
        }

        /// <summary>Widest gap in the ground a world allows. On sand the jump is shorter, so Desert gaps are 2 columns at most.</summary>
        public static int MaxGap(int world) => world == DesertWorld ? 2 : 3;

        /// <summary>Fills in the right-hand end of any gap wider than maxGap columns.</summary>
        public static void NarrowGaps(StageMap map, int maxGap)
        {
            int c = 0;
            while (c < StageMap.Cols)
            {
                if (!map.IsGap(c)) { c++; continue; }
                int start = c;
                while (c < StageMap.Cols && map.IsGap(c)) c++;
                for (int i = start + maxGap; i < c; i++) map.SetGap(i, false);
            }
        }

        /// <summary>Every ground column and every solid tile gets this surface.</summary>
        public static void PaintAll(StageMap map, Surface surface)
        {
            for (int c = 0; c < StageMap.Cols; c++) map.SetGround(c, surface);
            for (int r = 0; r < StageMap.Rows; r++)
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

                for (int row = 0; row < StageMap.Rows; row++)
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
                for (int r = 0; r < StageMap.Rows; r++)
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
