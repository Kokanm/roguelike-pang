using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 5 difficulty 3. A stepped bone mound: Lead bubbles skim the low ground, Ghosts sink through the steps.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W5, D3, MinStageInWorld 2.
    /// Concept: Lead Gloops skim low around a stepped bone mound; fight from its top while the Ghosts sink through
    /// the steps toward you.
    /// Archetype: Stepped terraces, built as a symmetric mound. Structure: mirror-symmetric about x = 0.
    /// Enemies: Lead XL in each low corner (columns 2 and 29), fast and low, so the mound top is the safe place to
    /// shoot from; Ghost L over the lower steps (columns 8 and 23), the only enemies that come straight down at the top.
    /// Pops: 15 + 15 + 7 + 7 = 44 (D3 band 35-65); shortest hard clock for slots 2-5 = 72 s, max 115.
    /// Hitless plan: start on the top step (feet 3). Shoot each Lead as it bounces up beside the mound; its children
    /// bounce lower still and are shot from the steps (every step is one row, so he walks and hops up and down).
    /// Shoot the Ghosts while solid as they come down through the steps.
    /// Over-break: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W5_BoneStair : StageSpec
    {
        public override string Id => "w5_bone_stair";
        public override string Name => "Bone Stair";
        public override int World => 5;
        public override int Difficulty => 3;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "Lead Gloops skim low around a stepped bone mound. Fight from its top while the Ghosts sink through the steps toward you.";

        public override string[] Rows
        {
            get
            {
                // Three one-row steps: row 0 from 5 to 26, row 1 from 9 to 22, row 2 from 13 to 18.
                var grid = StageSketch.Open14();
                StageSketch.Box(grid, 5, 26, 0, 0, '#');
                StageSketch.Box(grid, 9, 22, 1, 1, '#');
                StageSketch.Box(grid, 13, 18, 2, 2, '#');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(15, 3);
            b.BubbleAt(BubbleKind.Lead, StageBuilder.TierXL, 2, 7f, -1);
            b.BubbleAt(BubbleKind.Lead, StageBuilder.TierXL, 29, 7f, 1);
            b.BubbleAt(BubbleKind.Ghost, StageBuilder.TierL, 8, 10f, -1);
            b.BubbleAt(BubbleKind.Ghost, StageBuilder.TierL, 23, 10f, 1);
        }
    }
}
