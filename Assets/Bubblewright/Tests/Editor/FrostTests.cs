using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>Frost bubble: temporary ice on the ground column or platform tile it lands on.</summary>
    public class FrostTests
    {
        const float Dt = 1f / 60f;

        GameObject root;
        StageMap previousMap;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("FrostTests");
            previousMap = Game.Map;
            SetMap(StageMap.Flat());
            Game.Ice.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Game.Ice.Clear();
            SetMap(previousMap);
            Object.DestroyImmediate(root);
        }

        static void SetMap(StageMap map)
        {
            var property = typeof(Game).GetProperty("Map", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(property, "Game.Map must exist.");
            property.SetValue(null, map);
        }

        static void Run(TempIce ice, StageMap map, float seconds)
        {
            int n = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < n; i++) ice.Tick(map, Dt);
        }

        [Test]
        public void GroundFreezesThenThaws()
        {
            var map = StageMap.Flat();
            var ice = new TempIce();
            ice.FreezeGround(map, 10);
            Assert.AreEqual(Surface.Ice, map.GetGround(10));
            Run(ice, map, 3.9f);
            Assert.AreEqual(Surface.Ice, map.GetGround(10));
            Run(ice, map, 0.2f);
            Assert.AreEqual(Surface.Normal, map.GetGround(10));
            Assert.AreEqual(0, ice.Count);
        }

        [Test]
        public void SandReturnsToSand()
        {
            var map = StageMap.Flat();
            for (int c = 0; c < StageMap.Cols; c++) map.SetGround(c, Surface.Sand);
            var ice = new TempIce();
            ice.FreezeGround(map, 5);
            Assert.AreEqual(Surface.Ice, map.GetGround(5));
            Run(ice, map, 4.1f);
            Assert.AreEqual(Surface.Sand, map.GetGround(5));
        }

        [Test]
        public void PermanentIceStaysAndIsNotTracked()
        {
            var map = StageMap.Flat();
            map.SetGround(7, Surface.Ice);
            var ice = new TempIce();
            ice.FreezeGround(map, 7);
            Assert.AreEqual(0, ice.Count);
            Run(ice, map, 6f);
            Assert.AreEqual(Surface.Ice, map.GetGround(7));
        }

        [Test]
        public void FreezingAgainResetsTheClockAndKeepsTheFirstSurface()
        {
            var map = StageMap.Flat();
            var ice = new TempIce();
            ice.FreezeGround(map, 12);
            Run(ice, map, 3f);
            ice.FreezeGround(map, 12);
            Assert.AreEqual(1, ice.Count);
            Run(ice, map, 3.9f);
            Assert.AreEqual(Surface.Ice, map.GetGround(12));
            Run(ice, map, 0.3f);
            Assert.AreEqual(Surface.Normal, map.GetGround(12));
        }

        [Test]
        public void TileFreezesAndRestores()
        {
            var map = StageMap.Flat();
            map.Set(8, 4, new Cell { kind = TileKind.Solid, surface = Surface.Normal });
            var ice = new TempIce();
            ice.FreezeTile(map, 8, 4);
            Assert.AreEqual(Surface.Ice, map.Get(8, 4).surface);
            Run(ice, map, 4.1f);
            Assert.AreEqual(Surface.Normal, map.Get(8, 4).surface);
            Assert.AreEqual(TileKind.Solid, map.Get(8, 4).kind);
        }

        [Test]
        public void EmptyCellIsNotFrozen()
        {
            var map = StageMap.Flat();
            var ice = new TempIce();
            ice.FreezeTile(map, 8, 4);
            Assert.AreEqual(0, ice.Count);
            Assert.AreEqual(TileKind.Empty, map.Get(8, 4).kind);
        }

        [Test]
        public void MapVersionRisesOnFreezeAndThaw()
        {
            var map = StageMap.Flat();
            var ice = new TempIce();
            int v0 = map.Version;
            ice.FreezeGround(map, 3);
            int v1 = map.Version;
            Assert.Greater(v1, v0);
            Run(ice, map, 4.1f);
            Assert.Greater(map.Version, v1);
        }

        [Test]
        public void FreezeUnderRespectsTheStartSafeZone()
        {
            var map = StageMap.Flat();
            var ice = new TempIce();
            float r = 0.8f;
            ice.FreezeUnder(map, new Vector2(0.5f, r), r, 1f, 0f);
            Assert.AreEqual(0, ice.Count);
            ice.FreezeUnder(map, new Vector2(0.5f, r), r, 2.5f, 0f);
            Assert.AreEqual(Surface.Ice, map.GetGround(16));
            ice.FreezeUnder(map, new Vector2(6.5f, r), r, 1f, 0f);
            Assert.AreEqual(Surface.Ice, map.GetGround(22));
        }

        [Test]
        public void FreezeUnderOnAPlatformFreezesTheTileNotTheGround()
        {
            var map = StageMap.Flat();
            map.Set(20, 4, new Cell { kind = TileKind.Solid });
            var ice = new TempIce();
            float r = 0.8f;
            ice.FreezeUnder(map, new Vector2(4.5f, 5f + r), r, 10f, 0f);
            Assert.AreEqual(Surface.Ice, map.Get(20, 4).surface);
            Assert.AreEqual(Surface.Normal, map.GetGround(20));
        }

        [Test]
        public void ThePatchIsOnePointFiveTimesTheBubbleWide()
        {
            var map = StageMap.Flat();
            var ice = new TempIce();
            float r = 0.8f; // diameter 1.6, patch 2.4 wide: x 5.3 to 7.7, so columns holding 5.3..7.7
            ice.FreezeUnder(map, new Vector2(6.5f, r), r, 10f, -10f);
            int c0 = StageMap.Col(5.3f), c1 = StageMap.Col(7.7f);
            Assert.AreEqual(c1 - c0 + 1, ice.Count);
            for (int c = c0; c <= c1; c++) Assert.AreEqual(Surface.Ice, map.GetGround(c));
            Assert.AreEqual(Surface.Normal, map.GetGround(c0 - 1));
            Assert.AreEqual(Surface.Normal, map.GetGround(c1 + 1));
        }

        [Test]
        public void APlatformPatchFreezesNeighbouringTilesInTheSameRow()
        {
            var map = StageMap.Flat();
            for (int c = 18; c <= 22; c++) map.Set(c, 4, new Cell { kind = TileKind.Solid });
            var ice = new TempIce();
            float r = 0.8f;
            ice.FreezeUnder(map, new Vector2(4.5f, 5f + r), r, 10f, -10f);
            int hit = 0;
            for (int c = 18; c <= 22; c++) if (map.Get(c, 4).surface == Surface.Ice) hit++;
            Assert.Greater(hit, 1);
            Assert.AreEqual(Surface.Normal, map.GetGround(20));
        }

        [Test]
        public void AGapColumnIsNeverFrozen()
        {
            var map = StageMap.Flat();
            map.SetGap(9, true);
            var ice = new TempIce();
            ice.FreezeGround(map, 9);
            Assert.AreEqual(0, ice.Count);
            Assert.AreEqual(Surface.Normal, map.GetGround(9));
        }

        [Test]
        public void AFrostBubbleLeavesIceWhereItFirstLands()
        {
            var map = StageMap.Flat();
            SetMap(map);
            var b = Bubble.Create(root.transform, BubbleKind.Frost, 2, new Vector2(6.5f, 3f), Vector2.zero, false);
            for (int i = 0; i < 240 && Game.Ice.Count == 0; i++) b.Tick(Dt);
            Assert.Greater(Game.Ice.Count, 1, "the bubble landed and froze a patch");
            Assert.AreEqual(Surface.Ice, map.GetGround(StageMap.Col(b.Position.x)));
        }
    }
}
