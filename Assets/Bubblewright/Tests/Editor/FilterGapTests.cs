using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>
    /// Measurements the World 1 stages are built on. Slotted Bunker and Trigloop Sieve rely on a 1-tile slot holding
    /// M and bigger and a 2-tile hole holding L and bigger. Clinger Pogo stages rely on Moss getting past a triggered floor
    /// Pogo. Each test logs its numbers so they can be copied into the spec.
    /// </summary>
    public class FilterGapTests
    {
        const float Dt = 1f / 60f;
        const float Seconds = 60f;
        const int Drops = 24;
        const int RoofRow = 4;

        GameObject root;
        StageMap previousMap;
        Vector2 previousFeet;
        Rect previousBox;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("FilterGapTests");
            previousMap = Game.Map;
            previousFeet = Game.WickFeet;
            previousBox = Game.WickBox;
        }

        [TearDown]
        public void TearDown()
        {
            Game.InstallMap(previousMap);
            Game.WickFeet = previousFeet;
            Game.WickBox = previousBox;
            Object.DestroyImmediate(root);
        }

        /// <summary>A roof across the whole arena on row 4 with one slot in the middle, or three at columns 8, 16 and 24.</summary>
        static StageMap Roof(int width, int slots)
        {
            var map = StageMap.Flat();
            for (int c = 0; c < StageMap.Cols; c++) map.Set(c, RoofRow, new Cell { kind = TileKind.Solid });
            var centres = slots == 1 ? new[] { 16 } : new[] { 8, 16, 24 };
            foreach (int centre in centres)
                for (int c = centre - width / 2; c < centre - width / 2 + width; c++)
                    map.Set(c, RoofRow, new Cell { kind = TileKind.Empty });
            return map;
        }

        /// <summary>Drops one Gloop of the tier above the roof. Returns the seconds until its centre is under the roof, or -1.</summary>
        float Drop(int tier, float x, int dir)
        {
            var b = Bubble.Create(root.transform, BubbleKind.Gloop, tier, new Vector2(x, 9f),
                new Vector2(dir * Tuning.I.bubbleSpeedX, 0f), false);
            try
            {
                int ticks = Mathf.RoundToInt(Seconds / Dt);
                for (int i = 0; i < ticks; i++)
                {
                    b.Tick(Dt);
                    if (b.Position.y < RoofRow) return (i + 1) * Dt;
                }
                return -1f;
            }
            finally
            {
                Object.DestroyImmediate(b.gameObject);
            }
        }

        /// <summary>How many of Drops evenly spread start points fall through within Seconds, and the median time of those that do.</summary>
        int PassCount(int tier, out float median)
        {
            var times = new List<float>();
            for (int i = 0; i < Drops; i++)
            {
                float x = -14f + i * 28f / (Drops - 1);
                float t = Drop(tier, x, i % 2 == 0 ? 1 : -1);
                if (t >= 0f) times.Add(t);
            }
            times.Sort();
            median = times.Count > 0 ? times[times.Count / 2] : -1f;
            return times.Count;
        }

        [Test]
        public void SlotsFilterBubblesBySize()
        {
            string[] names = { "XL", "L", "M", "S" };
            var counts = new Dictionary<(int slots, int width, int tier), int>();
            var log = new StringBuilder("Gloops dropped onto a roof on row 4, " + Drops + " start points, " + Seconds + " s each:\n");
            foreach (int slots in new[] { 1, 3 })
                foreach (int width in new[] { 1, 2, 3 })
                {
                    Game.InstallMap(Roof(width, slots));
                    foreach (int tier in new[] { StageBuilder.TierS, StageBuilder.TierM, StageBuilder.TierL })
                    {
                        int n = PassCount(tier, out float median);
                        counts[(slots, width, tier)] = n;
                        log.AppendLine($"  {slots} slot(s) {width} wide, {names[tier]}: {n}/{Drops} pass, median {(median < 0f ? "-" : median.ToString("0.0") + " s")}");
                    }
                }
            Debug.Log(log.ToString());
            TestContext.WriteLine(log.ToString());

            // A 1-tile slot holds M and L. Three of them still let S through, if slowly (about 26 s median in the
            // offline model): they are firing slots first and drop slots second.
            Assert.AreEqual(0, counts[(3, 1, StageBuilder.TierM)], "an M must not fit a 1-tile slot");
            Assert.AreEqual(0, counts[(3, 1, StageBuilder.TierL)], "an L must not fit a 1-tile slot");
            Assert.GreaterOrEqual(counts[(3, 1, StageBuilder.TierS)], Drops / 2, "an S must get through three 1-tile slots");
            // A 2-tile hole is the M and S sieve: it holds L and lets M through.
            Assert.AreEqual(0, counts[(3, 2, StageBuilder.TierL)], "an L must not fit a 2-tile hole");
            Assert.GreaterOrEqual(counts[(3, 2, StageBuilder.TierM)], Drops / 2, "an M must get through two-tile holes");
            // A 3-tile hole is no filter: L gets through.
            Assert.GreaterOrEqual(counts[(3, 3, StageBuilder.TierL)], Drops / 2, "an L gets through 3-tile holes");
        }

        /// <summary>
        /// Moss with Heavy Boots runs at a patient floor Clinger Pogo and either jumps at a take-off point or just keeps
        /// running. Logs which approaches get past without a touch. A measurement for the World 2 Clinger Pogo stages.
        /// </summary>
        [Test]
        public void MossGetsPastATriggeredFloorPogo()
        {
            var log = new StringBuilder("Moss with Heavy Boots vs a triggered floor Clinger Pogo at x = 4 (take-off x, result):\n");
            int clean = 0, runs = 0;
            foreach (int tier in new[] { StageBuilder.TierM, StageBuilder.TierS })
            {
                log.AppendLine($"  {(tier == StageBuilder.TierM ? "M" : "S")} Pogo:");
                for (float takeOff = 0f; takeOff <= 3.51f; takeOff += 0.25f)
                {
                    bool ok = Approach(tier, takeOff, true);
                    log.AppendLine($"    jump at {takeOff:0.00}: {(ok ? "clean" : "touched")}");
                    runs++;
                    if (ok) clean++;
                }
                bool runOnly = Approach(tier, 0f, false);
                log.AppendLine($"    no jump: {(runOnly ? "clean" : "touched")}");
            }
            log.AppendLine($"  clean jumps: {clean}/{runs}");
            Debug.Log(log.ToString());
            TestContext.WriteLine(log.ToString());
            Assert.AreEqual(30, runs, "the sweep ran");
        }

        bool Approach(int tier, float takeOffX, bool jump)
        {
            Game.InstallMap(StageMap.Flat());
            var holder = new GameObject("Approach");
            holder.transform.SetParent(root.transform, false);
            try
            {
                var fx = Effects.Create(holder.transform);
                var wick = Wick.Create(holder.transform, fx);
                wick.BaseRunSpeed = Character.ById("moss").BaseRunSpeed * Debuff.ByName("Heavy Boots").speedScale;
                wick.transform.position = new Vector3(-6f, 0f, 0f);
                Game.WickFeet = wick.transform.position;
                Game.WickBox = wick.Hitbox;

                float radius = Tuning.I.tiers[tier].diameter * 0.5f;
                var pogo = Bubble.Create(holder.transform, BubbleKind.Clinger, tier, new Vector2(4f, radius),
                    new Vector2(Tuning.I.bubbleSpeedX, 0f), false, false, true, false, ClingerMount.Floor, true);

                bool jumped = false;
                for (int i = 0; i < Mathf.RoundToInt(4f / Dt); i++)
                {
                    bool press = jump && !jumped && wick.transform.position.x >= takeOffX;
                    if (press) jumped = true;
                    wick.Tick(1f, 0f, press, Dt, true);
                    Game.WickFeet = wick.transform.position;
                    Game.WickBox = wick.Hitbox;
                    pogo.Tick(Dt);
                    if (pogo.CanHurt && pogo.Overlaps(wick.Hitbox)) return false;
                    if (wick.transform.position.x > 4f + radius + 2f && wick.Mode == Mode.Ground) return true;
                }
                return false;
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }
    }
}
