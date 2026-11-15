using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>
    /// W11 Vee Harpoon, a harpoon head: for twelve volleys every fire press launches two straight slanted cables, veeAngle
    /// degrees either side of straight up, one from each hand. The cables are the game's own harpoons in their angled mode
    /// (Harpoon.FireAngled), so steel, ceramic, Ghosts, rolling balloons, the boss and run upgrades (speed, width, hold
    /// time) work exactly as for the plain harpoon. Each tip stops at the first wall, ceiling or tile and breaks a
    /// breakable one. Falling with Down held both go down and out at the same angle. The next volley waits until both
    /// cables of the last one are gone; Twin Cable picks allow one more volley in the air each, as for the Double Harpoon.
    /// A volley hurts a boss once however many of its cables touch (Harpoon.Volley).
    /// </summary>
    [Preserve]
    public sealed class VeeHarpoon : WeaponSpec
    {
        public override string Id => "vee";
        public override string Name => "Vee Harpoon";
        public override WeaponSlot Slot => WeaponSlot.Harpoon;
        public override WeaponTier Tier => WeaponTier.Common;
        public override int MaxAmmo => Tuning.I.veeAmmo;
        public override int Order => 11;

        static readonly string[] Icon11 =
        {
            "LL.......LL",
            "LS.......SL",
            ".S.L...L.S.",
            "..S.....S..",
            "..L.....L..",
            "...S...S...",
            "...L...L...",
            "....S.S....",
            "....L.L....",
            ".....D.....",
        };

        public override string[] IconRows => Icon11;
        public override WeaponBehaviour CreateBehaviour() => new Behaviour();

        /// <summary>Pixels either side of Wick's middle where the two cables leave: his two raised hands.</summary>
        public const int HandPixels = 6;

        /// <summary>The direction of one cable: side -1 is the left one, +1 the right. Down points them at the floor.</summary>
        public static Vector2 Direction(int side, bool down)
        {
            float a = Tuning.I.veeAngle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(a) * Mathf.Sign(side), down ? -Mathf.Cos(a) : Mathf.Cos(a));
        }

        internal sealed class Behaviour : WeaponBehaviour
        {
            /// <summary>Volleys this head fired that may still be in the air.</summary>
            internal readonly List<Volley> volleys = new List<Volley>();
            readonly List<int> sides = new List<int>(2);

            internal sealed class Volley
            {
                public readonly HarpoonVolley tag = new HarpoonVolley();
                public readonly List<Harpoon> cables = new List<Harpoon>(2);

                /// <summary>A cable still belongs to this volley while it flies (a hidden harpoon may be reused later).</summary>
                public bool InAir
                {
                    get
                    {
                        foreach (var h in cables)
                            if (h != null && h.Active && h.Volley == tag) return true;
                        return false;
                    }
                }
            }

            internal int VolleysInAir
            {
                get
                {
                    volleys.RemoveAll(v => !v.InAir);
                    return volleys.Count;
                }
            }

            public override bool VolleyReady(WeaponContext ctx) => VolleysInAir < DoubleHarpoon.MaxVolleys(ctx.Run);

            public override bool FireVolley(WeaponContext ctx)
            {
                if (VolleysInAir >= DoubleHarpoon.MaxVolleys(ctx.Run)) return false;

                var t = Tuning.I;
                float centre = ctx.Wick != null ? ctx.Wick.Hitbox.center.x : ctx.Muzzle.x;
                float limit = t.HalfWidth - t.Pixel;
                var map = Game.Map;
                sides.Clear();
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = Mathf.Clamp(centre + side * HandPixels * t.Pixel, -limit, limit);
                    if (map != null && map.IsSolid(StageMap.Col(x), StageMap.Row(ctx.Muzzle.y))) continue; // hand inside a wall or block
                    sides.Add(side);
                }
                if (sides.Count == 0) return false;

                var free = FreeHarpoons(ctx, sides.Count);
                if (free.Count < sides.Count) return false;

                var volley = new Volley();
                volley.tag.BossDamage = t.veeBossDamage;
                for (int i = 0; i < sides.Count; i++)
                {
                    var h = free[i];
                    float x = Mathf.Clamp(centre + sides[i] * HandPixels * t.Pixel, -limit, limit);
                    h.FireAngled(new Vector2(x, ctx.Muzzle.y), Direction(sides[i], ctx.ShootDown));
                    h.Volley = volley.tag;
                    volley.cables.Add(h);
                }
                volleys.Add(volley);
                return true;
            }

            static List<Harpoon> FreeHarpoons(WeaponContext ctx, int need)
            {
                var free = new List<Harpoon>(need);
                var all = ctx.Harpoons();
                int active = 0;
                foreach (var h in all) if (h != null && h.Active) active++;
                if (all.Count - active < need) all = ctx.Harpoons(active + need);
                foreach (var h in all)
                {
                    if (h == null || h.Active) continue;
                    free.Add(h);
                    if (free.Count == need) break;
                }
                return free;
            }

            public override void Dispose() => volleys.Clear(); // cables in the air fly on until their tips land
        }
    }

    public partial class Tuning
    {
        [Header("Weapon: Vee Harpoon")]
        [Tooltip("Volleys in a full Vee Harpoon crate. Each fire press while it is held is one volley of two slanted cables.")]
        public int veeAmmo = 12;
        [Tooltip("Degrees each Vee Harpoon cable leans away from straight up (or straight down for a down shot). Sensible 20 to 45.")]
        [Range(5f, 75f)] public float veeAngle = 30f;
        [Tooltip("Boss health one Vee Harpoon volley takes, however many of its cables touch the boss.")]
        public int veeBossDamage = 1;
    }
}
