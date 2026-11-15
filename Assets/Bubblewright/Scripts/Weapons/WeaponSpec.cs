using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>Which slot a weapon goes in. Weapon: fired with the weapon button. Harpoon: a head that changes what the fire button shoots.</summary>
    public enum WeaponSlot { Weapon, Harpoon }

    /// <summary>How rare a weapon is in crates (Tuning.weaponTierWeights).</summary>
    public enum WeaponTier { Common, Uncommon, Rare, VeryRare }

    /// <summary>
    /// One temporary weapon. Each weapon is its own sealed subclass in its own file under Scripts/Weapons/, marked
    /// [Preserve], with its numbers in a partial Tuning next to it. The table (All) finds every subclass by itself,
    /// so adding a weapon never edits a shared list and weapons built at the same time do not conflict.
    /// Pixel maps use the sheet's letters: S steel, L light steel, D dark steel, R red, Y yellow, W white, C cream,
    /// O olive, o dark olive, M mint, K ink, '.' empty. The top row comes first. The ink outline is added for you.
    /// </summary>
    public abstract class WeaponSpec
    {
        /// <summary>Short lower-case id, saved in the run save. Never change it once shipped.</summary>
        public abstract string Id { get; }
        public abstract string Name { get; }
        public virtual WeaponSlot Slot => WeaponSlot.Weapon;
        public virtual WeaponTier Tier => WeaponTier.Common;
        /// <summary>A full crate. Read it from Tuning so live tuning works.</summary>
        public abstract int MaxAmmo { get; }
        /// <summary>False keeps it out of crates (the debug test weapon).</summary>
        public virtual bool Drops => true;
        /// <summary>Place in All and in the debug cycle: the weapon's number on the sheet (W01 is 1).</summary>
        public virtual int Order => 100;
        /// <summary>False refuses a second crate while one is held (Lamp Lance): the crate stays on the floor.</summary>
        public virtual bool AllowSecondCopy => true;
        /// <summary>The 11 px wide icon used in the crate, on the button and in the HUD.</summary>
        public abstract string[] IconRows { get; }
        /// <summary>What Wick holds up in place of the launcher. Null keeps the launcher.</summary>
        public virtual string[] HeldRows => null;

        /// <summary>A fresh behaviour each time the weapon lands in a slot. It lives until the slot empties or changes.</summary>
        public abstract WeaponBehaviour CreateBehaviour();

        public Sprite Icon => WeaponArt.Icon(this);
        public Texture2D IconTexture => WeaponArt.IconTexture(this);

        /// <summary>Most ammo it can hold after topping up: a full crate times Tuning.weaponTopUpScale, rounded down.</summary>
        public int AmmoCap => Mathf.Max(MaxAmmo, Mathf.FloorToInt(MaxAmmo * Tuning.I.weaponTopUpScale + 0.0001f));

        static WeaponSpec[] all;

        /// <summary>Every weapon in the game, ordered by Order then Id.</summary>
        public static IReadOnlyList<WeaponSpec> All => all ??= Discover();

        /// <summary>The weapon with this id, or null for a missing or unknown id.</summary>
        public static WeaponSpec ById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var w in All) if (w.Id == id) return w;
            return null;
        }

        static WeaponSpec[] Discover()
        {
            var list = new List<WeaponSpec>();
            foreach (var type in typeof(WeaponSpec).Assembly.GetTypes())
            {
                if (type.IsAbstract || !typeof(WeaponSpec).IsAssignableFrom(type)) continue;
                if (type.GetConstructor(Type.EmptyTypes) == null) continue;
                list.Add((WeaponSpec)Activator.CreateInstance(type));
            }
            list.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Id, b.Id));
            return list.ToArray();
        }
    }

    /// <summary>
    /// What a held weapon does. Game ticks it every Play frame through Game.Weapons.cs; all hits go through the
    /// helpers on WeaponWorld so steel, ceramic, Ghosts, bosses and tiles follow the same rules for every weapon.
    /// </summary>
    public abstract class WeaponBehaviour
    {
        /// <summary>A weapon-slot weapon: called every Play frame while held. Spend ammo with ctx.Spend.</summary>
        public virtual void Tick(WeaponContext ctx, float dt) { }

        /// <summary>
        /// A harpoon head: the fire button was pressed and a harpoon was free. Fire the head's volley and return true
        /// (one head ammo is spent for you), or return false to let the normal harpoon fire.
        /// </summary>
        public virtual bool FireVolley(WeaponContext ctx) => false;

        /// <summary>
        /// A harpoon head: may the fire button fire a volley now? False skips the press entirely (no normal harpoon,
        /// no flash). The default asks for a free normal harpoon (ctx.HarpoonFree), like the plain launcher.
        /// </summary>
        public virtual bool VolleyReady(WeaponContext ctx) => ctx.HarpoonFree;

        /// <summary>A weapon-slot weapon with nothing to fire at right now: the touch button is drawn dim (Longshot).</summary>
        public virtual bool Dimmed => false;

        /// <summary>The weapon left its slot (empty, replaced) or the stage is starting over. Remove anything it owns.</summary>
        public virtual void Dispose() { }
    }
}
