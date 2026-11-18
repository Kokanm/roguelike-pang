using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 6 difficulty 4. A breakable vault with two ceramic Trigloops inside, between two spiked flanks.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W6, D4, MinStageInWorld 4, MaxStageInWorld 5.
    /// Concept: open the vault on your terms. Chip a side wall and fight the Trigloops in the low box, or chip the roof from
    ///   inside to let them out, while two big Scorches burn the flanks.
    /// Archetype(s): Breakable Vault (W1_BreakVault). Structure: mirror-symmetric about x = 0.
    /// Enemies: two ceramic Trigloop L sealed in the vault (four rows high, so they stay low and fast). An XL Scorch over
    ///   each flank, moving outward, over the spike strips.
    /// Pops: 15 + 15 + 14 + 14 = 58 (D4 band 55-90); shortest hard clock for slots 4-5 in W6 = 84 s -> max 134 pops.
    /// Hitless plan (Moss, Heavy Boots): pop the left XL Scorch from columns 6-10, off the strip. The vault fills columns
    ///   11-20, so chip its left wall (two hits a tile, rows 0 and 1), step in when both Trigloops are on the far side,
    ///   and shoot each twice. Chip the far wall to walk out, and pop the right XL Scorch the same way.
    /// Over-break check: with every B gone the vault is open floor; nothing is stranded and there is no pit.
    /// </remarks>
    [Preserve]
    public sealed class W6_SlagVault : StageSpec
    {
        public override string Id => "w6_slag_vault";
        public override string Name => "Slag Vault";
        public override int World => 6;
        public override int Difficulty => 4;
        public override int MinStageInWorld => 4;
        public override string Concept =>
            "Two ceramic Trigloops are sealed in a breakable vault. Chip your way in while big Scorches burn the spiked flanks.";

        public override string[] Rows
        {
            get
            {
                // Vault walls on columns 11 and 20 and roof on row 4, all breakable: four rows inside, Wick stands.
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 11, 11, 0, 4, 'B');
                StageSketch.Box(grid, 20, 20, 0, 4, 'B');
                StageSketch.Box(grid, 12, 19, 4, 4, 'B');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(7);
            b.Spike(2, 4);
            b.Spike(26, 4);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierXL, 3, 9f, -1);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierXL, 28, 9f, 1);
            b.BubbleAt(BubbleKind.Trigloop, StageBuilder.TierL, 14, 2.5f, -1, ceramic: true);
            b.BubbleAt(BubbleKind.Trigloop, StageBuilder.TierL, 17, 2.5f, 1, ceramic: true);
        }
    }
}
