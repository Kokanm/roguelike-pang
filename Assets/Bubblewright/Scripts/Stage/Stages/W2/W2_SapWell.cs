using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 2 difficulty 2. Twin wall-jump wells lead up to high pens, each with a patient Clinger.</summary>
    /// <remarks>
    /// Stage Design Brief
    /// World / Difficulty / slots: W2, D2, MinStageInWorld 2, MaxStageInWorld 5
    /// Concept: A patient Clinger waits over each high pen. Crouch under the lip into the narrow well and wall-jump up
    ///   to reach it, while Leads bounce below.
    /// Archetype(s): Wall-jump well (W1_WellShaft), twice
    /// Structure: mirror-symmetric about x = 0. A solid block (columns 0-4 / 27-31, rows 0-6) is each pen's floor. The
    ///   well is column 5 / 26, between the block and a lip (column 6 / 25) that rises from row 1 to row 10: Wick
    ///   crouches under the lip's foot into the well and climbs it with chained wall jumps (no ladder).
    /// Enemies and why each one is where it is: an L Clinger on the ceiling over column 2 / 29, only shootable from the
    ///   pen top (y = 7, 7 rows of headroom). Two L Leads start at columns 11 / 20 heading outward; their peak is 7.7 u,
    ///   so they stay in the court and never reach the pens over the lip.
    /// Pops: 7 + 7 + 7 + 7 = 28 (D2 band 20-40); shortest hard clock for slots 2-5 in W2 = 54 s -> max 86.
    /// Hitless plan for Moss with Heavy Boots and the default harpoon:
    ///   1. Start at x = 0; the Leads are 4.5 u away going outward, and bounce back off the lips.
    ///   2. Pop the Leads and their children in the court (they are fast and low: fire as one comes in, then sidestep).
    ///   3. Walk to column 7, crouch under the lip into the well (column 5), wall-jump up six rows and step left onto the
    ///      pen top at column 4. The Clinger is 2 u to the left: walk under it and fire at once (it wakes at 1.5 u, the
    ///      harpoon reaches it in under 0.2 s). Shoot its M and S children in the pen or over the court.
    ///   4. Drop back down the well, crouch out, and do the right pen.
    /// Over-break check: no B tiles. From the pen Wick drops into the well and crouches out, so nothing traps him.
    /// </remarks>
    [Preserve]
    public sealed class W2_SapWell : StageSpec
    {
        public override string Id => "w2_sap_well";
        public override string Name => "Sap Well";
        public override int World => 2;
        public override int Difficulty => 2;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "A patient Clinger waits over each high pen. Crouch under the lip into the narrow well and wall-jump up to reach it, while Leads bounce below.";

        public override string[] Rows
        {
            get
            {
                // Pen blocks on 0-4 and 27-31 (rows 0-6). Wells on columns 5 and 26. Lips on 6 and 25 start one row up,
                // so Wick crouches in, and rise to row 10 so the Leads stay in the court.
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 0, 4, 0, 6, '#');
                StageSketch.Box(grid, 27, 31, 0, 6, '#');
                StageSketch.Box(grid, 6, 6, 1, 10, '#');
                StageSketch.Box(grid, 25, 25, 1, 10, '#');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.BubbleAt(BubbleKind.Lead, StageBuilder.TierL, 11, 6f, -1);
            b.BubbleAt(BubbleKind.Lead, StageBuilder.TierL, 20, 6f, 1);
            b.Clinger(StageBuilder.TierL, StageMap.CenterX(2), 12f, dir: 1);
            b.Clinger(StageBuilder.TierL, StageMap.CenterX(29), 12f, dir: -1);
        }
    }
}
