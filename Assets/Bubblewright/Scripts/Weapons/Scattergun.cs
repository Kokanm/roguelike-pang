using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>
    /// W03 Scattergun: each press fires scatterPellets spark pellets spread evenly across scatterFan degrees around
    /// straight up (straight down when falling with Down held). Pellets fade after scatterRange units, so it is deadly up
    /// close and never reaches the ceiling from the floor. Halves born from a blast's pops are never hit by the same
    /// blast. Fired down in the air it kicks Wick up scatterHop units, once per time in the air. Between shots he pumps it
    /// for scatterCooldown seconds and the touch button is dim. All hits go through the shared Shot, so steel, ceramic,
    /// Ghosts, bosses and tiles follow the shared rules.
    /// </summary>
    [Preserve]
    public sealed class Scattergun : WeaponSpec
    {
        public override string Id => "scatter";
        public override string Name => "Scattergun";
        public override WeaponTier Tier => WeaponTier.Uncommon;
        public override int MaxAmmo => Tuning.I.scatterAmmo;
        public override int Order => 3;

        static readonly string[] Icon11 =
        {
            ".........LL",
            "LLLLLLLLLSR",
            "SSSSSSSSSSR",
            "DDSSSSSSSSR",
            "DD.DS....SS",
            "DD.DS......",
            "D..DD......",
        };

        static readonly string[] Hand =
        {
            "LSSSSSD",
            ".LSSSD.",
            ".LSSSD.",
            ".LSSSD.",
            ".LSSSD.",
            ".LSSSD.",
            ".LSSSD.",
            "..DDD..",
            "..DDD..",
        };

        public override string[] IconRows => Icon11;
        public override string[] HeldRows => Hand;
        public override WeaponBehaviour CreateBehaviour() => new Behaviour();

        /// <summary>
        /// The direction of pellet i of n: spread evenly across scatterFan degrees around aim, from one edge to the other.
        /// A single pellet goes straight along aim.
        /// </summary>
        public static Vector2 PelletDirection(Vector2 aim, int i, int n)
        {
            if (n <= 1) return aim.normalized;
            float fan = Mathf.Max(0f, Tuning.I.scatterFan);
            float angle = -fan * 0.5f + fan * i / (n - 1);
            return ((Vector2)(Quaternion.Euler(0f, 0f, angle) * aim)).normalized;
        }

        /// <summary>
        /// One blast: scatterPellets shared Shots from the muzzle, each fading after scatterRange units. Every pellet
        /// ignores whatever was not alive when the blast left, so the halves its own pops make are safe from it.
        /// Returns the pellets fired.
        /// </summary>
        public static List<Shot> Blast(WeaponWorld world, Vector2 muzzle, Vector2 aim)
        {
            var t = Tuning.I;
            var existing = new HashSet<object>();
            foreach (var b in world.Bubbles) if (b != null && !b.Popped) existing.Add(b);
            foreach (var r in world.Rollers) if (r != null && !r.Popped) existing.Add(r);
            System.Predicate<object> bornAfter = o => !existing.Contains(o);

            int n = Mathf.Max(1, t.scatterPellets);
            var pellets = new List<Shot>(n);
            for (int i = 0; i < n; i++)
            {
                var s = world.FireShot(muzzle, PelletDirection(aim, i, n) * t.scatterSpeed, t.scatterBossDamage, t.scatterRange);
                s.Ignores = bornAfter;
                pellets.Add(s);
            }
            return pellets;
        }

        internal sealed class Behaviour : WeaponBehaviour
        {
            /// <summary>Seconds of pumping left before the next shell can fire.</summary>
            internal float cooldown;
            /// <summary>The down-shot hop was used since he last stood on something.</summary>
            internal bool hopUsed;

            /// <summary>The touch button greys while he pumps it.</summary>
            public override bool Dimmed => cooldown > 0f;

            public override void Tick(WeaponContext ctx, float dt)
            {
                if (cooldown > 0f) cooldown = Mathf.Max(0f, cooldown - dt);
                if (!ctx.InAir) hopUsed = false;
                if (!ctx.Pressed || cooldown > 0f || !ctx.CanFire) return;
                if (!ctx.Spend()) return;

                cooldown = Tuning.I.scatterCooldown;
                Blast(ctx.World, ctx.Muzzle, ctx.Up);
                if (ctx.ShootDown && ctx.InAir && !hopUsed && ctx.Wick != null)
                {
                    hopUsed = true;
                    ctx.Wick.Hop(Tuning.I.scatterHop);
                }
                ctx.Flash();
            }

            public override void Dispose()
            {
                cooldown = 0f;
                hopUsed = false;
            }
        }
    }

    public partial class Tuning
    {
        [Header("Weapon: Scattergun")]
        [Tooltip("Shells in a full Scattergun crate. One shell is one blast of pellets.")]
        public int scatterAmmo = 8;
        [Tooltip("Pellets in one Scattergun blast.")]
        public int scatterPellets = 5;
        [Tooltip("Degrees the Scattergun's pellets spread across, edge to edge, centred on straight up (or down).")]
        public float scatterFan = 50f;
        [Tooltip("Units a Scattergun pellet travels before it fades.")]
        public float scatterRange = 6f;
        [Tooltip("Seconds between Scattergun shots while he pumps it. The touch button is dim meanwhile.")]
        public float scatterCooldown = 0.7f;
        [Tooltip("Units the Scattergun lifts Wick when fired down in the air, once per time in the air.")]
        public float scatterHop = 1.2f;
        [Tooltip("Units per second of a Scattergun pellet.")]
        public float scatterSpeed = 36f;
        [Tooltip("Boss health one Scattergun pellet takes. Fractions add up across pellets (0.4: a full blast takes 2).")]
        public float scatterBossDamage = 0.4f;
    }
}
