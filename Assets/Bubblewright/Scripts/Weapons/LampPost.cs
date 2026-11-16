using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>
    /// W16 Lamp Post: a press while Wick stands on a surface (not in the air, not on a ladder) holds him still for
    /// lampPlantTime, then a street lamp rises out of the surface at his feet and one ammo is spent. The post fires a
    /// harpoon of its own straight up every lampInterval seconds, a plain one without run upgrades, which pops, rings off
    /// steel, cracks ceramic, passes faded Ghosts, hurts the boss (lampBossDamage per cable) and chips a breakable block
    /// under the ceiling exactly like his. It goes after lampShots shots or lampLife seconds, dimming, flickering and
    /// sinking back. Up to lampMaxPlaced stand at once; with that many up, in the air or on a ladder the button is dim and
    /// does nothing. Nothing collides with the post: bubbles and Wick pass through it. Planted on a breakable block it
    /// falls with the block to the next surface (over a gap it is lost); on ice it does not slide. Posts live on the stage
    /// (Game.LampPosts), so the last one keeps working after the slot empties, and they are gone at stage end.
    /// </summary>
    [Preserve]
    public sealed class LampPost : WeaponSpec
    {
        public override string Id => "lamp";
        public override string Name => "Lamp Post";
        public override WeaponTier Tier => WeaponTier.Rare;
        public override int MaxAmmo => Mathf.Max(1, Tuning.I.lampAmmo);
        public override int Order => 16;

        static readonly string[] Icon11 =
        {
            "....DDD....",
            "...DDDDD...",
            "...LYYYL...",
            "...LYWYL...",
            "...LYYYL...",
            "...DDDDD...",
            ".....S.....",
            ".....S.....",
            "....DDD....",
        };

        static readonly string[] Hand =
        {
            ".DDD.",
            "DDDDD",
            "LYWYL",
            "LYYYL",
            "DDDDD",
            "..S..",
        };

        public override string[] IconRows => Icon11;
        public override string[] HeldRows => Hand;
        public override WeaponBehaviour CreateBehaviour() => new Behaviour();

        /// <summary>Most posts standing at once (at least 1).</summary>
        public static int MaxPlaced => Mathf.Max(1, Tuning.I.lampMaxPlaced);

        /// <summary>Wick can plant here: standing on a surface, not in the air or on a ladder.</summary>
        public static bool CanPlantFrom(WeaponContext ctx)
        {
            if (ctx.InAir) return false;
            return ctx.Wick == null || ctx.Wick.Mode == Mode.Ground;
        }

        public sealed class Behaviour : WeaponBehaviour
        {
            LampPostField own;
            Wick holding;
            bool dimmed;

            /// <summary>Seconds into planting, or below 0 when he is not planting.</summary>
            public float Planting { get; private set; } = -1f;

            /// <summary>The running game's posts, or (no game, in tests) a field this behaviour owns and ticks itself.</summary>
            public LampPostField Field => Game.ActiveLampPosts ?? (own ??= new LampPostField(null));

            /// <summary>In the air, on a ladder, while planting, or with every post already up: a press does nothing.</summary>
            public override bool Dimmed => dimmed;

            public override void Tick(WeaponContext ctx, float dt)
            {
                if (own != null && ctx.World != null) own.Tick(ctx.World, dt);
                bool canPlant = CanPlantFrom(ctx);
                if (Planting >= 0f)
                {
                    // Knocked off his feet (a hit, the block under him broke): the plant is off and nothing is spent.
                    if (!canPlant)
                    {
                        Cancel();
                    }
                    else
                    {
                        Planting += dt;
                        if (Planting >= Tuning.I.lampPlantTime) Plant(ctx);
                    }
                }
                else if (ctx.Pressed && canPlant && Field.Standing < MaxPlaced &&
                         (ctx.Run == null || ctx.Run.WeaponAmmo >= 1))
                {
                    Planting = 0f;
                    Hold(ctx.Wick);
                    if (Tuning.I.lampPlantTime <= 0f) Plant(ctx);
                }
                dimmed = Planting >= 0f || !canPlant || Field.Standing >= MaxPlaced;
            }

            void Plant(WeaponContext ctx)
            {
                Release();
                Planting = -1f;
                if (!ctx.Spend()) return;
                Vector2 feet = ctx.Wick != null ? new Vector2(ctx.Wick.transform.position.x, ctx.Wick.FeetY) : ctx.Muzzle;
                Field.Plant(feet, ctx.World != null ? ctx.World.Map : Game.Map);
            }

            void Cancel()
            {
                Release();
                Planting = -1f;
            }

            void Hold(Wick wick)
            {
                holding = wick;
                if (wick != null) wick.HoldStill = true;
            }

            void Release()
            {
                if (holding != null) holding.HoldStill = false;
                holding = null;
            }

            /// <summary>Lets go of Wick. Posts on the stage stay; only a field this behaviour owns (tests) goes.</summary>
            public override void Dispose()
            {
                Release();
                Planting = -1f;
                own?.Clear();
                own = null;
            }
        }
    }

    /// <summary>One planted lamp post. Position is the middle of its bottom edge.</summary>
    public sealed class PlantedPost
    {
        public Vector2 Position;
        public bool Resting;
        /// <summary>Seconds since it was planted.</summary>
        public float Age;
        /// <summary>Cables it has fired.</summary>
        public int Shots;
        /// <summary>Out of shots or time: it flickers, sinks and fires no more. It no longer counts as standing.</summary>
        public bool Retiring;
        /// <summary>Seconds since it started to retire.</summary>
        public float RetireAge;
        /// <summary>Its own harpoon: Wick's upgrades never reach it.</summary>
        public Harpoon Harpoon;

        public int ShotsLeft => Mathf.Max(0, LampPostField.ShotsPerPost - Shots);
        /// <summary>Where its cables leave: the top of the lamp.</summary>
        public Vector2 Top => Position + new Vector2(0f, LampPostField.HeightPixels * Tuning.I.Pixel);

        internal float fallSpeed;
        internal float nextShot;
        internal float flash;
        internal float bounce;
        internal SpriteRenderer view;
    }

    /// <summary>
    /// The lamp posts on the stage, their cables and what those cables hit. The game owns one (Game.LampPosts) and ticks
    /// it every Play frame through Game.TickLampPosts, so posts keep working after the weapon slot empties.
    /// </summary>
    public sealed class LampPostField
    {
        /// <summary>Pixels of the post (without its outline): a 5 px lamp on a 3 px pole with a 7 px foot.</summary>
        public const int WidthPixels = 7, HeightPixels = 24;
        /// <summary>Seconds the post takes to rise out of the floor; its first cable goes up when it is fully out.</summary>
        public const float RiseTime = 0.25f;
        /// <summary>Seconds it flickers before sinking, and seconds it takes to sink.</summary>
        public const float FlickerTime = 0.35f, SinkTime = 0.3f;
        const float FlashTime = 0.08f;

        readonly List<PlantedPost> posts = new List<PlantedPost>();
        readonly Transform parent;
        GameObject root;

        public LampPostField(Transform parent) { this.parent = parent; }

        /// <summary>Cables a post fires before it goes (at least 1).</summary>
        public static int ShotsPerPost => Mathf.Max(1, Tuning.I.lampShots);

        public int Count => posts.Count;
        public IReadOnlyList<PlantedPost> Posts => posts;

        /// <summary>Posts that still fire (not retiring). The button compares this with lampMaxPlaced.</summary>
        public int Standing
        {
            get
            {
                int n = 0;
                foreach (var p in posts) if (!p.Retiring) n++;
                return n;
            }
        }

        /// <summary>Plants a post on the surface under feet.</summary>
        public PlantedPost Plant(Vector2 feet, StageMap map)
        {
            var p = new PlantedPost { Position = feet, nextShot = RiseTime };
            p.Resting = Supported(map, p.Position, out float floor) && Mathf.Abs(floor - feet.y) < 0.01f;
            if (p.Resting) p.Position.y = floor;
            p.Harpoon = Harpoon.Create(Root(), HitTile);
            posts.Add(p);
            MakeView(p);
            Game.LampPlantFx(p.Position);
            return p;
        }

        System.Action<int, int> tileHit;
        void HitTile(int c, int r) => tileHit?.Invoke(c, r);

        /// <summary>Moves, fires and retires posts, ticks their cables and hits what they touch.</summary>
        public void Tick(WeaponWorld world, float dt)
        {
            tileHit = world != null ? world.OnHitTile : null;
            var map = world != null ? world.Map : Game.Map;
            var t = Tuning.I;
            for (int i = posts.Count - 1; i >= 0; i--)
            {
                var p = posts[i];
                p.Age += dt;
                if (!Move(p, map, dt))
                {
                    Remove(p); // fell into a gap with its block
                    continue;
                }
                if (p.Retiring) p.RetireAge += dt;
                else
                {
                    if (p.Age >= p.nextShot && p.Shots < ShotsPerPost) Fire(p, map);
                    if (p.Shots >= ShotsPerPost || p.Age >= t.lampLife)
                    {
                        p.Retiring = true;
                        p.RetireAge = 0f;
                    }
                }
                if (p.flash > 0f) p.flash -= dt;
                if (p.bounce > 0f) p.bounce -= dt;

                p.Harpoon.Tick(dt);
                if (world != null) Hits(world, p.Harpoon);

                // Gone once it has sunk and its last cable is done.
                if (p.Retiring && p.RetireAge >= FlickerTime + SinkTime && !p.Harpoon.Active)
                {
                    Remove(p);
                    continue;
                }
                UpdateView(p);
            }
        }

        void Fire(PlantedPost p, StageMap map)
        {
            var t = Tuning.I;
            p.nextShot += Mathf.Max(0.05f, t.lampInterval); // on a steady beat, so frames never add drift
            var top = p.Top;
            // A post right under a low tile has nowhere to fire; the shot is still counted, like a dud.
            p.Shots++;
            p.flash = FlashTime;
            if (map != null && map.IsSolid(StageMap.Col(top.x), StageMap.Row(top.y))) return;
            p.Harpoon.Fire(top);
            Game.LampShotFx(top);
        }

        /// <summary>
        /// The same rules the game's own harpoon loop uses: a steel bubble or steel rolling ball rings it off, a faded Ghost
        /// is passed, anything else takes one hit (ceramic cracks) and the cable is used up. The boss takes lampBossDamage.
        /// </summary>
        public static void Hits(WeaponWorld world, Harpoon h)
        {
            if (!h.Active) return;
            var bubbles = world.Bubbles;
            if (bubbles != null)
            {
                for (int i = 0; i < bubbles.Count; i++)
                {
                    var b = bubbles[i];
                    if (b == null || b.Popped) continue;
                    if (b.Steel && h.Hits(b))
                    {
                        h.Hide();
                        world.OnSteel?.Invoke(b.Position);
                        return;
                    }
                    if (!b.CanBeHit || !h.Hits(b)) continue;
                    h.Hide();
                    world.WeaponHit(b);
                    return;
                }
            }
            var rollers = world.Rollers;
            if (rollers != null)
            {
                for (int i = 0; i < rollers.Count; i++)
                {
                    var r = rollers[i];
                    if (r == null || r.Popped || !h.Hits(r)) continue;
                    h.Hide();
                    world.WeaponHit(r); // steel sparkles and is unharmed
                    return;
                }
            }
            var boss = world.LiveBoss;
            if (boss != null && h.Hits(boss.Body))
            {
                var body = boss.Body;
                var cable = h.CableRect;
                var at = new Vector2(Mathf.Clamp(cable.center.x, body.xMin, body.xMax), Mathf.Clamp(h.TipRect.center.y, body.yMin, body.yMax));
                h.Hide();
                world.WeaponHitBoss(Tuning.I.lampBossDamage, at);
            }
        }

        /// <summary>Drops with the block under it when that breaks. False when it fell out of the stage.</summary>
        static bool Move(PlantedPost p, StageMap map, float dt)
        {
            var t = Tuning.I;
            if (p.Resting && (!Supported(map, p.Position, out float under) || under < p.Position.y - 0.01f)) p.Resting = false;
            if (p.Resting) return true;
            p.fallSpeed = Mathf.Min(t.maxFallSpeed, p.fallSpeed + t.fallGravity * dt);
            float next = p.Position.y - p.fallSpeed * dt;
            bool hasFloor = Supported(map, p.Position, out float floor);
            if (hasFloor && next <= floor)
            {
                next = floor;
                p.fallSpeed = 0f;
                p.Resting = true;
                p.bounce = 0.08f;
            }
            p.Position.y = next;
            return hasFloor || next >= -0.5f;
        }

        /// <summary>The surface under p. False over a gap, where there is none.</summary>
        static bool Supported(StageMap map, Vector2 p, out float floor)
        {
            floor = map != null ? map.ShotFloor(p.x, p.y + 0.001f) : 0f;
            return floor >= 0f;
        }

        void Remove(PlantedPost p)
        {
            posts.Remove(p);
            Destroy(p);
        }

        static void Destroy(PlantedPost p)
        {
            DestroyObject(p.view != null ? p.view.gameObject : null);
            DestroyObject(p.Harpoon != null ? p.Harpoon.gameObject : null);
            p.view = null;
            p.Harpoon = null;
        }

        /// <summary>The stage was cleared: every post goes out with a puff and its cable vanishes.</summary>
        public void PutOut()
        {
            foreach (var p in posts) Game.LampPuff(p.Top);
            Clear();
        }

        /// <summary>Removes every post and cable (stage start, tests).</summary>
        public void Clear()
        {
            foreach (var p in posts) Destroy(p);
            posts.Clear();
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
            root = new GameObject("Lamp Posts");
            if (parent != null) root.transform.SetParent(parent, false);
            return root.transform;
        }

        void MakeView(PlantedPost p)
        {
            var go = new GameObject("Lamp Post");
            go.transform.SetParent(Root(), false);
            p.view = go.AddComponent<SpriteRenderer>();
            Sorting.Apply(p.view, Layer.Stage, 50); // behind bubbles, Wick and the cables
            Game.ApplySpriteMaterial(p.view);
            UpdateView(p);
        }

        /// <summary>The pixel rows showing above the surface: it rises out of it, and sinks back into it.</summary>
        public static int ShownRows(PlantedPost p)
        {
            if (p.Retiring && p.RetireAge > FlickerTime)
                return Mathf.Clamp(Mathf.RoundToInt(HeightPixels * (1f - (p.RetireAge - FlickerTime) / SinkTime)), 0, HeightPixels);
            if (p.Age < RiseTime)
            {
                float k = p.Age / RiseTime;
                k = 1f - (1f - k) * (1f - k); // eases out
                return Mathf.Clamp(Mathf.RoundToInt(HeightPixels * k), 1, HeightPixels);
            }
            return HeightPixels;
        }

        /// <summary>Lamp lit yellow, white for a moment on each shot, dim while flickering out; one small light per shot left.</summary>
        static void UpdateView(PlantedPost p)
        {
            if (p.view == null) return;
            var lamp = p.flash > 0f ? Lamp.Flash : Lamp.Lit;
            if (p.Retiring)
                lamp = p.RetireAge < FlickerTime && Mathf.Repeat(p.RetireAge, 0.1f) < 0.05f ? Lamp.Lit : Lamp.Dark;
            int rows = ShownRows(p);
            p.view.enabled = rows > 0;
            if (rows > 0) p.view.sprite = PostSprite(lamp, LightsOn(p.ShotsLeft), rows);
            float px = Tuning.I.Pixel;
            p.view.transform.position = new Vector3(p.Position.x, p.Position.y + (p.bounce > 0f ? px : 0f), 0f);
        }

        enum Lamp { Lit, Flash, Dark }

        static readonly Dictionary<int, Sprite> sprites = new Dictionary<int, Sprite>();

        /// <summary>Small lights still on (0 to 8) for this many shots left: they stand for shares of lampShots.</summary>
        public static int LightsOn(int shotsLeft) => Mathf.Clamp(Mathf.CeilToInt(8f * shotsLeft / ShotsPerPost - 0.0001f), 0, 8);

        /// <summary>The post's pixel rows, top first.</summary>
        public static string[] Rows(bool lit, bool flash, int lightsOn)
        {
            string glow = flash ? "W" : lit ? "Y" : "S";
            string core = flash || lit ? "W" : "S";
            var rows = new List<string>
            {
                "..DDD..",
                ".DDDDD.",
                ".L" + glow + core + glow + "L.",
                ".L" + glow + glow + glow + "L.",
                ".L" + glow + glow + glow + "L.",
                ".DDDDD.",
            };
            // The pole: 16 rows, with eight small lights down its middle that go out one per shot, from the top.
            for (int i = 0; i < 16; i++)
            {
                int light = i / 2;
                bool hasLight = i % 2 == 0;
                bool on = hasLight && light >= 8 - lightsOn;
                rows.Add(hasLight ? (on && lit ? "..LYD.." : "..LDD..") : "..LSD..");
            }
            rows.Add(".DSSSD.");
            rows.Add("DDDDDDD");
            return rows.ToArray();
        }

        static Sprite PostSprite(Lamp lamp, int lightsOn, int shownRows)
        {
            int key = ((int)lamp * 16 + lightsOn) * 64 + shownRows;
            if (sprites.TryGetValue(key, out var s) && s != null) return s;
            var all = Rows(lamp != Lamp.Dark, lamp == Lamp.Flash, lightsOn);
            var rows = new string[shownRows];
            System.Array.Copy(all, 0, rows, 0, shownRows); // only the top shows while it rises or sinks
            var c = WeaponArt.Outlined(rows);
            // Pivot at the middle of the bottom edge of the post (inside the outline).
            s = c.ToSprite("LampPost" + key, new Vector2(c.Width * 0.5f, 1f), Tuning.I.pixelsPerUnit);
            sprites[key] = s;
            return s;
        }
    }

    public partial class Game
    {
        LampPostField lampPosts;

        /// <summary>The lamp posts on the running game's stage (null with no game running, as in tests).</summary>
        public static LampPostField ActiveLampPosts => active != null ? active.LampPosts : null;

        LampPostField LampPosts
        {
            get
            {
                if (lampPosts != null) return lampPosts;
                _ = Weapons; // makes sure the weapon root exists
                lampPosts = new LampPostField(weaponRoot != null ? weaponRoot : world);
                return lampPosts;
            }
        }

        /// <summary>Called from TickWeapons: posts work whether or not Wick still holds the Lamp Post.</summary>
        void TickLampPosts(float dt) => lampPosts?.Tick(Weapons, dt);

        /// <summary>Stage start: no post carries over.</summary>
        void ClearLampPosts() => lampPosts?.Clear();

        /// <summary>Stage cleared: the posts go out with a puff.</summary>
        void PutOutLampPosts() => lampPosts?.PutOut();

        /// <summary>A post rising out of the floor: cream specks and a low clunk.</summary>
        public static void LampPlantFx(Vector2 at)
        {
            var g = active;
            if (g == null) return;
            g.fx.Sparkle(at + new Vector2(0f, 0.2f), Palette.Cream);
            Sfx.Play(Sound.Turn, 0.6f, 0.6f);
        }

        /// <summary>A post firing: a flash at the lamp and a softer fire sound than Wick's.</summary>
        public static void LampShotFx(Vector2 at)
        {
            var g = active;
            if (g == null) return;
            g.fx.MuzzleFlash(at);
            Sfx.Play(Sound.Fire, 1.2f, 0.55f);
        }

        /// <summary>A post going out at stage end.</summary>
        public static void LampPuff(Vector2 at)
        {
            var g = active;
            if (g == null) return;
            g.fx.Sparkle(at, Palette.Cream);
            Sfx.Play(Sound.Pop, 1.4f, 0.3f);
        }
    }

    public partial class Tuning
    {
        [Header("Weapon: Lamp Post")]
        [Tooltip("Posts in a Lamp Post crate.")]
        public int lampAmmo = 2;
        [Tooltip("Seconds between a post's cables. Its first cable goes up as soon as it has risen.")]
        public float lampInterval = 1.4f;
        [Tooltip("Seconds a post stands before it sinks away, even with shots left.")]
        public float lampLife = 12f;
        [Tooltip("Cables a post fires before it sinks away.")]
        public int lampShots = 8;
        [Tooltip("Seconds Wick stands still planting a post. Knocked off his feet, the plant is off and nothing is spent.")]
        public float lampPlantTime = 0.4f;
        [Tooltip("Most posts standing at once. With this many up the weapon button is dim and does nothing.")]
        public int lampMaxPlaced = 1;
        [Tooltip("Boss health one of a post's cables takes.")]
        public float lampBossDamage = 1f;
    }
}
