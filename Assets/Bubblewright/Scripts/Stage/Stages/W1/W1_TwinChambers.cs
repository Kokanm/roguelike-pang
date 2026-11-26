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
                // Divider on columns 15 and 16, a ladder on each face, and a bridge on row 11. Two rows above the bridge
                // let Wick and M or S bubbles cross, never an L or an XL.
                var grid = StageSketch.Open14();
                StageSketch.Box(grid, 15, 16, 0, 10, '#');
                StageSketch.Box(grid, 14, 14, 0, 10, 'H');
                StageSketch.Box(grid, 17, 17, 0, 10, 'H');
                StageSketch.Box(grid, 14, 17, 11, 11, '#');
                grid[11][14] = 'T';
                grid[11][17] = 'T';
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
