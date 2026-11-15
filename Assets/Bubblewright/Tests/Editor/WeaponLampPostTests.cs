using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>W16 Lamp Post: planted standing still, then it fires its own harpoon straight up on a timer.</summary>
    public class WeaponLampPostTests
    {
        const float Dt = 1f / 60f;

        /// <summary>Wick's feet in the tests: the middle of a tile column, on the floor.</summary>
        static readonly Vector2 Feet = new Vector2(0.5f, 0f);

        GameObject root;
        StageMap previousMap;
        List<Bubble> bubbles;
        List<Roller> rollers;
        WeaponWorld world;
        Run run;
        WeaponSpec lamp;
        LampPost.Behaviour behaviour;
        WeaponContext ctx;
        readonly List<Bubble> hitBubbles = new List<Bubble>();
        readonly List<Roller> hitRollers = new List<Roller>();
        readonly List<Vector2Int> tileHits = new List<Vector2Int>();
        int steelHits;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("WeaponLampPostTests");
            previousMap = Game.Map;
            SetMap(StageMap.Flat());
            bubbles = new List<Bubble>();
            rollers = new List<Roller>();
            hitBubbles.Clear();
            hitRollers.Clear();
            tileHits.Clear();
            steelHits = 0;
            world = MakeWorld(null);

            lamp = WeaponSpec.ById("lamp");
            Assert.NotNull(lamp, "the Lamp Post is in WeaponSpec.All");
            run = new Run(1);
            Assert.IsTrue(run.GiveWeapon(lamp));
            behaviour = (LampPost.Behaviour)lamp.CreateBehaviour();
            ctx = new WeaponContext
            {
                CanFire = true,
                Muzzle = Feet,
                Up = Vector2.up,
                Spec = lamp,
                World = world,
                Run = run,
                Rng = new System.Random(1),
            };
            var spend = typeof(WeaponContext).GetField("spend", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(spend, "WeaponContext.spend must exist.");
            spend.SetValue(ctx, (System.Func<int, bool>)(n => run.SpendAmmo(WeaponSlot.Weapon, n)));
        }

        WeaponWorld MakeWorld(Boss boss)
        {
            return new WeaponWorld(bubbles, rollers, null, boss == null ? (System.Func<Boss>)null : () => boss, root.transform)
            {
                OnHitBubble = b =>
                {
                    hitBubbles.Add(b);
                    if (!b.AbsorbHit()) b.Swell(Tuning.I.popSwell);
                },
                OnHitRoller = r =>
                {
                    hitRollers.Add(r);
                    if (!r.AbsorbHit()) r.Swell(Tuning.I.popSwell);
                },
                OnHitTile = (c, r) =>
                {
                    tileHits.Add(new Vector2Int(c, r));
                    Game.Map.Damage(c, r);
                },
                OnSteel = _ => steelHits++,
            };
        }

        [TearDown]
        public void TearDown()
        {
            behaviour?.Dispose();
            world.Destroy();
            SetMap(previousMap);
            Object.DestroyImmediate(root);
        }

        static void SetMap(StageMap map)
        {
            var property = typeof(Game).GetProperty("Map", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(property, "Game.Map must exist.");
            property.SetValue(null, map);
        }

        Bubble AddBubble(Vector2 at, int tier = 2, bool ceramic = false, bool steel = false)
        {
            var b = Bubble.Create(root.transform, BubbleKind.Gloop, tier, at, Vector2.zero, false, ceramic, steel, steel);
            bubbles.Add(b);
            return b;
        }

        Roller AddRoller(float x, bool steel = false)
        {
            var r = Roller.Create(root.transform, 0, x, 0f, 1, steel);
            rollers.Add(r);
            return r;
        }

        LampPostField Field => behaviour.Field;

        void Frame(bool pressed)
        {
            ctx.Pressed = pressed;
            ctx.Held = pressed;
            behaviour.Tick(ctx, Dt);
        }

        void Frames(float seconds)
        {
            int n = Mathf.CeilToInt(seconds / Dt);
            for (int i = 0; i < n; i++) Frame(false);
        }

        /// <summary>Presses once and waits out the planting time.</summary>
        PlantedPost Plant()
        {
            Frame(true);
            Frames(Tuning.I.lampPlantTime + Dt);
            Assume.That(Field.Count, Is.GreaterThan(0));
            return Field.Posts[Field.Count - 1];
        }

        /// <summary>Sets a Tuning number for one test and puts it back afterwards.</summary>
        static void WithTuning(System.Action change, System.Action restore, System.Action body)
        {
            change();
            try { body(); }
            finally { restore(); }
        }

        // ------------------------------------------------------------------ ammo and planting

        [Test]
        public void ACrateHoldsLampAmmoPosts()
        {
            Assert.AreEqual(Tuning.I.lampAmmo, lamp.MaxAmmo);
            Assert.AreEqual(Tuning.I.lampAmmo, run.WeaponAmmo);
            Assert.AreEqual(WeaponSlot.Weapon, lamp.Slot);
            Assert.AreEqual(WeaponTier.Rare, lamp.Tier);
        }

        [Test]
        public void PlantingTakesLampPlantTimeAndThenSpendsOnePost()
        {
            Frame(true);
            Assert.AreEqual(0, Field.Count, "nothing appears at once");
            Assert.GreaterOrEqual(behaviour.Planting, 0f);
            Assert.AreEqual(Tuning.I.lampAmmo, run.WeaponAmmo, "nothing is spent while planting");
            Assert.IsTrue(behaviour.Dimmed, "the button is dim while he plants");
            Frames(Tuning.I.lampPlantTime + Dt);
            Assert.AreEqual(1, Field.Count);
            Assert.AreEqual(Tuning.I.lampAmmo - 1, run.WeaponAmmo);
            Assert.AreEqual(Feet.x, Field.Posts[0].Position.x, 1e-4f, "at his feet");
            Assert.AreEqual(0f, Field.Posts[0].Position.y, 1e-4f, "on the floor");
        }

        [Test]
        public void PlantingHoldsWickStillAndLetsGoAfter()
        {
            var fx = Effects.Create(root.transform);
            var wick = Wick.Create(root.transform, fx);
            wick.ResetForStage(Feet.x, 0f, 3);
            ctx.Wick = wick;
            Frame(true);
            Assert.IsTrue(wick.HoldStill, "he stands still while planting");
            Frames(Tuning.I.lampPlantTime + Dt);
            Assert.IsFalse(wick.HoldStill, "and is free again once the post is up");
            Assert.AreEqual(1, Field.Count);
        }

        [Test]
        public void ItCannotBePlantedInTheAir()
        {
            ctx.InAir = true;
            Frame(true);
            Frames(Tuning.I.lampPlantTime + Dt);
            Assert.AreEqual(0, Field.Count);
            Assert.AreEqual(Tuning.I.lampAmmo, run.WeaponAmmo, "nothing is spent");
            Assert.IsTrue(behaviour.Dimmed, "the button is dim in the air");
        }

        [Test]
        public void LeavingTheGroundWhilePlantingCallsItOffAndSpendsNothing()
        {
            Frame(true);
            ctx.InAir = true; // knocked up by a hit
            Frames(Tuning.I.lampPlantTime + Dt);
            Assert.AreEqual(0, Field.Count);
            Assert.Less(behaviour.Planting, 0f);
            Assert.AreEqual(Tuning.I.lampAmmo, run.WeaponAmmo);
        }

        [Test]
        public void WithLampMaxPlacedUpTheButtonIsDimAndDoesNothing()
        {
            for (int i = 0; i < LampPost.MaxPlaced; i++) Plant();
            int ammo = run.WeaponAmmo;
            Assume.That(ammo, Is.GreaterThan(0), "lampAmmo is more than lampMaxPlaced");
            Assert.IsTrue(behaviour.Dimmed);
            Frame(true);
            Frames(Tuning.I.lampPlantTime + Dt);
            Assert.AreEqual(LampPost.MaxPlaced, Field.Count);
            Assert.AreEqual(ammo, run.WeaponAmmo);
        }

        [Test]
        public void TheLastPostEmptiesTheSlot()
        {
            WithTuning(() => Tuning.I.lampMaxPlaced = 10, () => Tuning.I.lampMaxPlaced = 1, () =>
            {
                for (int i = 0; i < Tuning.I.lampAmmo; i++) Plant();
                Assert.AreEqual(0, run.WeaponAmmo);
                Assert.IsNull(run.Weapon, "the slot is empty at zero");
            });
        }

        // ------------------------------------------------------------------ firing

        [Test]
        public void ItFiresAfterRisingAndThenEveryLampInterval()
        {
            var p = Plant();
            float sincePlant = p.Age;
            Frames(LampPostField.RiseTime - sincePlant + 2 * Dt);
            Assert.AreEqual(1, p.Shots, "the first cable goes up as soon as it has risen");
            Frames(Tuning.I.lampInterval - 4 * Dt);
            Assert.AreEqual(1, p.Shots);
            Frames(4 * Dt);
            Assert.AreEqual(2, p.Shots);
        }

        [Test]
        public void ItSinksAwayAfterItsLastShot()
        {
            var p = Plant();
            Frames(LampPostField.RiseTime + Tuning.I.lampInterval * (Tuning.I.lampShots - 1) - p.Age + 3 * Dt);
            Assert.AreEqual(Tuning.I.lampShots, p.Shots);
            Assert.IsTrue(p.Retiring);
            Assert.AreEqual(0, Field.Standing, "a sinking post no longer counts against lampMaxPlaced");
            Frames(LampPostField.FlickerTime + LampPostField.SinkTime + 2f);
            Assert.AreEqual(0, Field.Count, "gone");
            Assert.AreEqual(Tuning.I.lampShots, p.Shots, "it fired no more after the last");
        }

        [Test]
        public void ItSinksAwayAfterLampLifeEvenWithShotsLeft()
        {
            float interval = Tuning.I.lampInterval;
            WithTuning(() => Tuning.I.lampInterval = 100f, () => Tuning.I.lampInterval = interval, () =>
            {
                var p = Plant();
                Frames(Tuning.I.lampLife - p.Age - 0.1f);
                Assert.IsFalse(p.Retiring);
                Frames(0.2f);
                Assert.IsTrue(p.Retiring);
                Assert.AreEqual(1, p.Shots);
            });
        }

        [Test]
        public void ItsOwnCablePopsABubbleAboveIt()
        {
            var b = AddBubble(new Vector2(Feet.x, 8f));
            Plant();
            Frames(1f);
            CollectionAssert.Contains(hitBubbles, b);
            Assert.IsTrue(b.Popped);
        }

        [Test]
        public void ItsCableHasNoRunUpgrades()
        {
            var p = Plant();
            Assert.AreEqual(1f, p.Harpoon.SpeedMultiplier);
            Assert.AreEqual(1f, p.Harpoon.WidthMultiplier);
            Assert.AreEqual(0f, p.Harpoon.HoldTime);
        }

        [Test]
        public void ItKeepsFiringAfterTheSlotEmpties()
        {
            WithTuning(() => Tuning.I.lampMaxPlaced = 10, () => Tuning.I.lampMaxPlaced = 1, () =>
            {
                for (int i = 0; i < Tuning.I.lampAmmo; i++) Plant();
                Assume.That(run.Weapon, Is.Null);
                var b = AddBubble(new Vector2(Feet.x, 8f));
                Frames(1f);
                CollectionAssert.Contains(hitBubbles, b, "posts outlive the weapon");
            });
        }

        // ------------------------------------------------------------------ rules

        [Test]
        public void SteelRingsItsCableOff()
        {
            var steel = AddBubble(new Vector2(Feet.x, 6f), steel: true);
            var above = AddBubble(new Vector2(Feet.x, 12f));
            Plant();
            Frames(1f);
            Assert.Greater(steelHits, 0);
            Assert.IsFalse(steel.Popped);
            Assert.IsFalse(above.Popped, "the cable stopped at the steel ball");
        }

        [Test]
        public void EachCableIsOneHitOnCeramic()
        {
            var c = AddBubble(new Vector2(Feet.x, 8f), ceramic: true);
            var p = Plant();
            Frames(LampPostField.RiseTime + 0.6f);
            Assert.AreEqual(1, p.Shots);
            Assert.AreEqual(1, hitBubbles.Count);
            Assert.IsFalse(c.Popped, "the first cable cracks it");
            Frames(Tuning.I.lampInterval);
            Assert.IsTrue(c.Popped, "the second breaks it");
        }

        [Test]
        public void ItsCableHitsARollingBalloonAtTheFoot()
        {
            var r = AddRoller(Feet.x);
            Plant();
            Frames(LampPostField.RiseTime + 0.1f);
            CollectionAssert.Contains(hitRollers, r);
        }

        [Test]
        public void EachCableThatReachesTheBossTakesLampBossDamage()
        {
            var boss = Boss.Create(root.transform, BossSpec.ForWorld(1), 1, Themes.ForWorld(1));
            world.Destroy();
            world = MakeWorld(boss);
            ctx.World = world;
            int total = 0;
            world.OnDamageBoss = (n, _) => total += n;
            var y = typeof(Boss).GetField("y", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(y, "Boss.y must exist.");
            y.SetValue(boss, 8f); // inside the arena, as when it has entered
            ctx.Muzzle = new Vector2(boss.Body.center.x, 0f);
            var p = Plant();
            Frames(LampPostField.RiseTime + 1f);
            Assert.AreEqual(1, p.Shots);
            Assert.AreEqual(Mathf.FloorToInt(Tuning.I.lampBossDamage + 1e-3f), total);
        }

        [Test]
        public void ItsCableStopsUnderAPlatformAndChipsABreakableBlock()
        {
            var map = StageMap.Flat();
            int c = StageMap.Col(Feet.x);
            map.Set(c, 6, new Cell { kind = TileKind.Breakable, hp = 3 });
            SetMap(map);
            var above = AddBubble(new Vector2(Feet.x, 10f));
            var p = Plant();
            Frames(LampPostField.RiseTime + 0.6f);
            Assert.AreEqual(1, p.Shots);
            Assert.AreEqual(1, tileHits.Count, "one hit on the block");
            Assert.IsFalse(above.Popped, "the block stopped the cable");
        }

        [Test]
        public void PlantedOnABreakableBlockItFallsWithTheBlock()
        {
            var map = StageMap.Flat();
            int c = StageMap.Col(Feet.x);
            map.Set(c, 3, new Cell { kind = TileKind.Breakable, hp = 1 });
            SetMap(map);
            ctx.Muzzle = new Vector2(Feet.x, 4f);
            var p = Plant();
            Assert.AreEqual(4f, p.Position.y, 1e-4f, "standing on the block");
            map.Damage(c, 3);
            Assume.That(map.Get(c, 3).kind, Is.EqualTo(TileKind.Empty));
            Frames(1.5f);
            Assert.AreEqual(0f, p.Position.y, 1e-4f, "it fell to the floor");
            Assert.AreEqual(1, Field.Count);
        }

        [Test]
        public void ThePostRisesOutOfTheFloor()
        {
            var p = Plant();
            p.Age = 0f;
            Assert.Less(LampPostField.ShownRows(p), LampPostField.HeightPixels);
            p.Age = LampPostField.RiseTime;
            Assert.AreEqual(LampPostField.HeightPixels, LampPostField.ShownRows(p));
            Assert.AreEqual(LampPostField.HeightPixels, LampPostField.Rows(true, false, 8).Length, "lamp, pole and foot");
        }

        [Test]
        public void ItsSmallLightsGoOutOnePerShot()
        {
            int shots = Tuning.I.lampShots;
            Assert.AreEqual(8, LampPostField.LightsOn(shots));
            Assert.AreEqual(0, LampPostField.LightsOn(0));
            if (shots == 8) Assert.AreEqual(7, LampPostField.LightsOn(7));
        }
    }
}
