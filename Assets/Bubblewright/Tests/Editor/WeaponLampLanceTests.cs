using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>W06 Lamp Lance: one charge, a wind-up, then a column beam that removes everything whole for half a second.</summary>
    public class WeaponLampLanceTests
    {
        const float Dt = 1f / 60f;

        GameObject root;
        StageMap previousMap;
        List<Bubble> bubbles;
        List<Roller> rollers;
        List<Bullet> bullets;
        WeaponWorld world;
        Run run;
        WeaponSpec lance;
        LampLance.Behaviour behaviour;
        WeaponContext ctx;
        readonly List<Vector2Int> tileHits = new List<Vector2Int>();

        /// <summary>The weapon tip in the tests: standing in the middle of a tile column, about 1.4 units above the floor.</summary>
        static readonly Vector2 Tip = new Vector2(0.5f, 21f / 15f);

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("WeaponLampLanceTests");
            previousMap = Game.Map;
            SetMap(StageMap.Flat());
            Game.Ice.Clear();
            bubbles = new List<Bubble>();
            rollers = new List<Roller>();
            bullets = new List<Bullet>();
            tileHits.Clear();
            world = MakeWorld(null);

            lance = WeaponSpec.ById("lance");
            Assert.NotNull(lance, "the Lamp Lance is in WeaponSpec.All");
            run = new Run(1);
            Assert.IsTrue(run.GiveWeapon(lance));
            behaviour = (LampLance.Behaviour)lance.CreateBehaviour();
            ctx = new WeaponContext
            {
                CanFire = true,
                Muzzle = Tip,
                Up = Vector2.up,
                Spec = lance,
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
            };
        }

        [TearDown]
        public void TearDown()
        {
            behaviour?.Dispose();
            world.Destroy();
            Game.Ice.Clear();
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

        void Frames(int n)
        {
            for (int i = 0; i < n; i++) Frame(false);
        }

        static int WindUpFrames => Mathf.CeilToInt(Tuning.I.lanceWindUp / Dt) + 1;
        static int BeamFrames => Mathf.CeilToInt(Tuning.I.lanceBeamTime / Dt);

        /// <summary>Press and wait out the wind-up: the beam is out after this.</summary>
        void FireBeam()
        {
            Frame(true);
            Frames(WindUpFrames);
            Assert.AreEqual(LampLance.Phase.Beam, behaviour.Phase, "the beam is out after the wind-up");
        }

        /// <summary>Press and run the whole shot to the end.</summary>
        void FireAll()
        {
            Frame(true);
            Frames(WindUpFrames + BeamFrames + LampLance.FadeFrames + 4);
            Assert.AreEqual(LampLance.Phase.Done, behaviour.Phase);
        }

        // ------------------------------------------------------------------ table and ammo

        [Test]
        public void TheLanceIsAVeryRareWeaponSlotWeaponThatDrops()
        {
            Assert.AreEqual("Lamp Lance", lance.Name);
            Assert.AreEqual(WeaponSlot.Weapon, lance.Slot);
            Assert.AreEqual(WeaponTier.VeryRare, lance.Tier);
            Assert.IsTrue(lance.Drops);
            Assert.AreEqual(Tuning.I.lanceAmmo, lance.MaxAmmo, "a full crate comes from Tuning");
            Assert.AreEqual(Tuning.I.lanceAmmo, run.WeaponAmmo);
            Assert.LessOrEqual(WeaponArt.WidthOf(lance.IconRows), 11);
            Assert.NotNull(WeaponArt.Held(lance));
        }

        [Test]
        public void ASecondLanceIsRefusedAndTheCrateStays()
        {
            Assert.IsFalse(run.GiveWeapon(lance), "GiveWeapon refuses a second Lamp Lance");
            Assert.AreEqual(Tuning.I.lanceAmmo, run.WeaponAmmo);
            Assert.AreSame(lance, run.Weapon);
        }

        [Test]
        public void TheChargeIsSpentWhenTheBeamEndsAndTheSlotEmpties()
        {
            FireBeam();
            Assert.AreSame(lance, run.Weapon, "still held while the beam is out");
            Frames(BeamFrames + LampLance.FadeFrames + 4);
            Assert.IsTrue(behaviour.Spent);
            Assert.IsNull(run.Weapon, "the slot empties at zero");
            Assert.AreEqual(0, run.WeaponAmmo);
        }

        [Test]
        public void AShotCutShortByTheStageEndingIsStillSpent()
        {
            FireBeam();
            behaviour.Dispose();
            Assert.IsNull(run.Weapon, "a started shot is never kept for the next stage");
            behaviour = null;
        }

        [Test]
        public void AnUnusedLanceIsKeptWhenTheStageEnds()
        {
            behaviour.Dispose();
            Assert.AreSame(lance, run.Weapon);
            behaviour = null;
        }

        [Test]
        public void NothingFiresWithoutAPressOrWhenItCannotFire()
        {
            Frames(60);
            Assert.AreEqual(LampLance.Phase.Ready, behaviour.Phase);
            ctx.CanFire = false;
            Frame(true);
            Frames(60);
            Assert.AreEqual(LampLance.Phase.Ready, behaviour.Phase);
            Assert.AreSame(lance, run.Weapon);
        }

        // ------------------------------------------------------------------ the beam

        [Test]
        public void NothingIsRemovedDuringTheWindUp()
        {
            var b = AddBubble(Tip + new Vector2(0f, 4f), 0);
            Frame(true);
            Frames(Mathf.FloorToInt(Tuning.I.lanceWindUp / Dt) - 2);
            Assert.AreEqual(LampLance.Phase.WindUp, behaviour.Phase);
            Assert.Contains(b, bubbles, "a bubble can still reach him during the wind-up");
        }

        [Test]
        public void AnXLBubbleInTheBeamIsGoneWithNoPieces()
        {
            AddBubble(Tip + new Vector2(0f, 6f), 0);
            FireBeam();
            Assert.AreEqual(0, bubbles.Count, "removed whole: no smaller bubbles left");
            Assert.AreEqual(1, behaviour.Total.bubbles);
        }

        [Test]
        public void ABubbleBesideTheBeamIsUntouched()
        {
            float clear = Tuning.I.lanceWidth * 0.5f + Tuning.I.tiers[3].diameter * 0.5f + 0.3f;
            var side = AddBubble(Tip + new Vector2(clear, 3f));
            FireAll();
            Assert.Contains(side, bubbles);
            Assert.IsFalse(side.Popped);
        }

        [Test]
        public void SteelAndCeramicBubblesAreDestroyed()
        {
            AddBubble(Tip + new Vector2(0f, 3f), 2, steel: true);
            AddBubble(Tip + new Vector2(0f, 7f), 2, ceramic: true);
            FireBeam();
            Assert.AreEqual(0, bubbles.Count, "the only weapon that removes steel; ceramic goes at once");
        }

        [Test]
        public void ASteelRollingBallInTheBeamIsGone()
        {
            // Rolling balls sit on the floor; fired down from the air, the beam reaches them.
            ctx.ShootDown = true;
            ctx.Up = Vector2.down;
            ctx.Muzzle = new Vector2(Tip.x, 4f);
            AddRoller(Tip.x, steel: true);
            AddRoller(Tip.x - 6f);
            FireBeam();
            Assert.AreEqual(1, rollers.Count, "the steel ball under him is gone, the far one is not");
            Assert.AreEqual(1, behaviour.Total.rollers);
        }

        [Test]
        public void ABubbleThatDriftsIntoTheBeamLaterIsAlsoRemoved()
        {
            FireBeam();
            Frames(3);
            AddBubble(Tip + new Vector2(0f, 5f));
            Frames(2);
            Assert.AreEqual(0, bubbles.Count, "everything that touches it during the beam goes");
        }

        [Test]
        public void ItGoesThroughPlatformsWithoutHarmingThem()
        {
            var map = StageMap.Flat();
            int c = StageMap.Col(Tip.x);
            map.Set(c, 4, new Cell { kind = TileKind.Solid });
            SetMap(map);
            AddBubble(new Vector2(Tip.x, 9f));
            FireBeam();
            Assert.AreEqual(0, bubbles.Count, "the bubble above the platform is gone");
            Assert.AreEqual(TileKind.Solid, map.Get(c, 4).kind, "the platform is not harmed");
        }

        [Test]
        public void EveryBreakableBlockInTheColumnIsDestroyed()
        {
            var map = StageMap.Flat();
            int c = StageMap.Col(Tip.x);
            map.Set(c, 3, new Cell { kind = TileKind.Breakable, hp = 5 });
            map.Set(c, 7, new Cell { kind = TileKind.Breakable, hp = 2 });
            map.Set(c + 3, 3, new Cell { kind = TileKind.Breakable, hp = 2 });
            SetMap(map);
            FireBeam();
            Assert.AreEqual(TileKind.Empty, map.Get(c, 3).kind);
            Assert.AreEqual(TileKind.Empty, map.Get(c, 7).kind);
            Assert.AreEqual(TileKind.Breakable, map.Get(c + 3, 3).kind, "outside the beam");
            Assert.AreEqual(2, behaviour.Total.tiles);
        }

        [Test]
        public void ItMeltsFrostIceItTouches()
        {
            var map = StageMap.Flat();
            int c = StageMap.Col(Tip.x);
            map.Set(c, 5, new Cell { kind = TileKind.Solid, surface = Surface.Normal });
            map.Set(c + 4, 5, new Cell { kind = TileKind.Solid, surface = Surface.Normal });
            SetMap(map);
            Game.Ice.FreezeTile(map, c, 5);
            Game.Ice.FreezeTile(map, c + 4, 5);
            Assume.That(map.Get(c, 5).surface, Is.EqualTo(Surface.Ice));
            FireBeam();
            Assert.AreEqual(Surface.Normal, map.Get(c, 5).surface, "melted back to what it was");
            Assert.AreEqual(Surface.Ice, map.Get(c + 4, 5).surface, "ice outside the beam stays");
            Assert.AreEqual(1, Game.Ice.Count);
        }

        [Test]
        public void FiredDownTheBeamGoesToTheFloorNotUp()
        {
            ctx.ShootDown = true;
            ctx.Up = Vector2.down;
            ctx.Muzzle = new Vector2(Tip.x, 8f);
            var below = AddBubble(new Vector2(Tip.x, 4f));
            var above = AddBubble(new Vector2(Tip.x, 12f));
            FireBeam();
            Assert.IsFalse(bubbles.Contains(below), "the beam goes down");
            Assert.Contains(above, bubbles, "nothing above him is touched");
            Assert.AreEqual(0f, behaviour.Beam.yMin, 1e-4f);
        }

        // ------------------------------------------------------------------ bosses

        [Test]
        public void BossBulletsInTheBeamAreRemoved()
        {
            var theme = Themes.ForWorld(1);
            var inBeam = Bullet.Create(root.transform, Tip + new Vector2(0f, 5f), new Vector2(0f, -1f), theme);
            var far = Bullet.Create(root.transform, Tip + new Vector2(6f, 5f), new Vector2(0f, -1f), theme);
            bullets.Add(inBeam);
            bullets.Add(far);
            FireBeam();
            Assert.IsTrue(inBeam.Dead);
            Assert.IsFalse(far.Dead);
        }

        [Test]
        public void TheBossTakesLanceBossDamageOnceForTheWholeBeam()
        {
            var boss = Boss.Create(root.transform, BossSpec.ForWorld(1), 1, Themes.ForWorld(1));
            world.Destroy();
            world = MakeWorld(boss);
            ctx.World = world;
            int total = 0, calls = 0;
            world.OnDamageBoss = (n, _) => { total += n; calls++; };
            var y = typeof(Boss).GetField("y", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(y, "Boss.y must exist.");
            y.SetValue(boss, 9f);
            ctx.Muzzle = new Vector2(boss.Body.center.x, Tip.y);
            FireAll();
            Assert.AreEqual(1, calls, "once, however long the boss stays in the beam");
            Assert.AreEqual(Mathf.FloorToInt(Tuning.I.lanceBossDamage + 1e-3f), total);
        }
    }
}
