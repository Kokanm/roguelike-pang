using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Decides which pops drop a weapon crate in one stage. Seeded from the run seed and the stage, so the same run
    /// drops the same crates for the same pops (and tests repeat). Rules from Tuning "Weapons": a chance per pop, at
    /// most maxCratesPerStage a stage, none in the first crateFreeStages stages of world 1, none while a crate is on
    /// screen, and a tier drawn by weaponTierWeights from the tiers already open in this world.
    /// </summary>
    public class WeaponDropper
    {
        const int Salt = 0x5EA90;

        readonly System.Random rng;
        readonly int world;
        readonly bool allowed;
        readonly IReadOnlyList<WeaponSpec> table;

        public int Dropped { get; private set; }

        public WeaponDropper(int seed, int stage, IReadOnlyList<WeaponSpec> table = null)
        {
            rng = new System.Random(unchecked(seed * 7919 + stage * 104729 + Salt));
            world = Run.WorldOf(stage);
            allowed = StageAllowsCrates(stage);
            this.table = table ?? WeaponSpec.All;
        }

        /// <summary>The first crateFreeStages stages of world 1 have no crates.</summary>
        public static bool StageAllowsCrates(int stage) =>
            !(Run.WorldOf(stage) == 1 && Run.StageInWorldOf(stage) <= Tuning.I.crateFreeStages);

        /// <summary>Whether a tier can drop in this world at all.</summary>
        public static bool TierOpen(WeaponTier tier, int world)
        {
            var t = Tuning.I;
            switch (tier)
            {
                case WeaponTier.Uncommon: return world >= t.uncommonFromWorld;
                case WeaponTier.Rare:
                case WeaponTier.VeryRare: return world >= t.rareFromWorld;
                default: return true;
            }
        }

        /// <summary>
        /// One pop (or broken block) with this drop chance. Returns the weapon of the crate to drop, or null. With no
        /// droppable weapon in the table it always returns null.
        /// </summary>
        public WeaponSpec Roll(float chance, bool crateOnScreen)
        {
            // The roll is taken every time, so whether one pop drops does not shift the luck of the next.
            double roll = rng.NextDouble();
            double pick = rng.NextDouble();
            if (!allowed || crateOnScreen || Dropped >= Tuning.I.maxCratesPerStage || roll >= chance) return null;
            var spec = Pick(pick);
            if (spec != null) Dropped++;
            return spec;
        }

        /// <summary>
        /// A weapon for a crate that always drops (a boss's phase break): the same tier and weapon draw as Roll, but
        /// with no chance, no cap per stage and no limit of one crate on screen.
        /// </summary>
        public WeaponSpec RollGuaranteed()
        {
            rng.NextDouble(); // keep the same two draws per roll as Roll
            var spec = Pick(rng.NextDouble());
            if (spec != null) Dropped++;
            return spec;
        }

        /// <summary>A weapon for a draw in [0, 1): a tier by weight among the open tiers that hold a weapon, then a weapon in it.</summary>
        WeaponSpec Pick(double draw)
        {
            var weights = Tuning.I.weaponTierWeights;
            var byTier = new List<WeaponSpec>[4];
            float total = 0f;
            for (int tier = 0; tier < 4; tier++)
            {
                byTier[tier] = new List<WeaponSpec>();
                if (!TierOpen((WeaponTier)tier, world)) continue;
                foreach (var w in table)
                    if (w.Drops && (int)w.Tier == tier) byTier[tier].Add(w);
                if (byTier[tier].Count > 0) total += Weight(weights, tier);
            }
            if (total <= 0f) return null;

            float at = (float)draw * total;
            for (int tier = 0; tier < 4; tier++)
            {
                if (byTier[tier].Count == 0) continue;
                float wgt = Weight(weights, tier);
                if (at < wgt || tier == 3 || LastNonEmpty(byTier, tier))
                {
                    // Spread the leftover of the draw over the weapons of this tier.
                    float inTier = wgt > 0f ? Mathf.Clamp01(at / wgt) : 0f;
                    var list = byTier[tier];
                    return list[Mathf.Min(list.Count - 1, (int)(inTier * list.Count))];
                }
                at -= wgt;
            }
            return null;
        }

        static float Weight(float[] weights, int tier) => weights != null && tier < weights.Length ? Mathf.Max(0f, weights[tier]) : 0f;

        static bool LastNonEmpty(List<WeaponSpec>[] byTier, int tier)
        {
            for (int t = tier + 1; t < byTier.Length; t++) if (byTier[t].Count > 0) return false;
            return true;
        }
    }
}
