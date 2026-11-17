using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 1 difficulty 2. An open court and a one-column wall-jump well.</summary>
    [Preserve]
    public sealed class W1_WellShaft : StageSpec
    {
        public override string Id => "w1_well_shaft";
        public override string Name => "Well Shaft";
        public override int World => 1;
        public override int Difficulty => 2;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "One Gloop bounces in the open court. The other sits on the right roof, and the way up is a ladder inside a narrow well.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 25, 25, 1, 6, '#');
                StageSketch.Box(grid, 27, 27, 1, 6, '#');
                StageSketch.Box(grid, 22, 31, 6, 6, '#');
                for (int y = 0; y <= 5; y++) grid[y][26] = 'H';
                grid[6][26] = 'T';
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(3);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 8, 9f, 1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 29, 10f, 1);
        }
    }
}
