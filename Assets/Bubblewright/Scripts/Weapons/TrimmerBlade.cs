using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>
    /// W05 Trimmer Blade: a melee weapon. Each swing is one round sweep from his left side, over his head and down his
    /// right side (bladeArc degrees, centred on straight up), bladeReach units from his raised hand, so it reaches the
    /// floor on both sides. The next swing comes back the other way. Falling with Down held, the arc turns over and
    /// covers both sides and below him.
    /// The blade sweeps across that arc over bladeSwingTime, and a thing is hit when the blade reaches it — the same
    /// moment the picture shows the blade there. A balloon that drifts into the blade is hit; one that steps into the
    /// trail behind it is not, and children born from a pop during the swing are not hit by it. Solid tiles cut the
    /// swing off, so a wall or block beside him shields that side. Boss bullets the blade meets are cut out of the air,
    /// and a steel rolling ball is turned round so it rolls away from him. All hits go through the shared WeaponWorld rules.
    /// </summary>
    [Preserve]
    public sealed class TrimmerBlade : WeaponSpec
    {
        public override string Id => "blade";
        public override string Name => "Trimmer Blade";
        public override WeaponTier Tier => WeaponTier.Common;
        public override int MaxAmmo => Tuning.I.bladeAmmo;
        public override int Order => 5;

        static readonly string[] Icon11 =
        {
            ".........LC",
            "........LCS",
            ".......LCS.",
            "......LCS..",
            ".....LCS...",
            "....LCS....",
            ".D.LCS.....",
            "..DCS......",
            ".DDD.......",
            "DD..D......",
        };

        static readonly string[] Hand =
        {
            "..L..",
            ".LCS.",
            ".LCS.",
            ".LCS.",
            ".LCS.",
            ".LCS.",
            ".LCS.",
            ".LCS.",
            ".LCS.",
            ".LCS.",
            "DDDDD",
            "..D..",
        };

        public override string[] IconRows => Icon11;
        public override string[] HeldRows => Hand;
        public override WeaponBehaviour CreateBehaviour() => new Behaviour();

        /// <summary>What one swing did, for the effects and the tests.</summary>
        public struct SwingResult
        {
            /// <summary>Bubbles and rolling balloons that took a hit (ceramic ones that only cracked count too).</summary>
            public int hits;
            /// <summary>Steel things the blade clanged off.</summary>
            public int steel;
            /// <summary>Boss bullets cut out of the air.</summary>
            public int bullets;
            /// <summary>Breakable tiles that took a hit.</summary>
            public int tiles;
            /// <summary>The boss was inside the swing and took damage.</summary>
            public bool boss;
        }

        /// <summary>The centre of the swing: his raised hand, a few pixels under the launcher tip.</summary>
        public static Vector2 HandPoint(Vector2 launcherTip) => launcherTip - new Vector2(0f, Tuning.I.bladeHandDrop * Tuning.I.Pixel);

        /// <summary>How many degrees of sweep one drawn frame covers. Hits use these same frames, so the pop lines up with the blade.</summary>
        const float SweepFrameDegrees = 8f;
        /// <summary>The steel blade is a few pixels wide. This keeps a balloon the line visibly crosses from slipping between samples.</summary>
        const float BladeHalfWidthDegrees = 4f;

        /// <summary>Drawn frames in one sweep, from the first side to the last.</summary>
        public static int SweepFrameCount => Mathf.Clamp(Mathf.CeilToInt(Tuning.I.bladeArc / SweepFrameDegrees), 3, 48);

        /// <summary>Which drawn frame a sweep progress (0 at the start, 1 at the end) is showing.</summary>
        public static int FrameAt(float progress)
        {
            int n = SweepFrameCount;
            if (n <= 1) return 0;
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(progress) * (n - 1)), 0, n - 1);
        }

        /// <summary>Sweep progress of a drawn frame: 0 is the first side, 1 is the last.</summary>
        public static float FrameProgress(int frame)
        {
            int n = SweepFrameCount;
            if (n <= 1) return 1f;
            return Mathf.Clamp(frame, 0, n - 1) / (float)(n - 1);
        }

        /// <summary>Angle of the unflipped blade, in degrees. 0 is to his right, 90 is straight up.</summary>
        public static float SpriteDegrees(float progress)
        {
            float arc = Tuning.I.bladeArc;
            return 90f + arc * 0.5f - arc * Mathf.Clamp01(progress);
        }

        /// <summary>
        /// Angle of the blade in the world. direction -1 mirrors the sweep (right to left); down turns it over,
        /// the same two flips the picture uses.
        /// </summary>
        public static float HeadDegrees(float progress, int direction, bool down)
        {
            float a = SpriteDegrees(progress);
            if (direction < 0) a = 180f - a;
            if (down) a = -a;
            return a;
        }

        /// <summary>
        /// The whole swing, resolved at once: everything the blade reaches between the start of the arc and the end.
        /// Play spreads the same sweep over bladeSwingTime; tests that only care what a finished swing hits use this.
        /// </summary>
        public static SwingResult Swing(WeaponWorld world, Vector2 hand, bool down, List<WeaponTarget> scratch = null)
        {
            var result = new SwingResult();
            ResolveSwing(world, hand, hand, down, 1, 0f, 1f, scratch, null, new HashSet<EntityId>(), new HashSet<Vector2Int>(), ref result);
            return result;
        }

        /// <summary>
        /// Hits whatever the blade sweeps between two progresses. The hand may move; the blade is in his hand, so the
        /// hit follows it. eligible, when set, is the things that existed when the swing started (a child born from a
        /// pop is not among them). Each thing and each tile is hit once.
        /// </summary>
        static void ResolveSwing(WeaponWorld world, Vector2 fromHand, Vector2 toHand, bool down, int direction,
            float fromProgress, float toProgress, List<WeaponTarget> scratch, HashSet<EntityId> eligible, HashSet<EntityId> struck,
            HashSet<Vector2Int> cutTiles, ref SwingResult result)
        {
            if (world == null) return;
            int f0 = FrameAt(fromProgress);
            int f1 = FrameAt(toProgress);
            int steps = Mathf.Max(1, f1 - f0);
            for (int i = 0; i < steps; i++)
            {
                // The last sample of a step sits on the hand where the picture is, so the pop matches the blade.
                Vector2 hand = Vector2.Lerp(fromHand, toHand, (i + 1f) / steps);
                int frame1 = Mathf.Min(f1, f0 + i + 1);
                float a0 = HeadDegrees(FrameProgress(f0 + i), direction, down);
                float a1 = HeadDegrees(FrameProgress(frame1), direction, down);
                HitSlice(world, hand, a0, a1, scratch, eligible, struck, cutTiles, ref result);
            }
        }

        /// <summary>One slice of the sweep, from angle a0 to a1, bladeReach long. A zero-width slice is the blade itself.</summary>
        static void HitSlice(WeaponWorld world, Vector2 hand, float a0, float a1, List<WeaponTarget> scratch,
            HashSet<EntityId> eligible, HashSet<EntityId> struck, HashSet<Vector2Int> cutTiles, ref SwingResult result)
        {
            float delta = Mathf.DeltaAngle(a0, a1);
            float mid = a0 + delta * 0.5f;
            float rad = mid * Mathf.Deg2Rad;
            var facing = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            float arc = Mathf.Abs(delta) + BladeHalfWidthDegrees * 2f;
            var t = Tuning.I;
            // Hit from a copy: hitting can change the lists the query read from.
            var targets = world.InSector(hand, t.bladeReach, facing, arc, scratch).ToArray();
            var map = world.Map;
            foreach (var target in targets)
            {
                EntityId id = TargetId(target);
                if (id == EntityId.None || (struck != null && struck.Contains(id))) continue;
                if (eligible != null && !eligible.Contains(id)) continue;
                if (!Reaches(map, hand, NearPoint(target, hand), -1, -1, cutTiles)) continue;
                struck?.Add(id);
                switch (target.kind)
                {
                    case TargetKind.Bubble:
                        if (target.bubble.Steel) { world.WeaponHit(target.bubble); result.steel++; }
                        else if (world.WeaponHit(target.bubble)) result.hits++;
                        break;
                    case TargetKind.Roller:
                        if (target.roller.Steel)
                        {
                            world.WeaponHit(target.roller);
                            // Knocked back: it rolls away from him.
                            target.roller.Direction = target.roller.Position.x >= hand.x ? 1 : -1;
                            result.steel++;
                        }
                        else if (world.WeaponHit(target.roller)) result.hits++;
                        break;
                    case TargetKind.Bullet:
                        if (world.Hit(target, 0f))
                        {
                            world.OnSteel?.Invoke(target.position); // a spark where it was cut
                            result.bullets++;
                        }
                        break;
                    case TargetKind.Boss:
                        if (world.Hit(target, t.bladeBossDamage)) result.boss = true;
                        break;
                }
            }
            HitTiles(world, map, hand, t.bladeReach, facing, arc, cutTiles, ref result);
        }

        static EntityId TargetId(WeaponTarget target)
        {
            switch (target.kind)
            {
                case TargetKind.Bubble: return target.bubble != null ? target.bubble.GetEntityId() : EntityId.None;
                case TargetKind.Roller: return target.roller != null ? target.roller.GetEntityId() : EntityId.None;
                case TargetKind.Bullet: return target.bullet != null ? target.bullet.GetEntityId() : EntityId.None;
                case TargetKind.Boss: return target.boss != null ? target.boss.GetEntityId() : EntityId.None;
                default: return EntityId.None;
            }
        }

        /// <summary>The point of a target nearest the hand: where the blade meets it.</summary>
        static Vector2 NearPoint(WeaponTarget target, Vector2 hand)
        {
            float radius = target.kind switch
            {
                TargetKind.Bubble => target.bubble.Radius,
                TargetKind.Roller => target.roller.Radius,
                TargetKind.Bullet => target.bullet.Radius,
                _ => 0f, // the boss position is already the closest point of its body
            };
            Vector2 to = target.position - hand;
            float d = to.magnitude;
            if (d <= radius || d < 1e-5f) return hand;
            return hand + to * ((d - radius) / d);
        }

        /// <summary>
        /// True when no solid tile lies between the hand and a point, other than the cell (skipCol, skipRow) itself.
        /// A hand that is inside a tile reaches nothing. Tiles this swing has already cut still shield what is behind
        /// them, even after the hit destroys them.
        /// </summary>
        public static bool Reaches(StageMap map, Vector2 hand, Vector2 point, int skipCol, int skipRow, HashSet<Vector2Int> stillThere = null)
        {
            if (map == null) return true;
            Vector2 d = point - hand;
            float len = d.magnitude;
            const float step = 0.05f;
            int n = Mathf.CeilToInt(len / step);
            for (int i = 0; i <= n; i++)
            {
                // Stop just short of the point, so a thing resting on a tile is not hidden by that tile.
                float along = Mathf.Min(i * step, Mathf.Max(0f, len - 0.04f));
                Vector2 p = len > 1e-5f ? hand + d * (along / len) : hand;
                int c = StageMap.Col(p.x), r = StageMap.Row(p.y);
                if (c == skipCol && r == skipRow) continue;
                if (stillThere != null && stillThere.Contains(new Vector2Int(c, r))) return false;
                if (map.IsSolid(c, r)) return false;
            }
            return true;
        }

        /// <summary>Every breakable block the blade reaches in this slice, with nothing solid in front of it, takes one hit.</summary>
        static void HitTiles(WeaponWorld world, StageMap map, Vector2 hand, float reach, Vector2 facing, float arc,
            HashSet<Vector2Int> cutTiles, ref SwingResult result)
        {
            if (map == null) return;
            float half = arc * 0.5f;
            int c0 = StageMap.Col(hand.x - reach), c1 = StageMap.Col(hand.x + reach);
            int r0 = StageMap.Row(hand.y - reach), r1 = StageMap.Row(hand.y + reach);
            var hits = new List<Vector2Int>();
            for (int r = r0; r <= r1; r++)
            for (int c = c0; c <= c1; c++)
            {
                if (c < 0 || c >= StageMap.Cols || r < 0 || r >= map.Rows) continue;
                var cellId = new Vector2Int(c, r);
                if (cutTiles != null && cutTiles.Contains(cellId)) continue;
                if (map.Get(c, r).kind != TileKind.Breakable) continue;
                var cell = StageMap.CellRect(c, r);
                Vector2 q = new Vector2(Mathf.Clamp(hand.x, cell.xMin, cell.xMax), Mathf.Clamp(hand.y, cell.yMin, cell.yMax));
                if (Vector2.Distance(q, hand) > reach) continue;
                bool inArc = cell.Contains(hand) || Vector2.Angle(facing, q - hand) <= half;
                if (!inArc)
                {
                    // A corner within reach and inside the arc also counts, like WeaponWorld.InSector.
                    foreach (var corner in new[] { new Vector2(cell.xMin, cell.yMin), new Vector2(cell.xMax, cell.yMin), new Vector2(cell.xMin, cell.yMax), new Vector2(cell.xMax, cell.yMax) })
                        if (Vector2.Distance(corner, hand) <= reach && Vector2.Angle(facing, corner - hand) <= half) { inArc = true; q = corner; break; }
                }
                if (!inArc || !Reaches(map, hand, q, c, r, cutTiles)) continue;
                hits.Add(cellId);
            }
            // Hit after the search, so a block that breaks does not open a line to the one behind it in the same slice.
            // It keeps shielding for the rest of the swing too.
            foreach (var h in hits)
            {
                cutTiles?.Add(h);
                world.OnHitTile?.Invoke(h.x, h.y);
                result.tiles++;
            }
        }

        public sealed class Behaviour : WeaponBehaviour
        {
            /// <summary>Seconds since the last swing. Starts high so the first press swings at once.</summary>
            float sinceSwing = 999f;
            /// <summary>A press that came during the wait swings as soon as it ends, so a tap is never eaten.</summary>
            bool queued;
            /// <summary>Which way the next swing is drawn: +1 left to right, -1 right to left.</summary>
            int direction = 1;
            /// <summary>What the swing in progress (or the last one) has hit so far.</summary>
            public SwingResult Last => last;
            SwingResult last;
            /// <summary>Swings so far.</summary>
            public int Swings { get; private set; }

            /// <summary>A swing is sweeping. Hits land as the blade arrives, and another swing waits until this one is done.</summary>
            bool swinging;
            float swingTime;
            float sweptProgress;
            Vector2 swingHand;
            int swingDirection = 1;
            bool swingDown;
            /// <summary>Things that existed when the swing started. A child born from a pop is not one of them.</summary>
            readonly HashSet<EntityId> eligible = new HashSet<EntityId>();
            readonly HashSet<EntityId> struck = new HashSet<EntityId>();
            readonly HashSet<Vector2Int> cutTiles = new HashSet<Vector2Int>();

            readonly List<WeaponTarget> scratch = new List<WeaponTarget>();
            BladeArcView view;

            public override void Tick(WeaponContext ctx, float dt)
            {
                sinceSwing += dt;
                if (ctx.Pressed) queued = true;
                Vector2 hand = ctx.Wick != null ? HandPoint(ctx.Wick.LauncherTip) : ctx.Muzzle;

                if (swinging)
                {
                    swingTime += dt;
                    float dur = Mathf.Max(0.01f, Tuning.I.bladeSwingTime);
                    float progress = Mathf.Clamp01(swingTime / dur);
                    view?.Place(hand, swingTime);
                    ResolveSwing(ctx.World, swingHand, hand, swingDown, swingDirection, sweptProgress, progress,
                        scratch, eligible, struck, cutTiles, ref last);
                    swingHand = hand;
                    sweptProgress = progress;
                    if (progress >= 1f) swinging = false;
                    return;
                }

                view?.Tick(dt, ctx.Wick != null ? hand : (Vector2?)null);
                if (!ctx.CanFire) return;
                if (!queued && !ctx.Held) return;
                if (sinceSwing < Tuning.I.bladeCooldown) return;

                queued = false;
                if (!ctx.Spend()) return;
                sinceSwing = 0f;
                Swings++;

                swinging = true;
                swingTime = 0f;
                sweptProgress = 0f;
                swingHand = hand;
                swingDown = ctx.ShootDown;
                swingDirection = direction;
                last = default;
                struck.Clear();
                cutTiles.Clear();
                RememberTargets(ctx.World);

                if (ctx.Wick != null)
                {
                    ctx.Wick.OnFire();
                    view ??= BladeArcView.Create(ctx.Wick.transform.parent);
                    view.Play(hand, direction, ctx.ShootDown);
                }
                // The blade is already on the first side. Anything it is touching there is hit now, with the picture.
                ResolveSwing(ctx.World, hand, hand, swingDown, swingDirection, 0f, 0f, scratch, eligible, struck, cutTiles, ref last);
                Sfx.Play(Sound.Turn, 1.7f, 0.8f); // placeholder swish
                direction = -direction;
            }

            /// <summary>Bubbles, rollers, bullets and the boss that are already in the world. Later arrivals wait for the next swing.</summary>
            void RememberTargets(WeaponWorld world)
            {
                eligible.Clear();
                if (world == null) return;
                for (int i = 0; i < world.Bubbles.Count; i++)
                    if (world.Bubbles[i] != null) eligible.Add(world.Bubbles[i].GetEntityId());
                for (int i = 0; i < world.Rollers.Count; i++)
                    if (world.Rollers[i] != null) eligible.Add(world.Rollers[i].GetEntityId());
                for (int i = 0; i < world.Bullets.Count; i++)
                    if (world.Bullets[i] != null) eligible.Add(world.Bullets[i].GetEntityId());
                var boss = world.LiveBoss;
                if (boss != null) eligible.Add(boss.GetEntityId());
            }

            public override void Dispose()
            {
                if (view != null) view.Destroy();
                view = null;
            }
        }
    }

    /// <summary>The swing's trail: a cream arc that sweeps over his head, then fades through lilac.</summary>
    sealed class BladeArcView
    {
        const int FadeFrames = 3;
        const float FadeFrameTime = 0.05f;

        static Sprite[] frames;
        static float framesReach = -1f, framesArc = -1f;

        GameObject go;
        SpriteRenderer sr;
        float time = -1f;

        public static BladeArcView Create(Transform parent)
        {
            var v = new BladeArcView { go = new GameObject("BladeArc") };
            if (parent != null) v.go.transform.SetParent(parent, false);
            v.sr = v.go.AddComponent<SpriteRenderer>();
            Sorting.Apply(v.sr, Layer.Shots, 2);
            Game.ApplySpriteMaterial(v.sr);
            v.sr.enabled = false;
            return v;
        }

        public void Play(Vector2 hand, int direction, bool down)
        {
            time = 0f;
            sr.flipX = direction < 0;
            sr.flipY = down;
            go.transform.position = hand;
            Show();
        }

        /// <summary>Puts the picture on the same sweep the hits are using. swingTime is seconds since the swing started.</summary>
        public void Place(Vector2 hand, float swingTime)
        {
            if (go == null) return;
            go.transform.position = hand;
            float sweep = Mathf.Max(0.01f, Tuning.I.bladeSwingTime);
            // Stay on the last sweep frame until the next tick, which fades it out.
            time = swingTime >= sweep ? sweep - 0.0001f : Mathf.Max(0f, swingTime);
            Show();
        }

        public void Tick(float dt, Vector2? hand)
        {
            if (time < 0f || go == null) return;
            time += dt;
            if (hand.HasValue) go.transform.position = hand.Value;
            Show();
        }

        void Show()
        {
            var f = Frames();
            int sweepCount = TrimmerBlade.SweepFrameCount;
            float sweep = Mathf.Max(0.01f, Tuning.I.bladeSwingTime);
            int i = time < sweep
                ? TrimmerBlade.FrameAt(time / sweep)
                : sweepCount + Mathf.FloorToInt((time - sweep) / FadeFrameTime);
            if (i < 0 || i >= f.Length)
            {
                sr.enabled = false;
                time = -1f;
                return;
            }
            sr.sprite = f[i];
            sr.enabled = true;
        }

        public void Destroy()
        {
            if (go == null) return;
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
            go = null;
        }

        /// <summary>Sweep frames (left to right; flipped for the other way) then fade frames, painted once.</summary>
        static Sprite[] Frames()
        {
            var t = Tuning.I;
            int sweepCount = TrimmerBlade.SweepFrameCount;
            if (frames != null && framesReach == t.bladeReach && framesArc == t.bladeArc && frames.Length == sweepCount + FadeFrames)
                return frames;
            framesReach = t.bladeReach;
            framesArc = t.bladeArc;
            int radius = Mathf.Max(4, Mathf.RoundToInt(t.bladeReach * t.pixelsPerUnit));
            int size = radius * 2 + 5;
            float start = TrimmerBlade.SpriteDegrees(0f), span = t.bladeArc; // from low on his left, over the top, to low on his right
            var list = new List<Sprite>();
            for (int k = 0; k < sweepCount; k++)
            {
                var c = new PixelCanvas(size, size);
                float progress = TrimmerBlade.FrameProgress(k);
                float head = TrimmerBlade.SpriteDegrees(progress);
                for (float a = start; a >= head - 0.01f; a -= 0.5f)
                {
                    float behind = span * progress < 0.01f ? 0f : (a - head) / (span * progress); // 0 at the head, 1 at the tail
                    Color32 col = behind < 0.3f ? Palette.Cream : behind < 0.65f ? Palette.Lilac : Palette.Dusk;
                    int thick = behind < 0.3f ? 3 : behind < 0.65f ? 2 : 1;
                    ArcPixels(c, radius, a, thick, col);
                }
                Blade(c, radius, head);
                list.Add(c.ToSprite("BladeArcSweep" + k, new Vector2(size * 0.5f, size * 0.5f), Mathf.RoundToInt(t.pixelsPerUnit)));
            }
            Color32[] fade = { Palette.Cream, Palette.Lilac, Palette.Dusk };
            for (int k = 0; k < FadeFrames; k++)
            {
                var c = new PixelCanvas(size, size);
                for (float a = start; a >= start - span; a -= 0.5f) ArcPixels(c, radius, a, FadeFrames - k, fade[k]);
                list.Add(c.ToSprite("BladeArcFade" + k, new Vector2(size * 0.5f, size * 0.5f), Mathf.RoundToInt(t.pixelsPerUnit)));
            }
            return frames = list.ToArray();
        }

        static void ArcPixels(PixelCanvas c, int radius, float degrees, int thick, Color32 col)
        {
            float rad = degrees * Mathf.Deg2Rad, mid = c.Width * 0.5f;
            for (int k = 0; k < thick; k++)
                c.Set(Mathf.FloorToInt(mid + Mathf.Cos(rad) * (radius - k)), Mathf.FloorToInt(mid + Mathf.Sin(rad) * (radius - k)), col);
        }

        /// <summary>The steel blade from near the hand to the arc, at the head of the sweep.</summary>
        static void Blade(PixelCanvas c, int radius, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad, mid = c.Width * 0.5f;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            for (int s = 5; s <= radius - 2; s++)
            {
                c.Set(Mathf.FloorToInt(mid + dir.x * s), Mathf.FloorToInt(mid + dir.y * s), Palette.SteelLight);
                c.Set(Mathf.FloorToInt(mid + dir.x * s + 1), Mathf.FloorToInt(mid + dir.y * s), Palette.Steel);
            }
        }
    }

    public partial class Tuning
    {
        [Header("Weapon: Trimmer Blade")]
        [Tooltip("Swings in a full Trimmer Blade crate.")]
        public int bladeAmmo = 30;
        [Tooltip("Units from his raised hand that a swing reaches. 2.2 reaches the floor on both sides of him.")]
        public float bladeReach = 2.2f;
        [Tooltip("Degrees the swing covers, centred on straight up (straight down when falling with Down held).")]
        public float bladeArc = 270f;
        [Tooltip("Seconds the blade takes to sweep from one side to the other. A thing is hit when the blade reaches it.")]
        public float bladeSwingTime = 0.24f;
        [Tooltip("Seconds between swings, tapped or held. Longer than the sweep, so one swing finishes before the next.")]
        public float bladeCooldown = 0.45f;
        [Tooltip("Boss health one swing takes when the boss is inside it.")]
        public float bladeBossDamage = 1f;
        [Tooltip("Pixels below the launcher tip where his hand is: the centre of the swing.")]
        public float bladeHandDrop = 5f;
    }
}
