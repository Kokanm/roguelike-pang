using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    public enum PipeSpot { TopLeft, TopRight, TopMiddle }

    /// <summary>A balloon pipe finished its flash: put a Gloop of this tier here, heading in dir.</summary>
    public struct PipeLaunch
    {
        public PipeSpot spot;
        public int tier;
        public int storey;
        public Vector2 position;
        public int dir;
    }

    /// <summary>
    /// What a boss can do to its arena, as plain maths on one StageMap: break any tile, add short-lived tiles, open the
    /// floor, raise a hazard line, change a surface, fire a balloon pipe, and open the hatch a phase unlocks. Game owns
    /// one per stage (Game.Arena.cs) and ticks it with game time, so pause, hit-stop and slow motion freeze it. Every
    /// change lives on that stage's map, so the next stage starts clean. Shots never come through here: they still
    /// break only breakable blocks (StageMap.Damage).
    /// </summary>
    public sealed class ArenaEvents
    {
        struct TempTile
        {
            public int col;
            public int row;
            public float timeLeft;
        }

        struct FlashingPipe
        {
            public PipeSpot spot;
            public int tier;
            public int storey;
            public float timeLeft;
        }

        readonly StageMap map;
        readonly List<TempTile> temps = new List<TempTile>();
        readonly List<FlashingPipe> pipes = new List<FlashingPipe>();
        readonly bool[] hatchUnlocked = new bool[StageMap.MaxStoreys];

        float hazardSpeed, hazardStop;

        public ArenaEvents(StageMap map) { this.map = map; }

        /// <summary>A tile was broken by BreakTile: col, row. Game adds debris and sound.</summary>
        public Action<int, int> OnTileBroken;
        /// <summary>
        /// Asks whether a cell may not take a new temporary tile right now (Game says yes when Wick stands in it,
        /// so a block never appears inside him). Null means anything goes.
        /// </summary>
        public Func<int, int, bool> TileBlocked;
        /// <summary>A temporary tile ran out (it is already gone from the map): col, row. For debris and sound.</summary>
        public Action<int, int> OnTempExpire;
        /// <summary>A pipe finished flashing. The game spawns the Gloop.</summary>
        public Action<PipeLaunch> OnPipe;

        // ---------------------------------------------------------------- tiles

        /// <summary>Breaks any tile, solid or breakable, whatever its health. A ladder in the tile stays. False if the cell was empty.</summary>
        public bool BreakTile(int c, int r)
        {
            if (map == null || c < 0 || c >= StageMap.Cols || r < 0 || r >= map.Rows) return false;
            var cell = map.Get(c, r);
            if (cell.kind == TileKind.Empty) return false;
            map.Set(c, r, new Cell { kind = TileKind.Empty, ladder = cell.ladder });
            for (int i = temps.Count - 1; i >= 0; i--)
                if (temps[i].col == c && temps[i].row == r) temps.RemoveAt(i);
            OnTileBroken?.Invoke(c, r);
            return true;
        }

        /// <summary>
        /// Puts a tile of the given kind and surface into an empty, ladder-free cell for life seconds (an ice or sand
        /// block that crumbles or melts). False when the cell is taken or outside the map.
        /// </summary>
        public bool AddTempTile(int c, int r, TileKind kind, Surface surface, float life)
        {
            if (map == null || kind == TileKind.Empty || life <= 0f) return false;
            if (c < 0 || c >= StageMap.Cols || r < 0 || r >= map.Rows) return false;
            var cell = map.Get(c, r);
            if (cell.kind != TileKind.Empty || cell.ladder) return false;
            if (TileBlocked != null && TileBlocked(c, r)) return false;
            map.Set(c, r, new Cell { kind = kind, surface = surface, hp = (byte)(kind == TileKind.Breakable ? Mathf.Clamp(Tuning.I.breakableHp, 1, byte.MaxValue) : 0) });
            temps.Add(new TempTile { col = c, row = r, timeLeft = life });
            return true;
        }

        public int TempTileCount => temps.Count;

        /// <summary>True while the temporary tile at c, r is in its last Tuning.tempTileWarn seconds: the view blinks it.</summary>
        public bool IsCrumbling(int c, int r)
        {
            foreach (var t in temps)
                if (t.col == c && t.row == r) return t.timeLeft <= Tuning.I.tempTileWarn;
            return false;
        }

        /// <summary>Opens the ground columns c0 to c1 (inclusive) into a gap. Returns how many columns it opened.</summary>
        public int OpenFloorGap(int c0, int c1)
        {
            if (map == null) return 0;
            int opened = 0;
            for (int c = Mathf.Max(0, c0); c <= Mathf.Min(StageMap.Cols - 1, c1); c++)
            {
                if (map.IsGap(c)) continue;
                map.SetGap(c, true);
                opened++;
            }
            return opened;
        }

        /// <summary>Changes the surface of a tile, or of the ground when r is -1. False for an empty cell or a gap.</summary>
        public bool SetSurface(int c, int r, Surface surface)
        {
            if (map == null || c < 0 || c >= StageMap.Cols) return false;
            if (r < 0)
            {
                if (map.IsGap(c)) return false;
                map.SetGround(c, surface);
                return true;
            }
            if (r >= map.Rows) return false;
            var cell = map.Get(c, r);
            if (cell.kind == TileKind.Empty) return false;
            cell.surface = surface;
            map.Set(c, r, cell);
            return true;
        }

        // ---------------------------------------------------------------- rising hazard

        public bool HazardActive { get; private set; }
        /// <summary>Height of the hazard's top line in units. Meaningful while HazardActive.</summary>
        public float HazardY { get; private set; }
        public bool HazardStopped => HazardActive && HazardY >= hazardStop - 0.0001f;

        /// <summary>A lava line that starts at startY, rises speed units per second and stops at stopY.</summary>
        public void StartRisingHazard(float startY, float speed, float stopY)
        {
            HazardActive = true;
            HazardY = startY;
            hazardSpeed = Mathf.Max(0f, speed);
            hazardStop = Mathf.Max(startY, stopY);
        }

        /// <summary>True when the hazard is up and the box reaches below its line.</summary>
        public bool HazardHurts(Rect box) => HazardActive && box.yMin < HazardY;

        // ---------------------------------------------------------------- pipes

        /// <summary>X of a pipe in units: the middle, or Tuning.pipeOffsetX to the left or right.</summary>
        public static float PipeX(PipeSpot spot) =>
            spot == PipeSpot.TopLeft ? -Tuning.I.pipeOffsetX : spot == PipeSpot.TopRight ? Tuning.I.pipeOffsetX : 0f;

        /// <summary>The side pipes throw toward the middle; the middle one throws right.</summary>
        public static int PipeDir(PipeSpot spot) => spot == PipeSpot.TopRight ? -1 : 1;

        /// <summary>Y where a pipe's Gloop appears on a storey (just under its ceiling).</summary>
        public static float PipeY(int storey) => (storey + 1) * (float)StageMap.StoreyRows - Tuning.I.pipeDropY;

        public int PipesFlashing => pipes.Count;

        /// <summary>Starts a pipe flash on a storey: after Tuning.pipeFlashTime it launches a Gloop of the tier through OnPipe.</summary>
        public void FirePipe(PipeSpot spot, int tier, int storey)
        {
            int top = map != null ? map.Storeys - 1 : 0;
            pipes.Add(new FlashingPipe { spot = spot, tier = tier, storey = Mathf.Clamp(storey, 0, top), timeLeft = Tuning.I.pipeFlashTime });
        }

        /// <summary>Each flashing pipe as (spot, storey, how far through its flash, 0 to 1) for the view.</summary>
        public void ForEachFlashingPipe(Action<PipeSpot, int, float> draw)
        {
            if (draw == null) return;
            float total = Mathf.Max(0.0001f, Tuning.I.pipeFlashTime);
            foreach (var p in pipes) draw(p.spot, p.storey, 1f - Mathf.Clamp01(p.timeLeft / total));
        }

        // ---------------------------------------------------------------- hatches

        /// <summary>Unlocks the hatch up out of a storey (0 is the ground storey). It stays unlocked for the rest of the stage.</summary>
        public void OpenHatch(int storey)
        {
            if (storey >= 0 && storey < hatchUnlocked.Length) hatchUnlocked[storey] = true;
        }

        /// <summary>Opens the hatch a phase names (BossPhase.opensHatch), if it names one.</summary>
        public void OpenHatchOf(BossPhase phase)
        {
            if (phase != null && phase.opensHatch >= 0) OpenHatch(phase.opensHatch);
        }

        public bool HatchUnlocked(int storey) => storey >= 0 && storey < hatchUnlocked.Length && hatchUnlocked[storey];

        // ---------------------------------------------------------------- tick

        public void Tick(float dt)
        {
            if (dt <= 0f) return;

            if (HazardActive) HazardY = Mathf.Min(hazardStop, HazardY + hazardSpeed * dt);

            for (int i = temps.Count - 1; i >= 0; i--)
            {
                var t = temps[i];
                t.timeLeft -= dt;
                if (t.timeLeft > 0f) { temps[i] = t; continue; }
                temps.RemoveAt(i);
                if (map.Get(t.col, t.row).kind != TileKind.Empty) map.Set(t.col, t.row, new Cell());
                OnTempExpire?.Invoke(t.col, t.row);
            }

            for (int i = pipes.Count - 1; i >= 0; i--)
            {
                var p = pipes[i];
                p.timeLeft -= dt;
                if (p.timeLeft > 0f) { pipes[i] = p; continue; }
                pipes.RemoveAt(i);
                OnPipe?.Invoke(new PipeLaunch
                {
                    spot = p.spot,
                    tier = p.tier,
                    storey = p.storey,
                    position = new Vector2(PipeX(p.spot), PipeY(p.storey)),
                    dir = PipeDir(p.spot),
                });
            }
        }
    }
}
