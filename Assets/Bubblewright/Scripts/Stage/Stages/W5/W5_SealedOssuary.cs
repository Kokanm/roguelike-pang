using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 5 difficulty 2. A breakable reliquary holds two ceramic Gloops; Ghosts walk through its walls.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W5, D2, MinStageInWorld 2.
    /// Concept: ceramic Gloops are sealed in a B reliquary; the Ghosts outside pass through its walls toward Wick.
    /// Archetype: Breakable Vault. Structure: mirror-symmetric about x = 0.
    /// Enemies: ceramic Gloop L at columns 14 and 17 inside the reliquary (4 rows high, so they bump its roof);
    /// Ghost L at columns 2 and 29 in the corners, drifting into the walls first.
    /// Pops: 8 + 8 + 7 + 7 = 30 (D2 band 20-40); shortest hard clock for slots 2-5 in W5 = 72 s, max 115.
    /// Hitless plan: shoot the Ghosts in the open first, each while solid. Chip a side wall at its foot (two hits per
    /// tile), step in, shoot each ceramic Gloop twice and pop the children before they find the hole.
    /// Over-break: with every B gone the reliquary is open floor; nothing is stranded.
    /// </remarks>
    [Preserve]
    public sealed class W5_SealedOssuary : StageSpec
    {
        public override string Id => "w5_sealed_ossuary";
        public override string Name => "Sealed Ossuary";
        public override int World => 5;
        public override int Difficulty => 2;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "Two ceramic Gloops are sealed in a breakable reliquary. The Ghosts outside pass through its walls; chip your own way in.";

        public override string[] Rows
        {
            get
            {
                // Reliquary walls on columns 11 and 20 and its roof on row 4, all breakable. Inside: columns 12-19, rows 0-3.
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
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierL, 14, 2f, -1, ceramic: true);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierL, 17, 2f, 1, ceramic: true);
            b.BubbleAt(BubbleKind.Ghost, StageBuilder.TierL, 2, 10f, -1);
            b.BubbleAt(BubbleKind.Ghost, StageBuilder.TierL, 29, 10f, 1);
        }
    }
}
