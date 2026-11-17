using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    public class StageValidatorTests
    {
        [Test]
        public void MirrorFlipsGridGroundSpawnsWallClingersAndSpikesExact()
        {
            var plan = new MirrorProbe().Build(true);
            Assert.AreEqual(-4f, plan.wickPosition.x, 0.0001f);
            Assert.IsTrue(plan.map.IsSolid(31, 13), "the leading # moves to the right end of the top row");
            Assert.IsFalse(plan.map.IsSolid(0, 13));
            Assert.AreEqual(Surface.Ice, plan.map.GetGround(31));
            Assert.AreEqual(Surface.Normal, plan.map.GetGround(0));

            SpawnSpec bubble = plan.spawns.Find(s => s.kind == BubbleKind.Gloop);
            SpawnSpec clinger = plan.spawns.Find(s => s.kind == BubbleKind.Clinger);
            Assert.AreEqual(-3f, bubble.position.x, 0.0001f);
            Assert.AreEqual(-1, bubble.direction);
            Assert.AreEqual(-6f, clinger.position.x, 0.0001f);
            Assert.AreEqual(-1, clinger.direction);
            Assert.AreEqual(ClingerMount.RightWall, clinger.mount);
            Assert.AreEqual(1, plan.rollers.Count);
            Assert.AreEqual(-5f, plan.rollers[0].x, 0.0001f);
            Assert.AreEqual(1, plan.rollers[0].direction);
            Assert.AreEqual(1, plan.map.spikes.Count);
            Assert.AreEqual(25, plan.map.spikes[0].col);
            Assert.AreEqual(3, plan.map.spikes[0].width);
        }

        [Test]
        public void ValidatorCatchesUnlockViolationsEarlyHitsTrapPitsAndOverBreakSoftLocks()
        {
            var breakable = StageValidator.Validate(new BreakableOnIntro(), false);
            AssertHas(breakable, "1-1");
            AssertHas(breakable, "breakable");

            var early = StageValidator.Validate(new EarlyHit(), false);
            AssertHas(early, "1.2");

            var sand = StageValidator.Validate(new SandGap(), false);
            AssertHas(sand, "sand");
            AssertHas(sand, "gap");

            var bridge = StageValidator.Validate(new BreakableBridge(), false);
            AssertHas(bridge, "strand");
        }

        [Test]
        public void GroundGapsAreCappedAtTwoColumnsAndOneOnSand()
        {
            Assert.AreEqual(2, StageValidator.MaxGapColumns);
            Assert.AreEqual(1, StageValidator.MaxSandGapColumns);
            AssertHas(StageValidator.Validate(new WideGap(), false), "wider than a jump allows (2)");
            AssertHas(StageValidator.Validate(new TwoColumnSandGap(), false), "wider than sand allows (1)");
        }

        [Test]
        public void ValidatorRejectsHazardsOutsideTheirWorldAuthoredLuckyAndUnshootablePockets()
        {
            AssertHas(StageValidator.Validate(new IceInLava(), false), "Ice");
            AssertHas(StageValidator.Validate(new IceInLava(), false), "World 4");
            AssertHas(StageValidator.Validate(new SandInWinter(), false), "Sand");
            AssertHas(StageValidator.Validate(new SandInWinter(), false), "World 3");
            AssertHas(StageValidator.Validate(new SpikesInCatacombs(), false), "spike");
            AssertHas(StageValidator.Validate(new SpikesInCatacombs(), false), "World 6");
            AssertHas(StageValidator.Validate(new AuthoredLucky(), false), "Lucky");
            AssertHas(StageValidator.Validate(new TwoGuests(), false), "guest");
            AssertHas(StageValidator.Validate(new SealedPocket(), false), "pocket");
        }

        public static IEnumerable Cases()
        {
            foreach (var stage in StageSpec.All)
            {
                yield return new TestCaseData(stage.Id, false).SetName(stage.Id);
                if (stage.CanMirror)
                    yield return new TestCaseData(stage.Id, true).SetName(stage.Id + " mirror");
            }
        }

        [TestCaseSource(nameof(Cases))]
        public void EveryDiscoveredStageAndMirrorPassesValidation(string id, bool mirror)
        {
            var errors = StageValidator.Validate(StageSpec.ById(id), mirror);
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void World1OpensOnThreeDifferentStages()
        {
            var picked = StagePicker.PickForWorld(1, 1, false);
            var opening = picked.Take(3).Select(s => s.Id).ToArray();
            Assert.AreEqual(3, opening.Distinct().Count(), "the first three stages were " + string.Join(", ", opening));
            Assert.AreEqual("w1_first_pop", opening[0]);
            Assert.AreEqual(1, picked[0].MaxStageInWorld);
        }

        [Test]
        public void World1HasOneIntroFiveMediumAndSixHardStages()
        {
            var world = StageSpec.All.Where(s => s.World == 1).ToList();
            Assert.AreEqual(12, world.Count, string.Join(", ", world.Select(s => s.Id)));
            Assert.AreEqual(1, world.Count(s => s.Difficulty == 1 && s.MaxStageInWorld == 1));
            Assert.AreEqual(5, world.Count(s => s.Difficulty == 2 && s.MinStageInWorld >= 2));
            Assert.AreEqual(6, world.Count(s => s.Difficulty == 3 && s.MinStageInWorld >= 3));
            Assert.IsNotNull(StageSpec.ById("w1_slotted_bunker"));
        }

        [Test]
        public void ValidatorLetsMossJumpALevelGapAndWallJumpUpAOneColumnWell()
        {
            // The far Gloop is only reachable across a level two-column ground gap; the penned one only up a
            // one-column well with no ladder. Both are base moves for Moss.
            foreach (bool mirror in new[] { false, true })
            {
                var errors = StageValidator.Validate(new GapAndWell(), mirror);
                Assert.IsEmpty(errors, string.Join("\n", errors));
            }
        }

        [Test]
        public void StagePickerNeverRepeatsInAWorldAndMatchesCalmAndHardCurves()
        {
            for (int seed = 1; seed <= 100; seed++)
                foreach (bool hard in new[] { false, true })
                    for (int world = 1; world <= 6; world++)
                    {
                        var picked = StagePicker.PickForWorld(seed, world, hard);
                        var again = StagePicker.PickForWorld(seed, world, hard);
                        Assert.AreEqual(5, picked.Length);
                        for (int i = 0; i < picked.Length; i++)
                        {
                            Assert.NotNull(picked[i], $"seed {seed} world {world} slot {i + 1}");
                            Assert.AreEqual(picked[i].Id, again[i].Id);
                            Assert.AreEqual(world, picked[i].World);
                        }
                        if (world >= 2) Assert.AreEqual(1, picked[0].Difficulty, $"seed {seed} world {world} slot 1");
                        for (int i = 1; i < 4; i++)
                            Assert.LessOrEqual(picked[i].Difficulty, picked[i + 1].Difficulty, $"seed {seed} world {world} slot {i + 1}");
                        int pool = StageSpec.All.Count(s => s.World == world);
                        if (pool >= 5)
                            Assert.AreEqual(5, picked.Select(s => s.Id).Distinct().Count(), $"seed {seed} world {world}");
                    }
        }

        [Test]
        public void LuckyReplacesOnePlainBubbleAtRandomAndNeverASpecialOne()
        {
            float saved = Tuning.I.luckyChance;
            try
            {
                Tuning.I.luckyChance = 1f;
                var seen = new HashSet<int>();
                for (int seed = 1; seed <= 40; seed++)
                {
                    var plan = LuckyPlan();
                    StagePicker.ApplyLucky(plan, new Run(seed) { Stage = 4 }, 4);
                    int luckies = 0;
                    int which = -1;
                    for (int i = 0; i < plan.spawns.Count; i++)
                        if (plan.spawns[i].kind == BubbleKind.Lucky) { luckies++; which = i; }
                    Assert.AreEqual(1, luckies, $"seed {seed}");
                    Assert.IsTrue(which == 0 || which == 1, "only a plain Gloop is replaced");
                    Assert.AreEqual(BubbleKind.Trigloop, plan.spawns[2].kind);
                    Assert.IsTrue(plan.spawns[2].pogo);
                    Assert.AreEqual(BubbleKind.Lead, plan.spawns[3].kind);
                    Assert.IsTrue(plan.spawns[3].ceramic);
                    Assert.AreEqual(BubbleKind.Clinger, plan.spawns[4].kind);
                    Assert.AreEqual(1, plan.spawns[which].tier);
                    Assert.AreEqual(which == 0 ? -4f : 4f, plan.spawns[which].position.x, 0.0001f);
                    seen.Add(which);
                }
                Assert.IsTrue(seen.Contains(0) && seen.Contains(1), "both plain bubbles are chosen across seeds");

                Tuning.I.luckyChance = 0f;
                var none = LuckyPlan();
                StagePicker.ApplyLucky(none, new Run(3) { Stage = 4 }, 4);
                Assert.IsFalse(none.spawns.Exists(s => s.kind == BubbleKind.Lucky));

                Tuning.I.luckyChance = 1f;
                var first = LuckyPlan();
                StagePicker.ApplyLucky(first, new Run(3) { Stage = 1 }, 1);
                Assert.IsFalse(first.spawns.Exists(s => s.kind == BubbleKind.Lucky), "stage 1 never rolls a Lucky");

                var specials = new StagePlan { spawns = new List<SpawnSpec> { new SpawnSpec { kind = BubbleKind.Clinger, tier = 1 } } };
                StagePicker.ApplyLucky(specials, new Run(3) { Stage = 4 }, 4);
                Assert.AreEqual(BubbleKind.Clinger, specials.spawns[0].kind);
            }
            finally
            {
                Tuning.I.luckyChance = saved;
            }
        }

        [Test]
        public void SavedRunKeepsItsStagesWhenTheLibraryGrows()
        {
            const int seed = 12345;
            var run = new Run(seed) { Stage = 1 };
            StagePicker.Build(run, 1);
            var save = run.ToSave();
            Assert.AreEqual(2, save.version);
            Assert.AreEqual("w1_first_pop", save.stageIds[0]);

            var plain = StageSpec.ById("w1_first_pop").Build(false);
            var mirrored = StageSpec.ById("w1_first_pop").Build(true);
            Assert.Greater(Mathf.Abs(plain.spawns[0].position.x - mirrored.spawns[0].position.x), 0.01f);
            bool freshMirror = !Mathf.Approximately(StagePicker.Build(new Run(seed) { Stage = 1 }, 1).spawns[0].position.x, plain.spawns[0].position.x);
            save.stageMirrors[0] = !freshMirror;
            PlayerPrefs.SetString(SaveGame.Key, JsonUtility.ToJson(save));
            var kept = StagePicker.Build(SaveGame.Load(), 1);
            float want = save.stageMirrors[0] ? mirrored.spawns[0].position.x : plain.spawns[0].position.x;
            Assert.AreEqual(want, kept.spawns[0].position.x, 0.001f, "a saved mirror wins over a fresh roll");

            var probe = StagePicker.Build(new Run(seed) { Stage = 3 }, 3);
            bool probeMirror = !Mathf.Approximately(probe.spawns[0].position.x, plain.spawns[0].position.x);
            save.stageIds[2] = "not_a_real_stage";
            save.stageMirrors[2] = !probeMirror;
            PlayerPrefs.SetString(SaveGame.Key, JsonUtility.ToJson(save));
            var fallen = StagePicker.Build(SaveGame.Load(), 3);
            Assert.AreEqual(probe.stageId, fallen.stageId);
            Assert.AreEqual(probe.spawns[0].position.x, fallen.spawns[0].position.x, 0.001f, "an unknown id takes a fresh pick");

            var old = run.ToSave();
            old.version = 1;
            old.stageIds = new[] { "not_a_real_stage" };
            PlayerPrefs.SetString(SaveGame.Key, JsonUtility.ToJson(old));
            var loaded = SaveGame.Load();
            Assert.IsNotNull(loaded);
            Assert.AreEqual(seed, loaded.Seed);
            Assert.AreEqual("w1_first_pop", StagePicker.Build(loaded, 1).stageId);
            PlayerPrefs.DeleteKey(SaveGame.Key);
        }

        static void AssertHas(List<string> errors, string needle)
        {
            Assert.IsTrue(errors.Exists(e => e.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0),
                $"missing '{needle}' in:\n" + string.Join("\n", errors));
        }

        static StagePlan LuckyPlan() => new StagePlan
        {
            spawns = new List<SpawnSpec>
            {
                new SpawnSpec { kind = BubbleKind.Gloop, tier = 1, position = new Vector2(-4f, 8f), direction = -1 },
                new SpawnSpec { kind = BubbleKind.Gloop, tier = 1, position = new Vector2(4f, 8f), direction = 1 },
                new SpawnSpec { kind = BubbleKind.Trigloop, tier = 2, position = new Vector2(-8f, 8f), direction = -1, pogo = true },
                new SpawnSpec { kind = BubbleKind.Lead, tier = 2, position = new Vector2(8f, 8f), direction = 1, ceramic = true },
                new SpawnSpec { kind = BubbleKind.Clinger, tier = 1, position = new Vector2(0f, 11f), direction = 1, patient = true },
            },
        };

        static void Set(string[] rows, int col, int row, char ch)
        {
            int index = StageMap.StoreyRows - 1 - row;
            var chars = rows[index].ToCharArray();
            chars[col] = ch;
            rows[index] = new string(chars);
        }

        sealed class MirrorProbe : StageSpec
        {
            public override string Id => "mirror_probe";
            public override string Name => "Mirror Probe";
            public override int World => 4;
            public override int Difficulty => 1;
            public override string Concept => "A probe for the mirror.";
            public override string[] Rows
            {
                get
                {
                    var rows = BlankRows();
                    Set(rows, 0, 13, '#');
                    return rows;
                }
            }
            public override string Ground => "I" + new string('=', 31);
            protected override void Populate(StageBuilder b)
            {
                b.Wick(4f);
                b.Bubble(BubbleKind.Gloop, StageBuilder.TierM, 3f, 8f, 1);
                b.Clinger(StageBuilder.TierM, 6f, 8f, ClingerMount.LeftWall, 1);
                b.Roller(5f, 0f, -1, StageBuilder.TierS);
                b.Spike(4, 3);
            }
        }

        sealed class BreakableOnIntro : StageSpec
        {
            public override string Id => "invalid_breakable_intro";
            public override string Name => "Invalid Breakable Intro";
            public override int World => 1;
            public override int Difficulty => 1;
            public override int MaxStageInWorld => 1;
            public override string Concept => "A breakable tile on the first stage.";
            public override string[] Rows
            {
                get
                {
                    var rows = BlankRows();
                    Set(rows, 0, 12, 'B');
                    return rows;
                }
            }
            public override string Ground => SolidGround;
            protected override void Populate(StageBuilder b)
            {
                b.Wick(0f);
                b.Bubble(BubbleKind.Gloop, StageBuilder.TierL, -10f, 9f, -1);
                b.Bubble(BubbleKind.Gloop, StageBuilder.TierL, 6f, 9f, 1);
            }
        }

        sealed class EarlyHit : StageSpec
        {
            public override string Id => "invalid_early_hit";
            public override string Name => "Invalid Early Hit";
            public override int World => 1;
            public override int Difficulty => 1;
            public override int MaxStageInWorld => 1;
            public override string Concept => "A bubble starts on top of Wick.";
            public override string[] Rows => BlankRows();
            public override string Ground => SolidGround;
            protected override void Populate(StageBuilder b)
            {
                b.Wick(0f);
                b.Bubble(BubbleKind.Gloop, StageBuilder.TierS, 0f, 1f, 1);
            }
        }

        sealed class SandGap : StageSpec
        {
            public override string Id => "invalid_sand_gap";
            public override string Name => "Invalid Sand Gap";
            public override int World => 3;
            public override int Difficulty => 1;
            public override int MaxStageInWorld => 1;
            public override string Concept => "Three columns of gap on sand.";
            public override string[] Rows => BlankRows();
            public override string Ground
            {
                get
                {
                    var chars = SolidGround.ToCharArray();
                    chars[10] = chars[11] = chars[12] = '_';
                    return new string(chars);
                }
            }
            protected override void Populate(StageBuilder b)
            {
                b.Wick(-12f);
                b.Bubble(BubbleKind.Sinker, StageBuilder.TierL, -10f, 8f, 1);
                b.Bubble(BubbleKind.Sinker, StageBuilder.TierL, -7f, 8f, 1);
            }
        }

        sealed class WideGap : StageSpec
        {
            public override string Id => "invalid_wide_gap";
            public override string Name => "Invalid Wide Gap";
            public override int World => 1;
            public override int Difficulty => 2;
            public override int MinStageInWorld => 2;
            public override string Concept => "Three columns of gap on normal ground.";
            public override string[] Rows => BlankRows();
            public override string Ground => "==========___===================";
            protected override void Populate(StageBuilder b)
            {
                b.WickAtCol(3);
                b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 20, 9f, 1);
            }
        }

        sealed class TwoColumnSandGap : StageSpec
        {
            public override string Id => "invalid_two_column_sand_gap";
            public override string Name => "Invalid Two Column Sand Gap";
            public override int World => 3;
            public override int Difficulty => 2;
            public override int MinStageInWorld => 2;
            public override string Concept => "Two columns of gap on sand.";
            public override string[] Rows => BlankRows();
            public override string Ground => "==========__====================";
            protected override void Populate(StageBuilder b)
            {
                b.WickAtCol(3);
                b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 20, 8f, 1);
            }
        }

        sealed class BreakableBridge : StageSpec
        {
            public override string Id => "invalid_breakable_bridge";
            public override string Name => "Invalid Breakable Bridge";
            public override int World => 2;
            public override int Difficulty => 2;
            public override int MinStageInWorld => 2;
            public override string Concept => "A breakable stair is the only way into a solid room.";
            public override string[] Rows
            {
                get
                {
                    var rows = BlankRows();
                    Set(rows, 10, 0, 'B');
                    Set(rows, 11, 1, 'B');
                    Set(rows, 12, 2, 'B');
                    for (int c = 13; c <= 20; c++)
                    {
                        Set(rows, c, 3, '#');
                        Set(rows, c, 5, '#');
                    }
                    Set(rows, 20, 4, '#');
                    return rows;
                }
            }
            public override string Ground => SolidGround;
            protected override void Populate(StageBuilder b)
            {
                b.Wick(StageMap.CenterX(2));
                b.Bubble(BubbleKind.Gloop, StageBuilder.TierS, StageMap.CenterX(16), 4.5f, 1);
            }
        }

        sealed class IceInLava : StageSpec
        {
            public override string Id => "invalid_ice_in_lava";
            public override string Name => "Invalid Ice In Lava";
            public override int World => 6;
            public override int Difficulty => 1;
            public override int MaxStageInWorld => 1;
            public override string Concept => "Ice does not belong in the lava world.";
            public override string[] Rows
            {
                get
                {
                    var rows = BlankRows();
                    Set(rows, 0, 12, 'I');
                    return rows;
                }
            }
            public override string Ground => SolidGround;
            protected override void Populate(StageBuilder b)
            {
                b.Wick(0f);
                b.Bubble(BubbleKind.Scorch, StageBuilder.TierL, -10f, 9f, -1);
                b.Bubble(BubbleKind.Scorch, StageBuilder.TierL, 10f, 9f, 1);
            }
        }

        sealed class SandInWinter : StageSpec
        {
            public override string Id => "invalid_sand_in_winter";
            public override string Name => "Invalid Sand In Winter";
            public override int World => 4;
            public override int Difficulty => 1;
            public override int MaxStageInWorld => 1;
            public override string Concept => "Sand does not belong in the winter world.";
            public override string[] Rows
            {
                get
                {
                    var rows = BlankRows();
                    Set(rows, 0, 12, 'S');
                    return rows;
                }
            }
            public override string Ground => SolidGround;
            protected override void Populate(StageBuilder b)
            {
                b.Wick(0f);
                b.Bubble(BubbleKind.Frost, StageBuilder.TierL, -10f, 9f, -1);
                b.Bubble(BubbleKind.Frost, StageBuilder.TierL, 10f, 9f, 1);
            }
        }

        sealed class SpikesInCatacombs : StageSpec
        {
            public override string Id => "invalid_spikes_in_catacombs";
            public override string Name => "Invalid Spikes In Catacombs";
            public override int World => 5;
            public override int Difficulty => 1;
            public override int MaxStageInWorld => 1;
            public override string Concept => "Spike strips do not belong in the catacombs.";
            public override string[] Rows => BlankRows();
            public override string Ground => SolidGround;
            protected override void Populate(StageBuilder b)
            {
                b.Wick(0f);
                b.Bubble(BubbleKind.Ghost, StageBuilder.TierL, -10f, 9f, -1);
                b.Bubble(BubbleKind.Ghost, StageBuilder.TierL, 10f, 9f, 1);
                b.Spike(2, 2);
            }
        }

        sealed class AuthoredLucky : StageSpec
        {
            public override string Id => "invalid_authored_lucky";
            public override string Name => "Invalid Authored Lucky";
            public override int World => 2;
            public override int Difficulty => 2;
            public override int MinStageInWorld => 2;
            public override string Concept => "A Lucky placed by the author.";
            public override string[] Rows => BlankRows();
            public override string Ground => SolidGround;
            protected override void Populate(StageBuilder b)
            {
                b.Wick(0f);
                b.Bubble(BubbleKind.Lucky, StageBuilder.TierL, -8f, 9f, -1);
                b.Bubble(BubbleKind.Gloop, StageBuilder.TierL, 8f, 9f, 1);
            }
        }

        sealed class TwoGuests : StageSpec
        {
            public override string Id => "invalid_two_guests";
            public override string Name => "Invalid Two Guests";
            public override int World => 5;
            public override int Difficulty => 2;
            public override int MinStageInWorld => 2;
            public override string Concept => "A Clinger and a Sinker visiting together.";
            public override string[] Rows => BlankRows();
            public override string Ground => SolidGround;
            protected override void Populate(StageBuilder b)
            {
                b.Wick(0f);
                b.Clinger(StageBuilder.TierL, -8f, 11f);
                b.Bubble(BubbleKind.Sinker, StageBuilder.TierL, 8f, 8f, 1);
            }
        }

        sealed class SealedPocket : StageSpec
        {
            public override string Id => "invalid_sealed_pocket";
            public override string Name => "Invalid Sealed Pocket";
            public override int World => 1;
            public override int Difficulty => 2;
            public override int MinStageInWorld => 2;
            public override string Concept => "A small bubble shut inside a solid room.";
            public override string[] Rows
            {
                get
                {
                    var rows = BlankRows();
                    for (int c = 2; c <= 5; c++)
                    {
                        Set(rows, c, 2, '#');
                        Set(rows, c, 5, '#');
                    }
                    for (int r = 3; r <= 4; r++)
                    {
                        Set(rows, 2, r, '#');
                        Set(rows, 5, r, '#');
                    }
                    return rows;
                }
            }
            public override string Ground => SolidGround;
            protected override void Populate(StageBuilder b)
            {
                b.Wick(0f);
                b.Bubble(BubbleKind.Gloop, StageBuilder.TierS, -12f, 4f, 1);
                b.Bubble(BubbleKind.Gloop, StageBuilder.TierL, 8f, 9f, 1);
            }
        }

        sealed class GapAndWell : StageSpec
        {
            public override string Id => "valid_gap_and_well";
            public override string Name => "Valid Gap And Well";
            public override int World => 1;
            public override int Difficulty => 2;
            public override int MinStageInWorld => 2;
            public override string Concept => "A level gap to jump and a well to wall-jump up.";
            public override string[] Rows
            {
                get
                {
                    var rows = BlankRows();
                    for (int r = 1; r <= 10; r++) Set(rows, 25, r, '#');
                    for (int c = 27; c <= 31; c++)
                        for (int r = 0; r <= 6; r++)
                            Set(rows, c, r, '#');
                    return rows;
                }
            }
            public override string Ground => "==========__====================";
            protected override void Populate(StageBuilder b)
            {
                b.WickAtCol(3);
                b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 19, 9f, 1);
                b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 29, 10f, 1);
            }
        }
    }
}
