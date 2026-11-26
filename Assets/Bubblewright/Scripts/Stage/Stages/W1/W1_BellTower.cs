using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 1 difficulty 3. A two-storey tower. The hatch opens when the lower court is clear.</summary>
    [Preserve]
    public sealed class W1_BellTower : StageSpec
    {
        public override string Id => "w1_bell_tower";
        public override string Name => "Bell Tower";
        public override int World => 1;
        public override int Difficulty => 3;
        public override int MinStageInWorld => 3;
        public override int Storeys => 2;
        public override string Concept =>
            "Clear the Gloop between the lower pillars, climb either of the twin center ladders through a hatch, and pop the Trigloop in the belfry.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open(2);
                StageSketch.Box(grid, 0, 31, 18, 18, '#');
                // Twin ladders on columns 14 and 17, each with its own hatch on row 17. Pillars on 8 and 23 mirror each other.
                for (int y = 0; y <= 18; y++)
                {
                    grid[y][14] = y == 18 ? 'T' : 'H';
                    grid[y][17] = y == 18 ? 'T' : 'H';
                }
                StageSketch.Box(grid, 8, 8, 1, 5, '#');
                StageSketch.Box(grid, 23, 23, 1, 5, '#');
                StageSketch.Box(grid, 8, 8, 20, 24, '#');
                StageSketch.Box(grid, 23, 23, 20, 24, '#');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(2);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 5, 11f, 1);
            b.BubbleAt(BubbleKind.Trigloop, StageBuilder.TierXL, 27, 29f, 1);
        }
    }
}
