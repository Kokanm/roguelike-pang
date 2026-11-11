using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    public class SaveTests
    {
        [SetUp]
        public void SetUp() => PlayerPrefs.DeleteKey(SaveGame.Key);

        [TearDown]
        public void TearDown() => PlayerPrefs.DeleteKey(SaveGame.Key);

        static Run BuildSampleRun()
        {
            var run = new Run(4242);
            run.Take(Upgrade.ByName("Spare Wick"));
            run.Take(Upgrade.ByName("Swift Boots"));
            run.Take(Upgrade.ByName("Swift Boots"));
            run.Stage = 9;
            run.Score = 1234;
            run.Hearts = 2;
            run.SetHard(2, Debuff.ByName("Heavy Boots"));
            return run;
        }

        static string Describe(List<SpawnSpec> specs) =>
            string.Join(";", specs.Select(s => $"{s.kind}/{s.tier}/{s.position.x:F4}/{s.position.y:F4}/{s.direction}"));

        [Test]
        public void RunSurvivesSaveAndLoadWithEveryValueEqual()
        {
            var original = BuildSampleRun();
            Assert.AreEqual(4, original.MaxHearts);

            SaveGame.Save(original);
            Assert.IsTrue(SaveGame.Exists);

            var loaded = SaveGame.Load();
            Assert.IsNotNull(loaded);

            Assert.AreEqual(original.Seed, loaded.Seed);
            Assert.AreEqual(9, loaded.Stage);
            Assert.AreEqual(1234, loaded.Score);
            Assert.AreEqual(2, loaded.Hearts);
            Assert.AreEqual(4, loaded.MaxHearts);
            Assert.AreEqual(original.SpeedMultiplier, loaded.SpeedMultiplier, 0.0001f);
            Assert.AreEqual(original.HarpoonSpeedMultiplier, loaded.HarpoonSpeedMultiplier, 0.0001f);
            Assert.AreEqual(original.HarpoonWidthMultiplier, loaded.HarpoonWidthMultiplier, 0.0001f);
            Assert.AreEqual(original.CableHold, loaded.CableHold, 0.0001f);
            Assert.AreEqual(original.ExtraInvulnerable, loaded.ExtraInvulnerable, 0.0001f);
            Assert.AreEqual(original.Shots, loaded.Shots);

            Assert.IsTrue(loaded.IsHard);
            Assert.AreEqual("Heavy Boots", loaded.CurrentDebuff.Name);
            CollectionAssert.AreEqual(original.hardWorld, loaded.hardWorld);
            CollectionAssert.AreEqual(original.worldDebuff, loaded.worldDebuff);

            foreach (var u in Upgrade.All)
                Assert.AreEqual(original.TimesTaken(u), loaded.TimesTaken(u), u.Name);

            Assert.AreEqual(original.BuildSummary(), loaded.BuildSummary());
            Assert.AreEqual(1.24f * 0.85f, loaded.EffectiveSpeedMultiplier, 0.0001f);
            Assert.AreEqual(original.EffectiveSpeedMultiplier, loaded.EffectiveSpeedMultiplier, 0.0001f);
            CollectionAssert.AreEqual(original.ToSave().upgrades, loaded.ToSave().upgrades);
            Assert.AreEqual("wick", loaded.ToSave().character);
        }

        [Test]
        public void LoadedRunGeneratesTheSameStage()
        {
            var original = BuildSampleRun();
            SaveGame.Save(original);
            var loaded = SaveGame.Load();

            Assert.AreEqual(Describe(StageGenerator.Generate(original)), Describe(StageGenerator.Generate(loaded)));
            CollectionAssert.AreEqual(
                original.RollChoices().Select(u => u.Name).ToArray(),
                loaded.RollChoices().Select(u => u.Name).ToArray());
        }

        [Test]
        public void LoadReturnsNullWhenMissingNotJsonOrWrongVersion()
        {
            Assert.IsNull(SaveGame.Load());

            PlayerPrefs.SetString(SaveGame.Key, "not json {{{");
            Assert.IsNull(SaveGame.Load());

            var future = BuildSampleRun().ToSave();
            future.version = 99;
            PlayerPrefs.SetString(SaveGame.Key, JsonUtility.ToJson(future));
            Assert.IsNull(SaveGame.Load());
        }

        [Test]
        public void DeleteRemovesSaveAndExistsBecomesFalse()
        {
            SaveGame.Save(BuildSampleRun());
            Assert.IsTrue(SaveGame.Exists);

            SaveGame.Delete();
            Assert.IsFalse(SaveGame.Exists);
            Assert.IsNull(SaveGame.Load());
        }

        [Test]
        public void UnknownSavedUpgradeNameIsSkippedWithoutException()
        {
            var data = new Run(101).ToSave();
            data.upgrades = new[] { "Swift Boots", "Removed Upgrade", "Twin Cable" };
            PlayerPrefs.SetString(SaveGame.Key, JsonUtility.ToJson(data));

            Run loaded = null;
            Assert.DoesNotThrow(() => loaded = SaveGame.Load());
            Assert.IsNotNull(loaded);
            Assert.AreEqual(1, loaded.TimesTaken(Upgrade.ByName("Swift Boots")));
            Assert.AreEqual(1, loaded.TimesTaken(Upgrade.ByName("Twin Cable")));
            Assert.AreEqual(2, loaded.Shots);
            Assert.AreEqual(1.12f, loaded.SpeedMultiplier, 0.0001f);
            Assert.AreEqual("Twin Cable, Swift Boots", loaded.BuildSummary());
        }

        [Test]
        public void SavedPlaceFormatsWorldStageAndHearts()
        {
            var twoHearts = BuildSampleRun();
            Assert.AreEqual("WORLD 2-3, 2 HEARTS", Game.SavedPlace(twoHearts));

            var oneHeart = new Run(7) { Stage = 3, Hearts = 1 };
            Assert.AreEqual("WORLD 1-3, 1 HEART", Game.SavedPlace(oneHeart));
        }

        [Test]
        public void UpdateHeartsPersistsDamageWhileKeepingStageStartScore()
        {
            var run = BuildSampleRun();
            run.Hearts = 3;
            SaveGame.Save(run);

            // Simulate popping bubbles mid-stage, then taking two hits before quitting or closing the app.
            run.AddScore(500);
            SaveGame.UpdateHearts(1);

            var loaded = SaveGame.Load();
            Assert.IsNotNull(loaded);
            Assert.AreEqual(1, loaded.Hearts);
            Assert.AreEqual(4, loaded.MaxHearts);
            Assert.AreEqual(1234, loaded.Score, "mid-stage points must not be banked when continuing");
            Assert.AreEqual(9, loaded.Stage);

            // Mid-stage heart gains cannot raise the saved heart count above what was already lost.
            SaveGame.UpdateHearts(3);
            Assert.AreEqual(1, SaveGame.Load().Hearts);

            // Lethal damage removes the save.
            SaveGame.UpdateHearts(0);
            Assert.IsFalse(SaveGame.Exists);
        }
    }
}
