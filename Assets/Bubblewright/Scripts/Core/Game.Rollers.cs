using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Rolling balloons on normal stages: spawning, ticking, harpoon hits (same hit-stop and swell as a bubble), the
    /// split into two small ones, hurting Wick, and the debug spawn. Boss stages never have them.
    /// </summary>
    public partial class Game
    {
        readonly List<Roller> rollers = new List<Roller>();
        readonly List<Roller> hitRollers = new List<Roller>(); // swelling during the hit-stop, popped when it ends

        /// <summary>A normal stage is cleared only when no bubble and no roller is left (hit ones still count until they pop).</summary>
        public static bool StageCleared(int bubblesAlive, int rollersAlive) => bubblesAlive == 0 && rollersAlive == 0;

        void ClearRollers()
        {
            foreach (var r in rollers) if (r) Destroy(r.gameObject);
            rollers.Clear();
            hitRollers.Clear();
        }

        void SpawnRollers(StagePlan plan)
        {
            if (run.IsBossStage || plan.rollers == null) return;
            foreach (var s in plan.rollers) SpawnRoller(s.tier, s.x, s.surfaceY, s.direction, s.steel, s.ceramic);
        }

        Roller SpawnRoller(int tier, float x, float surfaceY, int dir, bool steel = false, bool ceramic = false)
        {
            var r = Roller.Create(bubbleRoot, tier, x, surfaceY, dir, steel, ceramic);
            rollers.Add(r);
            return r;
        }

        /// <summary>The storm speeds rollers up the same way it speeds bubbles up.</summary>
        void TickRollers(float dt)
        {
            float rdt = BubbleDt(dt);
            for (int i = 0; i < rollers.Count; i++) rollers[i].Tick(rdt, rollers);
        }

        /// <summary>A harpoon that touches a roller (cable or tip) hits it, like a bubble.</summary>
        void HarpoonHitsRollers()
        {
            foreach (var h in harpoons)
            {
                if (!h.Active) continue;
                for (int i = 0; i < rollers.Count; i++)
                {
                    if (rollers[i].Popped || !h.Hits(rollers[i])) continue;
                    h.Hide();
                    HitRoller(rollers[i]);
                    break;
                }
            }
        }

        /// <summary>Rolling balls that must be popped to clear the stage: every one except steel.</summary>
        int RequiredRollers()
        {
            int n = 0;
            foreach (var r in rollers) if (!r.Steel) n++;
            return n + hitRollers.Count;
        }

        void HitRoller(Roller r)
        {
            if (r.AbsorbHit())
            {
                // Steel rings and sparks; a ceramic ball cracks. Either way the shot is spent and nothing pops.
                fx.Sparkle(r.Position, r.Steel ? Palette.SteelLight : Palette.Cream);
                Sfx.Play(Sound.Bounce, r.Steel ? 2f : 1.6f, 0.6f);
                return;
            }
            r.Swell(T.popSwell);
            hitRollers.Add(r);
            freeze = Mathf.Max(freeze, T.popHitStop);
        }

        void PopHitRollers()
        {
            if (hitRollers.Count == 0) return;
            foreach (var r in hitRollers) PopRoller(r);
            hitRollers.Clear();
        }

        void PopRoller(Roller r)
        {
            rollers.Remove(r);
            run.AddScore(Roller.ScoreOf(r.Tier));
            // The pop effect sized like an M bubble for a big roller and an S bubble for a small one, in pearl.
            int fxTier = Mathf.Clamp(T.tiers.Length - 2 + r.Tier, 0, T.tiers.Length - 1);
            fx.Pop(r.Position, fxTier, T.rollerColor, T.popSwell);
            Sfx.Play(Sound.Pop, 1.3f + r.Tier * 0.2f, 0.8f);
            rollers.AddRange(Roller.Split(r, bubbleRoot));
            Destroy(r.gameObject);
        }

        /// <summary>A roller touching Wick hurts him like a bubble does, storm damage included. True when it did.</summary>
        bool RollersHurtWick()
        {
            if (debug.god || wick.Invulnerable || wick.Dead) return false;
            var box = wick.Hitbox;
            foreach (var r in rollers)
            {
                if (r.Popped || !r.Overlaps(box)) continue;
                HurtWick(r.Position.x, storm ? T.stormDamage : 1);
                return true;
            }
            return false;
        }

        /// <summary>Debug keys 5 (pearl), 6 (steel) and 8 (ceramic): a big roller on the ground at a random x, on solid ground with room above it.</summary>
        void DebugSpawnRoller(bool steel = false, bool ceramic = false)
        {
            if (run == null || run.IsBossStage || Map == null) return;
            float r = Roller.RadiusOf(0);
            for (int attempt = 0; attempt < 30; attempt++)
            {
                float x = Random.Range(-T.HalfWidth + 2f, T.HalfWidth - 2f);
                x = Map.NearestSolidGroundX(x);
                if (Map.IsGap(StageMap.Col(x))) continue;
                if (Map.OverlapsSolid(new Rect(x - r, 0.001f, r * 2f, r * 2f))) continue;
                SpawnRoller(0, x, 0f, Random.value < 0.5f ? -1 : 1, steel, ceramic);
                return;
            }
        }

        void DrawRollerHitboxes()
        {
            foreach (var r in rollers)
            {
                float rad = r.Radius;
                DrawWorldRect(new Rect(r.Position.x - rad, r.Position.y - rad, rad * 2f, rad * 2f), Palette.Pearl);
            }
        }
    }
}
