using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 1 difficulty 3. A deck with two two-tile sieve holes and a ladder at each end.</summary>
    [Preserve]
    public sealed class W1_TrigloopSieve : StageSpec
    {
        public override string Id => "w1_trigloop_sieve";
        public override string Name => "Trigloop Sieve";
        public override int World => 1;
        public override int Difficulty => 3;
        public override int MinStageInWorld => 3;
        public override string Concept =>
            "Two Trigloops bounce on a deck with two two-tile holes. Only their M and S children fit through and rain into the court below.";

        public override string[] Rows
        {
            get
            {
                // Deck on row 8, holes on columns 9-10 and 21-22, ladders on 4 and 27. A 2-tile hole lets M and S through
                // and holds L and XL (FilterGapTests).
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 0, 31, 8, 8, '#');
                StageSketch.Box(grid, 9, 10, 8, 8, '.');
                StageSketch.Box(grid, 21, 22, 8, 8, '.');
                StageSketch.Box(grid, 4, 4, 0, 7, 'H');
                StageSketch.Box(grid, 27, 27, 0, 7, 'H');
                grid[8][4] = 'T';
                grid[8][27] = 'T';
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(16);
            b.BubbleAt(BubbleKind.Trigloop, StageBuilder.TierXL, 6, 13f, -1);
            b.BubbleAt(BubbleKind.Trigloop, StageBuilder.TierL, 25, 13f, 1);
        }
    }
}
