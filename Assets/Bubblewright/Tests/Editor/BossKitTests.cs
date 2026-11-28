using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>The boss kit: parts and the one damage call, phases with a stagger, the phase break, the segmented bar and BossDef lookup.</summary>
    public class BossKitTests
    {
        const float Dt = 1f / 60f;

        GameObject root;
        StageMap previousMap;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("BossKitTests");
            previousMap = Game.Map;
            typeof(Game).GetProperty("Map", BindingFlags.Public | BindingFlags.Static).SetValue(null, StageMap.Flat());
        }

        [TearDown]
        public void TearDown()
        {
            typeof(Game).GetProperty("Map", BindingFlags.Public | BindingFlags.Static).SetValue(null, previousMap);
            Object.DestroyImmediate(root);
        }

        /// <summary>A boss with armour on the left, a core on the right and three phases (hp 2, 3, 4). Not discovered: it lives in the test assembly.</summary>
        class TestDef : BossDef
        {
            public int[] phaseHp = { 2, 3, 4 };
            public bool coreOpen;
            public override int World => 1;
            public override string Name => "TEST BOSS";
            public override Vector2 Size => new Vector2(4f, 3f);
            protected override BossPhase[] BuildPhases(bool hard) => phaseHp.Select(hp => new BossPhase { hp = hp }).ToArray();
            public override void BuildParts(Boss boss, bool hard)
            {
                boss.AddPart(new BossPart { Name = "armour", Local = new Rect(-2f, -1.5f, 2f, 3f) });
                boss.AddPart(new BossPart { Name = "core", Local = new Rect(0f, -1.5f, 2f, 3f), IsCore = true, Open = coreOpen });
            }
        }

        Boss Make(TestDef def = null, bool hard = false)
        {
            var boss = Boss.Create(root.transform, def ?? new TestDef(), Themes.ForWorld(1), hard);
            for (int i = 0; i < 300 && !boss.Entered; i++) boss.Tick(Dt, Vector2.zero, false);
            Assert.IsTrue(boss.Entered);
            boss.SetPosition(0f, 8f);
            return boss;
        }

        static Rect Around(Vector2 p) => new Rect(p.x - 0.1f, p.y - 0.1f, 0.2f, 0.2f);

        static readonly Vector2 OnArmour = new Vector2(-1f, 8f);
        static readonly Vector2 OnCore = new Vector2(1f, 8f);

        // ------------------------------------------------------------------ the one damage call

        [Test]
        public void AClosedCoreAndArmourTakeNoDamageAndClink()
        {
            var boss = Make();
            int clinks = 0;
            boss.OnClink = at => clinks++;
            int hp = boss.Hp;
            Assert.AreEqual(HitResult.Clink, BossFight.Hit(boss, Around(OnArmour), 1, OnArmour), "armour");
            boss.Tick(Dt * 60f, Vector2.zero, true); // let the clink cooldown pass
            Assert.AreEqual(HitResult.Clink, BossFight.Hit(boss, Around(OnCore), 1, OnCore), "a closed core draws and acts as armour");
            Assert.AreEqual(hp, boss.Hp);
            Assert.AreEqual(2, clinks);
        }

        [Test]
        public void AnOpenCoreTakesTheDamage()
        {
            var boss = Make(new TestDef { coreOpen = true });
            int hp = boss.Hp;
            Assert.AreEqual(HitResult.Damage, BossFight.Hit(boss, Around(OnCore), 1, OnCore));
            Assert.AreEqual(hp - 1, boss.Hp);
        }

        [Test]
        public void TheFirstPartTheAreaTouchesDecides()
        {
            var boss = Make(new TestDef { coreOpen = true });
            var both = new Rect(-1f, 7f, 2f, 2f); // overlaps the armour (listed first) and the open core
            Assert.AreEqual(HitResult.Clink, BossFight.Hit(boss, both, 1, Vector2.zero));
            Assert.AreEqual(HitResult.Miss, BossFight.Hit(boss, Around(new Vector2(9f, 8f)), 1, new Vector2(9f, 8f)));
        }

        [Test]
        public void AHarpoonThatTouchesArmourIsUsedUpAndDoesNoDamage()
        {
            var boss = Make();
            var h = Harpoon.Create(root.transform);
            h.Fire(new Vector2(-1f, 1f)); // under the armour half of the body
            for (int i = 0; i < 240 && !h.Hits(boss.Body); i++) h.Tick(Dt);
            Assert.IsTrue(h.Hits(boss.Body));
            int hp = boss.Hp, damage = 0;
            BossFight.HarpoonHits(boss, new List<Harpoon> { h }, (a, at) => damage += a);
            Assert.IsFalse(h.Active, "used up");
            Assert.AreEqual(0, damage);
            Assert.AreEqual(hp, boss.Hp);
        }

        [Test]
        public void WeaponDamageGoesThroughTheSameCall()
        {
            var open = Make(new TestDef { coreOpen = true });
            var world = new WeaponWorld(new List<Bubble>(), new List<Roller>(), new List<Bullet>(), () => open, root.transform);
            int hp = open.Hp;
            world.WeaponHitBoss(1f, OnCore);
            Assert.AreEqual(hp - 1, open.Hp);
            world.WeaponHitBoss(1f, OnArmour);
            Assert.AreEqual(hp - 1, open.Hp, "armour takes nothing, and no fraction is saved for later");
            Assert.AreEqual(0f, world.BossCarry, 1e-4f);

            var closed = Make();
            var closedWorld = new WeaponWorld(new List<Bubble>(), new List<Roller>(), new List<Bullet>(), () => closed, root.transform);
            closedWorld.WeaponHitBoss(1f, OnCore);
            Assert.AreEqual(closed.MaxHp, closed.Hp);
        }

        // ------------------------------------------------------------------ phases

        [Test]
        public void APhaseEndsWhenItsSegmentIsEmptyAndTheNextStartsAfterTheStagger()
        {
            var boss = Make();
            int breaks = 0;
            boss.OnPhaseBreak = i => breaks++;
            Assert.AreEqual(9, boss.Hp);
            Assert.AreEqual(0, boss.PhaseIndex);
            boss.Damage(5); // only the first segment of 2 is spent
            Assert.AreEqual(7, boss.Hp, "damage does not spill into the next segment");
            boss.Tick(Dt, Vector2.zero, true);
            Assert.IsTrue(boss.Staggering);
            Assert.AreEqual(1, breaks);
            Assert.AreEqual(0, boss.PhaseIndex, "still the old phase while it staggers");

            float t = 0f;
            while (t < Tuning.I.bossStaggerTime - 0.05f) { boss.Tick(Dt, Vector2.zero, true); t += Dt; }
            Assert.IsTrue(boss.Staggering);
            for (int i = 0; i < 30 && boss.Staggering; i++) boss.Tick(Dt, Vector2.zero, true);
            Assert.IsFalse(boss.Staggering);
            Assert.AreEqual(1, boss.PhaseIndex);
            Assert.AreEqual(3, boss.PhaseHp, "the next segment is full");
        }

        [Test]
        public void NothingHurtsTheBossWhileItStaggers()
        {
            var boss = Make(new TestDef { coreOpen = true });
            boss.Damage(2);
            boss.Tick(Dt, Vector2.zero, true);
            Assert.IsTrue(boss.Staggering);
            int hp = boss.Hp;
            Assert.AreEqual(HitResult.Clink, BossFight.Hit(boss, Around(OnCore), 1, OnCore));
            Assert.AreEqual(hp, boss.Hp);
        }

        [Test]
        public void TheLastPhaseEndingDefeatsTheBossWithoutAStagger()
        {
            var boss = Make(new TestDef { phaseHp = new[] { 1 } });
            boss.Damage(1);
            boss.Tick(Dt, Vector2.zero, true);
            Assert.AreEqual(0, boss.Hp);
            Assert.IsFalse(boss.Staggering);
            Assert.IsTrue(BossFight.StageCleared(boss, 2));
        }

        [Test]
        public void ThePhaseBreakClearsEveryBossBullet()
        {
            var bullets = new List<Bullet>();
            for (int i = 0; i < 3; i++) bullets.Add(Bullet.Create(root.transform, new Vector2(i, 5f), Vector2.down, Themes.ForWorld(1)));
            int destroyed = 0;
            BossFight.PhaseBreak(bullets, b => { destroyed++; Object.DestroyImmediate(b.gameObject); }, hard: false, hearts: 3, maxHearts: 3);
            Assert.AreEqual(0, bullets.Count);
            Assert.AreEqual(3, destroyed);
            Assert.AreEqual(0, root.GetComponentsInChildren<Bullet>().Length);
        }

        [Test]
        public void AHeartDropsOnTheCalmRoadWhenWickIsHurtAndNeverOnTheHardRoad()
        {
            var none = new List<Bullet>();
            System.Action<Bullet> destroy = b => { };
            Assert.IsTrue(BossFight.PhaseBreak(none, destroy, hard: false, hearts: 2, maxHearts: 3).dropHeart, "calm road, below maximum");
            Assert.IsFalse(BossFight.PhaseBreak(none, destroy, hard: false, hearts: 3, maxHearts: 3).dropHeart, "calm road at maximum");
            Assert.IsFalse(BossFight.PhaseBreak(none, destroy, hard: true, hearts: 1, maxHearts: 3).dropHeart, "never on the hard road");
            Assert.IsTrue(BossFight.PhaseBreak(none, destroy, hard: true, hearts: 1, maxHearts: 3).dropCrate, "a crate always drops");
        }

        [Test]
        public void ThePhaseBreakClosesEveryCore()
        {
            var boss = Make(new TestDef { coreOpen = true });
            boss.Damage(2);
            boss.Tick(Dt, Vector2.zero, true);
            Assert.IsTrue(boss.Staggering);
            Assert.IsTrue(boss.Parts.All(p => !p.Open));
        }

        // ------------------------------------------------------------------ the bar

        [Test]
        public void TheBarHasOneSegmentPerPhaseWithATwoPixelGap()
        {
            var boss = Make();
            var segments = BossFight.BarSegments(boss, 100f, 2f);
            Assert.AreEqual(3, segments.Count);
            // The first phase is the right-most segment, so the bar empties from right to left.
            Assert.AreEqual(2f, segments[0].x - (segments[1].x + segments[1].width), 1e-4f);
            Assert.AreEqual(2f, segments[1].x - (segments[2].x + segments[2].width), 1e-4f);
            Assert.AreEqual(0f, segments[2].x, 1e-4f);
            Assert.AreEqual(100f, segments[0].x + segments[0].width, 1e-3f);
            Assert.That(segments.Select(s => s.fill), Is.All.EqualTo(1f), "all full at the start");

            boss.Damage(1);
            segments = BossFight.BarSegments(boss, 100f, 2f);
            Assert.AreEqual(0.5f, segments[0].fill, 1e-4f, "the current segment drains");
            Assert.AreEqual(1f, segments[1].fill, 1e-4f);
        }

        [Test]
        public void SpentSegmentsAreEmptyAndAOnePhaseBossHasOneSegment()
        {
            var boss = Make();
            boss.Damage(2);
            for (int i = 0; i < 200 && boss.PhaseIndex == 0; i++) boss.Tick(Dt, Vector2.zero, true);
            var segments = BossFight.BarSegments(boss, 100f, 2f);
            Assert.AreEqual(0f, segments[0].fill);
            Assert.AreEqual(1f, segments[1].fill);

            var legacy = Boss.Create(root.transform, BossSpec.ForWorld(1), 1, Themes.ForWorld(1));
            Assert.AreEqual(1, BossFight.BarSegments(legacy, 100f, 2f).Count);
        }

        // ------------------------------------------------------------------ BossDef

        [Test]
        public void ForWorldReturnsTheLegacyBossForAWorldWithNoNewBoss()
        {
            for (int world = 1; world <= 6; world++)
            {
                if (BossDef.All.Any(d => d.World == world)) continue;
                var def = BossDef.ForWorld(world);
                Assert.IsInstanceOf<LegacyBoss>(def, $"world {world}");
                Assert.AreEqual(world, def.World);
                Assert.AreEqual(BossSpec.ForWorld(world).name, def.Name);
            }
        }

        [Test]
        public void EveryDiscoveredBossDefHasAUniqueWorld()
        {
            var worlds = BossDef.All.Select(d => d.World).ToList();
            Assert.AreEqual(worlds.Count, worlds.Distinct().Count());
            foreach (var d in BossDef.All) Assert.That(d.World, Is.InRange(1, 6));
        }

        [Test]
        public void TheLegacyBossIsOnePhaseWithTodaysHealthAndClock()
        {
            for (int world = 1; world <= 6; world++)
            {
                var boss = Boss.Create(root.transform, BossSpec.ForWorld(world), world, Themes.ForWorld(world));
                Assert.AreEqual(1, boss.Phases.Count);
                Assert.AreEqual(BossSpec.HpForWorld(world), boss.Hp);
                Assert.AreEqual(1, boss.Parts.Count);
                Assert.IsTrue(boss.Parts[0].IsCore && boss.Parts[0].Open, "the whole body is one open core");
                if (BossDef.ForWorld(world) is LegacyBoss) // a world with a new boss has its own clock
                    Assert.AreEqual(Tuning.I.legacyBossTime, StagePicker.BossTime(world), 1e-4f);
            }
        }

        [Test]
        public void TheClockDefaultsToBossTimeAndCanBeOverriddenPerBoss()
        {
            Assert.AreEqual(240f, Tuning.I.bossTime, 1e-4f);
            Assert.AreEqual(Tuning.I.bossTime, new TestDef().Clock, 1e-4f);
            Assert.AreEqual(Tuning.I.bossTime * Tuning.I.hardTimeScale, new TestDef().ClockFor(true), 1e-4f);
        }

        [Test]
        public void HardHealthScalesEveryPhaseSegmentUp()
        {
            var calm = Make(new TestDef());
            var hard = Make(new TestDef(), hard: true);
            Assert.AreEqual(Mathf.CeilToInt(2 * Tuning.I.hardBossHpScale), hard.PhaseHp);
            Assert.Greater(hard.MaxHp, calm.MaxHp);
        }
    }
}
