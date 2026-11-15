using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>
    /// The debug test weapon: one Shot per press, 20 ammo, never in crates. It is also the template for a real
    /// weapon: a sealed spec with [Preserve], its numbers in a partial Tuning, its behaviour nested inside.
    /// </summary>
    [Preserve]
    public sealed class TestWeapon : WeaponSpec
    {
        public override string Id => "test";
        public override string Name => "Test Shot";
        public override int MaxAmmo => Tuning.I.testWeaponAmmo;
        public override bool Drops => false;
        public override int Order => 0;

        static readonly string[] Icon11 =
        {
            "....RYR....",
            "...RYWYR...",
            "...RYWYR...",
            "....RYR....",
            ".....S.....",
            "...LSSSD...",
            "...LSSSD...",
            "...DDDDD...",
        };

        static readonly string[] Hand =
        {
            ".RYR.",
            ".LSD.",
            ".LSD.",
            ".LSD.",
            ".LSD.",
            ".LSD.",
            ".LSD.",
            ".LSD.",
            ".LSD.",
            "DDDDD",
        };

        public override string[] IconRows => Icon11;
        public override string[] HeldRows => Hand;
        public override WeaponBehaviour CreateBehaviour() => new Behaviour();

        sealed class Behaviour : WeaponBehaviour
        {
            public override void Tick(WeaponContext ctx, float dt)
            {
                if (!ctx.Pressed || !ctx.CanFire) return;
                if (!ctx.Spend()) return;
                ctx.World.FireShot(ctx.Muzzle, ctx.Up * Tuning.I.weaponShotSpeed, Tuning.I.testShotBossDamage);
                ctx.Flash();
            }
        }
    }
}
