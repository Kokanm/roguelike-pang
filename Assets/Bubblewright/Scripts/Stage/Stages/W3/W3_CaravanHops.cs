using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 3 difficulty 3. A row of one-column sand gaps with Sinker Pogos resting on the landings.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W3, D3, MinStageInWorld 2, MaxStageInWorld 5.
    /// Concept: a caravan route of one-column sand gaps. Sinker Pogos sink lower each bounce and rest on the landings;
    ///   hop past while they rest, before they spring back.
    /// Archetype(s): Gaps and Pogos (W1_PogoSteps), with Sinker Pogos and sand gaps.
    /// Structure: mirror-symmetric about x = 0. One-column ground gaps on 4, 9, 14 and 17, 22, 27 (the World 3 cap):
    ///   4-wide landings and a 2-wide centre landing where Wick starts. Every gap is 2 u centre to centre, inside
    ///   Moss's 2.7 u sand reach.
    /// Enemies and why each one is where it is: a Sinker Pogo M on the middle of each of the four inner landings (the
    ///   rhythm to time), at least 4 u from Wick's start. A Gloop XL over each outer landing, heading for its wall.
    ///   No guest.
    /// Pops: 3 + 3 + 3 + 3 + 15 + 15 = 42 (D3 band 35-65); shortest hard clock for slots 2-5 in W3 = 69 s -> max 110 pops.
    /// Hitless plan for Moss with Heavy Boots and the default harpoon: start on the centre landing. Pop the nearest
    ///   Sinker Pogo from the edge of the centre landing as it rises, or wait for it to rest and hop the gap past it.
    ///   Work outward one landing at a time: each Pogo is popped from the landing beside it or from its own landing
    ///   while it is high. The XLs bounce over the outer landings; split them there and pop the children as they come
    ///   inward. Every hop is a 1-column gap.
    /// Over-break check: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W3_CaravanHops : StageSpec
    {
        public override string Id => "w3_caravan_hops";
        public override string Name => "Caravan Hops";
        public override int World => 3;
        public override int Difficulty => 3;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "A caravan route of narrow sand gaps. Sinker Pogos sink lower each bounce and rest on the landings: hop past while they rest.";

        public override string[] Rows => BlankRows();

        //                                   01234567890123456789012345678901
        public override string Ground => "====_====_====_==_====_====_====";

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.Bubble(BubbleKind.Sinker, StageBuilder.TierM, -9f, 0.85f, -1, pogo: true);
            b.Bubble(BubbleKind.Sinker, StageBuilder.TierM, -4f, 0.85f, -1, pogo: true);
            b.Bubble(BubbleKind.Sinker, StageBuilder.TierM, 4f, 0.85f, 1, pogo: true);
            b.Bubble(BubbleKind.Sinker, StageBuilder.TierM, 9f, 0.85f, 1, pogo: true);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 2, 9f, -1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 29, 9f, 1);
        }
    }
}
