using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 1 difficulty 2. Two courts split by a tall wall and joined by a bridge.</summary>
    [Preserve]
    public sealed class W1_TwinChambers : StageSpec
    {
        public override string Id => "w1_twin_chambers";
        public override string Name => "Twin Chambers";
        public override int World => 1;
        public override int Difficulty => 2;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "A tall wall splits the roof into twin courts. Clear the near Gloop, climb either ladder, cross the bridge, and clear the far one.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 16, 16, 0, 10, '#');
                StageSketch.Box(grid, 14, 14, 0, 9, 'H');
                StageSketch.Box(grid, 18, 18, 0, 9, 'H');
                StageSketch.Box(grid, 13, 19, 10, 10, '#');
                grid[10][14] = 'T';
                grid[10][18] = 'T';
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(6);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 4, 8f, -1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 27, 8f, 1);
        }
    }
}
