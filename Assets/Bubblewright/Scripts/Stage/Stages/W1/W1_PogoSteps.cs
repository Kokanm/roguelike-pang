using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 1 difficulty 3. Stairs, a center dais, and floor Pogos.</summary>
    [Preserve]
    public sealed class W1_PogoSteps : StageSpec
    {
        public override string Id => "w1_pogo_steps";
        public override string Name => "Pogo Steps";
        public override int World => 1;
        public override int Difficulty => 3;
        public override int MinStageInWorld => 3;
        public override string Concept =>
            "Low stairs rise toward a center dais. Four small Pogos patrol the floor while two huge Gloops hang over the landings.";

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

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(16, 1);
            b.Bubble(BubbleKind.Gloop, StageBuilder.TierM, StageMap.CenterX(6), 0.85f, -1, pogo: true);
            b.Bubble(BubbleKind.Gloop, StageBuilder.TierM, StageMap.CenterX(10), 0.85f, -1, pogo: true);
            b.Bubble(BubbleKind.Gloop, StageBuilder.TierM, StageMap.CenterX(21), 0.85f, 1, pogo: true);
            b.Bubble(BubbleKind.Gloop, StageBuilder.TierM, StageMap.CenterX(25), 0.85f, 1, pogo: true);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 8, 9f, -1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 23, 9f, 1);
        }
    }
}
