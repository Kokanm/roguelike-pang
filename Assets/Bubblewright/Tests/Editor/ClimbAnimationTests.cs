using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>Wick's ladder climb frames: they alternate while he moves on a ladder and give way to the standing view off it.</summary>
    public class ClimbAnimationTests
    {
        const float Dt = 1f / 60f;

        static readonly string[] Stage =
        {
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "..........####.....T####........",
            "...................H............",
            "...................H............",
            "...................H............",
            "...................H............",
        };

        GameObject root;
        Wick wick;
        StageMap previousMap;

        [SetUp]
        public void SetUp()
        {
            previousMap = Game.Map;
            SetMap(StageLayout.Parse(Stage));
            root = new GameObject("ClimbAnimationTestRoot");
            wick = Wick.Create(root.transform, Effects.Create(root.transform));
        }

        [TearDown]
        public void TearDown()
        {
            SetMap(previousMap);
            Object.DestroyImmediate(root);
        }

        static void SetMap(StageMap m)
        {
            typeof(Game).GetProperty("Map", BindingFlags.Public | BindingFlags.Static).SetValue(null, m);
        }

        void Step(float seconds, float climb = 0f)
        {
            int ticks = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < ticks; i++) wick.Tick(0f, climb, false, Dt, true);
        }

        [Test]
        public void ClimbFramesAlternateWhileMovingOnALadder()
        {
            wick.ResetForStage(StageMap.CenterX(19), 5f, 3);
            Step(0.1f);
            Step(0.1f, climb: -1f);
            Assert.AreEqual(Mode.Climb, wick.Mode);
            var seen = new System.Collections.Generic.HashSet<int>();
            Step(0.1f, climb: -1f); // let the first rung step settle
            for (int i = 0; i < 40; i++)
            {
                Step(Dt, climb: -1f);
                seen.Add(wick.ClimbFrame);
            }
            Assert.IsTrue(seen.Contains(0) && seen.Contains(1), "both ladder frames show while he climbs");
            Assert.IsFalse(seen.Contains(-1));
        }

        [Test]
        public void TheStandingViewReturnsOffTheLadder()
        {
            wick.ResetForStage(StageMap.CenterX(19), 5f, 3);
            Step(0.1f);
            Step(0.5f, climb: -1f);
            Assert.AreEqual(Mode.Climb, wick.Mode);
            wick.ResetForStage(0f, 0f, 3);
            Step(0.2f);
            Assert.AreEqual(-1, wick.ClimbFrame);
        }

        [Test]
        public void ClimbSpritesKeepTheStandingCanvasAndPivot()
        {
            var set = PlaceholderArt.I.SpritesFor(Character.Wick);
            foreach (var s in new[] { set.ClimbA, set.ClimbB, set.ClimbAFlash, set.ClimbBFlash })
            {
                Assert.AreEqual(set.Back.rect.size, s.rect.size);
                Assert.AreEqual(set.Back.pivot, s.pivot);
            }
        }
    }
}
