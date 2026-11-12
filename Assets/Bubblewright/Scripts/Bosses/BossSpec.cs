using UnityEngine;

namespace Bubblewright
{
    /// <summary>Which body the boss painter draws. Each adds its own detail to the shared block body.</summary>
    public enum BossShape
    {
        /// <summary>The plain block body with a brick chimney on top.</summary>
        Block,
        /// <summary>A low, wide rounded body with five triangular thorns along the top.</summary>
        Wide,
        /// <summary>A circle with a flat band and the visor across the middle.</summary>
        Round,
        /// <summary>The block body with a row of six downward teeth.</summary>
        Jaw,
        /// <summary>A narrow body with a pointed hood and rib lines.</summary>
        Tall,
        /// <summary>The block body with three tall spikes on top and an accent glow line in the visor.</summary>
        Crown,
    }

    /// <summary>
    /// One boss as a row of data. These six are placeholders that Kokan will redesign, so everything about a boss
    /// (its name, size, speed, attacks and look) comes from the table in ForWorld.
    /// </summary>
    public class BossSpec
    {
        public string name;
        /// <summary>Body size in units. This is the hit box; the painter may add detail above or below it.</summary>
        public Vector2 size;
        /// <summary>Horizontal speed in units per second.</summary>
        public float speed;

        /// <summary>Seconds between aimed attacks; 0 = never.</summary>
        public float aimedEvery;
        /// <summary>Bullets per aimed shot; more than one fan out evenly across aimedSpread degrees.</summary>
        public int aimedCount = 1;
        public float aimedSpread = 0f;
        /// <summary>Aimed shots in a row per attack, 0.25 s apart, each aimed afresh at Wick.</summary>
        public int aimedBurst = 1;

        /// <summary>Seconds between bouncy bullets; 0 = never.</summary>
        public float bouncyEvery;
        /// <summary>Times a bouncy bullet bounces on the floor before it falls through it.</summary>
        public int bouncyBounces;

        /// <summary>Seconds between released balloons; 0 = never.</summary>
        public float balloonEvery;
        /// <summary>Tier of the released Gloop: 1 = L, 2 = M.</summary>
        public int balloonTier = 2;

        /// <summary>Below half health, speed and every attack rate are multiplied by Tuning.bossEnrageScale.</summary>
        public bool enrageAtHalf;

        public BossShape shape;

        /// <summary>Seconds between the shots of an aimed burst.</summary>
        public const float BurstGap = 0.25f;
        /// <summary>When each attack first fires after the boss has slid in, so they do not all fire together.</summary>
        public const float AimedStart = 1.0f, BouncyStart = 1.8f, BalloonStart = 3.0f;

        /// <summary>Health of the boss of a world: bossBaseHp, then bossHpPerWorld more for each later world.</summary>
        public static int HpForWorld(int world) =>
            Tuning.I.bossBaseHp + Tuning.I.bossHpPerWorld * (Mathf.Max(1, world) - 1);

        /// <summary>The boss of each world. Placeholder numbers: tune them here.</summary>
        public static BossSpec ForWorld(int world)
        {
            switch (Mathf.Clamp(world, 1, 6))
            {
                default:
                case 1:
                    return new BossSpec
                    {
                        name = "CHIMNEY KING", size = new Vector2(4f, 3f), speed = 3.0f, shape = BossShape.Block,
                        aimedEvery = 2.0f, aimedCount = 1,
                        balloonEvery = 8f, balloonTier = 2,
                    };
                case 2:
                    return new BossSpec
                    {
                        name = "OLD BRAMBLE", size = new Vector2(5f, 2.5f), speed = 3.5f, shape = BossShape.Wide,
                        bouncyEvery = 2.2f, bouncyBounces = 2,
                        balloonEvery = 7f, balloonTier = 2,
                    };
                case 3:
                    return new BossSpec
                    {
                        name = "DUNE WARDEN", size = new Vector2(3.5f, 3.5f), speed = 4.0f, shape = BossShape.Round,
                        aimedEvery = 2.5f, aimedCount = 3, aimedSpread = 30f,
                        balloonEvery = 9f, balloonTier = 1,
                    };
                case 4:
                    return new BossSpec
                    {
                        name = "FROSTMAW", size = new Vector2(4.5f, 3f), speed = 4.0f, shape = BossShape.Jaw,
                        aimedEvery = 3.5f, aimedCount = 1,
                        bouncyEvery = 1.8f, bouncyBounces = 3,
                        balloonEvery = 8f, balloonTier = 2,
                    };
                case 5:
                    return new BossSpec
                    {
                        name = "BONE ABBOT", size = new Vector2(3f, 4f), speed = 4.5f, shape = BossShape.Tall,
                        aimedEvery = 3.0f, aimedCount = 1, aimedBurst = 3,
                        bouncyEvery = 2.5f, bouncyBounces = 1,
                        balloonEvery = 8f, balloonTier = 1,
                    };
                case 6:
                    return new BossSpec
                    {
                        name = "CINDER TYRANT", size = new Vector2(5f, 3.5f), speed = 5.0f, shape = BossShape.Crown,
                        aimedEvery = 2.2f, aimedCount = 3, aimedSpread = 30f,
                        bouncyEvery = 2.0f, bouncyBounces = 4,
                        balloonEvery = 6f, balloonTier = 1,
                        enrageAtHalf = true,
                    };
            }
        }
    }
}
