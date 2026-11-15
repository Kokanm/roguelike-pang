using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>W05 Trimmer Blade: a round swing over his head that reaches both sides down to the floor, cut off by tiles.</summary>
    public class WeaponTrimmerBladeTests
    {
        const float Dt = 1f / 60f;

        GameObject root;
        StageMap previousMap;
        List<Bubble> bubbles;
        List<Roller> rollers;
        List<Bullet> bullets;
        WeaponWorld world;
        Run run;
        WeaponSpec blade;
        TrimmerBlade.Behaviour behaviour;
        WeaponContext ctx;
        readonly List<Vector2Int> tileHits = new List<Vector2Int>();
        int steelHits;

        /// <summary>His hand in the tests: standing in the middle of the arena, about 1.3 units above the floor.</summary>
        static readonly Vector2 Hand = new Vector2(0.3f, 20f / 15f);

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("WeaponTrimmerBladeTests");
            previousMap = Game.Map;
            SetMap(StageMap.Flat());
            bubbles = new List<Bubble>();
            rollers = new List<Roller>();
            bullets = new List<Bullet>();
            tileHits.Clear();
            steelHits = 0;
            world = MakeWorld(null);

            blade = WeaponSpec.ById("blade");
            Assert.NotNull(blade, "the Trimmer Blade is in WeaponSpec.All");
            run = new Run(1);
            Assert.IsTrue(run.GiveWeapon(blade));
            behaviour = (TrimmerBlade.Behaviour)blade.CreateBehaviour();
            ctx = new WeaponContext
            {
                CanFire = true,
                Muzzle = Hand, // with no Wick the swing centres on the muzzle
                Up = Vector2.up,
                Spec = blade,
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
                OnHitBubble = b => { if (!b.AbsorbHit()) b.Swell(Tuning.I.popSwell); },
                OnHitRoller = r => { if (!r.AbsorbHit()) r.Swell(Tuning.I.popSwell); },
                OnHitTile = (c, r) => { tileHits.Add(new Vector2Int(c, r)); Game.Map.Damage(c, r); },
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

        Bubble AddBubble(Vector2 at, int tier = 3, bool ceramic = false, bool steel = false)
        {
            var b = Bubble.Create(root.transform, BubbleKind.Gloop, tier, at, Vector2.zero, false, ceramic, steel, steel);
            bubbles.Add(b);
            return b;
        }

        Roller AddRoller(float x, int direction, bool steel = false)
        {
            int tier = Roller.TierCount - 1;
            var r = Roller.Create(root.transform, tier, x, 0f, direction, steel);
            rollers.Add(r);
            return r;
        }

        static bool Hit(Bubble b) => b.Popped || b.transform.localScale.x > 1.0001f || (b.Ceramic && b.Armor < Mathf.Max(2, Tuning.I.ceramicHits));

        void Frame(bool pressed, bool held)
        {
            ctx.Pressed = pressed;
            ctx.Held = held;
            behaviour.Tick(ctx, Dt);
        }

        void Idle(int frames)
        {
            for (int i = 0; i < frames; i++) Frame(false, false);
        }

        int Swung => blade.MaxAmmo - run.WeaponAmmo;

        // ------------------------------------------------------------------ table and ammo

        [Test]
        public void TheBladeIsACommonWeaponSlotWeaponThatDrops()
        {
            Assert.AreEqual("Trimmer Blade", blade.Name);
            Assert.AreEqual(WeaponSlot.Weapon, blade.Slot);
            Assert.AreEqual(WeaponTier.Common, blade.Tier);
            Assert.IsTrue(blade.Drops);
            Assert.AreEqual(Tuning.I.bladeAmmo, blade.MaxAmmo, "a full crate comes from Tuning");
            Assert.AreEqual(Tuning.I.bladeAmmo, run.WeaponAmmo);
            Assert.LessOrEqual(WeaponArt.WidthOf(blade.IconRows), 11);
            Assert.NotNull(WeaponArt.Held(blade));
        }

        [Test]
        public void OnePressIsOneSwingAndSpendsOne()
        {
            Frame(true, true);
            Assert.AreEqual(1, Swung);
            Assert.AreEqual(1, behaviour.Swings);
            Frame(false, false);
            Idle(60);
            Assert.AreEqual(1, Swung, "no second swing without a press");
        }

        [Test]
        public void HoldingKeepsSwingingOncePerCooldown()
        {
            float seconds = 3f;
            int frames = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < frames; i++) Frame(i == 0, true);
            int expected = Mathf.FloorToInt(seconds / Tuning.I.bladeCooldown);
            Assert.That(Swung, Is.InRange(expected - 1, expected + 1), "one swing every bladeCooldown seconds while held");
        }

        [Test]
        public void AnEarlyTapSwingsWhenTheWaitEnds()
        {
            Frame(true, true);
            Frame(false, false);
            Frame(true, false); // too soon
            Assert.AreEqual(1, Swung);
            Idle(Mathf.CeilToInt(Tuning.I.bladeCooldown / Dt) + 1);
            Assert.AreEqual(2, Swung, "the early tap was kept, not eaten");
        }

        [Test]
        public void TheSlotEmptiesAfterTheLastSwing()
        {
            int frames = 0;
            while (run.Weapon != null && frames < 100000)
            {
                Frame(true, true);
                frames++;
            }
            Assert.IsNull(run.Weapon, "the slot empties at zero");
            Assert.AreEqual(Tuning.I.bladeAmmo, behaviour.Swings, "one swing per ammo");
        }

        [Test]
        public void NoSwingWhenItCannotFire()
        {
            ctx.CanFire = false;
            Frame(true, true);
            Assert.AreEqual(0, Swung);
        }

        // ------------------------------------------------------------------ the arc

        [Test]
        public void ASwingHitsAboveAndBothSidesButNotOutOfReach()
        {
            float reach = Tuning.I.bladeReach;
            var over = AddBubble(Hand + new Vector2(0f, reach - 0.2f));
            var left = AddBubble(Hand + new Vector2(-reach + 0.2f, 0f));
            var right = AddBubble(Hand + new Vector2(reach - 0.2f, 0f));
            var far = AddBubble(Hand + new Vector2(0f, reach + 1.5f));
            var farSide = AddBubble(Hand + new Vector2(reach + 1.5f, 0f));
            var r = TrimmerBlade.Swing(world, Hand, false);
            Assert.AreEqual(3, r.hits);
            Assert.IsTrue(Hit(over) && Hit(left) && Hit(right));
            Assert.IsFalse(Hit(far), "out of reach above");
            Assert.IsFalse(Hit(farSide), "out of reach to the side");
        }

        [Test]
        public void RollingBalloonsOnBothSidesArePoppedBeforeTheyReachHim()
        {
            float r = Roller.RadiusOf(Roller.TierCount - 1);
            var fromLeft = AddRoller(Hand.x - 1.4f, +1);
            var fromRight = AddRoller(Hand.x + 1.4f, -1);
            Assert.Less(fromLeft.Position.y, Hand.y, "the balloons roll below his hand");
            Assert.Greater(r, 0f);
            var res = TrimmerBlade.Swing(world, Hand, false);
            Assert.AreEqual(2, res.hits, "the arc reaches down to the floor on both sides");
        }

        [Test]
        public void ItDoesNotReachUnderHisFeetUnlessSwingingDown()
        {
            SetMap(StageMap.Flat());
            var hand = new Vector2(0.3f, 8f); // high up in the air, so there is room below
            var below = AddBubble(hand + new Vector2(0f, -1.6f));
            TrimmerBlade.Swing(world, hand, false);
            Assert.IsFalse(Hit(below), "straight below is outside the upward arc");
            TrimmerBlade.Swing(world, hand, true);
            Assert.IsTrue(Hit(below), "with Down held the arc turns over");
        }

        [Test]
        public void DownHeldInTheAirSwingsDownward()
        {
            ctx.ShootDown = true;
            ctx.InAir = true;
            ctx.Muzzle = new Vector2(0.3f, 8f);
            var below = AddBubble(ctx.Muzzle + new Vector2(0f, -1.6f));
            var above = AddBubble(ctx.Muzzle + new Vector2(0f, 1.6f));
            Frame(true, true);
            Assert.IsTrue(Hit(below));
            Assert.IsFalse(Hit(above), "the down swing does not cover straight up");
        }

        [Test]
        public void EachThingIsHitOncePerSwing()
        {
            var c = AddBubble(Hand + new Vector2(0f, 1.2f), ceramic: true);
            TrimmerBlade.Swing(world, Hand, false);
            Assert.AreEqual(Mathf.Max(2, Tuning.I.ceramicHits) - 1, c.Armor, "one hit cracks it");
            Assert.IsFalse(c.Popped, "and does not break it in the same swing");
        }

        [Test]
        public void TwoSwingsBreakCeramic()
        {
            var c = AddBubble(Hand + new Vector2(0f, 1.2f), ceramic: true);
            TrimmerBlade.Swing(world, Hand, false);
            TrimmerBlade.Swing(world, Hand, false);
            Assert.IsTrue(c.transform.localScale.x > 1.0001f || c.Popped, "the second swing breaks it");
        }

        [Test]
        public void SteelClangsAndNothingPops()
        {
            var s = AddBubble(Hand + new Vector2(0f, 1.2f), steel: true);
            var r = TrimmerBlade.Swing(world, Hand, false);
            Assert.AreEqual(1, r.steel);
            Assert.AreEqual(0, r.hits);
            Assert.AreEqual(1, steelHits);
            Assert.IsFalse(s.Popped);
        }

        [Test]
        public void ASteelRollingBallIsTurnedAwayFromHim()
        {
            var fromLeft = AddRoller(Hand.x - 1.4f, +1, steel: true);
            var fromRight = AddRoller(Hand.x + 1.4f, -1, steel: true);
            TrimmerBlade.Swing(world, Hand, false);
            Assert.AreEqual(-1, fromLeft.Direction, "the left one now rolls left, away from him");
            Assert.AreEqual(+1, fromRight.Direction, "the right one now rolls right");
            Assert.IsFalse(fromLeft.Popped || fromRight.Popped);
        }

        [Test]
        public void AFadedGhostIsNotHit()
        {
            var g = Bubble.Create(root.transform, BubbleKind.Ghost, 2, Hand + new Vector2(0f, 1.2f), Vector2.zero, false);
            bubbles.Add(g);
            for (int i = 0; i < 156 && g.CanBeHit; i++) g.Tick(Dt);
            g.transform.position = Hand + new Vector2(0f, 1.2f);
            Assert.IsFalse(g.CanBeHit, "the Ghost has faded");
            var r = TrimmerBlade.Swing(world, Hand, false);
            Assert.AreEqual(0, r.hits);
            Assert.IsFalse(g.Popped);
        }

        // ------------------------------------------------------------------ tiles

        [Test]
        public void ASolidBlockBesideHimShieldsThatSide()
        {
            var map = StageMap.Flat();
            int col = StageMap.Col(Hand.x - 1f);
            map.Set(col, 0, new Cell { kind = TileKind.Solid });
            map.Set(col, 1, new Cell { kind = TileKind.Solid });
            map.Set(col, 2, new Cell { kind = TileKind.Solid });
            SetMap(map);
            var behind = AddBubble(new Vector2(StageMap.CenterX(col) - 0.9f, 1.4f));
            var open = AddBubble(Hand + new Vector2(1.6f, 0f));
            TrimmerBlade.Swing(world, Hand, false);
            Assert.IsFalse(Hit(behind), "the block hides it");
            Assert.IsTrue(Hit(open), "the other side is open");
        }

        [Test]
        public void APlatformOverheadShieldsWhatIsAboveIt()
        {
            var map = StageMap.Flat();
            int col = StageMap.Col(Hand.x);
            for (int c = col - 3; c <= col + 3; c++) map.Set(c, 2, new Cell { kind = TileKind.Solid });
            SetMap(map);
            var above = AddBubble(new Vector2(Hand.x, 3.4f));
            TrimmerBlade.Swing(world, Hand, false);
            Assert.IsFalse(Hit(above), "the swing does not reach through a platform");
        }

        [Test]
        public void ABreakableBlockInReachOnEitherSideTakesOneHit()
        {
            var map = StageMap.Flat();
            int left = StageMap.Col(Hand.x - 1.5f), right = StageMap.Col(Hand.x + 1.5f);
            map.Set(left, 1, new Cell { kind = TileKind.Breakable, hp = 5 });
            map.Set(right, 1, new Cell { kind = TileKind.Breakable, hp = 5 });
            int farCol = StageMap.Col(Hand.x + 4f);
            map.Set(farCol, 1, new Cell { kind = TileKind.Breakable, hp = 5 });
            SetMap(map);
            var r = TrimmerBlade.Swing(world, Hand, false);
            Assert.AreEqual(2, r.tiles);
            CollectionAssert.AreEquivalent(new[] { new Vector2Int(left, 1), new Vector2Int(right, 1) }, tileHits);
        }

        [Test]
        public void ABlockBehindABlockIsNotHit()
        {
            var map = StageMap.Flat();
            int near = StageMap.Col(Hand.x + 1f), far = near + 1;
            map.Set(near, 1, new Cell { kind = TileKind.Breakable, hp = 5 });
            map.Set(far, 1, new Cell { kind = TileKind.Breakable, hp = 5 });
            SetMap(map);
            TrimmerBlade.Swing(world, Hand, false);
            CollectionAssert.Contains(tileHits, new Vector2Int(near, 1));
            CollectionAssert.DoesNotContain(tileHits, new Vector2Int(far, 1), "the near block shields the far one");
        }

        // ------------------------------------------------------------------ bosses

        [Test]
        public void ASwingCutsBossBulletsFromTheSide()
        {
            var theme = Themes.ForWorld(1);
            var side = Bullet.Create(root.transform, Hand + new Vector2(1.5f, -0.3f), new Vector2(-4f, 0f), theme);
            var far = Bullet.Create(root.transform, Hand + new Vector2(5f, 0f), new Vector2(-4f, 0f), theme);
            bullets.Add(side);
            bullets.Add(far);
            var r = TrimmerBlade.Swing(world, Hand, false);
            Assert.AreEqual(1, r.bullets);
            Assert.IsTrue(side.Dead, "cut out of the air");
            Assert.IsFalse(far.Dead);
            Assert.AreEqual(1, steelHits, "a spark where it was cut");
        }

        [Test]
        public void ASwingNextToTheBossTakesBladeBossDamage()
        {
            var boss = Boss.Create(root.transform, BossSpec.ForWorld(1), 1, Themes.ForWorld(1));
            world.Destroy();
            world = MakeWorld(boss);
            int total = 0;
            world.OnDamageBoss = (n, _) => total += n;
            // It enters from above the arena; put it where a wall jump could bring him next to it.
            var y = typeof(Boss).GetField("y", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(y, "Boss.y must exist.");
            y.SetValue(boss, 9f);
            var hand = new Vector2(boss.Body.center.x, boss.Body.yMin - 1f);
            var r = TrimmerBlade.Swing(world, hand, false);
            Assert.IsTrue(r.boss);
            Assert.AreEqual(Mathf.FloorToInt(Tuning.I.bladeBossDamage + 1e-3f), total);
        }

        [Test]
        public void TheBossIsOutOfReachFromTheFloor()
        {
            var boss = Boss.Create(root.transform, BossSpec.ForWorld(1), 1, Themes.ForWorld(1));
            world.Destroy();
            world = MakeWorld(boss);
            int total = 0;
            world.OnDamageBoss = (n, _) => total += n;
            Assume.That(boss.Body.yMin, Is.GreaterThan(Hand.y + Tuning.I.bladeReach), "the first boss floats above reach");
            var r = TrimmerBlade.Swing(world, new Vector2(boss.Body.center.x, Hand.y), false);
            Assert.IsFalse(r.boss);
            Assert.AreEqual(0, total);
        }
    }
}
