using System;
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

        /// <summary>A bullet of any kind from a pattern's launch (position, velocity, kind and the kind's own numbers).</summary>
        public static Bullet Create(Transform parent, Launch launch, Theme theme)
        {
            var art = PlaceholderArt.I;
            Sprite sprite;
            switch (launch.kind)
            {
                case BulletKind.Wave: sprite = art.WaveFor(theme); break;
                case BulletKind.Shell: sprite = art.ShellFor(theme); break;
                case BulletKind.Missile: sprite = art.MissileFor(theme); break;
                case BulletKind.Stream: sprite = art.StreamFor(theme); break;
                case BulletKind.Bouncy: sprite = art.BouncyBulletFor(theme); break;
                default: sprite = art.BulletFor(theme); break;
            }
            var b = Make(parent, launch.kind + " Bullet", launch.position, sprite);
            b.State = Flight.FromLaunch(launch);
            b.facing = launch.kind == BulletKind.Missile;
            b.flip = launch.kind == BulletKind.Wave && launch.velocity.x < 0f;
            if (launch.kind == BulletKind.Stream)
                b.baseScale = new Vector3(1f, b.State.columnHeight * Tuning.I.pixelsPerUnit / 8f, 1f); // the 8 px sprite stretched to the column
            return b;
        }

        bool facing, flip;
        Vector3 baseScale = Vector3.one;

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

        public void Tick(float dt, Vector2? wick = null)
        {
            State.position = transform.position;
            if (State.Step(dt, wick))
            {
                squash = SquashTime;
                Sfx.Play(Sound.Bounce, 1.5f, 0.25f);
            }
            transform.position = State.position;
            if (squash > 0f) squash -= dt;
            transform.localScale = squash > 0f ? new Vector3(1.25f, 0.75f, 1f) : baseScale;
            if (flip) transform.localScale = new Vector3(-baseScale.x, baseScale.y, 1f);
            if (facing) transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(State.velocity.y, State.velocity.x) * Mathf.Rad2Deg);
        }

        public bool Overlaps(Rect rect) => State.Overlaps(rect);

        /// <summary>How a bullet moves, as a value type.</summary>
        [Serializable]
        public struct Flight
        {
            public Vector2 position, velocity;
            public float radius;
            public bool bouncy, sinking;
            public int bouncesLeft, floorTouches;
            public BulletKind kind;
            /// <summary>Seconds left (missile, stream). A wave or shell ends on the map instead.</summary>
            public float life;
            /// <summary>Degrees per second a missile may turn toward Wick.</summary>
            public float turnRate;
            /// <summary>The y of the surface a wave rides on, and the height of a stream's column above position.y.</summary>
            public float surfaceY, columnHeight;
            public bool done;
            public Action<Vector2> onLand;

            public static Flight FromLaunch(Launch l)
            {
                var t = Tuning.I;
                float ppu = t.pixelsPerUnit;
                switch (l.kind)
                {
                    case BulletKind.Bouncy:
                        return BouncyFrom(l.position, l.velocity.x >= 0f ? 1 : -1, Mathf.RoundToInt(l.life));
                    case BulletKind.Wave:
                    {
                        float r = t.bossWaveRadiusPixels / ppu;
                        return new Flight { kind = l.kind, position = new Vector2(l.position.x, l.surfaceY + r), velocity = l.velocity, radius = r, surfaceY = l.surfaceY };
                    }
                    case BulletKind.Shell:
                        return new Flight { kind = l.kind, position = l.position, velocity = l.velocity, radius = t.bossShellRadiusPixels / ppu, onLand = l.onLand };
                    case BulletKind.Missile:
                        return new Flight { kind = l.kind, position = l.position, velocity = l.velocity, radius = t.bossMissileRadiusPixels / ppu, life = l.life, turnRate = l.turnRate };
                    case BulletKind.Stream:
                        return new Flight { kind = l.kind, position = l.position, velocity = l.velocity, radius = t.bossStreamHalfWidthPixels / ppu, life = l.life, columnHeight = StageMap.StoreyRows };
                    default:
                        var f = Aimed(l.position, l.velocity);
                        return f;
                }
            }

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
                    if (done) return true;
                    if (bouncy) return position.y <= -1f;
                    if (kind == BulletKind.Wave || kind == BulletKind.Shell || kind == BulletKind.Missile || kind == BulletKind.Stream)
                        return false;
                    float half = Tuning.I.HalfWidth;
                    return position.y - radius <= 0f || position.y - radius > Tuning.I.arenaHeight ||
                           position.x + radius < -half || position.x - radius > half;
                }
            }

            /// <summary>Moves the bullet by dt. Returns true when a bouncy bullet bounced on the floor in this step.</summary>
            public bool Step(float dt, Vector2? wick = null)
            {
                if (done) return false;
                switch (kind)
                {
                    case BulletKind.Wave: StepWave(dt); return false;
                    case BulletKind.Shell: StepShell(dt); return false;
                    case BulletKind.Missile: StepMissile(dt, wick); return false;
                    case BulletKind.Stream: StepStream(dt); return false;
                }
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

            /// <summary>True when a wave at x (its leading edge) on surfaceY would be stopped: a wall in front, or no ground under it.</summary>
            public static bool WaveBlocked(StageMap map, float frontX, float surfaceY, float radius)
            {
                int c = StageMap.Col(frontX);
                if (c < 0 || c >= StageMap.Cols) return true;
                if (map.IsSolid(c, StageMap.Row(surfaceY + radius))) return true; // a wall
                int below = StageMap.Row(surfaceY - 0.01f);
                if (below < 0) return map.IsGap(c);   // the floor: gone over a gap
                return !map.IsSolid(c, below);        // a platform: gone past its edge
            }

            void StepWave(float dt)
            {
                var map = Game.Map;
                float dir = velocity.x >= 0f ? 1f : -1f;
                float next = position.x + velocity.x * dt;
                if (map != null && WaveBlocked(map, next + dir * radius, surfaceY, radius)) { done = true; return; }
                position.x = next;
            }

            void StepShell(float dt)
            {
                var t = Tuning.I;
                position.x += velocity.x * dt;
                position.y += velocity.y * dt - 0.5f * t.bossShellGravity * dt * dt;
                velocity.y -= t.bossShellGravity * dt;
                var map = Game.Map;
                bool hitTile = velocity.y < 0f && map != null &&
                               map.OverlapsSolid(new Rect(position.x - radius, position.y - radius, radius * 2f, radius * 2f));
                if (position.y - radius > 0f && !hitTile)
                {
                    float half = t.HalfWidth;
                    if (position.x < -half || position.x > half) done = true;
                    return;
                }
                if (position.y - radius <= 0f) position.y = radius;
                done = true;
                var cb = onLand;
                onLand = null;
                cb?.Invoke(position);
            }

            void StepMissile(float dt, Vector2? wick)
            {
                life -= dt;
                if (life <= 0f) { done = true; return; }
                float speed = velocity.magnitude;
                if (wick.HasValue && turnRate > 0f)
                {
                    Vector2 to = wick.Value - position;
                    if (to.sqrMagnitude > 1e-6f)
                    {
                        float now = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
                        float want = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
                        float turn = Mathf.Clamp(Mathf.DeltaAngle(now, want), -turnRate * dt, turnRate * dt);
                        float a = (now + turn) * Mathf.Deg2Rad;
                        velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed;
                    }
                }
                position += velocity * dt;
                var map = Game.Map;
                float half = Tuning.I.HalfWidth;
                if (position.x < -half || position.x > half || position.y < 0f ||
                    (map != null && map.OverlapsSolid(new Rect(position.x - radius, position.y - radius, radius * 2f, radius * 2f))))
                    done = true;
            }

            void StepStream(float dt)
            {
                life -= dt;
                position += velocity * dt;
                if (life <= 0f) done = true;
            }

            public bool Overlaps(Rect rect)
            {
                if (kind == BulletKind.Stream)
                    return rect.xMax > position.x - radius && rect.xMin < position.x + radius &&
                           rect.yMax > position.y && rect.yMin < position.y + columnHeight;
                float cx = Mathf.Clamp(position.x, rect.xMin, rect.xMax);
                float cy = Mathf.Clamp(position.y, rect.yMin, rect.yMax);
                float dx = position.x - cx, dy = position.y - cy;
                return dx * dx + dy * dy < radius * radius;
            }
        }
    }
}
