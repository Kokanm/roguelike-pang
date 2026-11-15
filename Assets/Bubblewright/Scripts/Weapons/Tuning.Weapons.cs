using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// The numbers every weapon shares: crate drops, tiers, ammo top-up and the shared Shot.
    /// Each weapon keeps its own numbers in its own partial Tuning file next to its code (Weapons/&lt;Name&gt;.cs or
    /// Weapons/Tuning.&lt;Name&gt;.cs), under a header "Weapon: &lt;Name&gt;", so weapons built in parallel never edit the
    /// same lines. A new field takes its default from the code, so Tuning.asset only needs editing to change an
    /// existing default.
    /// </summary>
    public partial class Tuning
    {
        [Header("Weapons")]
        [Tooltip("Chance that a popped bubble drops a weapon crate.")]
        [Range(0f, 1f)] public float weaponDropChance = 0.05f;
        [Tooltip("Chance that a destroyed breakable block drops a weapon crate.")]
        [Range(0f, 1f)] public float breakableCrateChance = 0.10f;
        [Tooltip("Most weapon crates one stage can drop.")]
        public int maxCratesPerStage = 2;
        [Tooltip("The first this many stages of world 1 drop no crates.")]
        public int crateFreeStages = 2;
        [Tooltip("How often each tier is drawn: common, uncommon, rare, very rare. Tiers not open yet, or with no weapon in them, are skipped.")]
        public float[] weaponTierWeights = { 60f, 28f, 10f, 2f };
        [Tooltip("Uncommon weapons can drop from this world on.")]
        public int uncommonFromWorld = 2;
        [Tooltip("Rare and very rare weapons can drop from this world on.")]
        public int rareFromWorld = 3;
        [Tooltip("Seconds a weapon crate stays before it vanishes. It blinks for the last Weapon Crate Blink seconds.")]
        public float weaponCrateLife = 8f;
        [Tooltip("Seconds of blinking at the end of a crate's life.")]
        public float weaponCrateBlink = 2f;
        [Tooltip("Taking the weapon already held adds a full load, up to this many times a full load (rounded down).")]
        public float weaponTopUpScale = 1.5f;
        [Tooltip("Units per second of the shared spark bullet (Shot), unless a weapon gives its own speed.")]
        public float weaponShotSpeed = 36f;
        [Tooltip("Seconds the weapon name banner shows after a crate is taken.")]
        public float weaponBannerTime = 1f;

        [Header("Weapon: Test (debug only, never drops)")]
        [Tooltip("Shots in the debug test weapon.")]
        public int testWeaponAmmo = 20;
        [Tooltip("Boss health one test shot takes. Fractions add up across shots.")]
        public float testShotBossDamage = 0.5f;
    }
}
