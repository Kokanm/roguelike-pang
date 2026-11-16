using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>
    /// W13 Lamplighter Torch: hold the weapon button for a jet of flame from the weapon tip. The flame is a cone
    /// torchLength long and torchCone degrees wide, pointing up (down when falling with Down held), cut short at the
    /// first solid tile. Everything hittable in it takes a hit, then is safe from the flame for torchTick seconds, so a
    /// ceramic bubble cracks and then breaks. Steel does not mind it, Scorch bubbles are immune, and Frost ice it touches
    /// melts. Fuel is whole seconds in the ammo number: a second is spent once it has fully burned, so a part-burned
    /// second is never lost and the saved run rounds the fuel up.
    /// </summary>
    [Preserve]
    public sealed class LamplighterTorch : WeaponSpec
    {
        public override string Id => "torch";
        public override string Name => "Lamplighter Torch";
        public override WeaponTier Tier => WeaponTier.Uncommon;
        public override int MaxAmmo => Mathf.Max(1, Tuning.I.torchFuel);
        public override int Order => 13;

        static readonly string[] Icon11 =
        {
            "........R..",
            ".......RYR.",
            "LLLLLL.RYYR",
            "SSSSSSDRWYR",
            "SSSSSS.RYYR",
            ".DS.DD.RYR.",
            ".DS.DD..R..",
            ".DD.DD.....",
        };

        static readonly string[] Hand =
        {
            ".LSD.",
            ".LSD.",
            ".LSD.",
            ".LSD.",
            "LSSSD",
            ".DDD.",
        };

        public override string[] IconRows => Icon11;
        public override string[] HeldRows => Hand;
        public override WeaponBehaviour CreateBehaviour() => new Behaviour();

        /// <summary>What one frame of flame did.</summary>
        public struct BurnResult
        {
            /// <summary>How far the flame reached along its aim this frame (torchLength, or less under a tile).</summary>
            public float length;
            public int bubbles, rollers, bullets;
            public bool boss;
            /// <summary>Frost ice spots melted.</summary>
            public int ice;
        }

        /// <summary>
        /// How far the flame reaches from the tip along aim: torchLength, cut short at the first solid tile (the floor,
        /// walls and ceiling count, since the map is solid outside the arena). A tip inside a tile reaches nothing.
        /// </summary>
        public static float Reach(StageMap map, Vector2 tip, Vector2 aim)
        {
            float max = Mathf.Max(0f, Tuning.I.torchLength);
            if (map == null) return max;
            aim = aim.sqrMagnitude > 1e-6f ? aim.normalized : Vector2.up;
            float floor = map.StoreyBase(tip.y);
            float ceil = map.StoreyCeiling(tip.y);
            const float step = 0.05f;
            for (float d = 0f; d <= max; d += step)
            {
                Vector2 p = tip + aim * d;
                if (p.y >= ceil || p.y < floor) return Mathf.Max(0f, d - step);
                int row = StageMap.Row(p.y);
                if (!map.RowInStorey(row, tip.y) || map.IsSolid(StageMap.Col(p.x), row)) return Mathf.Max(0f, d - step);
            }
            return max;
        }

        /// <summary>
        /// One frame of flame. Everything in the cone that is not cooling down (cooldowns, by thing) and has no solid tile
        /// between it and the tip takes a hit and cools down for torchTick seconds. Things born since the last frame
        /// (children of a pop) start cooled down, so a split bubble is not burned again the very next frame. Steel, Scorch
        /// bubbles and faded Ghosts are left alone. Frost ice the flame touches melts.
        /// </summary>
        public static BurnResult Burn(WeaponWorld world, Vector2 tip, Vector2 aim, Dictionary<EntityId, float> cooldowns,
            HashSet<EntityId> newborn = null, List<WeaponTarget> scratch = null)
        {
            var t = Tuning.I;
            var result = new BurnResult();
            if (world == null) return result;
            aim = aim.sqrMagnitude > 1e-6f ? aim.normalized : Vector2.up;
            var map = world.Map;
            result.length = Reach(map, tip, aim);
            if (result.length <= 0f) return result;

            scratch = world.InSector(tip, result.length, aim, t.torchCone, scratch);
            foreach (var target in scratch)
            {
                EntityId id = IdOf(target);
                if (id == EntityId.None) continue;
                if (cooldowns != null && cooldowns.ContainsKey(id)) continue;
                if (newborn != null && newborn.Contains(id))
                {
                    if (cooldowns != null) cooldowns[id] = t.torchTick;
                    continue;
                }
                if (!Burns(target)) continue;
                if (!TrimmerBlade.Reaches(map, tip, NearPoint(target, tip), -1, -1)) continue;
                if (!world.Hit(target, t.torchBossDamage)) continue;
                if (cooldowns != null) cooldowns[id] = t.torchTick;
                switch (target.kind)
                {
                    case TargetKind.Bubble: result.bubbles++; break;
                    case TargetKind.Roller: result.rollers++; break;
                    case TargetKind.Bullet: result.bullets++; break;
                    case TargetKind.Boss: result.boss = true; break;
                }
            }
            result.ice = MeltIce(map, tip, aim, result.length);
            return result;
        }

        /// <summary>Steel shrugs the flame off (no spark, no sound) and Scorch bubbles are made of it.</summary>
        static bool Burns(WeaponTarget target)
        {
            switch (target.kind)
            {
                case TargetKind.Bubble:
                    var b = target.bubble;
                    return b != null && !b.Popped && !b.Steel && b.Kind != BubbleKind.Scorch;
                case TargetKind.Roller:
                    return target.roller != null && !target.roller.Popped && !target.roller.Steel;
                default:
                    return true;
            }
        }

        public static EntityId IdOf(WeaponTarget target)
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

        /// <summary>The point of a target nearest the tip: where the flame first licks it.</summary>
        static Vector2 NearPoint(WeaponTarget target, Vector2 tip)
        {
            float radius = target.kind switch
            {
                TargetKind.Bubble => target.bubble.Radius,
                TargetKind.Roller => target.roller.Radius,
                TargetKind.Bullet => target.bullet.Radius,
                _ => 0f, // the boss position is already the closest point of its body
            };
            Vector2 to = target.position - tip;
            float d = to.magnitude;
            if (d <= radius || d < 1e-5f) return tip;
            return tip + to * ((d - radius) / d);
        }

        /// <summary>
        /// Melts every Frost ice spot the cone touches: the tile cells it reaches into (a hair past where it stops, so
        /// the top of the tile it lands on counts) and the ground under it when it reaches the floor.
        /// </summary>
        static int MeltIce(StageMap map, Vector2 tip, Vector2 aim, float length)
        {
            var ice = Game.Ice;
            if (ice == null || ice.Count == 0) return 0;
            float half = Mathf.Max(0f, Tuning.I.torchCone) * 0.5f;
            float reach = length + 0.1f;
            var cells = new HashSet<Vector2Int>();
            var ground = new HashSet<int>();
            for (float d = 0f; d <= reach + 1e-4f; d += 0.1f)
            {
                // Enough rays that neighbouring samples are under a tenth of a unit apart at this distance.
                int rays = Mathf.Max(1, Mathf.CeilToInt(2f * half * Mathf.Deg2Rad * d / 0.1f));
                for (int k = 0; k <= rays; k++)
                {
                    float a = rays == 0 ? 0f : -half + 2f * half * k / rays;
                    Vector2 p = tip + (Vector2)(Quaternion.Euler(0f, 0f, a) * aim) * d;
                    if (p.y < 0f)
                    {
                        if (p.y > -0.15f) ground.Add(StageMap.Col(p.x));
                        continue;
                    }
                    cells.Add(new Vector2Int(StageMap.Col(p.x), StageMap.Row(p.y)));
                }
            }
            int melted = 0;
            foreach (var c in cells) melted += ice.Melt(map, c.x, c.x, c.y, c.y, false);
            foreach (int col in ground) melted += ice.Melt(map, col, col, 1, 0, true);
            return melted;
        }

        public sealed class Behaviour : WeaponBehaviour
        {
            /// <summary>Seconds burned out of the current second of fuel. A second is spent from the ammo once it is full.</summary>
            public float Burned { get; private set; }
            /// <summary>The button is held and the flame is out this frame.</summary>
            public bool Burning { get; private set; }
            /// <summary>What the flame did on the last frame it was out.</summary>
            public BurnResult Last { get; private set; }

            readonly Dictionary<EntityId, float> cooldowns = new Dictionary<EntityId, float>();
            readonly List<EntityId> cooling = new List<EntityId>();
            HashSet<EntityId> seen = new HashSet<EntityId>(), now = new HashSet<EntityId>();
            readonly HashSet<EntityId> newborn = new HashSet<EntityId>();
            readonly List<WeaponTarget> scratch = new List<WeaponTarget>();
            Wick wick;
            TorchFlameView view;
            float fuel = -1f, crackle;

            /// <summary>Fuel left as a fraction of a full crate, for the touch button's bar.</summary>
            public override float Gauge => fuel < 0f ? -1f : Mathf.Clamp01(fuel / Mathf.Max(1, Tuning.I.torchFuel));

            public override void Tick(WeaponContext ctx, float dt)
            {
                if (ctx.Wick != null && ctx.Wick != wick)
                {
                    SetScales(1f, 1f);
                    wick = ctx.Wick;
                }

                int ammo = ctx.Run != null ? ctx.Run.WeaponAmmo : 0;
                fuel = Mathf.Max(0f, ammo - Burned);

                cooling.Clear();
                foreach (var k in cooldowns) cooling.Add(k.Key);
                foreach (var id in cooling)
                {
                    float left = cooldowns[id] - dt;
                    if (left <= 0f) cooldowns.Remove(id);
                    else cooldowns[id] = left;
                }

                if (!ctx.Held || !ctx.CanFire || ammo <= 0)
                {
                    Stop();
                    return;
                }

                if (!Burning) Remember(ctx.World, seen); // everything already here is fair game on the first frame
                Burning = true;
                SetScales(Tuning.I.torchWalkScale, ctx.ShootDown ? Tuning.I.torchDownFallScale : 1f);

                FindNewborn(ctx.World);
                Last = Burn(ctx.World, ctx.Muzzle, ctx.Up, cooldowns, newborn, scratch);

                if (ctx.Wick != null)
                {
                    view ??= TorchFlameView.Create(ctx.Wick.transform.parent);
                    view.Show(ctx.Muzzle, ctx.ShootDown, Last.length, dt);
                }
                crackle -= dt;
                if (crackle <= 0f)
                {
                    crackle = 0.12f;
                    Sfx.Play(Sound.Skid, 0.55f + ctx.Random01() * 0.15f, 0.35f); // placeholder roar of the flame
                }

                // Spend a second of fuel once it has fully burned; the last one empties the slot.
                Burned += dt;
                while (Burned >= 1f - 1e-4f)
                {
                    Burned = Mathf.Max(0f, Burned - 1f);
                    if (!ctx.Spend()) break;
                }
                fuel = Mathf.Max(0f, (ctx.Run != null ? ctx.Run.WeaponAmmo : 0) - Burned);
            }

            /// <summary>Things that were not in the world last frame were born from a pop: they start cooled down.</summary>
            void FindNewborn(WeaponWorld world)
            {
                Remember(world, now);
                newborn.Clear();
                foreach (var id in now) if (!seen.Contains(id)) newborn.Add(id);
                (seen, now) = (now, seen);
            }

            static void Remember(WeaponWorld world, HashSet<EntityId> into)
            {
                into.Clear();
                if (world == null) return;
                foreach (var b in world.Bubbles) if (b != null) into.Add(b.GetEntityId());
                foreach (var r in world.Rollers) if (r != null) into.Add(r.GetEntityId());
                if (world.Bullets != null) foreach (var bl in world.Bullets) if (bl != null) into.Add(bl.GetEntityId());
                var boss = world.LiveBoss;
                if (boss != null) into.Add(boss.GetEntityId());
            }

            void Stop()
            {
                Burning = false;
                SetScales(1f, 1f);
                view?.Hide();
            }

            void SetScales(float walk, float fall)
            {
                if (wick == null) return;
                wick.WeaponWalkScale = walk;
                wick.WeaponFallScale = fall;
            }

            public override void Dispose()
            {
                Stop();
                view?.Destroy();
                view = null;
                cooldowns.Clear();
                Burned = 0f;
            }
        }
    }

    /// <summary>The flame: a cone of red with a yellow body and a white core at the nozzle, three frames that flicker.</summary>
    sealed class TorchFlameView
    {
        const int Frames = 3;
        const float FrameTime = 0.06f;

        static readonly Dictionary<int, Sprite[]> cache = new Dictionary<int, Sprite[]>();
        static float cacheCone = -1f;

        GameObject go;
        SpriteRenderer sr;
        float time;

        public static TorchFlameView Create(Transform parent)
        {
            var v = new TorchFlameView { go = new GameObject("TorchFlame") };
            if (parent != null) v.go.transform.SetParent(parent, false);
            v.sr = v.go.AddComponent<SpriteRenderer>();
            Sorting.Apply(v.sr, Layer.Shots, 2);
            Game.ApplySpriteMaterial(v.sr);
            v.sr.enabled = false;
            return v;
        }

        public void Show(Vector2 tip, bool down, float length, float dt)
        {
            if (go == null) return;
            int px = Mathf.RoundToInt(length * Tuning.I.pixelsPerUnit);
            if (px < 2)
            {
                Hide();
                return;
            }
            time += dt;
            var f = FramesFor(px);
            sr.sprite = f[Mathf.FloorToInt(time / FrameTime) % Frames];
            sr.flipY = down;
            go.transform.position = tip;
            sr.enabled = true;
        }

        public void Hide()
        {
            if (sr != null) sr.enabled = false;
        }

        public void Destroy()
        {
            if (go == null) return;
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
            go = null;
        }

        /// <summary>Three flicker frames of a flame px pixels long, painted once per length and cone.</summary>
        static Sprite[] FramesFor(int px)
        {
            var t = Tuning.I;
            if (cacheCone != t.torchCone)
            {
                cache.Clear();
                cacheCone = t.torchCone;
            }
            if (cache.TryGetValue(px, out var frames) && frames[0] != null) return frames;
            float slope = Mathf.Tan(Mathf.Clamp(t.torchCone, 0f, 160f) * 0.5f * Mathf.Deg2Rad);
            int halfMax = Mathf.CeilToInt(1f + slope * px) + 2;
            int width = halfMax * 2 + 1;
            frames = new Sprite[Frames];
            for (int k = 0; k < Frames; k++)
            {
                var c = new PixelCanvas(width, px);
                var rng = new System.Random(17 + k * 31 + px);
                for (int y = 0; y < px; y++)
                {
                    float along = (float)y / px;
                    if (along > 0.9f && rng.NextDouble() > 0.5) continue; // ragged tip
                    float hw = 1f + slope * y;
                    float jitter = (float)(rng.NextDouble() - 0.5) * 3f * along;
                    float mid = halfMax + jitter;
                    Span(c, y, mid, hw, Palette.Spark);
                    if (along < 0.8f) Span(c, y, mid, hw * 0.6f, Palette.SparkCore);
                    if (along < 0.4f) Span(c, y, halfMax, Mathf.Max(0.5f, hw * 0.3f), new Color32(255, 255, 255, 255));
                }
                frames[k] = c.ToSprite("TorchFlame" + px + "_" + k, new Vector2(halfMax + 0.5f, 0f), Mathf.RoundToInt(t.pixelsPerUnit));
            }
            return cache[px] = frames;
        }

        static void Span(PixelCanvas c, int y, float mid, float hw, Color32 col)
        {
            int x0 = Mathf.RoundToInt(mid - hw), x1 = Mathf.RoundToInt(mid + hw);
            for (int x = x0; x <= x1; x++) c.Set(x, y, col);
        }
    }

    public partial class Tuning
    {
        [Header("Weapon: Lamplighter Torch")]
        [Tooltip("Seconds of flame in a full Lamplighter Torch crate. Shown as the ammo number.")]
        public int torchFuel = 4;
        [Tooltip("Units the torch flame reaches from the weapon tip, cut short at the first solid tile.")]
        public float torchLength = 4f;
        [Tooltip("Degrees wide the torch flame is at its far end.")]
        public float torchCone = 40f;
        [Tooltip("Seconds a thing the torch flame hit is safe from it before the next hit (ceramic: each one is a hit).")]
        public float torchTick = 0.3f;
        [Tooltip("Wick's walking speed while the torch is firing, as a fraction of normal.")]
        [Range(0f, 1f)] public float torchWalkScale = 0.8f;
        [Tooltip("Fired down in the air, his fall speed is multiplied by this while the flame is out.")]
        [Range(0f, 1f)] public float torchDownFallScale = 0.8f;
        [Tooltip("Boss health one torch hit takes. Hits come every torchTick, so 0.5 every 0.3 s is 1 health per 0.6 s.")]
        public float torchBossDamage = 0.5f;
    }
}
