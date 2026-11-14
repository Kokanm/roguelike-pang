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
            new KindSpec { kind = BubbleKind.Gloop,    color = Palette.Coral, children = 2, peakScale = 1f,   speedScale = 1f,   bounceTimeScale = 1f, largestTier = 0 },
            new KindSpec { kind = BubbleKind.Trigloop, color = Palette.Azure, children = 3, peakScale = 1f,   speedScale = 1f,   bounceTimeScale = 1f, largestTier = 0 },
            new KindSpec { kind = BubbleKind.Lead,     color = Palette.Plum,  children = 2, peakScale = 0.7f, speedScale = 1.4f, bounceTimeScale = 0.6f, largestTier = 0 },
            new KindSpec { kind = BubbleKind.Lucky,    color = Palette.Lemon,   children = 2, peakScale = 1f, speedScale = 1f, bounceTimeScale = 1f, largestTier = 0 },
            new KindSpec { kind = BubbleKind.Clinger,  color = Palette.Clinger, children = 2, peakScale = 1f, speedScale = 1f, bounceTimeScale = 1f, largestTier = 0 },
            new KindSpec { kind = BubbleKind.Sinker,   color = Palette.Sinker,  children = 2, peakScale = 1f, speedScale = 1f, bounceTimeScale = 1f, largestTier = 1 },
            new KindSpec { kind = BubbleKind.Frost,    color = Palette.Frost,   children = 2, peakScale = 1f, speedScale = 1f, bounceTimeScale = 1f, largestTier = 0 },
            new KindSpec { kind = BubbleKind.Ghost,    color = Palette.Ghost,   children = 2, peakScale = 1f, speedScale = 1f, bounceTimeScale = 1f, largestTier = 0 },
            new KindSpec { kind = BubbleKind.Scorch,   color = Palette.Scorch,  children = 2, peakScale = 1f, speedScale = 1f, bounceTimeScale = 1f, largestTier = 0 },
            new KindSpec { kind = BubbleKind.Drifter,  color = Palette.Drifter, children = 2, peakScale = 1f, speedScale = 1f, bounceTimeScale = 1f, largestTier = 1 },
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
        [Tooltip("Cost multiplier per kind, in BubbleKind order: Gloop 1, Trigloop 2, Lead 1.25, Lucky 1, Clinger 1, Sinker 1.25, Frost 1.25, Ghost 1.5, Scorch 1.5, Drifter 1.25.")]
        public float[] kindCostScale = { 1f, 2f, 1.25f, 1f, 1f, 1.25f, 1.25f, 1.5f, 1.5f, 1.25f };
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
        public float iceAccelTime = 0.40f;
        [Tooltip("Ice: seconds to brake from baseline Run Speed to zero when pressing the opposite direction (higher top speeds slide proportionally longer).")]
        public float iceTurnTime = 0.35f;
        [Tooltip("Ice: seconds to slide from baseline Run Speed to a stop when letting go of movement (higher top speeds coast proportionally longer).")]
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
        public float spikeDownTime = 2.6f;
        [Tooltip("Seconds the tips poke up and wobble before the spikes rise.")]
        public float spikeWarnTime = 0.5f;
        [Tooltip("Seconds the spikes stay raised. They hurt Wick only while raised, and always for one heart.")]
        public float spikeUpTime = 1.8f;
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
        [Tooltip("Gravity on bouncy bullets, in units per second squared.")]
        public float bouncyGravity = 22f;
        [Tooltip("Height above the floor that the top of a bouncy bullet reaches on each hop. Low enough that Wick must jump it or outrun it rather than walk under it.")]
        public float bouncyPeak = 2.6f;
        [Tooltip("Constant sideways speed of a bouncy bullet, in units per second. It heads toward the side Wick was on when it was fired.")]
        public float bouncySpeedX = 3.5f;
        [Tooltip("Radius of a bouncy bullet's hit circle, in pixels (the sprite is a 9 px disc).")]
        public int bouncyRadiusPixels = 4;
        [Tooltip("A boss does not release a balloon while this many bubbles are alive.")]
        public int bossMaxBubbles = 6;
        [Tooltip("A boss that enrages at half health multiplies its speed and every attack rate by this below half health.")]
        public float bossEnrageScale = 1.3f;

        [Header("Jump")]
        [Tooltip("Height of a jump from standing, in units. Platforms are 5 units up, so a jump never reaches one.")]
        public float jumpHeight = 1.9f;
        [Tooltip("Seconds from take-off to the top of the jump. Jump gravity is 2 x height / time squared.")]
        public float jumpTimeToApex = 0.28f;
        [Tooltip("Seconds after walking off an edge in which a jump still works.")]
        public float coyoteTime = 0.08f;
        [Tooltip("A jump pressed this many seconds before landing still jumps on landing.")]
        public float jumpBuffer = 0.1f;
        [Tooltip("How far below the floor, in units, his feet go into a gap before he loses a heart and comes back.")]
        public float gapFallDepth = 1.5f;
        [Tooltip("Hearts lost by falling into a gap in the ground. Ignores the safe time after a hit.")]
        public int gapDamage = 1;
        [Tooltip("Rig scale (x, y) at take-off, easing back to 1 x 1 at the top of the jump.")]
        public Vector2 jumpStretch = new Vector2(0.9f, 1.12f);
        [Tooltip("Rig scale (x, y) when he lands from a jump, easing back to 1 x 1.")]
        public Vector2 jumpLandSquash = new Vector2(1.15f, 0.85f);
        [Tooltip("Seconds the landing squash lasts.")]
        public float jumpLandTime = 0.08f;
        [Tooltip("From world 2 on, the chance that a normal stage uses a layout with gaps in the ground.")]
        [Range(0f, 1f)] public float gapLayoutChance = 0.33f;
        [Tooltip("He must stand on solid ground this long before that spot counts as safe to come back to after a gap.")]
        public float gapSafeStandTime = 0.2f;
        [Tooltip("After a gap he reappears this many units further from the gap edge than his last safe spot.")]
        public float gapRespawnInset = 0.5f;

        [Header("Rolling balloons")]
        [Tooltip("Diameter in units of a big and a small rolling balloon (18 px and 12 px). A jump (Jump Height) must clear the big one.")]
        public float[] rollerDiameters = { 1.2f, 0.8f };
        [Tooltip("Rolling speed in units per second: big, small.")]
        public float[] rollerSpeeds = { 3.5f, 4.5f };
        [Tooltip("Score for popping a rolling balloon: big, small.")]
        public int[] rollerScores = { 150, 200 };
        [Tooltip("Bubble budget a big rolling balloon costs. It is taken from the stage budget before bubbles are picked.")]
        public float rollerCost = 3f;
        [Tooltip("Rolling balloons can appear on normal stages from this world on (never on boss stages).")]
        public int rollerFromWorld = 2;
        [Tooltip("Most big rolling balloons a stage starts with.")]
        public int rollerMaxPerStage = 2;
        [Tooltip("Chance per stage, from Roller From World on, that the stage gets rolling balloons.")]
        [Range(0f, 1f)] public float rollerChance = 0.5f;
        [Tooltip("From this world on a stage with rolling balloons gets two instead of one (still capped by Roller Max Per Stage).")]
        public int rollerTwoFromWorld = 4;
        [Tooltip("Colour of rolling balloons, used for their pop effect.")]
        public Color rollerColor = Palette.Pearl;

        [Header("Steel and ceramic")]
        [Tooltip("Steel rolling balls (indestructible, not needed to clear the stage) can appear on normal stages from this world on.")]
        public int steelFromWorld = 3;
        [Tooltip("Chance per stage, from Steel From World on, that the stage gets steel rolling balls. They cost no budget.")]
        [Range(0f, 1f)] public float steelChance = 0.35f;
        [Tooltip("Most steel rolling balls a stage starts with.")]
        public int steelMaxPerStage = 1;
        [Tooltip("Ceramic bubbles and ceramic rolling balls can appear on normal stages from this world on.")]
        public int ceramicFromWorld = 3;
        [Tooltip("Chance that a generated bubble is ceramic (only tiers up to Ceramic Max Tier).")]
        [Range(0f, 1f)] public float ceramicChance = 0.2f;
        [Tooltip("Largest tier that can be ceramic: 0 XL, 1 L, 2 M, 3 S.")]
        public int ceramicMaxTier = 2;
        [Tooltip("Chance that a generated big rolling ball is ceramic.")]
        [Range(0f, 1f)] public float ceramicRollerChance = 0.3f;
        [Tooltip("Harpoon hits a ceramic bubble or rolling ball takes before it breaks. The pieces it leaves are ordinary.")]
        public int ceramicHits = 2;
        [Tooltip("A ceramic bubble or rolling ball costs this many times the budget of an ordinary one.")]
        public float ceramicCostScale = 1.5f;

        [Header("Frost bubble")]
        [Tooltip("Seconds a floor tile a Frost bubble landed on stays ice before it goes back to what it was.")]
        public float frostIceTime = 4f;
        [Tooltip("For this many seconds at the start of a stage a Frost bubble cannot freeze the ground next to where Wick starts.")]
        public float frostSafeTime = 2f;
        [Tooltip("How many ground columns each side of Wick's start are kept safe at first.")]
        public int frostSafeHalfWidth = 2;

        [Header("Clinger")]
        [Tooltip("A stuck Clinger starts to wobble when Wick crosses the lane straight out from the surface it holds, this close to its centre line (units). Under a ceiling, Wick's centre x; on a floor, Wick's centre x; on a wall, the gap between its centre and Wick's box vertically.")]
        public float clingerTriggerX = 1.5f;
        [Tooltip("How long a Clinger wobbles before it lets go (seconds).")]
        public float clingerWobbleTime = 0.4f;
        [Tooltip("After letting go, a Clinger ignores surfaces for this long so it leaves the one it held (seconds).")]
        public float clingerRestickDelay = 0.2f;
        [Tooltip("A Clinger lets go by itself after it has held a surface this long, even if Wick never crossed its lane (seconds). Keeps a big one on the floor from blocking Wick for good.")]
        public float clingerMaxStickTime = 4f;
        [Tooltip("When a Clinger lets go it moves sideways this many times faster than a Gloop, until it sticks again.")]
        public float clingerLetGoSpeedScale = 1.75f;
        [Tooltip("A Clinger letting go of a ceiling starts falling at this speed instead of from rest (units per second).")]
        public float clingerDropSpeed = 4f;
        [Tooltip("A Clinger only sticks under a platform when its bottom stays at least this high above the floor below, so Wick can walk under it.")]
        public float clingerMinClearance = 2f;

        [Header("Sinker")]
        [Tooltip("On sand, each landing keeps this share of the last bounce's height.")]
        [Range(0.1f, 1f)] public float sinkerKeepSand = 0.75f;
        [Tooltip("On any other surface, each landing keeps this share of the last bounce's height.")]
        [Range(0.1f, 1f)] public float sinkerKeep = 0.9f;
        [Tooltip("When the next bounce would rise less than this many units, the Sinker stops bouncing and rolls.")]
        public float sinkerRollBelow = 0.3f;

        [Header("Ghost")]
        [Tooltip("Seconds a Ghost stays solid before it fades.")]
        public float ghostSolidTime = 2.5f;
        [Tooltip("Seconds a Ghost stays faded: the harpoon passes through it and it cannot hurt Wick.")]
        public float ghostFadedTime = 1.5f;
        [Tooltip("Seconds at the end of the fade in which the Ghost flickers as a warning that it is about to turn solid.")]
        public float ghostFlickerTime = 0.3f;

        [Header("Scorch")]
        [Tooltip("Seconds a flame burns where a Scorch landed.")]
        public float flameLife = 1.5f;
        [Tooltip("Most flames burning at once. A new one puts the oldest out.")]
        public int flameMax = 6;
        [Tooltip("Height of a flame's hurt box above the surface, in units.")]
        public float flameHurtHeight = 0.5f;
        [Tooltip("The hurt box is a tile wide minus this on each side, in units.")]
        public float flameHurtInset = 0.15f;

        [Header("Drifter")]
        [Tooltip("Constant flight speed of a Drifter along its diagonal, in units per second.")]
        public float drifterSpeed = 5f;
        [Tooltip("A Drifter placed in a stage may not pass closer than this (plus its radius) to Wick's start in its first Drifter Safe Time.")]
        public float drifterSafeDistance = 3f;
        [Tooltip("Seconds of straight flight checked for the Drifter fair-start rule.")]
        public float drifterSafeTime = 1f;

        [Header("Stage recipes")]
        [Tooltip("How a normal stage spends its budget. World 1 stages 1 and 2 always use Mixed. Every other normal stage draws one row with In Pool on and From World at or below that world, and a row is not drawn again in that world until every such row has been used. Boss stages are not recipes.")]
        public RecipeDef[] recipes =
        {
            new RecipeDef { id = RecipeId.Mixed, name = "Mixed", fromWorld = 1, minTier = 0, maxTier = 3, startCapScale = 1f, startCapMax = 0, rollerShare = 0f, rollerMax = 0, ceramicChance = -1f, allRollersCeramic = false, inPool = true, patterns = new[] { PlacementPattern.Scattered } },
            new RecipeDef { id = RecipeId.Giants, name = "Giants", fromWorld = 1, minTier = 0, maxTier = 0, startCapScale = 1f, startCapMax = 2, rollerShare = 0f, rollerMax = 0, ceramicChance = -1f, allRollersCeramic = false, inPool = true, patterns = new[] { PlacementPattern.MirroredPair, PlacementPattern.TopRow } },
            new RecipeDef { id = RecipeId.Swarm, name = "Swarm", fromWorld = 1, minTier = 2, maxTier = 3, startCapScale = 2f, startCapMax = 0, rollerShare = 0f, rollerMax = 0, ceramicChance = -1f, allRollersCeramic = false, inPool = true, patterns = new[] { PlacementPattern.TopRow, PlacementPattern.OnePerFloor, PlacementPattern.Scattered } },
            new RecipeDef { id = RecipeId.RollingFloor, name = "Rolling floor", fromWorld = 2, minTier = 0, maxTier = 3, startCapScale = 1f, startCapMax = 3, rollerShare = 0.6f, rollerMax = 4, ceramicChance = -1f, allRollersCeramic = false, inPool = true, patterns = new[] { PlacementPattern.TopRow, PlacementPattern.Scattered } },
            new RecipeDef { id = RecipeId.HardShell, name = "Hard shell", fromWorld = 3, minTier = 0, maxTier = 3, startCapScale = 1f, startCapMax = 0, rollerShare = 0f, rollerMax = 0, ceramicChance = 0.7f, allRollersCeramic = true, inPool = true, patterns = new[] { PlacementPattern.Scattered, PlacementPattern.MirroredPair, PlacementPattern.OnePerFloor } },
            new RecipeDef { id = RecipeId.Reinforcements, name = "Reinforcements", fromWorld = 2, minTier = 0, maxTier = 3, startCapScale = 1f, startCapMax = 0, rollerShare = 0f, rollerMax = 0, ceramicChance = -1f, allRollersCeramic = false, inPool = true, waves = 2, startShare = 0.5f, timeScale = 1.15f, patterns = new[] { PlacementPattern.Scattered } },
        };
        [Tooltip("Bubbles a Giants stage may start with from Giants Late From World on, in place of that row's Start Cap Max.")]
        public int giantsMaxLate = 3;
        [Tooltip("From this world on, Giants uses Giants Max Late instead of its Start Cap Max.")]
        public int giantsLateFromWorld = 4;

        /// <summary>The row for this id, or the first row when the id is missing.</summary>
        public RecipeDef Recipe(RecipeId id)
        {
            if (recipes != null)
                foreach (var row in recipes)
                    if (row != null && row.id == id) return row;
            return recipes != null && recipes.Length > 0 ? recipes[0] : null;
        }

        [Header("Reinforcements")]
        [Tooltip("A wave is called when this many enemies or fewer are left on screen.")]
        public int waveEnemiesLeft = 2;
        [Tooltip("Seconds after the previous group arrived before the next wave is called anyway.")]
        public float waveMaxWait = 20f;
        [Tooltip("Seconds a warning marker blinks at the ceiling before its bubble drops in.")]
        public float waveWarningTime = 1.2f;
        [Tooltip("A marker never appears closer than this many units, sideways, to Wick.")]
        public float waveSafeDistance = 4f;

        [Header("Balance")]
        [Tooltip("Every bubble budget is multiplied by this, because jumping makes dodging easier.")]
        public float jumpBudgetBonus = 1.1f;

        /// <summary>Gravity during a jump, from jumpHeight and jumpTimeToApex.</summary>
        public float JumpGravity => 2f * jumpHeight / Mathf.Max(0.001f, jumpTimeToApex * jumpTimeToApex);
        /// <summary>Upward speed at take-off.</summary>
        public float JumpSpeed => JumpGravity * jumpTimeToApex;

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

    public enum BubbleKind { Gloop, Trigloop, Lead, Lucky, Clinger, Sinker, Frost, Ghost, Scorch, Drifter }

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
        [Tooltip("Largest tier this kind comes in: 0 XL, 1 L.")]
        public int largestTier;
    }

    [Serializable]
    public struct SpawnSpec
    {
        public BubbleKind kind;
        [Range(0, 3)] public int tier;
        public Vector2 position;
        [Tooltip("-1 left, +1 right")] public int direction;
        [Tooltip("Takes Tuning.ceramicHits hits to break.")] public bool ceramic;
    }
}
