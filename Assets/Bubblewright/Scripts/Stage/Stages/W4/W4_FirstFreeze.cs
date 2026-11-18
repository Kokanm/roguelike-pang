using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 4 difficulty 1. Bands of ice and normal ground, normal grip under Wick, two Frost bubbles.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W4, D1, MaxStageInWorld 1.
    /// Concept: Frost bubbles bounce over bands of ice and normal ground. Wick starts on the wide normal band in the middle.
    /// Archetype(s): open court (intro). Structure: mirror-symmetric about x = 0, ground bands of 4 columns, normal 8 in the middle.
    /// Enemies: two Frost L, one over each half, moving outward so the first second is calm. Their landings freeze 4 s of ground.
    /// Pops: 7 + 7 = 14 (D1 band 10-25); hard clock for slot 1 in W4 = 63 s -> max 100 pops.
    /// Hitless plan (Moss, Heavy Boots): stay on the middle band, shoot each L as it comes back; step onto the normal
    /// bands at 4-7 and 24-27 to chase the M and S children, and expect to slide when crossing an ice band.
    /// Over-break check: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W4_FirstFreeze : StageSpec
    {
        public override string Id => "w4_first_freeze";
        public override string Name => "First Freeze";
        public override int World => 4;
        public override int Difficulty => 1;
        public override int MaxStageInWorld => 1;
        public override string Concept => "Two Frost bubbles bounce over bands of ice and normal ground. Wick starts on the wide normal band in the middle.";
        public override string[] Rows => BlankRows();

        // Bands: ice 0-3, normal 4-7, ice 8-11, normal 12-19, ice 20-23, normal 24-27, ice 28-31.
        public override string Ground => "IIIINNNNIIII" + "NNNNNNNN" + "IIIINNNNIIII";

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.Bubble(BubbleKind.Frost, StageBuilder.TierL, -10f, 9f, -1);
            b.Bubble(BubbleKind.Frost, StageBuilder.TierL, 10f, 9f, 1);
        }
    }
}
