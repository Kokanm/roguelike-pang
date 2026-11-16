using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>
    /// W08 Pop Mine: a press drops a mine at Wick's feet (one ammo). It falls to the surface below and sticks there;
    /// over a gap it falls out of the stage and is lost. After mineArmTime the first bubble that can be hit, rolling
    /// balloon that is not steel, boss bouncy bullet or the boss itself within mineTriggerRadius sets it off: everything
    /// within mineBlastRadius is hit once (bubbles born from the blast are not), breakable blocks in it are destroyed and
    /// steel rolling balls turn to roll away from it. Steel never sets one off. Up to mineMaxPlaced lie in wait; with that
    /// many out the button is dim and does nothing. Dropped on ice while running, a mine slides up to mineIceSlide units.
    /// Wick is never hurt by his own mines. Placed mines live on the stage (Game.Mines), so the last one keeps waiting
    /// after the slot empties; they pop harmlessly when the stage is cleared and are never saved.
    /// </summary>
    [Preserve]
    public sealed class PopMine : WeaponSpec
    {
        public override string Id => "mine";
        public override string Name => "Pop Mine";
        public override WeaponTier Tier => WeaponTier.Uncommon;
        public override int MaxAmmo => Mathf.Max(1, Tuning.I.mineAmmo);
        public override int Order => 8;

        static readonly string[] Icon11 =
        {
            ".....R.....",
            "....RYR....",
            "...LLLLL...",
            "..LSSSSSD..",
            ".LSSSSSSSD.",
            "DDDDDDDDDDD",
            "D..D...D..D",
        };

        static readonly string[] Hand =
        {
            "..R..",
            ".LSD.",
            "LSSSD",
            "DDDDD",
        };

        public override string[] IconRows => Icon11;
        public override string[] HeldRows => Hand;
        public override WeaponBehaviour CreateBehaviour() => new Behaviour();

        /// <summary>Most mines out at once (at least 1).</summary>
        public static int MaxPlaced => Mathf.Max(1, Tuning.I.mineMaxPlaced);

        public sealed class Behaviour : WeaponBehaviour
        {
            MineField own;

            /// <summary>The running game's mines, or (no game, in tests) a field this behaviour owns and ticks itself.</summary>
            public MineField Field => Game.ActiveMines ?? (own ??= new MineField(null));

            /// <summary>All mines are out: the button does nothing until one goes off.</summary>
            public override bool Dimmed => Field.Count >= MaxPlaced;

            public override void Tick(WeaponContext ctx, float dt)
            {
                if (own != null && ctx.World != null) own.Tick(ctx.World, dt);
                if (!ctx.Pressed || Field.Count >= MaxPlaced) return;
                if (ctx.Run != null && ctx.Run.WeaponAmmo < 1) return;
                if (!ctx.Spend()) return;
                Vector2 feet = ctx.Wick != null ? new Vector2(ctx.Wick.transform.position.x, ctx.Wick.FeetY) : ctx.Muzzle;
                float runX = ctx.Wick != null ? ctx.Wick.VelocityX : 0f;
                Field.Place(feet, runX, ctx.World != null ? ctx.World.Map : Game.Map);
                Sfx.Play(Sound.Turn, 0.55f, 0.6f); // placeholder drop click
            }

            /// <summary>Mines on the stage stay when the slot empties; only a field this behaviour owns (tests) goes.</summary>
            public override void Dispose()
            {
                own?.Clear();
                own = null;
            }
        }
    }

    /// <summary>One placed mine. Position is the middle of its bottom edge.</summary>
    public sealed class Mine
    {
        public Vector2 Position;
        public bool Resting;
        public float Age;
        public bool Armed => Age >= Tuning.I.mineArmTime;
        /// <summary>Where triggers and the blast are measured from: the middle of the mine's body.</summary>
        public Vector2 Centre => Position + new Vector2(0f, 2f * Tuning.I.Pixel);

        internal float fallSpeed;
        internal float slideLeft;
        internal int slideDir;
        internal float bounce;
        internal SpriteRenderer view;
    }

    /// <summary>What one blast did, for the effects and the tests.</summary>
    public struct MineBlast
    {
        public Vector2 centre;
        public int bubbles, rollers, bullets, steelTurned, tiles;
        public bool boss;
    }

    /// <summary>
    /// The mines lying on the stage and their blasts. The game owns one (Game.Mines) and ticks it every Play frame
    /// through Game.TickMines, so mines keep working after the weapon slot empties.
    /// </summary>
    public sealed class MineField
    {
        /// <summary>Pixels of the mine body (without its outline).</summary>
        public const int WidthPixels = 7, HeightPixels = 4;
        const float SlideSpeed = 8f;
        const float BlastGrowTime = 0.15f;

        readonly List<Mine> mines = new List<Mine>();
        readonly List<BlastView> blasts = new List<BlastView>();
        readonly List<WeaponTarget> scratch = new List<WeaponTarget>();
        readonly Transform parent;
        GameObject root;

        public MineField(Transform parent) { this.parent = parent; }

        public int Count => mines.Count;
        public IReadOnlyList<Mine> Mines => mines;

        /// <summary>Drops a mine at his feet. runX is his run speed: on ice the mine slides that way.</summary>
        public Mine Place(Vector2 feet, float runX, StageMap map)
        {
            var m = new Mine { Position = feet };
            if (map != null && Mathf.Abs(runX) > 0.1f && map.SurfaceUnder(feet.x, feet.y) == Surface.Ice)
            {
                m.slideDir = runX > 0f ? 1 : -1;
                m.slideLeft = Mathf.Max(0f, Tuning.I.mineIceSlide);
            }
            m.Resting = Supported(map, m.Position, out float floor) && Mathf.Abs(floor - feet.y) < 0.01f;
            if (m.Resting) m.Position.y = floor;
            mines.Add(m);
            MakeView(m);
            return m;
        }

        /// <summary>Moves, arms and sets off mines. Returns the blasts this frame.</summary>
        public List<MineBlast> Tick(WeaponWorld world, float dt, List<MineBlast> into = null)
        {
            into ??= new List<MineBlast>();
            into.Clear();
            var map = world != null ? world.Map : Game.Map;
            for (int i = mines.Count - 1; i >= 0; i--)
            {
                if (i >= mines.Count) continue; // a blast removed mines
                var m = mines[i];
                m.Age += dt;
                if (!Move(m, map, dt))
                {
                    Remove(m); // fell into a gap
                    continue;
                }
                if (world != null && m.Armed && Triggered(world, m.Centre))
                {
                    Remove(m);
                    into.Add(Blast(world, m.Centre, scratch));
                    ShowBlast(m.Centre);
                    Game.MineBlastFx(m.Centre);
                    continue;
                }
                UpdateView(m);
            }
            TickBlasts(dt);
            return into;
        }

        /// <summary>Falls, slides on ice, drops when the block under it breaks. False when it fell out of the stage.</summary>
        static bool Move(Mine m, StageMap map, float dt)
        {
            var t = Tuning.I;
            if (m.Resting && m.slideLeft > 0f)
            {
                float step = Mathf.Min(m.slideLeft, SlideSpeed * dt * (0.3f + 0.7f * m.slideLeft / Mathf.Max(0.01f, t.mineIceSlide)));
                float half = WidthPixels * 0.5f * t.Pixel;
                float nx = m.Position.x + m.slideDir * step;
                float edge = nx + m.slideDir * half;
                bool blocked = Mathf.Abs(edge) > t.HalfWidth ||
                               (map != null && map.IsSolid(StageMap.Col(edge), StageMap.Row(m.Position.y + 0.05f)));
                if (blocked) m.slideLeft = 0f;
                else
                {
                    m.Position.x = nx;
                    m.slideLeft -= step;
                }
            }
            if (m.Resting)
            {
                // The block under it broke, or it slid off an edge: it falls again.
                if (!Supported(map, m.Position, out float floor) || floor < m.Position.y - 0.01f) m.Resting = false;
            }
            if (!m.Resting)
            {
                m.fallSpeed = Mathf.Min(t.maxFallSpeed, m.fallSpeed + t.fallGravity * dt);
                float next = m.Position.y - m.fallSpeed * dt;
                bool hasFloor = Supported(map, m.Position, out float floor);
                if (hasFloor && next <= floor)
                {
                    next = floor;
                    m.fallSpeed = 0f;
                    m.Resting = true;
                    m.bounce = 0.08f;
                }
                m.Position.y = next;
                if (!hasFloor && next < -0.5f) return false;
            }
            if (m.bounce > 0f) m.bounce -= dt;
            return true;
        }

        /// <summary>The surface under p. False over a gap, where there is none.</summary>
        static bool Supported(StageMap map, Vector2 p, out float floor)
        {
            floor = map != null ? map.ShotFloor(p.x, p.y + 0.001f) : 0f;
            return floor >= 0f;
        }

        /// <summary>A bubble that can be hit, a rolling balloon that is not steel, a bouncy bullet or the boss is in reach.</summary>
        public static bool Triggered(WeaponWorld world, Vector2 centre)
        {
            foreach (var target in world.InCircle(centre, Tuning.I.mineTriggerRadius))
            {
                switch (target.kind)
                {
                    case TargetKind.Bubble:
                        if (!target.bubble.Steel && target.bubble.CanBeHit) return true;
                        break;
                    case TargetKind.Roller:
                        if (!target.roller.Steel) return true;
                        break;
                    case TargetKind.Bullet:
                        if (target.bullet.Bouncy && !target.bullet.Dead) return true;
                        break;
                    case TargetKind.Boss:
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Hits everything within mineBlastRadius once. Things born from the blast are not hit (the list is taken first).
        /// Breakable blocks it reaches are destroyed; steel rolling balls turn to roll away from it.
        /// </summary>
        public static MineBlast Blast(WeaponWorld world, Vector2 centre, List<WeaponTarget> scratch = null)
        {
            var t = Tuning.I;
            var result = new MineBlast { centre = centre };
            float radius = t.mineBlastRadius;
            var targets = world.InCircle(centre, radius, scratch).ToArray();
            var map = world.Map;
            foreach (var target in targets)
            {
                if (map != null && target.kind != TargetKind.Boss && !map.InStorey(target.position.y, centre.y)) continue;
                switch (target.kind)
                {
                    case TargetKind.Bubble:
                        if (world.WeaponHit(target.bubble)) result.bubbles++;
                        break;
                    case TargetKind.Roller:
                        var r = target.roller;
                        if (r.Steel)
                        {
                            float away = r.Position.x - centre.x;
                            r.Direction = Mathf.Abs(away) > 0.01f ? (away > 0f ? 1 : -1) : -r.Direction;
                            world.OnSteel?.Invoke(r.Position);
                            result.steelTurned++;
                        }
                        else if (world.WeaponHit(r)) result.rollers++;
                        break;
                    case TargetKind.Bullet:
                        if (world.Hit(target, 0f)) result.bullets++;
                        break;
                    case TargetKind.Boss:
                        if (world.LiveBoss == null) break;
                        world.WeaponHitBoss(t.mineBossDamage, target.position);
                        result.boss = true;
                        break;
                }
            }
            result.tiles = BreakTiles(world, centre, radius);
            return result;
        }

        static int BreakTiles(WeaponWorld world, Vector2 centre, float radius)
        {
            var map = world.Map;
            if (map == null) return 0;
            int destroyed = 0;
            int lo = (int)map.StoreyBase(centre.y);
            int hi = (int)map.StoreyCeiling(centre.y);
            int c0 = Mathf.Max(0, StageMap.Col(centre.x - radius)), c1 = Mathf.Min(StageMap.Cols - 1, StageMap.Col(centre.x + radius));
            int r0 = Mathf.Max(lo, StageMap.Row(centre.y - radius)), r1 = Mathf.Min(hi - 1, StageMap.Row(centre.y + radius));
            for (int c = c0; c <= c1; c++)
            for (int r = r0; r <= r1; r++)
            {
                if (map.Get(c, r).kind != TileKind.Breakable) continue;
                var cell = StageMap.CellRect(c, r);
                var closest = new Vector2(Mathf.Clamp(centre.x, cell.xMin, cell.xMax), Mathf.Clamp(centre.y, cell.yMin, cell.yMax));
                if (Vector2.Distance(closest, centre) > radius) continue;
                // HitTile takes one hit at a time; a few is always enough for the strongest block.
                for (int k = 0; k < 16 && map.Get(c, r).kind == TileKind.Breakable; k++) world.OnHitTile?.Invoke(c, r);
                if (map.Get(c, r).kind != TileKind.Breakable) destroyed++;
            }
            return destroyed;
        }

        void Remove(Mine m)
        {
            mines.Remove(m);
            DestroyObject(m.view != null ? m.view.gameObject : null);
            m.view = null;
        }

        /// <summary>The stage was cleared: every mine pops with a puff and nothing is hit.</summary>
        public void PopHarmlessly()
        {
            foreach (var m in mines) Game.MinePuff(m.Centre);
            Clear();
        }

        /// <summary>Removes every mine and blast (stage start, tests).</summary>
        public void Clear()
        {
            foreach (var m in mines) DestroyObject(m.view != null ? m.view.gameObject : null);
            mines.Clear();
            foreach (var b in blasts) DestroyObject(b.sr != null ? b.sr.gameObject : null);
            blasts.Clear();
            DestroyObject(root);
            root = null;
        }

        static void DestroyObject(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }

        // ------------------------------------------------------------------ look

        Transform Root()
        {
            if (root != null) return root.transform;
            root = new GameObject("Pop Mines");
            if (parent != null) root.transform.SetParent(parent, false);
            return root.transform;
        }

        void MakeView(Mine m)
        {
            var go = new GameObject("Pop Mine");
            go.transform.SetParent(Root(), false);
            m.view = go.AddComponent<SpriteRenderer>();
            Sorting.Apply(m.view, Layer.Shots, 0);
            Game.ApplySpriteMaterial(m.view);
            UpdateView(m);
        }

        /// <summary>Red light blinks slowly while arming, fast once armed. It bobs up a pixel when it lands.</summary>
        static void UpdateView(Mine m)
        {
            if (m.view == null) return;
            float period = m.Armed ? 0.16f : 0.6f;
            bool lit = Mathf.Repeat(m.Age, period) < period * 0.5f;
            m.view.sprite = MineSprite(lit);
            float px = Tuning.I.Pixel;
            m.view.transform.position = new Vector3(m.Position.x, m.Position.y + (m.bounce > 0f ? px : 0f), 0f);
        }

        static Sprite lightOn, lightOff;

        static Sprite MineSprite(bool lit)
        {
            if (lit && lightOn != null) return lightOn;
            if (!lit && lightOff != null) return lightOff;
            var rows = new[]
            {
                lit ? "...R..." : "...D...",
                "..LSD..",
                ".LSSSD.",
                "DDDDDDD",
            };
            var c = WeaponArt.Outlined(rows);
            // Pivot at the middle of the bottom edge of the body (inside the outline).
            var s = c.ToSprite(lit ? "PopMineLit" : "PopMine", new Vector2(c.Width * 0.5f, 1f), Tuning.I.pixelsPerUnit);
            if (lit) lightOn = s; else lightOff = s;
            return s;
        }

        sealed class BlastView
        {
            public SpriteRenderer sr;
            public float age;
        }

        void ShowBlast(Vector2 centre)
        {
            var go = new GameObject("Mine Blast");
            go.transform.SetParent(Root(), false);
            go.transform.position = centre;
            var sr = go.AddComponent<SpriteRenderer>();
            Sorting.Apply(sr, Layer.Shots, 5);
            Game.ApplySpriteMaterial(sr);
            sr.sprite = BlastSprite(-1);
            blasts.Add(new BlastView { sr = sr });
        }

        /// <summary>One white frame, then a yellow ring growing to the full radius in 0.15 s, then gone.</summary>
        void TickBlasts(float dt)
        {
            int full = Mathf.Max(2, Mathf.RoundToInt(Tuning.I.mineBlastRadius * Tuning.I.pixelsPerUnit));
            for (int i = blasts.Count - 1; i >= 0; i--)
            {
                var b = blasts[i];
                b.age += dt;
                if (b.age > BlastGrowTime + 0.05f || b.sr == null)
                {
                    DestroyObject(b.sr != null ? b.sr.gameObject : null);
                    blasts.RemoveAt(i);
                    continue;
                }
                float k = Mathf.Clamp01(b.age / BlastGrowTime);
                // Quantised to a few sizes, so only a handful of ring sprites are ever made.
                int r = Mathf.Max(2, Mathf.RoundToInt(full * Mathf.Ceil(k * 5f) / 5f));
                b.sr.sprite = BlastSprite(r);
            }
        }

        static readonly Dictionary<int, Sprite> rings = new Dictionary<int, Sprite>();

        /// <summary>A ring of pixel radius r (yellow edge, dotted red inside), or for r below 0 the white flash.</summary>
        static Sprite BlastSprite(int r)
        {
            if (rings.TryGetValue(r, out var s) && s != null) return s;
            var white = new Color32(255, 255, 255, 255);
            PixelCanvas c;
            if (r < 0)
            {
                const int d = 13;
                c = new PixelCanvas(d, d);
                c.Circle(d * 0.5f - 0.5f, d * 0.5f - 0.5f, d * 0.5f - 0.5f, Palette.SparkCore);
                c.Circle(d * 0.5f - 0.5f, d * 0.5f - 0.5f, d * 0.5f - 2.5f, white);
            }
            else
            {
                int size = 2 * r + 3;
                c = new PixelCanvas(size, size);
                float mid = (size - 1) * 0.5f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - mid) * (x - mid) + (y - mid) * (y - mid));
                    if (d <= r + 0.5f && d > r - 1.5f) c.Set(x, y, Palette.SparkCore);
                    else if (d <= r - 3f && d > r - 4.2f && (x + y) % 3 == 0) c.Set(x, y, Palette.Spark);
                }
            }
            s = c.ToSprite("MineBlast" + r, new Vector2(c.Width * 0.5f, c.Height * 0.5f), Tuning.I.pixelsPerUnit);
            rings[r] = s;
            return s;
        }
    }

    public partial class Game
    {
        MineField mines;

        /// <summary>The mines on the running game's stage (null with no game running, as in tests).</summary>
        public static MineField ActiveMines => active != null ? active.Mines : null;

        MineField Mines
        {
            get
            {
                if (mines != null) return mines;
                _ = Weapons; // makes sure the weapon root exists
                mines = new MineField(weaponRoot != null ? weaponRoot : world);
                return mines;
            }
        }

        /// <summary>Called from TickWeapons: placed mines work whether or not Wick still holds the Pop Mine.</summary>
        void TickMines(float dt)
        {
            mines?.Tick(Weapons, dt);
        }

        /// <summary>Stage start: no mine carries over.</summary>
        void ClearMines() => mines?.Clear();

        /// <summary>Stage cleared: the mines left pop harmlessly during the celebration.</summary>
        void PopMinesHarmlessly() => mines?.PopHarmlessly();

        /// <summary>Pop Mine blast feedback: red and yellow sparks, a pop and a 1 px shake.</summary>
        public static void MineBlastFx(Vector2 at)
        {
            var g = active;
            if (g == null) return;
            g.fx.Sparkle(at, Palette.Spark);
            g.fx.Sparkle(at + new Vector2(0.3f, 0.2f), Palette.SparkCore);
            g.fx.Sparkle(at + new Vector2(-0.3f, 0.3f), Palette.Spark);
            g.shake = Mathf.Max(g.shake, g.T.shakeTime * 0.5f);
            Sfx.Play(Sound.Pop, 0.6f, 0.9f);
        }

        /// <summary>A mine popping harmlessly at stage end.</summary>
        public static void MinePuff(Vector2 at)
        {
            var g = active;
            if (g == null) return;
            g.fx.Sparkle(at, Palette.Cream);
            Sfx.Play(Sound.Pop, 1.6f, 0.3f);
        }
    }

    public partial class Tuning
    {
        [Header("Weapon: Pop Mine")]
        [Tooltip("Mines in a Pop Mine crate.")]
        public int mineAmmo = 5;
        [Tooltip("Most mines lying on the stage at once. With this many out the weapon button is dim and does nothing.")]
        public int mineMaxPlaced = 3;
        [Tooltip("Seconds after it is dropped before a mine can go off.")]
        public float mineArmTime = 0.5f;
        [Tooltip("Units from the mine's middle within which a bubble, rolling balloon (not steel), bouncy boss bullet or the boss sets it off.")]
        public float mineTriggerRadius = 0.7f;
        [Tooltip("Units from the mine's middle that the blast reaches. Everything inside is hit once.")]
        public float mineBlastRadius = 2f;
        [Tooltip("Boss health one blast takes.")]
        public float mineBossDamage = 3f;
        [Tooltip("Units a mine slides the way he was running when he drops it on ice.")]
        public float mineIceSlide = 2f;
    }
}
