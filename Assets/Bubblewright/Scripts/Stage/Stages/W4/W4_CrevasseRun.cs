using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 4 difficulty 4. An ice floor split by three two-column crevasses with grip only at their lips.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W4, D4, MinStageInWorld 4, MaxStageInWorld 5.
    /// Concept: Three crevasses split an icy floor, with grip only at their lips. Frost and Lead XL bounce across while Wick
    /// picks a safe edge to stop and shoot.
    /// Archetype(s): Gaps and Pogos (without Pogos). Structure: mirror-symmetric about x = 0. Two-column gaps on 7-8, 15-16
    /// and 23-24, normal ground two columns wide on both lips of each gap, ice everywhere else.
    /// Enemies: a Frost XL near each wall and a fast, low Lead XL inside it, all moving outward and away from Wick.
    /// Pops: 15 + 15 + 15 + 15 = 60 (D4 band 55-90); shortest hard clock for slots 4-5 in W4 = 72 s -> max 115.
    /// Hitless plan (Moss, Heavy Boots): start on the lip at 13; shoot the Lead XL as it comes back (it bounces low, so
    /// fire early) and jump the middle crevasse from lip to lip (column 14 to 17, 3 u). Never run onto ice toward a gap:
    /// walk, or stop on a normal lip first. Clear each side's Frost XL from the outer lips (5-6, 25-26).
    /// Over-break check: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W4_CrevasseRun : StageSpec
    {
        public override string Id => "w4_crevasse_run";
        public override string Name => "Crevasse Run";
        public override int World => 4;
        public override int Difficulty => 4;
        public override int MinStageInWorld => 4;
        public override string Concept =>
            "Three crevasses split an icy floor, with grip only at their lips. Frost and Lead XL bounce across while Wick picks a safe edge to stop and shoot.";

        public override string[] Rows => BlankRows();

        //                                   01234567890123456789012345678901
        public override string Ground =>    "IIIIINN__NNIINN__NNIINN__NNIIIII";

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(13);
            b.BubbleAt(BubbleKind.Frost, StageBuilder.TierXL, 4, 9f, -1);
            b.BubbleAt(BubbleKind.Frost, StageBuilder.TierXL, 27, 9f, 1);
            b.BubbleAt(BubbleKind.Lead, StageBuilder.TierXL, 9, 8f, -1);
            b.BubbleAt(BubbleKind.Lead, StageBuilder.TierXL, 22, 8f, 1);
        }
    }
}
