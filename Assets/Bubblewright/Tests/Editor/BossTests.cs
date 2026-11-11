using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>Boss framework: health, movement, aimed bullets, harpoon damage and the clear rule.</summary>
    public class BossTests
    {
        const float Dt = 1f / 60f;

        GameObject root;
        StageMap previousMap;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("BossTests");
            previousMap = Game.Map;
            SetMap(StageLayout.Parse(StageLayouts.All[0].rows)); // the flat boss stage
        }

        [TearDown]
        public void TearDown()
        {
            SetMap(previousMap);
            Object.DestroyImmediate(root);
        }

        static void SetMap(StageMap map)
        {
            var property = typeof(Game).GetProperty("Map", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(property, "Game.Map must exist.");
            property.SetValue(null, map);
        }

        Boss MakeBoss(int world = 1)
        {
            var boss = Boss.Create(root.transform, BossSpec.ForWorld(world), world, Themes.ForWorld(world));
            return boss;
        }

        /// <summary>Ticks the boss until it has finished sliding in, with Wick standing where target is.</summary>
        static void SlideIn(Boss boss, Vector2 target)
        {
            for (int i = 0; i < 120 && !boss.Entered; i++) boss.Tick(Dt, target, false);
            Assert.IsTrue(boss.Entered);
        }

        static Vector2 WickCentreAt(float x)
        {
            var t = Tuning.I;
            return new Rect(x - t.hitbox.x * 0.5f, 0f, t.hitbox.x, t.hitbox.y).center;
        }

        [Test]
        public void BossHealthIs12_16_20_24_28_32()
        {
            var expected = new[] { 12, 16, 20, 24, 28, 32 };
            for (int world = 1; world <= 6; world++)
            {
                Assert.AreEqual(expected[world - 1], BossSpec.HpForWorld(world), $"world {world}");
                var boss = MakeBoss(world);
                Assert.AreEqual(expected[world - 1], boss.Hp, $"world {world} boss");
                Assert.AreEqual(expected[world - 1], boss.MaxHp, $"world {world} max");
            }
        }

        [Test]
        public void PlaceholderSpecIsTheSameInEveryWorld()
        {
            for (int world = 1; world <= 6; world++)
            {
                var spec = BossSpec.ForWorld(world);
                Assert.AreEqual("WARDEN", spec.name);
                Assert.AreEqual(new Vector2(4f, 3f), spec.size);
                Assert.AreEqual(3f, spec.speed, 1e-5f);
                Assert.AreEqual(2f, spec.aimedEvery, 1e-5f);
            }
        }

        [Test]
        public void BossSlidesInFromAboveTheCeilingAndDoesNotMoveSidewaysOrShootYet()
        {
            var boss = MakeBoss();
            int shots = 0;
            boss.OnShoot = (from, dir) => shots++;
            Assert.Greater(boss.Body.yMin, Tuning.I.arenaHeight, "starts fully above the ceiling");
            for (int i = 0; i < 120 && !boss.Entered; i++)
            {
                boss.Tick(Dt, WickCentreAt(0f), false);
                Assert.AreEqual(0f, boss.Centre.x, 1e-5f);
            }
            Assert.IsTrue(boss.Entered);
            Assert.AreEqual(Tuning.I.bossY, boss.Centre.y, 0.01f);
            for (int i = 0; i < 600; i++) boss.Tick(Dt, WickCentreAt(0f), false);
            Assert.AreEqual(0, shots, "no shots before play starts");
            Assert.AreEqual(0f, boss.Centre.x, 1e-5f, "no sideways move before play starts");
        }

        [Test]
        public void BossStaysInTheArenaAndTurnsAtBothWalls()
        {
            var boss = MakeBoss();
            float minX = float.MaxValue, maxX = float.MinValue;
            int turnsAtLeftWall = 0, turnsAtRightWall = 0, lastDir = boss.Direction;
            for (int i = 0; i < 60 * 60; i++)
            {
                boss.Tick(Dt, WickCentreAt(0f), true);
                var body = boss.Body;
                minX = Mathf.Min(minX, body.xMin);
                maxX = Mathf.Max(maxX, body.xMax);
                Assert.GreaterOrEqual(body.xMin, -Tuning.I.HalfWidth - 1e-4f, $"tick {i}");
                Assert.LessOrEqual(body.xMax, Tuning.I.HalfWidth + 1e-4f, $"tick {i}");
                if (boss.Entered) Assert.LessOrEqual(body.yMax, Tuning.I.arenaHeight, $"tick {i}: stays under the ceiling once in");
                if (boss.Direction != lastDir)
                {
                    if (boss.Direction > 0) turnsAtLeftWall++; else turnsAtRightWall++;
                    lastDir = boss.Direction;
                }
            }
            Assert.AreEqual(-Tuning.I.HalfWidth, minX, 0.01f, "reaches the left wall");
            Assert.AreEqual(Tuning.I.HalfWidth, maxX, 0.01f, "reaches the right wall");
            Assert.GreaterOrEqual(turnsAtLeftWall, 1);
            Assert.GreaterOrEqual(turnsAtRightWall, 1);
        }

        [Test]
        public void StormMakesTheBossWalkFaster()
        {
            var calm = MakeBoss();
            var storm = MakeBoss();
            SlideIn(calm, Vector2.zero);
            SlideIn(storm, Vector2.zero);
            for (int i = 0; i < 60; i++)
            {
                calm.Tick(Dt, Vector2.zero, true);
                storm.Tick(Dt, Vector2.zero, true, Tuning.I.bossStormScale);
            }
            Assert.AreEqual(Mathf.Abs(calm.Centre.x) * Tuning.I.bossStormScale, Mathf.Abs(storm.Centre.x), 0.05f);
        }

        [Test]
        public void AimedBulletTravelsTowardWickAndPassesThroughHim()
        {
            var boss = MakeBoss();
            var target = WickCentreAt(-10f);
            Vector2 from = default, dir = default;
            int shots = 0;
            boss.OnShoot = (f, d) => { if (shots++ == 0) { from = f; dir = d; } };
            SlideIn(boss, target);
            for (int i = 0; i < 60 * 5 && shots == 0; i++) boss.Tick(Dt, target, true);
            Assert.AreEqual(1, shots, "one bullet per shot");
            Assert.AreEqual(1f, dir.magnitude, 1e-4f);
            Assert.AreEqual(boss.Body.yMin, from.y, 0.1f, "leaves from the bottom of the body");

            var bullet = Bullet.Create(root.transform, from, dir * Tuning.I.bulletSpeed, Themes.ForWorld(1));
            float closest = float.MaxValue;
            // Four seconds at 240 Hz. Speed 7 needs a bit over 2.6 s to reach Wick from the ceiling; 60*4 steps of 1/240 is only one second.
            for (int i = 0; i < 240 * 4; i++)
            {
                bullet.Tick(1f / 240f);
                closest = Mathf.Min(closest, Vector2.Distance(bullet.Position, target));
            }
            Assert.LessOrEqual(closest, 0.3f, "passes within 0.3 units of the centre of Wick's hitbox");
        }

        [Test]
        public void ShotsComeEveryAimedEverySecondsAfterSlidingIn()
        {
            var boss = MakeBoss();
            var times = new List<float>();
            float clock = 0f;
            boss.OnShoot = (f, d) => times.Add(clock);
            SlideIn(boss, WickCentreAt(0f));
            for (int i = 0; i < 60 * 7; i++)
            {
                clock += Dt;
                boss.Tick(Dt, WickCentreAt(0f), true);
            }
            Assert.AreEqual(3, times.Count);
            Assert.AreEqual(2f, times[1] - times[0], 0.05f);
        }

        [Test]
        public void TheLowerBandBlinksCreamBeforeEachShot()
        {
            var boss = MakeBoss();
            SlideIn(boss, WickCentreAt(0f));
            int shots = 0;
            boss.OnShoot = (f, d) => shots++;
            int blinkFrames = 0;
            float clock = 0f;
            while (shots == 0 && clock < 4f)
            {
                clock += Dt;
                boss.Tick(Dt, WickCentreAt(0f), true);
                if (boss.TellOn) { blinkFrames++; Assert.Greater(clock, 1.4f, "only in the last half second"); }
            }
            Assert.AreEqual(1, shots);
            Assert.Greater(blinkFrames, 0);
        }

        [Test]
        public void SeveralBulletsFanOutEvenlyAcrossTheSpread()
        {
            var dirs = Boss.Directions(Vector2.zero, new Vector2(0f, -10f), 3, 40f);
            Assert.AreEqual(3, dirs.Length);
            Assert.AreEqual(-20f, Vector2.SignedAngle(Vector2.down, dirs[0]), 0.01f);
            Assert.AreEqual(0f, Vector2.SignedAngle(Vector2.down, dirs[1]), 0.01f);
            Assert.AreEqual(20f, Vector2.SignedAngle(Vector2.down, dirs[2]), 0.01f);
        }

        [Test]
        public void BulletsLeaveTheArenaOrTouchTheFloor()
        {
            var theme = Themes.ForWorld(1);
            var falling = Bullet.Create(root.transform, new Vector2(0f, 1f), Vector2.down * 7f, theme);
            Assert.IsFalse(falling.Out);
            for (int i = 0; i < 60; i++) falling.Tick(Dt);
            Assert.IsTrue(falling.Out, "reached the floor");
            var sideways = Bullet.Create(root.transform, new Vector2(15f, 5f), Vector2.right * 7f, theme);
            for (int i = 0; i < 60; i++) sideways.Tick(Dt);
            Assert.IsTrue(sideways.Out, "left through the wall");
        }

        static Harpoon FireAt(Transform parent, float x)
        {
            var h = Harpoon.Create(parent);
            h.Fire(new Vector2(x, 1f));
            return h;
        }

        static void RiseUntilTouching(Harpoon h, Boss boss)
        {
            for (int i = 0; i < 120 && h.CableRect.yMax < boss.Body.yMin + 0.1f; i++) h.Tick(Dt);
        }

        [Test]
        public void HarpoonThroughTheBodyDamagesByOneAndIsHidden()
        {
            var boss = MakeBoss();
            SlideIn(boss, Vector2.zero);
            var h = FireAt(root.transform, 0f);
            RiseUntilTouching(h, boss);
            Assert.IsTrue(h.Active);
            int hp = boss.Hp, calls = 0;
            int hits = BossFight.HarpoonHits(boss, new List<Harpoon> { h }, (amount, at) => { calls++; boss.Damage(amount); });
            Assert.AreEqual(1, hits);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(hp - 1, boss.Hp);
            Assert.IsFalse(h.Active, "the harpoon is hidden afterwards");
            Assert.AreEqual(0, BossFight.HarpoonHits(boss, new List<Harpoon> { h }, (amount, at) => boss.Damage(amount)), "a hidden harpoon hits nothing");
        }

        [Test]
        public void HarpoonsThatMissDoNothing()
        {
            var boss = MakeBoss();
            SlideIn(boss, Vector2.zero);
            var h = FireAt(root.transform, 8f); // well to the side of the 4 unit wide body at x = 0
            for (int i = 0; i < 60; i++) h.Tick(Dt);
            Assert.AreEqual(0, BossFight.HarpoonHits(boss, new List<Harpoon> { h }, (amount, at) => boss.Damage(amount)));
            Assert.AreEqual(boss.MaxHp, boss.Hp);
        }

        [Test]
        public void TwoHarpoonsInTheSameTickDoTwoDamage()
        {
            var boss = MakeBoss();
            SlideIn(boss, Vector2.zero);
            var a = FireAt(root.transform, -0.5f);
            var b = FireAt(root.transform, 0.5f);
            RiseUntilTouching(a, boss);
            RiseUntilTouching(b, boss);
            int hp = boss.Hp;
            BossFight.HarpoonHits(boss, new List<Harpoon> { a, b }, (amount, at) => boss.Damage(amount));
            Assert.AreEqual(hp - 2, boss.Hp);
            Assert.IsFalse(a.Active);
            Assert.IsFalse(b.Active);
        }

        [Test]
        public void StageIsClearedWhenTheBossHasNoHealthEvenWithBubblesAlive()
        {
            var boss = MakeBoss();
            Assert.IsFalse(BossFight.StageCleared(boss, 0), "an empty bubble list does not clear a boss stage");
            boss.Damage(boss.Hp - 1);
            Assert.IsFalse(BossFight.StageCleared(boss, 0));
            boss.Damage(1);
            Assert.AreEqual(0, boss.Hp);
            Assert.IsTrue(BossFight.StageCleared(boss, 3), "cleared with 3 bubbles alive");
            Assert.IsFalse(BossFight.StageCleared(null, 2), "a normal stage still needs the bubbles gone");
            Assert.IsTrue(BossFight.StageCleared(null, 0));
        }

        [Test]
        public void RemainingBubblesPopAndLeaveNoChildren()
        {
            var bubbles = new List<Bubble>();
            for (int i = 0; i < 3; i++)
                bubbles.Add(Bubble.Create(root.transform, BubbleKind.Gloop, i, new Vector2(i * 3f - 3f, 6f), Vector2.zero, false));
            var popped = new List<Bubble>();
            int count = BossFight.PopRemaining(bubbles, b =>
            {
                popped.Add(b);
                Object.DestroyImmediate(b.gameObject);
            });
            Assert.AreEqual(3, count);
            Assert.AreEqual(3, popped.Count);
            Assert.AreEqual(0, bubbles.Count, "the list is empty: nothing was spawned by the pops");
            Assert.AreEqual(0, root.GetComponentsInChildren<Bubble>().Length);
        }

        [Test]
        public void BossStageSpawnsNoBubblesAndRunsOnTheBossClock()
        {
            for (int world = 1; world <= 6; world++)
            {
                int stage = world * Tuning.I.stagesPerWorld;
                Assert.IsTrue(Run.IsBossStageNumber(stage));
                var plan = StageGenerator.Generate(stage, new System.Random(world));
                Assert.AreEqual(0, plan.spawns.Count, $"world {world}");
                for (int c = 0; c < StageMap.Cols; c++)
                    for (int r = 0; r < StageMap.Rows; r++)
                        Assert.IsFalse(plan.map.IsSolid(c, r), $"world {world}: boss stages have no platforms ({c},{r})");
            }
            Assert.AreEqual(120f, StageGenerator.BossTime(), 1e-4f);
            Assert.AreEqual(120f * Tuning.I.hardTimeScale, StageGenerator.BossTime(true), 1e-4f);
        }

        [Test]
        public void BossArtUsesTheWorldColoursAndHasAnInkOutline()
        {
            for (int world = 1; world <= 6; world++)
            {
                var theme = Themes.ForWorld(world);
                var sprites = PlaceholderArt.I.BossFor(theme, BossSpec.ForWorld(world));
                var tex = sprites.body.texture;
                Assert.AreEqual(62, tex.width);
                Assert.AreEqual(47, tex.height);
                var pixels = tex.GetPixels32();
                Assert.AreEqual((Color32)Palette.Ink, pixels[tex.width / 2], $"{theme.name}: bottom edge is ink");
                bool fill = false, cap = false;
                foreach (var p in pixels)
                {
                    fill |= p.Equals(theme.tileFill);
                    cap |= p.Equals(theme.tileCap);
                    Assert.IsFalse(p.Equals(Palette.Saffron) || p.Equals(Palette.Mint), $"{theme.name}: no saffron or mint");
                }
                Assert.IsTrue(fill, $"{theme.name}: tileFill body");
                Assert.IsTrue(cap, $"{theme.name}: tileCap band");
            }
        }
    }
}
