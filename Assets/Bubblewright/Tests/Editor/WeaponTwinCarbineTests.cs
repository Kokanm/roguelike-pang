using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>W01 Twin Carbine: one bullet per press from two barrels in turn, tapped faster than held, shared hit rules.</summary>
    public class WeaponTwinCarbineTests
    {
        const float Dt = 1f / 60f;

        GameObject root;
        StageMap previousMap;
        List<Bubble> bubbles;
        List<Roller> rollers;
        WeaponWorld world;
        Run run;
        WeaponSpec carbine;
        WeaponBehaviour behaviour;
        WeaponContext ctx;
        readonly List<Vector2Int> tileHits = new List<Vector2Int>();
        int steelHits, flashes;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("WeaponTwinCarbineTests");
            previousMap = Game.Map;
            SetMap(StageMap.Flat());
            bubbles = new List<Bubble>();
            rollers = new List<Roller>();
            tileHits.Clear();
            steelHits = flashes = 0;
            world = new WeaponWorld(bubbles, rollers, null, null, root.transform)
            {
                OnHitBubble = b => { if (!b.AbsorbHit()) b.Swell(Tuning.I.popSwell); },
                OnHitRoller = r => { if (!r.AbsorbHit()) r.Swell(Tuning.I.popSwell); },
                OnHitTile = (c, r) => { tileHits.Add(new Vector2Int(c, r)); Game.Map.Damage(c, r); },
                OnSteel = _ => steelHits++,
            };

            carbine = WeaponSpec.ById("carbine");
            Assert.NotNull(carbine, "the Twin Carbine is in WeaponSpec.All");
            run = new Run(1);
            Assert.IsTrue(run.GiveWeapon(carbine));
            behaviour = carbine.CreateBehaviour();
            ctx = new WeaponContext
            {
                CanFire = true,
                Muzzle = new Vector2(0f, 2f),
                Up = Vector2.up,
                Spec = carbine,
                World = world,
                Run = run,
                Rng = new System.Random(1),
            };
            SetInternal("spend", (System.Func<int, bool>)(n => run.SpendAmmo(WeaponSlot.Weapon, n)));
            SetInternal("flash", (System.Action)(() => flashes++));
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

        void SetInternal(string field, object value)
        {
            var f = typeof(WeaponContext).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(f, $"WeaponContext.{field} must exist.");
            f.SetValue(ctx, value);
        }

        Bubble AddBubble(Vector2 at, int tier = 2, bool ceramic = false, bool steel = false)
        {
            var b = Bubble.Create(root.transform, BubbleKind.Gloop, tier, at, Vector2.zero, false, ceramic, steel, steel);
            bubbles.Add(b);
            return b;
        }

        /// <summary>One frame: the weapon reads its button, then the live bullets fly.</summary>
        void Frame(bool pressed, bool held)
        {
            ctx.Pressed = pressed;
            ctx.Held = held;
            behaviour.Tick(ctx, Dt);
            world.TickShots(Dt);
        }

        void Idle(int frames)
        {
            for (int i = 0; i < frames; i++) Frame(false, false);
        }

        int Fired => carbine.MaxAmmo - run.WeaponAmmo;

        // ------------------------------------------------------------------ table and ammo

        [Test]
        public void TheCarbineIsACommonWeaponSlotGunThatDrops()
        {
            Assert.AreEqual("Twin Carbine", carbine.Name);
            Assert.AreEqual(WeaponSlot.Weapon, carbine.Slot);
            Assert.AreEqual(WeaponTier.Common, carbine.Tier);
            Assert.IsTrue(carbine.Drops);
            Assert.AreEqual(Tuning.I.carbineAmmo, carbine.MaxAmmo, "a full crate comes from Tuning");
            Assert.AreEqual(Tuning.I.carbineAmmo, run.WeaponAmmo);
            Assert.LessOrEqual(WeaponArt.WidthOf(carbine.IconRows), 11);
            Assert.NotNull(WeaponArt.Held(carbine));
        }

        [Test]
        public void OnePressFiresOneBulletAndSpendsOne()
        {
            Frame(true, true);
            Assert.AreEqual(1, Fired);
            Assert.AreEqual(1, world.Shots.Count);
            Assert.AreEqual(1, flashes);
            Frame(false, false);
            Assert.AreEqual(1, Fired, "no second bullet without a second press");
        }

        [Test]
        public void TheSlotEmptiesAfterTheLastBullet()
        {
            int presses = 0;
            while (run.Weapon != null && presses < 1000)
            {
                Frame(true, true);
                Idle(10);
                presses++;
            }
            Assert.AreEqual(Tuning.I.carbineAmmo, presses, "one bullet per press, a full crate's worth");
            Assert.IsNull(run.Weapon);
            Assert.AreEqual(0, run.WeaponAmmo);
            Idle(60);
            Assert.AreEqual(0, world.Shots.Count);
            Frame(true, true);
            Assert.AreEqual(0, world.Shots.Count, "an empty carbine fires nothing");
        }

        [Test]
        public void BulletsLeaveFromTheTwoBarrelsInTurn()
        {
            var xs = new List<float>();
            for (int i = 0; i < 4; i++)
            {
                Frame(true, true);
                xs.Add(world.Shots[world.Shots.Count - 1].Position.x);
                Idle(10);
            }
            float gap = Tuning.I.carbineBarrelGap * Tuning.I.Pixel;
            Assert.Less(xs[0], ctx.Muzzle.x, "the first bullet leaves the left barrel");
            Assert.Greater(xs[1], ctx.Muzzle.x, "the second the right");
            Assert.AreEqual(gap, xs[1] - xs[0], Tuning.I.Pixel * 0.51f, "barrels are carbineBarrelGap pixels apart");
            Assert.AreEqual(xs[0], xs[2], 1e-4f);
            Assert.AreEqual(xs[1], xs[3], 1e-4f);
        }

        [Test]
        public void BulletsFlyUpAtCarbineSpeed()
        {
            Frame(true, true);
            var s = world.Shots[0];
            Assert.AreEqual(0f, s.Velocity.x, 1e-4f);
            Assert.AreEqual(Tuning.I.carbineSpeed, s.Velocity.y, 1e-4f);
            Assert.AreEqual(Tuning.I.carbineBossDamage, s.BossDamage, 1e-6f);
        }

        // ------------------------------------------------------------------ rate

        [Test]
        public void TappingFiresAtTheTopRate()
        {
            // A tap every other frame for one second: far faster than the cap, so the cap decides.
            for (int i = 0; i < 60; i++) Frame(i % 2 == 0, i % 2 == 0);
            int expected = Mathf.FloorToInt(Tuning.I.carbineRate) + 1; // the first press fires at once
            Assert.AreEqual(expected, Fired, 1, $"about {Tuning.I.carbineRate} a second when tapped");
        }

        [Test]
        public void HoldingFiresAtHalfTheTopRate()
        {
            Frame(true, true);
            for (int i = 1; i < 60; i++) Frame(false, true);
            int expected = Mathf.FloorToInt(Tuning.I.carbineRate * 0.5f) + 1;
            Assert.AreEqual(expected, Fired, 1, $"about {Tuning.I.carbineRate * 0.5f} a second when held");
        }

        [Test]
        public void TappingFastFiresMoreThanHolding()
        {
            for (int i = 0; i < 60; i++) Frame(i % 4 == 0, i % 4 < 2);
            int tapped = Fired;

            run.GiveWeapon(carbine); // top up and start over
            behaviour = carbine.CreateBehaviour();
            int before = run.WeaponAmmo;
            Frame(true, true);
            for (int i = 1; i < 60; i++) Frame(false, true);
            int held = before - run.WeaponAmmo;
            Assert.Greater(tapped, held);
        }

        [Test]
        public void ATapTooSoonIsNotLost()
        {
            Frame(true, true);
            Frame(false, false);
            Frame(true, true); // well inside the gap
            Assert.AreEqual(1, Fired);
            int frames = Mathf.CeilToInt(CarbineTapGap / Dt) + 1;
            Idle(frames);
            Assert.AreEqual(2, Fired, "the early tap fires as soon as the gap ends");
        }

        static float CarbineTapGap => 1f / Tuning.I.carbineRate;

        [Test]
        public void ThereIsNoCapOnBulletsInTheAir()
        {
            ctx.Muzzle = new Vector2(0f, 0.5f);
            for (int i = 0; i < 20; i++) Frame(i % 2 == 0, i % 2 == 0);
            Assert.AreEqual(Fired, world.Shots.Count, "every bullet fired is still flying");
            Assert.GreaterOrEqual(world.Shots.Count, 3);
        }

        [Test]
        public void NothingFiresOrIsSpentWhenTheMuzzleIsInATile()
        {
            ctx.CanFire = false;
            Frame(true, true);
            Frame(false, true);
            Assert.AreEqual(0, Fired);
            Assert.AreEqual(0, world.Shots.Count);
        }

        // ------------------------------------------------------------------ rules

        [Test]
        public void SteelSparksTheBulletOff()
        {
            var steel = AddBubble(new Vector2(ctx.Muzzle.x, 7f), tier: 3, steel: true);
            Frame(true, true);
            Idle(30);
            Assert.IsFalse(steel.Popped);
            Assert.AreEqual(1, steelHits);
            Assert.AreEqual(0, world.Shots.Count, "the bullet is spent");
        }

        [Test]
        public void TwoQuickTapsBreakCeramic()
        {
            var c = AddBubble(new Vector2(ctx.Muzzle.x, 7f), tier: 3, ceramic: true);
            Frame(true, true);
            Idle(Mathf.CeilToInt(CarbineTapGap / Dt));
            Frame(true, true);
            Idle(30);
            Assert.IsTrue(c.Popped, "one bullet per hit: two bullets break it");
        }

        [Test]
        public void AFullCrateTakesAboutTenBossHealth()
        {
            var boss = Boss.Create(root.transform, BossSpec.ForWorld(1), 1, Themes.ForWorld(1));
            var w = new WeaponWorld(bubbles, rollers, null, () => boss, root.transform);
            int total = 0;
            w.OnDamageBoss = (n, _) => total += n;
            for (int i = 0; i < Tuning.I.carbineAmmo; i++) w.WeaponHitBoss(Tuning.I.carbineBossDamage, Vector2.zero);
            Assert.AreEqual(Mathf.FloorToInt(Tuning.I.carbineAmmo * Tuning.I.carbineBossDamage + 1e-3f), total);
            Assert.That(total, Is.InRange(8, 12), "a full crate takes 8 to 12 boss health");
            w.Destroy();
        }

        [Test]
        public void ABulletStopsUnderAPlatform()
        {
            var map = StageMap.Flat();
            int col = StageMap.Col(ctx.Muzzle.x - Tuning.I.carbineBarrelGap * 0.5f * Tuning.I.Pixel);
            map.Set(col, 5, new Cell { kind = TileKind.Solid });
            SetMap(map);
            var above = AddBubble(new Vector2(StageMap.CenterX(col), 9f), tier: 3);
            Frame(true, true);
            var shot = world.Shots[0];
            Idle(30);
            Assert.IsFalse(shot.Alive);
            Assert.IsFalse(above.Popped, "the platform stopped it");
            Assert.AreEqual(0, tileHits.Count);
        }

        [Test]
        public void EachBulletHitsABreakableBlockOnce()
        {
            var map = StageMap.Flat();
            int left = StageMap.Col(TwinCarbine.BarrelPoint(ctx.Muzzle, 0).x);
            int right = StageMap.Col(TwinCarbine.BarrelPoint(ctx.Muzzle, 1).x);
            map.Set(left, 5, new Cell { kind = TileKind.Breakable, hp = 5 });
            if (right != left) map.Set(right, 5, new Cell { kind = TileKind.Breakable, hp = 5 });
            SetMap(map);
            Frame(true, true);
            Idle(30);
            Assert.AreEqual(1, tileHits.Count, "one bullet, one hit");
            Frame(true, true);
            Idle(30);
            Assert.AreEqual(2, tileHits.Count);
        }

        [Test]
        public void FallingWithDownHeldFiresDown()
        {
            ctx.Muzzle = new Vector2(0f, 8f);
            ctx.Up = Vector2.down;
            ctx.ShootDown = true;
            ctx.InAir = true;
            var below = AddBubble(new Vector2(TwinCarbine.BarrelPoint(ctx.Muzzle, 0).x, 3f), tier: 3);
            Frame(true, true);
            Assert.Less(world.Shots[0].Velocity.y, 0f, "the bullet goes down");
            Idle(30);
            Assert.IsTrue(below.Popped);
        }

        [Test]
        public void ItFiresUpInTheAir()
        {
            ctx.InAir = true;
            Frame(true, true);
            Assert.Greater(world.Shots[0].Velocity.y, 0f);
        }
    }
}
