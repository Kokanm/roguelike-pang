using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 1 difficulty 3. Three stacked roofs. Wick only climbs, and each roof has its own Gloop.</summary>
    [Preserve]
    public sealed class W1_ThreeRoofs : StageSpec
    {
        public override string Id => "w1_three_roofs";
        public override string Name => "Three Roofs";
        public override int World => 1;
        public override int Difficulty => 3;
        public override int MinStageInWorld => 3;
        public override int Storeys => 3;
        public override string Concept =>
            "Three roofs stack on a left-hand ladder. Pop the Gloop on the roof you are standing on; a parapet closes the right side of the upper two.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open(3);
                StageSketch.Box(grid, 0, 31, 18, 18, '#');
                StageSketch.Box(grid, 0, 31, 36, 36, '#');
                for (int y = 0; y <= 36; y++) grid[y][4] = y == 18 || y == 36 ? 'T' : 'H';
                StageSketch.Box(grid, 20, 31, 20, 20, '#');
                StageSketch.Box(grid, 20, 31, 38, 38, '#');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(12);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 22, 11f, 1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 16, 30f, 1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 16, 48f, 1);
        }
    }
}
