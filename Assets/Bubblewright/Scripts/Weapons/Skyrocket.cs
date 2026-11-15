using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>
    /// W14 Skyrocket: a firework. A press launches one rocket straight up (straight down when falling with Down held)
    /// that speeds up from rocketSpeed to rocketTopSpeed. It bursts on the first bubble (steel too; a faded Ghost lets it
    /// through), rolling balloon, boss, solid tile, ceiling or floor it touches. The burst hits everything within
    /// rocketRadius once (things born from the burst are not hit), hits each breakable block in it once, destroys boss
    /// bullets in it and never hurts Wick. One rocket flies at a time and the touch button is dim meanwhile and during
    /// the rocketCooldown after it.
    /// The rocket's ammo is spent when it bursts, not when it leaves: the slot (and this behaviour) only goes away at
    /// zero, so the last rocket still flies and bursts.
    /// </summary>
    [Preserve]
    public sealed class Skyrocket : WeaponSpec
    {
        public override string Id => "rocket";
        public override string Name => "Skyrocket";
        public override WeaponTier Tier => WeaponTier.Rare;
        public override int MaxAmmo => Mathf.Max(1, Tuning.I.rocketAmmo);
        public override int Order => 14;

        static readonly string[] Icon11 =
        {
            ".....R.....",
            "....RRR....",
            "...RRRRR...",
            "...CCCCC...",
            "...CYCYC...",
            "...CCCCC...",
            "...DDDDD...",
            ".....S.....",
            ".....S.....",
            ".....S.....",
        };

        static readonly string[] Hand =
        {
            "..R..",
            ".RRR.",
            ".CCC.",
            ".CYC.",
            ".CCC.",
            ".DDD.",
            "..S..",
            "..S..",
            "..S..",
        };

        /// <summary>The rocket in flight, nose on top. Its pivot is the nose.</summary>
        static readonly string[] Flying =
        {
            ".R.",
            "RRR",
            "CCC",
            "CYC",
            "CCC",
            "DDD",
            ".S.",
            ".S.",
        };

        public override string[] IconRows => Icon11;
        public override string[] HeldRows => Hand;
        public override WeaponBehaviour CreateBehaviour() => new Behaviour();

        /// <summary>Pixels of the rocket body that touch things: 3 wide, 6 long (the stick does not count).</summary>
        public const int BodyWidthPixels = 3, BodyLengthPixels = 6;

        /// <summary>A rocket that has left. Nose is its front tip; Direction is +1 going up, -1 going down.</summary>
        public sealed class Rocket
        {
            public Vector2 Nose;
            public int Direction = 1;
            public float Speed;
            public float Age;
            internal SpriteRenderer view;
            internal float smoke;
        }

        /// <summary>What one burst did, for the effects and the tests.</summary>
        public struct Burst
        {
            public Vector2 centre;
            public int bubbles, rollers, bullets, steel, tiles;
            public bool boss;
        }

        /// <summary>A new rocket at the muzzle, pointing along aim (up, or down for a down shot).</summary>
        public static Rocket Launch(Vector2 muzzle, Vector2 aim)
        {
            return new Rocket { Nose = muzzle, Direction = aim.y < 0f ? -1 : 1, Speed = Tuning.I.rocketSpeed };
        }

        /// <summary>The part of the rocket that touches things: the body behind the nose.</summary>
        public static Rect BodyBox(Rocket r)
        {
            float px = Tuning.I.Pixel, w = BodyWidthPixels * px, h = BodyLengthPixels * px;
            float y = r.Direction > 0 ? r.Nose.y - h : r.Nose.y;
            return new Rect(r.Nose.x - w * 0.5f, y, w, h);
        }

        /// <summary>
        /// Moves the rocket for dt in small steps, speeding up. True when it touched something; burstAt is then where it
        /// bursts (the nose, kept out of the tile, the ceiling or the floor it hit).
        /// </summary>
        public static bool Fly(WeaponWorld world, Rocket r, float dt, out Vector2 burstAt)
        {
            var t = Tuning.I;
            r.Age += dt;
            float top = Mathf.Max(t.rocketSpeed, t.rocketTopSpeed);
            float start = r.Speed;
            r.Speed = Mathf.Min(top, r.Speed + Mathf.Max(0f, t.rocketAccel) * dt);
            float dist = (start + r.Speed) * 0.5f * dt;
            int steps = Mathf.Max(1, Mathf.CeilToInt(dist / 0.2f));
            float step = dist / steps;
            var map = world != null ? world.Map : Game.Map;

            // Something already touching it at the muzzle sets it off at once.
            if (Touches(world, r))
            {
                burstAt = r.Nose;
                return true;
            }
            for (int k = 0; k < steps; k++)
            {
                Vector2 next = r.Nose + new Vector2(0f, step * r.Direction);
                if (r.Direction > 0 && next.y >= t.arenaHeight)
                {
                    burstAt = new Vector2(r.Nose.x, t.arenaHeight);
                    r.Nose = burstAt;
                    return true;
                }
                if (r.Direction < 0 && next.y <= 0f)
                {
                    burstAt = new Vector2(r.Nose.x, 0f);
                    r.Nose = burstAt;
                    return true;
                }
                if (map != null && map.IsSolid(StageMap.Col(next.x), StageMap.Row(next.y)))
                {
                    // Stop at the tile's edge, so the burst is just outside the tile it hit.
                    int row = StageMap.Row(next.y);
                    float edge = r.Direction > 0 ? row : row + 1f;
                    burstAt = new Vector2(r.Nose.x, edge - 0.01f * r.Direction);
                    r.Nose = burstAt;
                    return true;
                }
                r.Nose = next;
                if (Touches(world, r))
                {
                    burstAt = r.Nose;
                    return true;
                }
            }
            burstAt = r.Nose;
            return false;
        }

        /// <summary>A bubble (steel too, not a faded Ghost), a rolling balloon or the boss overlaps the rocket body.</summary>
        static bool Touches(WeaponWorld world, Rocket r)
        {
            if (world == null) return false;
            Rect box = BodyBox(r);
            foreach (var b in world.Bubbles)
            {
                if (b == null || b.Popped || !b.Overlaps(box)) continue;
                if (b.Steel || b.CanBeHit) return true;
            }
            foreach (var roller in world.Rollers)
                if (roller != null && !roller.Popped && roller.Overlaps(box)) return true;
            var boss = world.LiveBoss;
            return boss != null && boss.Body.Overlaps(box);
        }

        /// <summary>
        /// The burst: everything within rocketRadius of centre is hit once. The list is taken first, so the halves its
        /// pops make are not hit. Steel sparkles and is unharmed. Boss bullets in it are destroyed. The boss takes
        /// rocketBossDamage. Each breakable block it reaches takes one hit.
        /// </summary>
        public static Burst Explode(WeaponWorld world, Vector2 centre, List<WeaponTarget> scratch = null)
        {
            var t = Tuning.I;
            var result = new Burst { centre = centre };
            float radius = Mathf.Max(0f, t.rocketRadius);
            var targets = world.InCircle(centre, radius, scratch).ToArray();
            foreach (var target in targets)
            {
                switch (target.kind)
                {
                    case TargetKind.Bubble:
                        if (target.bubble.Steel) result.steel++;
                        if (world.WeaponHit(target.bubble)) result.bubbles++;
                        break;
                    case TargetKind.Roller:
                        if (target.roller.Steel) result.steel++;
                        if (world.WeaponHit(target.roller)) result.rollers++;
                        break;
                    case TargetKind.Bullet:
                        if (world.Hit(target, 0f)) result.bullets++;
                        break;
                    case TargetKind.Boss:
                        if (world.LiveBoss == null) break;
                        world.WeaponHitBoss(t.rocketBossDamage, target.position);
                        result.boss = true;
                        break;
                }
            }
            result.tiles = HitTiles(world, centre, radius);
            return result;
        }

        /// <summary>One hit on every breakable block the burst reaches. Returns how many were hit.</summary>
        static int HitTiles(WeaponWorld world, Vector2 centre, float radius)
        {
            var map = world.Map;
            if (map == null) return 0;
            int hit = 0;
            int c0 = Mathf.Max(0, StageMap.Col(centre.x - radius)), c1 = Mathf.Min(StageMap.Cols - 1, StageMap.Col(centre.x + radius));
            int r0 = Mathf.Max(0, StageMap.Row(centre.y - radius)), r1 = Mathf.Min(StageMap.Rows - 1, StageMap.Row(centre.y + radius));
            for (int c = c0; c <= c1; c++)
            for (int r = r0; r <= r1; r++)
            {
                if (map.Get(c, r).kind != TileKind.Breakable) continue;
                var cell = StageMap.CellRect(c, r);
                var closest = new Vector2(Mathf.Clamp(centre.x, cell.xMin, cell.xMax), Mathf.Clamp(centre.y, cell.yMin, cell.yMax));
                if (Vector2.Distance(closest, centre) > radius) continue;
                world.OnHitTile?.Invoke(c, r);
                hit++;
            }
            return hit;
        }

        public sealed class Behaviour : WeaponBehaviour
        {
            readonly List<WeaponTarget> scratch = new List<WeaponTarget>();

            /// <summary>The rocket in flight, or null.</summary>
            public Rocket Flight { get; private set; }
            /// <summary>Seconds left before the next rocket can leave.</summary>
            public float Cooldown { get; private set; }
            /// <summary>The last burst, for the tests.</summary>
            public Burst? LastBurst { get; private set; }

            /// <summary>A rocket is up, or the launcher is still cooling: the button does nothing.</summary>
            public override bool Dimmed => Flight != null || Cooldown > 0f;

            public override void Tick(WeaponContext ctx, float dt)
            {
                if (Cooldown > 0f) Cooldown = Mathf.Max(0f, Cooldown - dt);
                if (Flight != null) TickFlight(ctx, dt);
                if (!ctx.Pressed || Dimmed || !ctx.CanFire) return;
                if (ctx.Run != null && ctx.Run.WeaponAmmo < 1) return;

                Flight = Launch(ctx.Muzzle, ctx.Up);
                Cooldown = Tuning.I.rocketCooldown;
                MakeView(Flight);
                ctx.Flash();
            }

            void TickFlight(WeaponContext ctx, float dt)
            {
                var r = Flight;
                if (!Fly(ctx.World, r, dt, out Vector2 at))
                {
                    UpdateView(r, dt);
                    return;
                }
                LastBurst = Explode(ctx.World, at, scratch);
                Game.RocketBurstFx(at);
                RemoveView(r);
                Flight = null;
                ctx.Spend(); // last: at zero the slot empties and this behaviour is disposed
            }

            /// <summary>The stage was cleared with a rocket up: it fizzles with a puff, hits nothing and is spent.</summary>
            public bool Fizzle()
            {
                if (Flight == null) return false;
                Game.RocketPuff(Flight.Nose);
                RemoveView(Flight);
                Flight = null;
                return true;
            }

            public override void Dispose()
            {
                if (Flight != null) RemoveView(Flight);
                Flight = null;
                Cooldown = 0f;
            }
        }

        // ------------------------------------------------------------------ look

        static Sprite flyingSprite;

        static Sprite FlyingSprite
        {
            get
            {
                if (flyingSprite != null) return flyingSprite;
                var c = WeaponArt.Outlined(Flying);
                // Pivot on the nose: the top pixel row inside the outline.
                flyingSprite = c.ToSprite("SkyrocketFlying", new Vector2(c.Width * 0.5f, c.Height - 1f), Tuning.I.pixelsPerUnit);
                return flyingSprite;
            }
        }

        static void MakeView(Rocket r)
        {
            var go = new GameObject("Skyrocket");
            var parent = Game.WeaponParent;
            if (parent != null) go.transform.SetParent(parent, false);
            r.view = go.AddComponent<SpriteRenderer>();
            r.view.sprite = FlyingSprite;
            Sorting.Apply(r.view, Layer.Shots, 2);
            Game.ApplySpriteMaterial(r.view);
            // Pointing down: mirrored about the nose.
            go.transform.localScale = new Vector3(1f, r.Direction, 1f);
            UpdateView(r, 0f);
        }

        /// <summary>The rocket wobbles a pixel side to side and puffs grey smoke behind it.</summary>
        static void UpdateView(Rocket r, float dt)
        {
            if (r.view == null) return;
            float px = Tuning.I.Pixel;
            float wobble = Mathf.Repeat(r.Age, 0.1f) < 0.05f ? px : 0f;
            r.view.transform.position = new Vector3(r.Nose.x + wobble, r.Nose.y, 0f);
            r.smoke -= dt;
            if (r.smoke <= 0f)
            {
                r.smoke = 0.05f;
                Game.RocketSmoke(r.Nose - new Vector2(0f, (BodyLengthPixels + 2) * px * r.Direction));
            }
        }

        static void RemoveView(Rocket r)
        {
            if (r.view == null) return;
            if (Application.isPlaying) Object.Destroy(r.view.gameObject);
            else Object.DestroyImmediate(r.view.gameObject);
            r.view = null;
        }
    }

    public partial class Game
    {
        /// <summary>Where weapon objects live in the running game (null with no game, as in tests).</summary>
        public static Transform WeaponParent => active != null ? (active.weaponRoot != null ? active.weaponRoot : active.world) : null;

        /// <summary>Called from ClearWeaponShots: a rocket still up when the stage is cleared fizzles and is spent.</summary>
        void FizzleRocket()
        {
            if (weaponBehaviour is Skyrocket.Behaviour sky && sky.Fizzle()) run.SpendAmmo(WeaponSlot.Weapon);
        }

        /// <summary>Skyrocket burst: a white flash, then a ring of red, yellow and cream specks, a pop and a small shake.</summary>
        public static void RocketBurstFx(Vector2 at)
        {
            var g = active;
            if (g == null) return;
            g.fx.Firework(at, g.T.rocketRadius);
            g.shake = Mathf.Max(g.shake, g.T.shakeTime * 0.5f);
            Sfx.Play(Sound.Pop, 0.75f, 0.9f); // placeholder burst
        }

        /// <summary>A puff of smoke behind a rocket in flight.</summary>
        public static void RocketSmoke(Vector2 at)
        {
            var g = active;
            if (g == null) return;
            g.fx.Dust(at, 0f);
        }

        /// <summary>A rocket fizzling out at stage end.</summary>
        public static void RocketPuff(Vector2 at)
        {
            var g = active;
            if (g == null) return;
            g.fx.Sparkle(at, Palette.Cream);
            Sfx.Play(Sound.Pop, 1.6f, 0.3f);
        }
    }

    public partial class Tuning
    {
        [Header("Weapon: Skyrocket")]
        [Tooltip("Rockets in a full Skyrocket crate.")]
        public int rocketAmmo = 4;
        [Tooltip("Units per second a Skyrocket leaves at. It speeds up to Rocket Top Speed.")]
        public float rocketSpeed = 16f;
        [Tooltip("Most units per second a Skyrocket reaches.")]
        public float rocketTopSpeed = 30f;
        [Tooltip("Units per second added to a Skyrocket's speed each second until it reaches Rocket Top Speed.")]
        public float rocketAccel = 28f;
        [Tooltip("Units from the burst that a Skyrocket reaches. Everything inside is hit once.")]
        public float rocketRadius = 2.5f;
        [Tooltip("Seconds after a launch before the next Skyrocket can leave. One rocket flies at a time either way.")]
        public float rocketCooldown = 0.8f;
        [Tooltip("Boss health one Skyrocket burst takes.")]
        public float rocketBossDamage = 3f;
    }
}
