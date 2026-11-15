using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>W10 Double Harpoon: two cables per volley, the next volley after both are gone, shared harpoon rules.</summary>
    public class WeaponDoubleHarpoonTests
    {
        const float Dt = 1f / 60f;

        GameObject root;
        StageMap previousMap;
        Run run;
        WeaponSpec twin;
        WeaponBehaviour behaviour;
        WeaponContext ctx;
        List<Harpoon> harpoons;
        readonly List<Vector2Int> tileHits = new List<Vector2Int>();

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("WeaponDoubleHarpoonTests");
            previousMap = Game.Map;
            SetMap(StageMap.Flat());
            tileHits.Clear();
            harpoons = new List<Harpoon>();
            twin = WeaponSpec.ById("twin");
            Assert.NotNull(twin, "the Double Harpoon is in WeaponSpec.All");
            Give(new Run(1));
        }

        [TearDown]
        public void TearDown()
        {
            behaviour?.Dispose();
            SetMap(previousMap);
            Object.DestroyImmediate(root);
        }

        void Give(Run r)
        {
            run = r;
            Assert.IsTrue(run.GiveWeapon(twin));
            behaviour = twin.CreateBehaviour();
            ctx = new WeaponContext
            {
                CanFire = true,
                Muzzle = new Vector2(0f, 1f),
                Up = Vector2.up,
                Spec = twin,
                Run = run,
            };
            SetInternal("harpoons", (System.Func<int, IReadOnlyList<Harpoon>>)(n =>
            {
                while (harpoons.Count < n) harpoons.Add(Harpoon.Create(root.transform, (c, row) =>
                {
                    tileHits.Add(new Vector2Int(c, row));
                    Game.Map.Damage(c, row);
                }));
                return harpoons;
            }));
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

        /// <summary>A fire press the way Game handles it: ask the head, fire its volley, spend one.</summary>
        bool Press()
        {
            if (!behaviour.VolleyReady(ctx)) return false;
            if (!behaviour.FireVolley(ctx)) return false;
            run.SpendAmmo(WeaponSlot.Harpoon);
            return true;
        }

        List<Harpoon> Active()
        {
            var list = new List<Harpoon>();
            foreach (var h in harpoons) if (h.Active) list.Add(h);
            list.Sort((a, b) => a.CableRect.center.x.CompareTo(b.CableRect.center.x));
            return list;
        }

        void Tick(int frames)
        {
            for (int i = 0; i < frames; i++) foreach (var h in harpoons) h.Tick(Dt);
        }

        void HideAll()
        {
            foreach (var h in harpoons) h.Hide();
        }

        // ------------------------------------------------------------------ spec and ammo

        [Test]
        public void IsACommonHarpoonHeadWithAmmoFromTuning()
        {
            Assert.AreEqual(WeaponSlot.Harpoon, twin.Slot);
            Assert.AreEqual(WeaponTier.Common, twin.Tier);
            Assert.AreEqual(Tuning.I.twinAmmo, twin.MaxAmmo);
            Assert.AreEqual(12, Tuning.I.twinAmmo, "the sheet's starting value");
            Assert.AreSame(twin, run.Head, "it goes in the harpoon slot");
            Assert.IsNull(run.Weapon, "the weapon slot stays free for a gun");
            Assert.AreEqual(11, twin.IconRows[0].Length, "icons are 11 px wide");
            Assert.IsNull(twin.HeldRows, "he keeps the launcher");
        }

        [Test]
        public void EachVolleySpendsOneAndTheHeadIsGoneAtZero()
        {
            int full = Tuning.I.twinAmmo;
            for (int i = 0; i < full; i++)
            {
                Assert.AreEqual(full - i, run.HeadAmmo);
                Assert.IsTrue(Press(), $"volley {i + 1} fires");
                HideAll();
            }
            Assert.AreEqual(0, run.HeadAmmo);
            Assert.IsNull(run.Head, "after the last volley the normal harpoon is back");
        }

        // ------------------------------------------------------------------ cables

        [Test]
        public void AVolleyIsTwoCablesTwinGapApartCentredOnHim()
        {
            ctx.Muzzle = new Vector2(3f, 1f);
            Assert.IsTrue(Press());
            var up = Active();
            Assert.AreEqual(2, up.Count);
            float gap = up[1].CableRect.center.x - up[0].CableRect.center.x;
            Assert.AreEqual(Tuning.I.twinGap, gap, Tuning.I.Pixel + 0.001f);
            Assert.AreEqual(3f, (up[0].CableRect.center.x + up[1].CableRect.center.x) * 0.5f, Tuning.I.Pixel + 0.001f);
            Assert.IsFalse(up[0].Down);
            Assert.IsFalse(up[1].Down);
            Assert.AreSame(up[0].Volley, up[1].Volley, "both cables share one volley");
            Assert.NotNull(up[0].Volley);
        }

        [Test]
        public void CablesStayInsideTheWalls()
        {
            ctx.Muzzle = new Vector2(Tuning.I.HalfWidth - 0.2f, 1f);
            Assert.IsTrue(Press());
            foreach (var h in Active()) Assert.Less(h.CableRect.center.x, Tuning.I.HalfWidth);
        }

        [Test]
        public void TheNextVolleyWaitsUntilBothCablesAreGone()
        {
            Assert.IsTrue(Press());
            var up = Active();
            Assert.IsFalse(behaviour.VolleyReady(ctx), "both cables are up");
            up[0].Hide();
            Assert.IsFalse(behaviour.VolleyReady(ctx), "one cable is still up");
            Assert.IsFalse(Press());
            Assert.AreEqual(Tuning.I.twinAmmo - 1, run.HeadAmmo, "a blocked press spends nothing");
            up[1].Hide();
            Assert.IsTrue(behaviour.VolleyReady(ctx));
            Assert.IsTrue(Press());
        }

        [Test]
        public void EachCableFliesOnItsOwn()
        {
            Assert.IsTrue(Press());
            var up = Active();
            Tick(5);
            up[0].Hide(); // one pops something
            Tick(5);
            Assert.IsFalse(up[0].Active);
            Assert.IsTrue(up[1].Active, "the other keeps going");
        }

        [Test]
        public void TwinCableAllowsOneMoreVolleyInTheAir()
        {
            behaviour.Dispose();
            var r = new Run(1);
            r.Shots++; // a Twin Cable pick
            Give(r);
            Assert.AreEqual(2, DoubleHarpoon.MaxVolleys(run));
            Assert.IsTrue(Press());
            Assert.IsTrue(Press(), "a second volley while the first is up");
            Assert.AreEqual(4, Active().Count);
            Assert.IsFalse(Press(), "not a third");
        }

        [Test]
        public void FlintFiresThreeCablesWithTheThirdInTheMiddle()
        {
            behaviour.Dispose();
            Give(new Run(1, Character.ById("flint")));
            Assert.AreEqual(2, run.Shots, "Flint starts with two cables");
            Assert.AreEqual(1, DoubleHarpoon.MaxVolleys(run), "his own two cables are folded into the volley");
            ctx.Muzzle = new Vector2(-2f, 1f);
            Assert.IsTrue(Press());
            var up = Active();
            Assert.AreEqual(3, up.Count);
            Assert.AreEqual(-2f, up[1].CableRect.center.x, Tuning.I.Pixel + 0.001f);
            Assert.AreEqual(Tuning.I.twinGap, up[2].CableRect.center.x - up[0].CableRect.center.x, Tuning.I.Pixel + 0.001f);
            Assert.IsFalse(Press(), "one volley at a time");
        }

        [Test]
        public void NewCablesAreAddedWhenTheListIsShort()
        {
            Assert.AreEqual(0, harpoons.Count);
            Assert.IsTrue(Press());
            Assert.AreEqual(2, harpoons.Count);
        }

        [Test]
        public void AHarpoonReusedByANormalShotLeavesTheVolley()
        {
            Assert.IsTrue(Press());
            var up = Active();
            up[1].Hide();
            up[0].Hide();
            up[0].Fire(new Vector2(0f, 1f)); // the normal launcher takes it again
            Assert.IsNull(up[0].Volley);
            Assert.IsTrue(behaviour.VolleyReady(ctx), "a normal shot does not hold up the head");
        }

        // ------------------------------------------------------------------ air and down

        [Test]
        public void BothFireDownWithDownHeldInTheAir()
        {
            ctx.ShootDown = true;
            ctx.InAir = true;
            ctx.Up = Vector2.down;
            ctx.Muzzle = new Vector2(0f, 6f);
            Assert.IsTrue(Press());
            var down = Active();
            Assert.AreEqual(2, down.Count);
            Assert.IsTrue(down[0].Down);
            Assert.IsTrue(down[1].Down);
        }

        [Test]
        public void BothFireUpInTheAir()
        {
            ctx.InAir = true;
            ctx.Muzzle = new Vector2(0f, 6f);
            Assert.IsTrue(Press());
            foreach (var h in Active()) Assert.IsFalse(h.Down);
        }

        // ------------------------------------------------------------------ tiles

        [Test]
        public void EachCableStopsUnderItsOwnTileAndBreaksItsOwnBlock()
        {
            var map = StageMap.Flat();
            var xs = DoubleHarpoon.CableXs(0f, 2);
            int left = StageMap.Col(xs[0]), right = StageMap.Col(xs[1]);
            Assert.AreNotEqual(left, right, "the two cables are in different columns");
            map.Set(left, 4, new Cell { kind = TileKind.Solid });
            map.Set(right, 9, new Cell { kind = TileKind.Breakable, hp = 3 });
            SetMap(map);

            Assert.IsTrue(Press());
            var up = Active();
            Tick(180);
            Assert.IsFalse(up[0].Active);
            Assert.IsFalse(up[1].Active);
            Assert.AreEqual(1, tileHits.Count, "only the breakable block is hit");
            Assert.AreEqual(new Vector2Int(right, 9), tileHits[0]);
        }

        [Test]
        public void ACableInsideABlockIsNotFired()
        {
            var map = StageMap.Flat();
            var xs = DoubleHarpoon.CableXs(0f, 2);
            map.Set(StageMap.Col(xs[0]), StageMap.Row(1f), new Cell { kind = TileKind.Solid });
            SetMap(map);
            Assert.IsTrue(Press());
            var up = Active();
            Assert.AreEqual(1, up.Count, "only the cable in the open fires");
            Assert.AreEqual(xs[1], up[0].CableRect.center.x, Tuning.I.Pixel + 0.001f);
        }

        // ------------------------------------------------------------------ steel, ceramic

        [Test]
        public void EachCableHitsWhatIsOverItOnly()
        {
            var xs = DoubleHarpoon.CableXs(0f, 2);
            var left = Bubble.Create(root.transform, BubbleKind.Gloop, 1, new Vector2(xs[0], 3f), Vector2.zero, false, true);
            var far = Bubble.Create(root.transform, BubbleKind.Gloop, 1, new Vector2(8f, 3f), Vector2.zero, false);
            Assert.IsTrue(Press());
            var up = Active();
            Tick(30);
            Assert.IsTrue(up[0].Hits(left), "the left cable reaches the bubble over it");
            Assert.IsFalse(up[1].Hits(left), "the right cable is 1.6 u away");
            Assert.IsFalse(up[0].Hits(far));
            Assert.IsFalse(up[1].Hits(far));
        }

        // ------------------------------------------------------------------ boss

        [Test]
        public void AVolleyTakesOneBossHealthEvenWhenBothCablesTouch()
        {
            var boss = Boss.Create(root.transform, BossSpec.ForWorld(1), 1, Themes.ForWorld(1));
            for (int i = 0; i < 120 && !boss.Entered; i++) boss.Tick(Dt, Vector2.zero, false);
            Assert.IsTrue(boss.Entered);
            Assert.Less(Tuning.I.twinGap, boss.Body.width, "both cables fit under the body");

            ctx.Muzzle = new Vector2(boss.Body.center.x, 1f);
            Assert.IsTrue(Press());
            var up = Active();
            for (int i = 0; i < 180 && up[0].CableRect.yMax < boss.Body.yMin + 0.1f; i++) Tick(1);
            Assert.IsTrue(up[0].Hits(boss.Body) && up[1].Hits(boss.Body));

            int hp = boss.Hp;
            BossFight.HarpoonHits(boss, harpoons, (amount, at) => boss.Damage(amount));
            Assert.AreEqual(hp - Tuning.I.twinBossDamage, boss.Hp);
            Assert.AreEqual(1, Tuning.I.twinBossDamage);
            Assert.IsFalse(up[0].Active);
            Assert.IsFalse(up[1].Active);
        }

        [Test]
        public void TheSecondCableOfAVolleyDoesNoBossDamageLater()
        {
            var boss = Boss.Create(root.transform, BossSpec.ForWorld(1), 1, Themes.ForWorld(1));
            for (int i = 0; i < 120 && !boss.Entered; i++) boss.Tick(Dt, Vector2.zero, false);
            ctx.Muzzle = new Vector2(boss.Body.center.x, 1f);
            Assert.IsTrue(Press());
            var up = Active();
            for (int i = 0; i < 180 && up[0].CableRect.yMax < boss.Body.yMin + 0.1f; i++) Tick(1);

            int hp = boss.Hp;
            BossFight.HarpoonHits(boss, new List<Harpoon> { up[0] }, (amount, at) => boss.Damage(amount));
            Assert.AreEqual(hp - 1, boss.Hp);
            BossFight.HarpoonHits(boss, new List<Harpoon> { up[1] }, (amount, at) => boss.Damage(amount));
            Assert.AreEqual(hp - 1, boss.Hp, "the volley already took its health");
            Assert.IsFalse(up[1].Active, "the cable is still used up");
        }

        [Test]
        public void ANormalHarpoonStillTakesOneEach()
        {
            var boss = Boss.Create(root.transform, BossSpec.ForWorld(1), 1, Themes.ForWorld(1));
            for (int i = 0; i < 120 && !boss.Entered; i++) boss.Tick(Dt, Vector2.zero, false);
            var a = Harpoon.Create(root.transform);
            var b = Harpoon.Create(root.transform);
            a.Fire(new Vector2(boss.Body.center.x - 0.5f, 1f));
            b.Fire(new Vector2(boss.Body.center.x + 0.5f, 1f));
            for (int i = 0; i < 180 && a.CableRect.yMax < boss.Body.yMin + 0.1f; i++) { a.Tick(Dt); b.Tick(Dt); }
            int hp = boss.Hp;
            BossFight.HarpoonHits(boss, new List<Harpoon> { a, b }, (amount, at) => boss.Damage(amount));
            Assert.AreEqual(hp - 2, boss.Hp);
        }
    }
}
