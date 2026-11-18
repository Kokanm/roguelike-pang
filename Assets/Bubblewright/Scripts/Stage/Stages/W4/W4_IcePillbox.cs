using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 4 difficulty 2. A normal-grip bunker with two firing slots, flanked by ice lanes.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W4, D2, MinStageInWorld 2, MaxStageInWorld 5.
    /// Concept: Wick fires through two roof slots of a grippy bunker. Outside, Frost bubbles bounce over wide ice lanes.
    /// Archetype(s): Slotted Bunker. Structure: mirror-symmetric about x = 0. Bunker walls on 9 and 22, roof on row 4,
    /// slots on 12 and 19, B feet on both walls. Ground: ice 0-8 and 23-31, normal 9-22.
    /// Enemies: two Frost XL start over the slots (the reach check and the first shots both use them); one Frost M on
    /// each ice lane so Wick has a reason to chip out and skate.
    /// Pops: 15 + 15 + 3 + 3 = 36 (D2 band 20-40); shortest hard clock for slots 2-5 in W4 = 66 s -> max 105.
    /// Hitless plan (Moss, Heavy Boots): stand under slot 12 or 19 and fire as an XL crosses; the L children bounce on
    /// the roof and cross the slots too. When the roof is quiet, chip a B foot (2 hits each, rows 0-1), step out onto
    /// the ice lane, let the M come to you and shoot it; the lane is open to the ceiling.
    /// Over-break check: with every B gone the walls hang from row 2 and Wick walks in and out freely. Nothing strands.
    /// </remarks>
    [Preserve]
    public sealed class W4_IcePillbox : StageSpec
    {
        public override string Id => "w4_ice_pillbox";
        public override string Name => "Ice Pillbox";
        public override int World => 4;
        public override int Difficulty => 2;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "Wick fires through two roof slots of a grippy bunker. Outside, Frost bubbles bounce over wide ice lanes.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 9, 22, 4, 4, '#');
                grid[4][12] = '.';
                grid[4][19] = '.';
                StageSketch.Box(grid, 9, 9, 2, 3, '#');
                StageSketch.Box(grid, 22, 22, 2, 3, '#');
                StageSketch.Box(grid, 9, 9, 0, 1, 'B');
                StageSketch.Box(grid, 22, 22, 0, 1, 'B');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => "IIIIIIIII" + "NNNNNNNNNNNNNN" + "IIIIIIIII";

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.BubbleAt(BubbleKind.Frost, StageBuilder.TierXL, 12, 9f, -1);
            b.BubbleAt(BubbleKind.Frost, StageBuilder.TierXL, 19, 9f, 1);
            b.BubbleAt(BubbleKind.Frost, StageBuilder.TierM, 4, 6f, -1);
            b.BubbleAt(BubbleKind.Frost, StageBuilder.TierM, 27, 6f, 1);
        }
    }
}
