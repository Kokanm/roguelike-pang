using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// What a boss does to its arena. Bosses reach it through Game.Arena (an ArenaEvents on this stage's map):
    /// BreakTile, AddTempTile, OpenFloorGap, StartRisingHazard, SetSurface and FirePipe. Game adds the debris, the
    /// sounds, the Gloop from a pipe, the lava's hurt and the view, and ticks it with game time, so pause, hit-stop
    /// and slow motion freeze it. A new stage builds a new map and a new ArenaEvents, so nothing carries over.
    /// </summary>
    public partial class Game
    {
        static ArenaEvents arena;
        ArenaView arenaView;
        bool bossArena;

        /// <summary>This stage's arena events. Null until a stage has loaded.</summary>
        public static ArenaEvents Arena => arena;

        /// <summary>
        /// True when the hatch up out of storey f - 1 should be open. An ordinary stage opens it when that storey is
        /// clear; a boss arena only when a boss phase has opened it. Either way Wick must not be up there already:
        /// he never goes back down.
        /// </summary>
        public static bool HatchShouldOpen(bool bossArena, bool storeySettled, bool phaseOpened, int activeStorey, int f) =>
            (bossArena ? phaseOpened : storeySettled) && activeStorey < f;

        void SetupArena(StagePlan plan)
        {
            bossArena = plan != null && plan.isBossArena;
            arena = new ArenaEvents(Map);
            arena.OnTileBroken = (c, r) =>
            {
                fx.Debris(StageMap.CellRect(c, r).center, CurrentTheme.breakFill, CurrentTheme.tileShade, 8);
                Sfx.Play(Sound.Pop, 0.7f, 0.8f);
                tileShakeFrames = Mathf.Max(tileShakeFrames, 2);
            };
            arena.OnTempExpire = (c, r) =>
            {
                fx.Debris(StageMap.CellRect(c, r).center, CurrentTheme.tileFill, CurrentTheme.tileShade, 4);
                Sfx.Play(Sound.Bounce, 1.4f, 0.4f);
            };
            arena.TileBlocked = (c, r) => wick != null && wick.Hitbox.Overlaps(StageMap.CellRect(c, r));
            arena.OnPipe = launch =>
            {
                int tier = Mathf.Clamp(launch.tier, 0, T.tiers.Length - 1);
                float radius = T.tiers[tier].diameter * 0.5f;
                float top = (launch.storey + 1) * (float)StageMap.StoreyRows - radius - 0.05f;
                var at = new Vector2(launch.position.x, Mathf.Min(launch.position.y, top));
                SpawnBubble(BubbleKind.Gloop, tier, at, launch.dir, 0f, false);
                Sfx.Play(Sound.Bounce, 0.7f, 0.6f);
            };
            if (world != null && arenaView == null) arenaView = ArenaView.Create(world);
            arenaView?.Sync(arena);
        }

        void ClearArena()
        {
            arena = null;
            bossArena = false;
            arenaView?.Hide();
        }

        void TickArena(float dt)
        {
            if (arena == null) return;
            arena.Tick(dt);
            arenaView?.Sync(arena);
            if (!debug.god && !wick.Invulnerable && !wick.Dead && arena.HazardHurts(wick.Hitbox))
            {
                HurtWick(wick.Hitbox.center.x, 1);
                wick.Hop(T.hazardThrowHeight);
            }
        }
    }
}
