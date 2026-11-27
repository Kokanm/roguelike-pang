using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>
    /// Sootjack, the World 1 boss: the grate opens only after the coughs, a stomp sends waves both ways, a chimney loses
    /// its top when it jumps off, phase 3 comes out of the pot that puffed, Wick can walk under the belly, and the
    /// roof arena validates. The fight runs against a fake stage (no scene), with the real Tells ticking its attacks.
    /// </summary>
    public class SootjackTests
    {
        const float Dt = 1f / 60f;

        GameObject root;
        StageMap previousMap;
        StageMap map;
        FakeStage stage;

        /// <summary>What a phase asks of the stage, recorded, with the real tell timer so a telegraphed attack fires when it is due.</summary>
        sealed class FakeStage : IBossStage
        {
            public StageMap Map { get; set; }
            public ArenaEvents Arena { get; set; }
            public Rect WickBox { get; set; } = new Rect(-9.5f, 0f, 1f, 1.47f);
            public int BubblesAlive { get; set; }
            public readonly Tells Tells = new Tells(null);
            public readonly List<Launch> Fired = new List<Launch>();
            public readonly List<(TellKind kind, Rect area, float lead)> Told = new List<(TellKind, Rect, float)>();
            public int Impacts;

            public void Fire(Launch[] launches) => Fired.AddRange(launches);

            public float Telegraph(TellKind kind, Rect area, float lead, Action attack)
            {
                float used = Tells.Add(kind, area, lead, attack);
                Told.Add((kind, area, used));
                return used;
            }

            public void Impact(Vector2 at, float strength) => Impacts++;
        }

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("SootjackTests");
            previousMap = Game.Map;
            map = StageSpec.ById(Sootjack.ArenaName).Build(false).map;
            typeof(Game).GetProperty("Map", BindingFlags.Public | BindingFlags.Static).SetValue(null, map);
            stage = new FakeStage { Map = map, Arena = new ArenaEvents(map) };
            UnityEngine.Random.InitState(7);
        }

        [TearDown]
        public void TearDown()
        {
            typeof(Game).GetProperty("Map", BindingFlags.Public | BindingFlags.Static).SetValue(null, previousMap);
            UnityEngine.Object.DestroyImmediate(root);
        }

        Boss Make(bool hard = false)
        {
            var boss = Boss.Create(root.transform, new Sootjack(), Themes.ForWorld(1), hard);
            boss.Stage = stage;
            boss.OnPhaseBreak = _ => stage.Tells.Clear(); // what Game does at a phase break
            for (int i = 0; i < 600 && !boss.Entered; i++) boss.Tick(Dt, Vector2.zero, false);
            Assert.IsTrue(boss.Entered);
            return boss;
        }

        Vector2 Wick => stage.WickBox.center;

        /// <summary>Runs the fight for up to seconds, stopping as soon as until says so. Returns whether it did.</summary>
        bool Run(Boss boss, float seconds, Func<bool> until)
        {
            for (float t = 0f; t < seconds; t += Dt)
            {
                boss.Tick(Dt, Wick, true);
                stage.Tells.Tick(Dt);
                stage.Arena.Tick(Dt);
                if (until()) return true;
            }
            return false;
        }

        /// <summary>Takes the current phase's health, then runs until the next phase has started.</summary>
        void ToPhase(Boss boss, int phase)
        {
            while (boss.PhaseIndex < phase)
            {
                int now = boss.PhaseIndex;
                boss.Damage(boss.PhaseHp);
                Assert.IsTrue(Run(boss, 10f, () => boss.PhaseIndex > now && !boss.Staggering), "the next phase should start after the stagger");
            }
        }

        BossPart Grate(Boss boss) => boss.Part("grate");

        static Rect Around(Vector2 p) => new Rect(p.x - 0.1f, p.y - 0.1f, 0.2f, 0.2f);

        // ------------------------------------------------------------------ the def

        [Test]
        public void SootjackIsTheWorldOneBossWithThreePhasesOfSixSixAndEight()
        {
            var def = BossDef.ForWorld(1);
            Assert.IsInstanceOf<Sootjack>(def);
            Assert.AreEqual("SOOTJACK", def.Name.ToUpperInvariant());
            Assert.AreEqual(Sootjack.ArenaName, def.ArenaId);
            var phases = def.CreatePhases(false);
            CollectionAssert.AreEqual(new[] { 6, 6, 8 }, phases.Select(p => p.hp).ToArray());
            var hard = def.CreatePhases(true);
            Assert.AreEqual(Mathf.CeilToInt(6 * Tuning.I.hardBossHpScale), hard[0].hp);
            Assert.AreEqual(240f, def.Clock, 1e-4f);
        }

        // ------------------------------------------------------------------ phase 1: stilt walk

        [Test]
        public void TheGrateIsClosedAndClinksUntilThreeCoughsHaveBeenCoughed()
        {
            var boss = Make();
            var walk = (Sootjack.StiltWalk)boss.Phase;
            int hp = boss.Hp;
            int coughsWhenOpened = -1;
            bool closedHitClinked = false;
            bool opened = Run(boss, 60f, () =>
            {
                var grate = Grate(boss);
                if (!grate.Open)
                {
                    if (!closedHitClinked && walk.Coughs >= 1)
                    {
                        var at = grate.World(boss).center;
                        Assert.AreEqual(HitResult.Clink, BossFight.Hit(boss, Around(at), 1, at), "a closed grate is armour");
                        closedHitClinked = true;
                    }
                    return false;
                }
                coughsWhenOpened = walk.Coughs;
                return true;
            });
            Assert.IsTrue(opened, "the grate should open");
            Assert.AreEqual(3, coughsWhenOpened, "after three coughs and not before");
            Assert.IsTrue(closedHitClinked);
            Assert.AreEqual(hp, boss.Hp, "nothing hurt it while the grate was closed");

            var spot = Grate(boss).World(boss).center;
            Assert.AreEqual(HitResult.Damage, BossFight.Hit(boss, Around(spot), 1, spot));
            Assert.AreEqual(hp - 1, boss.Hp);
        }

        [Test]
        public void OnTheHardRoadTheGrateOpensAfterTwoCoughs()
        {
            var boss = Make(hard: true);
            var walk = (Sootjack.StiltWalk)boss.Phase;
            int coughs = -1;
            Assert.IsTrue(Run(boss, 60f, () =>
            {
                if (!Grate(boss).Open) return false;
                coughs = walk.Coughs;
                return true;
            }));
            Assert.AreEqual(2, coughs);
        }

        [Test]
        public void TheGrateClosesAgainAndTheCoughsStartOver()
        {
            var boss = Make();
            var walk = (Sootjack.StiltWalk)boss.Phase;
            Assert.IsTrue(Run(boss, 60f, () => Grate(boss).Open));
            Assert.IsTrue(Run(boss, 10f, () => !Grate(boss).Open), "the glow ends");
            Assert.AreEqual(0, walk.Coughs, "the count starts over");
        }

        [Test]
        public void EveryStopIsAStompThatSendsWavesBothWays()
        {
            var boss = Make();
            Assert.IsTrue(Run(boss, 30f, () => stage.Fired.Any(l => l.kind == BulletKind.Wave)));
            var waves = stage.Fired.Where(l => l.kind == BulletKind.Wave).ToList();
            Assert.IsTrue(waves.Any(w => w.velocity.x < 0f), "a wave goes left");
            Assert.IsTrue(waves.Any(w => w.velocity.x > 0f), "a wave goes right");
            Assert.IsTrue(waves.All(w => Mathf.Approximately(w.surfaceY, 0f)), "along the floor");
            Assert.Greater(stage.Impacts, 0, "the stomp shakes the screen");
            Assert.IsTrue(stage.Told.Any(t => t.kind == TellKind.DustColumn && t.lead >= Tuning.I.bossTellMin), "a dust column came first");
        }

        [Test]
        public void OnTheHardRoadEveryStompSendsTwoWavesEachWay()
        {
            var calm = Make();
            Assert.IsTrue(Run(calm, 30f, () => stage.Fired.Any(l => l.kind == BulletKind.Wave)));
            int calmWaves = stage.Fired.Count(l => l.kind == BulletKind.Wave);
            UnityEngine.Object.DestroyImmediate(calm.gameObject);

            stage = new FakeStage { Map = map, Arena = new ArenaEvents(map) };
            var hard = Make(hard: true);
            Assert.IsTrue(Run(hard, 30f, () => stage.Fired.Count(l => l.kind == BulletKind.Wave) >= calmWaves * 2));
            var waves = stage.Fired.Where(l => l.kind == BulletKind.Wave).ToList();
            Assert.IsTrue(waves.Any(w => w.delay > 0f), "the second pair leaves a moment after the first");
            Assert.AreEqual(waves.Count(w => w.velocity.x < 0f), waves.Count(w => w.velocity.x > 0f), "the same number each way");
        }

        [Test]
        public void ACoughIsAFanOfSootAimedAtWickAfterAGlowAtTheTop()
        {
            var boss = Make();
            Assert.IsTrue(Run(boss, 30f, () => stage.Fired.Any(l => l.kind == BulletKind.Aimed)));
            var fan = stage.Fired.Where(l => l.kind == BulletKind.Aimed).ToList();
            Assert.AreEqual(Tuning.I.sootCoughBullets, fan.Count);
            Assert.IsTrue(stage.Told.Any(t => t.kind == TellKind.EdgeGlow && t.lead >= Tuning.I.bossTellMin), "the pipe glowed first");
            Assert.IsTrue(fan.All(l => l.position.y > boss.Centre.y), "it comes out of the pipe, on top");
        }

        [Test]
        public void WickCanWalkUnderTheBellyAndOnlyTheStiltsAndBodyHurt()
        {
            var boss = Make();
            boss.SetPosition(0f, 3.2f);
            Assert.IsFalse(boss.ContactWith(new Rect(-0.5f, 0f, 1f, 1.47f)), "under the belly");
            Assert.IsFalse(boss.ContactWith(new Rect(-1.9f, 0f, 1f, 1.47f)), "still between the stilts");
            Assert.IsTrue(boss.ContactWith(new Rect(2.6f, 0f, 1f, 1.47f)), "a stilt");
            Assert.IsTrue(boss.ContactWith(new Rect(-0.5f, 2.4f, 1f, 1.47f)), "the stove");
            boss.Hidden = true;
            Assert.IsFalse(boss.ContactWith(new Rect(2.6f, 0f, 1f, 1.47f)), "not while it is in a chimney");
        }

        [Test]
        public void ItWalksBothWaysAndNeverLeavesItsRange()
        {
            var boss = Make();
            float min = boss.Centre.x, max = boss.Centre.x;
            Run(boss, 25f, () =>
            {
                min = Mathf.Min(min, boss.Centre.x);
                max = Mathf.Max(max, boss.Centre.x);
                return false;
            });
            Assert.Less(min, -1f);
            Assert.Greater(max, 1f);
            Assert.LessOrEqual(max, Tuning.I.sootWalkLimit + 1e-3f);
            Assert.GreaterOrEqual(min, -Tuning.I.sootWalkLimit - 1e-3f);
        }

        [Test]
        public void AHarpoonPassesThroughASootjackThatIsInAChimney()
        {
            var boss = Make();
            boss.SetPosition(0f, 3.2f);
            boss.Hidden = true;
            var h = Harpoon.Create(root.transform);
            h.Fire(new Vector2(0f, 0f));
            for (int i = 0; i < 60; i++) h.Tick(Dt);
            int hp = boss.Hp;
            Assert.AreEqual(0, BossFight.HarpoonHits(boss, new List<Harpoon> { h }));
            Assert.AreEqual(hp, boss.Hp);
            Assert.AreEqual(HitResult.Miss, BossFight.Hit(boss, boss.Body, 1, boss.Centre));
        }

        // ------------------------------------------------------------------ phase 2: chimney hop

        [Test]
        public void AChimneyLosesItsTopTileWhenSootjackLeavesIt()
        {
            var boss = Make();
            ToPhase(boss, 1);
            var hop = (Sootjack.ChimneyHop)boss.Phase;
            var perch = Sootjack.Perches[0];
            Assert.IsTrue(perch.IsStack, "it starts on a chimney");
            var top = perch.TopCells[0];
            Assert.IsTrue(Run(boss, 30f, () => hop.Landings >= 1), "it lands on its first perch");
            Assert.AreEqual(TileKind.Solid, map.Get(top.x, top.y).kind, "the top is there while it sits on it");

            Assert.IsTrue(Run(boss, 30f, () => hop.Leaves >= 1), "it jumps off");
            Assert.GreaterOrEqual(stage.Arena.TempTileCount, perch.TopCells.Length, "the top tiles are crumbling");
            foreach (var cell in perch.TopCells)
                Assert.AreEqual(TileKind.Solid, map.Get(cell.x, cell.y).kind, "not gone yet: it blinks first");
            Run(boss, Tuning.I.sootChimneyCrumble + 0.2f, () => false);
            foreach (var cell in perch.TopCells)
                Assert.AreEqual(TileKind.Empty, map.Get(cell.x, cell.y).kind, $"the top tile at {cell} is gone");
            var below = new Vector2Int(perch.TopCells[0].x, perch.TopCells[0].y - 1);
            Assert.AreEqual(TileKind.Solid, map.Get(below.x, below.y).kind, "only the top goes");
            Assert.IsTrue(map.Get(3, 3).ladder, "the ladder up the chimney stays");
        }

        [Test]
        public void ThePerchedGrateFacesDownOverOpenFloorAndOpensAfterTheCurtain()
        {
            var boss = Make();
            ToPhase(boss, 1);
            var hop = (Sootjack.ChimneyHop)boss.Phase;
            Assert.IsTrue(Run(boss, 30f, () => stage.Fired.Any(l => l.kind == BulletKind.Aimed && l.velocity.y < 0f)), "the curtain falls");
            var grate = Grate(boss).World(boss);
            var curtain = stage.Fired.Where(l => l.kind == BulletKind.Aimed).ToList();
            Assert.Greater(curtain.Count, 20, "a whole row");
            Assert.IsTrue(curtain.All(l => l.position.x < grate.xMin || l.position.x > grate.xMax), "no bullet falls through the lane under the grate");
            Assert.IsTrue(stage.Told.Any(t => t.kind == TellKind.LitLane && t.lead >= Tuning.I.bossCurtainTell - 1e-4f), "the lane was lit first");
            Assert.IsTrue(Run(boss, 10f, () => Grate(boss).Open), "the grate opens");
            int col = StageMap.Col(grate.center.x);
            Assert.AreEqual(0f, map.FloorBelow(grate.center.x, grate.yMin), 1e-3f, "open floor under the grate: nothing blocks the harpoon");
            Assert.IsTrue(!map.IsSolid(col, 0));
        }

        [Test]
        public void EachPerchDropsAGloopFromTheTopMiddlePipeUnlessTheScreenIsFull()
        {
            var boss = Make();
            ToPhase(boss, 1);
            Assert.IsTrue(Run(boss, 30f, () => stage.Arena.PipesFlashing > 0), "the pipe flashes");
            UnityEngine.Object.DestroyImmediate(boss.gameObject);

            stage = new FakeStage { Map = map, Arena = new ArenaEvents(map), BubblesAlive = Tuning.I.bossMaxBubbles };
            var full = Make();
            ToPhase(full, 1);
            Assert.IsFalse(Run(full, 20f, () => stage.Arena.PipesFlashing > 0), "no balloon while the cap is reached");
        }

        [Test]
        public void ItHopsFromPerchToPerchWithARedShadowFirst()
        {
            var boss = Make();
            ToPhase(boss, 1);
            var hop = (Sootjack.ChimneyHop)boss.Phase;
            Assert.IsTrue(Run(boss, 60f, () => hop.Landings >= 2));
            Assert.IsTrue(stage.Told.Any(t => t.kind == TellKind.RedShadow && t.lead >= Tuning.I.bossTellMin), "a red shadow on the next perch");
            Assert.AreNotEqual(Sootjack.Perches[0].name, Sootjack.Perches[hop.PerchIndex].name, "it changed perch");
        }

        // ------------------------------------------------------------------ phase 3: flue run

        [Test]
        public void InPhaseThreeSootjackComesOutOfThePotThatPuffed()
        {
            var boss = Make();
            ToPhase(boss, 2);
            var flue = (Sootjack.FlueRun)boss.Phase;
            Assert.IsTrue(boss.Hidden || flue.Underground, "it dived into a chimney");
            Assert.IsTrue(Run(boss, 60f, () => flue.Emerged >= 0), "it bursts out");
            Assert.AreEqual(1, flue.Puffed.Count, "on the calm road one pot puffs");
            Assert.AreEqual(flue.Puffed[0], flue.Emerged, "out of that pot");
            Assert.AreEqual(Sootjack.PotX[flue.Emerged], boss.Centre.x, 1e-3f, "right above it");
            Assert.IsFalse(boss.Hidden);
            Assert.IsTrue(stage.Told.Any(t => t.kind == TellKind.DustColumn && t.lead >= Tuning.I.bossTellMin), "a puff of dust came first");
            Assert.IsTrue(stage.Fired.Count(l => l.kind == BulletKind.Aimed) >= Tuning.I.sootRingSlots - Tuning.I.sootRingGap, "a ring bursts out with it");
        }

        [Test]
        public void ThePopRingLeavesItsGapStraightDownWhereWickStandsUnderThePot()
        {
            var boss = Make();
            ToPhase(boss, 2);
            var flue = (Sootjack.FlueRun)boss.Phase;
            Assert.IsTrue(Run(boss, 60f, () => flue.Emerged >= 0));
            var ring = stage.Fired.Where(l => l.kind == BulletKind.Aimed).ToList();
            Assert.IsFalse(ring.Any(l => l.velocity.y < 0f && Mathf.Abs(l.velocity.x) < 1e-3f), "nothing falls straight down");
            Assert.IsTrue(ring.Any(l => l.velocity.y > 0f && Mathf.Abs(l.velocity.x) < 1e-3f), "the rest of the ring is whole");
        }

        [Test]
        public void OnTheHardRoadTwoPotsPuffAndOnlyOneIsReal()
        {
            var boss = Make(hard: true);
            ToPhase(boss, 2);
            var flue = (Sootjack.FlueRun)boss.Phase;
            Assert.IsTrue(Run(boss, 60f, () => flue.Emerged >= 0));
            Assert.AreEqual(2, flue.Puffed.Count, "both pots puff");
            Assert.AreEqual(2, flue.Puffed.Distinct().Count());
            Assert.AreEqual(flue.RealPot, flue.Emerged, "it comes out of the real one");
            Assert.AreEqual(Sootjack.PotX[flue.RealPot], boss.Centre.x, 1e-3f);
            Assert.AreEqual(1, stage.Fired.Count(l => l.kind == BulletKind.Aimed && l.velocity.y > 0f && Mathf.Abs(l.velocity.x) < 1e-3f), "one ring only, from the real pot");
        }

        [Test]
        public void TheGrateIsOpenWhileItHangsOverThePotAndClosedWhenItIsGone()
        {
            var boss = Make();
            ToPhase(boss, 2);
            var flue = (Sootjack.FlueRun)boss.Phase;
            Assert.IsTrue(Run(boss, 60f, () => flue.Emerged >= 0));
            Assert.IsTrue(Grate(boss).Open);
            Assert.IsTrue(Run(boss, 20f, () => boss.Hidden), "it goes back down");
            Assert.IsFalse(Grate(boss).Open);
            Assert.IsNull(boss.FirstPartTouching(new Rect(-20f, -5f, 40f, 40f)), "nothing to hit while it is in the chimney");
        }

        [Test]
        public void BetweenPopsItChargesAlongTheRoofLowEnoughToJumpOntoAChimneyAndLongEnoughToNeedAnEdgeGlow()
        {
            var boss = Make();
            ToPhase(boss, 2);
            var flue = (Sootjack.FlueRun)boss.Phase;
            Assert.IsTrue(Run(boss, 90f, () => flue.Charges >= 1 && boss.Pose == Sootjack.ChargePose && !boss.Hidden), "it charges");
            Assert.IsTrue(stage.Told.Any(t => t.kind == TellKind.EdgeGlow && t.lead >= Tuning.I.bossTellMin), "the screen edge glowed first");
            var sled = boss.Part("sled").World(boss);
            Assert.AreEqual(Tuning.I.sootChargeHeight, sled.height, 1e-3f);
            Assert.AreEqual(0f, sled.yMin, 1e-3f, "along the floor");
            Assert.Greater(sled.yMax, 1.9f + 0.05f, "a single jump does not clear it");
            Assert.LessOrEqual(sled.yMax, map.FloorBelow(-14f, 20f) - 1f, "a chimney top is above it");
            float start = boss.Centre.x;
            Run(boss, 0.5f, () => false);
            Assert.AreEqual(Mathf.Sign(start) * -1f, Mathf.Sign(boss.Centre.x - start), "it runs across, not away");
            Assert.IsTrue(boss.ContactWith(new Rect(boss.Centre.x - 0.5f, 0f, 1f, 1.47f)), "it hurts on the floor");
            Assert.IsFalse(boss.ContactWith(new Rect(boss.Centre.x - 0.5f, 4f, 1f, 1.47f)), "not on a chimney");
        }

        // ------------------------------------------------------------------ the whole fight

        [Test]
        public void EveryTellIsAtLeastHalfASecondAheadOfItsAttack()
        {
            var boss = Make(hard: true);
            Run(boss, 20f, () => false);
            ToPhase(boss, 1);
            Run(boss, 25f, () => false);
            ToPhase(boss, 2);
            Run(boss, 40f, () => false);
            Assert.Greater(stage.Told.Count, 6);
            foreach (var t in stage.Told) Assert.GreaterOrEqual(t.lead, Tuning.I.bossTellMin - 1e-4f, t.kind.ToString());
        }

        [Test]
        public void TheThirdPhaseEndingIsTheDefeatAndNeverAnotherStagger()
        {
            var boss = Make();
            ToPhase(boss, 2);
            boss.Damage(boss.PhaseHp);
            Run(boss, 2f, () => false);
            Assert.AreEqual(0, boss.Hp, "the last phase's health is the end of the fight");
            Assert.IsFalse(boss.Staggering);
        }

        // ------------------------------------------------------------------ the arena and the kit

        [Test]
        public void TheRoofArenaValidatesAndHoldsTwoChimneysAndAHighPlatform()
        {
            var spec = StageSpec.ById(Sootjack.ArenaName);
            Assert.IsNotNull(spec);
            Assert.IsTrue(spec.IsBossArena);
            CollectionAssert.IsEmpty(StageValidator.Validate(spec, false), "the validator finds nothing wrong");
            Assert.AreEqual(1, spec.Storeys);
            Assert.AreEqual(4f, map.FloorBelow(-14f, 20f), 1e-3f, "the left chimney is 4 tall");
            Assert.AreEqual(4f, map.FloorBelow(14f, 20f), 1e-3f, "so is the right");
            Assert.AreEqual(8f, map.FloorBelow(0f, 20f), 1e-3f, "the high platform stands 8 up");
            Assert.AreEqual(0f, map.FloorBelow(0f, 7f), 1e-3f, "and Sootjack walks under it");
        }

        [Test]
        public void EveryPerchHasOpenAirUnderItsGrateAndStandsOnSolidGround()
        {
            foreach (var perch in Sootjack.Perches)
            {
                float surface = map.FloorBelow(perch.legX, 20f);
                Assert.Greater(surface, 3f, $"{perch.name} has something to stand on");
                var grateCentre = perch.GrateCentre(surface);
                Assert.IsFalse(map.OverlapsSolid(new Rect(grateCentre.x - 1.4f, 0f, 2.8f, grateCentre.y)),
                    $"{perch.name}: the harpoon can reach the grate from the floor");
            }
        }

        [Test]
        public void ACrumblingTileBlinksForItsLastSecondsAndThenBreaks()
        {
            var events = new ArenaEvents(map);
            Assert.IsTrue(events.CrumbleTile(1, 3, 1f));
            Assert.AreEqual(0, events.CrumblingCount, "not blinking yet");
            for (float t = 0f; t < 1f - Tuning.I.tempTileWarn * 0.5f; t += Dt) events.Tick(Dt);
            Assert.AreEqual(1, events.CrumblingCount);
            Assert.IsTrue(events.IsCrumbling(1, 3));
            Assert.AreEqual(TileKind.Solid, map.Get(1, 3).kind, "still there while it blinks");
            for (int i = 0; i < 60; i++) events.Tick(Dt);
            Assert.AreEqual(TileKind.Empty, map.Get(1, 3).kind);
            Assert.IsFalse(events.CrumbleTile(1, 3, 1f), "an empty cell has nothing to crumble");
        }

        [Test]
        public void ACrumblingTileKeepsItsLadder()
        {
            var events = new ArenaEvents(map);
            Assert.IsTrue(map.Get(3, 3).ladder);
            Assert.IsTrue(events.CrumbleTile(3, 3, 0.2f));
            for (int i = 0; i < 30; i++) events.Tick(Dt);
            Assert.AreEqual(TileKind.Empty, map.Get(3, 3).kind);
            Assert.IsTrue(map.Get(3, 3).ladder);
        }
    }
}
