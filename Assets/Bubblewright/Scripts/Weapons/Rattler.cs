using UnityEngine;
using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>
    /// W02 Rattler: hold the weapon button and, after a short spin-up, a stream of spark bullets leaves at rattlerRate a
    /// second, each within rattlerSpread degrees of straight up (or straight down when falling with Down held). Wick walks
    /// slower while the button is held, and fired down in the air his fall slows too. Releasing stops at once and the next
    /// hold spins up again. All hits go through the shared Shot, so steel, ceramic, Ghosts, bosses and tiles follow the
    /// shared rules.
    /// </summary>
    [Preserve]
    public sealed class Rattler : WeaponSpec
    {
        public override string Id => "rattler";
        public override string Name => "Rattler";
        public override WeaponTier Tier => WeaponTier.Uncommon;
        public override int MaxAmmo => Tuning.I.rattlerAmmo;
        public override int Order => 2;

        static readonly string[] Icon11 =
        {
            "...LLLLLLLR",
            "SSSSSSSSSS.",
            "SSSLLLLLLLR",
            "SSSSSSSSSS.",
            "DSDDDDD....",
            "DSDYYYD....",
            "DSDYYYD....",
            "DD.DDD.....",
        };

        static readonly string[] Hand =
        {
            "R.R.R..",
            "LSLSLSD",
            "LSLSLSD",
            "LSLSLSD",
            "LSLSLSD",
            "LSLSLSD",
            "DDDDDDD",
            "DSSSSSD",
            "DDDDDDD",
        };

        public override string[] IconRows => Icon11;
        public override string[] HeldRows => Hand;
        public override WeaponBehaviour CreateBehaviour() => new Behaviour();

        /// <summary>Seconds between bullets once it has spun up.</summary>
        public static float Gap => 1f / Mathf.Max(0.01f, Tuning.I.rattlerRate);

        /// <summary>
        /// The direction of one bullet: aim (up or down) turned by a random angle within rattlerSpread degrees either
        /// side. t is a random number in [0, 1); 0.5 is dead straight.
        /// </summary>
        public static Vector2 SpreadDirection(Vector2 aim, float t)
        {
            float angle = (t * 2f - 1f) * Mathf.Max(0f, Tuning.I.rattlerSpread);
            return (Vector2)(Quaternion.Euler(0f, 0f, angle) * aim);
        }

        internal sealed class Behaviour : WeaponBehaviour
        {
            /// <summary>Seconds the button has been held this time. Releasing sets it back to 0, so the next hold spins up again.</summary>
            internal float heldFor;
            /// <summary>Seconds since the last bullet left. The first bullet after spin-up leaves at once.</summary>
            internal float sinceShot;
            internal bool spunUp;
            Wick wick;

            /// <summary>The button is held: he is slowed, whether or not a bullet left this frame.</summary>
            internal bool Spinning => heldFor > 0f;

            public override void Tick(WeaponContext ctx, float dt)
            {
                if (ctx.Wick != null && ctx.Wick != wick)
                {
                    SetScales(1f, 1f);
                    wick = ctx.Wick;
                }

                if (!ctx.Held)
                {
                    heldFor = 0f;
                    spunUp = false;
                    SetScales(1f, 1f);
                    return;
                }

                heldFor += dt;
                SetScales(Tuning.I.rattlerWalkScale, ctx.ShootDown ? Tuning.I.rattlerDownFallScale : 1f);

                if (!spunUp)
                {
                    if (heldFor < Tuning.I.rattlerSpinUp) return;
                    spunUp = true;
                    sinceShot = Gap; // the first bullet leaves the moment it has spun up
                }
                else sinceShot += dt;

                if (!ctx.CanFire || sinceShot < Gap) return;
                if (!ctx.Spend()) return;

                var dir = SpreadDirection(ctx.Up, ctx.Random01());
                ctx.World.FireShot(ctx.Muzzle, dir * Tuning.I.rattlerSpeed, Tuning.I.rattlerBossDamage);
                // Keep the leftover time so the rate does not drift with the frame rate, but never bank a burst.
                sinceShot = Mathf.Min(sinceShot - Gap, dt);
                ctx.Flash();
            }

            void SetScales(float walk, float fall)
            {
                if (wick == null) return;
                wick.WeaponWalkScale = walk;
                wick.WeaponFallScale = fall;
            }

            public override void Dispose()
            {
                SetScales(1f, 1f);
                heldFor = 0f;
                spunUp = false;
            }
        }
    }

    public partial class Tuning
    {
        [Header("Weapon: Rattler")]
        [Tooltip("Bullets in a full Rattler crate.")]
        public int rattlerAmmo = 60;
        [Tooltip("Bullets per second while the weapon button is held, after the spin-up.")]
        public float rattlerRate = 12f;
        [Tooltip("Degrees either side of straight up (or down) a Rattler bullet may stray, picked at random per bullet.")]
        public float rattlerSpread = 3f;
        [Tooltip("Wick's walking speed while the Rattler's button is held, as a fraction of normal.")]
        [Range(0f, 1f)] public float rattlerWalkScale = 0.6f;
        [Tooltip("Seconds the button must be held before the first Rattler bullet leaves. Releasing resets it.")]
        public float rattlerSpinUp = 0.15f;
        [Tooltip("Units per second of a Rattler bullet.")]
        public float rattlerSpeed = 36f;
        [Tooltip("Boss health one Rattler bullet takes. Fractions add up across bullets (0.2: five bullets take 1).")]
        public float rattlerBossDamage = 0.2f;
        [Tooltip("Fired down in the air, his fall speed is multiplied by this while the button is held (0.67 slows it by a third).")]
        [Range(0f, 1f)] public float rattlerDownFallScale = 0.67f;
    }
}
