using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>W07 Arc Coil: a bolt to the nearest thing in reach, then on to up to four more. Steel conducts, ceramic stops it.</summary>
    public class WeaponArcCoilTests
    {
        const float Dt = 1f / 60f;

        /// <summary>The coil in the tests: standing in the middle of a tile column, about 1.4 units above the floor.</summary>
        static readonly Vector2 Tip = new Vector2(0.5f, 21f / 15f);

        GameObject root;
        StageMap previousMap;
        List<Bubble> bubbles;
        List<Roller> rollers;
        List<Bullet> bullets;
        WeaponWorld world;
        Run run;
        WeaponSpec arc;
        ArcCoil.Behaviour behaviour;
        WeaponContext ctx;
        readonly List<Bubble> hitBubbles = new List<Bubble>();
        readonly List<Roller> hitRollers = new List<Roller>();
        int steelHits;
        System.Action<Bubble> onHit;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("WeaponArcCoilTests");
            previousMap = Game.Map;
            SetMap(StageMap.Flat());
            bubbles = new List<Bubble>();
            rollers = new List<Roller>();
            bullets = new List<Bullet>();
            hitBubbles.Clear();
            hitRollers.Clear();
            steelHits = 0;
            onHit = null;
            world = MakeWorld(null);

            arc = WeaponSpec.ById("arc");
            Assert.NotNull(arc, "the Arc Coil is in WeaponSpec.All");
            run = new Run(1);
            Assert.IsTrue(run.GiveWeapon(arc));
            behaviour = (ArcCoil.Behaviour)arc.CreateBehaviour();
            ctx = new WeaponContext
            {
                CanFire = true,
                Muzzle = Tip,
                Up = Vector2.up,
                Spec = arc,
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
                // A hit is only counted (ceramic loses one armour), so the same bubbles can take later bolts.
                OnHitBubble = b =>
                {
                    hitBubbles.Add(b);
                    if (b.Ceramic) b.AbsorbHit();
                    onHit?.Invoke(b);
                },
                OnHitRoller = r => hitRollers.Add(r),
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

        /// <summary>Bubbles in a row two units apart at y = 5, starting straight above the coil.</summary>
        List<Bubble> Row(int count)
        {
            var list = new List<Bubble>();
            for (int i = 0; i < count; i++) list.Add(AddBubble(new Vector2(Tip.x + 2f * i, 5f)));
            return list;
        }

        void Frame(bool pressed)
        {
            ctx.Pressed = pressed;
            ctx.Held = pressed;
            behaviour.Tick(ctx, Dt);
        }

        void Frames(int n)
        {
            for (int i = 0; i < n; i++) Frame(false);
        }

        static int BoltFrames => Mathf.CeilToInt(Tuning.I.arcJumpTime * (Tuning.I.arcJumps + 2) / Dt) + 2;
        static int CooldownFrames => Mathf.CeilToInt(Tuning.I.arcCooldown / Dt) + 1;

        // ------------------------------------------------------------------ table and ammo

        [Test]
        public void TheArcCoilIsARareWeaponSlotWeaponThatDrops()
        {
            Assert.AreEqual("Arc Coil", arc.Name);
            Assert.AreEqual(WeaponSlot.Weapon, arc.Slot);
            Assert.AreEqual(WeaponTier.Rare, arc.Tier);
            Assert.IsTrue(arc.Drops);
            Assert.AreEqual(Tuning.I.arcAmmo, arc.MaxAmmo, "a full crate comes from Tuning");
            Assert.AreEqual(11, arc.IconRows[0].Length, "the icon is 11 pixels wide");
            Assert.NotNull(arc.HeldRows);
        }

        [Test]
        public void ABoltSpendsOneAndHitsTheFirstThingAtOnce()
        {
            var row = Row(3);
            Frame(true);
            Assert.AreEqual(Tuning.I.arcAmmo - 1, run.WeaponAmmo);
            Assert.AreEqual(1, hitBubbles.Count, "the first jump is at once, the rest follow one by one");
            Assert.AreSame(row[0], hitBubbles[0], "the nearest bubble is first");
        }

        [Test]
        public void NothingInReachSpendsNothing()
        {
            float reach = Tuning.I.arcReach;
            AddBubble(new Vector2(Tip.x, Tip.y + reach + 0.45f + 0.3f));
            Frame(true);
            Assert.AreEqual(Tuning.I.arcAmmo, run.WeaponAmmo);
            Assert.AreEqual(0, hitBubbles.Count);
            Assert.IsNull(behaviour.Current);
        }

        [Test]
        public void TheFirstJumpReachesArcReach()
        {
            float reach = Tuning.I.arcReach;
            var b = AddBubble(new Vector2(Tip.x, Tip.y + reach + 0.45f - 0.3f));
            Frame(true);
            CollectionAssert.Contains(hitBubbles, b);
        }

        [Test]
        public void ASecondPressWaitsForTheCooldown()
        {
            Row(2);
            Frame(true);
            Frames(BoltFrames);
            Frame(true);
            Assert.AreEqual(Tuning.I.arcAmmo - 1, run.WeaponAmmo, "too soon: nothing fires");
            Frames(CooldownFrames);
            Frame(true);
            Assert.AreEqual(Tuning.I.arcAmmo - 2, run.WeaponAmmo);
        }

        [Test]
        public void TheSlotEmptiesAfterTheLastBolt()
        {
            Row(2);
            for (int i = 0; i < Tuning.I.arcAmmo; i++)
            {
                Frame(true);
                Frames(CooldownFrames);
            }
            Assert.AreEqual(0, run.WeaponAmmo);
            Assert.IsNull(run.Weapon, "the slot is empty at zero");
        }

        // ------------------------------------------------------------------ the chain

        [Test]
        public void OneBoltPopsUpToFiveOneAfterAnother()
        {
            var row = Row(Tuning.I.arcJumps + 2);
            Frame(true);
            int seen = hitBubbles.Count;
            Frames(BoltFrames);
            Assert.AreEqual(Tuning.I.arcJumps + 1, hitBubbles.Count, "the first hit and arcJumps more");
            Assert.Greater(hitBubbles.Count, seen, "the later jumps come after the first frame");
            for (int i = 0; i <= Tuning.I.arcJumps; i++) Assert.AreSame(row[i], hitBubbles[i], "the bolt travels down the row");
            CollectionAssert.DoesNotContain(hitBubbles, row[row.Count - 1]);
        }

        [Test]
        public void JumpsAreSpacedByArcJumpTime()
        {
            Row(3);
            Frame(true);
            int framesPerJump = Mathf.FloorToInt(Tuning.I.arcJumpTime / Dt);
            Assume.That(framesPerJump, Is.GreaterThan(1));
            Frames(framesPerJump - 1);
            Assert.AreEqual(1, hitBubbles.Count, "the second jump waits");
            Frames(3);
            Assert.AreEqual(2, hitBubbles.Count);
        }

        [Test]
        public void TheJumpCountComesFromTuning()
        {
            int old = Tuning.I.arcJumps;
            try
            {
                Tuning.I.arcJumps = 2;
                Row(6);
                var bolt = ArcCoil.FireNow(world, Tip);
                Assert.AreEqual(3, bolt.Result.hits);
            }
            finally
            {
                Tuning.I.arcJumps = old;
            }
        }

        [Test]
        public void AJumpDoesNotReachPastArcJumpReach()
        {
            var a = AddBubble(new Vector2(Tip.x, 5f));
            var far = AddBubble(new Vector2(Tip.x + Tuning.I.arcJumpReach + 0.45f + 0.3f, 5f));
            var bolt = ArcCoil.FireNow(world, Tip);
            CollectionAssert.Contains(hitBubbles, a);
            CollectionAssert.DoesNotContain(hitBubbles, far);
            Assert.IsTrue(bolt.Done);
        }

        [Test]
        public void PiecesBornFromTheBoltAreNeverItsTargets()
        {
            Bubble born = null;
            onHit = b =>
            {
                if (born != null) return;
                born = AddBubble(b.Position + new Vector2(1f, 0f));
            };
            AddBubble(new Vector2(Tip.x, 5f));
            ArcCoil.FireNow(world, Tip);
            Assert.NotNull(born);
            CollectionAssert.DoesNotContain(hitBubbles, born);
        }

        [Test]
        public void TilesDoNotStopTheBolt()
        {
            var map = StageMap.Flat();
            for (int r = 2; r <= 4; r++)
            for (int c = StageMap.Col(-3f); c <= StageMap.Col(4f); c++)
                map.Set(c, r, new Cell { kind = TileKind.Solid });
            SetMap(map);
            var b = AddBubble(new Vector2(Tip.x, 6f));
            ArcCoil.FireNow(world, Tip);
            CollectionAssert.Contains(hitBubbles, b, "the bolt bends round tiles");
        }

        // ------------------------------------------------------------------ steel and ceramic

        [Test]
        public void SteelIsNeverTheFirstStop()
        {
            var s = AddBubble(new Vector2(Tip.x, 4f), steel: true);
            Frame(true);
            Assert.AreEqual(Tuning.I.arcAmmo, run.WeaponAmmo, "only steel in reach: nothing fires");
            Assert.IsFalse(s.Popped);
        }

        [Test]
        public void SteelConductsTheBoltAcrossAGap()
        {
            var a = AddBubble(new Vector2(Tip.x, 5f));
            var steel = AddBubble(new Vector2(Tip.x + 4f, 5f), steel: true);
            var b = AddBubble(new Vector2(Tip.x + 8f, 5f));
            Assume.That(8f - 0.45f, Is.GreaterThan(Tuning.I.arcJumpReach), "a and b are too far apart without the steel");
            var bolt = ArcCoil.FireNow(world, Tip);
            CollectionAssert.AreEqual(new[] { a, b }, hitBubbles);
            Assert.AreEqual(1, bolt.Result.steel);
            Assert.AreEqual(1, steelHits, "a spark where it passed");
            Assert.IsFalse(steel.Popped, "steel is not harmed");
        }

        [Test]
        public void SteelDoesNotUseUpAJump()
        {
            int old = Tuning.I.arcJumps;
            try
            {
                Tuning.I.arcJumps = 1;
                var a = AddBubble(new Vector2(Tip.x, 5f));
                AddBubble(new Vector2(Tip.x + 4f, 5f), steel: true);
                var b = AddBubble(new Vector2(Tip.x + 8f, 5f));
                ArcCoil.FireNow(world, Tip);
                CollectionAssert.AreEqual(new[] { a, b }, hitBubbles, "one jump, through the steel, still reaches b");
            }
            finally
            {
                Tuning.I.arcJumps = old;
            }
        }

        [Test]
        public void ASteelRollingBallConductsToo()
        {
            var a = AddBubble(new Vector2(Tip.x, 2f));
            var steel = AddRoller(Tip.x + 4f, steel: true);
            var b = AddBubble(new Vector2(Tip.x + 8f, 2f));
            var bolt = ArcCoil.FireNow(world, Tip);
            CollectionAssert.AreEqual(new[] { a, b }, hitBubbles);
            Assert.AreEqual(1, bolt.Result.steel);
            Assert.IsEmpty(hitRollers, "steel is not harmed");
            Assert.IsFalse(steel.Popped);
        }

        [Test]
        public void CeramicTakesOneHitAndEndsTheBolt()
        {
            var a = AddBubble(new Vector2(Tip.x, 5f));
            var c = AddBubble(new Vector2(Tip.x + 2f, 5f), ceramic: true);
            var b = AddBubble(new Vector2(Tip.x + 4f, 5f));
            var bolt = ArcCoil.FireNow(world, Tip);
            CollectionAssert.AreEqual(new[] { a, c }, hitBubbles);
            CollectionAssert.DoesNotContain(hitBubbles, b);
            Assert.AreEqual(Mathf.Max(2, Tuning.I.ceramicHits) - 1, c.Armor, "one hit cracks it");
            Assert.IsTrue(bolt.Result.endedOnCeramic);
        }

        [Test]
        public void ACeramicFirstStopEndsItAtOnce()
        {
            var c = AddBubble(new Vector2(Tip.x, 4f), ceramic: true);
            var b = AddBubble(new Vector2(Tip.x + 2f, 4f));
            ArcCoil.FireNow(world, Tip);
            CollectionAssert.AreEqual(new[] { c }, hitBubbles);
            CollectionAssert.DoesNotContain(hitBubbles, b);
        }

        [Test]
        public void RollingBalloonsAreHitAndPassItOn()
        {
            var r = AddRoller(Tip.x + 2f);
            var b = AddBubble(new Vector2(Tip.x + 4f, 3f));
            ArcCoil.FireNow(world, Tip);
            CollectionAssert.Contains(hitRollers, r);
            CollectionAssert.Contains(hitBubbles, b);
        }

        // ------------------------------------------------------------------ boss, air

        [Test]
        public void TheBossTakesArcBossDamageAndTheBoltJumpsOn()
        {
            var boss = Boss.Create(root.transform, BossSpec.ForWorld(1), 1, Themes.ForWorld(1));
            world.Destroy();
            world = MakeWorld(boss);
            int total = 0;
            world.OnDamageBoss = (n, _) => total += n;
            var y = typeof(Boss).GetField("y", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(y, "Boss.y must exist.");
            y.SetValue(boss, 8f);
            var body = boss.Body;
            var from = new Vector2(body.center.x, body.yMin - 2f);
            // The boss is nearest, so it is the first stop; the bubble is one jump from it.
            var b = AddBubble(new Vector2(body.center.x + 3f, body.yMin - 0.5f));
            var bolt = ArcCoil.FireNow(world, from);
            Assert.NotNull(bolt);
            Assert.IsTrue(bolt.Result.boss);
            Assert.AreEqual(Mathf.FloorToInt(Tuning.I.arcBossDamage + 1e-3f), total, "the boss is hit once per bolt");
            CollectionAssert.Contains(hitBubbles, b, "and the bolt jumps on to a balloon beside it");
        }

        [Test]
        public void ItFiresTheSameInTheAirAndDownDoesNothing()
        {
            var b = AddBubble(new Vector2(Tip.x, 5f));
            ctx.InAir = true;
            ctx.ShootDown = true;
            ctx.Up = Vector2.down;
            Frame(true);
            CollectionAssert.Contains(hitBubbles, b, "the bolt still finds the bubble above him");
            Assert.AreEqual(Tuning.I.arcAmmo - 1, run.WeaponAmmo);
        }

        [Test]
        public void NothingFiresWhenTheCoilIsInsideATile()
        {
            Row(2);
            ctx.CanFire = false;
            Frame(true);
            Assert.AreEqual(Tuning.I.arcAmmo, run.WeaponAmmo);
            Assert.IsEmpty(hitBubbles);
        }
    }
}
