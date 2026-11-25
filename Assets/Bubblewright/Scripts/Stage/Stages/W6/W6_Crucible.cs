using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 6 difficulty 4. The pre-boss gauntlet: a four-slot bunker, four spike strips and two reinforcement waves.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W6, D4, MinStageInWorld 4, MaxStageInWorld 5.
    /// Concept: the last stop before the lava boss. Hold the bunker under four slots while spikes cycle inside it and two
    ///   waves drop in; break out through its feet when the fight spills into the wings.
    /// Archetype(s): Slotted Bunker (W1_SlottedBunker). Structure: mirror-symmetric about x = 0 (the bubbles are not).
    /// Enemies: two ceramic XL Scorches over the outer slots (11 and 20) and a Trigloop L over slot 17, all on the roof.
    ///   Wave 1: an L Scorch. Wave 2: two M Trigloops. The waves fall from the ceiling, never within 4 u of Wick.
    /// Pops: 16 + 16 + 13 + 7 + 4 + 4 = 60 (D4 band 55-90); shortest hard clock for slots 4-5 in W6 = 84 s -> max 134 pops.
    /// Hitless plan (Moss, Heavy Boots): start between the inner strips. Shoot the Trigloop L through slot 17, then each
    ///   ceramic XL twice through slots 11 and 20, crossing the inner strips (12-13 and 18-19) only while they are down.
    ///   Chip a breakable foot (column 9 or 22, two hits per tile) to go out for children that roll off the roof into the
    ///   wings, where the outer strips (2-4 and 27-29) cycle. Come back in under the bunker for the waves.
    /// Over-break check: with all four B feet gone the walls hang from the roof, leaving a two-row doorway on each side;
    ///   nothing is stranded and no spot is a pit.
    /// </remarks>
    [Preserve]
    public sealed class W6_Crucible : StageSpec
    {
        public override string Id => "w6_crucible";
        public override string Name => "Crucible";
        public override int World => 6;
        public override int Difficulty => 4;
        public override int MinStageInWorld => 4;
        public override string Concept =>
            "Hold a four-slot bunker with spikes cycling inside while ceramic Scorches burn the roof and two waves drop in.";

        public override string[] Rows
        {
            get
            {
                // Roof on row 4 from column 9 to 22, slots on 11, 14, 17 and 20. Walls on 9 and 22 with breakable feet.
                var grid = StageSketch.Open14();
                StageSketch.Box(grid, 9, 22, 4, 4, '#');
                grid[4][11] = '.';
                grid[4][14] = '.';
                grid[4][17] = '.';
                grid[4][20] = '.';
                StageSketch.Box(grid, 9, 9, 2, 3, '#');
                StageSketch.Box(grid, 22, 22, 2, 3, '#');
                StageSketch.Box(grid, 9, 9, 0, 1, 'B');
                StageSketch.Box(grid, 22, 22, 0, 1, 'B');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(15);
            // Inside strips first, so they rise 0.8 s apart; the wing strips follow.
            b.Spike(12, 2);
            b.Spike(18, 2);
            b.Spike(2, 3);
            b.Spike(27, 3);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierXL, 11, 8.5f, -1, ceramic: true);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierXL, 20, 8.5f, 1, ceramic: true);
            b.BubbleAt(BubbleKind.Trigloop, StageBuilder.TierL, 17, 11.5f, 1);
            b.Wave(0, BubbleKind.Scorch, StageBuilder.TierL, -8f, 1);
            b.Wave(1, BubbleKind.Trigloop, StageBuilder.TierM, -8f, 1);
            b.Wave(1, BubbleKind.Trigloop, StageBuilder.TierM, 8f, -1);
        }
    }
}
