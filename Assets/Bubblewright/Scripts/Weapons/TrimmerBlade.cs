using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>
    /// W05 Trimmer Blade: a melee weapon. Each swing is one round sweep from his left side, over his head and down his
    /// right side (bladeArc degrees, centred on straight up), bladeReach units from his raised hand, so it reaches the
    /// floor on both sides. Falling with Down held, the arc turns over and covers both sides and below him.
    /// The hit test runs once at the start of the swing; the swing time is only the drawing. Solid tiles cut the swing
    /// off, so a wall or block beside him shields that side. Boss bullets in the arc are cut out of the air, and a
    /// steel rolling ball is turned round so it rolls away from him. All hits go through the shared WeaponWorld rules.
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

        /// <summary>
        /// One swing from the hand: hits everything inside the arc that no solid tile hides, once each.
        /// Things born from this swing (children of a pop) only appear after the hit-stop, so they are never hit by it.
        /// </summary>
        public static SwingResult Swing(WeaponWorld world, Vector2 hand, bool down, List<WeaponTarget> scratch = null)
        {
            var t = Tuning.I;
            var result = new SwingResult();
            float reach = t.bladeReach;
            Vector2 facing = down ? Vector2.down : Vector2.up;
            var found = world.InSector(hand, reach, facing, t.bladeArc, scratch);
            var map = world.Map;
            // Hit from a copy: hitting can change the lists the query read from.
            var targets = found.ToArray();
            foreach (var target in targets)
            {
                if (!Reaches(map, hand, NearPoint(target, hand), -1, -1)) continue;
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
            result.tiles = HitTiles(world, map, hand, reach, facing, t.bladeArc);
            return result;
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
        /// A hand that is inside a tile reaches nothing.
        /// </summary>
        public static bool Reaches(StageMap map, Vector2 hand, Vector2 point, int skipCol, int skipRow)
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
                if (map.IsSolid(c, r)) return false;
            }
            return true;
        }

        /// <summary>Every breakable block inside the arc, with nothing solid in front of it, takes one hit.</summary>
        static int HitTiles(WeaponWorld world, StageMap map, Vector2 hand, float reach, Vector2 facing, float arc)
        {
            if (map == null) return 0;
            float half = arc * 0.5f;
            int c0 = StageMap.Col(hand.x - reach), c1 = StageMap.Col(hand.x + reach);
            int r0 = StageMap.Row(hand.y - reach), r1 = StageMap.Row(hand.y + reach);
            var hits = new List<Vector2Int>();
            for (int r = r0; r <= r1; r++)
            for (int c = c0; c <= c1; c++)
            {
                if (c < 0 || c >= StageMap.Cols || r < 0 || r >= StageMap.Rows) continue;
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
                if (!inArc || !Reaches(map, hand, q, c, r)) continue;
                hits.Add(new Vector2Int(c, r));
            }
            // Hit after the search, so a block that breaks does not open a line to the one behind it in the same swing.
            foreach (var h in hits) world.OnHitTile?.Invoke(h.x, h.y);
            return hits.Count;
        }

        public sealed class Behaviour : WeaponBehaviour
        {
            /// <summary>Seconds since the last swing. Starts high so the first press swings at once.</summary>
            float sinceSwing = 999f;
            /// <summary>A press that came during the wait swings as soon as it ends, so a tap is never eaten.</summary>
            bool queued;
            /// <summary>Which way the next swing is drawn: +1 left to right, -1 right to left.</summary>
            int direction = 1;
            /// <summary>What the last swing did.</summary>
            public SwingResult Last { get; private set; }
            /// <summary>Swings so far.</summary>
            public int Swings { get; private set; }

            readonly List<WeaponTarget> scratch = new List<WeaponTarget>();
            BladeArcView view;

            public override void Tick(WeaponContext ctx, float dt)
            {
                sinceSwing += dt;
                view?.Tick(dt, ctx.Wick != null ? HandPoint(ctx.Wick.LauncherTip) : (Vector2?)null);
                if (ctx.Pressed) queued = true;
                if (!ctx.CanFire) return;
                if (!queued && !ctx.Held) return;
                if (sinceSwing < Tuning.I.bladeCooldown) return;

                queued = false;
                if (!ctx.Spend()) return;
                sinceSwing = 0f;
                Swings++;

                Vector2 hand = ctx.Wick != null ? HandPoint(ctx.Wick.LauncherTip) : ctx.Muzzle;
                Last = Swing(ctx.World, hand, ctx.ShootDown, scratch);

                if (ctx.Wick != null)
                {
                    ctx.Wick.OnFire();
                    view ??= BladeArcView.Create(ctx.Wick.transform.parent);
                    view.Play(hand, direction, ctx.ShootDown);
                }
                Sfx.Play(Sound.Turn, 1.7f, 0.8f); // placeholder swish
                direction = -direction;
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
        const int SweepFrames = 3, FadeFrames = 3;
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
            float sweep = Mathf.Max(0.01f, Tuning.I.bladeSwingTime);
            int i = time < sweep
                ? Mathf.Clamp(Mathf.FloorToInt(time / sweep * SweepFrames), 0, SweepFrames - 1)
                : SweepFrames + Mathf.FloorToInt((time - sweep) / FadeFrameTime);
            if (i >= f.Length)
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
            if (frames != null && framesReach == t.bladeReach && framesArc == t.bladeArc) return frames;
            framesReach = t.bladeReach;
            framesArc = t.bladeArc;
            int radius = Mathf.Max(4, Mathf.RoundToInt(t.bladeReach * t.pixelsPerUnit));
            int size = radius * 2 + 5;
            float start = 90f + t.bladeArc * 0.5f, span = t.bladeArc; // from low on his left, over the top, to low on his right
            var list = new List<Sprite>();
            for (int k = 1; k <= SweepFrames; k++)
            {
                var c = new PixelCanvas(size, size);
                float progress = (float)k / SweepFrames;
                float head = start - span * progress;
                for (float a = start; a >= head; a -= 0.5f)
                {
                    float behind = (a - head) / Mathf.Max(1f, span * progress); // 0 at the head, 1 at the tail
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
        [Tooltip("Seconds the swing takes to draw. The hit test happens at the start; this is only the animation.")]
        public float bladeSwingTime = 0.14f;
        [Tooltip("Seconds between swings, tapped or held.")]
        public float bladeCooldown = 0.3f;
        [Tooltip("Boss health one swing takes when the boss is inside it.")]
        public float bladeBossDamage = 1f;
        [Tooltip("Pixels below the launcher tip where his hand is: the centre of the swing.")]
        public float bladeHandDrop = 5f;
    }
}
