using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 1 difficulty 3. Stairs, a center dais, two-column ground gaps, and floor Pogos.</summary>
    [Preserve]
    public sealed class W1_PogoSteps : StageSpec
    {
        public override string Id => "w1_pogo_steps";
        public override string Name => "Pogo Steps";
        public override int World => 1;
        public override int Difficulty => 3;
        public override int MinStageInWorld => 3;
        public override string Concept =>
            "A center dais, a two-column gap on each side, and stairs at the edges. Four medium Pogos guard the landings while two huge Gloops bounce overhead.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 2, 3, 0, 0, '#');
                grid[1][3] = '#';
                StageSketch.Box(grid, 28, 29, 0, 0, '#');
                grid[1][28] = '#';
                StageSketch.Box(grid, 14, 17, 0, 0, '#');
                return StageSketch.Lines(grid);
            }
        }

        // Gaps on columns 8-9 and 22-23: Moss with Heavy Boots clears two columns from a standing jump.
        public override string Ground => "========__============__========";

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(16, 1);
            b.Bubble(BubbleKind.Gloop, StageBuilder.TierM, StageMap.CenterX(6), 0.85f, -1, pogo: true);
            b.Bubble(BubbleKind.Gloop, StageBuilder.TierM, StageMap.CenterX(11), 0.85f, -1, pogo: true);
            b.Bubble(BubbleKind.Gloop, StageBuilder.TierM, StageMap.CenterX(20), 0.85f, 1, pogo: true);
            b.Bubble(BubbleKind.Gloop, StageBuilder.TierM, StageMap.CenterX(25), 0.85f, 1, pogo: true);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 5, 11f, -1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 26, 11f, 1);
        }
    }
}
