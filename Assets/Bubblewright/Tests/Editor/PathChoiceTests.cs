using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>The left or right choice after a boss: which door is hard, which stages it follows, what it sets, and its text and art.</summary>
    public class PathChoiceTests
    {
        static Run RunAt(int seed, int stage) => new Run(seed) { Stage = stage };

        /// <summary>Opaque and not the ink outline. Out-of-range reads are clear, so they are not colour.</summary>
        static bool Colour(Color32 p) =>
            p.a > 0 && (p.r != Palette.Ink.r || p.g != Palette.Ink.g || p.b != Palette.Ink.b);

        [Test]
        public void SameSeedAndStageAlwaysGiveTheSameDoors()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                var a = RunAt(seed, 6).RollRoad();
                var b = RunAt(seed, 6).RollRoad();
                Assert.AreEqual(a.hardIsRight, b.hardIsRight, $"seed {seed}");
                Assert.AreSame(a.debuff, b.debuff, $"seed {seed}");
            }
        }

        [Test]
        public void BothSidesAndEveryDebuffComeUp()
        {
            int right = 0, left = 0;
            var seen = new HashSet<Debuff>();
            for (int seed = 1; seed <= 40; seed++)
            {
                var road = RunAt(seed, 6).RollRoad();
                if (road.hardIsRight) right++; else left++;
                seen.Add(road.debuff);
            }
            Assert.GreaterOrEqual(right, 10, "right door is hard too rarely");
            Assert.GreaterOrEqual(left, 10, "left door is hard too rarely");
            foreach (var d in Debuff.All) Assert.IsTrue(seen.Contains(d), $"{d.Name} never came up in 40 seeds");
        }

        [Test]
        public void ADebuffDoesNotRepeatInTheNextWorld()
        {
            foreach (var finished in Debuff.All)
            {
                for (int seed = 1; seed <= 40; seed++)
                {
                    var run = RunAt(seed, 12);
                    run.SetHard(2, finished);
                    Assert.AreNotSame(finished, run.RollRoad().debuff, $"seed {seed}, finished {finished.Name}");
                }
            }
        }

        [Test]
        public void ThePathFollowsEveryBossButTheLastOne()
        {
            var expected = new[] { 6, 12, 18, 24, 30 };
            for (int stage = 1; stage <= Tuning.I.RunLength; stage++)
                Assert.AreEqual(expected.Contains(stage), Run.PathFollows(stage), $"stage {stage}");
            Assert.IsFalse(Run.PathFollows(36), "no choice after the final boss");
            Assert.IsFalse(Run.PathFollows(1), "no choice before world 1");
        }

        [Test]
        public void ChoosingHardMakesOnlyTheNextWorldHard()
        {
            var run = RunAt(7, 12); // the boss of world 2 is done
            var debuff = Debuff.All[1];
            run.ChooseRoad(debuff);
            Assert.IsTrue(run.hardWorld[3]);
            Assert.AreEqual(debuff.Name, run.worldDebuff[3]);
            Assert.IsFalse(run.hardWorld[2], "the world just finished is untouched");
            Assert.IsFalse(run.hardWorld[4], "the world after is untouched");
            Assert.IsNull(run.worldDebuff[2]);
            Assert.IsNull(run.worldDebuff[4]);

            run.Stage = 13;
            Assert.IsTrue(run.IsHard);
            Assert.AreSame(debuff, run.CurrentDebuff);
        }

        [Test]
        public void ChoosingCalmChangesNothing()
        {
            var run = RunAt(7, 12);
            run.ChooseRoad(null);
            for (int w = 1; w < run.hardWorld.Length; w++) Assert.IsFalse(run.hardWorld[w], $"world {w}");
        }

        [Test]
        public void RoadSummaryListsThePlayedWorlds()
        {
            var run = RunAt(3, 1);
            Assert.AreEqual("C", run.RoadSummary());
            run.ChooseRoad(null);          // calm road into world 2
            run.Stage = 7;
            run.ChooseRoad(Debuff.All[0]); // hard road into world 3
            run.Stage = 13;
            Assert.AreEqual("C C H", run.RoadSummary()); // world 1 calm, world 2 calm, world 3 hard
            run.Stage = 1;
            run.SetHard(1, Debuff.All[2]);
            Assert.AreEqual("H", run.RoadSummary());
        }

        [Test]
        public void CardNumbersComeFromTuning()
        {
            var t = Tuning.I;
            Assert.AreEqual($"{Mathf.RoundToInt((t.hardBudgetScale - 1f) * 100f)}% more bubbles, {Mathf.RoundToInt((1f - t.hardTimeScale) * 100f)}% less time", Run.HardRoadNumbers());
            StringAssert.Contains($"pick {t.hardRewardPicks} of {t.hardRewardCards}", Run.HardRoadReward());
            StringAssert.StartsWith("Reward:", Run.HardRoadReward());
        }

        [Test]
        public void PathTextIsReadable()
        {
            var names = UiText.TextPairs().Select(p => p.name).ToList();
            foreach (var wanted in new[]
            {
                "Hard road title on a card", "Hard road title on a lit card",
                "Reward line on a card", "Reward line on a lit card", "Road hint in the ground strip",
            })
                Assert.IsTrue(names.Any(n => n.StartsWith(wanted)), $"{wanted} is not checked in UiText.TextPairs");

            foreach (var (name, text, background) in UiText.TextPairs())
            {
                if (!(name.StartsWith("Hard road") || name.StartsWith("Reward line") || name.StartsWith("Road hint"))) continue;
                Assert.GreaterOrEqual(UiText.Contrast(text, background), UiText.MinTextContrast, name);
            }
        }

        [Test]
        public void DoorsAreThirtyByFortyFiveAndOutlinedInInk()
        {
            foreach (var theme in Themes.All)
            {
                foreach (bool hard in new[] { false, true })
                {
                    var sprite = PlaceholderArt.I.DoorFor(theme, hard);
                    Assert.AreEqual(DoorArt.Width, sprite.texture.width);
                    Assert.AreEqual(DoorArt.Height, sprite.texture.height);
                    Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode);
                    var canvas = DoorArt.Paint(theme, hard);
                    // Every pixel on the outer edge of the shape is ink: an empty pixel next to colour is never left behind.
                    // The ink outline itself is that edge, so a clear pixel beside ink is the outside of the sprite.
                    for (int y = 0; y < canvas.Height; y++)
                        for (int x = 0; x < canvas.Width; x++)
                        {
                            if (canvas.Filled(x, y)) continue;
                            bool touchesColour = Colour(canvas.Get(x - 1, y)) || Colour(canvas.Get(x + 1, y))
                                || Colour(canvas.Get(x, y - 1)) || Colour(canvas.Get(x, y + 1));
                            Assert.IsFalse(touchesColour, $"{theme.name} {(hard ? "hard" : "calm")} door has a bare edge at {x},{y}");
                        }
                }
            }
        }

        [Test]
        public void TheHardDoorIsMarkedAndTheCalmOneIsNot()
        {
            var theme = Themes.ForWorld(3);
            var calm = DoorArt.Paint(theme, false);
            var hard = DoorArt.Paint(theme, true);
            bool HasColour(PixelCanvas c, Color32 col)
            {
                for (int y = 0; y < c.Height; y++)
                    for (int x = 0; x < c.Width; x++)
                    {
                        var p = c.Get(x, y);
                        if (p.r == col.r && p.g == col.g && p.b == col.b && p.a > 0) return true;
                    }
                return false;
            }
            Assert.IsTrue(HasColour(hard, Palette.Lemon), "warning mark");
            Assert.IsTrue(HasColour(hard, Palette.Steel), "spikes");
            Assert.IsFalse(HasColour(calm, Palette.Lemon));
            Assert.IsTrue(HasColour(calm, Palette.Cream), "lantern");
            Assert.IsFalse(HasColour(hard, Palette.Cream));
            Assert.IsTrue(HasColour(calm, theme.skyTop), "dark opening");
            Assert.IsTrue(HasColour(calm, theme.tileCap), "frame highlight");
        }
    }
}
