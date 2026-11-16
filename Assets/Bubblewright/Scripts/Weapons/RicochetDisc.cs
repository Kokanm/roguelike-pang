using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>
    /// W15 Ricochet Disc: each press launches a spinning steel disc at discAngle degrees off straight up, leaning to the
    /// side Wick is moving or last moved (downward at the same slant when falling with Down held). It flies straight at
    /// discSpeed and bounces off the arena walls, the ceiling, the floor line, solid tiles, steel and the boss, passing
    /// through what it pops. It ends after discBounces bounces, discPops pops or discLife seconds, flickering for
    /// discFlickerTime first, then breaks into steel specks. At most discMaxAlive fly at once. It never hurts Wick.
    /// The discs live in a DiscField on the WeaponWorld, so the last one keeps flying after the slot empties.
    /// </summary>
    [Preserve]
    public sealed class RicochetDisc : WeaponSpec
    {
        public override string Id => "disc";
        public override string Name => "Ricochet Disc";
        public override WeaponTier Tier => WeaponTier.Uncommon;
        public override int MaxAmmo => Tuning.I.discAmmo;
        public override int Order => 15;

        static readonly string[] Icon11 =
        {
            "...LLLLL...",
            "..LSSSSSD..",
            ".LSSYYSSSD.",
            ".LSYWWYSSD.",
            ".LSYWWYSSD.",
            ".LSSYYSSSD.",
            "..DSSSSDD..",
            "...DDDDD...",
        };

        static readonly string[] Hand =
        {
            ".LLLLL.",
            "LSSYSSD",
            "LSYWYSD",
            "LSSYSSD",
            ".DDDDD.",
            "...D...",
        };

        public override string[] IconRows => Icon11;
        public override string[] HeldRows => Hand;
        public override WeaponBehaviour CreateBehaviour() => new Behaviour();

        /// <summary>
        /// The launch direction: discAngle degrees off straight up toward side (-1 left, +1 right), or off straight down
        /// when down is true.
        /// </summary>
        public static Vector2 LaunchDirection(int side, bool down)
        {
            float a = Tuning.I.discAngle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(a) * (side < 0 ? -1f : 1f), Mathf.Cos(a) * (down ? -1f : 1f));
        }

        /// <summary>The discs in flight in this world (made on first use).</summary>
        public static DiscField FieldOf(WeaponWorld world) => world.Field(() => new DiscField(world));

        public sealed class Behaviour : WeaponBehaviour
        {
            /// <summary>The side he is moving, or last moved: -1 left, +1 right. Starts right.</summary>
            public int Side = 1;
            DiscField field;

            /// <summary>Dim while the most discs allowed are already in the air.</summary>
            public override bool Dimmed => field != null && field.Count >= Tuning.I.discMaxAlive;

            public override void Tick(WeaponContext ctx, float dt)
            {
                if (ctx.World == null) return;
                field ??= FieldOf(ctx.World);
                if (ctx.Wick != null && Mathf.Abs(ctx.Wick.VelocityX) > 0.05f) Side = ctx.Wick.VelocityX < 0f ? -1 : 1;

                if (!ctx.Pressed || !ctx.CanFire) return;
                if (field.Count >= Tuning.I.discMaxAlive) return; // nothing spent
                if (!ctx.Spend()) return;

                field.Launch(ctx.Muzzle, LaunchDirection(Side, ctx.ShootDown) * Tuning.I.discSpeed);
                ctx.Flash();
            }

            // The discs belong to the field, not the slot: the last one flies on after the slot empties.
            public override void Dispose() => field = null;
        }
    }

    /// <summary>One Ricochet Disc in flight.</summary>
    public sealed class Disc
    {
        public Vector2 Position;
        public Vector2 Velocity;
        /// <summary>Where it was fired from. Bounces stay inside that storey.</summary>
        public float OriginY;
        public int Bounces;
        public int Pops;
        public float Age;
        /// <summary>It already took its one boss hit.</summary>
        public bool BossHit;
        /// <summary>Seconds of flicker left once it has ended; below 0 while it still works. A flickering disc hits nothing.</summary>
        public float Flicker = -1f;
        public bool Alive = true;

        public bool Ending => Flicker >= 0f;

        /// <summary>Things it is touching now: each touch hits once, so it must leave a thing before it can hit it again.</summary>
        internal readonly HashSet<object> touching = new HashSet<object>();
        /// <summary>Bubbles and rolling balloons that existed at its last tick. A newcomer already overlapping it (the halves of its own pop) is spared until they part.</summary>
        internal readonly HashSet<object> known = new HashSet<object>();
        /// <summary>Where it popped things (x, y, radius). What is born there is its own pop's halves.</summary>
        internal readonly List<Vector3> popSpots = new List<Vector3>();
        /// <summary>The halves of its own pops: it never hits them.</summary>
        internal readonly HashSet<object> spared = new HashSet<object>();

        internal GameObject go;
        internal SpriteRenderer sr;
        internal readonly SpriteRenderer[] trail = new SpriteRenderer[3];
        internal readonly Vector2[] history = new Vector2[8];
        internal int historyCount;
        internal bool squashSideways, squashNow;
    }

    /// <summary>
    /// Every Ricochet Disc in flight, kept on the WeaponWorld so discs outlive the weapon's slot. Ticked after the shots
    /// and cleared with them. All hits go through WeaponWorld's shared helpers.
    /// </summary>
    public sealed class DiscField : IWeaponField
    {
        const float Step = 0.1f, Skin = 0.01f;

        readonly WeaponWorld world;
        readonly List<Disc> discs = new List<Disc>();
        readonly List<WeaponTarget> scratch = new List<WeaponTarget>();
        readonly HashSet<object> now = new HashSet<object>();

        public DiscField(WeaponWorld world) { this.world = world; }

        public IReadOnlyList<Disc> Discs => discs;
        public int Count => discs.Count;

        /// <summary>The disc's hit radius: half its 7 px body.</summary>
        public static float Radius => 3.5f * Tuning.I.Pixel;

        public Disc Launch(Vector2 from, Vector2 velocity)
        {
            var d = new Disc { Position = from, Velocity = velocity, OriginY = from.y };
            foreach (var b in world.Bubbles) if (b != null && !b.Popped) d.known.Add(b);
            foreach (var r in world.Rollers) if (r != null && !r.Popped) d.known.Add(r);
            DiscArt.Build(d, world.Root);
            discs.Add(d);
            DiscArt.Show(d);
            return d;
        }

        public void Tick(float dt)
        {
            var t = Tuning.I;
            for (int i = discs.Count - 1; i >= 0; i--)
            {
                var d = discs[i];
                d.Age += dt;
                d.squashNow = false;
                float dist = d.Velocity.magnitude * dt;
                int steps = Mathf.Max(1, Mathf.CeilToInt(dist / Step));
                Vector2 step = d.Velocity * (dt / steps);
                for (int k = 0; k < steps && d.Alive; k++)
                {
                    Move(d, 0, step.x);
                    if (d.Alive) Move(d, 1, step.y);
                    step = d.Velocity * (dt / steps); // a bounce turned it
                    if (d.Alive && !d.Ending) Touch(d);
                    if (d.Alive && !d.Ending && (d.Bounces >= t.discBounces || d.Pops >= t.discPops)) d.Flicker = t.discFlickerTime;
                }
                if (d.Alive && !d.Ending && d.Age >= t.discLife) d.Flicker = t.discFlickerTime;
                if (d.Alive && d.Ending)
                {
                    d.Flicker -= dt;
                    if (d.Flicker <= 0f)
                    {
                        d.Alive = false;
                        Game.DiscBreakFx(d.Position);
                    }
                }
                if (d.Alive) Remember(d);
                DiscArt.Show(d);
                if (!d.Alive)
                {
                    DiscArt.Destroy(d);
                    discs.RemoveAt(i);
                }
            }
        }

        public void Clear()
        {
            foreach (var d in discs)
            {
                d.Alive = false;
                DiscArt.Destroy(d);
            }
            discs.Clear();
        }

        /// <summary>Notes what is alive now. Anything new that appeared where it popped something is spared.</summary>
        void Remember(Disc d)
        {
            if (d.popSpots.Count > 0)
            {
                foreach (var b in world.Bubbles) if (b != null && !b.Popped && !d.known.Contains(b) && BornAtPop(d, b.Position)) d.spared.Add(b);
                foreach (var r in world.Rollers) if (r != null && !r.Popped && !d.known.Contains(r) && BornAtPop(d, r.Position)) d.spared.Add(r);
            }
            d.known.Clear();
            foreach (var b in world.Bubbles) if (b != null && !b.Popped) d.known.Add(b);
            foreach (var r in world.Rollers) if (r != null && !r.Popped) d.known.Add(r);
        }

        static bool BornAtPop(Disc d, Vector2 at)
        {
            foreach (var s in d.popSpots)
                if (Vector2.Distance(at, new Vector2(s.x, s.y)) <= s.z + 0.5f) return true;
            return false;
        }

        // ------------------------------------------------------------------ walls and tiles

        /// <summary>Moves along one axis (0 x, 1 y) and bounces off the arena edge or a solid tile it runs into.</summary>
        void Move(Disc d, int axis, float delta)
        {
            if (Mathf.Abs(delta) < 1e-7f) return;
            var t = Tuning.I;
            float r = Radius;
            Vector2 p = d.Position;
            p[axis] += delta;

            if (axis == 0)
            {
                if (p.x - r < -t.HalfWidth) { p.x = -t.HalfWidth + r; Turn(d, 0, 1f, p); return; }
                if (p.x + r > t.HalfWidth) { p.x = t.HalfWidth - r; Turn(d, 0, -1f, p); return; }
            }
            else
            {
                var map = world.Map;
                float ceil = map != null ? map.StoreyCeiling(d.OriginY) : t.arenaHeight;
                float baseLine = map != null ? map.StoreyBase(d.OriginY) : 0f;
                if (p.y + r > ceil) { p.y = ceil - r; Turn(d, 1, -1f, p); return; }
                if (p.y - r < baseLine)
                {
                    if (baseLine <= 0.01f && (p.y < 0f || (map != null && map.IsGap(StageMap.Col(p.x)))))
                    {
                        // Over a gap it drops through, and once its middle is below the floor it is lost.
                        d.Position = p;
                        if (p.y < 0f) d.Alive = false;
                        return;
                    }
                    p.y = baseLine + r;
                    Turn(d, 1, 1f, p);
                    return;
                }
            }

            if (SolidCellIn(p, r, d.OriginY, out int col, out int row))
            {
                Rect cell = StageMap.CellRect(col, row);
                float sign = delta > 0f ? -1f : 1f;
                if (axis == 0) p.x = delta > 0f ? cell.xMin - r - Skin : cell.xMax + r + Skin;
                else p.y = delta > 0f ? cell.yMin - r - Skin : cell.yMax + r + Skin;
                if (!d.Ending && world.Map.Get(col, row).kind == TileKind.Breakable) world.OnHitTile?.Invoke(col, row);
                Turn(d, axis, sign, p);
                return;
            }
            d.Position = p;
        }

        /// <summary>The first solid cell inside the arena that the disc's box overlaps.</summary>
        bool SolidCellIn(Vector2 p, float r, float originY, out int col, out int row)
        {
            col = row = -1;
            var map = world.Map;
            if (map == null) return false;
            int lo = (int)map.StoreyBase(originY);
            int hi = (int)map.StoreyCeiling(originY);
            int c0 = Mathf.Max(0, StageMap.Col(p.x - r + 1e-4f)), c1 = Mathf.Min(StageMap.Cols - 1, StageMap.Col(p.x + r - 1e-4f));
            int r0 = Mathf.Max(lo, StageMap.Row(p.y - r + 1e-4f)), r1 = Mathf.Min(hi - 1, StageMap.Row(p.y + r - 1e-4f));
            for (int c = c0; c <= c1; c++)
                for (int rr = r0; rr <= r1; rr++)
                    if (map.IsSolid(c, rr)) { col = c; row = rr; return true; }
            return false;
        }

        /// <summary>Points the velocity along sign on axis, puts the disc at p and counts the bounce.</summary>
        void Turn(Disc d, int axis, float sign, Vector2 p)
        {
            d.Position = p;
            Vector2 v = d.Velocity;
            v[axis] = Mathf.Abs(v[axis]) * sign;
            d.Velocity = v;
            Bounced(d, axis == 0);
        }

        void Bounced(Disc d, bool sideways)
        {
            d.squashNow = true;
            d.squashSideways = sideways;
            if (d.Ending) return; // a flickering disc still bounces, but it no longer counts
            d.Bounces++;
            Game.DiscBounceFx(d.Position);
        }

        // ------------------------------------------------------------------ things it touches

        void Touch(Disc d)
        {
            var t = Tuning.I;
            float r = Radius;
            world.InCircle(d.Position, r, scratch);
            now.Clear();
            foreach (var target in scratch)
            {
                if (target.kind == TargetKind.Bullet) continue;
                object key = Key(target);
                now.Add(key);
                if (d.touching.Contains(key)) continue;
                d.touching.Add(key);
                if (d.Pops >= t.discPops) continue;

                switch (target.kind)
                {
                    case TargetKind.Bubble:
                        if (target.bubble.Steel) { world.WeaponHit(target.bubble); BounceOffCircle(d, target.bubble.Position, target.bubble.Radius); break; }
                        if (!d.known.Contains(target.bubble) || d.spared.Contains(target.bubble)) break; // its own pop's halves
                        if (world.WeaponHit(target.bubble)) Popped(d, target.position, target.bubble.Radius);
                        break;
                    case TargetKind.Roller:
                        if (target.roller.Steel) { world.WeaponHit(target.roller); BounceOffCircle(d, target.roller.Position, target.roller.Radius); break; }
                        if (!d.known.Contains(target.roller) || d.spared.Contains(target.roller)) break;
                        if (world.WeaponHit(target.roller)) Popped(d, target.position, target.roller.Radius);
                        break;
                    case TargetKind.Boss:
                        if (!d.BossHit)
                        {
                            d.BossHit = true;
                            world.WeaponHitBoss(t.discBossDamage, d.Position);
                        }
                        BounceOffRect(d, target.boss.Body);
                        break;
                }
            }
            d.touching.RemoveWhere(k => !now.Contains(k));
        }

        static void Popped(Disc d, Vector2 at, float radius)
        {
            d.Pops++;
            d.popSpots.Add(new Vector3(at.x, at.y, radius));
        }

        static object Key(WeaponTarget t)
        {
            switch (t.kind)
            {
                case TargetKind.Bubble: return t.bubble;
                case TargetKind.Roller: return t.roller;
                case TargetKind.Boss: return t.boss;
                default: return t.bullet;
            }
        }

        void BounceOffCircle(Disc d, Vector2 centre, float radius)
        {
            Vector2 n = d.Position - centre;
            if (n.sqrMagnitude < 1e-8f) n = -d.Velocity;
            n.Normalize();
            float along = Vector2.Dot(d.Velocity, n);
            if (along < 0f) d.Velocity -= 2f * along * n;
            d.Position = centre + n * (radius + Radius + Skin);
            Bounced(d, Mathf.Abs(n.x) > Mathf.Abs(n.y));
        }

        void BounceOffRect(Disc d, Rect body)
        {
            float r = Radius;
            Vector2 p = d.Position, v = d.Velocity;
            // Push out the shortest way and turn that axis round.
            float left = p.x + r - body.xMin, right = body.xMax - (p.x - r);
            float down = p.y + r - body.yMin, up = body.yMax - (p.y - r);
            float best = Mathf.Min(Mathf.Min(left, right), Mathf.Min(down, up));
            bool sideways;
            if (best == left) { p.x = body.xMin - r - Skin; v.x = -Mathf.Abs(v.x); sideways = true; }
            else if (best == right) { p.x = body.xMax + r + Skin; v.x = Mathf.Abs(v.x); sideways = true; }
            else if (best == down) { p.y = body.yMin - r - Skin; v.y = -Mathf.Abs(v.y); sideways = false; }
            else { p.y = body.yMax + r + Skin; v.y = Mathf.Abs(v.y); sideways = false; }
            d.Position = p;
            d.Velocity = v;
            Bounced(d, sideways);
        }
    }

    /// <summary>
    /// The disc's look: a 7 px steel disc with a yellow hub in 4 spin frames, a 3-dot cream trail, a 1-frame squash on
    /// each bounce and a flicker before it breaks.
    /// </summary>
    static class DiscArt
    {
        const int Frames = 4;
        static Sprite[] spin;
        static Sprite dot;

        static Sprite[] Spin
        {
            get
            {
                if (spin != null && spin[0] != null) return spin;
                spin = new Sprite[Frames];
                // A notch on the rim walks a quarter turn per frame so the spin reads.
                var notch = new[] { new Vector2Int(3, 6), new Vector2Int(6, 3), new Vector2Int(3, 0), new Vector2Int(0, 3) };
                for (int f = 0; f < Frames; f++)
                {
                    var c = new PixelCanvas(9, 9);
                    for (int y = 0; y < 7; y++)
                        for (int x = 0; x < 7; x++)
                        {
                            float dx = x - 3f, dy = y - 3f;
                            if (dx * dx + dy * dy > 11.5f) continue;
                            Color32 col = Palette.Steel;
                            if (dx * dx + dy * dy > 5.5f) col = dx + dy > 0f ? Palette.SteelLight : Palette.SteelDark;
                            c.Set(x + 1, y + 1, col);
                        }
                    c.Set(notch[f].x + 1, notch[f].y + 1, Palette.SteelDark);
                    for (int i = -1; i <= 1; i++) { c.Set(4 + i, 4, Palette.SparkCore); c.Set(4, 4 + i, Palette.SparkCore); }
                    c.Set(4, 4, new Color32(255, 255, 255, 255));
                    c.Outline(Palette.Ink);
                    spin[f] = c.ToSprite("RicochetDisc" + f, new Vector2(4.5f, 4.5f), Tuning.I.pixelsPerUnit);
                }
                return spin;
            }
        }

        static Sprite Dot
        {
            get
            {
                if (dot != null) return dot;
                var c = new PixelCanvas(1, 1);
                c.Set(0, 0, Palette.Cream);
                dot = c.ToSprite("RicochetDiscTrail", new Vector2(0.5f, 0.5f), Tuning.I.pixelsPerUnit);
                return dot;
            }
        }

        public static void Build(Disc d, Transform parent)
        {
            d.go = new GameObject("RicochetDisc");
            if (parent != null) d.go.transform.SetParent(parent, false);
            d.sr = d.go.AddComponent<SpriteRenderer>();
            d.sr.sprite = Spin[0];
            Sorting.Apply(d.sr, Layer.Shots, 2);
            Game.ApplySpriteMaterial(d.sr);
            for (int i = 0; i < d.trail.Length; i++)
            {
                var go = new GameObject("Trail");
                go.transform.SetParent(parent != null ? parent : d.go.transform.parent, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Dot;
                sr.enabled = false;
                Sorting.Apply(sr, Layer.Shots, 1);
                Game.ApplySpriteMaterial(sr);
                d.trail[i] = sr;
            }
        }

        public static void Show(Disc d)
        {
            if (d.go == null) return;
            float px = Tuning.I.Pixel;
            Vector2 snapped = new Vector2(Mathf.Round(d.Position.x / px) * px, Mathf.Round(d.Position.y / px) * px);
            d.go.transform.position = snapped;
            int frame = Mathf.FloorToInt(d.Age * 24f) % 4;
            if (d.Velocity.x < 0f) frame = 3 - frame; // spins the way it travels
            d.sr.sprite = Spin[frame];
            d.go.transform.localScale = !d.squashNow ? Vector3.one : d.squashSideways ? new Vector3(0.72f, 1.28f, 1f) : new Vector3(1.28f, 0.72f, 1f);
            d.sr.enabled = d.Alive && (!d.Ending || Mathf.FloorToInt(d.Flicker * 30f) % 2 == 0);

            // Trail: the spots it was at 2, 4 and 6 ticks ago.
            for (int i = d.history.Length - 1; i > 0; i--) d.history[i] = d.history[i - 1];
            d.history[0] = snapped;
            d.historyCount = Mathf.Min(d.historyCount + 1, d.history.Length);
            for (int i = 0; i < d.trail.Length; i++)
            {
                int back = 2 * (i + 1);
                var sr = d.trail[i];
                if (sr == null) continue;
                sr.enabled = d.sr.enabled && back < d.historyCount;
                if (sr.enabled) sr.transform.position = d.history[back];
            }
        }

        public static void Destroy(Disc d)
        {
            Kill(d.go);
            d.go = null;
            for (int i = 0; i < d.trail.Length; i++)
            {
                if (d.trail[i] != null) Kill(d.trail[i].gameObject);
                d.trail[i] = null;
            }
        }

        static void Kill(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }
    }

    public partial class Game
    {
        /// <summary>A disc bounce: two red sparks and a high ping.</summary>
        public static void DiscBounceFx(Vector2 at)
        {
            var g = active;
            if (g == null) return;
            g.fx.Sparkle(at, Palette.Spark);
            Sfx.Play(Sound.Bounce, 2.2f, 0.3f); // placeholder ping
        }

        /// <summary>A disc's end: it breaks into steel specks.</summary>
        public static void DiscBreakFx(Vector2 at)
        {
            var g = active;
            if (g == null) return;
            g.fx.Debris(at, Palette.Steel, Palette.SteelLight, 6);
            Sfx.Play(Sound.Bounce, 1.4f, 0.25f);
        }
    }

    public partial class Tuning
    {
        [Header("Weapon: Ricochet Disc")]
        [Tooltip("Discs in a full Ricochet Disc crate.")]
        public int discAmmo = 6;
        [Tooltip("Units per second of a Ricochet Disc.")]
        public float discSpeed = 22f;
        [Tooltip("Degrees off straight up (or down) a disc leaves at, leaning to the side Wick is moving or last moved.")]
        public float discAngle = 25f;
        [Tooltip("Bounces off walls, ceiling, floor, tiles, steel and the boss before a disc ends.")]
        public int discBounces = 6;
        [Tooltip("Things one disc can pop (or hit, for ceramic) before it ends.")]
        public int discPops = 3;
        [Tooltip("Most Ricochet Discs in the air at once. The button is dim and does nothing while this many fly.")]
        public int discMaxAlive = 2;
        [Tooltip("Boss health a disc takes when it bounces off the boss, once per disc.")]
        public float discBossDamage = 1f;
        [Tooltip("Seconds a disc flies at most before it ends.")]
        public float discLife = 4f;
        [Tooltip("Seconds a disc flickers after its last bounce, pop or second, before it breaks. It hits nothing meanwhile.")]
        public float discFlickerTime = 0.2f;
    }
}
