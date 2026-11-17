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
            "Clear the Gloop between the lower pillars, climb the ladder through the hatch, and pop the Trigloop in the belfry.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open(2);
                StageSketch.Box(grid, 0, 31, 14, 14, '#');
                for (int y = 0; y <= 14; y++) grid[y][16] = y == 14 ? 'T' : 'H';
                StageSketch.Box(grid, 8, 8, 1, 4, '#');
                StageSketch.Box(grid, 24, 24, 1, 4, '#');
                StageSketch.Box(grid, 8, 8, 16, 19, '#');
                StageSketch.Box(grid, 24, 24, 16, 19, '#');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(2);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 6, 8f, 1);
            b.BubbleAt(BubbleKind.Trigloop, StageBuilder.TierXL, 26, 22f, 1);
        }
    }
}
