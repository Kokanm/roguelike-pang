using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>
    /// W04 Longshot: a rifle that aims by itself. A press locks a red sight line on the nearest bubble or rolling balloon
    /// Wick can see from the weapon tip (no solid tile and no steel in between; steel and faded Ghosts are never
    /// chosen). With none, a boss stage locks on the boss. With nothing in sight the press does nothing and no round is
    /// spent, and the touch button shows dim. The line tracks its target for longshotAim seconds while he walks at
    /// longshotWalkScale, then the shot goes from the tip through the target to the first wall, solid tile or steel
    /// thing, and everything on that line is hit at the same moment. Ceramic breaks in one shot. A breakable block at
    /// the end is destroyed. The boss takes longshotBossDamage. Down does nothing: it always picks its own angle.
    /// </summary>
    [Preserve]
    public sealed class Longshot : WeaponSpec
    {
        public override string Id => "longshot";
        public override string Name => "Longshot";
        public override WeaponTier Tier => WeaponTier.Rare;
        public override int MaxAmmo => Tuning.I.longshotAmmo;
        public override int Order => 4;

        static readonly string[] Icon11 =
        {
            "...DLLLD...",
            "...DDDDD...",
            "LLLLLLLLLLR",
            "SSSSSS.....",
            ".DSSDS.....",
            ".DSS.......",
            ".DDD.......",
        };

        static readonly string[] Hand =
        {
            "..R..",
            "..L..",
            "..LS.",
            "..LS.",
            "..LS.",
            "..LS.",
            "DDLS.",
            "DDLS.",
            "DDLS.",
            "..LS.",
            "..LS.",
            "..LS.",
            ".DDD.",
            ".DDD.",
        };

        public override string[] IconRows => Icon11;
        public override string[] HeldRows => Hand;
        public override WeaponBehaviour CreateBehaviour() => new Behaviour();

        /// <summary>Half the width of the sight line and the shot, in units (one pixel either side).</summary>
        static float HalfWidth => Tuning.I.Pixel;
        /// <summary>Step used when walking a line through the tiles.</summary>
        const float TileStep = 0.05f;

        // ------------------------------------------------------------------ targeting

        /// <summary>
        /// The nearest bubble or rolling balloon the tip can see, or the boss when there is none. Steel, faded Ghosts and
        /// popped things are never chosen. False when nothing is in sight.
        /// </summary>
        public static bool FindTarget(WeaponWorld world, Vector2 tip, out WeaponTarget target, List<WeaponTarget> scratch = null)
        {
            target = default;
            float best = float.PositiveInfinity;
            bool found = false;
            foreach (var b in world.Bubbles)
            {
                if (b == null || b.Popped || b.Steel || !b.CanBeHit) continue;
                float d = Vector2.Distance(tip, b.Position);
                if (d >= best) continue;
                var t = new WeaponTarget { kind = TargetKind.Bubble, bubble = b, position = b.Position, distance = d };
                if (!InSight(world, tip, t, scratch)) continue;
                best = d;
                target = t;
                found = true;
            }
            foreach (var r in world.Rollers)
            {
                if (r == null || r.Popped || r.Steel) continue;
                float d = Vector2.Distance(tip, r.Position);
                if (d >= best) continue;
                var t = new WeaponTarget { kind = TargetKind.Roller, roller = r, position = r.Position, distance = d };
                if (!InSight(world, tip, t, scratch)) continue;
                best = d;
                target = t;
                found = true;
            }
            if (found) return true;

            var boss = world.LiveBoss;
            if (boss == null) return false;
            var bt = new WeaponTarget { kind = TargetKind.Boss, boss = boss, position = boss.Body.center, distance = Vector2.Distance(tip, boss.Body.center) };
            if (!InSight(world, tip, bt, scratch)) return false;
            target = bt;
            return true;
        }

        /// <summary>Is this target still there, still hittable and still in sight? Its position is refreshed.</summary>
        public static bool StillValid(WeaponWorld world, Vector2 tip, ref WeaponTarget target, List<WeaponTarget> scratch = null)
        {
            switch (target.kind)
            {
                case TargetKind.Bubble:
                    var b = target.bubble;
                    if (b == null || b.Popped || b.Steel || !b.CanBeHit || !world.Bubbles.Contains(b)) return false;
                    target.position = b.Position;
                    break;
                case TargetKind.Roller:
                    var r = target.roller;
                    if (r == null || r.Popped || r.Steel || !world.Rollers.Contains(r)) return false;
                    target.position = r.Position;
                    break;
                case TargetKind.Boss:
                    var boss = world.LiveBoss;
                    if (boss == null || boss != target.boss) return false;
                    target.position = boss.Body.center;
                    break;
                default:
                    return false;
            }
            target.distance = Vector2.Distance(tip, target.position);
            return InSight(world, tip, target, scratch);
        }

        /// <summary>No solid tile and no steel thing between the tip and the target.</summary>
        public static bool InSight(WeaponWorld world, Vector2 tip, WeaponTarget target, List<WeaponTarget> scratch = null)
        {
            Vector2 aim = AimPoint(tip, target);
            float reach = Vector2.Distance(tip, aim) - TargetRadius(target);
            if (reach > 0f && TileBlockDistance(world.Map, tip, aim, reach) < reach) return false;
            // A steel bubble or rolling balloon in the way hides it.
            var along = world.AlongSegment(tip, aim, HalfWidth, scratch);
            float own = Vector2.Distance(tip, aim);
            foreach (var t in along)
            {
                if (t.distance >= own) break;
                if (IsSteel(t)) return false;
            }
            return true;
        }

        /// <summary>Where the sight line points: the target's centre (the boss: the middle of its body).</summary>
        static Vector2 AimPoint(Vector2 tip, WeaponTarget t) => t.kind == TargetKind.Boss && t.boss != null ? t.boss.Body.center : t.position;

        static float TargetRadius(WeaponTarget t)
        {
            switch (t.kind)
            {
                case TargetKind.Bubble: return t.bubble != null ? t.bubble.Radius : 0f;
                case TargetKind.Roller: return t.roller != null ? t.roller.Radius : 0f;
                case TargetKind.Boss:
                    if (t.boss == null) return 0f;
                    var s = t.boss.Body.size;
                    return Mathf.Min(s.x, s.y) * 0.5f;
            }
            return 0f;
        }

        static bool IsSteel(WeaponTarget t) =>
            (t.kind == TargetKind.Bubble && t.bubble != null && t.bubble.Steel) ||
            (t.kind == TargetKind.Roller && t.roller != null && t.roller.Steel);

        /// <summary>How far from a towards b the first solid tile starts (maxDistance when none is reached).</summary>
        static float TileBlockDistance(StageMap map, Vector2 a, Vector2 b, float maxDistance)
        {
            if (map == null) return maxDistance;
            Vector2 dir = b - a;
            if (dir.sqrMagnitude < 1e-8f) return maxDistance;
            dir.Normalize();
            for (float d = 0f; d < maxDistance; d += TileStep)
            {
                Vector2 p = a + dir * d;
                int row = StageMap.Row(p.y);
                if (!map.RowInStorey(row, a.y)) return maxDistance;
                if (map.IsSolid(StageMap.Col(p.x), row)) return d;
            }
            return maxDistance;
        }

        // ------------------------------------------------------------------ the shot

        public enum StopKind { Wall, Tile, Steel }

        /// <summary>What one shot hit, for the effects and the tests.</summary>
        public struct ShotResult
        {
            public Vector2 from, end;
            public StopKind stop;
            public int bubbles, rollers;
            public bool boss;
            /// <summary>A breakable block at the end of the line was destroyed.</summary>
            public bool brokeTile;
        }

        /// <summary>
        /// Fires from the tip through the given point to the first wall, ceiling, floor, solid tile or steel thing, and
        /// hits everything on that line at once. Bubbles split by this shot are not hit by it.
        /// </summary>
        public static ShotResult Fire(WeaponWorld world, Vector2 tip, Vector2 through, List<WeaponTarget> scratch = null)
        {
            var t = Tuning.I;
            var result = new ShotResult { from = tip, end = tip, stop = StopKind.Wall };
            Vector2 dir = through - tip;
            if (dir.sqrMagnitude < 1e-8f) dir = Vector2.up;
            dir.Normalize();

            // Out to the arena edge.
            float length = EdgeDistance(tip, dir);
            // Cut at the first solid tile.
            float tileAt = TileBlockDistance(world.Map, tip, tip + dir * length, length);
            int tileCol = -1, tileRow = -1;
            if (tileAt < length)
            {
                length = tileAt;
                result.stop = StopKind.Tile;
                Vector2 p = tip + dir * (tileAt + TileStep * 0.5f);
                tileCol = StageMap.Col(p.x);
                tileRow = StageMap.Row(p.y);
            }

            var along = world.AlongSegment(tip, tip + dir * length, HalfWidth, scratch).ToArray();
            // Cut at the first steel thing.
            WeaponTarget? steel = null;
            foreach (var target in along)
            {
                if (!IsSteel(target)) continue;
                steel = target;
                length = Mathf.Min(length, target.distance);
                result.stop = StopKind.Steel;
                break;
            }
            result.end = tip + dir * length;

            // Everything on the line at the same moment (a copy: hits change the lists).
            foreach (var target in along)
            {
                if (target.distance > length || IsSteel(target)) continue;
                switch (target.kind)
                {
                    case TargetKind.Bubble:
                        if (Break(world, target.bubble)) result.bubbles++;
                        break;
                    case TargetKind.Roller:
                        if (Break(world, target.roller)) result.rollers++;
                        break;
                    case TargetKind.Boss:
                        if (world.LiveBoss == null) break;
                        world.WeaponHitBoss(t.longshotBossDamage, target.position);
                        result.boss = true;
                        break;
                }
            }

            if (steel.HasValue) world.Hit(steel.Value, 0f); // sparkle and ring, nothing else
            else if (result.stop == StopKind.Tile) result.brokeTile = BreakTile(world, tileCol, tileRow);
            return result;
        }

        /// <summary>A bubble hit until it breaks: ceramic takes all its hits from the one shot.</summary>
        static bool Break(WeaponWorld world, Bubble b)
        {
            if (b == null || b.Popped || !b.CanBeHit) return false;
            for (int i = 0; i < 8 && b.Ceramic && b.Armor > 1; i++) world.WeaponHit(b);
            return world.WeaponHit(b);
        }

        static bool Break(WeaponWorld world, Roller r)
        {
            if (r == null || r.Popped) return false;
            for (int i = 0; i < 8 && r.Ceramic && r.Armor > 1; i++) world.WeaponHit(r);
            return world.WeaponHit(r);
        }

        /// <summary>A breakable block in the way is destroyed (and stops the shot). Solid tiles are left alone.</summary>
        static bool BreakTile(WeaponWorld world, int c, int r)
        {
            var map = world.Map;
            if (map == null || map.Get(c, r).kind != TileKind.Breakable) return false;
            for (int k = 0; k < 16 && map.Get(c, r).kind == TileKind.Breakable; k++) world.OnHitTile?.Invoke(c, r);
            return map.Get(c, r).kind != TileKind.Breakable;
        }

        /// <summary>Distance from p along dir to the walls, ceiling or floor.</summary>
        static float EdgeDistance(Vector2 p, Vector2 dir)
        {
            var t = Tuning.I;
            var map = Game.Map;
            float ceil = map != null ? map.StoreyCeiling(p.y) : t.arenaHeight;
            float floor = map != null ? Mathf.Max(0f, map.StoreyBase(p.y)) : 0f;
            float best = 100f;
            if (dir.x > 1e-6f) best = Mathf.Min(best, (t.HalfWidth - p.x) / dir.x);
            if (dir.x < -1e-6f) best = Mathf.Min(best, (-t.HalfWidth - p.x) / dir.x);
            if (dir.y > 1e-6f) best = Mathf.Min(best, (ceil - p.y) / dir.y);
            if (dir.y < -1e-6f) best = Mathf.Min(best, (floor - p.y) / dir.y);
            return Mathf.Max(0f, best);
        }

        // ------------------------------------------------------------------ behaviour

        public enum Phase { Ready, Aim, Cooldown }

        public sealed class Behaviour : WeaponBehaviour
        {
            public Phase Phase { get; private set; } = Phase.Ready;
            public float PhaseTime { get; private set; }
            /// <summary>What the sight line is locked on while aiming.</summary>
            public WeaponTarget Target => target;
            /// <summary>Something is in sight right now (the touch button is lit).</summary>
            public bool HasSight { get; private set; }
            /// <summary>The last shot, for the tests.</summary>
            public ShotResult LastShot { get; private set; }
            public int ShotsFired { get; private set; }

            WeaponTarget target;
            Wick wick;
            readonly List<WeaponTarget> scratch = new List<WeaponTarget>();
            LongshotView view;

            public override bool Dimmed => Phase == Phase.Ready && !HasSight;

            public override void Tick(WeaponContext ctx, float dt)
            {
                wick = ctx.Wick;
                var t = Tuning.I;
                PhaseTime += dt;
                Vector2 tip = Tip(ctx);
                bool canFire = CanFireFrom(ctx, tip);

                switch (Phase)
                {
                    case Phase.Cooldown:
                        view?.Tick(dt);
                        if (PhaseTime < t.longshotCooldown) break;
                        Enter(Phase.Ready);
                        goto case Phase.Ready;

                    case Phase.Ready:
                        view?.Tick(dt);
                        HasSight = canFire && FindTarget(ctx.World, tip, out _, scratch);
                        if (!ctx.Pressed || !canFire) break;
                        if (ctx.Run != null && ctx.Run.WeaponAmmo < 1) break;
                        if (!FindTarget(ctx.World, tip, out target, scratch)) break; // nothing in sight: nothing spent
                        Enter(Phase.Aim);
                        Slow(true);
                        if (wick != null) view ??= LongshotView.Create(wick.transform.parent);
                        Sfx.Play(Sound.Turn, 1.7f, 0.45f); // placeholder lock-on click
                        view?.Aim(tip, target, 0f);
                        break;

                    case Phase.Aim:
                        HasSight = true;
                        if (!StillValid(ctx.World, tip, ref target, scratch) && !FindTarget(ctx.World, tip, out target, scratch))
                        {
                            // Lost it and nothing else is in sight: stand down, nothing spent.
                            Slow(false);
                            view?.Hide();
                            Enter(Phase.Ready);
                            break;
                        }
                        float k = PhaseTime / Mathf.Max(0.01f, t.longshotAim);
                        view?.Aim(tip, target, k);
                        if (k < 1f || !canFire) break;
                        Shoot(ctx, tip);
                        break;
                }
            }

            void Shoot(WeaponContext ctx, Vector2 tip)
            {
                Slow(false);
                if (!ctx.Spend())
                {
                    view?.Hide();
                    Enter(Phase.Ready);
                    return;
                }
                LastShot = Fire(ctx.World, tip, AimPoint(tip, target), scratch);
                ShotsFired++;
                view?.Shot(LastShot.from, LastShot.end);
                var muzzle = ctx.Muzzle;
                ctx.Muzzle = tip;
                ctx.Flash(); // the kick, the flash and the shared fire sound
                ctx.Muzzle = muzzle;
                Sfx.Play(Sound.Fire, 0.55f, 0.9f); // placeholder: a low crack under it
                Enter(Phase.Cooldown);
            }

            /// <summary>Always the raised weapon tip: Down does nothing, the rifle picks its own angle.</summary>
            static Vector2 Tip(WeaponContext ctx) => ctx.Wick != null ? ctx.Wick.LauncherTip : ctx.Muzzle;

            static bool CanFireFrom(WeaponContext ctx, Vector2 tip)
            {
                if (ctx.Wick == null) return ctx.CanFire;
                var map = Game.Map;
                return map == null || !map.IsSolid(StageMap.Col(tip.x), StageMap.Row(tip.y));
            }

            void Enter(Phase p)
            {
                Phase = p;
                PhaseTime = 0f;
            }

            void Slow(bool on)
            {
                if (wick != null) wick.WeaponWalkScale = on ? Tuning.I.longshotWalkScale : 1f;
            }

            public override void Dispose()
            {
                Slow(false);
                view?.Destroy();
                view = null;
            }
        }
    }

    /// <summary>
    /// The Longshot's look: a dotted one pixel red sight line from the tip to the target with four corner marks closing
    /// in on it, then for two frames a solid yellow and white line out to where the shot stopped.
    /// </summary>
    sealed class LongshotView
    {
        const int ShotFrames = 2;
        const float FrameTime = 1f / 60f;
        /// <summary>How far out the corner marks start, in pixels beyond the target's edge.</summary>
        const int MarkStartPixels = 12;

        static Sprite dotted, solid, mark;

        GameObject root;
        SpriteRenderer line;
        readonly SpriteRenderer[] marks = new SpriteRenderer[4];
        float shotTime;

        public static LongshotView Create(Transform parent)
        {
            var v = new LongshotView { root = new GameObject("Longshot") };
            if (parent != null) v.root.transform.SetParent(parent, false);
            v.line = v.Renderer("Line", 2);
            v.line.drawMode = SpriteDrawMode.Tiled;
            for (int i = 0; i < 4; i++) v.marks[i] = v.Renderer("Mark" + i, 3);
            return v;
        }

        SpriteRenderer Renderer(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            Sorting.Apply(sr, Layer.Shots, order);
            Game.ApplySpriteMaterial(sr);
            sr.enabled = false;
            return sr;
        }

        /// <summary>The sight line on the target; progress 0 to 1 closes the corner marks in.</summary>
        public void Aim(Vector2 tip, WeaponTarget target, float progress)
        {
            if (root == null) return;
            shotTime = 0f;
            Line(tip, target.position, Dotted());
            var t = Tuning.I;
            float px = t.Pixel;
            float edge = target.kind == TargetKind.Boss && target.boss != null
                ? Mathf.Max(target.boss.Body.width, target.boss.Body.height) * 0.5f
                : target.kind == TargetKind.Bubble && target.bubble != null ? target.bubble.Radius
                : target.roller != null ? target.roller.Radius : 0.5f;
            float r = edge + Mathf.Lerp(MarkStartPixels, 1f, Mathf.Clamp01(progress)) * px;
            r = Mathf.Round(r / px) * px;
            for (int i = 0; i < 4; i++)
            {
                int sx = i % 2 == 0 ? -1 : 1, sy = i < 2 ? 1 : -1;
                var m = marks[i];
                m.sprite = Mark();
                m.transform.position = Snap(target.position + new Vector2(sx * r, sy * r));
                // The mark is an L in the top-left corner; flip it for the other three.
                m.flipX = sx > 0;
                m.flipY = sy < 0;
                m.enabled = true;
            }
        }

        /// <summary>The shot itself: solid for two frames out to where it stopped.</summary>
        public void Shot(Vector2 from, Vector2 end)
        {
            if (root == null) return;
            foreach (var m in marks) m.enabled = false;
            Line(from, end, Solid());
            shotTime = ShotFrames * FrameTime;
        }

        public void Tick(float dt)
        {
            if (root == null || shotTime <= 0f) return;
            shotTime -= dt;
            if (shotTime <= 0f) Hide();
        }

        void Line(Vector2 a, Vector2 b, Sprite sprite)
        {
            float px = Tuning.I.Pixel;
            Vector2 d = b - a;
            float len = Mathf.Max(px, d.magnitude);
            line.sprite = sprite;
            line.size = new Vector2(len, sprite.rect.height * px);
            line.transform.position = Snap(a);
            line.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            line.enabled = true;
        }

        public void Hide()
        {
            if (root == null) return;
            line.enabled = false;
            foreach (var m in marks) m.enabled = false;
        }

        public void Destroy()
        {
            if (root == null) return;
            if (Application.isPlaying) Object.Destroy(root);
            else Object.DestroyImmediate(root);
            root = null;
        }

        static Vector3 Snap(Vector2 p)
        {
            float px = Tuning.I.Pixel;
            return new Vector3(Mathf.Round(p.x / px) * px, Mathf.Round(p.y / px) * px, 0f);
        }

        static int Ppu => Tuning.I.pixelsPerUnit;

        /// <summary>Two red pixels, two empty, repeated along the line.</summary>
        static Sprite Dotted()
        {
            if (dotted != null) return dotted;
            var c = new PixelCanvas(4, 1);
            c.Set(0, 0, Palette.Spark);
            c.Set(1, 0, Palette.Spark);
            return dotted = Tiling(c.ToSprite("LongshotSight", new Vector2(0f, 0.5f), Ppu));
        }

        /// <summary>Three rows: yellow, a white core, yellow.</summary>
        static Sprite Solid()
        {
            if (solid != null) return solid;
            var c = new PixelCanvas(1, 3);
            var white = new Color32(255, 255, 255, 255);
            c.Set(0, 0, Palette.SparkCore);
            c.Set(0, 1, white);
            c.Set(0, 2, Palette.SparkCore);
            return solid = Tiling(c.ToSprite("LongshotShot", new Vector2(0f, 1.5f), Ppu));
        }

        /// <summary>A 5 x 5 red corner: an L with its corner at the top left, pivot on that corner.</summary>
        static Sprite Mark()
        {
            if (mark != null) return mark;
            var c = new PixelCanvas(5, 5);
            for (int i = 0; i < 5; i++)
            {
                c.Set(i, 4, Palette.Spark);
                c.Set(0, i, Palette.Spark);
            }
            return mark = c.ToSprite("LongshotMark", new Vector2(0.5f, 4.5f), Ppu);
        }

        static Sprite Tiling(Sprite s)
        {
            s.texture.wrapMode = TextureWrapMode.Repeat;
            return s;
        }
    }

    public partial class Tuning
    {
        [Header("Weapon: Longshot")]
        [Tooltip("Rounds in a full Longshot crate.")]
        public int longshotAmmo = 6;
        [Tooltip("Seconds the sight line tracks its target before the shot.")]
        public float longshotAim = 0.5f;
        [Tooltip("Seconds from one shot until the next press can aim.")]
        public float longshotCooldown = 0.9f;
        [Tooltip("Walk speed while aiming, as a share of normal (1 = full speed).")]
        [Range(0.3f, 1f)] public float longshotWalkScale = 0.6f;
        [Tooltip("Boss health one Longshot round takes.")]
        public float longshotBossDamage = 2f;
    }
}
