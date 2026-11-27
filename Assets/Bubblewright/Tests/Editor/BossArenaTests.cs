using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>
    /// The arena kit: 5-storey boss arenas, the stage picker's boss arena, hatches opened by phases, the arena
    /// events (break a tile, temporary tiles, floor gaps, a rising hazard, surfaces), balloon pipes and the
    /// validator's boss rules.
    /// </summary>
    public class BossArenaTests
    {
        const float Dt = 1f / 60f;

        static string Blank() => new string('.', StageMap.Cols);

        /// <summary>Rows top first. Every storey is a floor on its bottom row (except the ground) with a ladder shaft at column 16.</summary>
        static string[] TowerRows(int storeys, bool openShaft = true)
        {
            int n = StageMap.StoreyRows * storeys;
            var grid = new char[n][];
            for (int y = 0; y < n; y++) grid[y] = Blank().ToCharArray();
            for (int s = 1; s < storeys; s++)
                for (int c = 0; c < StageMap.Cols; c++) grid[s * StageMap.StoreyRows][c] = '#';
            if (openShaft)
                for (int y = 0; y < n - StageMap.StoreyRows + 1; y++)
                    grid[y][16] = y > 0 && y % StageMap.StoreyRows == 0 ? 'T' : 'H';
            var rows = new string[n];
            for (int y = 0; y < n; y++) rows[n - 1 - y] = new string(grid[y]);
            return rows;
        }

        class TestArena : StageSpec
        {
            public string[] rows;
            public int storeys = 1;
            public bool arena = true;
            public override string Id => "test_arena";
            public override string Name => "Test Arena";
            public override int World => 1;
            public override int Difficulty => 4;
            public override bool IsBossArena => arena;
            public override string Concept => "A test arena.";
            public override int Storeys => storeys;
            public override string[] Rows => rows ?? BlankRows(storeys);
            public override string Ground => SolidGround;
            protected override void Populate(StageBuilder b) => b.Wick(0f);
        }

        class TestBoss : BossDef
        {
            public Vector2? start;
            public string arenaId;
            public override string ArenaId => arenaId;
            public List<Vector2> cores = new List<Vector2>();
            public override int World => 1;
            public override string Name => "TEST";
            public override Vector2 Size => new Vector2(4f, 3f);
            public override Vector2? StartSpot => start;
            public override IReadOnlyList<Vector2> CoreSpots => cores;
            protected override BossPhase[] BuildPhases(bool hard) => new[] { new BossPhase { hp = 3 } };
        }

        // ---------------------------------------------------------------- storeys

        [Test]
        public void AFiveStoreyBossArenaParsesAndValidates()
        {
            var spec = new TestArena { storeys = 5, rows = TowerRows(5) };
            var map = spec.Build().map;
            Assert.AreEqual(5, map.Storeys);
            Assert.AreEqual(70, map.Rows);
            var errors = StageValidator.Validate(spec, false, new TestBoss());
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void AFourStoreyOrdinaryStageStillFailsValidation()
        {
            var spec = new TestArena { storeys = 4, rows = TowerRows(4), arena = false };
            var errors = StageValidator.Validate(spec, false);
            Assert.IsTrue(errors.Any(e => e.Contains("Storeys 4")), string.Join("\n", errors));
        }

        [Test]
        public void ASixStoreyBossArenaFailsValidation()
        {
            var spec = new TestArena { storeys = 6, rows = TowerRows(6) };
            var errors = StageValidator.Validate(spec, false, new TestBoss());
            Assert.IsTrue(errors.Any(e => e.Contains("Storeys 6")), string.Join("\n", errors));
        }

        [Test]
        public void TheLayoutParserAcceptsOneToFiveStoreysAndRejectsSix()
        {
            for (int n = 1; n <= 5; n++)
                Assert.AreEqual(n, StageLayout.Parse(TowerRows(n), new string('=', StageMap.Cols)).Storeys);
            Assert.Throws<System.ArgumentException>(() => StageLayout.Parse(TowerRows(6), new string('=', StageMap.Cols)));
        }

        // ---------------------------------------------------------------- picker

        [Test]
        public void BossArenasAreNeverPickedForOrdinarySlotsAndEveryWorldHasOne()
        {
            Assert.IsFalse(StageSpec.All.Any(s => s.IsBossArena));
            for (int world = 1; world <= 6; world++)
            {
                var arena = StageSpec.BossArenas.FirstOrDefault(s => s.World == world);
                Assert.IsNotNull(arena, $"world {world} has no boss arena");
                Assert.IsTrue(arena.IsBossArena);
                Assert.AreSame(arena, StageSpec.ById(arena.Id));
                foreach (bool hard in new[] { false, true })
                    Assert.IsFalse(StagePicker.PickForWorld(7, world, hard).Any(s => s != null && s.IsBossArena));
            }
        }

        [Test]
        public void TheBossStageBuildsTheArenaOfTheWorldsBossDef()
        {
            var run = new Run(3);
            for (int world = 1; world <= 6; world++)
            {
                int stage = world * Tuning.I.stagesPerWorld;
                var plan = StagePicker.Build(run, stage);
                var arena = StagePicker.BossArenaFor(BossDef.ForWorld(world));
                Assert.AreEqual(arena.Id, plan.stageId, $"world {world}");
                Assert.AreEqual(arena.Name, plan.layoutName);
                Assert.AreEqual(arena.Storeys, plan.map.Storeys);
            }
        }

        [Test]
        public void ABossDefNamesItsArenaAndAMissingOrUnknownIdFallsBackToTheWorldsPlaceholder()
        {
            Assert.AreEqual("w1_boss_arena", StagePicker.BossArenaFor(new TestBoss()).Id, "no ArenaId means the world's placeholder");
            Assert.AreEqual("w1_boss_arena", StagePicker.BossArenaFor(new TestBoss { arenaId = "no_such_arena" }).Id);
            Assert.AreEqual("w1_boss_arena", StagePicker.BossArenaFor(new TestBoss { arenaId = "w1_boss_arena" }).Id, "a named arena is the one used");
            var plan = StagePicker.BossPlan(new TestBoss());
            Assert.AreEqual(1, plan.map.Storeys);
            Assert.AreEqual("w1_boss_arena", plan.stageId);
        }

        // ---------------------------------------------------------------- hatches

        [Test]
        public void AHatchOnABossStageStaysShutUntilItsPhaseEnds()
        {
            // Storey 0 has no bubbles left (settled) but the boss phase has not ended.
            Assert.IsFalse(Game.HatchShouldOpen(true, true, false, 0, 1), "boss arena: settled alone is not enough");
            Assert.IsTrue(Game.HatchShouldOpen(true, false, true, 0, 1), "boss arena: the phase ended");
            Assert.IsFalse(Game.HatchShouldOpen(true, false, true, 1, 1), "Wick is up already: it shuts behind him");
            Assert.IsTrue(Game.HatchShouldOpen(false, true, false, 0, 1), "an ordinary stage opens when the storey is clear");
            Assert.IsFalse(Game.HatchShouldOpen(false, false, true, 0, 1), "an ordinary stage ignores phases");
        }

        [Test]
        public void ThePhaseOpensItsHatchThroughTheArenaEvents()
        {
            var map = new TestArena { storeys = 3, rows = TowerRows(3) }.Build().map;
            var arena = new ArenaEvents(map);
            Assert.IsFalse(arena.HatchUnlocked(0));
            arena.OpenHatch(0);
            Assert.IsTrue(arena.HatchUnlocked(0));
            Assert.IsFalse(arena.HatchUnlocked(1), "each phase opens only its own hatch");
            var phase = new BossPhase { opensHatch = 1 };
            arena.OpenHatchOf(phase);
            Assert.IsTrue(arena.HatchUnlocked(1));
            arena.OpenHatchOf(new BossPhase());
            Assert.IsFalse(arena.HatchUnlocked(2), "a phase with no hatch opens none");
        }

        // ---------------------------------------------------------------- tiles

        [Test]
        public void BossBreakTileBreaksASolidTileAndAShotStillCannot()
        {
            var map = StageMap.Flat();
            map.Set(5, 3, new Cell { kind = TileKind.Solid });
            map.Set(6, 3, new Cell { kind = TileKind.Breakable, hp = 2 });
            var arena = new ArenaEvents(map);

            Assert.IsFalse(map.Damage(5, 3), "a shot cannot break a solid tile");
            Assert.IsTrue(map.IsSolid(5, 3));

            Assert.IsTrue(arena.BreakTile(5, 3));
            Assert.IsFalse(map.IsSolid(5, 3));
            Assert.IsTrue(arena.BreakTile(6, 3), "the boss breaks a breakable block in one call");
            Assert.IsFalse(map.IsSolid(6, 3));
            Assert.IsFalse(arena.BreakTile(5, 3), "an empty cell has nothing to break");
            Assert.IsFalse(arena.BreakTile(-1, 3), "outside the grid is ignored");
        }

        [Test]
        public void AShotStillBreaksAnOrdinaryBreakableBlockInTwoHits()
        {
            var map = StageMap.Flat();
            map.Set(7, 2, new Cell { kind = TileKind.Breakable, hp = 2 });
            Assert.IsFalse(map.Damage(7, 2));
            Assert.IsTrue(map.Damage(7, 2));
            Assert.IsFalse(map.IsSolid(7, 2));
        }

        [Test]
        public void ATempTileDisappearsAfterItsLifeAndOnlyFillsEmptyCells()
        {
            var map = StageMap.Flat();
            map.Set(9, 4, new Cell { kind = TileKind.Solid });
            var arena = new ArenaEvents(map);
            var expired = new List<Vector2Int>();
            arena.OnTempExpire = (c, r) => expired.Add(new Vector2Int(c, r));

            Assert.IsFalse(arena.AddTempTile(9, 4, TileKind.Solid, Surface.Ice, 1f), "a solid tile is not replaced");
            Assert.IsTrue(arena.AddTempTile(10, 4, TileKind.Solid, Surface.Ice, 1f));
            Assert.IsTrue(map.IsSolid(10, 4));
            Assert.AreEqual(Surface.Ice, map.Get(10, 4).surface);
            Assert.AreEqual(1, arena.TempTileCount);

            arena.Tick(0.9f);
            Assert.IsTrue(map.IsSolid(10, 4), "still there before its life is up");
            arena.Tick(0.2f);
            Assert.IsFalse(map.IsSolid(10, 4), "gone after its life");
            Assert.AreEqual(0, arena.TempTileCount);
            CollectionAssert.AreEqual(new[] { new Vector2Int(10, 4) }, expired);
        }

        [Test]
        public void ATempTileThatWasBrokenMeanwhileLeavesNothingToRestore()
        {
            var map = StageMap.Flat();
            var arena = new ArenaEvents(map);
            arena.AddTempTile(3, 2, TileKind.Solid, Surface.Sand, 5f);
            Assert.AreEqual(Surface.Sand, map.Get(3, 2).surface);
            arena.BreakTile(3, 2);
            arena.Tick(6f);
            Assert.IsFalse(map.IsSolid(3, 2));
            Assert.AreEqual(0, arena.TempTileCount);
        }

        [Test]
        public void OpenFloorGapOpensEveryColumnAndSetSurfaceChangesATileOrTheGround()
        {
            var map = StageMap.Flat();
            var arena = new ArenaEvents(map);
            Assert.AreEqual(3, arena.OpenFloorGap(10, 12));
            for (int c = 10; c <= 12; c++) Assert.IsTrue(map.IsGap(c), $"column {c}");
            Assert.IsFalse(map.IsGap(9));
            Assert.IsFalse(map.IsGap(13));
            Assert.AreEqual(0, arena.OpenFloorGap(12, 10), "a reversed range opens nothing");

            map.Set(4, 5, new Cell { kind = TileKind.Solid });
            Assert.IsTrue(arena.SetSurface(4, 5, Surface.Ice));
            Assert.AreEqual(Surface.Ice, map.Get(4, 5).surface);
            Assert.IsFalse(arena.SetSurface(5, 5, Surface.Ice), "an empty cell has no surface");
            Assert.IsTrue(arena.SetSurface(20, -1, Surface.Ice), "row -1 is the ground");
            Assert.AreEqual(Surface.Ice, map.GetGround(20));
        }

        // ---------------------------------------------------------------- rising hazard

        [Test]
        public void TheRisingHazardClimbsAtItsSpeedAndStopsAtItsStopHeight()
        {
            var arena = new ArenaEvents(StageMap.Flat(2));
            Assert.IsFalse(arena.HazardActive);
            arena.StartRisingHazard(0f, 2f, 6f);
            Assert.IsTrue(arena.HazardActive);
            arena.Tick(1f);
            Assert.AreEqual(2f, arena.HazardY, 0.001f);
            for (int i = 0; i < 60 * 10; i++) arena.Tick(Dt);
            Assert.AreEqual(6f, arena.HazardY, 0.001f, "it stops at the stop height");
            Assert.IsTrue(arena.HazardStopped);
            arena.Tick(5f);
            Assert.AreEqual(6f, arena.HazardY, 0.001f);
        }

        [Test]
        public void TheHazardHurtsAnythingBelowItsLine()
        {
            var arena = new ArenaEvents(StageMap.Flat(2));
            Assert.IsFalse(arena.HazardHurts(new Rect(0f, 0f, 1f, 1f)), "no hazard, no hurt");
            arena.StartRisingHazard(2f, 1f, 8f);
            Assert.IsTrue(arena.HazardHurts(new Rect(0f, 1f, 1f, 1.8f)), "his feet are under the line");
            Assert.IsFalse(arena.HazardHurts(new Rect(0f, 2.5f, 1f, 1.8f)), "standing above the line is safe");
        }

        // ---------------------------------------------------------------- pipes

        [Test]
        public void APipeGloopAppearsAfterItsFlash()
        {
            var arena = new ArenaEvents(StageMap.Flat(3));
            var launches = new List<PipeLaunch>();
            arena.OnPipe = launches.Add;

            arena.FirePipe(PipeSpot.TopRight, StageBuilder.TierM, 1);
            Assert.AreEqual(1, arena.PipesFlashing);
            arena.Tick(Tuning.I.pipeFlashTime - 0.05f);
            Assert.IsEmpty(launches, "still flashing");
            Assert.AreEqual(1, arena.PipesFlashing);
            arena.Tick(0.1f);
            Assert.AreEqual(1, launches.Count);
            Assert.AreEqual(0, arena.PipesFlashing);
            var l = launches[0];
            Assert.AreEqual(PipeSpot.TopRight, l.spot);
            Assert.AreEqual(StageBuilder.TierM, l.tier);
            Assert.AreEqual(-1, l.dir, "the right pipe throws the Gloop toward the middle");
            Assert.Greater(l.position.x, 0f);
            Assert.Greater(l.position.y, 1f * StageMap.StoreyRows, "the pipe sits at the top of storey 1 (the second one)");
            Assert.Less(l.position.y, 2f * StageMap.StoreyRows);
            Assert.Greater(l.position.y, 2f * StageMap.StoreyRows - 3f, "close under its ceiling");
        }

        [Test]
        public void ThePipeSpotsAreLeftRightAndMiddle()
        {
            Assert.Less(ArenaEvents.PipeX(PipeSpot.TopLeft), 0f);
            Assert.Greater(ArenaEvents.PipeX(PipeSpot.TopRight), 0f);
            Assert.AreEqual(0f, ArenaEvents.PipeX(PipeSpot.TopMiddle), 0.001f);
            Assert.AreEqual(1, ArenaEvents.PipeDir(PipeSpot.TopLeft));
            Assert.AreEqual(-1, ArenaEvents.PipeDir(PipeSpot.TopRight));
        }

        // ---------------------------------------------------------------- validator boss rules

        [Test]
        public void ABossThatStartsInsideASolidTileOrOutsideTheArenaIsRejected()
        {
            var spec = new TestArena();
            var inside = StageValidator.Validate(spec, false, new TestBoss { start = new Vector2(0f, 8f) });
            Assert.IsEmpty(inside, string.Join("\n", inside));
            var outside = StageValidator.Validate(spec, false, new TestBoss { start = new Vector2(0f, 40f) });
            Assert.IsTrue(outside.Any(e => e.Contains("start")), string.Join("\n", outside));
            var wall = StageValidator.Validate(spec, false, new TestBoss { start = new Vector2(-15f, 8f) });
            Assert.IsTrue(wall.Any(e => e.Contains("start")), string.Join("\n", wall));
        }

        [Test]
        public void ACoreWickCannotReachIsRejected()
        {
            var spec = new TestArena();
            var rows = TowerRows(1, false);
            var roof = rows[rows.Length - 1 - 8].ToCharArray();
            for (int c = 24; c <= 26; c++) roof[c] = '#'; // a roof over columns 24 to 26 that Wick cannot climb onto
            rows[rows.Length - 1 - 8] = new string(roof);
            spec.rows = rows;
            float x = StageMap.CenterX(25);
            var reachable = StageValidator.Validate(spec, false, new TestBoss { start = new Vector2(0f, 8f), cores = { new Vector2(x, 5f) } });
            Assert.IsEmpty(reachable, string.Join("\n", reachable));
            var tooHigh = StageValidator.Validate(spec, false, new TestBoss { start = new Vector2(0f, 8f), cores = { new Vector2(x, 10f) } });
            Assert.IsTrue(tooHigh.Any(e => e.Contains("core")), string.Join("\n", tooHigh));
        }

        [Test]
        public void AHatchNoOneCanReachIsRejectedOnABossArena()
        {
            var rows = TowerRows(2);
            // Fill the foot of the shaft: the ladder starts four rows up, out of Wick's jump.
            for (int y = 0; y <= 3; y++)
            {
                var chars = rows[rows.Length - 1 - y].ToCharArray();
                chars[16] = '#';
                rows[rows.Length - 1 - y] = new string(chars);
            }
            var spec = new TestArena { storeys = 2, rows = rows };
            var errors = StageValidator.Validate(spec, false, new TestBoss());
            Assert.IsTrue(errors.Any(e => e.Contains("hatch")), string.Join("\n", errors));
        }

        public static IEnumerable<TestCaseData> ArenaCases()
        {
            foreach (var arena in StageSpec.BossArenas)
                yield return new TestCaseData(arena.Id).SetName(arena.Id);
        }

        [TestCaseSource(nameof(ArenaCases))]
        public void EveryDiscoveredBossArenaPassesValidation(string id)
        {
            var errors = StageValidator.Validate(StageSpec.ById(id), false);
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }
    }
}
