using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// One boss as a row of data. Kokan will design the real bosses later, so a boss is only numbers here:
    /// how big it is, how fast it moves and how it shoots. Step 18 gives each world its own row.
    /// </summary>
    public class BossSpec
    {
        public string name;
        /// <summary>Body size in units.</summary>
        public Vector2 size;
        /// <summary>Horizontal speed in units per second.</summary>
        public float speed;
        /// <summary>Seconds between aimed shots.</summary>
        public float aimedEvery;
        /// <summary>Bullets per aimed shot; more than one fan out evenly across aimedSpread degrees.</summary>
        public int aimedCount = 1;
        public float aimedSpread = 0f;

        /// <summary>Health of the boss of a world: bossBaseHp, then bossHpPerWorld more for each later world.</summary>
        public static int HpForWorld(int world) =>
            Tuning.I.bossBaseHp + Tuning.I.bossHpPerWorld * (Mathf.Max(1, world) - 1);

        /// <summary>For now every world has the same placeholder boss.</summary>
        public static BossSpec ForWorld(int world) => new BossSpec
        {
            name = "WARDEN",
            size = new Vector2(4f, 3f),
            speed = 3f,
            aimedEvery = 2f,
            aimedCount = 1,
            aimedSpread = 0f,
        };
    }
}
