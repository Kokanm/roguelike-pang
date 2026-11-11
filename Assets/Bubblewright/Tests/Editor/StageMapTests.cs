using System;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    public class StageMapTests
    {
        static readonly string[] Shelf =
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
            "..........##T#########..........",
            "............H...................",
            "............H...................",
            "............H...................",
            "............H...................",
        };

        [Test]
        public void FlatHasNoSolidCellsInsideAndIsSolidOutside()
        {
            var map = StageMap.Flat();
            for (int c = 0; c < StageMap.Cols; c++)
            {
                for (int r = 0; r < StageMap.Rows; r++)
                {
                    Assert.AreEqual(TileKind.Empty, map.Get(c, r).kind);
                    Assert.IsFalse(map.IsSolid(c, r));
                }
            }

            Assert.AreEqual(TileKind.Solid, map.Get(-1, 0).kind);
            Assert.AreEqual(TileKind.Solid, map.Get(StageMap.Cols, 0).kind);
            Assert.AreEqual(TileKind.Solid, map.Get(0, -1).kind);
            Assert.AreEqual(TileKind.Solid, map.Get(0, StageMap.Rows).kind);
            Assert.IsTrue(map.IsSolid(-1, 5));
            Assert.IsTrue(map.IsSolid(StageMap.Cols, 5));
            Assert.IsTrue(map.IsSolid(5, -1));
            Assert.IsTrue(map.IsSolid(5, StageMap.Rows));
        }

        [Test]
        public void GridCoordinatesMatchArena()
        {
            Assert.AreEqual(0, StageMap.Col(-16f));
            Assert.AreEqual(0, StageMap.Col(-15.01f));
            Assert.AreEqual(1, StageMap.Col(-15f));
            Assert.AreEqual(16, StageMap.Col(0f));
            Assert.AreEqual(31, StageMap.Col(15.99f));

            Assert.AreEqual(0, StageMap.Row(0f));
            Assert.AreEqual(4, StageMap.Row(4.5f));
            Assert.AreEqual(13, StageMap.Row(13.99f));
            Assert.AreEqual(-1, StageMap.Row(-0.1f));

            Assert.AreEqual(new Rect(-16f, 0f, 1f, 1f), StageMap.CellRect(0, 0));
            Assert.AreEqual(new Rect(-6f, 4f, 1f, 1f), StageMap.CellRect(10, 4));
            Assert.AreEqual(new Rect(15f, 13f, 1f, 1f), StageMap.CellRect(31, 13));
            Assert.AreEqual(-15.5f, StageMap.CenterX(0), 1e-5f);
            Assert.AreEqual(15.5f, StageMap.CenterX(31), 1e-5f);
        }

        [Test]
        public void ParseReadsShelfAndBreakables()
        {
            var map = StageLayout.Parse(Shelf);

            for (int c = 0; c < StageMap.Cols; c++)
            {
                bool onShelf = c >= 10 && c <= 21;
                Assert.AreEqual(onShelf, map.IsSolid(c, 4), $"col {c} row 4 solid");
                Assert.AreEqual(onShelf ? TileKind.Solid : TileKind.Empty, map.Get(c, 4).kind, $"col {c} row 4 kind");
            }

            Assert.AreEqual(TileKind.Solid, map.Get(12, 4).kind);
            Assert.IsTrue(map.Get(12, 4).ladder);
            Assert.IsFalse(map.Get(10, 4).ladder);

            for (int r = 0; r <= 3; r++)
            {
                Assert.AreEqual(TileKind.Empty, map.Get(12, r).kind, $"row {r} kind");
                Assert.IsTrue(map.Get(12, r).ladder, $"row {r} ladder");
                Assert.IsFalse(map.IsSolid(12, r), $"row {r} solid");
            }

            var withBreakable = (string[])Shelf.Clone();
            withBreakable[13] = "B...............................";
            var bMap = StageLayout.Parse(withBreakable);
            Assert.AreEqual(TileKind.Breakable, bMap.Get(0, 0).kind);
            Assert.AreEqual(2, bMap.Get(0, 0).hp);
            Assert.IsTrue(bMap.IsSolid(0, 0));
        }

        [Test]
        public void ParseRejectsBadRowsAndCharacters()
        {
            var shortRow = (string[])Shelf.Clone();
            shortRow[5] = new string('.', 31);
            Assert.Throws<ArgumentException>(() => StageLayout.Parse(shortRow));

            var badChar = (string[])Shelf.Clone();
            badChar[5] = "...............?................";
            Assert.Throws<ArgumentException>(() => StageLayout.Parse(badChar));

            var wrongCount = new string[13];
            for (int i = 0; i < wrongCount.Length; i++) wrongCount[i] = new string('.', 32);
            Assert.Throws<ArgumentException>(() => StageLayout.Parse(wrongCount));
        }

        [Test]
        public void FloorBelowAndCeilingAboveQueryShelf()
        {
            var map = StageLayout.Parse(Shelf);

            Assert.AreEqual(5f, map.FloorBelow(0f, 8f), 1e-5f);
            Assert.AreEqual(5f, map.FloorBelow(0f, 5f), 1e-5f);
            Assert.AreEqual(0f, map.FloorBelow(0f, 3f), 1e-5f);

            float ceil = map.CeilingAbove(0f, 1f, out int hitRow);
            Assert.AreEqual(4f, ceil, 1e-5f);
            Assert.AreEqual(4, hitRow);

            float sky = map.CeilingAbove(-14f, 1f, out int skyRow);
            Assert.AreEqual(14f, sky, 1e-5f);
            Assert.AreEqual(StageMap.Rows, skyRow);
        }

        [Test]
        public void OverlapsSolidAndSurfaceUnderRespectTilesAndBounds()
        {
            var map = StageLayout.Parse(Shelf);
            map.SetGround(3, Surface.Sand);
            var icy = map.Get(16, 4);
            icy.surface = Surface.Ice;
            map.Set(16, 4, icy);

            Assert.IsFalse(map.OverlapsSolid(new Rect(-0.5f, 5f, 1f, 1.5f)));
            Assert.IsTrue(map.OverlapsSolid(new Rect(-0.5f, 4.5f, 1f, 1f)));
            Assert.IsTrue(map.OverlapsSolid(new Rect(-16.1f, 1f, 1f, 1f)));
            Assert.IsTrue(map.OverlapsSolid(new Rect(15.2f, 1f, 1f, 1f)));
            Assert.IsTrue(map.OverlapsSolid(new Rect(0f, -0.1f, 1f, 1f)));
            Assert.IsTrue(map.OverlapsSolid(new Rect(0f, 13.5f, 1f, 1f)));

            Assert.AreEqual(Surface.Sand, map.SurfaceUnder(StageMap.CenterX(3), 0f));
            Assert.AreEqual(Surface.Sand, map.SurfaceUnder(StageMap.CenterX(3), 0.005f));
            Assert.AreEqual(Surface.Normal, map.SurfaceUnder(0f, 0f));
            Assert.AreEqual(Surface.Ice, map.SurfaceUnder(0.5f, 5f));
        }

        [Test]
        public void ResolveCirclePushesOutOfPlatformAndArenaBounds()
        {
            var map = StageLayout.Parse(Shelf);

            // 0.3 above the platform top (including near a column seam) is pushed up to radius above it.
            var pTop = new Vector2(0.2f, 5.3f);
            var cTop = map.ResolveCircle(ref pTop, 0.5f);
            Assert.AreEqual(0.2f, pTop.x, 1e-5f);
            Assert.AreEqual(5.5f, pTop.y, 1e-5f);
            Assert.IsTrue(cTop.landed);
            Assert.IsFalse(cTop.bumped);
            Assert.AreEqual(0, cTop.side);

            // Just under the platform is pushed down with bumped true.
            var pUnder = new Vector2(0f, 3.7f);
            var cUnder = map.ResolveCircle(ref pUnder, 0.5f);
            Assert.AreEqual(0f, pUnder.x, 1e-5f);
            Assert.AreEqual(3.5f, pUnder.y, 1e-5f);
            Assert.IsTrue(cUnder.bumped);
            Assert.IsFalse(cUnder.landed);
            Assert.AreEqual(0, cUnder.side);

            // Overlapping the platform's left end (x = -6) from the side gets side -1.
            var pLeftEnd = new Vector2(-6.3f, 4.5f);
            var cLeftEnd = map.ResolveCircle(ref pLeftEnd, 0.5f);
            Assert.AreEqual(-6.5f, pLeftEnd.x, 1e-5f);
            Assert.AreEqual(4.5f, pLeftEnd.y, 1e-5f);
            Assert.AreEqual(-1, cLeftEnd.side);
            Assert.IsFalse(cLeftEnd.landed);
            Assert.IsFalse(cLeftEnd.bumped);

            // Near the right arena wall (x = 15.9) is pushed inside with side -1.
            var pRightWall = new Vector2(15.9f, 2f);
            var cRightWall = map.ResolveCircle(ref pRightWall, 0.5f);
            Assert.AreEqual(15.5f, pRightWall.x, 1e-5f);
            Assert.AreEqual(2f, pRightWall.y, 1e-5f);
            Assert.AreEqual(-1, cRightWall.side);
            Assert.IsFalse(cRightWall.landed);
            Assert.IsFalse(cRightWall.bumped);

            // Centre inside a solid tile is pushed out along the smallest penetration axis.
            var pInside = new Vector2(0.5f, 4.9f);
            var cInside = map.ResolveCircle(ref pInside, 0.5f);
            Assert.AreEqual(0.5f, pInside.x, 1e-5f);
            Assert.AreEqual(5.5f, pInside.y, 1e-5f);
            Assert.IsTrue(cInside.landed);

            // Far from everything: not moved and reports nothing.
            var pFree = new Vector2(-10f, 8f);
            var cFree = map.ResolveCircle(ref pFree, 0.5f);
            Assert.AreEqual(new Vector2(-10f, 8f), pFree);
            Assert.IsFalse(cFree.landed);
            Assert.IsFalse(cFree.bumped);
            Assert.AreEqual(0, cFree.side);
        }

        [Test]
        public void VersionIncrementsWhenTileOrGroundChanges()
        {
            var map = StageMap.Flat();
            int v0 = map.Version;

            map.Set(5, 5, new Cell { kind = TileKind.Solid });
            Assert.AreEqual(v0 + 1, map.Version);

            map.SetGround(5, Surface.Ice);
            Assert.AreEqual(v0 + 2, map.Version);

            // Out-of-bounds Set and SetGround do nothing.
            map.Set(-1, 0, new Cell { kind = TileKind.Solid });
            map.SetGround(StageMap.Cols, Surface.Sand);
            Assert.AreEqual(v0 + 2, map.Version);
        }
    }
}
