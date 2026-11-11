using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    public class HarpoonMapTests
    {
        static readonly string[] Tower =
        {
            "................................",
            "................................",
            "................................",
            "................................",
            "..........#########T##..........",
            "...................H............",
            "...................H............",
            "...................H............",
            "...................H............",
            "....##T##################T##....",
            "......H..................H......",
            "......H..................H......",
            "......H..................H......",
            "......H..................H......",
        };

        GameObject root;
        Harpoon harpoon;
        StageMap previous;

        [SetUp]
        public void SetUp()
        {
            previous = Game.Map;
            SetMap(StageLayout.Parse(Tower));
            root = new GameObject("HarpoonTestRoot");
            harpoon = Harpoon.Create(root.transform);
        }

        [TearDown]
        public void TearDown()
        {
            SetMap(previous);
            Object.DestroyImmediate(root);
        }

        static void SetMap(StageMap map)
        {
            var property = typeof(Game).GetProperty("Map", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(property, "Game.Map must exist.");
            property.SetValue(null, map);
        }

        // Highest point reached by the tip over the run, and how long the harpoon was active.
        float Run(Vector2 from, float seconds, out float activeTime)
        {
            float top = from.y;
            activeTime = 0f;
            harpoon.Fire(from);
            const float dt = 1f / 60f;
            for (float t = 0f; t < seconds; t += dt)
            {
                if (!harpoon.Active) break;
                harpoon.Tick(dt);
                activeTime += dt;
                var tip = harpoon.transform.Find("Tip");
                top = Mathf.Max(top, tip.position.y + (float)PlaceholderArt.HarpoonTipHeight / Tuning.I.pixelsPerUnit);
            }
            return top;
        }

        [Test]
        public void StopsUnderAPlatform()
        {
            harpoon.HoldTime = 0f;
            float top = Run(new Vector2(0f, 1.67f), 0.5f, out _);
            Assert.LessOrEqual(top, 4f + 0.001f);
            Assert.IsFalse(harpoon.Active);
        }

        [Test]
        public void ReachesTheCeilingWhereNothingIsAbove()
        {
            harpoon.HoldTime = 0f;
            float top = Run(new Vector2(-14f, 1.67f), 2f, out _);
            Assert.AreEqual(14f, top, 0.05f);
            Assert.IsFalse(harpoon.Active);
        }

        [Test]
        public void HooksUnderAPlatformForHoldTime()
        {
            harpoon.HoldTime = 1.5f;
            Run(new Vector2(0f, 1.67f), 1.4f, out _);
            Assert.IsTrue(harpoon.Active, "Still hooked before HoldTime is up.");
            for (int i = 0; i < 60 * 2 && harpoon.Active; i++) harpoon.Tick(1f / 60f);
            Assert.IsFalse(harpoon.Active);
        }

        [Test]
        public void StandingOnAPlatformStopsUnderTheNextOne()
        {
            harpoon.HoldTime = 0f;
            float top = Run(new Vector2(0f, 6.67f), 1f, out _);
            Assert.LessOrEqual(top, 9f + 0.001f);
            Assert.Greater(top, 8f);
        }
    }
}
