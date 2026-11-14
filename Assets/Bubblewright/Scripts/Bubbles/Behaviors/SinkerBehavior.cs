using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// The Sinker runs out of bounce. Every landing keeps only part of the last bounce's height (less on sand).
    /// When the next rise would be tiny it stops bouncing and rolls along its surface, turning at walls,
    /// at the edge of a ground gap and at the end of a platform, so it never falls off by itself.
    /// A Sinker Pogo cannot roll: when its bounce runs out it rests, wobbles, and springs back to full height.
    /// </summary>
    public class SinkerBehavior : BubbleBehavior
    {
        float keep = 1f;      // share of the full rise that is left
        bool rolling;
        float surfaceY;       // height of the surface it rolls on
        bool resting;         // a Pogo that ran out of bounce
        float restTime;

        /// <summary>Share of the full rise that is left (1 for a fresh Sinker). Tests set it to skip many bounces.</summary>
        public float Keep { get => keep; set => keep = value; }
        public bool Rolling => rolling;
        public bool Resting => resting;

        public override bool Tick(Bubble b, float dt)
        {
            if (resting) return TickRest(b, dt);
            if (!rolling) return false;
            var map = Game.Map;
            if (map == null) { rolling = false; return false; }

            float r = b.Radius;
            // Lost its footing (a breakable floor was shot away): fall with the normal bounce.
            if (!HasFloor(map, StageMap.Col(b.Position.x), surfaceY))
            {
                rolling = false;
                return false;
            }

            float vx = b.Velocity.x;
            var p = new Vector2(b.Position.x + vx * dt, surfaceY + r);
            float lead = p.x + Mathf.Sign(vx) * r;

            var contact = map.ResolveCircle(ref p, r);
            bool turn = contact.side != 0 || !HasFloor(map, StageMap.Col(lead), surfaceY);
            if (turn)
            {
                // Undo this tick's move and head the other way.
                b.Velocity.x = -vx;
                p = new Vector2(b.Position.x, surfaceY + r);
            }
            else
            {
                p.y = surfaceY + r;
            }
            b.Velocity.y = 0f;
            b.transform.position = p;
            return true;
        }

        public override void OnLanded(Bubble b)
        {
            var map = Game.Map;
            var t = Tuning.I;
            float v = t.BounceSpeed(b.Kind, b.Tier);
            float g = t.Gravity(b.Kind, b.Tier);
            float fullRise = v * v / (2f * g);

            // b.Position is still where the bubble was before this landing step, a little above the surface,
            // so look the surface up instead of trusting the bubble's own height.
            float bottom = b.Position.y - b.Radius;
            if (map != null) bottom = Mathf.Max(0f, map.FloorBelow(b.Position.x, bottom + 0.01f));
            bool sand = map != null && map.SurfaceUnder(b.Position.x, bottom) == Surface.Sand;
            keep *= sand ? t.sinkerKeepSand : t.sinkerKeep;

            if (keep * fullRise < t.sinkerRollBelow && b.Pogo)
            {
                resting = true;
                restTime = 0f;
                surfaceY = bottom;
                b.Velocity = Vector2.zero;
            }
            else if (keep * fullRise < t.sinkerRollBelow)
            {
                rolling = true;
                surfaceY = bottom;
                b.Velocity.y = 0f;
            }
            else
            {
                b.Velocity.y = Mathf.Sqrt(keep) * v; // height goes with the square of the speed
            }
        }

        /// <summary>Rests on its surface, wobbles at the end, then springs back to a full bounce.</summary>
        bool TickRest(Bubble b, float dt)
        {
            var t = Tuning.I;
            restTime += dt;
            b.Velocity = Vector2.zero;
            b.transform.position = new Vector3(b.Position.x, surfaceY + b.Radius, b.transform.position.z);
            if (Game.Map != null && !HasFloor(Game.Map, StageMap.Col(b.Position.x), surfaceY))
            {
                // Its floor was shot away: fall with the normal bounce, with its bounce back.
                resting = false;
                keep = 1f;
                b.VisualScale = Vector2.one;
                return false;
            }
            if (restTime >= t.pogoSinkerRest)
            {
                resting = false;
                keep = 1f;
                b.VisualScale = Vector2.one;
                b.Velocity = new Vector2(0f, t.BounceSpeed(b.Kind, b.Tier));
                b.BeginBounce(surfaceY);
                b.StartSquash();
                return true;
            }
            if (restTime >= t.pogoSinkerRest - t.pogoSinkerWobble)
            {
                bool wide = (int)((restTime - (t.pogoSinkerRest - t.pogoSinkerWobble)) / 0.05f) % 2 == 0;
                b.VisualScale = wide ? new Vector2(1.1f, 0.92f) : new Vector2(0.92f, 1.1f);
            }
            return true;
        }

        /// <summary>Whether column c has something to roll on at height bottom: ground that is not a gap, or a solid tile top.</summary>
        static bool HasFloor(StageMap map, int c, float bottom)
        {
            if (c < 0 || c >= StageMap.Cols) return false;
            if (bottom <= 0.01f) return !map.IsGap(c);
            return map.IsSolid(c, StageMap.Row(bottom - 0.01f));
        }
    }
}
