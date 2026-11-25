using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 1 difficulty 2. A wall splits the roof; the only way across is a crouch tunnel under it.</summary>
    [Preserve]
    public sealed class W1_Crawlway : StageSpec
    {
        public override string Id => "w1_crawlway";
        public override string Name => "Crawlway";
        public override int World => 1;
        public override int Difficulty => 2;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "A wall splits the roof into two courts. Wick crawls through the one-row tunnel under it and fires up through its two slots.";

        public override string[] Rows
        {
            get
            {
                // Tunnel under row 1 from column 10 to 21, firing slots at 12 and 19, divider wall up to the ceiling on 15 and 16.
                var grid = StageSketch.Open14();
                StageSketch.Box(grid, 10, 21, 1, 1, '#');
                grid[1][12] = '.';
                grid[1][19] = '.';
                StageSketch.Box(grid, 15, 16, 2, 13, '#');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(8);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 3, 9f, -1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 28, 9f, 1);
        }
    }
}
