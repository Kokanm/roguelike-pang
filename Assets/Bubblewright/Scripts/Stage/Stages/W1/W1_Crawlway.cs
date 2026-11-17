using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 1 difficulty 2. A one-row crouch tunnel between two open courts.</summary>
    [Preserve]
    public sealed class W1_Crawlway : StageSpec
    {
        public override string Id => "w1_crawlway";
        public override string Name => "Crawlway";
        public override int World => 1;
        public override int Difficulty => 2;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "A one-row ceiling cuts the court in half. Wick crouches through it, then stands in either open end to pop the Gloop waiting there.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 8, 23, 1, 1, '#');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 3, 9f, -1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 28, 9f, 1);
        }
    }
}
