using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>W11 Vee Harpoon: two slanted cables per volley, open overhead, stopping at the first surface.</summary>
    public class WeaponVeeHarpoonTests
    {
        const float Dt = 1f / 60f;
        const int Small = 3; // tier S

        GameObject root;
        StageMap previousMap;
        Run run;
        WeaponSpec vee;
        WeaponBehaviour behaviour;
        WeaponContext ctx;
        List<Harpoon> harpoons;
        readonly List<Vector2Int> tileHits = new List<Vector2Int>();

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("WeaponVeeHarpoonTests");
            previousMap = Game.Map;
            SetMap(StageMap.Flat());
            tileHits.Clear();
            harpoons = new List<Harpoon>();
            vee = WeaponSpec.ById("vee");
            Assert.NotNull(vee, "the Vee Harpoon is in WeaponSpec.All");
            run = new Run(1);
            Assert.IsTrue(run.GiveWeapon(vee));
            behaviour = vee.CreateBehaviour();
            ctx = new WeaponContext
            {
                CanFire = true,
                Muzzle = new Vector2(0f, 1f),
                Up = Vector2.up,
                Spec = vee,
                Run = run,
            };
            var f = typeof(WeaponContext).GetField("harpoons", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(f, "WeaponContext.harpoons must exist.");
            f.SetValue(ctx, (System.Func<int, IReadOnlyList<Harpoon>>)(n =>
            {
                while (harpoons.Count < n) harpoons.Add(Harpoon.Create(root.transform, (c, row) =>
                {
                    tileHits.Add(new Vector2Int(c, row));
                    Game.Map.Damage(c, row);
                }));
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

        /// <summary>A fire press the way Game handles it: ask the head, fire its volley, spend one.</summary>
        bool Press()
        {
            if (!behaviour.VolleyReady(ctx)) return false;
            if (!behaviour.FireVolley(ctx)) return false;
            run.SpendAmmo(WeaponSlot.Harpoon);
            return true;
        }

        /// <summary>The flying cables, left one first.</summary>
        List<Harpoon> Active()
        {
            var list = new List<Harpoon>();
            foreach (var h in harpoons) if (h.Active) list.Add(h);
            list.Sort((a, b) => a.AngledTip.x.CompareTo(b.AngledTip.x));
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

        /// <summary>x of a cable fired from the default muzzle at height y (side -1 left, +1 right).</summary>
        static float PathX(int side, float y, float fromY = 1f)
        {
            float x0 = side * VeeHarpoon.HandPixels * Tuning.I.Pixel;
            return x0 + side * Mathf.Abs(y - fromY) * Mathf.Tan(Tuning.I.veeAngle * Mathf.Deg2Rad);
        }

        // ------------------------------------------------------------------ spec and ammo

        [Test]
        public void IsACommonHarpoonHeadWithAmmoFromTuning()
        {
            Assert.AreEqual(WeaponSlot.Harpoon, vee.Slot);
            Assert.AreEqual(WeaponTier.Common, vee.Tier);
            Assert.AreEqual(Tuning.I.veeAmmo, vee.MaxAmmo);
            Assert.AreEqual(12, Tuning.I.veeAmmo, "the sheet's starting value");
            Assert.AreEqual(30f, Tuning.I.veeAngle, "the sheet's starting value");
            Assert.AreSame(vee, run.Head, "it goes in the harpoon slot");
            Assert.IsNull(run.Weapon, "the weapon slot stays free for a gun");
            Assert.AreEqual(11, vee.IconRows[0].Length, "icons are 11 px wide");
            Assert.IsNull(vee.HeldRows, "he keeps the launcher");
        }

        [Test]
        public void EachVolleySpendsOneAndTheHeadIsGoneAtZero()
        {
            int full = Tuning.I.veeAmmo;
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
        public void AVolleyIsTwoCablesLeaningOutAtVeeAngle()
        {
            Assert.IsTrue(Press());
            var up = Active();
            Assert.AreEqual(2, up.Count);
            Tick(10);
            float want = Tuning.I.veeAngle;
            for (int i = 0; i < 2; i++)
            {
                var h = up[i];
                Assert.IsTrue(h.Angled);
                Assert.IsFalse(h.Down);
                Vector2 d = h.AngledTip - h.AngledBase;
                Assert.Greater(d.y, 0f, "both go up");
                Assert.AreEqual(want, Vector2.Angle(Vector2.up, d), 0.5f);
                Assert.AreEqual(i == 0 ? -1f : 1f, Mathf.Sign(d.x), "one leans left, one right");
                Assert.AreEqual(i == 0 ? -1f : 1f, Mathf.Sign(h.AngledBase.x), "each leaves from its own hand");
            }
        }

        [Test]
        public void ABubbleStraightAboveIsMissedAndBubblesToTheSidesAreHit()
        {
            var above = Bubble.Create(root.transform, BubbleKind.Gloop, Small, new Vector2(0f, 5f), Vector2.zero, false);
            var left = Bubble.Create(root.transform, BubbleKind.Gloop, Small, new Vector2(PathX(-1, 5f), 5f), Vector2.zero, false);
            var right = Bubble.Create(root.transform, BubbleKind.Gloop, Small, new Vector2(PathX(1, 5f), 5f), Vector2.zero, false);
            Assert.IsTrue(Press());
            var up = Active();
            Tick(30);
            Assert.IsTrue(up[0].Hits(left), "the left cable reaches the bubble up to the left");
            Assert.IsTrue(up[1].Hits(right), "the right cable reaches the bubble up to the right");
            Assert.IsFalse(up[0].Hits(above), "open straight overhead");
            Assert.IsFalse(up[1].Hits(above), "open straight overhead");
            Assert.IsFalse(up[0].Hits(right));
            Assert.IsFalse(up[1].Hits(left));
        }

        [Test]
        public void TheTipHitsBeforeTheCableReachesPastIt()
        {
            var b = Bubble.Create(root.transform, BubbleKind.Gloop, Small, new Vector2(PathX(1, 6f), 6f), Vector2.zero, false);
            Assert.IsTrue(Press());
            var right = Active()[1];
            Assert.IsFalse(right.Hits(b), "not yet: the tip is still at his hand");
            bool hit = false;
            for (int i = 0; i < 60 && !hit; i++)
            {
                Tick(1);
                hit = right.Hits(b);
            }
            Assert.IsTrue(hit);
            Assert.Less(right.AngledTip.y, 6f + b.Radius + 0.5f, "touched as the tip arrived, not later");
        }

        [Test]
        public void TheNextVolleyWaitsUntilBothCablesAreGone()
        {
            Assert.IsTrue(Press());
            var up = Active();
            Assert.IsFalse(behaviour.VolleyReady(ctx), "both still flying");
            up[0].Hide();
            Assert.IsFalse(behaviour.VolleyReady(ctx), "one still flying");
            up[1].Hide();
            Assert.IsTrue(behaviour.VolleyReady(ctx));
            Assert.IsTrue(Press());
        }

        [Test]
        public void AReusedHarpoonFiresStraightAgain()
        {
            Assert.IsTrue(Press());
            var h = Active()[0];
            h.Hide();
            h.Fire(new Vector2(0f, 1f));
            Assert.IsFalse(h.Angled, "the normal launcher fires straight up");
            Assert.IsNull(h.Volley);
            Tick(5);
            Assert.AreEqual(0f, h.CableRect.center.x, Tuning.I.Pixel + 0.001f);
        }

        // ------------------------------------------------------------------ walls, ceiling, tiles

        [Test]
        public void TipsStopAtTheCeilingAndWallsAndNeverPassThem()
        {
            ctx.Muzzle = new Vector2(12f, 1f); // near the right wall: the right cable meets the wall first
            Assert.IsTrue(Press());
            var up = Active();
            for (int i = 0; i < 240; i++)
            {
                Tick(1);
                foreach (var h in up)
                {
                    if (!h.Active) continue;
                    Assert.LessOrEqual(Mathf.Abs(h.AngledTip.x), Tuning.I.HalfWidth);
                    Assert.LessOrEqual(h.AngledTip.y, Tuning.I.arenaHeight);
                }
            }
            Assert.IsFalse(up[0].Active, "the left cable stopped at the ceiling and went");
            Assert.IsFalse(up[1].Active, "the right cable stopped at the wall and went");
            Assert.AreEqual(0, tileHits.Count);
        }

        [Test]
        public void EachTipStopsAtTheFirstTileAndBreaksABreakableOneOnce()
        {
            var map = StageMap.Flat();
            int leftCol = StageMap.Col(PathX(-1, 4f)), rightCol = StageMap.Col(PathX(1, 4f));
            map.Set(leftCol, 4, new Cell { kind = TileKind.Solid });
            map.Set(rightCol, 4, new Cell { kind = TileKind.Breakable, hp = 3 });
            SetMap(map);

            Assert.IsTrue(Press());
            var up = Active();
            float topReached = 0f;
            for (int i = 0; i < 120; i++)
            {
                Tick(1);
                foreach (var h in up) if (h.Active) topReached = Mathf.Max(topReached, h.AngledTip.y);
            }
            Assert.IsFalse(up[0].Active);
            Assert.IsFalse(up[1].Active);
            Assert.LessOrEqual(topReached, 4f + 0.01f, "neither tip went into its tile");
            Assert.AreEqual(1, tileHits.Count, "only the breakable block is hit, once");
            Assert.AreEqual(new Vector2Int(rightCol, 4), tileHits[0]);
        }

        [Test]
        public void AnchorTipHooksTheCableForItsHoldTime()
        {
            Assert.IsTrue(Press());
            var up = Active();
            foreach (var h in up) h.HoldTime = 0.5f;
            // From y = 1 to the ceiling at veeAngle is about 15 u of cable; wait a quarter second past that.
            float reach = (Tuning.I.arenaHeight - 1f) / Mathf.Cos(Tuning.I.veeAngle * Mathf.Deg2Rad) / Tuning.I.harpoonSpeed;
            Tick(Mathf.CeilToInt((reach + 0.25f) / Dt));
            Assert.IsTrue(up[0].Active && up[1].Active, "hooked");
            Tick(30);
            Assert.IsFalse(up[0].Active || up[1].Active, "let go after the hold time");
        }

        [Test]
        public void ACableWhoseHandIsInsideABlockIsNotFired()
        {
            var map = StageMap.Flat();
            map.Set(StageMap.Col(PathX(-1, 1f)), StageMap.Row(1f), new Cell { kind = TileKind.Solid });
            SetMap(map);
            Assert.IsTrue(Press());
            var up = Active();
            Assert.AreEqual(1, up.Count, "only the cable in the open fires");
            Assert.Greater(up[0].AngledBase.x, 0f);
        }

        // ------------------------------------------------------------------ air and down

        [Test]
        public void DownInTheAirSweepsTheFloorOnBothSides()
        {
            ctx.ShootDown = true;
            ctx.InAir = true;
            ctx.Up = Vector2.down;
            ctx.Muzzle = new Vector2(0f, 6f);
            float landX = PathX(1, 0f, 6f);
            var leftRoller = Roller.Create(root.transform, 1, -landX, 0f, 1);
            var rightRoller = Roller.Create(root.transform, 1, landX, 0f, -1);
            var under = Roller.Create(root.transform, 1, 0f, 0f, 1);

            Assert.IsTrue(Press());
            var down = Active();
            Assert.AreEqual(2, down.Count);
            bool hitLeft = false, hitRight = false;
            for (int i = 0; i < 60; i++)
            {
                Tick(1);
                foreach (var h in down)
                {
                    if (!h.Active) continue;
                    Assert.IsTrue(h.Down);
                    Assert.GreaterOrEqual(h.AngledTip.y, -1e-4f, "never through the floor");
                    hitLeft |= h.Hits(leftRoller);
                    hitRight |= h.Hits(rightRoller);
                    Assert.IsFalse(h.Hits(under), "open straight below him");
                }
            }
            Assert.IsTrue(hitLeft && hitRight, "rolling balloons on both sides are swept");
            Assert.Less(down[0].AngledTip.x, 0f);
            Assert.Greater(down[1].AngledTip.x, 0f);
        }

        // ------------------------------------------------------------------ boss

        [Test]
        public void AVolleyTakesOneBossHealthEvenWhenBothCablesTouch()
        {
            var boss = Boss.Create(root.transform, BossSpec.ForWorld(1), 1, Themes.ForWorld(1));
            for (int i = 0; i < 120 && !boss.Entered; i++) boss.Tick(Dt, Vector2.zero, false);
            Assert.IsTrue(boss.Entered);

            ctx.Muzzle = new Vector2(boss.Body.center.x, boss.Body.yMin - 0.5f); // fired from just under the body
            Assert.IsTrue(Press());
            var up = Active();
            for (int i = 0; i < 60 && !(up[0].Hits(boss.Body) && up[1].Hits(boss.Body)); i++) Tick(1);
            Assert.IsTrue(up[0].Hits(boss.Body) && up[1].Hits(boss.Body));

            int hp = boss.Hp;
            BossFight.HarpoonHits(boss, harpoons, (amount, at) => boss.Damage(amount));
            Assert.AreEqual(hp - Tuning.I.veeBossDamage, boss.Hp);
            Assert.AreEqual(1, Tuning.I.veeBossDamage);
            Assert.IsFalse(up[0].Active);
            Assert.IsFalse(up[1].Active);
        }
    }
}
