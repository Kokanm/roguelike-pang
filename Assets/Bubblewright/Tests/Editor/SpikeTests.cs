using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    public class SpikeTests
    {
        GameObject root;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("SpikeTests");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void CycleHidesWarnsRisesThenHides()
        {
            // Shorter down time and longer up time since step 20: spikes can be jumped now.
            Assert.AreEqual(2.6f, Tuning.I.spikeDownTime, 0.0001f);
            Assert.AreEqual(0.5f, Tuning.I.spikeWarnTime, 0.0001f);
            Assert.AreEqual(1.8f, Tuning.I.spikeUpTime, 0.0001f);
            Assert.AreEqual(0.8f, Tuning.I.spikePhaseStep, 0.0001f);
            Assert.AreEqual(7, Tuning.I.spikeHeightPixels);

            Spikes spikes = MakeStrip(0f);
            Rect standing = StandingOnStrip();

            spikes.Tick(1f);
            Assert.IsFalse(spikes.Hurts(standing, out _), "hidden at 1 s");

            spikes.Tick(1.8f);
            Assert.IsFalse(spikes.Hurts(standing, out _), "warning at 2.8 s");

            spikes.Tick(0.6f);
            Assert.IsTrue(spikes.Hurts(standing, out float centre), "raised at 3.4 s");
            Assert.AreEqual(StripCentre(), centre, 0.02f);

            spikes.Tick(1.6f);
            Assert.IsFalse(spikes.Hurts(standing, out _), "hidden again at 5.0 s (the cycle is 4.9 s)");
        }

        [Test]
        public void RaisedStripHurtsOnlySomeoneStandingOnIt()
        {
            Spikes spikes = MakeStrip(0f);
            spikes.Tick(3.8f);

            Assert.IsTrue(spikes.Hurts(StandingOnStrip(), out _), "standing on the strip");
            Assert.IsFalse(spikes.Hurts(OneTileToTheSide(), out _), "one tile to the side");
            Assert.IsFalse(spikes.Hurts(LevelAbove(), out _), "the level above");

            float left = StripLeft();
            float pixel = Tuning.I.Pixel;
            // One pixel in from the end, so it sits inside the 2 px margin and does not touch the hurt box.
            var endCap = new Rect(left, 0f, pixel, 0.4f);
            Assert.IsFalse(spikes.Hurts(endCap, out _), "the outer 2 px are outside the hurt box");

            var justInside = new Rect(left + pixel * 2f, 0f, pixel, 0.4f);
            Assert.IsTrue(spikes.Hurts(justInside, out _), "just inside the 2 px margin");
        }

        [Test]
        public void LavaStagesBeforeTheBossPlaceValidStrips()
        {
            bool sawGround = false, sawPlatform = false;
            for (int stageInWorld = 1; stageInWorld <= 5; stageInWorld++)
            {
                int stage = (WorldRules.LavaWorld - 1) * Tuning.I.stagesPerWorld + stageInWorld;
                int expected = stageInWorld <= 2 ? 2 : stageInWorld <= 4 ? 3 : 4;
                for (int seed = 0; seed < 10; seed++)
                {
                    StageMap map = StageGenerator.Generate(stage, new System.Random(seed)).map;
                    Assert.AreEqual(expected, map.spikes.Count, $"world 6-{stageInWorld} seed {seed}");
                    for (int i = 0; i < map.spikes.Count; i++)
                    {
                        StageMap.SpikeStrip strip = map.spikes[i];
                        Assert.AreEqual(i * Tuning.I.spikePhaseStep, strip.phase, 0.0001f, $"seed {seed} phase");
                        Assert.IsTrue(strip.width == 2 || strip.width == 3, $"seed {seed} width");
                        AssertStripIsSafe(map, strip, seed);
                        if (strip.row < 0) sawGround = true;
                        else sawPlatform = true;
                    }
                    AssertNoOverlaps(map, seed);
                }
            }
            Assert.IsTrue(sawGround, "some strips sit on the ground");
            Assert.IsTrue(sawPlatform, "some strips sit on a platform");
        }

        [Test]
        public void OtherWorldsAndTheLavaBossHaveNoStrips()
        {
            for (int stage = 1; stage <= Tuning.I.RunLength; stage++)
            {
                if (Run.WorldOf(stage) == WorldRules.LavaWorld && !Run.IsBossStageNumber(stage)) continue;
                for (int seed = 0; seed < 3; seed++)
                {
                    StageMap map = StageGenerator.Generate(stage, new System.Random(seed)).map;
                    Assert.AreEqual(0, map.spikes.Count, $"stage {stage} seed {seed}");
                }
            }
        }

        [Test]
        public void AShortPlatformIsNotCovered()
        {
            var map = StageMap.Flat();
            for (int c = 0; c < 4; c++)
                map.Set(c, 4, new Cell { kind = TileKind.Solid });

            WorldRules.PlaceSpikes(map, 5, new System.Random(1));

            Assert.Greater(map.spikes.Count, 0);
            foreach (var strip in map.spikes)
                Assert.AreNotEqual(4, strip.row, "a 4-tile platform cannot keep 3 tiles free");
        }

        static void AssertStripIsSafe(StageMap map, StageMap.SpikeStrip strip, int seed)
        {
            Assert.GreaterOrEqual(strip.col, 0);
            Assert.LessOrEqual(strip.col + strip.width, StageMap.Cols);
            for (int c = strip.col; c < strip.col + strip.width; c++)
            {
                Assert.IsFalse(ColumnHasLadder(map, c - 1) || ColumnHasLadder(map, c) || ColumnHasLadder(map, c + 1),
                    $"seed {seed} column {c} is on or next to a ladder");
                if (strip.row < 0)
                {
                    Assert.IsTrue(c < 11 || c > 20, $"seed {seed} ground column {c} overlaps the start");
                    Assert.IsFalse(map.IsSolid(c, 0), $"seed {seed} ground column {c} is buried");
                }
                else
                {
                    Assert.IsTrue(map.IsSolid(c, strip.row), $"seed {seed} platform column {c} is not solid");
                    Assert.IsFalse(map.IsSolid(c, strip.row + 1), $"seed {seed} platform column {c} has no air above");
                }
            }

            if (strip.row < 0) return;
            int start = strip.col;
            while (start > 0 && map.IsSolid(start - 1, strip.row)) start--;
            int end = strip.col + strip.width;
            while (end < StageMap.Cols && map.IsSolid(end, strip.row)) end++;
            int covered = 0;
            foreach (var other in map.spikes)
            {
                if (other.row != strip.row) continue;
                for (int c = other.col; c < other.col + other.width; c++)
                    if (c >= start && c < end) covered++;
            }
            Assert.GreaterOrEqual(end - start - covered, 3, $"seed {seed} platform row {strip.row} keeps a path");
        }

        static void AssertNoOverlaps(StageMap map, int seed)
        {
            for (int a = 0; a < map.spikes.Count; a++)
            {
                for (int b = a + 1; b < map.spikes.Count; b++)
                {
                    var s = map.spikes[a];
                    var t = map.spikes[b];
                    if (s.row != t.row) continue;
                    bool overlap = s.col < t.col + t.width && t.col < s.col + s.width;
                    Assert.IsFalse(overlap, $"seed {seed} strips overlap");
                }
            }
        }

        static bool ColumnHasLadder(StageMap map, int c)
        {
            if (c < 0 || c >= StageMap.Cols) return false;
            for (int r = 0; r < StageMap.Rows; r++)
                if (map.Get(c, r).ladder) return true;
            return false;
        }

        Spikes MakeStrip(float phase)
        {
            var map = new StageMap();
            map.spikes.Add(new StageMap.SpikeStrip { col = 4, row = -1, width = 2, phase = phase });
            return Spikes.Create(root.transform, map);
        }

        static float StripLeft() => StageMap.CellRect(4, 0).xMin;
        static float StripCentre() => StripLeft() + 1f;

        static Rect StandingOnStrip() => new Rect(StripCentre() - 0.5f, 0f, 1f, 22f / 15f);

        static Rect OneTileToTheSide() => new Rect(StripLeft() + 2f, 0f, 1f, 22f / 15f);

        static Rect LevelAbove() => new Rect(StripCentre() - 0.5f, 5f, 1f, 22f / 15f);
    }
}
