using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>The four selectable characters: stats table, runs, art colours, unlocks, saves and card text.</summary>
    public class CharacterTests
    {
        [SetUp]
        public void SetUp()
        {
            Unlocks.Reset();
            Unlocks.DebugAll = false;
            PlayerPrefs.DeleteKey(SaveGame.Key);
        }

        [TearDown]
        public void TearDown()
        {
            Unlocks.Reset();
            Unlocks.DebugAll = false;
            PlayerPrefs.DeleteKey(SaveGame.Key);
        }

        static bool Equal(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        [Test]
        public void ThereAreFourCharactersWithUniqueIds()
        {
            Assert.AreEqual(4, Character.All.Length);
            CollectionAssert.AllItemsAreUnique(Character.All.Select(c => c.id).ToArray());
            CollectionAssert.AreEqual(new[] { "wick", "ember", "moss", "flint" }, Character.All.Select(c => c.id).ToArray());
            foreach (var c in Character.All) Assert.AreSame(c, Character.ById(c.id));
        }

        [Test]
        public void UnknownIdFallsBackToWick()
        {
            Assert.AreSame(Character.Wick, Character.ById("nobody"));
            Assert.AreSame(Character.Wick, Character.ById(null));
            Assert.AreSame(Character.Wick, Character.ById(""));
        }

        [Test]
        public void WicksNumbersEqualTheTuningDefaults()
        {
            var defaults = ScriptableObject.CreateInstance<Tuning>();
            try
            {
                var wick = Character.Wick;
                Assert.AreEqual(defaults.hearts, wick.hearts);
                Assert.AreEqual(defaults.runSpeed, wick.runSpeed, 0.0001f);
                Assert.AreEqual(defaults.harpoonSpeed, wick.harpoonSpeed, 0.0001f);
                Assert.AreEqual(defaults.invulnerableTime, wick.invulnerableTime, 0.0001f);
                Assert.AreEqual(1, wick.shots);
            }
            finally { Object.DestroyImmediate(defaults); }
        }

        [Test]
        public void WicksEffectiveNumbersFollowTheLiveTuning()
        {
            var wick = Character.Wick;
            Assert.AreEqual(Tuning.I.hearts, wick.StartHearts);
            Assert.AreEqual(Tuning.I.runSpeed, wick.BaseRunSpeed, 0.0001f);
            Assert.AreEqual(Tuning.I.harpoonSpeed, wick.BaseHarpoonSpeed, 0.0001f);
            Assert.AreEqual(Tuning.I.invulnerableTime, wick.BaseInvulnerable, 0.0001f);
        }

        [Test]
        public void EveryRunStartsWithItsCharactersHeartsAndShots()
        {
            foreach (var c in Character.All)
            {
                var run = new Run(1, c);
                Assert.AreSame(c, run.Character, c.id);
                Assert.AreEqual(c.StartHearts, run.MaxHearts, c.id);
                Assert.AreEqual(c.StartHearts, run.Hearts, c.id);
                Assert.AreEqual(c.StartShots, run.Shots, c.id);
            }
            Assert.AreSame(Character.Wick, new Run(1).Character, "a run with no character is Wick's");
        }

        [Test]
        public void UpgradesMultiplyTheCharactersBaseNumbers()
        {
            foreach (var c in Character.All)
            {
                var run = new Run(1, c);
                run.Take(Upgrade.ByName("Swift Boots"));
                Assert.AreEqual(c.BaseRunSpeed * 1.12f, c.BaseRunSpeed * run.EffectiveSpeedMultiplier, 0.0001f, c.id);
            }
            var flint = new Run(1, Character.ById("flint"));
            flint.Take(Upgrade.ByName("Twin Cable"));
            Assert.AreEqual(3, flint.Shots, "Twin Cable adds one cable on top of Flint's two");
            var moss = new Run(1, Character.ById("moss"));
            moss.Take(Upgrade.ByName("Spare Wick"));
            Assert.AreEqual(5, moss.MaxHearts);
            Assert.AreEqual(5, moss.Hearts);
        }

        [Test]
        public void OnlyWickUsesSaffron()
        {
            foreach (var c in Character.All)
            {
                bool isWick = c.id == "wick";
                foreach (var col in new[] { c.body, c.bodyDark, c.bodyLine })
                    foreach (var saffron in new[] { Palette.Saffron, Palette.SaffronDark, Palette.SaffronLine })
                        if (!isWick) Assert.IsFalse(Equal(col, saffron), $"{c.id} uses a saffron colour");
                if (isWick) continue;

                var sprites = PlaceholderArt.I.SpritesFor(c);
                foreach (var sprite in new[] { sprites.Back, sprites.Front, sprites.Launcher, sprites.Hand })
                    foreach (var px in sprite.texture.GetPixels32())
                        foreach (var saffron in new[] { Palette.Saffron, Palette.SaffronDark, Palette.SaffronLine })
                            Assert.IsFalse(Equal(px, saffron), $"{c.id} sprite {sprite.name} has a saffron pixel");
            }
        }

        [Test]
        public void EveryBodyColourStandsOutFromEveryThemesSkyAndFarLayer()
        {
            foreach (var c in Character.All)
                foreach (var theme in Themes.All)
                    foreach (var (label, bg) in new[] { ("skyBottom", theme.skyBottom), ("far", theme.far) })
                        Assert.GreaterOrEqual(UiText.Contrast(c.body, bg), UiText.MinControlContrast,
                            $"{c.id} body on {theme.name} {label}");
        }

        [Test]
        public void EveryCharacterHasItsOwnSprites()
        {
            var seen = new List<(string id, Color32[] pixels)>();
            foreach (var c in Character.All)
            {
                var s = PlaceholderArt.I.SpritesFor(c);
                Assert.AreSame(s, PlaceholderArt.I.SpritesFor(c), $"{c.id} sprites are built once");
                foreach (var sprite in new[] { s.Back, s.Front, s.BackFlash, s.FrontFlash, s.Hand, s.Launcher, s.LauncherFlash, s.FrontLocked })
                {
                    Assert.IsNotNull(sprite, c.id);
                    Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode, $"{c.id} {sprite.name} must stay pixel sharp");
                }
                var front = s.Front.texture.GetPixels32();
                foreach (var other in seen)
                    Assert.IsFalse(front.SequenceEqual(other.pixels, new Color32Comparer()), $"{c.id} and {other.id} look the same");
                seen.Add((c.id, front));
            }
            Assert.AreSame(PlaceholderArt.I.WickBack, PlaceholderArt.I.SpritesFor(Character.Wick).Back, "Wick's art is unchanged");
        }

        class Color32Comparer : IEqualityComparer<Color32>
        {
            public bool Equals(Color32 a, Color32 b) => Equal(a, b);
            public int GetHashCode(Color32 c) => c.r | c.g << 8 | c.b << 16 | c.a << 24;
        }

        [Test]
        public void WickAndEmberAreOpenAndMossAndFlintAreLockedWithEmptyPrefs()
        {
            Assert.IsTrue(Unlocks.IsOpen(Character.ById("wick")));
            Assert.IsTrue(Unlocks.IsOpen(Character.ById("ember")));
            Assert.IsFalse(Unlocks.IsOpen(Character.ById("moss")));
            Assert.IsFalse(Unlocks.IsOpen(Character.ById("flint")));
        }

        [Test]
        public void UnlockReturnsTrueOnceAndTheCharacterThenStaysOpen()
        {
            Assert.IsTrue(Unlocks.Unlock("moss"));
            Assert.IsFalse(Unlocks.Unlock("moss"));
            Assert.IsTrue(Unlocks.IsOpen(Character.ById("moss")));
            Assert.IsFalse(Unlocks.IsOpen(Character.ById("flint")));
            Assert.IsTrue(Unlocks.Unlock("flint"));
            Assert.AreEqual("moss,flint", PlayerPrefs.GetString(Unlocks.Key));
        }

        [Test]
        public void UnlockAllIsNotSavedAndResetLocksAgain()
        {
            Unlocks.DebugAll = true;
            Assert.IsTrue(Unlocks.IsOpen(Character.ById("moss")));
            Assert.IsTrue(Unlocks.IsOpen(Character.ById("flint")));
            Assert.IsFalse(PlayerPrefs.HasKey(Unlocks.Key), "the cheat must not write PlayerPrefs");
            Unlocks.DebugAll = false;
            Assert.IsFalse(Unlocks.IsOpen(Character.ById("moss")));

            Unlocks.Unlock("moss");
            Unlocks.Reset();
            Assert.IsFalse(Unlocks.IsOpen(Character.ById("moss")));
        }

        [Test]
        public void WickUnlocksMossAndEmberUnlocksFlint()
        {
            Assert.AreEqual("wick", Character.ById("moss").unlockedByFinishingWith);
            Assert.AreEqual("ember", Character.ById("flint").unlockedByFinishingWith);
            Assert.IsNull(Character.ById("wick").unlockedByFinishingWith);
            Assert.IsNull(Character.ById("ember").unlockedByFinishingWith);
            Assert.AreEqual("Finish a run with WICK to unlock", Character.ById("moss").UnlockHint);
            Assert.AreEqual("Finish a run with EMBER to unlock", Character.ById("flint").UnlockHint);
            Assert.IsNull(Character.Wick.UnlockHint);
        }

        [Test]
        public void SaveAndLoadKeepTheCharacter()
        {
            foreach (var c in Character.All)
            {
                var run = new Run(77, c);
                run.Stage = 5;
                run.Hearts = 1;
                SaveGame.Save(run);
                var loaded = SaveGame.Load();
                Assert.IsNotNull(loaded, c.id);
                Assert.AreSame(c, loaded.Character, c.id);
                Assert.AreEqual(c.id, loaded.ToSave().character);
                Assert.AreEqual(c.StartShots, loaded.Shots, c.id);
                Assert.AreEqual(1, loaded.Hearts, c.id);
                Assert.AreEqual(c.StartHearts, loaded.MaxHearts, c.id);
            }
        }

        [Test]
        public void ASaveWithAnUnknownCharacterLoadsAsWick()
        {
            var data = new Run(5, Character.ById("moss")).ToSave();
            data.character = "no-such-hero";
            PlayerPrefs.SetString(SaveGame.Key, JsonUtility.ToJson(data));
            var loaded = SaveGame.Load();
            Assert.IsNotNull(loaded);
            Assert.AreSame(Character.Wick, loaded.Character);
        }

        [Test]
        public void StatPipsRankBetweenTheLowestAndHighestValue()
        {
            var speeds = Character.All.Select(c => c.SpeedPips).ToArray();
            var shotSpeeds = Character.All.Select(c => c.ShotSpeedPips).ToArray();
            Assert.AreEqual(1, speeds.Min());
            Assert.AreEqual(5, speeds.Max());
            Assert.AreEqual(1, shotSpeeds.Min());
            Assert.AreEqual(5, shotSpeeds.Max());
            Assert.AreEqual(5, Character.ById("ember").SpeedPips);
            Assert.AreEqual(1, Character.ById("moss").SpeedPips);
            Assert.AreEqual(1, Character.ById("flint").ShotSpeedPips);
            Assert.AreEqual(5, Character.ById("ember").ShotSpeedPips);
        }

        [Test]
        public void CardTextFits()
        {
            foreach (var c in Character.All)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(c.name));
                Assert.LessOrEqual(c.name.Length, Character.MaxNameLength, $"{c.name}: name too long for a card");
                Assert.IsFalse(string.IsNullOrWhiteSpace(c.blurb), $"{c.name} has no blurb");
                // Four cards share the 480 px screen (112 px each), so the blurb gets 2 lines of about 19 characters.
                Assert.LessOrEqual(c.blurb.Length, Character.MaxBlurbLength, $"{c.name}: blurb too long for two lines on a card");
                if (c.UnlockHint != null)
                    Assert.LessOrEqual(c.UnlockHint.Length, Character.MaxUnlockHintLength, $"{c.name}: unlock text too long for three lines on a card");
            }
            foreach (var label in new[] { "HEARTS", "SPEED", "CABLES", "SHOT SPEED" })
                Assert.LessOrEqual(label.Length, 10, label);
        }
    }
}
