using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 1 difficulty 3. Three terraces, each a step higher, each with a Gloop.</summary>
    [Preserve]
    public sealed class W1_TerraceClimb : StageSpec
    {
        public override string Id => "w1_terrace_climb";
        public override string Name => "Terrace Climb";
        public override int World => 1;
        public override int Difficulty => 3;
        public override int MinStageInWorld => 3;
        public override string Concept =>
            "The roof steps up in three terraces toward the right. Each landing hides a huge Gloop that can only be shot from that height.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 8, 31, 0, 0, '#');
                StageSketch.Box(grid, 16, 31, 1, 1, '#');
                StageSketch.Box(grid, 24, 31, 2, 2, '#');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 6, 9f, 1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 12, 9f, 1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 28, 10f, 1);
        }
    }
}
