using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>Boss bullet patterns (pure functions that return launches), the new bullet flights and the tells.</summary>
    public class BossPatternTests
    {
        const float Dt = 1f / 60f;
        StageMap previousMap;

        static void SetMap(StageMap map) =>
            typeof(Game).GetProperty("Map", BindingFlags.Public | BindingFlags.Static).SetValue(null, map);

        [SetUp] public void SetUp() { previousMap = Game.Map; SetMap(StageMap.Flat()); }
        [TearDown] public void TearDown() => SetMap(previousMap);

        static float AngleOf(Launch l) => Mathf.Atan2(l.velocity.y, l.velocity.x) * Mathf.Rad2Deg;

        [Test]
        public void Aimed_FlyTowardTarget()
        {
            var l = Patterns.Aimed(Vector2.zero, new Vector2(0f, -5f), 7f);
            Assert.AreEqual(BulletKind.Aimed, l.kind);
            Assert.AreEqual(7f, l.velocity.magnitude, 1e-3f);
            Assert.Less(l.velocity.y, 0f);
        }

        [Test]
        public void Fan_IsCentredOnTargetAndEvenlySpaced()
        {
            var fan = Patterns.Fan(Vector2.zero, new Vector2(10f, 0f), 7f, 3, 20f);
            Assert.AreEqual(3, fan.Length);
            Assert.AreEqual(0f, AngleOf(fan[1]), 1e-2f);
            Assert.AreEqual(20f, AngleOf(fan[0]) - AngleOf(fan[1]) , 1e-2f * 1f);
            Assert.AreEqual(20f, AngleOf(fan[1]) - AngleOf(fan[2]), 1e-2f);
        }

        [Test]
        public void Burst_ShotsAreSpacedInTime()
        {
            var burst = Patterns.Burst(Vector2.zero, new Vector2(5f, 0f), 7f, 4, 0.1f);
            Assert.AreEqual(4, burst.Length);
            for (int i = 0; i < 4; i++) Assert.AreEqual(i * 0.1f, burst[i].delay, 1e-4f);
        }

        [Test]
        public void RingWithGap_LeavesExactlyTheGapEmpty()
        {
            const int count = 24, gapWidth = 3;
            for (int gapStart = 0; gapStart < count; gapStart += 5)
            {
                var ring = Patterns.RingWithGap(Vector2.zero, count, gapStart, gapWidth, 5f);
                Assert.AreEqual(count - gapWidth, ring.Length);
                var slots = ring.Select(l => Mathf.RoundToInt(Mathf.Repeat(AngleOf(l), 360f) / (360f / count)) % count).ToArray();
                for (int i = 0; i < count; i++)
                {
                    bool inGap = ((i - gapStart) % count + count) % count < gapWidth;
                    Assert.AreEqual(!inGap, slots.Contains(i), $"slot {i}, gap starts at {gapStart}");
                }
            }
        }

        [Test]
        public void CurtainWithLane_NeverCoversItsLane()
        {
            for (int lane = 0; lane <= StageMap.Cols - 3; lane += 4)
            {
                var curtain = Patterns.CurtainWithLane(20f, lane, 3);
                Assert.AreEqual(StageMap.Cols - 3, curtain.Length);
                float laneLeft = StageMap.CellRect(lane, 0).xMin, laneRight = StageMap.CellRect(lane + 2, 0).xMax;
                foreach (var l in curtain)
                {
                    Assert.IsFalse(l.position.x > laneLeft && l.position.x < laneRight, $"bullet at {l.position.x} in lane {lane}");
                    Assert.Less(l.velocity.y, 0f);
                    Assert.AreEqual(0f, l.velocity.x);
                }
            }
        }

        [Test]
        public void CurtainWithLane_StartsAtTheCeilingOfTheStoreyThatContainsY()
        {
            var curtain = Patterns.CurtainWithLane(20f, 0, 3);
            Assert.That(curtain.All(l => l.position.y <= 28f && l.position.y > 26f));
        }

        [Test]
        public void Spiral_ArmsAreEvenlySpaced()
        {
            foreach (int arms in new[] { 2, 3, 5, 6 })
            {
                var s = Patterns.Spiral(Vector2.zero, arms, 33f, 4f);
                Assert.AreEqual(arms, s.Length);
                var angles = s.Select(l => Mathf.Repeat(AngleOf(l), 360f)).OrderBy(a => a).ToArray();
                for (int i = 0; i < arms; i++)
                {
                    float next = angles[(i + 1) % arms] + (i + 1 == arms ? 360f : 0f);
                    Assert.AreEqual(360f / arms, next - angles[i], 1e-2f);
                }
            }
        }

        [Test]
        public void Spiral_TurnsByTheStepEachCall()
        {
            float a = Mathf.Repeat(AngleOf(Patterns.Spiral(Vector2.zero, 1, 10f, 4f)[0]), 360f);
            float b = Mathf.Repeat(AngleOf(Patterns.Spiral(Vector2.zero, 1, 25f, 4f)[0]), 360f);
            Assert.AreEqual(15f, b - a, 1e-2f);
        }

        [Test]
        public void GroundWave_BothWaysWhenDirIsZero()
        {
            var waves = Patterns.GroundWave(0f, 3f, 0);
            Assert.AreEqual(2, waves.Length);
            Assert.AreEqual(-Mathf.Sign(waves[0].velocity.x), Mathf.Sign(waves[1].velocity.x));
            Assert.AreEqual(1, Patterns.GroundWave(0f, 3f, 1).Length);
        }

        static Bullet.Flight Flight(Launch l) => Bullet.Flight.FromLaunch(l);

        [Test]
        public void GroundWave_StopsAtAPlatformEdge()
        {
            var map = StageMap.Flat();
            for (int c = 10; c <= 15; c++) map.Set(c, 5, new Cell { kind = TileKind.Solid });
            SetMap(map);
            float surface = 6f, startX = StageMap.CenterX(11);
            var f = Flight(Patterns.GroundWave(surface, startX, 1)[0]);
            for (int i = 0; i < 600 && !f.Out; i++) f.Step(Dt);
            Assert.IsTrue(f.Out, "the wave should end");
            Assert.LessOrEqual(f.position.x, StageMap.CellRect(15, 5).xMax + 0.01f, "it must not ride off the edge");
            Assert.Greater(f.position.x, StageMap.CenterX(12));
        }

        [Test]
        public void GroundWave_StopsAtAWall()
        {
            // The wave runs along the top of a solid row 0 (surface y 1); the wall is a tile in row 1.
            var map = StageMap.Flat();
            for (int c = 0; c < StageMap.Cols; c++) map.Set(c, 0, new Cell { kind = TileKind.Solid });
            map.Set(20, 1, new Cell { kind = TileKind.Solid });
            SetMap(map);
            var f = Flight(Patterns.GroundWave(1f, StageMap.CenterX(15), 1)[0]);
            for (int i = 0; i < 600 && !f.Out; i++) f.Step(Dt);
            Assert.IsTrue(f.Out);
            Assert.LessOrEqual(f.position.x + f.radius, StageMap.CellRect(20, 1).xMin + 0.01f);
        }

        [Test]
        public void GroundWave_OnTheFloorStopsAtAGap()
        {
            var map = StageMap.Flat();
            map.SetGap(18, true);
            SetMap(map);
            var f = Flight(Patterns.GroundWave(0f, StageMap.CenterX(10), 1)[0]);
            for (int i = 0; i < 600 && !f.Out; i++) f.Step(Dt);
            Assert.IsTrue(f.Out);
            Assert.LessOrEqual(f.position.x, StageMap.CellRect(18, 0).xMin + 0.01f);
        }

        [Test]
        public void ArcingShell_LandsOnTheTargetAndRunsOnLand()
        {
            Vector2? landed = null;
            var target = new Vector2(6f, 0.5f);
            var f = Flight(Patterns.ArcingShell(new Vector2(-6f, 9f), target, p => landed = p));
            for (int i = 0; i < 600 && !f.Out; i++) f.Step(Dt);
            Assert.IsTrue(landed.HasValue, "onLand did not run");
            Assert.AreEqual(target.x, landed.Value.x, 0.5f);
        }

        [Test]
        public void ArcingShell_RunsOnLandOnlyOnce()
        {
            int calls = 0;
            var f = Flight(Patterns.ArcingShell(new Vector2(0f, 5f), new Vector2(2f, 0f), p => calls++));
            for (int i = 0; i < 600; i++) f.Step(Dt);
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void HomingMissile_NeverTurnsFasterThanItsRate()
        {
            const float rate = 60f;
            var f = Flight(Patterns.HomingMissile(new Vector2(0f, 10f), Vector2.right, rate, 5f));
            var wick = new Vector2(-5f, 10f); // directly behind it: it must swing round at the capped rate
            float prev = Mathf.Atan2(f.velocity.y, f.velocity.x) * Mathf.Rad2Deg;
            for (int i = 0; i < 120 && !f.Out; i++)
            {
                f.Step(Dt, wick);
                float now = Mathf.Atan2(f.velocity.y, f.velocity.x) * Mathf.Rad2Deg;
                Assert.LessOrEqual(Mathf.Abs(Mathf.DeltaAngle(prev, now)), rate * Dt + 1e-2f);
                prev = now;
            }
        }

        [Test]
        public void HomingMissile_KeepsItsSpeedAndDiesAfterItsLife()
        {
            var launch = Patterns.HomingMissile(new Vector2(0f, 10f), Vector2.right, 90f, 1f);
            var f = Flight(launch);
            float speed = f.velocity.magnitude;
            for (int i = 0; i < 30; i++) f.Step(Dt, new Vector2(5f, 5f));
            Assert.AreEqual(speed, f.velocity.magnitude, 1e-3f);
            Assert.IsFalse(f.Out);
            for (int i = 0; i < 40; i++) f.Step(Dt, new Vector2(5f, 5f));
            Assert.IsTrue(f.Out);
        }

        [Test]
        public void HomingMissile_DiesOnATile()
        {
            var map = StageMap.Flat();
            map.Set(16, 10, new Cell { kind = TileKind.Solid });
            SetMap(map);
            var f = Flight(Patterns.HomingMissile(new Vector2(StageMap.CenterX(10), 10.5f), Vector2.right, 0f, 10f));
            for (int i = 0; i < 300 && !f.Out; i++) f.Step(Dt, Vector2.zero);
            Assert.IsTrue(f.Out);
            Assert.Less(f.position.x, StageMap.CellRect(16, 10).xMax);
        }

        [Test]
        public void SweepStream_CrossesTheStoreyAndEnds()
        {
            var launch = Patterns.SweepStream(-10f, 10f, 5f, 0f);
            Assert.AreEqual(BulletKind.Stream, launch.kind);
            var f = Flight(launch);
            Assert.AreEqual(-10f, f.position.x, 1e-3f);
            int steps = 0;
            while (!f.Out && steps < 1000) { f.Step(Dt); steps++; }
            Assert.AreEqual(20f / 5f, steps * Dt, 0.1f);
            Assert.AreEqual(10f, f.position.x, 0.2f);
        }

        [Test]
        public void SweepStream_HurtsAnywhereUpItsColumn()
        {
            var f = Flight(Patterns.SweepStream(0f, 10f, 5f, 14f));
            Assert.IsTrue(f.Overlaps(new Rect(-0.1f, 14f + 10f, 0.2f, 0.5f)));
            Assert.IsFalse(f.Overlaps(new Rect(3f, 15f, 0.2f, 0.5f)));
        }

        // ---------------------------------------------------------------- tells

        [Test]
        public void Lead_IsNeverShorterThanTheMinimum()
        {
            float min = Tuning.I.bossTellMin;
            Assert.AreEqual(0.5f, min, 1e-4f);
            Assert.AreEqual(min, Tells.Lead(0.1f));
            Assert.AreEqual(min, Tells.Lead(0f));
            Assert.AreEqual(0.9f, Tells.Lead(0.9f));
        }

        [Test]
        public void Tell_StartsAtLeastHalfASecondBeforeItsAttack()
        {
            var tells = new Tells(null);
            bool fired = false;
            float lead = tells.Add(TellKind.LitLane, new Rect(0f, 0f, 3f, 14f), 0.1f, () => fired = true);
            Assert.GreaterOrEqual(lead, 0.5f);
            Assert.AreEqual(1, tells.Active);
            float t = 0f;
            while (!fired && t < 5f) { tells.Tick(Dt); t += Dt; }
            Assert.IsTrue(fired);
            Assert.GreaterOrEqual(t, 0.5f - Dt);
            Assert.AreEqual(0, tells.Active, "the tell goes when the attack fires");
        }

        [Test]
        public void Tell_FiresAtAnyKindsLongerLead()
        {
            foreach (TellKind kind in System.Enum.GetValues(typeof(TellKind)))
            {
                var tells = new Tells(null);
                bool fired = false;
                tells.Add(kind, new Rect(0f, 0f, 1f, 1f), 0.8f, () => fired = true);
                for (int i = 0; i < 0.7f / Dt; i++) tells.Tick(Dt);
                Assert.IsFalse(fired, kind.ToString());
                for (int i = 0; i < 0.2f / Dt; i++) tells.Tick(Dt);
                Assert.IsTrue(fired, kind.ToString());
            }
        }

        [Test]
        public void Tells_ClearDropsPendingAttacks()
        {
            var tells = new Tells(null);
            bool fired = false;
            tells.Add(TellKind.RedShadow, new Rect(0f, 0f, 1f, 1f), 1f, () => fired = true);
            tells.Clear();
            for (int i = 0; i < 120; i++) tells.Tick(Dt);
            Assert.IsFalse(fired);
        }

        [Test]
        public void Tells_DelayedLaunchesFireInOrder()
        {
            var tells = new Tells(null);
            var order = new System.Collections.Generic.List<int>();
            tells.After(0.2f, () => order.Add(2));
            tells.After(0.1f, () => order.Add(1));
            for (int i = 0; i < 30; i++) tells.Tick(Dt);
            CollectionAssert.AreEqual(new[] { 1, 2 }, order);
        }
    }
}
