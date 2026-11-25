using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 5 difficulty 2. Two penned Gloops on crypt roofs, reached up one-column wells by wall jumps.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W5, D2, MinStageInWorld 2.
    /// Concept: a Gloop is penned on each crypt roof; crouch under a lip and wall-jump up its well while the Ghost
    /// drifts through the stone after him.
    /// Archetype: Wall-jump well (W1_WellShaft), doubled. Structure: mirror-symmetric about x = 0.
    /// Enemies: Gloop XL on each roof (columns 2 and 29): the lip's three rows to the ceiling hold the XL, its L
    /// children get out into the court. Ghost L at column 20 in the court: the one enemy that can follow Wick into a well.
    /// Pops: 15 + 15 + 7 = 37 (D2 band 20-40); shortest hard clock for slots 2-5 = 72 s, max 115.
    /// Hitless plan: shoot the Ghost in the court while it is solid. Crouch under the lip at column 6, stand in the
    /// well (column 5) and wall-jump up to row 7, step onto the roof and shoot the XL from beside it. Drop back to the
    /// court for the L children. Same on the right through column 26.
    /// Over-break: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W5_CharnelWell : StageSpec
    {
        public override string Id => "w5_charnel_well";
        public override string Name => "Charnel Well";
        public override int World => 5;
        public override int Difficulty => 2;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "A Gloop is penned on each crypt roof. Crouch under a lip and wall-jump up its well, while the Ghost drifts through the stone after you.";

        public override string[] Rows
        {
            get
            {
                // Crypt roofs on columns 0-4 and 27-31, rows 0-6. The wells are columns 5 and 26. Each lip (columns 6
                // and 25) stops one row above the ground, so Wick crouches in, and rises to row 10 as the pen's parapet.
                var grid = StageSketch.Open14();
                StageSketch.Box(grid, 0, 4, 0, 6, '#');
                StageSketch.Box(grid, 27, 31, 0, 6, '#');
                StageSketch.Box(grid, 6, 6, 1, 10, '#');
                StageSketch.Box(grid, 25, 25, 1, 10, '#');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(13);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 2, 10f, -1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 29, 10f, 1);
            b.BubbleAt(BubbleKind.Ghost, StageBuilder.TierL, 20, 9f, 1);
        }
    }
}
