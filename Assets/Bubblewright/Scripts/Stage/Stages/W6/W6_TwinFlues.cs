using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 6 difficulty 2. A spiked court between two one-column flues that Wick climbs with chained wall jumps.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W6, D2, MinStageInWorld 2, MaxStageInWorld 5.
    /// Concept: World 6's wall-jump stage. Each furnace block has a Scorch penned on its roof, reached only up a flue.
    /// Archetype(s): Wall-jump well (W1_WellShaft), twice. Structure: mirror-symmetric about x = 0.
    /// Enemies: an XL Scorch roams the court over the spike strips, moving away from Wick. An L Scorch is penned on each
    ///   block roof: the parapet (flue lip) leaves two rows to the ceiling, so the L stays penned and its M children
    ///   spill into the court.
    /// Pops: 15 + 7 + 7 = 29 (D2 band 20-40); shortest hard clock for slots 2-5 in W6 = 78 s -> max 124 pops.
    /// Hitless plan (Moss, Heavy Boots): pop the court XL and its children from between the strips. Crouch under the lip
    ///   on column 6 into the flue (column 5), wall-jump up rows 1-6, hop left onto the block roof at row 7, shoot the
    ///   penned L. Drop back into the flue, slide down, crouch out. Mirror for the right flue (lip 25, flue 26).
    /// Over-break check: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W6_TwinFlues : StageSpec
    {
        public override string Id => "w6_twin_flues";
        public override string Name => "Twin Flues";
        public override int World => 6;
        public override int Difficulty => 2;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "A Scorch is penned on each furnace roof. Crouch under a flue's lip and wall-jump up the narrow flue to reach it.";

        public override string[] Rows
        {
            get
            {
                // Furnace blocks on columns 0-4 and 27-31, rows 0-6: their roofs are at row 7.
                // Flues on columns 5 and 26. The lips on 6 and 25 start one row up (Wick crouches under them) and rise to
                // row 11 as the pens' parapets, leaving two rows to the ceiling: L stays in, M and S get out.
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 0, 4, 0, 6, '#');
                StageSketch.Box(grid, 6, 6, 1, 11, '#');
                StageSketch.Box(grid, 25, 25, 1, 11, '#');
                StageSketch.Box(grid, 27, 31, 0, 6, '#');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(15);
            b.Spike(9, 3);
            b.Spike(20, 3);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierXL, 11, 9f, -1);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierL, 2, 10.5f, 1);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierL, 29, 10.5f, -1);
        }
    }
}
