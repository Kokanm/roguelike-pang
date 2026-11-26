using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 1 difficulty 3. A breakable box with two Gloops locked inside.</summary>
    [Preserve]
    public sealed class W1_BreakVault : StageSpec
    {
        public override string Id => "w1_break_vault";
        public override string Name => "Break Vault";
        public override int World => 1;
        public override int Difficulty => 3;
        public override int MinStageInWorld => 3;
        public override string Concept =>
            "Two huge Gloops bounce in the open. Two more sit in a breakable vault: chip a side wall, step in, and pop them.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 11, 11, 0, 4, 'B');
                StageSketch.Box(grid, 20, 20, 0, 4, 'B');
                StageSketch.Box(grid, 12, 19, 4, 4, 'B');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(7);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 3, 11f, -1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 28, 11f, 1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierL, 14, 2.5f, -1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierL, 17, 2.5f, 1);
        }
    }
}
