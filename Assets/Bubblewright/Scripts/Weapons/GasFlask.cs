using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>
    /// W09 Gas Flask: three flasks. A press lobs a flask straight up; it bursts gasLobHeight above the weapon tip, or on
    /// the first bubble, rolling balloon, boss or solid tile it touches, into an olive cloud of gasRadius that hangs for
    /// gasCloudTime and rises slowly through platforms. A bubble or rolling balloon that touches the cloud turns sick and
    /// is hit once gasSickTime later, wherever it has gone. Steel is immune; faded Ghosts are caught too; ceramic cracks
    /// and needs a second dose. A Scorch bubble touching a cloud sets it off: everything inside is hit once, cloud gone.
    /// The boss loses gasBossDamage health a second inside a cloud, at most gasBossMax per flask.
    /// Falling with Down held, the flask breaks at his feet and the cloud sits on the floor below him.
    /// Flasks in flight, clouds and sick timers live in a GasField that outlasts the weapon, so the last flask's cloud
    /// still works after the slot empties.
    /// </summary>
    [Preserve]
    public sealed class GasFlask : WeaponSpec
    {
        public override string Id => "gas";
        public override string Name => "Gas Flask";
        public override WeaponTier Tier => WeaponTier.Rare;
        public override int MaxAmmo => Mathf.Max(1, Tuning.I.gasAmmo);
        public override int Order => 9;

        static readonly string[] Icon11 =
        {
            "....DD.....",
            "....CC.....",
            "....SS.....",
            "...OOOO....",
            "..OOCOOO...",
            ".OOCOOOOo..",
            ".OOOOOOOo..",
            ".OOOOOOoo..",
            "..OOOOoo...",
            "...oooo....",
        };

        static readonly string[] Hand =
        {
            "..DD..",
            "..CC..",
            ".OOOO.",
            "OOCOOo",
            "OOOOOo",
            "OOOOoo",
            ".oooo.",
        };

        public override string[] IconRows => Icon11;
        public override string[] HeldRows => Hand;
        /// <summary>The flask in flight uses the in-hand art.</summary>
        public static string[] FlaskRows => Hand;
        public override WeaponBehaviour CreateBehaviour() => new Behaviour();

        public sealed class Behaviour : WeaponBehaviour
        {
            /// <summary>Where thrown flasks go. Null uses the running game's field (Game.Gas). Tests set their own.</summary>
            public GasField Field;
            float cooldown;

            public override void Tick(WeaponContext ctx, float dt)
            {
                if (cooldown > 0f) cooldown -= dt;
                if (!ctx.Pressed || !ctx.CanFire || cooldown > 0f) return;
                var field = Field ?? Game.Gas;
                if (field == null || !ctx.Spend()) return;
                cooldown = Tuning.I.gasCooldown;
                ctx.Flash();
                field.Throw(ctx.Muzzle, ctx.ShootDown);
            }
        }
    }

    /// <summary>
    /// The Gas Flask's flasks in flight, clouds and sick bubbles. Game owns one (ticked after the shots, cleared at every
    /// stage start and stage clear); tests build their own on a WeaponWorld.
    /// </summary>
    public sealed class GasField
    {
        public sealed class Flask
        {
            public Vector2 Position;
            public float StartY;
            public float Age;
            public SpriteRenderer sr;
        }

        public sealed class Cloud
        {
            public Vector2 Centre;
            public float Age;
            /// <summary>A cloud from a down throw sits on the floor and does not rise.</summary>
            public bool Grounded;
            /// <summary>The height the flask was thrown from. The cloud stays in that storey.</summary>
            public float HomeY;
            /// <summary>Boss health this cloud has taken so far (at most gasBossMax).</summary>
            public float BossTaken;
            public SpriteRenderer sr;
        }

        public WeaponWorld World;
        /// <summary>A flask broke here (effects).</summary>
        public System.Action<Vector2> OnBurst;
        /// <summary>A sick thing gives off an olive speck here (effects).</summary>
        public System.Action<Vector2> OnSpeck;
        /// <summary>A Scorch bubble set a cloud off here (effects).</summary>
        public System.Action<Vector2> OnScorch;

        readonly Transform parent;
        readonly List<Flask> flasks = new List<Flask>();
        readonly List<Cloud> clouds = new List<Cloud>();
        readonly Dictionary<Bubble, float> sickBubbles = new Dictionary<Bubble, float>();
        readonly Dictionary<Roller, float> sickRollers = new Dictionary<Roller, float>();
        readonly List<Bubble> bubbleScratch = new List<Bubble>();
        readonly List<Roller> rollerScratch = new List<Roller>();
        float speckClock;

        public static readonly Color SickTint = new Color(0.72f, 0.78f, 0.32f, 1f);
        const float SpeckEvery = 0.25f;
        const float SpinStep = 3f / 60f; // a quarter turn every 3 frames

        public GasField(WeaponWorld world, Transform parent)
        {
            World = world;
            this.parent = parent;
        }

        public IReadOnlyList<Flask> Flasks => flasks;
        public IReadOnlyList<Cloud> Clouds => clouds;
        public bool IsSick(Bubble b) => b != null && sickBubbles.ContainsKey(b);
        public bool IsSick(Roller r) => r != null && sickRollers.ContainsKey(r);
        public int SickCount => sickBubbles.Count + sickRollers.Count;
        float burstFromY = float.NaN;

        /// <summary>A flask leaves from the weapon tip. Thrown down, it breaks at once and the cloud sits on the floor below.</summary>
        public void Throw(Vector2 from, bool down)
        {
            if (down)
            {
                float floor = Game.Map != null ? Game.Map.ShotFloor(from.x, from.y) : 0f;
                if (floor < 0f) floor = 0f;
                burstFromY = from.y;
                Burst(new Vector2(from.x, floor), true);
                return;
            }
            var f = new Flask { Position = from, StartY = from.y };
            f.sr = Renderer("Flask", Layer.Shots, 2, GasArt.Flask);
            Show(f);
            flasks.Add(f);
        }

        public void Tick(float dt)
        {
            TickFlasks(dt);
            TickClouds(dt);
            TickSick(dt);
        }

        // ------------------------------------------------------------------ flasks

        void TickFlasks(float dt)
        {
            var t = Tuning.I;
            for (int i = flasks.Count - 1; i >= 0; i--)
            {
                var f = flasks[i];
                f.Age += dt;
                float dist = t.gasFlaskSpeed * dt;
                int steps = Mathf.Max(1, Mathf.CeilToInt(dist / 0.2f));
                bool burst = false;
                for (int k = 0; k < steps && !burst; k++)
                {
                    f.Position.y += dist / steps;
                    float cap = Game.Map != null ? Game.Map.StoreyCeiling(f.StartY) : t.arenaHeight;
                    if (f.Position.y - f.StartY >= t.gasLobHeight || f.Position.y >= cap)
                    {
                        f.Position.y = Mathf.Min(f.Position.y, Mathf.Min(f.StartY + t.gasLobHeight, cap));
                        burst = true;
                    }
                    else burst = Touches(f);
                }
                if (burst)
                {
                    Kill(f.sr);
                    flasks.RemoveAt(i);
                    burstFromY = f.StartY;
                    Burst(f.Position, false);
                }
                else Show(f);
            }
        }

        /// <summary>The flask breaks on the first bubble (any kind), rolling balloon, boss or solid tile it touches.</summary>
        bool Touches(Flask f)
        {
            Vector2 p = f.Position;
            var map = Game.Map;
            if (map != null)
            {
                int row = StageMap.Row(p.y);
                if (map.RowInStorey(row, f.StartY) && map.IsSolid(StageMap.Col(p.x), row)) return true;
            }
            float half = 2f * Tuning.I.Pixel;
            var box = new Rect(p.x - half, p.y - half, 2f * half, 2f * half);
            foreach (var b in World.Bubbles)
                if (b != null && !b.Popped && b.Overlaps(box) && (map == null || map.InStorey(b.Position.y, f.StartY))) return true;
            foreach (var r in World.Rollers)
                if (r != null && !r.Popped && r.Overlaps(box) && (map == null || map.InStorey(r.Position.y, f.StartY))) return true;
            var boss = World.LiveBoss;
            return boss != null && boss.Body.Overlaps(box);
        }

        void Show(Flask f)
        {
            if (f.sr == null) return;
            f.sr.transform.position = f.Position;
            int quarter = Mathf.FloorToInt(f.Age / SpinStep) % 4;
            f.sr.transform.rotation = Quaternion.Euler(0f, 0f, -90f * quarter);
        }

        void Burst(Vector2 at, bool grounded)
        {
            float home = float.IsNaN(burstFromY) ? at.y : burstFromY;
            burstFromY = float.NaN;
            var c = new Cloud { Centre = at, Grounded = grounded, HomeY = home };
            c.sr = Renderer("GasCloud", Layer.Bubbles, -1, GasArt.Cloud);
            clouds.Add(c);
            ShowCloud(c);
            OnBurst?.Invoke(at);
        }

        // ------------------------------------------------------------------ clouds

        void TickClouds(float dt)
        {
            var t = Tuning.I;
            for (int i = clouds.Count - 1; i >= 0; i--)
            {
                var c = clouds[i];
                c.Age += dt;
                if (!c.Grounded)
                {
                    float cap = Game.Map != null ? Game.Map.StoreyCeiling(c.HomeY) : t.arenaHeight;
                    c.Centre.y = Mathf.Min(cap, c.Centre.y + t.gasRise * dt);
                }
                if (c.Age >= t.gasCloudTime)
                {
                    RemoveCloud(i);
                    continue;
                }
                if (ScorchInside(c))
                {
                    Detonate(c);
                    RemoveCloud(i);
                    continue;
                }
                Infect(c, dt);
                ShowCloud(c);
            }
        }

        public bool Inside(Cloud c, Vector2 p, float radius) => Vector2.Distance(p, c.Centre) < Tuning.I.gasRadius + radius;

        bool ScorchInside(Cloud c)
        {
            foreach (var b in World.Bubbles)
                if (b != null && !b.Popped && b.Kind == BubbleKind.Scorch && !b.Steel && Inside(c, b.Position, b.Radius)) return true;
            return false;
        }

        /// <summary>Things touching the cloud turn sick (steel never); the boss loses health while inside.</summary>
        void Infect(Cloud c, float dt)
        {
            var t = Tuning.I;
            foreach (var b in World.Bubbles)
            {
                if (b == null || b.Popped || b.Steel || sickBubbles.ContainsKey(b) || !Inside(c, b.Position, b.Radius)) continue;
                if (Game.Map != null && !Game.Map.InStorey(b.Position.y, c.HomeY)) continue;
                sickBubbles[b] = t.gasSickTime;
                Tint(b, true);
            }
            foreach (var r in World.Rollers)
            {
                if (r == null || r.Popped || r.Steel || sickRollers.ContainsKey(r) || !Inside(c, r.Position, r.Radius)) continue;
                if (Game.Map != null && !Game.Map.InStorey(r.Position.y, c.HomeY)) continue;
                sickRollers[r] = t.gasSickTime;
                Tint(r, true);
            }
            var boss = World.LiveBoss;
            if (boss != null && c.BossTaken < t.gasBossMax && BossInside(c, boss))
            {
                float amount = Mathf.Min(t.gasBossDamage * dt, t.gasBossMax - c.BossTaken);
                c.BossTaken += amount;
                World.WeaponHitBoss(amount, Closest(boss.Body, c.Centre));
            }
        }

        bool BossInside(Cloud c, Boss boss) => Vector2.Distance(Closest(boss.Body, c.Centre), c.Centre) < Tuning.I.gasRadius;

        static Vector2 Closest(Rect r, Vector2 p) => new Vector2(Mathf.Clamp(p.x, r.xMin, r.xMax), Mathf.Clamp(p.y, r.yMin, r.yMax));

        /// <summary>A Scorch set it off: everything inside is hit once (steel never), the boss takes what is left of its share.</summary>
        void Detonate(Cloud c)
        {
            var t = Tuning.I;
            bubbleScratch.Clear();
            foreach (var b in World.Bubbles)
                if (b != null && !b.Popped && !b.Steel && Inside(c, b.Position, b.Radius)) bubbleScratch.Add(b);
            rollerScratch.Clear();
            foreach (var r in World.Rollers)
                if (r != null && !r.Popped && !r.Steel && Inside(c, r.Position, r.Radius)) rollerScratch.Add(r);
            foreach (var b in bubbleScratch) HitSick(b);
            foreach (var r in rollerScratch) HitSick(r);
            var boss = World.LiveBoss;
            if (boss != null && BossInside(c, boss))
            {
                float amount = Mathf.Max(0f, Mathf.Min(t.gasBossDamage, t.gasBossMax - c.BossTaken));
                c.BossTaken += amount;
                World.WeaponHitBoss(amount, Closest(boss.Body, c.Centre));
            }
            OnScorch?.Invoke(c.Centre);
        }

        void RemoveCloud(int i)
        {
            Kill(clouds[i].sr);
            clouds.RemoveAt(i);
        }

        void ShowCloud(Cloud c)
        {
            if (c.sr == null) return;
            var t = Tuning.I;
            float px = t.Pixel;
            c.sr.transform.position = new Vector3(Mathf.Round(c.Centre.x / px) * px, Mathf.Round(c.Centre.y / px) * px, 0f);
            float left = t.gasCloudTime - c.Age;
            float fade = Mathf.Max(0.01f, t.gasFadeTime);
            c.sr.color = new Color(1f, 1f, 1f, left < fade ? Mathf.Clamp01(left / fade) : 1f);
        }

        // ------------------------------------------------------------------ sickness

        void TickSick(float dt)
        {
            bubbleScratch.Clear();
            var keys = new List<Bubble>(sickBubbles.Keys);
            foreach (var b in keys)
            {
                if (b == null || b.Popped || !World.Bubbles.Contains(b))
                {
                    sickBubbles.Remove(b);
                    continue;
                }
                float left = sickBubbles[b] - dt;
                sickBubbles[b] = left;
                Wobble(b, left);
                if (left <= 0f) bubbleScratch.Add(b);
            }
            rollerScratch.Clear();
            var rkeys = new List<Roller>(sickRollers.Keys);
            foreach (var r in rkeys)
            {
                if (r == null || r.Popped || !World.Rollers.Contains(r))
                {
                    sickRollers.Remove(r);
                    continue;
                }
                float left = sickRollers[r] - dt;
                sickRollers[r] = left;
                if (left <= 0f) rollerScratch.Add(r);
            }
            foreach (var b in bubbleScratch) HitSick(b);
            foreach (var r in rollerScratch) HitSick(r);

            speckClock += dt;
            if (speckClock >= SpeckEvery)
            {
                speckClock = 0f;
                if (OnSpeck != null)
                {
                    foreach (var b in sickBubbles.Keys) if (b != null) OnSpeck(b.Position + Vector2.up * b.Radius);
                    foreach (var r in sickRollers.Keys) if (r != null) OnSpeck(r.Position + Vector2.up * r.Radius);
                }
            }
        }

        /// <summary>
        /// One hit from the gas. It goes through the shared helper, except that a faded Ghost is caught too: the gas does
        /// not need to touch it, so it is hit even while the harpoon would pass through. Steel never.
        /// </summary>
        void HitSick(Bubble b)
        {
            Cure(b);
            if (b == null || b.Popped || b.Steel) return;
            if (b.CanBeHit) World.WeaponHit(b);
            else World.OnHitBubble?.Invoke(b);
        }

        void HitSick(Roller r)
        {
            Cure(r);
            if (r == null || r.Popped || r.Steel) return;
            World.WeaponHit(r);
        }

        void Cure(Bubble b)
        {
            if (b == null) return;
            sickBubbles.Remove(b);
            Tint(b, false);
            if (Wobbles(b)) b.VisualScale = Vector2.one;
        }

        void Cure(Roller r)
        {
            if (r == null) return;
            sickRollers.Remove(r);
            Tint(r, false);
        }

        /// <summary>Sinkers and Clingers squash themselves through VisualScale, so they are left alone.</summary>
        static bool Wobbles(Bubble b) => b.Kind != BubbleKind.Sinker && b.Kind != BubbleKind.Clinger;

        static void Wobble(Bubble b, float left)
        {
            if (!Wobbles(b)) return;
            float w = 0.06f * Mathf.Sin(left * 30f);
            b.VisualScale = new Vector2(1f + w, 1f - w);
        }

        static void Tint(Component c, bool sick)
        {
            if (c == null) return;
            var sr = c.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = sick ? SickTint : Color.white;
        }

        // ------------------------------------------------------------------ cleanup

        /// <summary>Stage start or stage clear: flasks and clouds vanish, sick things get well.</summary>
        public void Clear()
        {
            foreach (var f in flasks) Kill(f.sr);
            flasks.Clear();
            foreach (var c in clouds) Kill(c.sr);
            clouds.Clear();
            foreach (var b in new List<Bubble>(sickBubbles.Keys)) Cure(b);
            foreach (var r in new List<Roller>(sickRollers.Keys)) Cure(r);
            sickBubbles.Clear();
            sickRollers.Clear();
        }

        SpriteRenderer Renderer(string name, Layer layer, int order, Sprite sprite)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            Sorting.Apply(sr, layer, order);
            Game.ApplySpriteMaterial(sr);
            return sr;
        }

        static void Kill(SpriteRenderer sr)
        {
            if (sr == null) return;
            if (Application.isPlaying) Object.Destroy(sr.gameObject);
            else Object.DestroyImmediate(sr.gameObject);
        }
    }

    /// <summary>The flask in flight and the olive cloud: dense puffs, a lighter fleck here and there, a dithered edge.</summary>
    static class GasArt
    {
        static Sprite flask, cloud;
        static int cloudRadius = -1;

        public static Sprite Flask
        {
            get
            {
                if (flask != null) return flask;
                var c = WeaponArt.Outlined(GasFlask.FlaskRows);
                return flask = c.ToSprite("GasFlaskThrown", new Vector2(c.Width * 0.5f, c.Height * 0.5f), Tuning.I.pixelsPerUnit);
            }
        }

        public static Sprite Cloud
        {
            get
            {
                int r = Mathf.Max(2, Mathf.RoundToInt(Tuning.I.gasRadius * Tuning.I.pixelsPerUnit));
                if (cloud != null && cloudRadius == r) return cloud;
                cloudRadius = r;
                int size = 2 * r + 4;
                var c = new PixelCanvas(size, size);
                float mid = size * 0.5f;
                // Nine puffs around the middle and one in it, like the sheet's cloud.
                var puffs = new List<Vector3>();
                var rng = new System.Random(3);
                for (int i = 0; i < 9; i++)
                {
                    float a = i / 9f * Mathf.PI * 2f;
                    puffs.Add(new Vector3(mid + Mathf.Cos(a) * r * 0.45f, mid + Mathf.Sin(a) * r * 0.36f, r * (0.45f + (float)rng.NextDouble() * 0.2f)));
                }
                puffs.Add(new Vector3(mid, mid, r * 0.6f));
                var light = Color32.Lerp(Palette.Gas, Palette.Cream, 0.35f);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float m = 9f;
                    foreach (var p in puffs) m = Mathf.Min(m, Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), p) / p.z);
                    if (m > 1f) continue;
                    if (m > 0.86f) { if ((x + y) % 2 == 0) c.Set(x, y, Palette.GasDark); }
                    else if (m > 0.6f) { if ((x + y) % 2 == 0) c.Set(x, y, Palette.Gas); }
                    else if (!(x % 2 == 0 && y % 2 == 0)) c.Set(x, y, (x + y) % 5 == 0 ? light : (Color32)Palette.Gas);
                }
                return cloud = c.ToSprite("GasCloud" + r, new Vector2(mid, mid), Tuning.I.pixelsPerUnit);
            }
        }
    }

    public partial class Game
    {
        GasField gasField;

        /// <summary>The running game's Gas Flask field (null with no game running).</summary>
        public static GasField Gas => active?.GasFieldNow;

        GasField GasFieldNow
        {
            get
            {
                if (gasField != null) return gasField;
                var w = Weapons;
                gasField = new GasField(w, weaponRoot != null ? weaponRoot : world)
                {
                    OnBurst = at =>
                    {
                        fx.Sparkle(at, Palette.Gas);
                        Sfx.Play(Sound.Bounce, 2.6f, 0.35f); // placeholder glassy tick
                    },
                    OnSpeck = at => fx.Sparkle(at, Palette.Gas),
                    OnScorch = at =>
                    {
                        fx.Sparkle(at, Palette.SparkCore);
                        fx.MuzzleFlash(at);
                        Sfx.Play(Sound.Fire, 0.6f, 0.7f); // placeholder whoomph
                    },
                };
                return gasField;
            }
        }

        /// <summary>Called from TickWeapons after the shots: flasks fly, clouds drift, sick things pop.</summary>
        void TickGas(float dt) => gasField?.Tick(dt);

        /// <summary>Stage start and stage clear: no flask, cloud or sickness carries over.</summary>
        void ClearGas() => gasField?.Clear();
    }

    public partial class Tuning
    {
        [Header("Weapon: Gas Flask")]
        [Tooltip("Flasks in a Gas Flask crate.")]
        public int gasAmmo = 3;
        [Tooltip("How far above the weapon tip a flask bursts, in units, if it touches nothing on the way.")]
        public float gasLobHeight = 5f;
        [Tooltip("Speed of a thrown flask, units per second.")]
        public float gasFlaskSpeed = 14f;
        [Tooltip("Seconds between throws.")]
        public float gasCooldown = 0.4f;
        [Tooltip("Cloud radius in units.")]
        public float gasRadius = 2.5f;
        [Tooltip("Seconds a cloud lasts.")]
        public float gasCloudTime = 4f;
        [Tooltip("Seconds over which a cloud fades out at the end of its life.")]
        public float gasFadeTime = 1f;
        [Tooltip("How fast a cloud rises, units per second (a cloud from a down throw stays on the floor).")]
        public float gasRise = 0.4f;
        [Tooltip("Seconds a sick bubble or rolling balloon lives before it is hit once.")]
        public float gasSickTime = 1.2f;
        [Tooltip("Boss health a cloud takes per second while the boss is inside it.")]
        public float gasBossDamage = 1f;
        [Tooltip("Most boss health one flask's cloud can take.")]
        public float gasBossMax = 3f;
    }
}
