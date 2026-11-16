using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// The Drifter: no gravity. It flies a constant diagonal at Tuning.drifterSpeed and reflects off the walls, floor,
    /// ceiling and every side of a platform. Its centre arrow always points along the flight.
    /// A Drifter Pogo flies straight up and down at the same speed, between the surface below and the one above, and
    /// only moves sideways during the hop after a pop.
    /// </summary>
    public class DrifterBehavior : BubbleBehavior
    {
        int signX = 1, signY = 1;
        int shownX, shownY; // the arrow currently drawn (0 = none yet)

        public override void OnSpawn(Bubble b, bool child)
        {
            var t = Tuning.I;
            float v = t.drifterSpeed / Mathf.Sqrt(2f);
            signX = b.Velocity.x < 0f ? -1 : 1;
            signY = child ? 1 : (b.Position.y >= 7f ? -1 : 1);
            b.Velocity = b.Pogo ? new Vector2(0f, signY * t.drifterSpeed) : new Vector2(signX * v, signY * v);
            UpdateSprite(b);
        }

        public override bool Tick(Bubble b, float dt)
        {
            if (Game.Map == null) return true;
            // Keep the speed constant even if something nudged the velocity.
            var vel = b.Velocity;
            float v = Tuning.I.drifterSpeed / Mathf.Sqrt(2f);
            if (b.Pogo)
            {
                // Straight up and down at full speed; a hop after a pop adds a short sideways part.
                float hop = b.Hopping ? vel.x : 0f;
                if (b.Hopping)
                {
                    b.HopLeft -= Mathf.Abs(hop) * dt;
                    if (b.HopLeft <= 0f) { b.EndHop(); hop = 0f; }
                }
                vel = new Vector2(hop, Mathf.Sign(vel.y == 0f ? signY : vel.y) * Tuning.I.drifterSpeed);
            }
            else
                vel = new Vector2(Mathf.Sign(vel.x == 0f ? signX : vel.x) * v, Mathf.Sign(vel.y == 0f ? signY : vel.y) * v);

            var p = (Vector2)b.transform.position;
            int steps = Mathf.Max(1, Mathf.CeilToInt(vel.magnitude * dt / 0.4f));
            float stepDt = dt / steps;
            bool reflected = false;
            for (int i = 0; i < steps; i++)
            {
                p += vel * stepDt;
                Contact c = Game.Map.ResolveCircle(ref p, b.Radius);
                b.SealHomeStorey(ref p, ref c);
                if (c.landed) { vel.y = Mathf.Abs(vel.y); reflected = true; }
                if (c.bumped) { vel.y = -Mathf.Abs(vel.y); reflected = true; }
                if (c.side != 0)
                {
                    if (b.Pogo) vel.x = 0f;
                    else { vel.x = c.side * Mathf.Abs(vel.x); reflected = true; }
                }
                if (b.Pogo && b.Hopping && (c.landed || c.bumped)) { b.EndHop(); vel.x = 0f; }
            }
            b.transform.position = p;
            b.Velocity = vel;
            if (reflected)
            {
                b.StartSquash();
                Sfx.Play(Sound.Bounce, 1f + b.Tier * 0.25f, 0.5f - b.Tier * 0.1f);
            }
            if (vel.x != 0f || !b.Pogo) signX = vel.x < 0f ? -1 : 1;
            signY = vel.y < 0f ? -1 : 1;
            UpdateSprite(b);
            return true;
        }

        /// <summary>A Drifter Pogo points up on the surface below and down at the surface above.</summary>
        public override float PogoMorph(Bubble b) => PogoShape.BetweenSurfaces(b);

        void UpdateSprite(Bubble b)
        {
            if (b.Pogo) return; // a Pogo draws its own frames, arrow included
            if (b.Ceramic) return; // a ceramic Drifter keeps its ceramic sprite and does not turn
            if (shownX == signX && shownY == signY) return;
            shownX = signX;
            shownY = signY;
            b.SetSprite(PlaceholderArt.I.DrifterBubble(b.Tier, signX, signY));
        }

        /// <summary>
        /// True when a Drifter placed at position and flying along velocity stays clear of Wick's middle for the first
        /// drifterSafeTime seconds. The straight line is walked in 0.05 s steps; reflections are ignored.
        /// </summary>
        public static bool StartIsFair(Vector2 position, Vector2 velocity, Vector2 wickStart)
        {
            var t = Tuning.I;
            float radius = t.tiers[1].diameter * 0.5f; // L, the largest a Drifter comes
            float limit = t.drifterSafeDistance + radius;
            var wick = wickStart + new Vector2(0f, 1f);
            for (float time = 0f; time <= t.drifterSafeTime + 1e-4f; time += 0.05f)
                if (((position + velocity * time) - wick).sqrMagnitude < limit * limit) return false;
            return true;
        }
    }
}
