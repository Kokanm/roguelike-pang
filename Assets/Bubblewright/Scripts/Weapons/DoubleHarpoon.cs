using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>
    /// W10 Double Harpoon, a harpoon head: for twelve volleys every fire press launches two normal harpoons at once,
    /// side by side with the barbed tips touching (twinGapPixels apart), centred on Wick (Flint, who already has two
    /// cables, gets a third in the middle). The cables are
    /// the game's own harpoons, so steel, ceramic, Ghosts, rolling balloons, tiles and run upgrades work exactly as for
    /// the plain harpoon. The next volley waits until every cable of the last one is gone; Twin Cable picks allow one
    /// more volley in the air each. A volley hurts a boss once however many of its cables touch (Harpoon.Volley).
    /// </summary>
    [Preserve]
    public sealed class DoubleHarpoon : WeaponSpec
    {
        public override string Id => "twin";
        public override string Name => "Double Harpoon";
        public override WeaponSlot Slot => WeaponSlot.Harpoon;
        public override WeaponTier Tier => WeaponTier.Common;
        public override int MaxAmmo => Tuning.I.twinAmmo;
        public override int Order => 10;

        static readonly string[] Icon11 =
        {
            ".L.....L...",
            "LLL...LLL..",
            ".L.....L...",
            ".S.....S...",
            ".L.....L...",
            ".S.....S...",
            ".L.....L...",
            ".S.....S...",
        };

        public override string[] IconRows => Icon11;
        public override WeaponBehaviour CreateBehaviour() => new Behaviour();

        /// <summary>Cables in one volley: two, or three for a character who starts with two cables (Flint).</summary>
        public static int CablesPerVolley(Run run) =>
            run != null && run.Character != null && run.Character.StartShots >= 2 ? 3 : 2;

        /// <summary>Volleys allowed in the air at once: one, plus one for each Twin Cable pick.</summary>
        public static int MaxVolleys(Run run)
        {
            if (run == null) return 1;
            int start = run.Character != null ? run.Character.StartShots : 1;
            return Mathf.Max(1, 1 + run.Shots - start);
        }

        /// <summary>
        /// Where the cables leave, left to right: side by side, twinGapPixels apart (whole pixels, so the barbed tips
        /// touch with no gap), the group centred on centreX and kept inside the walls as a whole.
        /// </summary>
        public static List<float> CableXs(float centreX, int cables, List<float> into = null)
        {
            into ??= new List<float>();
            into.Clear();
            if (cables <= 0) return into;
            var t = Tuning.I;
            float px = t.Pixel;
            int step = Mathf.Max(1, t.twinGapPixels);
            int span = step * (cables - 1);
            float limit = t.HalfWidth - px;
            int left = Mathf.RoundToInt(centreX / px - span * 0.5f);
            left = Mathf.Clamp(left, Mathf.CeilToInt(-limit / px), Mathf.FloorToInt(limit / px) - span);
            for (int i = 0; i < cables; i++) into.Add((left + i * step) * px);
            return into;
        }

        internal sealed class Behaviour : WeaponBehaviour
        {
            /// <summary>Volleys this head fired that may still be in the air.</summary>
            internal readonly List<Volley> volleys = new List<Volley>();
            readonly List<float> xs = new List<float>();

            internal sealed class Volley
            {
                public readonly HarpoonVolley tag = new HarpoonVolley();
                public readonly List<Harpoon> cables = new List<Harpoon>();

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

            public override bool VolleyReady(WeaponContext ctx) => VolleysInAir < MaxVolleys(ctx.Run);

            public override bool FireVolley(WeaponContext ctx)
            {
                if (VolleysInAir >= MaxVolleys(ctx.Run)) return false;

                float centre = ctx.Wick != null ? ctx.Wick.Hitbox.center.x : ctx.Muzzle.x;
                CableXs(centre, CablesPerVolley(ctx.Run), xs);
                var map = Game.Map;
                for (int i = xs.Count - 1; i >= 0; i--)
                    if (map != null && map.IsSolid(StageMap.Col(xs[i]), StageMap.Row(ctx.Muzzle.y))) xs.RemoveAt(i); // inside a wall or block
                if (xs.Count == 0) return false;

                var free = FreeHarpoons(ctx, xs.Count);
                if (free.Count < xs.Count) return false;

                var volley = new Volley();
                volley.tag.BossDamage = Tuning.I.twinBossDamage;
                for (int i = 0; i < xs.Count; i++)
                {
                    var h = free[i];
                    var from = new Vector2(xs[i], ctx.Muzzle.y);
                    if (ctx.ShootDown) h.FireDown(from);
                    else h.Fire(from);
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

            public override void Dispose() => volleys.Clear(); // cables in the air fly on as normal harpoons
        }
    }

    public partial class Tuning
    {
        [Header("Weapon: Double Harpoon")]
        [Tooltip("Volleys in a full Double Harpoon crate. Each fire press while it is held is one volley.")]
        public int twinAmmo = 12;
        [Tooltip("Pixels between the Double Harpoon's cables, centred on Wick. 7 = the barbed tips touch, no gap. Flint gets a third cable in the middle.")]
        public int twinGapPixels = 7;
        [Tooltip("Boss health one Double Harpoon volley takes, however many of its cables touch the boss.")]
        public int twinBossDamage = 1;
    }
}
