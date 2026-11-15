using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>W08 Pop Mine: dropped at his feet, armed after a moment, set off by the first thing that touches it.</summary>
    public class WeaponPopMineTests
    {
        const float Dt = 1f / 60f;

        /// <summary>Wick's feet in the tests: the middle of a tile column, on the floor.</summary>
        static readonly Vector2 Feet = new Vector2(0.5f, 0f);

        GameObject root;
        StageMap previousMap;
        List<Bubble> bubbles;
        List<Roller> rollers;
        List<Bullet> bullets;
        WeaponWorld world;
        Run run;
        WeaponSpec mine;
        PopMine.Behaviour behaviour;
        WeaponContext ctx;
        readonly List<Bubble> hitBubbles = new List<Bubble>();
        readonly List<Roller> hitRollers = new List<Roller>();
        int steelHits;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("WeaponPopMineTests");
            previousMap = Game.Map;
            SetMap(StageMap.Flat());
            bubbles = new List<Bubble>();
            rollers = new List<Roller>();
            bullets = new List<Bullet>();
            hitBubbles.Clear();
            hitRollers.Clear();
            steelHits = 0;
            world = MakeWorld(null);

            mine = WeaponSpec.ById("mine");
            Assert.NotNull(mine, "the Pop Mine is in WeaponSpec.All");
            run = new Run(1);
            Assert.IsTrue(run.GiveWeapon(mine));
            behaviour = (PopMine.Behaviour)mine.CreateBehaviour();
            ctx = new WeaponContext
            {
                CanFire = true,
                Muzzle = Feet,
                Up = Vector2.up,
                Spec = mine,
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
                    if (!b.AbsorbHit()) b.Swell(Tuning.I.popSwell);
                },
                OnHitRoller = r =>
                {
                    hitRollers.Add(r);
                    if (!r.AbsorbHit()) r.Swell(Tuning.I.popSwell);
                },
                OnHitTile = (c, r) => Game.Map.Damage(c, r),
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

        Bubble AddBubble(Vector2 at, bool ceramic = false, bool steel = false)
        {
            var b = Bubble.Create(root.transform, BubbleKind.Gloop, 3, at, Vector2.zero, false, ceramic, steel, steel);
            bubbles.Add(b);
            return b;
        }

        Roller AddRoller(float x, bool steel = false)
        {
            var r = Roller.Create(root.transform, 0, x, 0f, 1, steel);
            rollers.Add(r);
            return r;
        }

        MineField Field => behaviour.Field;

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

        /// <summary>Drops a mine at Feet and waits until it is armed.</summary>
        Mine PlaceArmed()
        {
            Frame(true);
            var m = Field.Mines[Field.Count - 1];
            Frames(Tuning.I.mineArmTime + Dt);
            Assume.That(m.Armed, Is.True);
            return m;
        }

        /// <summary>A spot in the trigger radius, just above the mine.</summary>
        static Vector2 OnTheMine(Mine m) => m.Centre + new Vector2(0f, 0.6f);

        // ------------------------------------------------------------------ table and ammo

        [Test]
        public void ThePopMineIsAnUncommonWeaponSlotWeaponThatDrops()
        {
            Assert.AreEqual("Pop Mine", mine.Name);
            Assert.AreEqual(WeaponSlot.Weapon, mine.Slot);
            Assert.AreEqual(WeaponTier.Uncommon, mine.Tier);
            Assert.IsTrue(mine.Drops);
            Assert.AreEqual(Tuning.I.mineAmmo, mine.MaxAmmo);
            Assert.AreEqual(Tuning.I.mineAmmo, run.WeaponAmmo, "a crate comes full");
        }

        [Test]
        public void EachPressDropsOneMineAtHisFeetAndSpendsOne()
        {
            Frame(true);
            Assert.AreEqual(1, Field.Count);
            Assert.AreEqual(Tuning.I.mineAmmo - 1, run.WeaponAmmo);
            Assert.AreEqual(Feet.x, Field.Mines[0].Position.x, 1e-4f);
            Assert.AreEqual(0f, Field.Mines[0].Position.y, 1e-4f, "on the floor he stands on");
            Assert.IsTrue(Field.Mines[0].Resting);
            Frame(false);
            Assert.AreEqual(1, Field.Count, "holding or not pressing drops nothing more");
        }

        [Test]
        public void WithTheMostMinesOutTheButtonIsDimAndDoesNothing()
        {
            int max = PopMine.MaxPlaced;
            for (int i = 0; i < max; i++) Frame(true);
            Assert.AreEqual(max, Field.Count);
            Assert.IsTrue(behaviour.Dimmed);
            int ammo = run.WeaponAmmo;
            Frame(true);
            Assert.AreEqual(max, Field.Count);
            Assert.AreEqual(ammo, run.WeaponAmmo, "no mine spent");
        }

        [Test]
        public void TheSlotEmptiesWhenTheLastMineIsDropped()
        {
            for (int i = 0; i < mine.MaxAmmo; i++)
            {
                if (Field.Count >= PopMine.MaxPlaced) Field.Clear();
                Frame(true);
            }
            Assert.AreEqual(0, run.WeaponAmmo);
            Assert.IsNull(run.Weapon);
        }

        // ------------------------------------------------------------------ triggers

        [Test]
        public void AMineDoesNotGoOffBeforeItIsArmed()
        {
            Frame(true);
            var m = Field.Mines[0];
            var b = AddBubble(OnTheMine(m));
            Frames(Tuning.I.mineArmTime * 0.5f);
            Assert.AreEqual(1, Field.Count);
            Assert.IsEmpty(hitBubbles);
            Frames(Tuning.I.mineArmTime);
            Assert.AreEqual(0, Field.Count, "armed now, so the bubble sets it off");
            CollectionAssert.Contains(hitBubbles, b);
        }

        [Test]
        public void TheBlastHitsEverythingInItsRadiusOnceAndNothingOutside()
        {
            var m = PlaceArmed();
            float r = Tuning.I.mineBlastRadius;
            var near = AddBubble(m.Centre + new Vector2(r - 0.6f, 0.4f));
            var far = AddBubble(m.Centre + new Vector2(r + 1.2f, 0.4f));
            var trigger = AddBubble(OnTheMine(m));
            Frame(false);
            Assert.AreEqual(0, Field.Count);
            CollectionAssert.Contains(hitBubbles, trigger);
            CollectionAssert.Contains(hitBubbles, near);
            CollectionAssert.DoesNotContain(hitBubbles, far);
            Assert.AreEqual(1, hitBubbles.FindAll(x => x == near).Count, "once");
        }

        [Test]
        public void BubblesBornFromTheBlastAreNotHitByIt()
        {
            var m = PlaceArmed();
            var trigger = AddBubble(OnTheMine(m));
            Bubble child = null;
            world.OnHitBubble = b =>
            {
                hitBubbles.Add(b);
                if (child == null) child = AddBubble(b.Position + new Vector2(0.2f, 0f));
            };
            Frame(false);
            Assert.NotNull(child);
            CollectionAssert.Contains(hitBubbles, trigger);
            CollectionAssert.DoesNotContain(hitBubbles, child);
        }

        [Test]
        public void ARollingBalloonOverAnArmedMineIsPoppedWithItsNeighbours()
        {
            var m = PlaceArmed();
            var roller = AddRoller(m.Position.x + 0.3f);
            var near = AddBubble(m.Centre + new Vector2(-1.2f, 0.8f));
            Frame(false);
            Assert.AreEqual(0, Field.Count);
            CollectionAssert.Contains(hitRollers, roller);
            CollectionAssert.Contains(hitBubbles, near);
        }

        [Test]
        public void SteelDoesNotSetItOff()
        {
            var m = PlaceArmed();
            AddBubble(OnTheMine(m), steel: true);
            AddRoller(m.Position.x, steel: true);
            Frames(0.5f);
            Assert.AreEqual(1, Field.Count, "a steel Pogo and a steel ball roll over it");
            Assert.AreEqual(0, steelHits);
        }

        [Test]
        public void TheBlastTurnsASteelRollingBallAway()
        {
            var m = PlaceArmed();
            var steel = AddRoller(m.Position.x - 1f, steel: true);
            steel.Direction = 1; // rolling toward the mine
            AddBubble(OnTheMine(m));
            Frame(false);
            Assert.AreEqual(-1, steel.Direction, "now rolling away from the blast");
            Assert.IsFalse(steel.Popped);
            CollectionAssert.DoesNotContain(hitRollers, steel);
        }

        [Test]
        public void ACeramicBubbleTakesOneHitFromTheBlast()
        {
            var m = PlaceArmed();
            var ceramic = AddBubble(OnTheMine(m), ceramic: true);
            int armor = ceramic.Armor;
            Frame(false);
            Assert.AreEqual(1, hitBubbles.FindAll(x => x == ceramic).Count);
            Assert.AreEqual(armor - 1, ceramic.Armor);
        }

        [Test]
        public void ABouncyBossBulletSetsItOffAndIsDestroyed()
        {
            var m = PlaceArmed();
            var bouncy = Bullet.CreateBouncy(root.transform, OnTheMine(m), 1, 3, Themes.ForWorld(1));
            bullets.Add(bouncy);
            Frame(false);
            Assert.AreEqual(0, Field.Count);
            Assert.IsTrue(bouncy.Dead);
        }

        [Test]
        public void AnAimedBossBulletDoesNotSetItOff()
        {
            var m = PlaceArmed();
            var aimed = Bullet.Create(root.transform, OnTheMine(m), Vector2.zero, Themes.ForWorld(1));
            bullets.Add(aimed);
            Frame(false);
            Assert.AreEqual(1, Field.Count);
            Assert.IsFalse(aimed.Dead);
        }

        [Test]
        public void TheBossTakesMineBossDamageFromABlast()
        {
            var boss = Boss.Create(root.transform, BossSpec.ForWorld(1), 1, Themes.ForWorld(1));
            world.Destroy();
            world = MakeWorld(boss);
            ctx.World = world;
            int total = 0;
            world.OnDamageBoss = (n, _) => total += n;
            var at = new Vector2(boss.Body.center.x, boss.Body.yMin - 0.5f);
            var blast = MineField.Blast(world, at);
            Assert.IsTrue(blast.boss);
            Assert.AreEqual(Mathf.FloorToInt(Tuning.I.mineBossDamage + 1e-3f), total);
        }

        // ------------------------------------------------------------------ tiles

        [Test]
        public void TheBlastDestroysBreakableBlocksInsideIt()
        {
            var map = StageMap.Flat();
            int c = StageMap.Col(Feet.x);
            map.Set(c + 1, 0, new Cell { kind = TileKind.Breakable, hp = 5 });
            map.Set(c + 6, 0, new Cell { kind = TileKind.Breakable, hp = 2 });
            SetMap(map);
            var m = PlaceArmed();
            AddBubble(OnTheMine(m));
            Frame(false);
            Assert.AreEqual(TileKind.Empty, map.Get(c + 1, 0).kind);
            Assert.AreEqual(TileKind.Breakable, map.Get(c + 6, 0).kind, "out of reach");
        }

        [Test]
        public void AMineOnABlockThatBreaksFallsToTheNextSurface()
        {
            var map = StageMap.Flat();
            int c = StageMap.Col(Feet.x);
            map.Set(c, 3, new Cell { kind = TileKind.Breakable, hp = 1 });
            SetMap(map);
            ctx.Muzzle = new Vector2(Feet.x, 4f);
            Frame(true);
            var m = Field.Mines[0];
            Assert.IsTrue(m.Resting);
            Assert.AreEqual(4f, m.Position.y, 1e-4f);
            map.Damage(c, 3);
            Frames(1.5f);
            Assert.IsTrue(m.Resting);
            Assert.AreEqual(0f, m.Position.y, 1e-4f, "on the floor now");
        }

        // ------------------------------------------------------------------ air, gaps and ice

        [Test]
        public void DroppedInTheAirItFallsAndSticksWhereItLands()
        {
            var map = StageMap.Flat();
            int c = StageMap.Col(Feet.x);
            map.Set(c, 2, new Cell { kind = TileKind.Solid });
            SetMap(map);
            ctx.InAir = true;
            ctx.Muzzle = new Vector2(Feet.x, 6f);
            Frame(true);
            var m = Field.Mines[0];
            Assert.IsFalse(m.Resting);
            Frames(2f);
            Assert.IsTrue(m.Resting);
            Assert.AreEqual(3f, m.Position.y, 1e-4f, "on top of the platform");
        }

        [Test]
        public void DroppedIntoAGapItIsLost()
        {
            var map = StageMap.Flat();
            int c = StageMap.Col(Feet.x);
            map.SetGap(c, true);
            SetMap(map);
            ctx.Muzzle = new Vector2(Feet.x, 3f);
            Frame(true);
            Assert.AreEqual(1, Field.Count);
            Frames(2f);
            Assert.AreEqual(0, Field.Count);
            Assert.AreEqual(Tuning.I.mineAmmo - 1, run.WeaponAmmo, "the mine is spent all the same");
        }

        [Test]
        public void DroppedWhileRunningOnIceItSlidesTheWayHeWasGoing()
        {
            var map = StageMap.Flat();
            for (int c = 0; c < StageMap.Cols; c++) map.SetGround(c, Surface.Ice);
            SetMap(map);
            var m = Field.Place(Feet, 5f, map);
            Frames(1.5f);
            Assert.AreEqual(Feet.x + Tuning.I.mineIceSlide, m.Position.x, 0.02f);
            var still = Field.Place(Feet, 0f, map);
            Frames(1f);
            Assert.AreEqual(Feet.x, still.Position.x, 1e-4f, "standing still it stays put");
        }

        [Test]
        public void OnNormalGroundItDoesNotSlide()
        {
            var m = Field.Place(Feet, 5f, Game.Map);
            Frames(1f);
            Assert.AreEqual(Feet.x, m.Position.x, 1e-4f);
        }
    }
}
