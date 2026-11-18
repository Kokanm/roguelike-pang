using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 5 difficulty 4. Three crypt floors on one ladder, a Ghost haunting each one.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W5, D4, MinStageInWorld 4, Storeys 3.
    /// Concept: three crypt floors on one ladder, each hatch opening when its floor is clear; a Ghost haunts every
    /// floor and the top one calls in two more.
    /// Archetype: Storey Tower Climb (W1_ThreeRoofs). Structure: one ladder on column 4 through both hatches; each
    /// floor has a low lintel beside the ladder foot, cover from bubbles bouncing in from the right.
    /// Enemies: floor 1 Gloop XL (column 22) and Ghost L (column 28); floor 2 Trigloop L (column 18) and Ghost L
    /// (column 26); floor 3 Ghost L at columns 14 and 26, then a wave of two Ghost L.
    /// Pops: 15 + 7 + 13 + 7 + 7 + 7 + wave 7 + 7 = 70 (D4 band 55-90); slot 4-5 hard clock 78 s allows 124.
    /// Hitless plan: on each floor, shoot the bubbles in the open from the left half (they start on the right and
    /// drift right), shoot each Ghost while solid, then climb. Every arrival is at column 4, at least 9 u from the
    /// floor's enemies. On the top floor, step clear of the wave markers.
    /// Over-break: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W5_TripleCrypt : StageSpec
    {
        public override string Id => "w5_triple_crypt";
        public override string Name => "Triple Crypt";
        public override int World => 5;
        public override int Difficulty => 4;
        public override int MinStageInWorld => 4;
        public override int Storeys => 3;
        public override string Concept =>
            "Three crypt floors on one ladder; each hatch opens when its floor is clear. A Ghost haunts every floor and the top one calls two more.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open(3);
                StageSketch.Box(grid, 0, 31, 14, 14, '#');
                StageSketch.Box(grid, 0, 31, 28, 28, '#');
                for (int y = 0; y <= 28; y++) grid[y][4] = y == 14 || y == 28 ? 'T' : 'H';
                // A lintel beside the ladder foot on every floor, four rows up, from column 6 to 9.
                StageSketch.Box(grid, 6, 9, 4, 4, '#');
                StageSketch.Box(grid, 6, 9, 19, 19, '#');
                StageSketch.Box(grid, 6, 9, 33, 33, '#');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(8);
            // Floor 1.
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 22, 8f, 1);
            b.BubbleAt(BubbleKind.Ghost, StageBuilder.TierL, 28, 9f, 1);
            // Floor 2.
            b.BubbleAt(BubbleKind.Trigloop, StageBuilder.TierL, 18, 22f, 1);
            b.BubbleAt(BubbleKind.Ghost, StageBuilder.TierL, 26, 23f, 1);
            // Floor 3.
            b.BubbleAt(BubbleKind.Ghost, StageBuilder.TierL, 14, 36f, 1);
            b.BubbleAt(BubbleKind.Ghost, StageBuilder.TierL, 26, 37f, 1);
            b.Wave(0, BubbleKind.Ghost, StageBuilder.TierL, -8f, 1);
            b.Wave(0, BubbleKind.Ghost, StageBuilder.TierL, 8f, -1);
        }
    }
}
