using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>
    /// W12 Skewer Harpoon, a harpoon head: for ten shots every fire press launches one normal harpoon that does not stop
    /// at the first pop. Its cable stays up and pops up to skewerPops things (bubbles or rolling balloons) before it goes,
    /// or it goes earlier by the normal rules (ceiling, platform, hold time). Steel ends it at once. A ceramic bubble's
    /// crack counts as one of its pops, so one skewer can crack and then break it. Things it hit, and things born after
    /// it was fired (the halves of its pops), are left alone for skewerImmunity seconds so they can fly clear. The boss
    /// takes skewerBossDamage once and the shot ends there. The cable is the game's own harpoon, so run upgrades (speed,
    /// width, Anchor Tip hold time) and down shots work as for the plain harpoon.
    /// </summary>
    [Preserve]
    public sealed class SkewerHarpoon : WeaponSpec
    {
        public override string Id => "skewer";
        public override string Name => "Skewer Harpoon";
        public override WeaponSlot Slot => WeaponSlot.Harpoon;
        public override WeaponTier Tier => WeaponTier.Uncommon;
        public override int MaxAmmo => Tuning.I.skewerAmmo;
        public override int Order => 12;

        static readonly string[] Icon11 =
        {
            ".....L.....",
            "....LLL....",
            "...L.L.L...",
            ".....S.....",
            "..CC.L.CC..",
            ".C...S...C.",
            "..CC.L.CC..",
            ".....S.....",
            ".....L.....",
        };

        static readonly string[] Held =
        {
            ".CC.",
            "DDDD",
            "LSSS",
            "LSSS",
            "LSSS",
            "LSSS",
            "LSSS",
            "LSSS",
            "LSSS",
            "LSSS",
        };

        public override string[] IconRows => Icon11;
        public override string[] HeldRows => Held;
        public override WeaponBehaviour CreateBehaviour() => new Behaviour();

        internal sealed class Behaviour : WeaponBehaviour
        {
            public override bool FireVolley(WeaponContext ctx)
            {
                Harpoon free = null;
                foreach (var h in ctx.Harpoons())
                {
                    if (h == null || h.Active) continue;
                    free = h;
                    break;
                }
                if (free == null) return false;

                if (ctx.ShootDown) free.FireDown(ctx.Muzzle);
                else free.Fire(ctx.Muzzle);
                free.Volley = new HarpoonVolley { BossDamage = Tuning.I.skewerBossDamage };
                free.Pierce = new HarpoonPierce(Tuning.I.skewerPops, ctx.World?.Bubbles, ctx.World?.Rollers);
                return true;
            }
        }
    }

    /// <summary>
    /// A harpoon that keeps going after a pop (Skewer Harpoon). The game's hit loops ask Ignores before a hit and call
    /// Harpoon.Spend after one. Ticked by its harpoon, so it keeps working after the head's last shot empties the slot.
    /// </summary>
    public sealed class HarpoonPierce
    {
        /// <summary>Hits it may still make. The harpoon goes when this reaches zero.</summary>
        public int PopsLeft { get; private set; }
        /// <summary>Seconds since it was fired.</summary>
        public float Clock { get; private set; }

        readonly IList<Bubble> bubbles;
        readonly IList<Roller> rollers;
        readonly HashSet<Object> known = new HashSet<Object>();
        readonly Dictionary<Object, float> since = new Dictionary<Object, float>(); // born or hit at this Clock

        /// <summary>Things alive now are old; anything that shows up in these lists later counts as born from a pop.</summary>
        public HarpoonPierce(int pops, IList<Bubble> bubbles = null, IList<Roller> rollers = null)
        {
            PopsLeft = Mathf.Max(1, pops);
            this.bubbles = bubbles;
            this.rollers = rollers;
            Look();
            watching = true;
        }

        public void Tick(float dt)
        {
            Clock += dt;
            Look();
        }

        /// <summary>New bubbles and rolling balloons start their immunity the frame they appear.</summary>
        void Look()
        {
            if (bubbles != null)
                foreach (var b in bubbles) Note(b);
            if (rollers != null)
                foreach (var r in rollers) Note(r);
        }

        void Note(Object thing)
        {
            if (thing == null || !known.Add(thing)) return;
            if (watching) since[thing] = Clock;
        }

        bool watching; // false during the first look: those things were there before the shot

        /// <summary>Immune: born after the shot, or hit by it, less than skewerImmunity seconds ago.</summary>
        public bool Ignores(Object thing) =>
            thing != null && since.TryGetValue(thing, out float at) && Clock - at < Tuning.I.skewerImmunity;

        /// <summary>A hit: one pop used, the thing is immune for a while. False when no pops are left.</summary>
        public bool Strike(Object thing)
        {
            if (thing != null) since[thing] = Clock;
            PopsLeft = Mathf.Max(0, PopsLeft - 1);
            return PopsLeft > 0;
        }
    }

    public partial class Tuning
    {
        [Header("Weapon: Skewer Harpoon")]
        [Tooltip("Shots in a full Skewer Harpoon crate. Each fire press while it is held is one shot.")]
        public int skewerAmmo = 10;
        [Tooltip("Bubbles or rolling balloons one Skewer Harpoon cable can hit before it goes. A ceramic crack counts as one. Sensible 2 to 4.")]
        public int skewerPops = 3;
        [Tooltip("Boss health one Skewer Harpoon shot takes. The shot ends on the boss.")]
        public int skewerBossDamage = 2;
        [Tooltip("Seconds a thing the skewer hit, or a bubble born after it was fired, is left alone by it so it can fly clear.")]
        public float skewerImmunity = 0.25f;
    }
}
