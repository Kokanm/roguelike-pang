using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 2 difficulty 3. Leads bounce on a sieve deck; Clingers hang under it over Wick's court.</summary>
    /// <remarks>
    /// Stage Design Brief
    /// World / Difficulty / slots: W2, D3, MinStageInWorld 2, MaxStageInWorld 5
    /// Concept: Two big Leads bounce on a canopy deck with two-tile holes, raining small children through. Two Clingers
    ///   hang under it, waiting for Wick.
    /// Archetype(s): Sieve Ceiling (W1_TrigloopSieve) with under-deck Clingers
    /// Structure: mirror-symmetric about x = 0. A deck on row 6 across the whole width, 2-tile holes on columns 9-10 and
    ///   21-22, ladders on columns 4 and 27. Above: the Leads' court. Below: Wick's court, 6 rows high.
    /// Enemies and why each one is where it is: XL Leads start on the deck over columns 13 / 18, heading outward; the
    ///   2-tile holes hold XL and L, so only their M and S children rain down. Patient L Clingers hang under the deck at
    ///   columns 12 / 19, 3.5 u from Wick's start: each wakes when he steps under it to shoot a falling child.
    /// Pops: 15 + 15 + 7 + 7 = 44 (D3 band 35-65); shortest hard clock for slots 2-5 in W2 = 54 s -> max 86.
    /// Hitless plan for Moss with Heavy Boots and the default harpoon:
    ///   1. Start at x = 0 under the deck; the Leads are above a solid roof, the Clingers 3.5 u to each side.
    ///   2. Step under each Clinger in turn and fire at once (it hangs at 4.75 u; the harpoon gets there in about 0.2 s).
    ///      Shoot its children where they stick, under the deck or on the floor.
    ///   3. Climb a ladder (column 4 / 27) to the deck when a Lead is bouncing on the far side, and pop the XLs and their
    ///      L children from the deck (7 rows of headroom up there). Jump the 2-tile holes or drop through them.
    ///   4. M and S children that fall through the holes are shot in the lower court.
    /// Over-break check: no B tiles. The ladders and holes link both courts both ways.
    /// </remarks>
    [Preserve]
    public sealed class W2_BoughSieve : StageSpec
    {
        public override string Id => "w2_bough_sieve";
        public override string Name => "Bough Sieve";
        public override int World => 2;
        public override int Difficulty => 3;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "Two big Leads bounce on a canopy deck with two-tile holes, raining small children through. Two Clingers hang under it, waiting for Wick.";

        public override string[] Rows
        {
            get
            {
                // Deck on row 6, 2-tile holes on 9-10 and 21-22 (they hold XL and L, FilterGapTests), ladders on 4 and 27.
                var grid = StageSketch.Open14();
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
            b.Wick(0f);
            b.BubbleAt(BubbleKind.Lead, StageBuilder.TierXL, 13, 10f, -1);
            b.BubbleAt(BubbleKind.Lead, StageBuilder.TierXL, 18, 10f, 1);
            b.Clinger(StageBuilder.TierL, StageMap.CenterX(12), 4f, dir: -1);
            b.Clinger(StageBuilder.TierL, StageMap.CenterX(19), 4f, dir: 1);
        }
    }
}
