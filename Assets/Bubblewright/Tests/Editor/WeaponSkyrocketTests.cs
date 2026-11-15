using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>W14 Skyrocket: a rocket climbs until it touches something, then bursts and hits everything in a ring once.</summary>
    public class WeaponSkyrocketTests
    {
        const float Dt = 1f / 60f;

        /// <summary>The muzzle in the tests: the middle of a tile column, a little above the floor.</summary>
        static readonly Vector2 Muzzle = new Vector2(0.5f, 1.5f);

        GameObject root;
        StageMap previousMap;
        List<Bubble> bubbles;
        List<Roller> rollers;
        List<Bullet> bullets;
        WeaponWorld world;
        Run run;
        WeaponSpec rocket;
        Skyrocket.Behaviour behaviour;
        WeaponContext ctx;
        readonly List<Bubble> hitBubbles = new List<Bubble>();
        readonly List<Roller> hitRollers = new List<Roller>();
        readonly List<(int c, int r)> tileHits = new List<(int, int)>();
        System.Action<Bubble> onPop;
        int steelHits;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("WeaponSkyrocketTests");
            previousMap = Game.Map;
            SetMap(StageMap.Flat());
            bubbles = new List<Bubble>();
            rollers = new List<Roller>();
            bullets = new List<Bullet>();
            hitBubbles.Clear();
            hitRollers.Clear();
            tileHits.Clear();
            onPop = null;
            steelHits = 0;
            world = MakeWorld(null);

            rocket = WeaponSpec.ById("rocket");
            Assert.NotNull(rocket, "the Skyrocket is in WeaponSpec.All");
            run = new Run(1);
            Assert.IsTrue(run.GiveWeapon(rocket));
            behaviour = (Skyrocket.Behaviour)rocket.CreateBehaviour();
            ctx = new WeaponContext
            {
                CanFire = true,
                Muzzle = Muzzle,
                Up = Vector2.up,
                Spec = rocket,
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
            return new WeaponWorld(bubbles, rollers, bullets, boss == null ? (System.Func<Boss>)null : () => boss, root.transform)
            {
                OnHitBubble = b =>
                {
                    hitBubbles.Add(b);
                    if (!b.AbsorbHit())
                    {
                        b.Swell(Tuning.I.popSwell);
                        onPop?.Invoke(b);
                    }
                },
                OnHitRoller = r =>
                {
                    hitRollers.Add(r);
                    if (!r.AbsorbHit()) r.Swell(Tuning.I.popSwell);
                },
                OnHitTile = (c, r) =>
                {
                    tileHits.Add((c, r));
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

        Bubble AddBubble(Vector2 at, int tier = 3, bool ceramic = false, bool steel = false, BubbleKind kind = BubbleKind.Gloop)
        {
            var b = Bubble.Create(root.transform, kind, tier, at, Vector2.zero, false, ceramic, steel, steel);
            bubbles.Add(b);
            return b;
        }

        Roller AddRoller(float x, bool steel = false)
        {
            var r = Roller.Create(root.transform, 0, x, 0f, 1, steel);
            rollers.Add(r);
            return r;
        }

        void Frame(bool pressed)
        {
            ctx.Pressed = pressed;
            ctx.Held = pressed;
            behaviour.Tick(ctx, Dt);
        }

        /// <summary>Launches one rocket and flies it until it bursts (at most 3 s). Returns the burst.</summary>
        Skyrocket.Burst FireAndWait()
        {
            Frame(true);
            Assert.NotNull(behaviour.Flight, "a rocket left");
            for (int i = 0; i < 180 && behaviour.Flight != null; i++) Frame(false);
            Assert.IsNull(behaviour.Flight, "the rocket burst");
            Assert.IsTrue(behaviour.LastBurst.HasValue);
            return behaviour.LastBurst.Value;
        }

        void WaitCooldown()
        {
            for (int i = 0; i < 120 && behaviour.Cooldown > 0f; i++) Frame(false);
        }

        // ------------------------------------------------------------------ ammo and firing

        [Test]
        public void AFullCrateHoldsFourRockets()
        {
            Assert.AreEqual(Tuning.I.rocketAmmo, rocket.MaxAmmo);
            Assert.AreEqual(4, rocket.MaxAmmo, "the sheet's number");
            Assert.AreEqual(WeaponSlot.Weapon, rocket.Slot);
            Assert.AreEqual(WeaponTier.Rare, rocket.Tier);
            Assert.AreEqual(rocket.MaxAmmo, run.WeaponAmmo);
        }

        [Test]
        public void ARocketIsSpentWhenItBurstsAndTheSlotEmptiesAtZero()
        {
            int full = run.WeaponAmmo;
            Frame(true);
            Assert.AreEqual(full, run.WeaponAmmo, "nothing is spent while it flies");
            for (int i = 0; i < 180 && behaviour.Flight != null; i++) Frame(false);
            Assert.AreEqual(full - 1, run.WeaponAmmo, "spent on the burst");

            for (int n = 1; n < full; n++)
            {
                WaitCooldown();
                FireAndWait();
            }
            Assert.AreEqual(0, run.WeaponAmmo);
            Assert.IsNull(run.Weapon, "the slot empties at zero");
        }

        [Test]
        public void OneRocketFliesAtATimeAndTheButtonIsDimMeanwhile()
        {
            Assert.IsFalse(behaviour.Dimmed);
            Frame(true);
            var first = behaviour.Flight;
            Assert.NotNull(first);
            Assert.IsTrue(behaviour.Dimmed);
            for (int i = 0; i < 3; i++) Frame(true);
            Assert.AreSame(first, behaviour.Flight, "presses while it flies do nothing");
        }

        [Test]
        public void TheNextRocketWaitsForTheCooldown()
        {
            AddBubble(Muzzle + new Vector2(0f, 1f)); // so the first one bursts at once
            FireAndWait();
            Assert.Greater(behaviour.Cooldown, 0f);
            Assert.IsTrue(behaviour.Dimmed);
            Frame(true);
            Assert.IsNull(behaviour.Flight, "still cooling down");
            WaitCooldown();
            Frame(true);
            Assert.NotNull(behaviour.Flight, "ready again after rocketCooldown");
        }

        [Test]
        public void NothingLeavesWhenTheMuzzleIsInsideATile()
        {
            ctx.CanFire = false;
            Frame(true);
            Assert.IsNull(behaviour.Flight);
        }

        [Test]
        public void ItSpeedsUpFromRocketSpeedToRocketTopSpeed()
        {
            var t = Tuning.I;
            var r = Skyrocket.Launch(new Vector2(0.5f, 0.5f), Vector2.up);
            Assert.AreEqual(t.rocketSpeed, r.Speed, 1e-4f);
            Skyrocket.Fly(world, r, Dt, out _);
            Assert.Greater(r.Speed, t.rocketSpeed);
            for (int i = 0; i < 60; i++) Skyrocket.Fly(world, r, Dt, out _);
            Assert.AreEqual(t.rocketTopSpeed, r.Speed, 1e-4f, "never faster than the top speed");
        }

        // ------------------------------------------------------------------ the burst

        [Test]
        public void ItBurstsOnTheFirstBubbleAndPopsEverythingInsideTheRingOnce()
        {
            var t = Tuning.I;
            var target = AddBubble(new Vector2(Muzzle.x, 8f));
            var nearLeft = AddBubble(new Vector2(Muzzle.x - t.rocketRadius * 0.6f, 8.3f));
            var nearRight = AddBubble(new Vector2(Muzzle.x + t.rocketRadius * 0.6f, 7.6f));
            var far = AddBubble(new Vector2(Muzzle.x + t.rocketRadius + 2f, 8f));
            var burst = FireAndWait();
            Assert.IsTrue(target.Popped && nearLeft.Popped && nearRight.Popped, "everything inside the ring pops");
            Assert.IsFalse(far.Popped, "outside the ring");
            Assert.AreEqual(3, burst.bubbles);
            Assert.AreEqual(3, hitBubbles.Count, "each was hit once");
            Assert.Less(burst.centre.y, 8f, "it burst where it touched the first bubble, below its middle");
        }

        [Test]
        public void BubblesBornFromTheBurstAreNotHitByIt()
        {
            onPop = b => AddBubble(b.Position + new Vector2(0.3f, 0f));
            AddBubble(new Vector2(Muzzle.x, 8f));
            var burst = FireAndWait();
            Assert.AreEqual(2, bubbles.Count, "the pop made a child inside the ring");
            Assert.IsFalse(bubbles[1].Popped, "the child is immune to that burst");
            Assert.AreEqual(1, burst.bubbles);
        }

        [Test]
        public void ItBurstsOnSteelAndTheSteelIsUnharmed()
        {
            var steel = AddBubble(new Vector2(Muzzle.x, 8f), steel: true);
            var beside = AddBubble(new Vector2(Muzzle.x + 1.2f, 8f));
            var above = AddBubble(new Vector2(Muzzle.x, 12.5f));
            var burst = FireAndWait();
            Assert.IsFalse(steel.Popped, "steel is unharmed");
            Assert.AreEqual(1, burst.steel);
            Assert.Greater(steelHits, 0, "steel sparkles");
            Assert.IsTrue(beside.Popped, "the burst still hits what is beside it");
            Assert.IsFalse(above.Popped, "it never got past the steel");
        }

        [Test]
        public void AFadedGhostLetsTheRocketThrough()
        {
            var ghost = AddBubble(new Vector2(Muzzle.x, 5f), tier: 2, kind: BubbleKind.Ghost);
            for (int i = 0; i < 156 && ghost.CanBeHit; i++) ghost.Tick(Dt);
            ghost.transform.position = new Vector3(Muzzle.x, 5f, 0f);
            Assume.That(ghost.CanBeHit, Is.False);
            var plain = AddBubble(new Vector2(Muzzle.x, 11f));
            FireAndWait();
            Assert.IsFalse(ghost.Popped);
            Assert.IsTrue(plain.Popped, "it flew on through the Ghost");
        }

        [Test]
        public void TheBurstIsOneHitOnCeramic()
        {
            var c = AddBubble(new Vector2(Muzzle.x, 8f), tier: 2, ceramic: true);
            FireAndWait();
            Assert.IsFalse(c.Popped, "one hit cracks it");
            Assert.AreEqual(1, hitBubbles.Count);
            WaitCooldown();
            FireAndWait();
            Assert.IsTrue(c.Popped, "the second burst breaks it");
        }

        [Test]
        public void RollingBalloonsInTheRingPopAndSteelOnesAreUnharmed()
        {
            var plain = AddRoller(Muzzle.x + 1f);
            var steel = AddRoller(Muzzle.x - 1f, steel: true);
            var burst = Skyrocket.Explode(world, new Vector2(Muzzle.x, 0.5f));
            Assert.AreEqual(1, burst.rollers);
            Assert.IsTrue(plain.Popped);
            Assert.IsFalse(steel.Popped);
            Assert.AreEqual(1, burst.steel);
        }

        [Test]
        public void ItBurstsOnARollingBalloon()
        {
            var r = AddRoller(Muzzle.x);
            ctx.Muzzle = new Vector2(Muzzle.x, 0.1f);
            var burst = FireAndWait();
            Assert.IsTrue(r.Popped);
            Assert.Less(burst.centre.y, 2f, "it went off on the balloon, not the ceiling");
        }

        [Test]
        public void BossBulletsInTheRingAreDestroyed()
        {
            var bullet = Bullet.Create(root.transform, new Vector2(Muzzle.x + 1f, 8f), Vector2.zero, Themes.ForWorld(1));
            bullets.Add(bullet);
            AddBubble(new Vector2(Muzzle.x, 8f));
            var burst = FireAndWait();
            Assert.IsTrue(bullet.Dead);
            Assert.AreEqual(1, burst.bullets);
        }

        [Test]
        public void ItBurstsOnTheBossForRocketBossDamage()
        {
            var boss = Boss.Create(root.transform, BossSpec.ForWorld(1), 1, Themes.ForWorld(1));
            var y = typeof(Boss).GetField("y", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(y, "Boss.y must exist.");
            y.SetValue(boss, 9f); // inside the arena, as when it has entered
            world.Destroy();
            world = MakeWorld(boss);
            ctx.World = world;
            int total = 0;
            world.OnDamageBoss = (n, _) => total += n;
            ctx.Muzzle = new Vector2(boss.Body.center.x, 1.5f);
            var burst = FireAndWait();
            Assert.IsTrue(burst.boss);
            Assert.LessOrEqual(burst.centre.y, boss.Body.yMin + 0.21f, "it burst on the boss's underside (within one flight step)");
            Assert.AreEqual(Mathf.FloorToInt(Tuning.I.rocketBossDamage + 1e-3f), total);
            Assert.AreEqual(3, total, "the sheet's number: a full crate takes 12");
        }

        // ------------------------------------------------------------------ tiles, ceiling, floor

        [Test]
        public void ItBurstsUnderAPlatformAndCannotReachAboveIt()
        {
            var map = StageMap.Flat();
            for (int c = 10; c < 22; c++) map.Set(c, 9, new Cell { kind = TileKind.Solid });
            SetMap(map);
            var above = AddBubble(new Vector2(Muzzle.x, 12.5f));
            var burst = FireAndWait();
            Assert.AreEqual(9f, burst.centre.y, 0.05f, "burst at the platform's underside");
            Assert.IsFalse(above.Popped);
            Assert.AreEqual(0, tileHits.Count, "solid tiles take no hits");
        }

        [Test]
        public void BreakableBlocksInTheBurstTakeOneHitEach()
        {
            var map = StageMap.Flat();
            int col = StageMap.Col(Muzzle.x);
            map.Set(col, 9, new Cell { kind = TileKind.Breakable, hp = 5 });
            map.Set(col + 1, 9, new Cell { kind = TileKind.Breakable, hp = 5 });
            map.Set(col + 8, 9, new Cell { kind = TileKind.Breakable, hp = 5 });
            SetMap(map);
            var burst = FireAndWait();
            Assert.AreEqual(2, burst.tiles);
            Assert.AreEqual(4, map.Get(col, 9).hp, "the block it hit took one hit");
            Assert.AreEqual(4, map.Get(col + 1, 9).hp, "the block beside it took one hit");
            Assert.AreEqual(5, map.Get(col + 8, 9).hp, "out of reach");
        }

        [Test]
        public void WithNothingInTheWayItBurstsOnTheCeiling()
        {
            var burst = FireAndWait();
            Assert.AreEqual(Tuning.I.arenaHeight, burst.centre.y, 1e-3f);
        }

        [Test]
        public void FiredDownInTheAirItBurstsOnTheFloor()
        {
            ctx.ShootDown = true;
            ctx.InAir = true;
            ctx.Up = Vector2.down;
            ctx.Muzzle = new Vector2(Muzzle.x, 4f);
            var under = AddBubble(new Vector2(Muzzle.x + 1f, 0.5f));
            var burst = FireAndWait();
            Assert.AreEqual(0f, burst.centre.y, 1e-3f, "it went down to the floor");
            Assert.IsTrue(under.Popped, "the floor burst hits what is near it");
        }

        [Test]
        public void ALeftoverRocketFizzlesAndHitsNothing()
        {
            var b = AddBubble(new Vector2(Muzzle.x, 12f));
            Frame(true);
            Assert.IsTrue(behaviour.Fizzle());
            Assert.IsNull(behaviour.Flight);
            for (int i = 0; i < 60; i++) Frame(false);
            Assert.IsFalse(b.Popped);
            Assert.IsFalse(behaviour.Fizzle(), "nothing left to fizzle");
        }
    }
}
