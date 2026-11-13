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
        public void ReinforcementsStagesSplitTheBudgetIntoAStartAndWaves()
        {
            var row = Tuning.I.Recipe(RecipeId.Reinforcements);
            int seen = 0;
            for (int seed = 1; seed <= 200; seed++)
            {
                var run = new Run(seed);
                int perWorld = 0, world = 1;
                for (int stage = 1; stage <= 36; stage++)
                {
                    if (Run.WorldOf(stage) != world) { world = Run.WorldOf(stage); perWorld = 0; }
                    var plan = StageGenerator.GenerateFor(run, stage);
                    if (plan.recipeName != row.name)
                    {
                        Assert.AreEqual(1f, plan.timeScale, 1e-5f, $"seed {seed} stage {stage} time scale");
                        Assert.AreEqual(0, plan.waves.Count, $"seed {seed} stage {stage} waves");
                        continue;
                    }
                    seen++;
                    perWorld++;
                    Assert.GreaterOrEqual(Run.WorldOf(stage), row.fromWorld, $"seed {seed} stage {stage}");
                    Assert.LessOrEqual(perWorld, 1, $"seed {seed} world {world} draws it more than once");
                    Assert.AreEqual(row.timeScale, plan.timeScale, 1e-5f);
                    Assert.LessOrEqual(plan.waves.Count, row.waves, $"seed {seed} stage {stage}");

                    float start = 0f, wave = 0f;
                    int picks = 0;
                    foreach (var s in plan.spawns)
                    {
                        if (s.kind == BubbleKind.Lucky) continue;
                        start += Cost(s);
                        picks++;
                    }
                    foreach (var list in plan.waves)
                    {
                        Assert.Greater(list.Count, 0, $"seed {seed} stage {stage} has an empty wave");
                        foreach (var s in list)
                        {
                            wave += Cost(s);
                            picks++;
                        }
                    }
                    float all = start + wave;
                    Assert.LessOrEqual(all + plan.rollerSpend, plan.budget + 0.01f, $"seed {seed} stage {stage} overspent");
                    if (picks >= 4 && plan.waves.Count > 0)
                    {
                        // Too small only when no waiting bubble could have joined without pushing the start past 65%.
                        bool couldJoin = false;
                        foreach (var list in plan.waves)
                            foreach (var s in list)
                                if (start + Cost(s) <= all * 0.65f + 0.01f) couldJoin = true;
                        if (couldJoin) Assert.GreaterOrEqual(start, all * 0.35f - 0.01f, $"seed {seed} stage {stage} start too small");
                        if (plan.spawns.FindAll(s => s.kind != BubbleKind.Lucky).Count > 1)
                            Assert.LessOrEqual(start, all * 0.65f + 0.01f, $"seed {seed} stage {stage} start too big");
                    }

                    var again = StageGenerator.GenerateFor(run, stage);
                    Assert.AreEqual(plan.waves.Count, again.waves.Count, $"seed {seed} stage {stage}");
                    for (int w = 0; w < plan.waves.Count; w++)
                    {
                        Assert.AreEqual(plan.waves[w].Count, again.waves[w].Count);
                        for (int i = 0; i < plan.waves[w].Count; i++)
                        {
                            Assert.AreEqual(plan.waves[w][i].kind, again.waves[w][i].kind);
                            Assert.AreEqual(plan.waves[w][i].tier, again.waves[w][i].tier);
                            Assert.AreEqual(plan.waves[w][i].position.x, again.waves[w][i].position.x);
                        }
                    }
                }
            }
            Assert.Greater(seen, 0, "Reinforcements was drawn");
        }

        [Test]
        public void TheReportCountsWaveBubblesAndUsesTheTimeScale()
        {
            var row = Tuning.I.Recipe(RecipeId.Reinforcements);
            bool checkedOne = false;
            for (int seed = 1; seed <= 200 && !checkedOne; seed++)
            {
                var run = new Run(seed);
                for (int stage = 7; stage <= 36 && !checkedOne; stage++)
                {
                    var plan = StageGenerator.GenerateFor(run, stage);
                    if (plan.recipeName != row.name || plan.waves.Count == 0) continue;
                    var report = StageReport.From(stage, plan, run.IsHardWorld(Run.WorldOf(stage)));
                    int inWaves = 0;
                    foreach (var list in plan.waves) inWaves += list.Count;
                    Assert.AreEqual(plan.waves.Count, report.waves);
                    Assert.AreEqual(inWaves, report.waveBubbles);
                    Assert.AreEqual(StageGenerator.StageTime(stage, run.IsHardWorld(Run.WorldOf(stage))) * 1.15f, report.time, 0.01f);
                    Assert.IsTrue(StageReport.Header().Contains("waves\twave bubbles"));
                    checkedOne = true;
                }
            }
            Assert.IsTrue(checkedOne, "found a Reinforcements stage with waves");
        }

        static float Cost(SpawnSpec s) =>
            StageGenerator.BubbleCost(s.kind, s.tier) * (s.ceramic ? Tuning.I.ceramicCostScale : 1f);
    }
}
