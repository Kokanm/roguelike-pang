using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>Patches of floor a Frost bubble turned to ice. Each returns to its old surface after Tuning.frostIceTime.</summary>
    public class TempIce
    {
        struct Spot
        {
            public bool ground;
            public int col;
            public int row;
            public Surface before;
            public float timeLeft;
        }

        readonly List<Spot> spots = new List<Spot>();

        public int Count => spots.Count;

        int Find(bool ground, int col, int row)
        {
            for (int i = 0; i < spots.Count; i++)
                if (spots[i].ground == ground && spots[i].col == col && spots[i].row == row) return i;
            return -1;
        }

        void Refresh(int index)
        {
            var s = spots[index];
            s.timeLeft = Tuning.I.frostIceTime;
            spots[index] = s;
        }

        public void FreezeGround(StageMap map, int col)
        {
            if (map == null || col < 0 || col >= StageMap.Cols || map.IsGap(col)) return;
            int i = Find(true, col, 0);
            if (i >= 0) { Refresh(i); return; }
            Surface before = map.GetGround(col);
            if (before == Surface.Ice) return; // permanent ice stays as it is
            map.SetGround(col, Surface.Ice);
            spots.Add(new Spot { ground = true, col = col, row = 0, before = before, timeLeft = Tuning.I.frostIceTime });
        }

        public void FreezeTile(StageMap map, int col, int row)
        {
            if (map == null || col < 0 || col >= StageMap.Cols || row < 0 || row >= StageMap.Rows) return;
            Cell cell = map.Get(col, row);
            if (cell.kind == TileKind.Empty) return;
            int i = Find(false, col, row);
            if (i >= 0) { Refresh(i); return; }
            if (cell.surface == Surface.Ice) return;
            Surface before = cell.surface;
            cell.surface = Surface.Ice;
            map.Set(col, row, cell);
            spots.Add(new Spot { ground = false, col = col, row = row, before = before, timeLeft = Tuning.I.frostIceTime });
        }

        public void Tick(StageMap map, float dt)
        {
            for (int i = spots.Count - 1; i >= 0; i--)
            {
                var s = spots[i];
                s.timeLeft -= dt;
                if (s.timeLeft > 0f) { spots[i] = s; continue; }
                spots.RemoveAt(i);
                if (map == null) continue;
                if (s.ground) map.SetGround(s.col, s.before);
                else
                {
                    Cell cell = map.Get(s.col, s.row);
                    if (cell.kind == TileKind.Empty) continue; // the tile broke meanwhile
                    cell.surface = s.before;
                    map.Set(s.col, s.row, cell);
                }
            }
        }

        /// <summary>Forget every spot without restoring (a new stage's map replaces the old one).</summary>
        public void Clear() => spots.Clear();

        /// <summary>Ice patch width as a multiple of the bubble's diameter.</summary>
        public const float PatchWidthFactor = 1.5f;

        /// <summary>
        /// Freeze what a bubble at centre with the given radius has just landed on: every column the patch touches,
        /// a patch PatchWidthFactor times the bubble's diameter wide and centred under it.
        /// </summary>
        public void FreezeUnder(StageMap map, Vector2 centre, float radius, float stageTime, float wickStartX)
        {
            if (map == null) return;
            float lowest = centre.y - radius;
            float half = radius * PatchWidthFactor; // half of 1.5 x diameter
            int first = StageMap.Col(centre.x - half);
            int last = StageMap.Col(centre.x + half);
            bool onGround = Mathf.Abs(lowest) <= 0.05f;
            int row = StageMap.Row(lowest - 0.5f);
            var T = Tuning.I;
            for (int col = first; col <= last; col++)
            {
                if (onGround)
                {
                    if (stageTime < T.frostSafeTime && Mathf.Abs(col - StageMap.Col(wickStartX)) <= T.frostSafeHalfWidth) continue;
                    FreezeGround(map, col);
                }
                else FreezeTile(map, col, row);
            }
        }
    }
}
