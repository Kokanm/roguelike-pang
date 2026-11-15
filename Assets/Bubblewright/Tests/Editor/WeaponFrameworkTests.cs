using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>W00: the two weapon slots, saving, crate drops and the shared Shot with its hit rules.</summary>
    public class WeaponFrameworkTests
    {
        const float Dt = 1f / 60f;

        sealed class FakeGun : WeaponSpec
        {
            readonly string id;
            readonly int max;
            readonly WeaponSlot slot;
            readonly bool second;
            readonly WeaponTier tier;
            public FakeGun(string id, int max = 10, WeaponSlot slot = WeaponSlot.Weapon, bool second = true, WeaponTier tier = WeaponTier.Common)
            {
                this.id = id; this.max = max; this.slot = slot; this.second = second; this.tier = tier;
            }
            public override string Id => id;
            public override string Name => id;
            public override WeaponSlot Slot => slot;
            public override WeaponTier Tier => tier;
            public override int MaxAmmo => max;
            public override bool AllowSecondCopy => second;
            public override string[] IconRows => new[] { "S" };
            public override WeaponBehaviour CreateBehaviour() => null;
        }

        GameObject root;
        StageMap previousMap;
        Rect previousBox;
        List<Bubble> bubbles;
        List<Roller> rollers;
        WeaponWorld world;
        readonly List<Vector2Int> tileHits = new List<Vector2Int>();
        int steelHits;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(SaveGame.Key);
            root = new GameObject("WeaponFrameworkTests");
            previousMap = Game.Map;
            previousBox = Game.WickBox;
            Game.WickBox = new Rect(100f, 100f, 1f, 1f);
            SetMap(StageMap.Flat());
            bubbles = new List<Bubble>();
            rollers = new List<Roller>();
            tileHits.Clear();
            steelHits = 0;
            world = new WeaponWorld(bubbles, rollers, null, null, root.transform)
            {
                // The same order of events as Game.Hit and Game.HitRoller: a ceramic one cracks, the rest swell to pop.
                OnHitBubble = b => { if (!b.AbsorbHit()) b.Swell(Tuning.I.popSwell); },
                OnHitRoller = r => { if (!r.AbsorbHit()) r.Swell(Tuning.I.popSwell); },
                OnHitTile = (c, r) => { tileHits.Add(new Vector2Int(c, r)); Game.Map.Damage(c, r); },
                OnSteel = _ => steelHits++,
            };
        }

        [TearDown]
        public void TearDown()
        {
            world.Destroy();
            SetMap(previousMap);
            Game.WickBox = previousBox;
            Object.DestroyImmediate(root);
            PlayerPrefs.DeleteKey(SaveGame.Key);
        }

        static void SetMap(StageMap map)
        {
            var property = typeof(Game).GetProperty("Map", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(property, "Game.Map must exist.");
            property.SetValue(null, map);
        }

        Bubble AddBubble(Vector2 at, int tier = 2, bool ceramic = false, BubbleKind kind = BubbleKind.Gloop, bool steel = false)
        {
            var b = Bubble.Create(root.transform, kind, tier, at, Vector2.zero, false, ceramic, steel, steel);
            bubbles.Add(b);
            return b;
        }

        void Fly(int frames)
        {
            for (int i = 0; i < frames; i++) world.TickShots(Dt);
        }

        // ------------------------------------------------------------------ table

        [Test]
        public void TheTableHoldsTheTestWeaponWhichNeverDrops()
        {
            var test = WeaponSpec.ById("test");
            Assert.NotNull(test, "the debug test weapon is in WeaponSpec.All");
            Assert.AreEqual(WeaponSlot.Weapon, test.Slot);
            Assert.AreEqual(20, test.MaxAmmo);
            Assert.IsFalse(test.Drops);
            Assert.IsNull(WeaponSpec.ById("nope"));
            Assert.IsNull(WeaponSpec.ById(null));
            var ids = new HashSet<string>();
            foreach (var w in WeaponSpec.All) Assert.IsTrue(ids.Add(w.Id), $"id {w.Id} is unique");
        }

        [Test]
        public void EveryWeaponHasArt()
        {
            foreach (var w in WeaponSpec.All)
            {
                Assert.NotNull(w.Icon, w.Id);
                Assert.LessOrEqual(WeaponArt.WidthOf(w.IconRows), 11, $"{w.Id} icon fits 11 px");
                Assert.NotNull(WeaponArt.Crate(w), w.Id);
                Assert.NotNull(WeaponArt.Button(w, false), w.Id);
                if (w.HeldRows != null) Assert.NotNull(WeaponArt.Held(w), w.Id);
            }
            Assert.NotNull(WeaponArt.ShotSprite);
        }

        // ------------------------------------------------------------------ slots

        [Test]
        public void GivingAWeaponFillsItsSlotWithAFullLoad()
        {
            var run = new Run(1);
            var gun = new FakeGun("gun", 10);
            Assert.IsTrue(run.GiveWeapon(gun));
            Assert.AreSame(gun, run.Weapon);
            Assert.AreEqual(10, run.WeaponAmmo);
            Assert.IsNull(run.Head, "the harpoon slot is separate");
        }

        [Test]
        public void TheSameWeaponTopsUpToOneAndAHalfLoadsRoundedDown()
        {
            var run = new Run(1);
            var gun = new FakeGun("gun", 7);
            run.GiveWeapon(gun);
            run.SpendAmmo(WeaponSlot.Weapon, 2); // 5 left
            Assert.IsTrue(run.GiveWeapon(gun));
            Assert.AreEqual(10, run.WeaponAmmo, "5 + 7 is capped at floor(7 x 1.5) = 10");
            Assert.AreEqual(Mathf.FloorToInt(7 * Tuning.I.weaponTopUpScale), gun.AmmoCap);
            Assert.IsTrue(run.GiveWeapon(gun));
            Assert.AreEqual(10, run.WeaponAmmo, "never above the cap");
        }

        [Test]
        public void ADifferentWeaponReplacesTheHeldOneAndItsAmmo()
        {
            var run = new Run(1);
            run.GiveWeapon(new FakeGun("a", 10));
            run.SpendAmmo(WeaponSlot.Weapon, 3);
            var b = new FakeGun("b", 4);
            Assert.IsTrue(run.GiveWeapon(b));
            Assert.AreSame(b, run.Weapon);
            Assert.AreEqual(4, run.WeaponAmmo);
        }

        [Test]
        public void AWeaponCanRefuseASecondCopy()
        {
            var run = new Run(1);
            var lance = new FakeGun("lance", 1, second: false);
            Assert.IsTrue(run.GiveWeapon(lance));
            Assert.IsFalse(run.GiveWeapon(lance), "the crate stays on the floor");
            Assert.AreEqual(1, run.WeaponAmmo);
            Assert.IsFalse(run.GiveWeapon(null));
        }

        [Test]
        public void AHeadGoesInTheHarpoonSlotBesideAWeapon()
        {
            var run = new Run(1);
            run.GiveWeapon(new FakeGun("gun", 10));
            var head = new FakeGun("head", 12, WeaponSlot.Harpoon);
            Assert.IsTrue(run.GiveWeapon(head));
            Assert.AreSame(head, run.Head);
            Assert.AreEqual(12, run.HeadAmmo);
            Assert.AreEqual("gun", run.Weapon.Id, "holding a head keeps the weapon");
        }

        [Test]
        public void SpendingTheLastAmmoEmptiesTheSlot()
        {
            var run = new Run(1);
            run.GiveWeapon(new FakeGun("gun", 2));
            Assert.IsTrue(run.SpendAmmo(WeaponSlot.Weapon));
            Assert.NotNull(run.Weapon);
            Assert.IsTrue(run.SpendAmmo(WeaponSlot.Weapon));
            Assert.IsNull(run.Weapon, "gone at zero");
            Assert.AreEqual(0, run.WeaponAmmo);
            Assert.IsFalse(run.SpendAmmo(WeaponSlot.Weapon), "nothing left to spend");
        }

        [Test]
        public void SpendingMoreThanIsLeftSpendsNothing()
        {
            var run = new Run(1);
            run.GiveWeapon(new FakeGun("gun", 3));
            Assert.IsFalse(run.SpendAmmo(WeaponSlot.Weapon, 4));
            Assert.AreEqual(3, run.WeaponAmmo);
        }

        // ------------------------------------------------------------------ saving

        [Test]
        public void WeaponsSurviveSaveAndLoad()
        {
            var run = new Run(77);
            var test = WeaponSpec.ById("test");
            run.GiveWeapon(test);
            run.SpendAmmo(WeaponSlot.Weapon, 5);
            SaveGame.Save(run);
            var loaded = SaveGame.Load();
            Assert.NotNull(loaded);
            Assert.AreSame(test, loaded.Weapon);
            Assert.AreEqual(15, loaded.WeaponAmmo);
            Assert.IsNull(loaded.Head);
            Assert.AreEqual(0, loaded.HeadAmmo);
        }

        [Test]
        public void UnknownOrMissingWeaponIdsLoadAsEmpty()
        {
            var save = new Run(5).ToSave();
            save.weapon = "no-such-weapon";
            save.weaponAmmo = 9;
            save.head = "test"; // a weapon-slot id in the head slot
            save.headAmmo = 4;
            var run = Run.FromSave(save);
            Assert.IsNull(run.Weapon);
            Assert.AreEqual(0, run.WeaponAmmo);
            Assert.IsNull(run.Head);
            Assert.AreEqual(0, run.HeadAmmo);

            var old = JsonUtility.FromJson<SaveData>("{\"version\":1,\"seed\":3,\"stage\":2,\"hearts\":3,\"maxHearts\":3}");
            var fromOld = Run.FromSave(old);
            Assert.IsNull(fromOld.Weapon, "a save from before weapons has none");
        }

        [Test]
        public void SavedAmmoIsCappedAndZeroAmmoLoadsEmpty()
        {
            var save = new Run(5).ToSave();
            save.weapon = "test";
            save.weaponAmmo = 999;
            Assert.AreEqual(WeaponSpec.ById("test").AmmoCap, Run.FromSave(save).WeaponAmmo);
            save.weaponAmmo = 0;
            Assert.IsNull(Run.FromSave(save).Weapon);
        }

        // ------------------------------------------------------------------ drops

        static List<WeaponSpec> Table() => new List<WeaponSpec>
        {
            new FakeGun("c1", tier: WeaponTier.Common),
            new FakeGun("u1", tier: WeaponTier.Uncommon),
            new FakeGun("r1", tier: WeaponTier.Rare),
            new FakeGun("v1", tier: WeaponTier.VeryRare),
        };

        static int FirstStageOfWorld(int w) => (w - 1) * Tuning.I.stagesPerWorld + 1;

        static List<string> Rolls(int seed, int stage, int pops, float chance = 0.5f)
        {
            var d = new WeaponDropper(seed, stage, Table());
            var got = new List<string>();
            for (int i = 0; i < pops; i++) got.Add(d.Roll(chance, false)?.Id ?? "-");
            return got;
        }

        [Test]
        public void DropsRepeatForTheSameSeedAndStage()
        {
            int stage = FirstStageOfWorld(3) + 1;
            CollectionAssert.AreEqual(Rolls(123, stage, 40), Rolls(123, stage, 40));
            Assert.AreNotEqual(string.Join(",", Rolls(123, stage, 40)), string.Join(",", Rolls(124, stage, 40)),
                "another seed drops differently");
        }

        [Test]
        public void AStageDropsAtMostTheCap()
        {
            int stage = FirstStageOfWorld(3) + 1;
            var d = new WeaponDropper(9, stage, Table());
            int n = 0;
            for (int i = 0; i < 500; i++) if (d.Roll(1f, false) != null) n++;
            Assert.AreEqual(Tuning.I.maxCratesPerStage, n);
        }

        [Test]
        public void NoCratesInTheFirstStagesOfWorldOne()
        {
            for (int stage = 1; stage <= Tuning.I.crateFreeStages; stage++)
            {
                Assert.IsFalse(WeaponDropper.StageAllowsCrates(stage));
                var d = new WeaponDropper(9, stage, Table());
                for (int i = 0; i < 200; i++) Assert.IsNull(d.Roll(1f, false), $"stage {stage}");
            }
            Assert.IsTrue(WeaponDropper.StageAllowsCrates(Tuning.I.crateFreeStages + 1));
        }

        [Test]
        public void NoCrateWhileAnotherIsOnScreen()
        {
            var d = new WeaponDropper(9, FirstStageOfWorld(2), Table());
            for (int i = 0; i < 100; i++) Assert.IsNull(d.Roll(1f, true));
            Assert.AreEqual(0, d.Dropped);
        }

        [Test]
        public void TiersOpenByWorld()
        {
            var seen = new HashSet<string>();
            for (int seed = 0; seed < 300; seed++)
            {
                var w1 = new WeaponDropper(seed, FirstStageOfWorld(1) + Tuning.I.crateFreeStages, Table()).Roll(1f, false);
                if (w1 != null) seen.Add(w1.Id);
            }
            CollectionAssert.AreEquivalent(new[] { "c1" }, seen, "world 1 drops only common weapons");

            seen.Clear();
            for (int seed = 0; seed < 300; seed++)
            {
                var w2 = new WeaponDropper(seed, FirstStageOfWorld(2), Table()).Roll(1f, false);
                if (w2 != null) seen.Add(w2.Id);
            }
            CollectionAssert.AreEquivalent(new[] { "c1", "u1" }, seen, "world 2 adds uncommon");

            seen.Clear();
            for (int seed = 0; seed < 3000; seed++)
            {
                var w3 = new WeaponDropper(seed, FirstStageOfWorld(3), Table()).Roll(1f, false);
                if (w3 != null) seen.Add(w3.Id);
            }
            CollectionAssert.AreEquivalent(new[] { "c1", "u1", "r1", "v1" }, seen, "world 3 adds rare and very rare");
        }

        [Test]
        public void WithOnlyTheTestWeaponNothingDrops()
        {
            var d = new WeaponDropper(4, FirstStageOfWorld(3), new[] { WeaponSpec.ById("test") });
            for (int i = 0; i < 200; i++) Assert.IsNull(d.Roll(1f, false));
        }

        // ------------------------------------------------------------------ shots

        [Test]
        public void AShotStopsAtASolidTile()
        {
            var map = StageMap.Flat();
            map.Set(16, 5, new Cell { kind = TileKind.Solid });
            SetMap(map);
            var shot = world.FireShot(new Vector2(StageMap.CenterX(16), 1f), Vector2.up * 36f, 0f);
            Fly(30);
            Assert.IsFalse(shot.Alive);
            Assert.Less(shot.Position.y, 6f, "it never got past the tile");
            Assert.AreEqual(0, world.Shots.Count);
            Assert.AreEqual(0, tileHits.Count, "a solid tile is not hit");
        }

        [Test]
        public void AShotDamagesABreakableTileOnce()
        {
            var map = StageMap.Flat();
            map.Set(16, 5, new Cell { kind = TileKind.Breakable, hp = 3 });
            SetMap(map);
            world.FireShot(new Vector2(StageMap.CenterX(16), 1f), Vector2.up * 36f, 0f);
            Fly(60);
            Assert.AreEqual(1, tileHits.Count);
            Assert.AreEqual(2, Game.Map.Get(16, 5).hp, "one hit taken");
        }

        [Test]
        public void AShotLeavesAtTheCeiling()
        {
            var shot = world.FireShot(new Vector2(0f, 1f), Vector2.up * 36f, 0f);
            Fly(60);
            Assert.IsFalse(shot.Alive);
            Assert.AreEqual(0, world.Shots.Count);
        }

        [Test]
        public void AShotDownStopsAtTheFloor()
        {
            var shot = world.FireShot(new Vector2(0f, 5f), Vector2.down * 36f, 0f);
            Fly(60);
            Assert.IsFalse(shot.Alive);
        }

        [Test]
        public void AShotPopsAnOrdinaryBubble()
        {
            var b = AddBubble(new Vector2(0f, 6f));
            world.FireShot(new Vector2(0f, 1f), Vector2.up * 36f, 0f);
            Fly(30);
            Assert.IsTrue(b.Popped);
            Assert.AreEqual(0, world.Shots.Count, "the shot is spent");
        }

        [Test]
        public void AShotSparksOffSteelAndIsSpent()
        {
            var steel = AddBubble(new Vector2(0f, 6f), steel: true);
            Assert.IsTrue(steel.Steel);
            var shot = world.FireShot(new Vector2(0f, 1f), Vector2.up * 36f, 0f);
            Fly(30);
            Assert.IsFalse(steel.Popped, "steel never pops");
            Assert.AreEqual(1, steelHits, "it sparkles");
            Assert.IsFalse(shot.Alive);
            Assert.Less(shot.Position.y, 6f, "it stopped at the steel");
        }

        [Test]
        public void AShotSparksOffASteelRoller()
        {
            var r = Roller.Create(root.transform, 0, 0f, 0f, 1, steel: true);
            rollers.Add(r);
            world.FireShot(new Vector2(0f, 0.2f), Vector2.up * 36f, 0f);
            Fly(10);
            Assert.IsFalse(r.Popped);
            Assert.AreEqual(1, steelHits);
        }

        [Test]
        public void AShotTakesOneOfTwoHitsOffCeramic()
        {
            Assert.AreEqual(2, Tuning.I.ceramicHits);
            var c = AddBubble(new Vector2(0f, 6f), tier: 1, ceramic: true);
            world.FireShot(new Vector2(0f, 1f), Vector2.up * 36f, 0f);
            Fly(30);
            Assert.IsFalse(c.Popped, "the first shot only cracks it");
            Assert.AreEqual(1, c.Armor);
            world.FireShot(new Vector2(0f, 1f), Vector2.up * 36f, 0f);
            Fly(30);
            Assert.IsTrue(c.Popped, "the second breaks it");
        }

        [Test]
        public void AShotPassesThroughAFadedGhost()
        {
            var ghost = AddBubble(new Vector2(0f, 6f), tier: 2, kind: BubbleKind.Ghost);
            var plain = AddBubble(new Vector2(0f, 11f), tier: 3);
            for (int i = 0; i < 156 && ghost.CanBeHit; i++) ghost.Tick(Dt);
            ghost.transform.position = new Vector3(0f, 6f, 0f);
            Assert.IsFalse(ghost.CanBeHit, "the Ghost has faded");
            world.FireShot(new Vector2(0f, 1f), Vector2.up * 36f, 0f);
            Fly(30);
            Assert.IsFalse(ghost.Popped, "a faded Ghost is not hit");
            Assert.IsTrue(plain.Popped, "the shot went on through it");
        }

        [Test]
        public void ShotsAreReusedFromThePool()
        {
            var a = world.FireShot(new Vector2(0f, 1f), Vector2.up * 36f, 0f);
            Fly(60);
            var b = world.FireShot(new Vector2(0f, 1f), Vector2.up * 36f, 0f);
            Assert.AreSame(a, b);
            Assert.IsTrue(b.Alive);
            Assert.AreEqual(0f, b.Travelled);
        }

        [Test]
        public void AShotWithARangeFadesAtItsEnd()
        {
            var s = world.FireShot(new Vector2(0f, 1f), Vector2.up * 30f, 0f, range: 3f);
            Fly(5); // 2.5 units
            Assert.IsTrue(s.Alive);
            Fly(2); // 3.5 units
            Assert.IsFalse(s.Alive);
        }

        // ------------------------------------------------------------------ boss damage and queries

        [Test]
        public void BossDamageFractionsAddUpToWholeHits()
        {
            var boss = Boss.Create(root.transform, BossSpec.ForWorld(1), 1, Themes.ForWorld(1));
            var w = new WeaponWorld(bubbles, rollers, null, () => boss, root.transform);
            var hits = new List<int>();
            w.OnDamageBoss = (n, _) => hits.Add(n);
            for (int i = 0; i < 5; i++) w.WeaponHitBoss(0.4f, Vector2.zero);
            CollectionAssert.AreEqual(new[] { 1, 1 }, hits, "0.4 x 5 = 2 whole hits, passed on as they add up");
            Assert.AreEqual(0f, w.BossCarry, 1e-4f);
            w.WeaponHitBoss(2.5f, Vector2.zero);
            CollectionAssert.AreEqual(new[] { 1, 1, 2 }, hits);
            Assert.AreEqual(0.5f, w.BossCarry, 1e-4f);
        }

        [Test]
        public void QueriesFindThingsInsideAndSortThemNearestFirst()
        {
            var near = AddBubble(new Vector2(0f, 3f), tier: 3);
            var far = AddBubble(new Vector2(0f, 8f), tier: 3);
            var aside = AddBubble(new Vector2(6f, 3f), tier: 3);

            var line = world.AlongSegment(new Vector2(0f, 0f), new Vector2(0f, 14f), 0.2f);
            Assert.AreEqual(2, line.Count);
            Assert.AreSame(near, line[0].bubble);
            Assert.AreSame(far, line[1].bubble);

            var circle = world.InCircle(new Vector2(0f, 3f), 1f);
            Assert.AreEqual(1, circle.Count);
            Assert.AreSame(near, circle[0].bubble);

            var up = world.InSector(new Vector2(0f, 0f), 10f, Vector2.up, 40f);
            Assert.AreEqual(2, up.Count, "a narrow arc up misses the one to the side");
            var right = world.InSector(new Vector2(0f, 0f), 10f, Vector2.right, 120f);
            Assert.AreEqual(1, right.Count, "an arc to the right finds only the one to the side");
            Assert.AreSame(aside, right[0].bubble);
        }

        [Test]
        public void TheHitHelpersFollowTheSharedRules()
        {
            var plain = AddBubble(new Vector2(0f, 3f));
            var steel = AddBubble(new Vector2(3f, 3f), steel: true);
            Assert.IsTrue(world.WeaponHit(plain));
            Assert.IsTrue(plain.Popped);
            Assert.IsFalse(world.WeaponHit(plain), "already popped");
            Assert.IsFalse(world.WeaponHit(steel));
            Assert.AreEqual(1, steelHits);
            var roller = Roller.Create(root.transform, 0, -5f, 0f, 1, ceramic: true);
            Assert.IsTrue(world.WeaponHit(roller), "ceramic takes the hit");
            Assert.IsFalse(roller.Popped, "and cracks");
        }
    }
}
