using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 6 difficulty 4. Two storeys: lava gaps and spikes below, Scorch and Ghost guests above.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W6, D4, MinStageInWorld 4, MaxStageInWorld 5.
    /// Concept: climb the caldera. Below, cross two lava gaps and two spike strips to the twin ladders; above, two shelves
    ///   shelter Wick from the Scorch but not from the Ghosts (the guest), which pass straight through them.
    /// Archetype(s): Storey Tower Climb (W1_BellTower) + Gaps and Pogos (W1_PogoSteps). Structure: storey 1 is
    ///   mirror-symmetric; storey 2 is symmetric except where its three bubbles start.
    /// Enemies, storey 1: an XL Scorch over the left spike strip and a Trigloop L over the right one, both moving
    ///   right; an M Gloop Pogo on each landing past a gap (columns 12 and 19), as in W1_PogoSteps.
    /// Enemies, storey 2: an XL Scorch and two L Ghosts, all starting over open floor away from the hatches and
    ///   moving away from them.
    /// Pops: 15 + 13 + 3 + 3 = 34 on storey 1, 15 + 7 + 7 = 29 on storey 2, 63 in all (D4 band 55-90); shortest hard
    ///   clock for slots 4-5 in W6 = 84 s -> max 134 pops.
    /// Hitless plan (Moss, Heavy Boots): pop the XL Scorch from the left ground, off its strip. Jump the two-column gap
    ///   (column 8 to 11) when the Pogo on 12 is low, shoot it, cross the middle, shoot the Pogo on 19, jump to 23, pop the
    ///   Trigloop L between strip cycles. Climb ladder 14 or 17 through its hatch. Upstairs, wait under a shelf while the
    ///   Scorch is high, shoot the Ghosts only while they are solid.
    /// Over-break check: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W6_CalderaClimb : StageSpec
    {
        public override string Id => "w6_caldera_climb";
        public override string Name => "Caldera Climb";
        public override int World => 6;
        public override int Difficulty => 4;
        public override int MinStageInWorld => 4;
        public override int Storeys => 2;
        public override string Concept =>
            "Cross lava gaps and spikes to the twin ladders, then fight a Scorch and two Ghost guests that drift through the shelves above.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open(2);
                StageSketch.Box(grid, 0, 31, 14, 14, '#');
                // Twin ladders on columns 14 and 17, each with its hatch on row 13.
                for (int y = 0; y <= 14; y++)
                {
                    grid[y][14] = y == 14 ? 'T' : 'H';
                    grid[y][17] = y == 14 ? 'T' : 'H';
                }
                // Storey 2 shelves on row 19: four rows of cover under each, which the Ghosts ignore.
                StageSketch.Box(grid, 2, 7, 19, 19, '#');
                StageSketch.Box(grid, 24, 29, 19, 19, '#');
                return StageSketch.Lines(grid);
            }
        }

        // Lava gaps on columns 9-10 and 21-22: two columns, what Moss with Heavy Boots clears from a standing jump.
        public override string Ground => "=========__==========__=========";

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(1);
            b.Spike(3, 4);
            b.Spike(25, 4);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierXL, 5, 9f, 1);
            b.BubbleAt(BubbleKind.Trigloop, StageBuilder.TierL, 27, 9f, 1);
            b.Bubble(BubbleKind.Gloop, StageBuilder.TierM, StageMap.CenterX(12), 0.85f, -1, pogo: true);
            b.Bubble(BubbleKind.Gloop, StageBuilder.TierM, StageMap.CenterX(19), 0.85f, 1, pogo: true);

            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierXL, 11, 24f, -1);
            b.BubbleAt(BubbleKind.Ghost, StageBuilder.TierL, 8, 22f, -1);
            b.BubbleAt(BubbleKind.Ghost, StageBuilder.TierL, 20, 22f, 1);
        }
    }
}
