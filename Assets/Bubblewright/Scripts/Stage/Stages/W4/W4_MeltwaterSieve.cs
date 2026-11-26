using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 4 difficulty 4. An icy deck with three two-tile holes; Sinker guests roll on it, Frost XL bounce above.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W4, D4, MinStageInWorld 4, MaxStageInWorld 5.
    /// Concept: Two Frost XL bounce on an icy deck with three two-tile holes; their small children rain into Wick's court.
    /// Sinker guests settle and roll along the deck.
    /// Archetype(s): Sieve Ceiling. Structure: mirror-symmetric about x = 0. Deck on row 6 (ice), holes on 6-7, 15-16 and
    /// 24-25, ladders on 2 and 29 up to T ends. Six rows of court under the deck, seven above it.
    /// Enemies: two Frost XL over the outer holes, moving outward; guest: two Sinker L on the deck, which lose their
    /// bounce and then roll along it, turning at the hole edges (they never fall through a 2-tile hole); one Frost L in
    /// each end of the court, moving outward.
    /// Pops: 15 + 15 + 7 + 7 + 7 + 7 = 58 (D4 band 55-90); shortest hard clock for slots 4-5 in W4 = 72 s -> max 115.
    /// Hitless plan (Moss, Heavy Boots): from the court shoot the Frost Ls as they come back toward the middle, then
    /// fire up the holes at the XLs and Sinkers when they cross (the holes are 2 columns wide, so stand in one).
    /// M and S children drop through the holes into the court. Climb a ladder for whatever stays on the deck; jump the
    /// rolling Sinkers or shoot them from the deck beside a hole.
    /// Over-break check: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W4_MeltwaterSieve : StageSpec
    {
        public override string Id => "w4_meltwater_sieve";
        public override string Name => "Meltwater Sieve";
        public override int World => 4;
        public override int Difficulty => 4;
        public override int MinStageInWorld => 4;
        public override string Concept =>
            "Two Frost XL bounce on an icy deck with three two-tile holes; their small children rain into Wick's court. Sinker guests settle and roll along the deck.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 0, 31, 6, 6, 'I');
                StageSketch.Box(grid, 6, 7, 6, 6, '.');
                StageSketch.Box(grid, 15, 16, 6, 6, '.');
                StageSketch.Box(grid, 24, 25, 6, 6, '.');
                StageSketch.Box(grid, 2, 2, 0, 5, 'H');
                StageSketch.Box(grid, 29, 29, 0, 5, 'H');
                grid[6][2] = 'T';
                grid[6][29] = 'T';
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.BubbleAt(BubbleKind.Frost, StageBuilder.TierXL, 7, 10.5f, -1);
            b.BubbleAt(BubbleKind.Frost, StageBuilder.TierXL, 24, 10.5f, 1);
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 11, 10f, -1);
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 20, 10f, 1);
            b.BubbleAt(BubbleKind.Frost, StageBuilder.TierL, 3, 3f, -1);
            b.BubbleAt(BubbleKind.Frost, StageBuilder.TierL, 28, 3f, 1);
        }
    }
}
