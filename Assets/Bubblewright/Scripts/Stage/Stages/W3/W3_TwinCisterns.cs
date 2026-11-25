using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 3 difficulty 2. Two sealed cisterns, each reached by crouching under a lip and wall-jumping up a well.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W3, D2, MinStageInWorld 2, MaxStageInWorld 5.
    /// Concept: a Sinker waits in a high cistern at each end; crouch under the lip and wall-jump up the narrow well to it.
    /// Archetype(s): Wall-jump well (W1_WellShaft), doubled and mirrored.
    /// Structure: mirror-symmetric about x = 0. Cistern floors are blocks on columns 0-4 and 27-31, rows 0-6 (feet 7).
    ///   The wells are columns 5 and 26. Their inner walls (columns 6 and 25) run from row 1 to the ceiling, so the
    ///   cisterns are closed rooms: a Sinker can only settle on a cistern floor, where Wick stands. One row under each
    ///   wall is the crouch entrance.
    /// Enemies and why each one is where it is: one Sinker L in each cistern (the puzzle), Gloop XL in the open court
    ///   heading away from Wick. No guest.
    /// Pops: 7 + 7 + 15 = 29 (D2 band 20-40); shortest hard clock for slots 2-5 in W3 = 69 s -> max 110 pops.
    /// Hitless plan for Moss with Heavy Boots and the default harpoon: start in the court on column 15 and pop the XL
    ///   family there. Crouch under the left wall at column 6 into the well, wall-jump up column 5 one row at a time,
    ///   and step onto the cistern floor at feet 7. Shoot the Sinker while it bounces; once it rolls, fire on column 4
    ///   and drop back into the well, and it rolls into the cable when it turns at the well's edge. S children fall
    ///   down the well and out of the crouch entrance into the court. Repeat on the right.
    /// Over-break check: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W3_TwinCisterns : StageSpec
    {
        public override string Id => "w3_twin_cisterns";
        public override string Name => "Twin Cisterns";
        public override int World => 3;
        public override int Difficulty => 2;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "A Sinker waits in a high cistern at each end. Crouch under the lip and wall-jump up the narrow well to reach it.";

        public override string[] Rows
        {
            get
            {
                // Wells on columns 5 and 26, no ladders: the way up is chained wall jumps.
                var grid = StageSketch.Open14();
                StageSketch.Box(grid, 0, 4, 0, 6, '#');
                StageSketch.Box(grid, 6, 6, 1, 13, '#');
                StageSketch.Box(grid, 27, 31, 0, 6, '#');
                StageSketch.Box(grid, 25, 25, 1, 13, '#');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(15);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 21, 9f, 1);
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 2, 10f, 1);
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 29, 10f, -1);
        }
    }
}
