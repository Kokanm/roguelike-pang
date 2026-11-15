using UnityEngine;
using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>
    /// W01 Twin Carbine: one spark bullet per press, from two barrels in turn, with no cap on bullets in the air.
    /// Tapping fires up to carbineRate shots a second; holding the button fires at half that. Fires up, or down when
    /// falling with Down held. All hits go through the shared Shot, so steel, ceramic, Ghosts, bosses and tiles follow
    /// the shared rules.
    /// </summary>
    [Preserve]
    public sealed class TwinCarbine : WeaponSpec
    {
        public override string Id => "carbine";
        public override string Name => "Twin Carbine";
        public override WeaponTier Tier => WeaponTier.Common;
        public override int MaxAmmo => Tuning.I.carbineAmmo;
        public override int Order => 1;

        static readonly string[] Icon11 =
        {
            "LLLLLLLLLLR",
            "SSSSSSSSSS.",
            "LLLLLLLLLLR",
            "SSSSSSSSSS.",
            ".DSSD.SD...",
            ".DSS.......",
            ".DSS.......",
            ".DDD.......",
        };

        static readonly string[] Hand =
        {
            "RR.RR",
            "LS.LS",
            "LS.LS",
            "LS.LS",
            "LS.LS",
            "LS.LS",
            "LS.LS",
            "LS.LS",
            "DDDDD",
            "DDDDD",
        };

        public override string[] IconRows => Icon11;
        public override string[] HeldRows => Hand;
        public override WeaponBehaviour CreateBehaviour() => new Behaviour();

        /// <summary>Seconds between tapped shots at the top rate.</summary>
        public static float TapGap => 1f / Mathf.Max(0.01f, Tuning.I.carbineRate);

        /// <summary>Seconds between shots while the button is held down: half the top rate.</summary>
        public static float HoldGap => 2f * TapGap;

        /// <summary>
        /// Where the next bullet leaves: the muzzle shifted half the barrel gap to the left (barrel 0) or right (barrel 1),
        /// snapped to the pixel grid.
        /// </summary>
        public static Vector2 BarrelPoint(Vector2 muzzle, int barrel)
        {
            float px = Tuning.I.Pixel;
            float half = Tuning.I.carbineBarrelGap * 0.5f * px;
            float x = muzzle.x + (barrel == 0 ? -half : half);
            return new Vector2(Mathf.Round(x / px) * px, muzzle.y);
        }

        internal sealed class Behaviour : WeaponBehaviour
        {
            /// <summary>Seconds since the last bullet left. Starts high so the first press fires at once.</summary>
            internal float sinceShot = 999f;
            /// <summary>A tap that came too soon fires as soon as the gap allows, so fast tapping is never eaten.</summary>
            internal bool queued;
            /// <summary>The barrel the next bullet leaves from: 0 left, 1 right.</summary>
            internal int barrel;

            public override void Tick(WeaponContext ctx, float dt)
            {
                sinceShot += dt;
                if (ctx.Pressed) queued = true;
                if (!ctx.CanFire) return;

                float gap;
                if (queued) gap = TapGap;
                else if (ctx.Held) gap = HoldGap;
                else return;
                if (sinceShot < gap) return;

                queued = false;
                if (!ctx.Spend()) return;

                var from = BarrelPoint(ctx.Muzzle, barrel);
                ctx.World.FireShot(from, ctx.Up * Tuning.I.carbineSpeed, Tuning.I.carbineBossDamage);
                barrel ^= 1;
                // Keep the leftover time so the rate does not drift with the frame rate, but never bank a burst.
                sinceShot = Mathf.Min(sinceShot - gap, dt);

                var muzzle = ctx.Muzzle;
                ctx.Muzzle = from; // the flash shows on the barrel that fired
                ctx.Flash();
                ctx.Muzzle = muzzle;
            }
        }
    }

    public partial class Tuning
    {
        [Header("Weapon: Twin Carbine")]
        [Tooltip("Bullets in a full Twin Carbine crate.")]
        public int carbineAmmo = 30;
        [Tooltip("Top shots per second when tapping. Holding the button fires at half this rate.")]
        public float carbineRate = 8f;
        [Tooltip("Pixels between the carbine's two barrels. Bullets alternate between them.")]
        public float carbineBarrelGap = 4f;
        [Tooltip("Units per second of a carbine bullet.")]
        public float carbineSpeed = 36f;
        [Tooltip("Boss health one carbine bullet takes. Fractions add up across bullets (1/3: three bullets take 1).")]
        public float carbineBossDamage = 1f / 3f;
    }
}
