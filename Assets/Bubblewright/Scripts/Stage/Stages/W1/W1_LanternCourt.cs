using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 1 difficulty 2. Four raised pads, one Gloop over each.</summary>
    [Preserve]
    public sealed class W1_LanternCourt : StageSpec
    {
        public override string Id => "w1_lantern_court";
        public override string Name => "Lantern Court";
        public override int World => 1;
        public override int Difficulty => 2;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "Four lantern pads stand in a row. One large Gloop hangs over each, and Wick hops the pads to line up a shot.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open14();
                StageSketch.Box(grid, 1, 4, 0, 0, '#');
                StageSketch.Box(grid, 9, 12, 0, 0, '#');
                StageSketch.Box(grid, 19, 22, 0, 0, '#');
                StageSketch.Box(grid, 27, 30, 0, 0, '#');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(16);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierL, 2, 8f, -1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierL, 10, 8f, -1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierL, 21, 8f, 1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierL, 29, 8f, 1);
        }
    }
}
