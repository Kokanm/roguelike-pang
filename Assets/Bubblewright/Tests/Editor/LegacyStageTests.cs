using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>
    /// The arena grew from 14 to 18 rows per storey (no ground strip any more). Stages still drawn at 14 rows are padded
    /// by StageLayout.PadLegacy: rows 0-12 stay, the cap moves up 4 rows with copies of it underneath, and every placed
    /// y moves up 4 per storey below it.
    /// </summary>
    public class LegacyStageTests
    {
        static string[] Blank(int count)
        {
            var rows = new string[count];
            for (int i = 0; i < count; i++) rows[i] = new string('.', StageMap.Cols);
            return rows;
        }

        static void Paint(string[] rows, int row, int col, char ch)
        {
            int index = rows.Length - 1 - row;
            var chars = rows[index].ToCharArray();
            chars[col] = ch;
            rows[index] = new string(chars);
        }

        [Test]
        public void PaddingKeepsTheLowRowsAndRaisesTheCapWithCopiesUnderIt()
        {
            var rows = Blank(14);
            Paint(rows, 0, 3, '#');
            Paint(rows, 12, 5, 'B');
            Paint(rows, 13, 15, '#'); // a wall that reaches the ceiling
            Paint(rows, 12, 15, '#');

            var padded = StageLayout.PadLegacy(rows);
            Assert.AreEqual(StageMap.StoreyRows, padded.Length);
            var map = StageLayout.Parse(rows);
            Assert.AreEqual(1, map.Storeys);
            Assert.AreEqual(StageMap.StoreyRows, map.Rows);
            Assert.AreEqual(TileKind.Solid, map.Get(3, 0).kind);
            Assert.AreEqual(TileKind.Breakable, map.Get(5, 12).kind);
            for (int r = 12; r <= 17; r++)
                Assert.AreEqual(TileKind.Solid, map.Get(15, r).kind, $"the wall still reaches the ceiling at row {r}");
            for (int r = 13; r <= 17; r++)
                Assert.AreEqual(TileKind.Empty, map.Get(5, r).kind, $"open headroom at row {r}");
        }

        [Test]
        public void TwoStoreysKeepTheirHatchLadderAndFloor()
        {
            var rows = Blank(28);
            for (int r = 0; r <= 14; r++) Paint(rows, r, 4, r == 14 ? 'T' : 'H');
            for (int c = 0; c < StageMap.Cols; c++) if (c != 4) Paint(rows, 14, c, '#');
            Paint(rows, 20, 9, '#');

            var map = StageLayout.Parse(rows);
            Assert.AreEqual(2, map.Storeys);
            Assert.AreEqual(2 * StageMap.StoreyRows, map.Rows);
            for (int r = 0; r <= 17; r++) Assert.IsTrue(map.Get(4, r).ladder, $"ladder at row {r}");
            Assert.IsTrue(map.IsStoreyCap(17));
            Assert.AreEqual(TileKind.Solid, map.Get(4, 18).kind, "the T over the hatch is the floor of storey 2");
            Assert.IsTrue(map.Get(4, 18).ladder);
            Assert.AreEqual(TileKind.Solid, map.Get(0, 18).kind, "storey 2's floor moved from row 14 to row 18");
            Assert.AreEqual(TileKind.Solid, map.Get(9, 24).kind, "row 20 moved to row 24");
        }

        [Test]
        public void LegacyYAndRowMoveUpFourPerStoreyBelow()
        {
            Assert.AreEqual(8f, StageLayout.LegacyY(8f), 1e-5f);
            Assert.AreEqual(13.5f, StageLayout.LegacyY(13.5f), 1e-5f);
            Assert.AreEqual(18f, StageLayout.LegacyY(14f), 1e-5f);
            Assert.AreEqual(26f, StageLayout.LegacyY(22f), 1e-5f);
            Assert.AreEqual(44f, StageLayout.LegacyY(36f), 1e-5f);
            Assert.AreEqual(-1, StageLayout.LegacyRow(-1), "the ground stays the ground");
            Assert.AreEqual(5, StageLayout.LegacyRow(5));
            Assert.AreEqual(19, StageLayout.LegacyRow(15));
            Assert.AreEqual(36, StageLayout.LegacyRow(28));
        }

        [Test]
        public void ALegacyStageBuildsAtTheNewHeightWithItsThingsMoved()
        {
            var spec = StageSpec.ById("w1_three_roofs");
            Assert.NotNull(spec);
            Assert.IsTrue(spec.IsLegacy);
            Assert.AreEqual(14 * spec.Storeys, spec.Rows.Length);
            Assert.AreEqual(StageMap.StoreyRows * spec.Storeys, spec.TileRows.Length);
            var plan = spec.Build();
            Assert.AreEqual(StageMap.StoreyRows * spec.Storeys, plan.map.Rows);
            // Authored at y = 8, 22 and 36: one bubble per storey.
            var ys = new System.Collections.Generic.List<float>();
            foreach (var s in plan.spawns) ys.Add(s.position.y);
            ys.Sort();
            CollectionAssert.AreEqual(new[] { 8f, 26f, 44f }, ys);
        }

        [Test]
        public void EveryStageBuildsAtTheNewHeight()
        {
            foreach (var spec in StageSpec.All)
            {
                var plan = spec.Build();
                Assert.AreEqual(StageMap.StoreyRows * spec.Storeys, plan.map.Rows, spec.Id);
                foreach (var s in plan.spawns)
                    Assert.Less(s.position.y, plan.map.Height, spec.Id + ": every placed bubble is inside the taller map");
            }
        }
    }
}
