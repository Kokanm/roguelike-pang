using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 5 difficulty 3. Three tombs side by side, linked by gallery ladders and breakable doors.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W5, D3, MinStageInWorld 2.
    /// Concept: Ghosts drift through the tomb walls, the Gloops cannot; break a door or climb the gallery and drop in.
    /// Archetype: Divided Twin Chambers (three of them) + Breakable Vault doors. Structure: mirror-symmetric about x = 0.
    /// Enemies: Gloop XL in each side tomb (columns 4 and 27). The walls rise to row 9, so no Gloop of any size
    /// bounces over them (an XL tops out at 9.5 u at its bottom). Ghost L high in each side tomb (columns 7 and 24),
    /// drifting outward first: they are the only enemies that reach the middle tomb on their own.
    /// Pops: 15 + 15 + 7 + 7 = 44 (D3 band 35-65); shortest hard clock for slots 2-5 = 72 s, max 115.
    /// Hitless plan: start in the middle tomb and shoot the Ghosts as they come through the walls while solid. Then
    /// either chip a door (columns 10 or 21, rows 0-1) and fight the XL's children as they come through it two rows
    /// high, or climb the gallery ladder (column 11 or 20) to the wall top and drop into the side tomb behind the XL.
    /// Over-break: with every door broken the tombs join at floor level; the ladders still lead to the wall tops.
    /// </remarks>
    [Preserve]
    public sealed class W5_DividedTombs : StageSpec
    {
        public override string Id => "w5_divided_tombs";
        public override string Name => "Divided Tombs";
        public override int World => 5;
        public override int Difficulty => 3;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "Three tombs share walls. Ghosts drift through them, Gloops cannot: break a tomb's door or climb the gallery and drop in.";

        public override string[] Rows
        {
            get
            {
                // Walls on columns 10 and 21 up to row 9, with breakable doors on rows 0-1. Gallery ladders on the middle
                // tomb's side of each wall (columns 11 and 20), topped by a T on row 9 so Wick steps onto the wall top.
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 10, 10, 0, 1, 'B');
                StageSketch.Box(grid, 21, 21, 0, 1, 'B');
                StageSketch.Box(grid, 10, 10, 2, 9, '#');
                StageSketch.Box(grid, 21, 21, 2, 9, '#');
                StageSketch.Box(grid, 11, 11, 0, 8, 'H');
                StageSketch.Box(grid, 20, 20, 0, 8, 'H');
                grid[9][11] = 'T';
                grid[9][20] = 'T';
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 4, 8f, -1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 27, 8f, 1);
            b.BubbleAt(BubbleKind.Ghost, StageBuilder.TierL, 7, 11f, -1);
            b.BubbleAt(BubbleKind.Ghost, StageBuilder.TierL, 24, 11f, 1);
        }
    }
}
