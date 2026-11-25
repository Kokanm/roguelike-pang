using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 4 difficulty 3. A one-row ice slide under a deck, between two wall-jump chimneys.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W4, D3, MinStageInWorld 2, MaxStageInWorld 5.
    /// Concept: Crouch-slide the icy duct under the deck and fire up through its slots, or wall-jump a chimney to the deck
    /// where two Clinger Pogo guests wait.
    /// Archetype(s): Crouch Duct Sniping + Wall-jump well. Structure: mirror-symmetric about x = 0. Deck on row 1 from 6 to
    /// 25 with 1-tile slots on 10 and 21; the duct under it is ice. One-column chimneys on 5 and 26 (walls 4/6 and 25/27,
    /// rows 1-8) climbed by chained wall jumps. Crouch gaps on row 0 of columns 4 and 27 lead in from the flanks.
    /// Enemies: two Frost XL start over the slots, moving outward. Guest: two patient floor Clinger S Pogos on the deck
    /// (13 and 18); passing under them in the duct wakes them, they leap to the ceiling and drop back onto the deck.
    /// One Frost L in the right flank gives the far side a job.
    /// Pops: 15 + 15 + 1 + 1 + 7 = 39 (D3 band 35-65); shortest hard clock for slots 2-5 in W4 = 66 s -> max 105.
    /// Hitless plan (Moss, Heavy Boots): from the left flank crouch through column 4 into the chimney, shuffle along the
    /// duct to slot 10 or 21 (standing room there) and fire up at the XLs and their children. To reach the deck, stand in
    /// a chimney, wall-jump up to row 8, hop onto the wall top (row 9) and drop to the deck; S Pogos are jumped over or
    /// woken from 3 u away and shot as they hang. Back down through a slot. The right flank is reached through the duct
    /// and the crouch gap on 27.
    /// Over-break check: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W4_FrozenSlide : StageSpec
    {
        public override string Id => "w4_frozen_slide";
        public override string Name => "Frozen Slide";
        public override int World => 4;
        public override int Difficulty => 3;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "Crouch-slide the icy duct under the deck and fire up through its slots, or wall-jump a chimney to the deck where two Clinger Pogo guests wait.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open14();
                StageSketch.Box(grid, 6, 25, 1, 1, '#');
                grid[1][10] = '.';
                grid[1][21] = '.';
                StageSketch.Box(grid, 4, 4, 1, 8, '#');
                StageSketch.Box(grid, 6, 6, 1, 8, '#');
                StageSketch.Box(grid, 25, 25, 1, 8, '#');
                StageSketch.Box(grid, 27, 27, 1, 8, '#');
                return StageSketch.Lines(grid);
            }
        }

        // Normal ground in the flanks, ice from the crouch gaps through the duct.
        public override string Ground => "NNNN" + new string('I', 24) + "NNNN";

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(1);
            b.BubbleAt(BubbleKind.Frost, StageBuilder.TierXL, 10, 9f, -1);
            b.BubbleAt(BubbleKind.Frost, StageBuilder.TierXL, 21, 9f, 1);
            b.FloorClinger(StageBuilder.TierS, StageMap.CenterX(13), 2f);
            b.FloorClinger(StageBuilder.TierS, StageMap.CenterX(18), 2f);
            b.BubbleAt(BubbleKind.Frost, StageBuilder.TierL, 30, 8f, 1);
        }
    }
}
