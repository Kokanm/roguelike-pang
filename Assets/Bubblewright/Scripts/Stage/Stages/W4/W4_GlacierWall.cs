using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 4 difficulty 2. A thick glacier wall with an ice-capped bridge splits two chambers.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W4, D2, MinStageInWorld 2, MaxStageInWorld 5.
    /// Concept: A glacier wall splits two chambers, each with a ceramic Frost XL. Climb a ladder and skate the icy bridge
    /// to reach the far one.
    /// Archetype(s): Divided Twin Chambers. Structure: mirror-symmetric about x = 0. Wall on 14-17 up to row 10, ladders
    /// on its faces (13 and 18), bridge on row 11 whose middle (14-17) is ice. Two rows above the bridge let Wick and
    /// M or S bubbles cross, never an L or an XL, so each chamber keeps its own big bubble. Ground ice at both ends.
    /// Enemies: one ceramic Frost XL per chamber, moving away from the wall (2 hits on the XL, ordinary children).
    /// Pops: 16 + 16 = 32 (D2 band 20-40); shortest hard clock for slots 2-5 in W4 = 66 s -> max 105.
    /// Hitless plan (Moss, Heavy Boots): from column 8 (normal ground) hit the near XL twice as it comes back, clear its
    /// children in the left chamber, climb the ladder on 13, walk across the bridge (it slides; grip again on the T at
    /// 18), climb down 18 and clear the right chamber the same way.
    /// Over-break check: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W4_GlacierWall : StageSpec
    {
        public override string Id => "w4_glacier_wall";
        public override string Name => "Glacier Wall";
        public override int World => 4;
        public override int Difficulty => 2;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "A glacier wall splits two chambers, each with a ceramic Frost XL. Climb a ladder and skate the icy bridge to reach the far one.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 14, 17, 0, 10, '#');
                StageSketch.Box(grid, 13, 13, 0, 10, 'H');
                StageSketch.Box(grid, 18, 18, 0, 10, 'H');
                StageSketch.Box(grid, 14, 17, 11, 11, 'I');
                grid[11][13] = 'T';
                grid[11][18] = 'T';
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => "IIIIII" + "NNNNNNNNNNNNNNNNNNNN" + "IIIIII";

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(8);
            b.BubbleAt(BubbleKind.Frost, StageBuilder.TierXL, 4, 8f, -1, ceramic: true);
            b.BubbleAt(BubbleKind.Frost, StageBuilder.TierXL, 27, 8f, 1, ceramic: true);
        }
    }
}
