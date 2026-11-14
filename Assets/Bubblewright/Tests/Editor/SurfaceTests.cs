using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    public class SurfaceTests
    {
        const float Dt = 1f / 60f;

        GameObject root;
        Wick wick;
        StageMap previousMap;
        StageMap map;

        [SetUp]
        public void SetUp()
        {
            previousMap = Game.Map;
            map = StageMap.Flat();
            SetMap(map);
            root = new GameObject("SurfaceTestRoot");
            var fx = Effects.Create(root.transform);
            wick = Wick.Create(root.transform, fx);
            wick.ResetForStage(0f, 0f, 3);
        }

        [TearDown]
        public void TearDown()
        {
            SetMap(previousMap);
            Object.DestroyImmediate(root);
        }

        static void SetMap(StageMap m)
        {
            var property = typeof(Game).GetProperty("Map", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(property, "Game.Map must exist.");
            property.SetValue(null, m);
        }

        void Step(float seconds, float move)
        {
            int ticks = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < ticks; i++) wick.Tick(move, 0f, Dt, true);
        }

        static void AllGround(StageMap m, Surface s)
        {
            for (int c = 0; c < StageMap.Cols; c++) m.SetGround(c, s);
        }

        [Test]
        public void IceTuningDefaultsMatchSpec()
        {
            Assert.AreEqual(0.35f, Tuning.I.iceAccelTime, 1e-4f);
            Assert.AreEqual(0.30f, Tuning.I.iceTurnTime, 1e-4f);
            Assert.AreEqual(0.9f, Tuning.I.iceStopTime, 1e-4f);
        }

        [Test]
        public void IceIsSlowToStart()
        {
            AllGround(map, Surface.Ice);
            Step(0.17f, 1f);
            float top = Tuning.I.runSpeed;
            Assert.LessOrEqual(wick.VelocityX, top * 0.5f + 0.01f, "on ice he is around half of top speed after 0.17 s");
            Step(0.20f, 1f);
            Assert.AreEqual(top, wick.VelocityX, 0.001f, "reaches top speed by 0.37 s");
        }

        [Test]
        public void IceReversesMuchFasterThanCoastingButSlowerThanNormalGround()
        {
            // Normal ground reverses from -runSpeed to +runSpeed within 0.12 s.
            Step(0.25f, -1f);
            Assert.AreEqual(-Tuning.I.runSpeed, wick.VelocityX, 0.001f);
            Step(0.12f, 1f);
            Assert.AreEqual(Tuning.I.runSpeed, wick.VelocityX, 0.001f, "normal ground reverses almost immediately");

            // Reset onto ice and reach full left speed (-runSpeed).
            AllGround(map, Surface.Ice);
            wick.ResetForStage(0f, 0f, 3);
            Step(0.45f, -1f);
            float top = Tuning.I.runSpeed;
            Assert.AreEqual(-top, wick.VelocityX, 0.001f);

            // Press right: still sliding left for the first several frames, shedding speed progressively.
            Step(4f * Dt, 1f); // ~0.067 s
            float vAfter4 = wick.VelocityX;
            Assert.That(vAfter4, Is.InRange(-top + 0.5f, -1f), "still moving left, visibly slower after 4 frames");

            Step(7f * Dt, 1f); // ~0.183 s total
            float vAfter11 = wick.VelocityX;
            Assert.That(vAfter11, Is.GreaterThan(vAfter4).And.LessThan(0f), "still moving left, slower again after 11 frames");

            // Past 0.30 s of braking + 6 frames of acceleration (0.40 s total): now moving right, not yet at top speed.
            Step(13f * Dt, 1f); // 0.40 s total
            Assert.That(wick.VelocityX, Is.GreaterThan(0.5f).And.LessThan(top - 0.5f), "moving right and building speed by 0.40 s");

            // Full reverse complete by ~0.70 s (0.65 s exact, vs 1.4 s previously).
            Step(0.30f, 1f);
            Assert.AreEqual(top, wick.VelocityX, 0.001f, "at full right speed by ~0.70 s");
        }

        [Test]
        public void IceReacceleratesWithIceAccelTimeAfterBriefCoast()
        {
            AllGround(map, Surface.Ice);
            float top = Tuning.I.runSpeed;
            Step(0.45f, 1f);
            Assert.AreEqual(top, wick.VelocityX, 0.001f);

            // Coast to roughly half speed (~0.45 s of 0.9 s stop time -> ~4.5 u/s).
            Step(0.45f, 0f);
            Assert.That(wick.VelocityX, Is.InRange(top * 0.4f, top * 0.6f));

            // After 6 frames (0.10 s) pressing right, iceAccelTime (0.35 s) puts speed near 7.07 u/s,
            // whereas mistakenly using iceTurnTime (0.30 s) would already reach 7.50 u/s.
            Step(6f * Dt, 1f);
            Assert.That(wick.VelocityX, Is.InRange(top * 0.75f, top * 0.81f), "uses iceAccelTime rather than iceTurnTime");

            // Finishes recovering to top speed by ~0.22 s total.
            Step(0.12f, 1f);
            Assert.AreEqual(top, wick.VelocityX, 0.001f);
        }

        [Test]
        public void JumpingOffIceSteersAtNormalGroundRateInAir()
        {
            AllGround(map, Surface.Ice);
            float top = Tuning.I.runSpeed;
            Step(0.45f, -1f);
            Assert.AreEqual(-top, wick.VelocityX, 0.001f);

            // Jump while running left on ice, then press right in the air: airborne steering uses normal ground rates.
            wick.Tick(-1f, 0f, true, Dt, true);
            Assert.IsTrue(wick.Jumping);
            Step(0.12f, 1f);
            Assert.AreEqual(top, wick.VelocityX, 0.001f);
        }

        [Test]
        public void NormalGroundReachesTopSpeedByQuarterSecond()
        {
            Step(0.25f, 1f);
            Assert.AreEqual(Tuning.I.runSpeed, wick.VelocityX, 0.001f);
        }

        [Test]
        public void IceIsSlowToStop()
        {
            AllGround(map, Surface.Ice);
            Step(0.7f, 1f);
            Assert.AreEqual(Tuning.I.runSpeed, wick.VelocityX, 0.001f, "top speed is unchanged on ice");
            Step(0.4f, 0f);
            Assert.Greater(wick.VelocityX, 0.5f, "still sliding 0.4 s after letting go");
            Step(0.6f, 0f);
            Assert.AreEqual(0f, wick.VelocityX, 0.001f, "stopped by 1.0 s");
        }

        [Test]
        public void SandCapsTopSpeed()
        {
            AllGround(map, Surface.Sand);
            float limit = Tuning.I.runSpeed * Tuning.I.sandSpeedScale + 0.01f;
            for (int i = 0; i < 60; i++)
            {
                wick.Tick(1f, 0f, Dt, true);
                Assert.LessOrEqual(wick.VelocityX, limit, $"tick {i}");
            }
            Assert.AreEqual(Tuning.I.runSpeed * Tuning.I.sandSpeedScale, wick.VelocityX, 0.01f);
        }

        [Test]
        public void DesertMakesEverythingSand()
        {
            var m = StageLayout.Parse(StageLayouts.All[1].rows);
            WorldRules.Apply(m, 3, new System.Random(5));
            for (int c = 0; c < StageMap.Cols; c++)
            {
                Assert.AreEqual(Surface.Sand, m.GetGround(c), $"ground column {c}");
                for (int r = 0; r < StageMap.Rows; r++)
                    if (m.IsSolid(c, r)) Assert.AreEqual(Surface.Sand, m.Get(c, r).surface, $"tile {c},{r}");
            }
        }

        [Test]
        public void WinterHasIceButNeverUnderWickAtTheStart()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var m = StageLayout.Parse(StageLayouts.All[1].rows);
                WorldRules.Apply(m, 4, new System.Random(seed));
                bool anyIce = false;
                for (int c = 0; c < StageMap.Cols; c++)
                {
                    if (m.GetGround(c) == Surface.Ice) anyIce = true;
                    if (c >= 14 && c <= 17) Assert.AreEqual(Surface.Normal, m.GetGround(c), $"seed {seed}, column {c}");
                }
                Assert.IsTrue(anyIce, $"seed {seed} has some ice on the ground");
            }
        }

        [Test]
        public void WinterIcesWholePlatformsOrNone()
        {
            bool sawIce = false, sawNormal = false;
            for (int seed = 0; seed < 50; seed++)
            {
                var m = StageLayout.Parse(StageLayouts.All[1].rows);
                WorldRules.Apply(m, 4, new System.Random(seed));
                for (int r = 0; r < StageMap.Rows; r++)
                {
                    int c = 0;
                    while (c < StageMap.Cols)
                    {
                        if (!m.IsSolid(c, r)) { c++; continue; }
                        Surface first = m.Get(c, r).surface;
                        while (c < StageMap.Cols && m.IsSolid(c, r))
                        {
                            Assert.AreEqual(first, m.Get(c, r).surface, $"seed {seed}, row {r}, column {c}");
                            c++;
                        }
                        if (first == Surface.Ice) sawIce = true; else sawNormal = true;
                    }
                }
            }
            Assert.IsTrue(sawIce && sawNormal, "some platforms are icy and some are not");
        }

        [Test]
        public void OtherWorldsChangeNothing()
        {
            foreach (int world in new[] { 1, 2, 5, 6 })
            {
                var m = StageLayout.Parse(StageLayouts.All[1].rows);
                int before = m.Version;
                WorldRules.Apply(m, world, new System.Random(1));
                Assert.AreEqual(before, m.Version, $"world {world}");
                for (int c = 0; c < StageMap.Cols; c++)
                {
                    Assert.AreEqual(Surface.Normal, m.GetGround(c));
                    for (int r = 0; r < StageMap.Rows; r++) Assert.AreEqual(Surface.Normal, m.Get(c, r).surface);
                }
            }
        }

        [Test]
        public void DesertStagesGetMoreTime()
        {
            Assert.AreEqual((60f + 16f) * Tuning.I.desertTimeScale, StageGenerator.StageTime(13), 0.001f);
            Assert.AreEqual(60f + 24f, StageGenerator.StageTime(19), 0.001f, "Winter is unchanged");
        }
    }
}
