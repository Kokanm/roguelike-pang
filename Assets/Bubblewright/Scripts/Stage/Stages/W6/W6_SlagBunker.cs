using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 6 difficulty 2. A slotted bunker on a raised plinth, with spike strips on the outer wings.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W6, D2, MinStageInWorld 2, MaxStageInWorld 5.
    /// Concept: big Scorches burn the bunker roof, not Wick; he snipes through two slots from the raised plinth.
    /// Archetype(s): Slotted Bunker. Structure: mirror-symmetric about x = 0.
    /// Enemies: two XL Scorches start over the slots on columns 13 and 18, so the first shot is always lined up. Their
    ///   flames sit on the roof. L children roll off the roof ends into the spiked wings; M children slip in through the
    ///   two-row mouths and bounce on the plinth.
    /// Pops: 15 + 15 = 30 (D2 band 20-40); shortest hard clock for slots 2-5 in W6 = 78 s -> max 124 pops.
    /// Hitless plan (Moss, Heavy Boots): shoot each XL through its slot from the plinth. Walk out under a lip (two rows,
    ///   standing), hop down off the plinth edge, and take the L children in a wing while its strip is down; hop back up
    ///   the one-row plinth edge to shelter.
    /// Over-break check: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W6_SlagBunker : StageSpec
    {
        public override string Id => "w6_slag_bunker";
        public override string Name => "Slag Bunker";
        public override int World => 6;
        public override int Difficulty => 2;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "Wick snipes two big Scorches through the slots of a bunker on a raised plinth. Their flames burn the roof; the wings have spikes.";

        public override string[] Rows
        {
            get
            {
                // Plinth one row high on columns 9-22, so Wick stands at row 1 above the floor flames.
                // Roof on row 5 (columns 10-21) with slots on 13 and 18. The lips on 10 and 21 hang down to row 3,
                // leaving a two-row mouth: Wick walks out standing, M and S get in, L and XL stay on the roof.
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 9, 22, 0, 0, '#');
                StageSketch.Box(grid, 10, 21, 5, 5, '#');
                StageSketch.Box(grid, 10, 10, 3, 4, '#');
                StageSketch.Box(grid, 21, 21, 3, 4, '#');
                grid[5][13] = '.';
                grid[5][18] = '.';
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(15, 1);
            b.Spike(2, 5);
            b.Spike(25, 5);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierXL, 13, 10f, -1);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierXL, 18, 10f, 1);
        }
    }
}
