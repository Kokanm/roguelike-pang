namespace Bubblewright
{
    /// <summary>
    /// The two weapon slots of a run. Weapon: a gun or gadget fired with the weapon button. Head: a harpoon head that
    /// changes what the fire button shoots. Wick holds at most one of each. Ammo carries over between stages and is
    /// saved with the run at the start of every stage.
    /// </summary>
    public partial class Run
    {
        public WeaponSpec Weapon;
        public int WeaponAmmo;
        public WeaponSpec Head;
        public int HeadAmmo;

        public WeaponSpec HeldIn(WeaponSlot slot) => slot == WeaponSlot.Harpoon ? Head : Weapon;
        public int AmmoIn(WeaponSlot slot) => slot == WeaponSlot.Harpoon ? HeadAmmo : WeaponAmmo;

        /// <summary>
        /// Takes a crate. The same weapon adds a full load, up to its AmmoCap. A different weapon in the same slot
        /// replaces it, ammo and all. Returns false, changing nothing, for null or a second copy the spec refuses.
        /// </summary>
        public bool GiveWeapon(WeaponSpec spec)
        {
            if (spec == null) return false;
            var current = HeldIn(spec.Slot);
            int ammo;
            if (current != null && current.Id == spec.Id)
            {
                if (!spec.AllowSecondCopy) return false;
                ammo = System.Math.Min(spec.AmmoCap, AmmoIn(spec.Slot) + spec.MaxAmmo);
            }
            else ammo = spec.MaxAmmo;
            SetSlot(spec.Slot, ammo > 0 ? spec : null, ammo);
            return true;
        }

        /// <summary>Spends ammo from a slot. False, spending nothing, when the slot is empty or holds less. At 0 the slot empties.</summary>
        public bool SpendAmmo(WeaponSlot slot, int amount = 1)
        {
            if (amount <= 0) return true;
            var spec = HeldIn(slot);
            int ammo = AmmoIn(slot);
            if (spec == null || ammo < amount) return false;
            ammo -= amount;
            SetSlot(slot, ammo > 0 ? spec : null, ammo);
            return true;
        }

        public void ClearSlot(WeaponSlot slot) => SetSlot(slot, null, 0);

        void SetSlot(WeaponSlot slot, WeaponSpec spec, int ammo)
        {
            if (spec == null) ammo = 0;
            if (slot == WeaponSlot.Harpoon) { Head = spec; HeadAmmo = ammo; }
            else { Weapon = spec; WeaponAmmo = ammo; }
        }

        /// <summary>Restores a slot from a save: unknown ids, ids for the other slot and empty ammo load as empty.</summary>
        void LoadSlot(WeaponSlot slot, string id, int ammo)
        {
            var spec = WeaponSpec.ById(id);
            if (spec == null || spec.Slot != slot || ammo <= 0) { ClearSlot(slot); return; }
            SetSlot(slot, spec, System.Math.Min(ammo, spec.AmmoCap));
        }

        void SaveWeapons(SaveData d)
        {
            d.weapon = Weapon?.Id;
            d.weaponAmmo = Weapon != null ? WeaponAmmo : 0;
            d.head = Head?.Id;
            d.headAmmo = Head != null ? HeadAmmo : 0;
        }

        void LoadWeapons(SaveData d)
        {
            LoadSlot(WeaponSlot.Weapon, d.weapon, d.weaponAmmo);
            LoadSlot(WeaponSlot.Harpoon, d.head, d.headAmmo);
        }
    }
}
