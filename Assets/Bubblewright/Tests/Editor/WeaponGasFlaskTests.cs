using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>W09 Gas Flask: a lobbed flask bursts into a cloud; whatever touches it turns sick and pops a moment later.</summary>
    public class WeaponGasFlaskTests
    {
        const float Dt = 1f / 60f;

        GameObject root;
        StageMap previousMap;
        List<Bubble> bubbles;
        List<Roller> rollers;
        List<Bullet> bullets;
        WeaponWorld world;
        GasField field;
        Run run;
        WeaponSpec gas;
        GasFlask.Behaviour behaviour;
        WeaponContext ctx;
        int bubbleHits, rollerHits;

        static readonly Vector2 Tip = new Vector2(0.5f, 21f / 15f);

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("WeaponGasFlaskTests");
            previousMap = Game.Map;
            SetMap(StageMap.Flat());
            bubbles = new List<Bubble>();
            rollers = new List<Roller>();
            bullets = new List<Bullet>();
            bubbleHits = rollerHits = 0;
            world = MakeWorld(null);
            field = new GasField(world, root.transform);

            gas = WeaponSpec.ById("gas");
            Assert.NotNull(gas, "the Gas Flask is in WeaponSpec.All");
            run = new Run(1);
            Assert.IsTrue(run.GiveWeapon(gas));
            behaviour = (GasFlask.Behaviour)gas.CreateBehaviour();
            behaviour.Field = field;
            ctx = new WeaponContext
            {
                CanFire = true,
                Muzzle = Tip,
                Up = Vector2.up,
                Spec = gas,
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
                OnHitBubble = b => { bubbleHits++; if (!b.AbsorbHit()) b.Swell(Tuning.I.popSwell); },
                OnHitRoller = r => { rollerHits++; if (!r.AbsorbHit()) r.Swell(Tuning.I.popSwell); },
                OnHitTile = (c, r) => Game.Map.Damage(c, r),
            };
        }

        [TearDown]
        public void TearDown()
        {
            field.Clear();
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

        void Press()
        {
            ctx.Pressed = true;
            behaviour.Tick(ctx, Dt);
            ctx.Pressed = false;
        }

        void Advance(float seconds)
        {
            int n = Mathf.CeilToInt(seconds / Dt);
            for (int i = 0; i < n; i++)
            {
                behaviour.Tick(ctx, Dt);
                field.Tick(Dt);
            }
        }

        /// <summary>A cloud straight away, centred here (a down throw makes one on the floor; this puts one anywhere).</summary>
        GasField.Cloud CloudAt(Vector2 at)
        {
            var throwUp = typeof(GasField).GetMethod("Burst", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(throwUp, "GasField.Burst must exist.");
            throwUp.Invoke(field, new object[] { at, true });
            return field.Clouds[field.Clouds.Count - 1];
        }

        static float SickTime => Tuning.I.gasSickTime;

        // ------------------------------------------------------------------ table and ammo

        [Test]
        public void TheGasFlaskIsARareWeaponSlotWeapon()
        {
            Assert.AreEqual("Gas Flask", gas.Name);
            Assert.AreEqual(WeaponSlot.Weapon, gas.Slot);
            Assert.AreEqual(WeaponTier.Rare, gas.Tier);
            Assert.AreEqual(Tuning.I.gasAmmo, gas.MaxAmmo);
            Assert.IsTrue(gas.Drops);
            Assert.AreEqual(11, WeaponArt.WidthOf(gas.IconRows));
        }

        [Test]
        public void EachPressThrowsOneFlaskAndTheSlotEmptiesAtZero()
        {
            int full = run.WeaponAmmo;
            Press();
            Assert.AreEqual(full - 1, run.WeaponAmmo);
            Assert.AreEqual(1, field.Flasks.Count);
            Press(); // inside the cooldown: nothing
            Assert.AreEqual(full - 1, run.WeaponAmmo);
            for (int i = 1; i < full; i++)
            {
                Advance(Tuning.I.gasCooldown + Dt);
                Press();
            }
            Assert.AreEqual(0, run.WeaponAmmo);
            Assert.IsNull(run.Weapon, "the slot empties after the last flask");
        }

        [Test]
        public void NothingIsThrownWhenTheTipIsInsideATile()
        {
            ctx.CanFire = false;
            Press();
            Assert.AreEqual(gas.MaxAmmo, run.WeaponAmmo);
            Assert.AreEqual(0, field.Flasks.Count);
        }

        // ------------------------------------------------------------------ the lob

        [Test]
        public void AFlaskBurstsGasLobHeightAboveTheTip()
        {
            Press();
            Advance(2f);
            Assert.AreEqual(0, field.Flasks.Count);
            Assert.AreEqual(1, field.Clouds.Count);
            float risen = Tuning.I.gasRise * (2f - Tuning.I.gasLobHeight / Tuning.I.gasFlaskSpeed);
            Assert.AreEqual(Tip.y + Tuning.I.gasLobHeight, field.Clouds[0].Centre.y - risen, 0.25f);
            Assert.AreEqual(Tip.x, field.Clouds[0].Centre.x, 1e-4f);
        }

        [Test]
        public void AFlaskBurstsOnTheFirstBubbleItTouches()
        {
            var b = AddBubble(Tip + new Vector2(0f, 2f), 2);
            Press();
            Advance(0.5f);
            Assert.AreEqual(1, field.Clouds.Count);
            Assert.Less(field.Clouds[0].Centre.y, Tip.y + 2f, "it broke on the bubble, below where it would have");
            Assert.IsTrue(field.IsSick(b) || b.Popped);
        }

        [Test]
        public void AFlaskBurstsOnASolidTile()
        {
            var map = StageMap.Flat();
            int c = StageMap.Col(Tip.x);
            map.Set(c, 3, new Cell { kind = TileKind.Solid });
            SetMap(map);
            Press();
            Advance(0.2f);
            Assert.AreEqual(1, field.Clouds.Count);
            Assert.AreEqual(3f, field.Clouds[0].Centre.y, 0.3f, "it broke where the tile starts");
            Assert.AreEqual(TileKind.Solid, map.Get(c, 3).kind, "tiles are unharmed");
        }

        // ------------------------------------------------------------------ sickness

        [Test]
        public void ABubbleThatTouchesTheCloudPopsByItselfAfterTheSickTime()
        {
            var b = AddBubble(new Vector2(5f, 6f));
            CloudAt(new Vector2(5f, 6f));
            field.Tick(Dt);
            Assert.IsTrue(field.IsSick(b));
            // It flies away: it still pops, wherever it is.
            b.transform.position = new Vector3(-12f, 10f, 0f);
            Advance(SickTime - 4 * Dt);
            Assert.IsFalse(b.Popped, "not before the sick time");
            Advance(6 * Dt);
            Assert.IsTrue(b.Popped);
            Assert.AreEqual(1, bubbleHits, "hit exactly once");
        }

        [Test]
        public void ABubbleOutsideTheCloudIsNotSick()
        {
            var far = AddBubble(new Vector2(5f + Tuning.I.gasRadius + 2f, 6f));
            CloudAt(new Vector2(5f, 6f));
            Advance(SickTime + 0.2f);
            Assert.IsFalse(field.IsSick(far));
            Assert.IsFalse(far.Popped);
        }

        [Test]
        public void SteelIsImmune()
        {
            var steel = AddBubble(new Vector2(5f, 6f), steel: true);
            var ball = AddRoller(5f, steel: true);
            CloudAt(new Vector2(5f, 1f));
            CloudAt(new Vector2(5f, 6f));
            Advance(SickTime + 0.2f);
            Assert.IsFalse(field.IsSick(steel));
            Assert.IsFalse(field.IsSick(ball));
            Assert.IsFalse(steel.Popped);
            Assert.IsFalse(ball.Popped);
            Assert.AreEqual(0, bubbleHits + rollerHits);
        }

        [Test]
        public void CeramicCracksAndNeedsASecondDose()
        {
            var b = AddBubble(new Vector2(5f, 6f), ceramic: true);
            int armor = b.Armor;
            CloudAt(new Vector2(5f, 6f));
            field.Tick(Dt);
            b.transform.position = new Vector3(-12f, 10f, 0f); // out of the cloud before the first dose takes
            Advance(SickTime + 0.1f);
            Assert.IsFalse(b.Popped, "the first dose only cracks it");
            Assert.AreEqual(armor - 1, b.Armor);
            Assert.IsFalse(field.IsSick(b), "sickness ends with the hit");
            b.transform.position = new Vector3(5f, 6f, 0f); // back into the cloud: a second dose
            Advance(SickTime + 0.1f);
            Assert.IsTrue(b.Popped);
        }

        [Test]
        public void AFadedGhostIsCaughtAndPops()
        {
            var ghost = AddBubble(new Vector2(5f, 6f), kind: BubbleKind.Ghost);
            var faded = typeof(GhostBehavior).GetField("faded", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(faded, "GhostBehavior.faded must exist.");
            faded.SetValue(ghost.Behavior, true);
            Assert.IsFalse(ghost.CanBeHit, "a faded Ghost cannot be hit by a harpoon");
            CloudAt(new Vector2(5f, 6f));
            Advance(SickTime + 0.1f);
            Assert.IsTrue(ghost.Popped);
        }

        [Test]
        public void ARollingBalloonTurnsSickAndPops()
        {
            var r = AddRoller(5f);
            CloudAt(new Vector2(5f, 1f));
            field.Tick(Dt);
            Assert.IsTrue(field.IsSick(r));
            Advance(SickTime + 0.1f);
            Assert.IsTrue(r.Popped);
            Assert.AreEqual(1, rollerHits);
        }

        [Test]
        public void ASickBubbleIsTintedAndGetsWellWhenHit()
        {
            var b = AddBubble(new Vector2(5f, 6f), ceramic: true);
            var sr = b.GetComponent<SpriteRenderer>();
            CloudAt(new Vector2(5f, 6f));
            field.Tick(Dt);
            Assert.AreEqual(GasField.SickTint, sr.color);
            b.transform.position = new Vector3(-12f, 10f, 0f);
            Advance(SickTime + 0.1f);
            Assert.AreEqual(Color.white, sr.color);
        }

        // ------------------------------------------------------------------ the cloud

        [Test]
        public void TheCloudRisesAndIsGoneAfterTheCloudTime()
        {
            Press();
            Advance(1f);
            Assert.AreEqual(1, field.Clouds.Count);
            float y = field.Clouds[0].Centre.y;
            Advance(0.5f);
            Assert.AreEqual(y + Tuning.I.gasRise * 0.5f, field.Clouds[0].Centre.y, 0.05f);
            Advance(Tuning.I.gasCloudTime);
            Assert.AreEqual(0, field.Clouds.Count);
        }

        [Test]
        public void TheLastFlasksCloudStillWorksAfterTheSlotEmpties()
        {
            run.SpendAmmo(WeaponSlot.Weapon, run.WeaponAmmo - 1);
            Press();
            Assert.IsNull(run.Weapon);
            var b = AddBubble(Tip + new Vector2(0f, Tuning.I.gasLobHeight), 2);
            Advance(SickTime + 1f);
            Assert.IsTrue(b.Popped);
        }

        [Test]
        public void AScorchBubbleSetsTheCloudOffAndEverythingInsideIsHitOnce()
        {
            var a = AddBubble(new Vector2(5f, 6f));
            var c = AddBubble(new Vector2(6f, 6.5f), ceramic: true);
            var outside = AddBubble(new Vector2(-10f, 6f));
            CloudAt(new Vector2(5f, 6f));
            AddBubble(new Vector2(4f, 5.5f), kind: BubbleKind.Scorch);
            field.Tick(Dt);
            Assert.AreEqual(0, field.Clouds.Count, "the cloud is gone");
            Assert.IsTrue(a.Popped);
            Assert.IsFalse(c.Popped, "ceramic takes one hit");
            Assert.AreEqual(Tuning.I.ceramicHits - 1, c.Armor);
            Assert.IsFalse(outside.Popped);
        }

        [Test]
        public void ADownThrowMakesACloudOnTheFloorThatStaysThere()
        {
            ctx.ShootDown = true;
            ctx.Muzzle = new Vector2(3f, 2.5f);
            Press();
            Assert.AreEqual(0, field.Flasks.Count, "it breaks at once");
            Assert.AreEqual(1, field.Clouds.Count);
            Assert.AreEqual(0f, field.Clouds[0].Centre.y, 1e-4f);
            var r = AddRoller(3.5f);
            Advance(1f);
            Assert.AreEqual(0f, field.Clouds[0].Centre.y, 1e-4f, "it sits on the floor");
            Advance(SickTime);
            Assert.IsTrue(r.Popped);
        }

        [Test]
        public void TheBossLosesHealthEachSecondUpToTheCapPerFlask()
        {
            var boss = Boss.Create(root.transform, BossSpec.ForWorld(1), 1, Themes.ForWorld(1));
            world.Destroy();
            world = MakeWorld(boss);
            field.World = world;
            int total = 0;
            world.OnDamageBoss = (n, _) => total += n;
            var y = typeof(Boss).GetField("y", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(y, "Boss.y must exist.");
            y.SetValue(boss, 9f);
            var cloud = CloudAt(boss.Body.center);
            Advance(1.05f);
            Assert.AreEqual(Mathf.FloorToInt(Tuning.I.gasBossDamage * 1f + 1e-3f), total, "one second inside");
            Advance(Tuning.I.gasCloudTime);
            Assert.AreEqual(Mathf.FloorToInt(Tuning.I.gasBossMax + 1e-3f), total, "never more than the cap per flask");
            Assert.AreEqual(Tuning.I.gasBossMax, cloud.BossTaken, 1e-3f);
        }
    }
}
