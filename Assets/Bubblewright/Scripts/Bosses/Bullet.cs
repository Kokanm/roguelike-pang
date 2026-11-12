using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// A boss bullet. An aimed bullet is a disc that flies in a straight line. A bouncy bullet falls under
    /// bouncyGravity at a constant sideways speed, turns at the walls, hops on the floor a set number of times so its
    /// top reaches bouncyPeak, and then falls through the floor. No physics. Game ticks it, so hit-stop, slow motion
    /// and pause freeze it. Harpoons do not stop bullets and bullets do not stop harpoons.
    /// </summary>
    public class Bullet : MonoBehaviour
    {
        const float SquashTime = 0.05f;

        /// <summary>Everything that moves a bullet, as plain data, so tests can look ahead without GameObjects.</summary>
        public Flight State;

        public Vector2 Velocity { get => State.velocity; set => State.velocity = value; }
        public float Radius => State.radius;
        public Vector2 Position => transform.position;
        public bool Dead { get; set; }

        public bool Bouncy => State.bouncy;
        /// <summary>Floor bounces left before a bouncy bullet falls through the floor.</summary>
        public int BouncesLeft => State.bouncesLeft;
        /// <summary>How many times a bouncy bullet has reached the floor (its bounces, plus the touch it falls through on).</summary>
        public int FloorTouches => State.floorTouches;
        /// <summary>A bouncy bullet with no bounces left that is falling through the floor.</summary>
        public bool Sinking => State.sinking;
        /// <summary>False once a bouncy bullet's centre is below the floor: it can no longer hurt Wick.</summary>
        public bool CanHurt => State.CanHurt;
        /// <summary>True when the bullet should be removed (see Flight.Out).</summary>
        public bool Out => State.Out;

        float squash;

        public static Bullet Create(Transform parent, Vector2 position, Vector2 velocity, Theme theme)
        {
            var b = Make(parent, "Bullet", position, PlaceholderArt.I.BulletFor(theme));
            b.State = Flight.Aimed(position, velocity);
            return b;
        }

        /// <summary>A bouncy bullet that starts falling from position and moves sideways toward dirX (+1 right, -1 left).</summary>
        public static Bullet CreateBouncy(Transform parent, Vector2 position, int dirX, int bounces, Theme theme)
        {
            var b = Make(parent, "Bouncy Bullet", position, PlaceholderArt.I.BouncyBulletFor(theme));
            b.State = Flight.BouncyFrom(position, dirX, bounces);
            return b;
        }

        static Bullet Make(Transform parent, string name, Vector2 position, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var b = go.AddComponent<Bullet>();
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            // Over every bubble. The Bubbles layer is above the Stage layer, so a bullet falling through the floor
            // is drawn in front of the ground strip.
            Sorting.Apply(sr, Layer.Bubbles, 20);
            Game.ApplySpriteMaterial(sr);
            return b;
        }

        /// <summary>The velocity of a bullet that leaves from and flies toward target at speed.</summary>
        public static Vector2 Aim(Vector2 from, Vector2 target, float speed)
        {
            Vector2 d = target - from;
            if (d.sqrMagnitude < 1e-6f) d = Vector2.down;
            return d.normalized * speed;
        }

        /// <summary>The upward speed that takes the top of a bullet of this radius, resting on the floor, to bouncyPeak.</summary>
        public static float BounceSpeed(float radius)
        {
            var t = Tuning.I;
            float rise = Mathf.Max(0f, t.bouncyPeak - 2f * radius);
            return Mathf.Sqrt(2f * t.bouncyGravity * rise);
        }

        public void Tick(float dt)
        {
            State.position = transform.position;
            if (State.Step(dt))
            {
                squash = SquashTime;
                Sfx.Play(Sound.Bounce, 1.5f, 0.25f);
            }
            transform.position = State.position;
            if (squash > 0f) squash -= dt;
            transform.localScale = squash > 0f ? new Vector3(1.25f, 0.75f, 1f) : Vector3.one;
        }

        public bool Overlaps(Rect rect) => State.Overlaps(rect);

        /// <summary>How a bullet moves, as a value type.</summary>
        public struct Flight
        {
            public Vector2 position, velocity;
            public float radius;
            public bool bouncy, sinking;
            public int bouncesLeft, floorTouches;

            public static Flight Aimed(Vector2 position, Vector2 velocity) => new Flight
            {
                position = position,
                velocity = velocity,
                radius = Tuning.I.bulletRadiusPixels / (float)Tuning.I.pixelsPerUnit,
            };

            public static Flight BouncyFrom(Vector2 position, int dirX, int bounces) => new Flight
            {
                position = position,
                velocity = new Vector2((dirX >= 0 ? 1f : -1f) * Tuning.I.bouncySpeedX, 0f),
                radius = Tuning.I.bouncyRadiusPixels / (float)Tuning.I.pixelsPerUnit,
                bouncy = true,
                bouncesLeft = Mathf.Max(0, bounces),
            };

            public bool CanHurt => !bouncy || position.y >= 0f;

            /// <summary>
            /// An aimed bullet is out when it has left the arena or reached the floor; a bouncy bullet when its centre
            /// is 1 unit below the floor.
            /// </summary>
            public bool Out
            {
                get
                {
                    if (bouncy) return position.y <= -1f;
                    float half = Tuning.I.HalfWidth;
                    return position.y - radius <= 0f || position.y - radius > Tuning.I.arenaHeight ||
                           position.x + radius < -half || position.x - radius > half;
                }
            }

            /// <summary>Moves the bullet by dt. Returns true when a bouncy bullet bounced on the floor in this step.</summary>
            public bool Step(float dt)
            {
                if (!bouncy)
                {
                    position += velocity * dt;
                    return false;
                }

                var t = Tuning.I;
                // Exact constant-gravity step, so the height of each hop does not depend on the frame rate.
                position.x += velocity.x * dt;
                position.y += velocity.y * dt - 0.5f * t.bouncyGravity * dt * dt;
                velocity.y -= t.bouncyGravity * dt;

                float half = t.HalfWidth;
                if (position.x - radius < -half) { position.x = -half + radius; velocity.x = Mathf.Abs(velocity.x); }
                else if (position.x + radius > half) { position.x = half - radius; velocity.x = -Mathf.Abs(velocity.x); }

                if (sinking || velocity.y >= 0f || position.y - radius > 0f) return false;
                floorTouches++;
                if (bouncesLeft <= 0)
                {
                    sinking = true; // keeps falling, through the floor
                    return false;
                }
                bouncesLeft--;
                position.y = radius;
                velocity.y = BounceSpeed(radius);
                return true;
            }

            public bool Overlaps(Rect rect)
            {
                float cx = Mathf.Clamp(position.x, rect.xMin, rect.xMax);
                float cy = Mathf.Clamp(position.y, rect.yMin, rect.yMax);
                float dx = position.x - cx, dy = position.y - cy;
                return dx * dx + dy * dy < radius * radius;
            }
        }
    }
}
