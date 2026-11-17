using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>Reinforcements: the wave rule, the warning markers, the generator split and the clear rule.</summary>
    public class WaveTests
    {
        const float Tick = 0.1f;

        static SpawnSpec MakeSpec(int tier = 2, float x = 0f) => new SpawnSpec
        {
            kind = BubbleKind.Gloop, tier = tier, position = new Vector2(x, 0f), direction = 1,
        };

        static List<List<SpawnSpec>> TwoWaves(int perWave = 2, float x = 0f)
        {
            var waves = new List<List<SpawnSpec>>();
            for (int w = 0; w < 2; w++)
            {
                var wave = new List<SpawnSpec>();
                for (int i = 0; i < perWave; i++) wave.Add(MakeSpec(2, x));
                waves.Add(wave);
            }
            return waves;
        }

        [Test]
        public void ANearlyEmptyStageCallsTheFirstWaveAtOnce()
        {
            var director = new WaveDirector(TwoWaves());
            director.Tick(Tick, 2, 0f, (s, x) => { });
            Assert.AreEqual(2, director.Markers.Count);
            Assert.AreEqual(1, director.WavesLeft);
        }

        [Test]
        public void AStageThatStartsWithTwoBubblesDoesNotCallAWaveUntilOneIsPopped()
        {
            var director = new WaveDirector(TwoWaves(), 2);
            for (int i = 0; i < 20; i++) director.Tick(Tick, 2, 0f, (s, x) => { });
            Assert.AreEqual(0, director.Markers.Count, "nothing popped yet");
            director.Tick(Tick, 1, 0f, (s, x) => { });
            Assert.AreEqual(2, director.Markers.Count);
        }

        [Test]
        public void ABusyStageWaitsTwentySecondsForTheFirstWave()
        {
            var director = new WaveDirector(TwoWaves());
            for (int i = 0; i < 39; i++) director.Tick(0.5f, 5, 0f, (s, x) => { }); // 19.5 s
            Assert.AreEqual(0, director.Markers.Count);
            director.Tick(0.5f, 5, 0f, (s, x) => { }); // 20 s
            Assert.AreEqual(2, director.Markers.Count);
        }

        [Test]
        public void AMarkerSpawnsItsBubbleAfterTheWarningTimeAndOnlyOnce()
        {
            var director = new WaveDirector(TwoWaves(3));
            var spawned = new List<float>();
            float elapsed = 0f;
            director.Tick(Tick, 0, 0f, (s, x) => spawned.Add(x));
            Assert.AreEqual(3, director.Markers.Count);
            while (spawned.Count == 0 && elapsed < 3f)
            {
                director.Tick(Tick, 5, 0f, (s, x) => spawned.Add(x));
                elapsed += Tick;
            }
            Assert.AreEqual(Tuning.I.waveWarningTime, elapsed, Tick + 0.001f);
            Assert.AreEqual(3, spawned.Count, "all three markers of the wave run out together");
            director.Tick(Tick, 5, 0f, (s, x) => spawned.Add(x));
            Assert.AreEqual(3, spawned.Count, "nothing spawns twice");
            Assert.AreEqual(0, director.Markers.Count);
        }

        [Test]
        public void TheSecondWaveWaitsForTheFirstMarkersAndCountsFromTheLastArrival()
        {
            var director = new WaveDirector(TwoWaves());
            director.Tick(Tick, 0, 0f, (s, x) => { });
            Assert.AreEqual(1, director.WavesLeft);
            // Enemies are few, but markers are pending: the second wave is not called.
            float t = 0f;
            while (director.Markers.Count > 0 && t < 3f)
            {
                director.Tick(Tick, 0, 0f, (s, x) => { });
                t += Tick;
                if (director.Markers.Count > 0) Assert.AreEqual(1, director.WavesLeft, "not called while markers are pending");
            }
            Assert.AreEqual(0, director.Markers.Count);

            // Now many enemies: only the 20 s count can call it, counted from the arrival.
            var late = new WaveDirector(TwoWaves());
            late.Tick(Tick, 0, 0f, (s, x) => { });
            while (late.Markers.Count > 0) late.Tick(Tick, 9, 0f, (s, x) => { });
            for (int i = 0; i < 39; i++) late.Tick(0.5f, 9, 0f, (s, x) => { });
            Assert.AreEqual(0, late.Markers.Count, "19.5 s after the arrival");
            Assert.AreEqual(1, late.WavesLeft);
            late.Tick(0.5f, 9, 0f, (s, x) => { });
            Assert.AreEqual(0, late.WavesLeft, "20 s after the arrival");
            Assert.AreEqual(2, late.Markers.Count);
        }

        [Test]
        public void MarkersKeepAwayFromWickInsideTheWallsAndApartFromEachOther()
        {
            var tuning = Tuning.I;
            foreach (float wickX in new[] { -15f, 0f, 15f })
                foreach (float wished in new[] { -15f, -2f, 0f, 3f, 15f })
                {
                    var wave = new List<SpawnSpec> { MakeSpec(0, wished), MakeSpec(1, wished), MakeSpec(2, wished), MakeSpec(3, wished) };
                    var director = new WaveDirector(new List<List<SpawnSpec>> { wave });
                    director.Tick(Tick, 0, wickX, (s, x) => { });
                    Assert.AreEqual(4, director.Markers.Count);
                    for (int i = 0; i < director.Markers.Count; i++)
                    {
                        var m = director.Markers[i];
                        float r = tuning.tiers[m.spec.tier].diameter * 0.5f;
                        string what = $"wick {wickX} wished {wished} marker {i}";
                        Assert.GreaterOrEqual(Mathf.Abs(m.x - wickX), tuning.waveSafeDistance - 0.001f, what + " distance from Wick");
                        Assert.GreaterOrEqual(m.x, -tuning.HalfWidth + r - 0.001f, what + " left wall");
                        Assert.LessOrEqual(m.x, tuning.HalfWidth - r + 0.001f, what + " right wall");
                        for (int j = 0; j < i; j++)
                        {
                            var o = director.Markers[j];
                            float rj = tuning.tiers[o.spec.tier].diameter * 0.5f;
                            Assert.GreaterOrEqual(Mathf.Abs(m.x - o.x), Mathf.Max(r, rj) * 2f - 0.001f, what + $" apart from marker {j}");
                        }
                    }
                }
        }

        [Test]
        public void DoneOnlyAfterEveryWaveIsCalledAndEveryMarkerHasSpawned()
        {
            var director = new WaveDirector(TwoWaves());
            Assert.IsFalse(director.Done);
            int alive = 0;
            for (int i = 0; i < 200 && !director.Done; i++)
            {
                director.Tick(Tick, alive, 0f, (s, x) => alive++);
                if (director.Markers.Count == 0 && i % 2 == 1) alive = 0; // the player pops everything that arrived
                if (director.WavesLeft == 0 && director.Markers.Count > 0) Assert.IsFalse(director.Done, "markers still pending");
            }
            Assert.IsTrue(director.Done);
            Assert.IsTrue(new WaveDirector(new List<List<SpawnSpec>>()).Done, "no waves at all is done");
        }

        [Test]
        public void CallNowCallsTheNextWaveOnTheNextTick()
        {
            var director = new WaveDirector(TwoWaves());
            director.Tick(Tick, 9, 0f, (s, x) => { });
            Assert.AreEqual(0, director.Markers.Count);
            director.CallNow();
            director.Tick(Tick, 9, 0f, (s, x) => { });
            Assert.AreEqual(2, director.Markers.Count);
        }

        [Test]
        public void AStageWithWavesIsNotClearedUntilTheWavesAreDone()
        {
            Assert.IsFalse(Game.StageClearedWithWaves(0, 0, false));
            Assert.IsFalse(Game.StageClearedWithWaves(1, 0, true));
            Assert.IsFalse(Game.StageClearedWithWaves(0, 1, true));
            Assert.IsTrue(Game.StageClearedWithWaves(0, 0, true));
        }

        [Test]
        public void TheReportCountsWaveBubblesAndUsesTheTimeScale()
        {
            const int stage = 8;
            var plan = new StagePlan
            {
                map = StageMap.Flat(),
                spawns = new List<SpawnSpec>(),
                waves = new List<List<SpawnSpec>>
                {
                    new List<SpawnSpec> { new SpawnSpec { kind = BubbleKind.Gloop, tier = 3, position = new Vector2(-4f, 8f) } },
                    new List<SpawnSpec> { new SpawnSpec { kind = BubbleKind.Gloop, tier = 3, position = new Vector2(4f, 8f) } },
                },
                timeScale = 1.15f,
            };
            var report = StageReport.From(stage, plan, false);
            Assert.AreEqual(2, report.waves);
            Assert.AreEqual(2, report.waveBubbles);
            Assert.AreEqual(StagePicker.StageTime(stage, false) * 1.15f, report.time, 0.01f);
            Assert.IsTrue(StageReport.Header().Contains("waves\twave bubbles"));
        }
    }
}
