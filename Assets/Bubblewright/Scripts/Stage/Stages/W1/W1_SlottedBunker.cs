using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 1 difficulty 2. A roofed bunker with three one-tile slots.</summary>
    [Preserve]
    public sealed class W1_SlottedBunker : StageSpec
    {
        public override string Id => "w1_slotted_bunker";
        public override string Name => "Slotted Bunker";
        public override int World => 1;
        public override int Difficulty => 2;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "Wick starts under a roof with three one-tile slots. Two huge Gloops bounce overhead until a shot through a slot splits them.";

        // Roof slots at columns 12, 16 and 20. Walls at 9 and 23, with breakable feet so he can step out.
        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open14();
                StageSketch.Box(grid, 9, 11, 4, 4, '#');
                StageSketch.Box(grid, 13, 15, 4, 4, '#');
                StageSketch.Box(grid, 17, 19, 4, 4, '#');
                StageSketch.Box(grid, 21, 23, 4, 4, '#');
                StageSketch.Box(grid, 9, 9, 2, 3, '#');
                StageSketch.Box(grid, 23, 23, 2, 3, '#');
                StageSketch.Box(grid, 9, 9, 0, 1, 'B');
                StageSketch.Box(grid, 23, 23, 0, 1, 'B');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 12, 9f, -1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 20, 9f, 1);
        }
    }
}
