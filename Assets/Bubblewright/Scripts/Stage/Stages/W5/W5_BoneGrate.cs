using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 5 difficulty 3. A bone grate with three 2-tile holes; the Trigloop stays on it, the Ghosts do not.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W5, D3, MinStageInWorld 2.
    /// Concept: a Trigloop bounces on a bone grate and only its small children rain through; two Ghosts ignore the
    /// grate and drift between both floors.
    /// Archetype: Sieve Ceiling. Structure: mirror-symmetric about x = 0.
    /// Enemies: Trigloop XL over the middle hole (columns 15-16), which holds it; Ghost L low in each corner of the
    /// court (columns 4 and 27), so the first Ghosts come up from below the grate, not down onto Wick.
    /// Pops: 40 + 7 + 7 = 54 (D3 band 35-65); shortest hard clock for slots 2-5 = 72 s, max 115.
    /// Hitless plan: shoot the Trigloop through the middle hole while it crosses, then pop the M and S children that
    /// fall through. Shoot the Ghosts in the court while solid. Climb a corner ladder (column 2 or 29) to the grate
    /// for the L children that stay up there.
    /// Over-break: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W5_BoneGrate : StageSpec
    {
        public override string Id => "w5_bone_grate";
        public override string Name => "Bone Grate";
        public override int World => 5;
        public override int Difficulty => 3;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "A Trigloop bounces on a bone grate and only its small children rain through. Two Ghosts ignore the grate and drift between both floors.";

        public override string[] Rows
        {
            get
            {
                // Grate on row 6 with 2-tile holes at 7-8, 15-16 and 23-24 (M and S fall through, L and XL stay up).
                // Ladders up to the grate at columns 2 and 29.
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 0, 31, 6, 6, '#');
                StageSketch.Box(grid, 7, 8, 6, 6, '.');
                StageSketch.Box(grid, 15, 16, 6, 6, '.');
                StageSketch.Box(grid, 23, 24, 6, 6, '.');
                StageSketch.Box(grid, 2, 2, 0, 5, 'H');
                StageSketch.Box(grid, 29, 29, 0, 5, 'H');
                grid[6][2] = 'T';
                grid[6][29] = 'T';
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(11);
            b.BubbleAt(BubbleKind.Trigloop, StageBuilder.TierXL, 15, 10f, 1);
            b.BubbleAt(BubbleKind.Ghost, StageBuilder.TierL, 4, 4f, -1);
            b.BubbleAt(BubbleKind.Ghost, StageBuilder.TierL, 27, 4f, 1);
        }
    }
}
