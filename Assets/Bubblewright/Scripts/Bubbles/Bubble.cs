using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// A kinematic bubble with its own bounce maths (no Rigidbody, no physics materials).
    /// Horizontal speed is constant. Each floor bounce launches it with exactly the speed that makes its
    /// top reach the tier's peak height, so every tier always peaks at the same height wherever it was born.
    /// Pivot at the centre.
    /// </summary>
    public class Bubble : MonoBehaviour
    {
        public BubbleKind Kind { get; private set; }
        public int Tier { get; private set; }
        public Vector2 Velocity;
        public bool Popped { get; set; }
        /// <summary>A ceramic bubble takes Tuning.ceramicHits harpoon hits to break.</summary>
        public bool Ceramic { get; private set; }
        /// <summary>Hits left before it breaks (1 for an ordinary bubble).</summary>
        public int Armor { get; private set; } = 1;
        /// <summary>Multiplied into the drawn scale. Behaviours use it for a wobble or a squash of their own.</summary>
        public Vector2 VisualScale = Vector2.one;
        /// <summary>When true, the bounce only resolves the arena walls, ceiling and floor.</summary>
        public bool IgnoresTiles;
        /// <summary>A Pogo: a soft triangle that bounces in place and never moves sideways (except one hop after a pop).</summary>
        public bool Pogo { get; private set; }
        /// <summary>A steel Pogo: harpoons glance off it and it is not needed to clear the stage.</summary>
        public bool Steel { get; private set; }
        /// <summary>A Pogo's shape: 0 points up (on the floor), 1 points down (at the top of its bounce).</summary>
        public float Morph { get; private set; }
        /// <summary>True while a Pogo born from a pop is still on its one sideways hop.</summary>
        public bool Hopping { get; private set; }
        /// <summary>How a faded Ghost Pogo is drawn right now; Normal while solid. Set by the Ghost behaviour.</summary>
        public PogoLook GhostLook
        {
            get => ghostLook;
            set { ghostLook = value; RefreshPogoSprite(true); }
        }

        Tuning T => Tuning.I;
        SpriteRenderer sr;
        BubbleBehavior behavior;
        float grow = 1f;      // 0.7 -> 1 for freshly split children
        float growTimer = -1f;
        float squashTimer = -1f;
        PogoLook ghostLook = PogoLook.Normal;
        bool morphReady;      // the bounce the morph follows has been measured
        float bounceFloor;    // surface the current bounce left from
        float bounceRise;     // how high the bubble's bottom rises above it this bounce
        int shownFrame = -1, shownArrow;
        PogoLook shownLook;

        /// <summary>The kind's behaviour, or null for a plain bouncing bubble. Tests read its state.</summary>
        public BubbleBehavior Behavior => behavior;
        public bool CanBeHit => !Popped && !Steel && (behavior?.CanBeHit(this) ?? true);
        public bool CanHurt => !Popped && (behavior?.CanHurt(this) ?? true);

        public float FullRadius => T.tiers[Tier].diameter * 0.5f;
        public float Radius => FullRadius * grow;
        public Vector2 Position => transform.position;

        public static Bubble Create(Transform parent, BubbleKind kind, int tier, Vector2 position, Vector2 velocity, bool child, bool ceramic = false,
            bool pogo = false, bool steel = false)
        {
            steel = steel && pogo;
            var go = new GameObject($"{(steel ? "Steel " : "")}{(pogo ? "Pogo" : "Bubble")} {kind} {Tuning.I.tiers[tier].name}");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var b = go.AddComponent<Bubble>();
            b.Kind = kind;
            b.Tier = tier;
            b.Velocity = velocity;
            b.sr = go.AddComponent<SpriteRenderer>();
            b.Pogo = pogo;
            b.Steel = steel;
            if (steel) ceramic = false;
            b.Ceramic = ceramic;
            b.Armor = ceramic ? Mathf.Max(2, Tuning.I.ceramicHits) : 1;
            b.sr.sprite = pogo ? null : ceramic ? PlaceholderArt.I.CeramicBubble(kind, tier, false) : PlaceholderArt.I.Bubbles[(int)kind, tier];
            Sorting.Apply(b.sr, Layer.Bubbles, 3 - tier); // smaller bubbles draw over bigger ones
            Game.ApplySpriteMaterial(b.sr);
            if (child)
            {
                b.grow = Tuning.I.childStartScale;
                b.growTimer = 0f;
            }
            b.behavior = steel ? null : BubbleBehavior.For(kind);
            b.behavior?.OnSpawn(b, child);
            if (pogo)
            {
                if (!b.Hopping) b.Velocity.x = 0f;
                b.UpdateMorph();
                b.RefreshPogoSprite(true);
            }
            b.ApplyVisual();
            return b;
        }

        public float SpeedX => T.bubbleSpeedX * T.Kind(Kind).speedScale;

        public void Tick(float dt)
        {
            if (behavior == null || !behavior.Tick(this, dt)) TickBounce(dt);
            if (Pogo)
            {
                UpdateMorph();
                RefreshPogoSprite(false);
            }
            TickGrowAndSquash(dt);
            ApplyVisual();
        }

        /// <summary>
        /// A Pogo born from a pop hops sideways once at this speed, then bounces in place where it lands. It stops at a
        /// wall and at the edge of a ground gap.
        /// </summary>
        public void StartHop(float vx, float distance)
        {
            if (!Pogo) return;
            Velocity.x = vx;
            Hopping = vx != 0f;
            HopLeft = Mathf.Abs(distance);
        }

        /// <summary>Sideways distance the hop may still cover. Behaviours without gravity (the Drifter) end the hop on it.</summary>
        public float HopLeft { get; set; }

        /// <summary>Ends the hop: from now on it never moves sideways.</summary>
        public void EndHop()
        {
            Hopping = false;
            if (Pogo) Velocity.x = 0f;
        }

        /// <summary>
        /// The morph follows the height: 0 on the surface the bounce left from, 1 at the top of the bounce. A behaviour
        /// that moves the Pogo its own way (a Drifter, a Clinger) says where it is between two surfaces instead.
        /// </summary>
        void UpdateMorph()
        {
            float own = behavior?.PogoMorph(this) ?? -1f;
            if (own >= 0f)
            {
                Morph = Mathf.Clamp01(own);
                return;
            }
            float bottom = Position.y - Radius;
            if (!morphReady)
            {
                // Placed in the air or dropped in by a wave: it starts its first fall from here.
                morphReady = true;
                bounceFloor = FloorUnder(bottom);
                float g = T.Gravity(Kind, Tier);
                float up = Velocity.y > 0f ? Velocity.y * Velocity.y / (2f * g) : 0f;
                bounceRise = Mathf.Max(0f, bottom - bounceFloor) + up;
            }
            Morph = bounceRise > 0.05f ? Mathf.Clamp01((bottom - bounceFloor) / bounceRise) : 0f;
        }

        /// <summary>Forget the measured bounce: the next morph measures a fall from where the Pogo is now.</summary>
        public void ResetMorph() => morphReady = false;

        float FloorUnder(float bottom)
        {
            if (IgnoresTiles || Game.Map == null) return 0f;
            return Mathf.Max(0f, Game.Map.FloorBelow(Position.x, bottom + 0.01f));
        }

        /// <summary>A Pogo's bounce leaves a surface: the morph measures this bounce from there. Behaviours that launch it call this too.</summary>
        public void BeginBounce(float floor)
        {
            morphReady = true;
            bounceFloor = floor;
            float g = T.Gravity(Kind, Tier);
            bounceRise = Velocity.y > 0f ? Velocity.y * Velocity.y / (2f * g) : 0f;
            // A ceiling or platform above cuts the bounce short: the Pogo points down when it touches it.
            if (Game.Map != null && !IgnoresTiles)
            {
                float top = Game.Map.CeilingAbove(Position.x, Position.y + Radius, out _);
                bounceRise = Mathf.Min(bounceRise, Mathf.Max(0.05f, top - floor - 2f * Radius));
            }
            else
            {
                bounceRise = Mathf.Min(bounceRise, Mathf.Max(0.05f, T.arenaHeight - floor - 2f * Radius));
            }
        }

        /// <summary>Shows the Pogo sprite for the current morph, look and (for a Drifter) direction, if it changed.</summary>
        void RefreshPogoSprite(bool force)
        {
            if (!Pogo || sr == null) return;
            var t = T;
            int frame = PogoShape.Frame(Morph, t.pogoFrames);
            PogoLook look = Steel ? PogoLook.Steel
                : ghostLook != PogoLook.Normal ? ghostLook
                : Ceramic ? (Armor < Mathf.Max(2, t.ceramicHits) ? PogoLook.Cracked : PogoLook.Ceramic)
                : PogoLook.Normal;
            int arrow = Kind == BubbleKind.Drifter ? (Velocity.y < 0f ? -1 : 1) : 0;
            if (!force && frame == shownFrame && look == shownLook && arrow == shownArrow) return;
            shownFrame = frame;
            shownLook = look;
            shownArrow = arrow;
            sr.sprite = PlaceholderArt.I.PogoFrame(Kind, Tier, frame, look, arrow);
        }

        public void OnStorm() => behavior?.OnStorm(this);

        public void SetSprite(Sprite s)
        {
            if (Pogo) return; // a Pogo draws its own frames
            if (sr != null) sr.sprite = s;
        }

        public void StartSquash() => squashTimer = 0f;

        /// <summary>Puts back the sprite this bubble normally shows: ordinary, ceramic, or cracked ceramic.</summary>
        public void RestoreSprite()
        {
            if (sr == null) return;
            if (Pogo) { ghostLook = PogoLook.Normal; RefreshPogoSprite(true); return; }
            if (!Ceramic) sr.sprite = PlaceholderArt.I.Bubbles[(int)Kind, Tier];
            else sr.sprite = PlaceholderArt.I.CeramicBubble(Kind, Tier, Armor < Mathf.Max(2, T.ceramicHits));
        }

        void TickBounce(float dt)
        {
            float g = T.Gravity(Kind, Tier);
            var p = (Vector2)transform.position;
            if (Pogo && !Hopping) Velocity.x = 0f;

            // Keep the exact constant-gravity ballistic step, but split long moves so a bubble cannot
            // cross a one-unit tile between collision checks.
            var displacement = new Vector2(Velocity.x * dt, Velocity.y * dt - 0.5f * g * dt * dt);
            int steps = Mathf.Max(1, Mathf.CeilToInt(displacement.magnitude / 0.4f));
            float stepDt = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                float oldX = p.x;
                p.x += Velocity.x * stepDt;
                // A hopping Pogo stops at the edge of a ground gap rather than hop over it.
                if (Hopping && Game.Map != null && p.y - Radius < 1f &&
                    Game.Map.IsGap(StageMap.Col(p.x)) && !Game.Map.IsGap(StageMap.Col(oldX)))
                {
                    p.x = oldX;
                    Velocity.x = 0f;
                }
                p.y += Velocity.y * stepDt - 0.5f * g * stepDt * stepDt;
                Velocity.y -= g * stepDt;

                Contact contact = IgnoresTiles
                    ? Game.Map.ResolveBounds(ref p, Radius)
                    : Game.Map.ResolveCircle(ref p, Radius);
                if (contact.landed)
                {
                    Velocity.y = T.BounceSpeed(Kind, Tier);
                    squashTimer = 0f;
                    if (Hopping) EndHop();
                    transform.position = p; // behaviours read Position: it must be the landing spot, not last frame's
                    behavior?.OnLanded(this);
                    if (Pogo) BeginBounce(p.y - Radius);
                    Sfx.Play(Sound.Bounce, 1f + Tier * 0.25f, 0.5f - Tier * 0.1f); // big bubbles thud, small ones tap
                }
                if (contact.bumped) Velocity.y = -Mathf.Abs(Velocity.y);
                if (contact.side != 0) Velocity.x = Pogo ? 0f : contact.side * Mathf.Abs(Velocity.x);
            }
            transform.position = p;
        }

        void TickGrowAndSquash(float dt)
        {
            if (growTimer >= 0f)
            {
                growTimer += dt;
                float k = Mathf.Clamp01(growTimer / Mathf.Max(0.001f, T.childGrowTime));
                grow = Mathf.Lerp(T.childStartScale, 1f, k);
                if (k >= 1f) growTimer = -1f;
            }
            if (squashTimer >= 0f)
            {
                squashTimer += dt;
                if (squashTimer > 0.1f) squashTimer = -1f;
            }
        }

        void ApplyVisual()
        {
            // Bounce: squash 1.2 x 0.8 on contact for 50 ms, stretch 0.9 x 1.12 leaving the floor.
            Vector3 s = Vector3.one * grow;
            if (squashTimer >= 0f)
                s = squashTimer < 0.05f
                    ? new Vector3(1.2f * grow, 0.8f * grow, 1f)
                    : new Vector3(0.9f * grow, 1.12f * grow, 1f);
            s.x *= VisualScale.x;
            s.y *= VisualScale.y;
            s.z = 1f;
            transform.localScale = s;
        }

        /// <summary>
        /// A hit on a ceramic bubble that still has armor: it cracks and squashes instead of breaking. Returns true when
        /// the hit was absorbed (the bubble lives on), false when this hit breaks it.
        /// </summary>
        public bool AbsorbHit()
        {
            if (Armor <= 1) return false;
            Armor--;
            if (Pogo) RefreshPogoSprite(true);
            else sr.sprite = PlaceholderArt.I.CeramicBubble(Kind, Tier, true);
            squashTimer = 0f;
            return true;
        }

        /// <summary>Hit: swell during the hit-stop, then the game pops it.</summary>
        public void Swell(float scale)
        {
            Popped = true;
            transform.localScale = new Vector3(grow * scale, grow * scale, 1f);
        }

        /// <summary>Circle vs axis-aligned rectangle.</summary>
        public bool Overlaps(Rect rect)
        {
            if (Pogo) return PogoShape.Overlaps(Position, Radius, Morph, rect);
            var c = Position;
            float cx = Mathf.Clamp(c.x, rect.xMin, rect.xMax);
            float cy = Mathf.Clamp(c.y, rect.yMin, rect.yMax);
            float dx = c.x - cx, dy = c.y - cy;
            float r = Radius;
            return dx * dx + dy * dy < r * r;
        }
    }
}
