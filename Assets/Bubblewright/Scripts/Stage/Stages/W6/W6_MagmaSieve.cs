using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 6 difficulty 3. A deck with two two-tile holes over a low court with two spike strips.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W6, D3, MinStageInWorld 2, MaxStageInWorld 5.
    /// Concept: big Scorches burn the deck while their M children rain through the holes into a spiked court.
    /// Archetype(s): Sieve Ceiling (W1_TrigloopSieve). Structure: mirror-symmetric about x = 0.
    /// Enemies: an XL Scorch on each half of the deck, moving outward. Two-tile holes on 9-10 and 21-22 hold their L
    ///   children and let the M ones through. Two Lead L bounce low in the six-row court under the deck, moving outward.
    /// Pops: 15 + 15 + 7 + 7 = 44 (D3 band 35-65); shortest hard clock for slots 2-5 in W6 = 78 s -> max 124 pops.
    /// Hitless plan (Moss, Heavy Boots): pop the Lead L pair from the court's middle (columns 14-17, between the strips).
    ///   Climb the ladder on column 4 (or 27) to the deck, shoot that XL and its L children from the deck, jump the
    ///   two-column hole to cross. Drop back into the court for the M children that fell through, stepping round the
    ///   strips while they are up.
    /// Over-break check: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W6_MagmaSieve : StageSpec
    {
        public override string Id => "w6_magma_sieve";
        public override string Name => "Magma Sieve";
        public override int World => 6;
        public override int Difficulty => 3;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "Two big Scorches burn a deck with two holes; only their M children fall through into a low court with spike strips.";

        public override string[] Rows
        {
            get
            {
                // Deck on row 6, holes on columns 9-10 and 21-22, ladders on 4 and 27 (as W1_TrigloopSieve).
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 0, 31, 6, 6, '#');
                StageSketch.Box(grid, 9, 10, 6, 6, '.');
                StageSketch.Box(grid, 21, 22, 6, 6, '.');
                StageSketch.Box(grid, 4, 4, 0, 5, 'H');
                StageSketch.Box(grid, 27, 27, 0, 5, 'H');
                grid[6][4] = 'T';
                grid[6][27] = 'T';
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(15);
            b.Spike(11, 3);
            b.Spike(18, 3);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierXL, 6, 10f, -1);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierXL, 25, 10f, 1);
            b.BubbleAt(BubbleKind.Lead, StageBuilder.TierL, 7, 3.5f, -1);
            b.BubbleAt(BubbleKind.Lead, StageBuilder.TierL, 24, 3.5f, 1);
        }
    }
}
