using System;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Every number from the design sheet that the prototype uses, in one place.
    /// Lives at Assets/Bubblewright/Resources/Tuning.asset (created by Bubblewright > Run Project Setup).
    /// Edit it in the Inspector, even while playing: changes apply on the next frame.
    /// </summary>
    [CreateAssetMenu(menuName = "Bubblewright/Tuning", fileName = "Tuning")]
    public class Tuning : ScriptableObject
    {
        [Header("Screen and arena (units, 15 px per unit)")]
        public int pixelsPerUnit = 15;
        public Vector2Int referenceResolution = new Vector2Int(480, 270);
        [Tooltip("Play area width. Walls sit at -width/2 and +width/2.")]
        public float arenaWidth = 32f;
        [Tooltip("Floor is y = 0, ceiling is y = arenaHeight.")]
        public float arenaHeight = 14f;
        [Tooltip("Strip under the floor that holds the thumb controls.")]
        public float groundStrip = 4f;

        [Header("Wick")]
        [Tooltip("Top speed in units per second.")]
        public float runSpeed = 9f;
        [Tooltip("Seconds to reach top speed from standing.")]
        public float accelTime = 0.06f;
        [Tooltip("Seconds to stop from top speed (also used when reversing).")]
        public float stopTime = 0.04f;
        [Tooltip("Hitbox in units. 15 x 22 px. Bottom sits at his feet.")]
        public Vector2 hitbox = new Vector2(15f / 15f, 22f / 15f);
        [Tooltip("Launcher tip relative to Wick's feet, in units. Shots start here. Sheet says +0.35 u, which is 5 px.")]
        public Vector2 launcherTip = new Vector2(5f / 15f, 25f / 15f);
        public int hearts = 3;
        public float hurtFreeze = 0.12f;
        public float invulnerableTime = 1.5f;
        [Tooltip("Units Wick is knocked away from the bubble that hit him.")]
        public float knockback = 1f;
        public float knockbackTime = 0.15f;

        [Header("Climbing and falling")]
        [Tooltip("Vertical speed on ladders, in units per second.")]
        public float climbSpeed = 5f;
        [Tooltip("Downward acceleration while falling, in units per second squared.")]
        public float fallGravity = 45f;
        [Tooltip("Maximum downward speed while falling, in units per second.")]
        public float maxFallSpeed = 20f;
        [Tooltip("How wide Wick's footing is in units, checked at both edges for floor support.")]
        public float feetWidth = 0.6f;
        [Tooltip("How far in units from a ladder's centre line Wick can grab it.")]
        public float ladderGrabWidth = 0.6f;

        [Header("Harpoon")]
        public float harpoonSpeed = 20f;
        [Tooltip("Hold fire to shoot again as soon as the last shot is gone.")]
        public bool holdToRefire = true;

        [Header("Breakable blocks")]
        [Tooltip("Harpoon hits required to destroy a breakable tile.")]
        public int breakableHp = 2;
        [Tooltip("Chance that a destroyed breakable tile drops a heart pickup.")]
        [Range(0f, 1f)] public float breakableHeartChance = 0.03f;
        [Tooltip("Seconds before an uncollected heart pickup disappears.")]
        public float heartPickupLife = 8f;

        [Header("Bubbles")]
        [Tooltip("Horizontal speed, constant for every tier.")]
        public float bubbleSpeedX = 4f;
        [Tooltip("XL, L, M, S. Peak height is the TOP of the bubble above the floor.")]
        public TierSpec[] tiers =
        {
            new TierSpec { name = "XL", diameter = 3.5f, peakHeight = 13f,  bounceTime = 3.0f, score = 50 },
            new TierSpec { name = "L",  diameter = 2.5f, peakHeight = 11f,  bounceTime = 2.6f, score = 100 },
            new TierSpec { name = "M",  diameter = 1.6f, peakHeight = 8.5f, bounceTime = 2.2f, score = 150 },
            new TierSpec { name = "S",  diameter = 0.9f, peakHeight = 6f,   bounceTime = 1.8f, score = 200 },
        };
        public KindSpec[] kinds =
        {
            new KindSpec { kind = BubbleKind.Gloop,    color = Palette.Coral, children = 2, peakScale = 1f,   speedScale = 1f,   bounceTimeScale = 1f },
            new KindSpec { kind = BubbleKind.Trigloop, color = Palette.Azure, children = 3, peakScale = 1f,   speedScale = 1f,   bounceTimeScale = 1f },
            new KindSpec { kind = BubbleKind.Lead,     color = Palette.Plum,  children = 2, peakScale = 0.7f, speedScale = 1.4f, bounceTimeScale = 0.6f },
            new KindSpec { kind = BubbleKind.Lucky,    color = Palette.Lemon, children = 2, peakScale = 1f,   speedScale = 1f,   bounceTimeScale = 1f },
        };
        [Tooltip("How high (units) children are kicked up and out when their parent pops.")]
        public float childKickHeight = 2f;
        [Tooltip("Extra kick for the middle child of a Trigloop, so the three separate.")]
        public float trigloopMiddleKick = 1.6f;
        public float childStartScale = 0.7f;
        public float childGrowTime = 0.25f;

        [Header("Feel (starting values from the sheet's animation table)")]
        [Tooltip("Game freezes this long when a bubble is hit; the bubble swells meanwhile.")]
        public float popHitStop = 0.04f;
        [Tooltip("A hit bubble swells to this scale before it pops.")]
        public float popSwell = 1.15f;
        public float shakeTime = 0.15f;
        public int shakePixels = 3;
        public float lastPopSlowMo = 0.4f;
        public float introTime = 0.9f;
        public float turnTime = 0.2f;
        [Tooltip("The turn also stretches him a little taller at its thinnest point.")]
        public float turnStretchY = 1.06f;

        [Header("Wick animation")]
        [Tooltip("Run start: stretch (x, y) that eases back to round over runStartTime.")]
        public Vector2 runStartStretch = new Vector2(0.9f, 1.1f);
        public float runStartTime = 0.06f;
        [Tooltip("Degrees he leans into the run.")]
        public float runLean = 8f;
        [Tooltip("Stop or reverse from at least this fraction of top speed to skid.")]
        public float skidMinSpeed = 0.5f;
        public float skidTime = 0.08f;
        [Tooltip("Degrees he leans back while skidding.")]
        public float skidLean = 6f;
        [Tooltip("Settle squash (x, y) at the end of a skid.")]
        public Vector2 skidSettle = new Vector2(1.05f, 0.95f);
        public Vector2 fireSquash = new Vector2(1.1f, 0.9f);
        public float fireTime = 0.07f;
        [Tooltip("Pixels the launcher kicks down when firing.")]
        public int fireKickPixels = 2;
        [Tooltip("Hurt: he shows as a cream silhouette for the freeze plus this long.")]
        public float hurtFlashTime = 0.06f;
        public float celebrateTime = 0.9f;
        [Tooltip("Units high for each celebration hop.")]
        public float celebrateHop = 0.8f;
        public float celebrateFlameScale = 1.3f;
        [Tooltip("Seconds the stage-clear banner takes to drop in.")]
        public float bannerDropTime = 0.4f;

        [Header("Sound (placeholder, generated in code)")]
        [Range(0f, 1f)] public float volume = 0.7f;
        public bool mute;

        [Header("Worlds")]
        [Tooltip("A run is this many worlds, each with stagesPerWorld stages. The last stage of a world is its boss stage.")]
        public int worlds = 6;
        public int stagesPerWorld = 6;
        public string[] worldNames = { "Rooftops", "Forest", "Desert", "Winter", "Catacombs", "Lava" };

        [Header("Run (roguelike loop)")]
        [Tooltip("Bubble budget = budgetBase + budgetPerWorld * (world - 1) + budgetPerStage * (stage in world - 1). The sheet's costs: XL 8, L 4, M 2, S 1.")]
        public float budgetBase = 8f;
        public float budgetPerWorld = 6f;
        public float budgetPerStage = 2f;
        [Tooltip("Budget cost per tier: XL, L, M, S.")]
        public float[] tierCost = { 8f, 4f, 2f, 1f };
        [Tooltip("Cost multiplier per kind: Gloop, Trigloop, Lead, Lucky. A Trigloop XL takes 40 pops instead of 15.")]
        public float[] kindCostScale = { 1f, 2f, 1.25f, 1f };
        [Tooltip("Most bubbles at the start of a stage = startBubblesBase + (world + 1) / 2 (whole-number division); leftover budget is dropped.")]
        public int startBubblesBase = 3;
        [Tooltip("Trigloops can appear from this stage of the run on.")]
        public int trigloopFromStage = 3;
        [Tooltip("Leads can appear from this world on.")]
        public int leadFromWorld = 2;
        [Tooltip("Chance per stage (from stage 2) of a free Lucky bubble on top of the budget.")]
        [Range(0f, 1f)] public float luckyChance = 0.25f;
        [Tooltip("With more than one cable, the gap between shots while fire is held.")]
        public float multiShotGap = 0.15f;

        [Header("Hard worlds")]
        [Tooltip("A hard world has this much more bubble budget (boss stage included).")]
        public float hardBudgetScale = 1.3f;
        [Tooltip("A hard world has this share of the normal clock.")]
        public float hardTimeScale = 0.75f;
        [Tooltip("Everything scored in a hard world counts this many times.")]
        public float hardScoreScale = 2f;
        [Tooltip("Chance that a card slot is drawn from the rare upgrades.")]
        [Range(0f, 1f)] public float rareChance = 0.15f;
        [Tooltip("Cards offered after the boss of a hard world, and how many of them can be taken.")]
        public int hardRewardCards = 4;
        public int hardRewardPicks = 2;

        [Header("Road choice")]
        [Tooltip("The two doors stand this far left and right of the middle (units).")]
        public float pathDoorX = 7f;
        [Tooltip("Seconds after the screen opens before input counts, so a tap meant for the previous screen cannot pick a door.")]
        public float pathInputDelay = 0.35f;
        [Tooltip("Seconds the screen takes to fade to the ground strip colour once Wick reaches his door.")]
        public float pathFadeTime = 0.3f;

        [Header("Storm timer")]
        [Tooltip("Seconds on the clock = timeBase + timePerWorld * (world - 1) + timePerStage * (stage in world - 1). The clock runs only while playing, not in the intro.")]
        public float timeBase = 60f;
        public float timePerWorld = 8f;
        public float timePerStage = 4f;
        [Tooltip("When the clock runs out, every bubble moves this much faster.")]
        public float stormSpeed = 1.6f;
        [Tooltip("Hearts a bubble takes during the storm.")]
        public int stormDamage = 2;
        [Tooltip("The bar turns lemon and blinks for the last seconds.")]
        public float stormWarning = 10f;

        [Header("Surfaces")]
        [Tooltip("Ice: seconds to reach top speed from standing still (normal floor: Accel Time).")]
        public float iceAccelTime = 0.5f;
        [Tooltip("Ice: seconds to slide from top speed to a stop (normal floor: Stop Time).")]
        public float iceStopTime = 0.9f;
        [Tooltip("Sand: top speed is the normal run speed times this.")]
        public float sandSpeedScale = 0.7f;
        [Tooltip("Sand: seconds to reach top speed from standing still.")]
        public float sandAccelTime = 0.1f;
        [Tooltip("Desert stages get this much more time on the storm clock, because everything takes longer.")]
        public float desertTimeScale = 1.15f;
        [Tooltip("While walking on sand, a dust puff at his feet every this many seconds.")]
        public float sandDustInterval = 0.25f;

        [Header("Spikes")]
        [Tooltip("Seconds a strip stays down, showing only the holes in the floor.")]
        public float spikeDownTime = 3f;
        [Tooltip("Seconds the tips poke up and wobble before the spikes rise.")]
        public float spikeWarnTime = 0.5f;
        [Tooltip("Seconds the spikes stay raised. They hurt Wick only while raised, and always for one heart.")]
        public float spikeUpTime = 1.5f;
        [Tooltip("Each later strip starts this many seconds further into the cycle than the one before it, so they do not all rise together.")]
        public float spikePhaseStep = 0.8f;
        [Tooltip("How tall a raised spike's hurt box is, in pixels, measured up from the surface it sits on.")]
        public int spikeHeightPixels = 7;

        [Header("Bosses")]
        [Tooltip("Health of the first boss. Each later world's boss has bossHpPerWorld more.")]
        public int bossBaseHp = 12;
        public int bossHpPerWorld = 4;
        [Tooltip("Seconds on the storm clock of a boss stage. A hard world still scales it.")]
        public float bossTime = 120f;
        [Tooltip("Height of the boss's centre, in units above the floor.")]
        public float bossY = 11f;
        [Tooltip("Seconds the boss shows a cream silhouette after a hit.")]
        public float bossHitFlash = 0.08f;
        [Tooltip("Hearts Wick loses when he touches the boss's body.")]
        public int bossContactDamage = 1;
        [Tooltip("Speed of aimed bullets, in units per second.")]
        public float bulletSpeed = 7f;
        [Tooltip("Radius of a bullet's hit circle, in pixels.")]
        public int bulletRadiusPixels = 3;
        [Tooltip("During the storm the boss and its bullets move this much faster.")]
        public float bossStormScale = 1.4f;
        [Tooltip("Freeze when a harpoon hits the boss, in frames at 60 per second.")]
        public int bossHitStopFrames = 2;
        [Tooltip("Seconds the boss shakes and sheds debris after its last health is gone, before it bursts.")]
        public float bossDeathShake = 0.6f;

        public int RunLength => worlds * stagesPerWorld;
        public float HalfWidth => arenaWidth * 0.5f;
        public float Pixel => 1f / pixelsPerUnit;

        public KindSpec Kind(BubbleKind k)
        {
            foreach (var s in kinds) if (s.kind == k) return s;
            return kinds[0];
        }

        /// <summary>Gravity for a bubble so it always peaks at its tier's height.</summary>
        public float Gravity(BubbleKind kind, int tier)
        {
            var t = tiers[tier];
            var k = Kind(kind);
            float rise = Mathf.Max(0.1f, t.peakHeight * k.peakScale - t.diameter);
            float period = Mathf.Max(0.1f, t.bounceTime * k.bounceTimeScale);
            return 8f * rise / (period * period);
        }

        /// <summary>Upward speed when leaving the floor, so the top of the bubble reaches peakHeight.</summary>
        public float BounceSpeed(BubbleKind kind, int tier)
        {
            var t = tiers[tier];
            var k = Kind(kind);
            float rise = Mathf.Max(0.1f, t.peakHeight * k.peakScale - t.diameter);
            return Mathf.Sqrt(2f * Gravity(kind, tier) * rise);
        }

        static Tuning instance;
        public static Tuning I
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<Tuning>("Tuning");
                    if (instance == null)
                    {
                        instance = CreateInstance<Tuning>();
                        instance.name = "Tuning (defaults)";
                    }
                }
                return instance;
            }
        }
    }

    public enum BubbleKind { Gloop, Trigloop, Lead, Lucky }

    [Serializable]
    public struct TierSpec
    {
        public string name;
        public float diameter;
        public float peakHeight;
        [Tooltip("Seconds from leaving the floor to landing again. Sets this tier's gravity.")]
        public float bounceTime;
        public int score;
    }

    [Serializable]
    public struct KindSpec
    {
        public BubbleKind kind;
        public Color color;
        public int children;
        public float peakScale;
        public float speedScale;
        public float bounceTimeScale;
    }

    [Serializable]
    public struct SpawnSpec
    {
        public BubbleKind kind;
        [Range(0, 3)] public int tier;
        public Vector2 position;
        [Tooltip("-1 left, +1 right")] public int direction;
    }
}
