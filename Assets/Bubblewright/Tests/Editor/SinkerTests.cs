using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>The Sinker loses height on every landing, then rolls along its surface without falling off.</summary>
    public class SinkerTests
    {
        const float Dt = 1f / 60f;

        GameObject root;
        StageMap previousMap;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("SinkerTests");
            previousMap = Game.Map;
            SetMap(StageMap.Flat());
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

        static SinkerBehavior BehaviorOf(Bubble b)
        {
            var field = typeof(Bubble).GetField("behavior", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, "Bubble must keep its behaviour in a field called behavior.");
            var behavior = field.GetValue(b) as SinkerBehavior;
            Assert.NotNull(behavior, "A Sinker must have a SinkerBehavior.");
            return behavior;
        }

        Bubble Spawn(int tier, Vector2 position, Vector2 velocity, bool child = false) =>
            Bubble.Create(root.transform, BubbleKind.Sinker, tier, position, velocity, child);

        /// <summary>Peak height of the bubble's bottom above the floor for each bounce after the first drop.</summary>
        static List<float> BouncePeaks(Bubble b, int count, float maxSeconds)
        {
            var peaks = new List<float>();
            float peak = 0f;
            float prevVy = b.Velocity.y;
            bool seenFirstLanding = false;
            for (int i = 0; i < Mathf.RoundToInt(maxSeconds / Dt) && peaks.Count < count; i++)
            {
                b.Tick(Dt);
                float bottom = b.Position.y - b.Radius;
                if (prevVy < 0f && b.Velocity.y > 0f)
                {
                    if (seenFirstLanding) peaks.Add(peak);
                    seenFirstLanding = true;
                    peak = 0f;
                }
                peak = Mathf.Max(peak, bottom);
                prevVy = b.Velocity.y;
            }
            return peaks;
        }

        [Test]
        public void EachBounceIsNinetyPercentAsHighAsTheLastOnOrdinaryGround()
        {
            var b = Spawn(2, new Vector2(0f, 6f), Vector2.zero);
            var peaks = BouncePeaks(b, 6, 60f);
            Assert.AreEqual(6, peaks.Count);
            for (int i = 1; i < peaks.Count; i++)
                Assert.AreEqual(Tuning.I.sinkerKeep, peaks[i] / peaks[i - 1], 0.03f, $"bounce {i + 1}");
        }

        [Test]
        public void EachBounceIsSeventyFivePercentAsHighAsTheLastOnSand()
        {
            var map = StageMap.Flat();
            WorldRules.PaintAll(map, Surface.Sand);
            SetMap(map);
            var b = Spawn(2, new Vector2(0f, 6f), Vector2.zero);
            var peaks = BouncePeaks(b, 5, 60f);
            Assert.AreEqual(5, peaks.Count);
            for (int i = 1; i < peaks.Count; i++)
                Assert.AreEqual(Tuning.I.sinkerKeepSand, peaks[i] / peaks[i - 1], 0.03f, $"bounce {i + 1}");
        }

        [Test]
        public void OnSandAnLargeSinkerIsRollingWithinAMinuteAndStaysOnTheFloor()
        {
            var map = StageMap.Flat();
            WorldRules.PaintAll(map, Surface.Sand);
            SetMap(map);
            var b = Spawn(1, new Vector2(0f, 6f), new Vector2(Tuning.I.bubbleSpeedX, 0f));
            var behavior = BehaviorOf(b);
            int ticks = 0;
            while (!behavior.Rolling && ticks < Mathf.RoundToInt(60f / Dt)) { b.Tick(Dt); ticks++; }
            Assert.IsTrue(behavior.Rolling, "rolling within 60 s");
            for (int i = 0; i < 600; i++)
            {
                b.Tick(Dt);
                Assert.AreEqual(b.Radius, b.Position.y, 1e-3f, $"tick {i}");
            }
        }

        [Test]
        public void ARollingSinkerTurnsAtBothWallsAndKeepsItsSpeed()
        {
            var b = Spawn(2, new Vector2(0f, 1f), new Vector2(Tuning.I.bubbleSpeedX, 0f));
            var behavior = BehaviorOf(b);
            behavior.Keep = 0.001f;
            float min = 0f, max = 0f, speed = Mathf.Abs(b.Velocity.x);
            for (int i = 0; i < Mathf.RoundToInt(30f / Dt); i++)
            {
                b.Tick(Dt);
                min = Mathf.Min(min, b.Position.x);
                max = Mathf.Max(max, b.Position.x);
                if (behavior.Rolling) Assert.AreEqual(speed, Mathf.Abs(b.Velocity.x), 1e-4f);
            }
            Assert.IsTrue(behavior.Rolling);
            Assert.Less(min, -16f + b.Radius + 0.3f, "reached the left wall");
            Assert.Greater(max, 16f - b.Radius - 0.3f, "reached the right wall");
            Assert.GreaterOrEqual(min, -16f - 0.001f);
            Assert.LessOrEqual(max, 16f + 0.001f);
        }

        [Test]
        public void ARollingSinkerOnTheGroundTurnsBeforeAGap()
        {
            var map = StageMap.Flat();
            for (int c = 20; c <= 22; c++) map.SetGap(c, true);
            SetMap(map);
            var b = Spawn(2, new Vector2(0f, 1f), new Vector2(Tuning.I.bubbleSpeedX, 0f));
            var behavior = BehaviorOf(b);
            behavior.Keep = 0.001f;
            float max = float.MinValue;
            for (int i = 0; i < Mathf.RoundToInt(20f / Dt); i++)
            {
                b.Tick(Dt);
                max = Mathf.Max(max, b.Position.x);
                Assert.LessOrEqual(b.Position.x, 4f - b.Radius + 0.1f, $"tick {i}");
            }
            Assert.IsTrue(behavior.Rolling);
            Assert.Greater(max, 4f - b.Radius - 0.5f, "it did roll up to the gap");
        }

        [Test]
        public void ARollingSinkerOnAPlatformTurnsAtItsEnds()
        {
            var rows = new List<string>();
            for (int i = 0; i < StageMap.Rows; i++)
            {
                int r = StageMap.Rows - 1 - i;
                rows.Add(r == 4 ? new string('.', 10) + new string('#', 12) + new string('.', 10) : new string('.', StageMap.Cols));
            }
            SetMap(StageLayout.Parse(rows.ToArray()));
            // The platform covers columns 10 to 21, so x from -6 to 6, and its top is at y = 5.
            var b = Spawn(2, new Vector2(0f, 5f + Tuning.I.tiers[2].diameter * 0.5f), new Vector2(Tuning.I.bubbleSpeedX, 0f));
            var behavior = BehaviorOf(b);
            behavior.Keep = 0.001f;
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < Mathf.RoundToInt(20f / Dt); i++)
            {
                b.Tick(Dt);
                min = Mathf.Min(min, b.Position.x);
                max = Mathf.Max(max, b.Position.x);
                Assert.GreaterOrEqual(b.Position.x, -6f, $"tick {i}");
                Assert.LessOrEqual(b.Position.x, 6f, $"tick {i}");
                if (behavior.Rolling) Assert.AreEqual(5f + b.Radius, b.Position.y, 1e-3f, $"tick {i}");
            }
            Assert.IsTrue(behavior.Rolling);
            Assert.Less(min, -6f + b.Radius + 0.5f, "reached the left end");
            Assert.Greater(max, 6f - b.Radius - 0.5f, "reached the right end");
        }

        [Test]
        public void ASinkerBornFromAPopStartsWithAFullBounce()
        {
            var child = Spawn(2, new Vector2(0f, 5f), new Vector2(Tuning.I.bubbleSpeedX, 3f), true);
            var behavior = BehaviorOf(child);
            Assert.AreEqual(1f, behavior.Keep, 1e-6f);
            Assert.IsFalse(behavior.Rolling);
        }

        [Test]
        public void ASinkerThatLosesItsFloorFallsAndLandsRollingAgain()
        {
            var map = StageMap.Flat();
            map.Set(10, 4, new Cell { kind = TileKind.Breakable, hp = 1 });
            SetMap(map);
            var b = Spawn(3, new Vector2(-5.5f, 5.45f), new Vector2(0f, 0f));
            var behavior = BehaviorOf(b);
            behavior.Keep = 0.001f;
            for (int i = 0; i < 30; i++) b.Tick(Dt);
            Assert.IsTrue(behavior.Rolling);
            Assert.IsTrue(map.Damage(10, 4), "the tile it rests on is shot away");
            for (int i = 0; i < 600; i++) b.Tick(Dt);
            Assert.IsTrue(behavior.Rolling, "it is rolling again, on the ground");
            Assert.AreEqual(b.Radius, b.Position.y, 1e-3f);
        }
    }
}
