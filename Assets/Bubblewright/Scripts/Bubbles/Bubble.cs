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

        Tuning T => Tuning.I;
        SpriteRenderer sr;
        BubbleBehavior behavior;
        float grow = 1f;      // 0.7 -> 1 for freshly split children
        float growTimer = -1f;
        float squashTimer = -1f;

        /// <summary>The kind's behaviour, or null for a plain bouncing bubble. Tests read its state.</summary>
        public BubbleBehavior Behavior => behavior;
        public bool CanBeHit => !Popped && (behavior?.CanBeHit(this) ?? true);
        public bool CanHurt => !Popped && (behavior?.CanHurt(this) ?? true);

        public float FullRadius => T.tiers[Tier].diameter * 0.5f;
        public float Radius => FullRadius * grow;
        public Vector2 Position => transform.position;

        public static Bubble Create(Transform parent, BubbleKind kind, int tier, Vector2 position, Vector2 velocity, bool child, bool ceramic = false)
        {
            var go = new GameObject($"Bubble {kind} {Tuning.I.tiers[tier].name}");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var b = go.AddComponent<Bubble>();
            b.Kind = kind;
            b.Tier = tier;
            b.Velocity = velocity;
            b.sr = go.AddComponent<SpriteRenderer>();
            b.Ceramic = ceramic;
            b.Armor = ceramic ? Mathf.Max(2, Tuning.I.ceramicHits) : 1;
            b.sr.sprite = ceramic ? PlaceholderArt.I.CeramicBubble(kind, tier, false) : PlaceholderArt.I.Bubbles[(int)kind, tier];
            Sorting.Apply(b.sr, Layer.Bubbles, 3 - tier); // smaller bubbles draw over bigger ones
            Game.ApplySpriteMaterial(b.sr);
            if (child)
            {
                b.grow = Tuning.I.childStartScale;
                b.growTimer = 0f;
            }
            b.behavior = BubbleBehavior.For(kind);
            b.behavior?.OnSpawn(b, child);
            b.ApplyVisual();
            return b;
        }

        public float SpeedX => T.bubbleSpeedX * T.Kind(Kind).speedScale;

        public void Tick(float dt)
        {
            if (behavior == null || !behavior.Tick(this, dt)) TickBounce(dt);
            TickGrowAndSquash(dt);
            ApplyVisual();
        }

        public void OnStorm() => behavior?.OnStorm(this);

        public void SetSprite(Sprite s)
        {
            if (sr != null) sr.sprite = s;
        }

        public void StartSquash() => squashTimer = 0f;

        /// <summary>Puts back the sprite this bubble normally shows: ordinary, ceramic, or cracked ceramic.</summary>
        public void RestoreSprite()
        {
            if (sr == null) return;
            if (!Ceramic) sr.sprite = PlaceholderArt.I.Bubbles[(int)Kind, Tier];
            else sr.sprite = PlaceholderArt.I.CeramicBubble(Kind, Tier, Armor < Mathf.Max(2, T.ceramicHits));
        }

        void TickBounce(float dt)
        {
            float g = T.Gravity(Kind, Tier);
            var p = (Vector2)transform.position;

            // Keep the exact constant-gravity ballistic step, but split long moves so a bubble cannot
            // cross a one-unit tile between collision checks.
            var displacement = new Vector2(Velocity.x * dt, Velocity.y * dt - 0.5f * g * dt * dt);
            int steps = Mathf.Max(1, Mathf.CeilToInt(displacement.magnitude / 0.4f));
            float stepDt = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                p.x += Velocity.x * stepDt;
                p.y += Velocity.y * stepDt - 0.5f * g * stepDt * stepDt;
                Velocity.y -= g * stepDt;

                Contact contact = IgnoresTiles
                    ? Game.Map.ResolveBounds(ref p, Radius)
                    : Game.Map.ResolveCircle(ref p, Radius);
                if (contact.landed)
                {
                    Velocity.y = T.BounceSpeed(Kind, Tier);
                    squashTimer = 0f;
                    transform.position = p; // behaviours read Position: it must be the landing spot, not last frame's
                    behavior?.OnLanded(this);
                    Sfx.Play(Sound.Bounce, 1f + Tier * 0.25f, 0.5f - Tier * 0.1f); // big bubbles thud, small ones tap
                }
                if (contact.bumped) Velocity.y = -Mathf.Abs(Velocity.y);
                if (contact.side != 0) Velocity.x = contact.side * Mathf.Abs(Velocity.x);
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
            sr.sprite = PlaceholderArt.I.CeramicBubble(Kind, Tier, true);
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
            var c = Position;
            float cx = Mathf.Clamp(c.x, rect.xMin, rect.xMax);
            float cy = Mathf.Clamp(c.y, rect.yMin, rect.yMax);
            float dx = c.x - cx, dy = c.y - cy;
            float r = Radius;
            return dx * dx + dy * dy < r * r;
        }
    }
}
