using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 6 difficulty 4. Stone islands in a lava floor, a one-tile spike in the middle of each.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W6, D4, MinStageInWorld 4, MaxStageInWorld 5.
    /// Concept: the floor is lava with stepping stones. Every stone has a spike in its middle, and Scorch flames only
    ///   catch on stone (never over lava), so the places to stand keep changing.
    /// Archetype(s): Gaps and Pogos (W1_PogoSteps), without the Pogos. Structure: mirror-symmetric about x = 0.
    /// Enemies: a ceramic XL Scorch over each outer island and a Trigloop L over each inner one, all moving outward.
    /// Pops: 16 + 16 + 13 + 13 = 58 (D4 band 55-90); shortest hard clock for slots 4-5 in W6 = 84 s -> max 134 pops.
    /// Hitless plan (Moss, Heavy Boots): from the centre island (columns 14-17) shoot each Trigloop L as it comes back in.
    ///   Jump the two-column gaps from island edge to island edge (11 to 14, 6 to 9, 1 to 4 and the mirror), stand on an
    ///   island's edge column, not its spike, and hop over the one-tile spike when it is up. Shoot each ceramic XL twice
    ///   from its island.
    /// Over-break check: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W6_MoltenIslands : StageSpec
    {
        public override string Id => "w6_molten_islands";
        public override string Name => "Molten Islands";
        public override int World => 6;
        public override int Difficulty => 4;
        public override int MinStageInWorld => 4;
        public override string Concept =>
            "Stepping stones in a lava floor, a spike in the middle of each. Scorch flames catch on stone, never over the lava.";

        public override string[] Rows => BlankRows14();

        // Islands of three columns (four in the middle), lava gaps of two: the widest Moss with Heavy Boots clears.
        public override string Ground => "==__===__===__====__===__===__==";

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(15);
            // The inner pair rises together, the outer pair half a cycle later.
            b.Spike(10, 1, phase: 0f);
            b.Spike(21, 1, phase: 0f);
            b.Spike(5, 1, phase: 2.4f);
            b.Spike(26, 1, phase: 2.4f);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierXL, 5, 9f, -1, ceramic: true);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierXL, 26, 9f, 1, ceramic: true);
            b.BubbleAt(BubbleKind.Trigloop, StageBuilder.TierL, 10, 9f, -1);
            b.BubbleAt(BubbleKind.Trigloop, StageBuilder.TierL, 21, 9f, 1);
        }
    }
}
