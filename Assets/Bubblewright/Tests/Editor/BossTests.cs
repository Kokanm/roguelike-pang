using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>Boss framework and the six bosses: health, movement, aimed and bouncy bullets, balloons, harpoon damage and the clear rule.</summary>
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
        public void SixBossesMatchTheTable()
        {
            // name, w, h, speed, aimedEvery, aimedCount, aimedSpread, aimedBurst, bouncyEvery, bouncyBounces, balloonEvery, balloonTier, enrage, shape
            var rows = new (string, float, float, float, float, int, float, int, float, int, float, int, bool, BossShape)[]
            {
                ("CHIMNEY KING",  4f,   3f,   3.0f, 2.0f, 1, 0f,  1, 0f,   0, 8f, 2, false, BossShape.Block),
                ("OLD BRAMBLE",   5f,   2.5f, 3.5f, 0f,   1, 0f,  1, 2.2f, 2, 7f, 2, false, BossShape.Wide),
                ("DUNE WARDEN",   3.5f, 3.5f, 4.0f, 2.5f, 3, 30f, 1, 0f,   0, 9f, 1, false, BossShape.Round),
                ("FROSTMAW",      4.5f, 3f,   4.0f, 3.5f, 1, 0f,  1, 1.8f, 3, 8f, 2, false, BossShape.Jaw),
                ("BONE ABBOT",    3f,   4f,   4.5f, 3.0f, 1, 0f,  3, 2.5f, 1, 8f, 1, false, BossShape.Tall),
                ("CINDER TYRANT", 5f,   3.5f, 5.0f, 2.2f, 3, 30f, 1, 2.0f, 4, 6f, 1, true,  BossShape.Crown),
            };
            var names = new HashSet<string>();
            for (int world = 1; world <= 6; world++)
            {
                var spec = BossSpec.ForWorld(world);
                var r = rows[world - 1];
                string at = $"world {world}";
                Assert.AreEqual(r.Item1, spec.name, at);
                Assert.AreEqual(new Vector2(r.Item2, r.Item3), spec.size, at);
                Assert.AreEqual(r.Item4, spec.speed, 1e-5f, at);
                Assert.AreEqual(r.Item5, spec.aimedEvery, 1e-5f, at);
                Assert.AreEqual(r.Item6, spec.aimedCount, at);
                Assert.AreEqual(r.Item7, spec.aimedSpread, 1e-5f, at);
                Assert.AreEqual(r.Item8, spec.aimedBurst, at);
                Assert.AreEqual(r.Item9, spec.bouncyEvery, 1e-5f, at);
                Assert.AreEqual(r.Item10, spec.bouncyBounces, at);
                Assert.AreEqual(r.Item11, spec.balloonEvery, 1e-5f, at);
                Assert.AreEqual(r.Item12, spec.balloonTier, at);
                Assert.AreEqual(r.Item13, spec.enrageAtHalf, at);
                Assert.AreEqual(r.Item14, spec.shape, at);
                names.Add(spec.name);
            }
            Assert.AreEqual(6, names.Count, "six different names");
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
            // World 1 shoots every 2 s, the first shot 1 s after sliding in: at 1, 3 and 5 s.
            for (int i = 0; i < 60 * 6; i++)
            {
                clock += Dt;
                boss.Tick(Dt, WickCentreAt(0f), true);
            }
            Assert.AreEqual(3, times.Count);
            Assert.AreEqual(BossSpec.AimedStart, times[0], 0.05f);
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
                if (boss.TellOn) { blinkFrames++; Assert.Greater(clock, BossSpec.AimedStart - 0.55f, "only in the last half second"); }
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
                var spec = BossSpec.ForWorld(world);
                var sprites = PlaceholderArt.I.BossFor(theme, spec);
                var tex = sprites.body.texture;
                int w = Mathf.RoundToInt(spec.size.x * Tuning.I.pixelsPerUnit), h = Mathf.RoundToInt(spec.size.y * Tuning.I.pixelsPerUnit);
                var (top, bottom) = PlaceholderArt.BossExtra(spec.shape);
                Assert.AreEqual(w + 2, tex.width, $"{theme.name}: width");
                Assert.AreEqual(h + top + bottom + 2, tex.height, $"{theme.name}: height");
                var pixels = tex.GetPixels32();
                int lowest = 0;
                while (lowest < tex.height && pixels[lowest * tex.width + tex.width / 2].a == 0) lowest++;
                Assert.Less(lowest, tex.height);
                Assert.AreEqual((Color32)Palette.Ink, pixels[lowest * tex.width + tex.width / 2], $"{theme.name}: bottom edge is ink");
                bool fill = false, cap = false;
                foreach (var p in pixels)
                {
                    fill |= p.Equals(theme.tileFill);
                    cap |= p.Equals(theme.tileCap);
                    Assert.IsFalse(p.Equals(Palette.Saffron) || p.Equals(Palette.Mint), $"{theme.name}: no saffron or mint");
                    Assert.IsFalse(p.Equals(Palette.Coral) || p.Equals(Palette.Azure) || p.Equals(Palette.Plum) || p.Equals(Palette.Lemon),
                        $"{theme.name}: no bubble colours");
                }
                Assert.IsTrue(fill, $"{theme.name}: tileFill body");
                Assert.IsTrue(cap, $"{theme.name}: tileCap band");
            }
        }

        [Test]
        public void TheVisorDoublesWhileReleasingABalloon()
        {
            for (int world = 1; world <= 6; world++)
            {
                var theme = Themes.ForWorld(world);
                var sprites = PlaceholderArt.I.BossFor(theme, BossSpec.ForWorld(world));
                int closed = 0, open = 0;
                foreach (var p in sprites.body.texture.GetPixels32()) if (p.Equals(theme.skyTop)) closed++;
                foreach (var p in sprites.open.texture.GetPixels32()) if (p.Equals(theme.skyTop)) open++;
                Assert.Greater(open, closed, theme.name);
            }
        }

        // ------------------------------------------------------------------ bouncy bullets

        [Test]
        public void BouncyBulletWithThreeBouncesTouchesTheFloorFourTimesAndHopsToThePeak()
        {
            var t = Tuning.I;
            var b = Bullet.CreateBouncy(root.transform, new Vector2(0f, 9f), 1, 3, Themes.ForWorld(2));
            int bouncesSeen = 0;
            float top = float.MinValue;
            int lastTouches = 0;
            for (int i = 0; i < 60 * 20 && !b.Out; i++)
            {
                b.Tick(Dt);
                if (b.FloorTouches != lastTouches)
                {
                    // A touch ends a hop: every hop after the first bounce must top out at bouncyPeak.
                    if (lastTouches >= 1)
                    {
                        Assert.AreEqual(t.bouncyPeak, top, 0.1f, $"hop {lastTouches}");
                        bouncesSeen++;
                    }
                    lastTouches = b.FloorTouches;
                    top = float.MinValue;
                }
                top = Mathf.Max(top, b.Position.y + b.Radius);
                Assert.GreaterOrEqual(b.Position.x - b.Radius, -t.HalfWidth - 1e-4f);
                Assert.LessOrEqual(b.Position.x + b.Radius, t.HalfWidth + 1e-4f);
            }
            Assert.IsTrue(b.Out, "removed in the end");
            Assert.AreEqual(4, b.FloorTouches, "three bounces and the touch it falls through on");
            Assert.AreEqual(3, bouncesSeen);
        }

        [Test]
        public void AfterItsLastBounceABouncyBulletFallsThroughTheFloorAndCannotHurt()
        {
            var b = Bullet.CreateBouncy(root.transform, new Vector2(0f, 4f), -1, 1, Themes.ForWorld(2));
            Assert.IsTrue(b.CanHurt);
            bool wentBelow = false;
            for (int i = 0; i < 60 * 10 && !b.Out; i++)
            {
                b.Tick(Dt);
                if (b.Position.y < 0f)
                {
                    wentBelow = true;
                    Assert.IsTrue(b.Sinking);
                    Assert.IsFalse(b.CanHurt, "cannot hurt once its centre is under the floor");
                    var wick = new Rect(b.Position.x - 0.5f, b.Position.y - 0.5f, 1f, 1f);
                    Assert.IsTrue(b.Overlaps(wick) && !b.CanHurt);
                }
            }
            Assert.IsTrue(wentBelow);
            Assert.IsTrue(b.Out);
            Assert.LessOrEqual(b.Position.y, -1f + 0.5f, "removed about 1 unit under the floor");
        }

        [Test]
        public void BouncyBulletHeadsTowardWick()
        {
            var boss = MakeBoss(2);
            SlideIn(boss, Vector2.zero);
            int dir = 0;
            boss.OnBouncy = (from, d, n) => dir = d;
            boss.FireBouncy(WickCentreAt(-10f));
            Assert.AreEqual(-1, dir);
            boss.FireBouncy(WickCentreAt(10f));
            Assert.AreEqual(1, dir);
        }

        // ------------------------------------------------------------------ balloons

        [Test]
        public void NoBalloonWhileTheBubbleCapIsReachedThenOneWithinItsInterval()
        {
            var boss = MakeBoss(2); // OLD BRAMBLE: a balloon every 7 s
            Assert.AreEqual(7f, boss.Spec.balloonEvery, 1e-5f);
            SlideIn(boss, WickCentreAt(0f));
            int balloons = 0, tier = -1;
            Vector2 at = default;
            boss.OnBalloon = (p, d, tr) => { balloons++; tier = tr; at = p; };
            int cap = Tuning.I.bossMaxBubbles;
            for (int i = 0; i < 60 * 30; i++) boss.Tick(Dt, WickCentreAt(0f), true, 1f, cap);
            Assert.AreEqual(0, balloons, "none while the cap is reached");

            float clock = 0f;
            for (int i = 0; i < 60 * 7 && balloons == 0; i++)
            {
                clock += Dt;
                boss.Tick(Dt, WickCentreAt(0f), true, 1f, cap - 1);
            }
            Assert.AreEqual(1, balloons, "one within 7 s once a bubble is popped");
            Assert.LessOrEqual(clock, 7f + 1e-3f);
            Assert.AreEqual(2, tier, "a medium Gloop");
            Assert.IsTrue(boss.Open, "the visor is open while it drops");
            Assert.Less(at.y, boss.Body.yMin, "drops from under the body");
            Assert.AreEqual(boss.Centre.x, at.x, 1e-4f, "from the bottom centre");
        }

        // ------------------------------------------------------------------ enrage

        [Test]
        public void EnragedBossIsFasterBelowHalfHealth()
        {
            var boss = MakeBoss(6);
            Assert.IsTrue(boss.Spec.enrageAtHalf);
            Assert.AreEqual(boss.Spec.speed, boss.CurrentSpeed, 1e-5f);
            boss.Damage(boss.MaxHp - boss.MaxHp / 2); // exactly half
            Assert.AreEqual(boss.Spec.speed, boss.CurrentSpeed, 1e-5f, "not yet at exactly half");
            boss.Damage(1); // half minus one
            Assert.AreEqual(boss.Spec.speed * 1.3f, boss.CurrentSpeed, 1e-4f);

            var calm = MakeBoss(1);
            calm.Damage(calm.MaxHp / 2 + 1);
            Assert.AreEqual(calm.Spec.speed, calm.CurrentSpeed, 1e-5f, "a boss without enrageAtHalf never speeds up");
        }

        // ------------------------------------------------------------------ walkability

        /// <summary>The dodger's jump: feet height and upward speed, with the same numbers as Wick's jump.</summary>
        struct Hop
        {
            public float y, vy;
            public bool InAir => y > 0f || vy > 0f;

            public void Start() => vy = Tuning.I.JumpSpeed;

            public void Step(float dt)
            {
                if (!InAir) return;
                vy -= Tuning.I.JumpGravity * dt;
                y += vy * dt;
                if (y <= 0f) { y = 0f; vy = 0f; }
            }
        }

        /// <summary>
        /// Hits Wick takes in 30 s against one boss. Each bullet that touches him counts once and is removed; there is
        /// no invulnerability, so standing still is punished for every bullet. Balloons are left out: they are ordinary
        /// bubbles, shot and dodged as in any stage. Bouncy bullets hop too low to walk under, so the dodger also jumps
        /// when one is within 1.5 units of him horizontally and below 1 unit high.
        /// </summary>
        int HitsIn30Seconds(int world, bool dodge)
        {
            var t = Tuning.I;
            var theme = Themes.ForWorld(world);
            var boss = MakeBoss(world);
            var flights = new List<Bullet.Flight>();
            boss.OnShoot = (from, dir) => flights.Add(Bullet.Flight.Aimed(from, dir * t.bulletSpeed));
            boss.OnBouncy = (from, dir, n) => flights.Add(Bullet.Flight.BouncyFrom(from, dir, n));
            float x = 0f;
            var hop = new Hop();
            SlideIn(boss, WickCentreAt(x));
            int hits = 0;
            for (int i = 0; i < 60 * 30; i++)
            {
                if (dodge)
                {
                    if (!hop.InAir && ShouldJump(x, flights)) hop.Start();
                    x = Clamp(x + Dodge(x, hop, flights, boss.Centre.x) * t.runSpeed * Dt);
                    hop.Step(Dt);
                }
                boss.Tick(Dt, WickCentreAt(x), true);
                hits += StepFlights(flights, x, hop.y, Dt);
            }
            Object.DestroyImmediate(boss.gameObject);
            return hits;
        }

        static bool ShouldJump(float x, List<Bullet.Flight> flights)
        {
            foreach (var f in flights)
                if (f.bouncy && f.CanHurt && Mathf.Abs(f.position.x - x) < 1.5f && f.position.y < 1f)
                    return true;
            return false;
        }

        static float Clamp(float x)
        {
            var t = Tuning.I;
            float limit = t.HalfWidth - t.hitbox.x * 0.5f;
            return Mathf.Clamp(x, -limit, limit);
        }

        static Rect WickBoxAt(float x, float feetY = 0f)
        {
            var t = Tuning.I;
            return new Rect(x - t.hitbox.x * 0.5f, feetY, t.hitbox.x, t.hitbox.y);
        }

        /// <summary>Moves every bullet one step; the ones that hit Wick at x (feet at feetY) or leave are removed. Returns the hits.</summary>
        static int StepFlights(List<Bullet.Flight> flights, float x, float feetY, float dt)
        {
            int hits = 0;
            var box = WickBoxAt(x, feetY);
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                var f = flights[i];
                f.Step(dt);
                if (f.CanHurt && f.Overlaps(box)) { hits++; flights.RemoveAt(i); continue; }
                if (f.Out) { flights.RemoveAt(i); continue; }
                flights[i] = f;
            }
            return hits;
        }

        /// <summary>Where a threat is: an aimed bullet's point at Wick's height, a bouncy bullet's x.</summary>
        static float ThreatX(Bullet.Flight f, float y)
        {
            if (f.bouncy || f.velocity.y >= -1e-4f) return f.position.x;
            float time = Mathf.Max(0f, (f.position.y - y) / -f.velocity.y);
            return f.position.x + f.velocity.x * time;
        }

        /// <summary>
        /// A simple dodger: it wants to walk away from the nearest threat (or back to the middle when there is none). It
        /// looks 0.75 s ahead (finishing any jump it is in) and takes that way if it is safe, otherwise standing or the
        /// other way, whichever is hit least.
        /// </summary>
        static int Dodge(float x, Hop hop, List<Bullet.Flight> flights, float bossX)
        {
            float y = Tuning.I.hitbox.y * 0.5f, best = float.MaxValue, threat = 0f;
            bool any = false;
            foreach (var f in flights)
            {
                if (!f.CanHurt) continue;
                float tx = ThreatX(f, y), d = Mathf.Abs(tx - x);
                if (d < best) { best = d; threat = tx; any = true; }
            }
            int want = !any ? (Mathf.Abs(x) < 1f ? 0 : x > 0f ? -1 : 1)
                : x > threat ? 1 : x < threat ? -1 : x >= bossX ? 1 : -1;
            int choice = want, fewest = int.MaxValue;
            foreach (int dir in new[] { want, 0, -want, -1, 1 })
            {
                int h = LookAhead(x, hop, dir, flights, 0.75f);
                if (h < fewest) { fewest = h; choice = dir; }
            }
            return choice;
        }

        static int LookAhead(float x, Hop hop, int dir, List<Bullet.Flight> flights, float seconds)
        {
            var copy = new List<Bullet.Flight>(flights);
            int hits = 0;
            for (int i = 0; i < Mathf.RoundToInt(seconds * 60f); i++)
            {
                x = Clamp(x + dir * Tuning.I.runSpeed * Dt);
                hop.Step(Dt);
                hits += StepFlights(copy, x, hop.y, Dt);
            }
            return hits;
        }

        [Test]
        public void EveryBossCanBeDodgedOnFoot()
        {
            for (int world = 1; world <= 6; world++)
            {
                int still = HitsIn30Seconds(world, false);
                int dodger = HitsIn30Seconds(world, true);
                Assert.Greater(still, 0, $"world {world}: standing still gets hit");
                Assert.Less(dodger * 2, still, $"world {world}: the dodger took {dodger} hits, standing still {still}");
            }
        }
    }
}
