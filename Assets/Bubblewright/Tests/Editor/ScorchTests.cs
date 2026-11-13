using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    public class ScorchTests
    {
        const float Dt = 1f / 60f;

        GameObject root;
        Flames flames;
        StageMap previousMap;
        Flames previousFlames;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("ScorchTests");
            flames = Flames.Create(root.transform);
            previousMap = Game.Map;
            previousFlames = Game.Flames;
            SetMap(StageMap.Flat());
            SetFlames(flames);
        }

        [TearDown]
        public void TearDown()
        {
            SetMap(previousMap);
            SetFlames(previousFlames);
            Object.DestroyImmediate(root);
        }

        static void SetMap(StageMap map)
        {
            var property = typeof(Game).GetProperty("Map", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(property, "Game.Map must exist.");
            property.SetValue(null, map);
        }

        static void SetFlames(Flames value)
        {
            var property = typeof(Game).GetProperty("Flames", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(property, "Game.Flames must exist.");
            property.SetValue(null, value);
        }

        void Run(float seconds)
        {
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++) flames.Tick(Game.Map, Dt);
        }

        // A Wick-sized box standing on a surface, centred on a column.
        static Rect StandingOn(int col, float surfaceY, float lift = 0f) =>
            new Rect(StageMap.CenterX(col) - 0.3f, surfaceY + 0.02f + lift, 0.6f, 1.2f);

        [Test]
        public void AFlameBurnsForItsLifeThenGoesOut()
        {
            flames.Add(Game.Map, 10, 0f);
            Assert.AreEqual(1, flames.Count);
            Run(1.4f);
            Assert.AreEqual(1, flames.Count, "still burning at 1.4 s");
            Run(0.2f);
            Assert.AreEqual(0, flames.Count, "out after 1.5 s");
        }

        [Test]
        public void AddingOnTheSameSurfaceResetsTheClock()
        {
            flames.Add(Game.Map, 10, 0f);
            Run(1.0f);
            flames.Add(Game.Map, 10, 0f);
            Assert.AreEqual(1, flames.Count);
            Run(1.0f);
            Assert.AreEqual(1, flames.Count, "the clock restarted");
            Run(0.6f);
            Assert.AreEqual(0, flames.Count);
        }

        [Test]
        public void ASeventhFlamePutsTheOldestOut()
        {
            for (int i = 0; i < 7; i++) flames.Add(Game.Map, 2 + i, 0f);
            Assert.AreEqual(6, flames.Count);
            Assert.IsFalse(flames.Hurts(StandingOn(2, 0f), out _), "the first one is gone");
            Assert.IsTrue(flames.Hurts(StandingOn(3, 0f), out _), "the second one still burns");
            Assert.IsTrue(flames.Hurts(StandingOn(8, 0f), out _), "the newest burns");
        }

        [Test]
        public void NoFlameOverAGroundGap()
        {
            Game.Map.SetGap(12, true);
            flames.Add(Game.Map, 12, 0f);
            Assert.AreEqual(0, flames.Count);
            flames.Add(Game.Map, 13, 0f);
            Assert.AreEqual(1, flames.Count);
        }

        [Test]
        public void HurtsOnlyWhoeverStandsInIt()
        {
            flames.Add(Game.Map, 10, 0f);
            Assert.IsTrue(flames.Hurts(StandingOn(10, 0f), out float x));
            Assert.AreEqual(StageMap.CenterX(10), x, 0.01f);
            Assert.IsFalse(flames.Hurts(StandingOn(12, 0f), out _), "two tiles away");
            Assert.IsFalse(flames.Hurts(StandingOn(10, 0f, Tuning.I.flameHurtHeight + 0.1f), out _), "jumping over it");
        }

        [Test]
        public void APlatformFlameHurtsOnlyOnThePlatform()
        {
            Game.Map.Set(10, 4, new Cell { kind = TileKind.Solid });
            flames.Add(Game.Map, 10, 5f);
            Assert.IsTrue(flames.Hurts(StandingOn(10, 5f), out _));
            Assert.IsFalse(flames.Hurts(StandingOn(10, 0f), out _), "on the ground below");
        }

        [Test]
        public void RemovingTheTileUnderAPlatformFlameRemovesIt()
        {
            Game.Map.Set(10, 4, new Cell { kind = TileKind.Solid });
            flames.Add(Game.Map, 10, 5f);
            Game.Map.Set(10, 4, new Cell { kind = TileKind.Empty });
            flames.Tick(Game.Map, Dt);
            Assert.AreEqual(0, flames.Count);
        }

        [Test]
        public void AddUnderFindsTheSurfaceUnderABubble()
        {
            // Ground: a bubble of radius 0.8 resting at x = 3.5 is on column 19.
            flames.AddUnder(Game.Map, new Vector2(3.5f, 0.8f), 0.8f);
            Assert.AreEqual(1, flames.Count);
            Assert.IsTrue(flames.Hurts(StandingOn(19, 0f), out _));

            // Platform: resting on a row-4 tile, the surface is y = 5.
            flames.Clear();
            Game.Map.Set(19, 4, new Cell { kind = TileKind.Solid });
            flames.AddUnder(Game.Map, new Vector2(3.5f, 5.8f), 0.8f);
            Assert.AreEqual(1, flames.Count);
            Assert.IsTrue(flames.Hurts(StandingOn(19, 5f), out _));
            Assert.IsFalse(flames.Hurts(StandingOn(19, 0f), out _));
        }

        [Test]
        public void AScorchLeavesAFlameWhereItLandsAndASmallOneLeavesNone()
        {
            var medium = Bubble.Create(root.transform, BubbleKind.Scorch, 2, new Vector2(3.5f, 3f), Vector2.zero, false);
            for (int i = 0; i < 120 && flames.Count == 0; i++) medium.Tick(Dt);
            Assert.AreEqual(1, flames.Count, "a medium Scorch lands once and leaves a flame");
            Assert.IsTrue(flames.Hurts(StandingOn(StageMap.Col(medium.Position.x), 0f), out _));

            flames.Clear();
            var small = Bubble.Create(root.transform, BubbleKind.Scorch, 3, new Vector2(-8.5f, 3f), Vector2.zero, false);
            for (int i = 0; i < 120; i++) small.Tick(Dt);
            Assert.AreEqual(0, flames.Count, "an S Scorch leaves none");
        }
    }
}
