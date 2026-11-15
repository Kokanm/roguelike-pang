using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>W12 Skewer Harpoon: one harpoon per shot that stays up for up to three pops.</summary>
    public class WeaponSkewerHarpoonTests
    {
        const float Dt = 1f / 60f;
        const int Small = 3; // tier S

        GameObject root;
        StageMap previousMap;
        Run run;
        WeaponSpec skewer;
        WeaponBehaviour behaviour;
        WeaponContext ctx;
        List<Harpoon> harpoons;
        List<Bubble> bubbles;
        List<Roller> rollers;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("WeaponSkewerHarpoonTests");
            previousMap = Game.Map;
            SetMap(StageMap.Flat());
            harpoons = new List<Harpoon>();
            bubbles = new List<Bubble>();
            rollers = new List<Roller>();
            skewer = WeaponSpec.ById("skewer");
            Assert.NotNull(skewer, "the Skewer Harpoon is in WeaponSpec.All");
            run = new Run(1);
            Assert.IsTrue(run.GiveWeapon(skewer));
            behaviour = skewer.CreateBehaviour();
            ctx = new WeaponContext
            {
                CanFire = true,
                Muzzle = new Vector2(0f, 1f),
                Up = Vector2.up,
                Spec = skewer,
                Run = run,
                World = new WeaponWorld(bubbles, rollers, new List<Bullet>(), () => null, root.transform),
            };
            var f = typeof(WeaponContext).GetField("harpoons", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(f, "WeaponContext.harpoons must exist.");
            f.SetValue(ctx, (System.Func<int, IReadOnlyList<Harpoon>>)(n =>
            {
                while (harpoons.Count < Mathf.Max(1, n)) harpoons.Add(Harpoon.Create(root.transform, (c, row) => Game.Map.Damage(c, row)));
                return harpoons;
            }));
        }

        [TearDown]
        public void TearDown()
        {
            behaviour?.Dispose();
            SetMap(previousMap);
            Object.DestroyImmediate(root);
        }

        static void SetMap(StageMap map)
        {
            var property = typeof(Game).GetProperty("Map", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(property, "Game.Map must exist.");
            property.SetValue(null, map);
        }

        /// <summary>A fire press the way Game handles it: ask the head, fire its shot, spend one.</summary>
        bool Press()
        {
            ctx.HarpoonFree = true;
            foreach (var h in harpoons) if (h.Active) ctx.HarpoonFree = false;
            if (!behaviour.VolleyReady(ctx)) return false;
            if (!behaviour.FireVolley(ctx)) return false;
            run.SpendAmmo(WeaponSlot.Harpoon);
            return true;
        }

        Harpoon Shot()
        {
            foreach (var h in harpoons) if (h.Active) return h;
            return null;
        }

        void Tick(int frames)
        {
            for (int i = 0; i < frames; i++) foreach (var h in harpoons) h.Tick(Dt);
        }

        Bubble AddBubble(float x, float y, bool ceramic = false)
        {
            var b = Bubble.Create(root.transform, BubbleKind.Gloop, Small, new Vector2(x, y), Vector2.zero, false, ceramic);
            bubbles.Add(b);
            return b;
        }

        /// <summary>The game's bubble hit loop for one harpoon and one bubble (Game.TickPlay): true if it hit.</summary>
        static bool Touch(Harpoon h, Bubble b)
        {
            if (!h.Active || !b.CanBeHit || h.Ignores(b) || !h.Hits(b)) return false;
            h.Spend(b);
            if (!b.AbsorbHit()) b.Swell(1f);
            return true;
        }

        /// <summary>The game's rolling balloon hit loop for one harpoon and one roller (Game.HarpoonHitsRollers).</summary>
        static bool Touch(Harpoon h, Roller r)
        {
            if (!h.Active || r.Popped || h.Ignores(r) || !h.Hits(r)) return false;
            h.Spend(r, r.Steel);
            if (!r.AbsorbHit()) r.Swell(1f);
            return true;
        }

        // ------------------------------------------------------------------ spec and ammo

        [Test]
        public void IsAnUncommonHarpoonHeadWithNumbersFromTuning()
        {
            Assert.AreEqual(WeaponSlot.Harpoon, skewer.Slot);
            Assert.AreEqual(WeaponTier.Uncommon, skewer.Tier);
            Assert.AreEqual(Tuning.I.skewerAmmo, skewer.MaxAmmo);
            Assert.AreEqual(10, Tuning.I.skewerAmmo, "the sheet's starting value");
            Assert.AreEqual(3, Tuning.I.skewerPops, "the sheet's starting value");
            Assert.AreEqual(2, Tuning.I.skewerBossDamage, "the sheet's starting value");
            Assert.AreEqual(0.25f, Tuning.I.skewerImmunity, 1e-5f, "the sheet's starting value");
            Assert.AreSame(skewer, run.Head, "it goes in the harpoon slot");
            Assert.IsNull(run.Weapon, "the weapon slot stays free for a gun");
            Assert.AreEqual(11, skewer.IconRows[0].Length, "icons are 11 px wide");
            Assert.NotNull(skewer.HeldRows, "its own spiked launcher in his hand");
            Assert.NotNull(WeaponArt.Held(skewer));
        }

        [Test]
        public void EachShotSpendsOneAndTheHeadIsGoneAtZero()
        {
            int full = Tuning.I.skewerAmmo;
            for (int i = 0; i < full; i++)
            {
                Assert.AreEqual(full - i, run.HeadAmmo);
                Assert.IsTrue(Press(), $"shot {i + 1} fires");
                Shot().Hide();
            }
            Assert.AreEqual(0, run.HeadAmmo);
            Assert.IsNull(run.Head, "after the last shot the normal harpoon is back");
        }

        [Test]
        public void AShotIsOneHarpoonWithSkewerPopsAndWaitsForTheLastOne()
        {
            Assert.IsTrue(Press());
            var h = Shot();
            Assert.NotNull(h.Pierce);
            Assert.AreEqual(Tuning.I.skewerPops, h.Pierce.PopsLeft);
            Assert.IsFalse(h.Down);
            int active = 0;
            foreach (var x in harpoons) if (x.Active) active++;
            Assert.AreEqual(1, active);
            Assert.IsFalse(Press(), "like the plain harpoon: one at a time");
            Assert.AreEqual(Tuning.I.skewerAmmo - 1, run.HeadAmmo);
        }

        [Test]
        public void ANormalShotFromTheSameHarpoonIsNotASkewer()
        {
            Assert.IsTrue(Press());
            var h = Shot();
            h.Hide();
            h.Fire(new Vector2(0f, 1f));
            Assert.IsNull(h.Pierce, "every Fire clears it");
            var b = AddBubble(0f, 4f);
            Tick(30);
            Assert.IsTrue(Touch(h, b));
            Assert.IsFalse(h.Active, "a normal harpoon is used up by one pop");
        }

        // ------------------------------------------------------------------ pops

        [Test]
        public void ItPopsThreeThingsAndThenGoes()
        {
            var a = AddBubble(0f, 3f);
            var b = AddBubble(0f, 6f);
            var c = AddBubble(0f, 9f);
            var d = AddBubble(0f, 12f);
            Assert.IsTrue(Press());
            var h = Shot();
            var order = new List<Bubble> { a, b, c, d };
            var hit = new List<Bubble>();
            for (int i = 0; i < 120 && h.Active; i++)
            {
                Tick(1);
                foreach (var x in order)
                    if (Touch(h, x)) { hit.Add(x); break; } // the game hits one thing per harpoon per frame
            }
            CollectionAssert.AreEqual(new[] { a, b, c }, hit, "the lowest three, in the order the tip reached them");
            Assert.IsFalse(h.Active, "gone after its third pop");
            Assert.IsTrue(d.CanBeHit, "the fourth is left for the next shot");
        }

        [Test]
        public void ItStillGoesAtTheCeilingWithPopsLeft()
        {
            Assert.IsTrue(Press());
            var h = Shot();
            for (int i = 0; i < 240 && h.Active; i++) Tick(1);
            Assert.IsFalse(h.Active, "the normal rules still end it");
        }

        [Test]
        public void ItShootsDownInTheAir()
        {
            ctx.ShootDown = true;
            ctx.Muzzle = new Vector2(0f, 8f);
            ctx.Up = Vector2.down;
            var b = AddBubble(0f, 4f);
            Assert.IsTrue(Press());
            var h = Shot();
            Assert.IsTrue(h.Down);
            Assert.NotNull(h.Pierce);
            bool hit = false;
            for (int i = 0; i < 60 && !hit; i++)
            {
                Tick(1);
                hit = Touch(h, b);
            }
            Assert.IsTrue(hit);
            Assert.IsTrue(h.Active, "still up after one pop");
        }

        // ------------------------------------------------------------------ immunity

        [Test]
        public void BubblesBornAfterTheShotAreLeftAloneForAQuarterSecond()
        {
            var parent = AddBubble(0f, 3f);
            Assert.IsTrue(Press());
            var h = Shot();
            Tick(1);
            var old = AddBubble(0f, 4f); // stands for a child born from the pop: it was not there when the shot left
            Tick(1);
            Assert.IsTrue(h.Ignores(old), "a fresh half can fly clear");
            Assert.IsFalse(h.Ignores(parent), "what was there before the shot is fair game");
            Tick(Mathf.CeilToInt(Tuning.I.skewerImmunity / Dt) + 1);
            Assert.IsFalse(h.Ignores(old), "after skewerImmunity it can be skewered too");
        }

        [Test]
        public void AFreshHalfFallingBackOntoTheCableIsPopped()
        {
            Assert.IsTrue(Press());
            var h = Shot();
            for (int i = 0; i < 12; i++) Tick(1); // the cable is up past y = 3
            var half = AddBubble(0f, 2.5f);
            Tick(1);
            Assert.IsFalse(Touch(h, half), "not on the frame it is born");
            int frames = 0;
            while (!Touch(h, half) && frames++ < 60 && h.Active) Tick(1);
            Assert.IsFalse(half.CanBeHit, "popped once the immunity ran out");
            Assert.GreaterOrEqual(frames * Dt, Tuning.I.skewerImmunity - 2f * Dt);
        }

        [Test]
        public void ACeramicBubbleCracksAndThenBreaksOnTheSameSkewer()
        {
            var b = AddBubble(0f, 4f, ceramic: true);
            Assert.AreEqual(2, b.Armor);
            Assert.IsTrue(Press());
            var h = Shot();
            int hits = 0;
            for (int i = 0; i < 120 && h.Active && b.CanBeHit; i++)
            {
                Tick(1);
                if (Touch(h, b))
                {
                    hits++;
                    if (hits == 1)
                    {
                        Assert.IsTrue(b.CanBeHit, "the first touch only cracks it");
                        Assert.IsTrue(h.Ignores(b), "and the skewer leaves it for a moment");
                        Assert.IsFalse(Touch(h, b), "not twice in the same frame");
                    }
                }
            }
            Assert.AreEqual(2, hits, "each touch is one of its pops");
            Assert.IsFalse(b.CanBeHit, "one skewer broke it by itself");
            Assert.AreEqual(Tuning.I.skewerPops - 2, h.Pierce.PopsLeft);
        }

        [Test]
        public void ItKeepsCountingAfterTheLastShotEmptiedTheSlot()
        {
            run.SpendAmmo(WeaponSlot.Harpoon, Tuning.I.skewerAmmo - 1);
            Assert.IsTrue(Press());
            Assert.IsNull(run.Head, "the last shot emptied the slot");
            behaviour.Dispose();
            var h = Shot();
            float before = h.Pierce.Clock;
            Tick(5);
            Assert.Greater(h.Pierce.Clock, before, "the harpoon ticks its own skewer");
        }

        // ------------------------------------------------------------------ steel, rollers, boss

        [Test]
        public void SteelEndsTheShotAtOnce()
        {
            var r = Roller.Create(root.transform, 0, 0f, 0f, 1, steel: true);
            rollers.Add(r);
            Assert.IsTrue(Press());
            var h = Shot();
            bool hit = false;
            for (int i = 0; i < 60 && !hit; i++)
            {
                Tick(1);
                hit = Touch(h, r);
            }
            Assert.IsTrue(hit);
            Assert.IsFalse(h.Active, "steel ends it with pops left");
        }

        [Test]
        public void ARollingBalloonIsOneOfItsPops()
        {
            var r = Roller.Create(root.transform, 1, 0f, 0f, 1);
            rollers.Add(r);
            Assert.IsTrue(Press());
            var h = Shot();
            bool hit = false;
            for (int i = 0; i < 60 && !hit; i++)
            {
                Tick(1);
                hit = Touch(h, r);
            }
            Assert.IsTrue(hit);
            Assert.IsTrue(h.Active, "still up");
            Assert.AreEqual(Tuning.I.skewerPops - 1, h.Pierce.PopsLeft);
        }

        [Test]
        public void ABossTakesSkewerBossDamageAndTheShotEnds()
        {
            var boss = Boss.Create(root.transform, BossSpec.ForWorld(1), 1, Themes.ForWorld(1));
            for (int i = 0; i < 120 && !boss.Entered; i++) boss.Tick(Dt, Vector2.zero, false);
            Assert.IsTrue(boss.Entered);
            ctx.Muzzle = new Vector2(boss.Body.center.x, 1f);
            Assert.IsTrue(Press());
            var h = Shot();
            for (int i = 0; i < 180 && h.Active && !h.Hits(boss.Body); i++) Tick(1);
            Assert.IsTrue(h.Hits(boss.Body));

            int hp = boss.Hp;
            BossFight.HarpoonHits(boss, harpoons, (amount, at) => boss.Damage(amount));
            Assert.AreEqual(hp - Tuning.I.skewerBossDamage, boss.Hp);
            Assert.IsFalse(h.Active);
        }
    }
}
