using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 2 difficulty 3. Two-column gullies pen a pearl roller in each outer lane.</summary>
    /// <remarks>
    /// Stage Design Brief
    /// World / Difficulty / slots: W2, D3, MinStageInWorld 2, MaxStageInWorld 5
    /// Concept: Two-column gullies pen a pearl roller in each outer lane. Hop a gully between its passes to shoot it,
    ///   while two huge Gloops bounce overhead.
    /// Archetype(s): Gaps and lanes (W1_PogoSteps) with ground rollers
    /// Structure: mirror-symmetric about x = 0. Ground gaps on columns 9-10 and 21-22 (2 columns, Moss with Heavy Boots
    ///   jumps them standing) cut the floor into a 10-column centre lane and two 9-column outer lanes. A ground roller
    ///   turns at gap edges, so each outer roller paces its lane back and forth and never reaches the centre.
    /// Enemies and why each one is where it is: a big roller in each outer lane (column 4 / 27) heading inward, so it turns
    ///   at the gully edge first; two XL Gloops over the outer lanes (columns 4 / 27, y = 9), heading outward, so their
    ///   children fall into the lanes and pull Wick across the gullies.
    /// Pops: 3 + 3 + 15 + 15 = 36 (D3 band 35-65); shortest hard clock for slots 2-5 in W2 = 54 s -> max 86.
    /// Hitless plan for Moss with Heavy Boots and the default harpoon:
    ///   1. Start at x = 0 in the centre lane; the rollers and Gloops are 11.5 u away and can never roll into this lane.
    ///   2. Pop the XLs and their children as they bounce over the centre lane (stay back from the gully edges).
    ///   3. Watch a roller turn at the far wall, then hop the gully (column 11 to column 8, 3 u, level) as it rolls away,
    ///      and fire as it comes back: the harpoon cable stands from the floor, so it rolls into it. Jump it (1.2 u tall)
    ///      if the timing slips. Its two small halves roll apart; shoot each as it passes.
    ///   4. Hop back to the centre and do the other lane.
    /// Over-break check: no B tiles. Every lane joins the next by a level 2-column jump both ways.
    /// </remarks>
    [Preserve]
    public sealed class W2_RollerGully : StageSpec
    {
        public override string Id => "w2_roller_gully";
        public override string Name => "Roller Gully";
        public override int World => 2;
        public override int Difficulty => 3;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "Two-column gullies pen a pearl roller in each outer lane. Hop a gully between its passes to shoot it, while two huge Gloops bounce overhead.";

        public override string[] Rows => BlankRows14();

        // Gaps on columns 9-10 and 21-22.
        public override string Ground => "=========__==========__=========";

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 4, 9f, -1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 27, 9f, 1);
            b.Roller(StageMap.CenterX(4), 0f, 1);
            b.Roller(StageMap.CenterX(27), 0f, -1);
        }
    }
}
