using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>
    /// W07 Arc Coil: a press sends a bolt to the nearest bubble, rolling balloon or boss within arcReach of the coil,
    /// then on from each thing it hits to the nearest one not yet hit within arcJumpReach, up to arcJumps more times,
    /// one jump every arcJumpTime seconds, so the pops ripple outward. Steel conducts: a steel ball or steel Pogo can be
    /// a stop, is not harmed and does not use up a jump. Ceramic takes one hit and ends the bolt. The boss takes
    /// arcBossDamage and the bolt jumps on from it. Things born from a bolt (the halves of a pop) are never its targets.
    /// The bolt ignores tiles and Down; with nothing in reach a press does nothing and spends nothing.
    /// </summary>
    [Preserve]
    public sealed class ArcCoil : WeaponSpec
    {
        public override string Id => "arc";
        public override string Name => "Arc Coil";
        public override WeaponTier Tier => WeaponTier.Rare;
        public override int MaxAmmo => Tuning.I.arcAmmo;
        public override int Order => 7;

        static readonly string[] Icon11 =
        {
            ".........Y.",
            ".L.L.L.LYWY",
            "SLSLSLSLSY.",
            "SDSDSDSD.Y.",
            ".D.D.D.D...",
            "..DS.......",
            "..DS.......",
            "..DD.......",
        };

        static readonly string[] Hand =
        {
            "..Y..",
            ".YWY.",
            "..Y..",
            ".LSD.",
            "LSSSD",
            ".LSD.",
            "LSSSD",
            ".LSD.",
            "LSSSD",
            ".DDD.",
        };

        public override string[] IconRows => Icon11;
        public override string[] HeldRows => Hand;
        public override WeaponBehaviour CreateBehaviour() => new Behaviour();

        // ------------------------------------------------------------------ targets

        /// <summary>Something the bolt can hurt: a bubble that can be hit, a rolling balloon that is not steel, a live boss.</summary>
        public static bool Hittable(WeaponWorld world, WeaponTarget t)
        {
            switch (t.kind)
            {
                case TargetKind.Bubble: return t.bubble != null && t.bubble.CanBeHit;
                case TargetKind.Roller: return t.roller != null && !t.roller.Popped && !t.roller.Steel;
                case TargetKind.Boss: return world.LiveBoss != null;
            }
            return false;
        }

        /// <summary>Steel: a free stop that passes the bolt on.</summary>
        public static bool Conducts(WeaponTarget t) =>
            (t.kind == TargetKind.Bubble && t.bubble != null && !t.bubble.Popped && t.bubble.Steel) ||
            (t.kind == TargetKind.Roller && t.roller != null && !t.roller.Popped && t.roller.Steel);

        static bool IsCeramic(WeaponTarget t) =>
            (t.kind == TargetKind.Bubble && t.bubble != null && t.bubble.Ceramic) ||
            (t.kind == TargetKind.Roller && t.roller != null && t.roller.Ceramic);

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

        /// <summary>
        /// The first stop: the nearest thing the bolt can hurt within arcReach of the coil (tiles ignored). Steel is never
        /// the first stop, so a press with only steel near does nothing.
        /// </summary>
        public static bool FindFirst(WeaponWorld world, Vector2 from, out WeaponTarget target, List<WeaponTarget> scratch = null)
        {
            var found = world.InCircle(from, Tuning.I.arcReach, scratch);
            foreach (var t in found)
            {
                if (!Hittable(world, t)) continue;
                target = t;
                return true;
            }
            target = default;
            return false;
        }

        /// <summary>What one bolt did, for the effects and the tests.</summary>
        public struct BoltResult
        {
            /// <summary>Things it hurt (bubbles, rolling balloons and the boss).</summary>
            public int hits;
            /// <summary>Steel things it passed through.</summary>
            public int steel;
            public bool boss;
            /// <summary>A ceramic thing ended it.</summary>
            public bool endedOnCeramic;
        }

        /// <summary>
        /// One bolt on its way. Begin hits the first stop at once; each Step jumps to the next. Only things that were in
        /// the world when it began can be stops, so the halves of a pop it caused are never targets.
        /// </summary>
        public sealed class Bolt
        {
            readonly WeaponWorld world;
            readonly HashSet<object> visited = new HashSet<object>();
            readonly HashSet<object> eligible = new HashSet<object>();
            readonly List<WeaponTarget> scratch = new List<WeaponTarget>();
            readonly List<Vector2> points = new List<Vector2>();
            BoltResult result;
            Vector2 at;
            int jumpsLeft;

            /// <summary>Where the bolt went: the coil, then every stop in order.</summary>
            public IReadOnlyList<Vector2> Points => points;
            public BoltResult Result => result;
            /// <summary>No more jumps: out of jumps, nothing in reach, or ceramic ended it.</summary>
            public bool Done { get; private set; }
            /// <summary>Seconds until the next jump.</summary>
            public float NextJumpIn { get; private set; }

            public Bolt(WeaponWorld world, Vector2 from)
            {
                this.world = world;
                at = from;
                points.Add(from);
                foreach (var b in world.Bubbles) if (b != null && !b.Popped) eligible.Add(b);
                foreach (var r in world.Rollers) if (r != null && !r.Popped) eligible.Add(r);
                jumpsLeft = Mathf.Max(0, Tuning.I.arcJumps);
            }

            /// <summary>Hits the first stop (from FindFirst).</summary>
            public void Begin(WeaponTarget first)
            {
                Apply(first, false);
                NextJumpIn = Tuning.I.arcJumpTime;
            }

            /// <summary>Waits out the time between jumps and jumps as often as it allows.</summary>
            public void Tick(float dt)
            {
                if (Done) return;
                NextJumpIn -= dt;
                int guard = 64;
                while (!Done && NextJumpIn <= 0f && guard-- > 0)
                {
                    Step();
                    NextJumpIn += Mathf.Max(0.001f, Tuning.I.arcJumpTime);
                }
            }

            /// <summary>
            /// One jump: on to the nearest stop not yet visited within arcJumpReach. Steel stops are free, so it carries
            /// on through them in the same jump until it reaches something it can hurt. Returns false when it ended.
            /// </summary>
            public bool Step()
            {
                if (Done) return false;
                if (jumpsLeft <= 0) { Done = true; return false; }
                int guard = 256;
                while (guard-- > 0)
                {
                    if (!Next(out var t)) { Done = true; return false; }
                    bool free = Conducts(t);
                    Apply(t, true);
                    if (!free || Done) return !Done;
                }
                Done = true;
                return false;
            }

            bool Next(out WeaponTarget target)
            {
                var found = world.InCircle(at, Tuning.I.arcJumpReach, scratch);
                foreach (var t in found)
                {
                    var key = Key(t);
                    if (key == null || visited.Contains(key)) continue;
                    if (t.kind != TargetKind.Boss && !eligible.Contains(key)) continue;
                    if (!Hittable(world, t) && !Conducts(t)) continue;
                    target = t;
                    return true;
                }
                target = default;
                return false;
            }

            void Apply(WeaponTarget t, bool isJump)
            {
                visited.Add(Key(t));
                at = t.position;
                points.Add(at);
                if (Conducts(t))
                {
                    // Not harmed: a spark where it passes, and it costs no jump.
                    world.Hit(t, 0f);
                    result.steel++;
                    Sfx.Play(Sound.Bounce, 2.4f, 0.3f);
                    return;
                }
                if (isJump) jumpsLeft--;
                bool ceramic = IsCeramic(t);
                if (world.Hit(t, Tuning.I.arcBossDamage))
                {
                    result.hits++;
                    if (t.kind == TargetKind.Boss) result.boss = true;
                }
                Sfx.Play(Sound.Turn, 2.2f, 0.35f); // placeholder crackle per jump
                if (ceramic)
                {
                    result.endedOnCeramic = true;
                    Done = true;
                }
                if (jumpsLeft <= 0) Done = true;
            }
        }

        /// <summary>
        /// Fires a whole bolt at once, every jump in the same frame (tests and tools). Null when nothing is in reach.
        /// </summary>
        public static Bolt FireNow(WeaponWorld world, Vector2 from)
        {
            if (!FindFirst(world, from, out var first)) return null;
            var bolt = new Bolt(world, from);
            bolt.Begin(first);
            while (bolt.Step()) { }
            return bolt;
        }

        // ------------------------------------------------------------------ behaviour

        public sealed class Behaviour : WeaponBehaviour
        {
            /// <summary>Seconds since the last bolt. Starts high so the first press fires at once.</summary>
            public float SinceBolt { get; private set; } = 999f;
            /// <summary>The bolt still jumping, or the last one.</summary>
            public Bolt Current { get; private set; }

            readonly List<WeaponTarget> scratch = new List<WeaponTarget>();
            ArcView view;

            public override void Tick(WeaponContext ctx, float dt)
            {
                SinceBolt += dt;
                if (Current != null && !Current.Done)
                {
                    int before = Current.Points.Count;
                    Current.Tick(dt);
                    for (int i = before; i < Current.Points.Count; i++) view?.Link(Current.Points[i - 1], Current.Points[i]);
                }
                view?.Tick(dt);

                if (!ctx.Pressed || !ctx.CanFire) return;
                if (SinceBolt < Tuning.I.arcCooldown) return;
                if (Current != null && !Current.Done) return;

                // Down does nothing: the bolt always leaves the coil in his raised hand.
                Vector2 from = ctx.Wick != null ? ctx.Wick.LauncherTip : ctx.Muzzle;
                if (!FindFirst(ctx.World, from, out var first, scratch)) return; // nothing in reach: nothing spent
                if (!ctx.Spend()) return;

                SinceBolt = 0f;
                Current = new Bolt(ctx.World, from);
                Current.Begin(first);
                if (ctx.Wick != null) view ??= ArcView.Create(ctx.Wick.transform.parent);
                view?.Link(Current.Points[0], Current.Points[1]);
                var muzzle = ctx.Muzzle;
                ctx.Muzzle = from;
                ctx.Flash();
                ctx.Muzzle = muzzle;
            }

            public override void Dispose()
            {
                Current = null;
                view?.Destroy();
                view = null;
            }
        }
    }

    /// <summary>
    /// The Arc Coil's look: each jump of the bolt is a jagged line, a white core with yellow edges, drawn as a few
    /// stretched pixel strips and given new kinks every frame for arcBoltTime seconds.
    /// </summary>
    sealed class ArcView
    {
        const int Kinks = 6;

        struct LinkLife
        {
            public Vector2 a, b;
            public float age;
        }

        static Sprite strip;

        GameObject root;
        readonly List<LinkLife> links = new List<LinkLife>();
        readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();
        readonly Vector2[] kinks = new Vector2[Kinks + 1];

        public static ArcView Create(Transform parent)
        {
            var v = new ArcView { root = new GameObject("ArcCoil") };
            if (parent != null) v.root.transform.SetParent(parent, false);
            return v;
        }

        public void Link(Vector2 a, Vector2 b)
        {
            if (root == null) return;
            links.Add(new LinkLife { a = a, b = b });
        }

        public void Tick(float dt)
        {
            if (root == null) return;
            float life = Mathf.Max(0.01f, Tuning.I.arcBoltTime);
            int used = 0;
            for (int i = links.Count - 1; i >= 0; i--)
            {
                var l = links[i];
                l.age += dt;
                if (l.age > life) { links.RemoveAt(i); continue; }
                links[i] = l;
                used = Draw(l.a, l.b, used);
            }
            for (int i = used; i < pool.Count; i++) pool[i].enabled = false;
        }

        /// <summary>A new jagged path from a to b, as strips taken from the pool from index used. Returns the next free index.</summary>
        int Draw(Vector2 a, Vector2 b, int used)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return used;
            Vector2 n = new Vector2(-d.y, d.x) / len;
            float px = Tuning.I.Pixel;
            int count = Mathf.Clamp(Mathf.RoundToInt(len / (7f * px)), 2, Kinks);
            for (int i = 0; i <= count; i++)
            {
                float t = (float)i / count;
                float jitter = i == 0 || i == count ? 0f : (Random.value - 0.5f) * 7f * px;
                kinks[i] = a + d * t + n * jitter;
            }
            for (int i = 0; i < count; i++)
            {
                var sr = Strip(used++);
                Vector2 p = kinks[i], q = kinks[i + 1];
                Vector2 s = q - p;
                sr.transform.position = new Vector3(Mathf.Round(p.x / px) * px, Mathf.Round(p.y / px) * px, 0f);
                sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(s.y, s.x) * Mathf.Rad2Deg);
                sr.transform.localScale = new Vector3(Mathf.Max(px, s.magnitude + px) / px, 1f, 1f);
                sr.enabled = true;
            }
            return used;
        }

        SpriteRenderer Strip(int i)
        {
            while (pool.Count <= i)
            {
                var go = new GameObject("Bolt");
                go.transform.SetParent(root.transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = StripSprite();
                Sorting.Apply(sr, Layer.Shots, 3);
                Game.ApplySpriteMaterial(sr);
                pool.Add(sr);
            }
            return pool[i];
        }

        /// <summary>One pixel long, three high: yellow edge, white core, yellow edge. Pivot on the left middle.</summary>
        static Sprite StripSprite()
        {
            if (strip != null) return strip;
            var c = new PixelCanvas(1, 3);
            c.Set(0, 0, Palette.SparkCore);
            c.Set(0, 1, new Color32(255, 255, 255, 255));
            c.Set(0, 2, Palette.SparkCore);
            strip = c.ToSprite("ArcBolt", new Vector2(0f, 1.5f), Tuning.I.pixelsPerUnit);
            return strip;
        }

        public void Destroy()
        {
            if (root == null) return;
            if (Application.isPlaying) Object.Destroy(root);
            else Object.DestroyImmediate(root);
            root = null;
            links.Clear();
            pool.Clear();
        }
    }

    public partial class Tuning
    {
        [Header("Weapon: Arc Coil")]
        [Tooltip("Bolts in a full Arc Coil crate.")]
        public int arcAmmo = 5;
        [Tooltip("Units from the coil the first jump can reach. Nothing in reach: the press does nothing and spends nothing.")]
        public float arcReach = 9f;
        [Tooltip("Units each later jump can reach from the thing just hit.")]
        public float arcJumpReach = 5f;
        [Tooltip("Jumps after the first hit. Steel things it passes through cost no jump.")]
        public int arcJumps = 4;
        [Tooltip("Seconds between two bolts.")]
        public float arcCooldown = 0.8f;
        [Tooltip("Boss health one bolt takes when it reaches the boss.")]
        public float arcBossDamage = 2f;
        [Tooltip("Seconds between one jump of the bolt and the next, so the pops ripple outward.")]
        public float arcJumpTime = 0.06f;
        [Tooltip("Seconds each jump of the bolt stays on screen, flickering.")]
        public float arcBoltTime = 0.2f;
    }
}
