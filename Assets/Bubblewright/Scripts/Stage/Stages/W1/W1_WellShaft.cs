using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 1 difficulty 2. An open court and a one-column well that Wick climbs with chained wall jumps.</summary>
    [Preserve]
    public sealed class W1_WellShaft : StageSpec
    {
        public override string Id => "w1_well_shaft";
        public override string Name => "Well Shaft";
        public override int World => 1;
        public override int Difficulty => 2;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "One Gloop bounces in the open court. The other is penned on the right roof: crouch under the well's lip and wall-jump up the narrow well to reach it.";

        public override string[] Rows
        {
            get
            {
                // The well is column 26. Its left lip (column 25) stops one row above the ground, so Wick crouches in.
                // The lip rises to row 10 as the pen's parapet: three rows to the ceiling hold the XL, its L children get out.
                // The block on columns 27-31 is the roof the XL bounces on. No ladder: the way up is chained wall jumps.
                var grid = StageSketch.Open14();
                StageSketch.Box(grid, 25, 25, 1, 10, '#');
                StageSketch.Box(grid, 27, 31, 0, 6, '#');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(3);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 9, 9f, 1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 29, 10f, 1);
        }
    }
}
