using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Everything a held weapon needs for one frame, filled in by Game.Weapons.cs. One object is reused every frame,
    /// so do not keep it between frames.
    /// </summary>
    public class WeaponContext
    {
        /// <summary>The weapon button went down this frame (or, for a head, the fire button).</summary>
        public bool Pressed;
        /// <summary>The weapon button is held.</summary>
        public bool Held;
        /// <summary>Falling with Down held: weapons fire straight down from under his feet, like the harpoon.</summary>
        public bool ShootDown;
        /// <summary>Wick is in the air (jumping or falling), not on the ground or a ladder.</summary>
        public bool InAir;
        /// <summary>False when the muzzle is inside a solid tile: nothing may fire.</summary>
        public bool CanFire;
        /// <summary>Where shots leave: Wick.LauncherTip, or Wick.DownShotStart when ShootDown.</summary>
        public Vector2 Muzzle;
        /// <summary>(0, 1), or (0, -1) when ShootDown.</summary>
        public Vector2 Up;

        public WeaponSpec Spec;
        public WeaponWorld World;
        public Wick Wick;
        public Run Run;
        /// <summary>Seeded from the run seed and the stage, so a weapon's randomness repeats in tests and replays.</summary>
        public System.Random Rng;

        /// <summary>For a head: one of the normal harpoons (the first run.Shots) is free to fire.</summary>
        public bool HarpoonFree;

        internal System.Func<int, bool> spend;
        internal System.Func<int, System.Collections.Generic.IReadOnlyList<Harpoon>> harpoons;
        internal System.Action flash;

        /// <summary>Spends ammo from this weapon's slot. False (and nothing spent) when there is not that much left. The slot empties at 0.</summary>
        public bool Spend(int amount = 1) => spend != null && spend(amount);

        /// <summary>
        /// The game's harpoons, grown to at least count. A head that fires cables uses these, so the normal harpoon
        /// hit loops (bubbles, steel, ceramic, Ghosts, rolling balloons, the boss), run upgrades and stage cleanup all
        /// apply to its cables too. Empty when there is no game (tests set their own).
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<Harpoon> Harpoons(int count = 0) =>
            harpoons != null ? harpoons(count) : System.Array.Empty<Harpoon>();

        /// <summary>The shared firing feedback: launcher kick, muzzle flash and the fire sound.</summary>
        public void Flash() => flash?.Invoke();

        /// <summary>A random float in [0, 1) from Rng.</summary>
        public float Random01() => Rng != null ? (float)Rng.NextDouble() : 0.5f;
    }
}
