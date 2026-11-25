using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 4 difficulty 3. Two breakable igloos, each with a Frost L sealed inside.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W4, D3, MinStageInWorld 2, MaxStageInWorld 5.
    /// Concept: Two breakable igloos each hold a Frost L. Clear the big Frost overhead, then chip a door in and pop the one
    /// inside on your terms.
    /// Archetype(s): Breakable Vault. Structure: mirror-symmetric about x = 0. Igloo walls on 4 and 11 (and 20, 27), rows
    /// 0-3; roof on row 4, a dome cap on row 5; every igloo tile is B; the igloo floors are ice.
    /// Enemies: one Frost L sealed in each igloo; two Frost XL in the open sky over the middle, moving outward.
    /// Pops: 7 + 7 + 15 + 15 = 44 (D3 band 35-65); shortest hard clock for slots 2-5 in W4 = 66 s -> max 105.
    /// Hitless plan (Moss, Heavy Boots): from the normal middle floor shoot the XLs as they come back over it (they bounce
    /// on the igloo domes, not inside). Then chip a door from beside an igloo wall (2 hits a tile, rows 0-1): a two-row
    /// door lets the L's M children out but holds the L, so pop it from the doorway or break the roof above it.
    /// Over-break check: with every B gone the floor is flat and open; nothing is stranded and Wick can leave every spot.
    /// </remarks>
    [Preserve]
    public sealed class W4_TwinIgloos : StageSpec
    {
        public override string Id => "w4_twin_igloos";
        public override string Name => "Twin Igloos";
        public override int World => 4;
        public override int Difficulty => 3;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "Two breakable igloos each hold a Frost L. Clear the big Frost overhead, then chip a door in and pop the one inside on your terms.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open14();
                for (int lo = 4; lo <= 20; lo += 16)
                {
                    StageSketch.Box(grid, lo, lo, 0, 3, 'B');
                    StageSketch.Box(grid, lo + 7, lo + 7, 0, 3, 'B');
                    StageSketch.Box(grid, lo + 1, lo + 6, 4, 4, 'B');
                    StageSketch.Box(grid, lo + 2, lo + 5, 5, 5, 'B');
                }
                return StageSketch.Lines(grid);
            }
        }

        // Ice inside the igloos (5-10 and 21-26), normal ground everywhere else.
        public override string Ground => "NNNNN" + "IIIIII" + "NNNNNNNNNN" + "IIIIII" + "NNNNN";

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.Bubble(BubbleKind.Frost, StageBuilder.TierL, -7.5f, 2f, -1);
            b.Bubble(BubbleKind.Frost, StageBuilder.TierL, 7.5f, 2f, 1);
            b.BubbleAt(BubbleKind.Frost, StageBuilder.TierXL, 13, 10f, -1);
            b.BubbleAt(BubbleKind.Frost, StageBuilder.TierXL, 18, 10f, 1);
        }
    }
}
