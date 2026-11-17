using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 1 difficulty 3. A deck with a two-tile sieve and a ladder to each side.</summary>
    [Preserve]
    public sealed class W1_TrigloopSieve : StageSpec
    {
        public override string Id => "w1_trigloop_sieve";
        public override string Name => "Trigloop Sieve";
        public override int World => 1;
        public override int Difficulty => 3;
        public override int MinStageInWorld => 3;
        public override string Concept =>
            "A deck is split by a two-tile slot. Climb either ladder and pop the Trigloop and the Gloop; their smaller children drop through the slot.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 0, 13, 6, 6, '#');
                StageSketch.Box(grid, 16, 31, 6, 6, '#');
                StageSketch.Box(grid, 4, 4, 0, 5, 'H');
                StageSketch.Box(grid, 24, 24, 0, 5, 'H');
                grid[6][4] = 'T';
                grid[6][24] = 'T';
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(30);
            b.BubbleAt(BubbleKind.Trigloop, StageBuilder.TierXL, 8, 10f, -1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierL, 20, 10f, 1);
        }
    }
}
