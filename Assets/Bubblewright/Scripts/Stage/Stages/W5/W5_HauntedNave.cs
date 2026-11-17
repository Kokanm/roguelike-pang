using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 5 difficulty 1. A nave with one raised shelf: the Gloop bounces on it, the Ghosts sink through it.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W5, D1, MaxStageInWorld 1.
    /// Concept: a Gloop bounces on the shelf but both Ghosts sink straight through it; shoot a Ghost while it is solid.
    /// Archetype: Pads (one raised shelf with ladders at both ends). Structure: mirror-symmetric about x = 0.
    /// Enemies: Ghost L at columns 3 and 28 in the open aisles, drifting outward so the start is calm; a Gloop L on
    /// the shelf so the first thing Wick sees is a bubble that stops on stone next to two that do not.
    /// Pops: 7 + 7 + 7 = 21 (D1 band 10-25); slot 1 hard clock 69 s allows 110.
    /// Hitless plan: wait under the shelf, step out and shoot each Ghost from below as it comes down solid; climb
    /// either ladder and shoot the Gloop and its children on the shelf, or wait for them to fall off its ends.
    /// Over-break: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W5_HauntedNave : StageSpec
    {
        public override string Id => "w5_haunted_nave";
        public override string Name => "Haunted Nave";
        public override int World => 5;
        public override int Difficulty => 1;
        public override int MaxStageInWorld => 1;
        public override string Concept =>
            "A Gloop bounces on the nave's shelf, but both Ghosts sink straight through it. Shoot a Ghost only while it is solid.";

        public override string[] Rows
        {
            get
            {
                // Shelf on row 5 from column 10 to 21, a ladder up each end.
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 10, 21, 5, 5, '#');
                StageSketch.Box(grid, 10, 10, 0, 4, 'H');
                StageSketch.Box(grid, 21, 21, 0, 4, 'H');
                grid[5][10] = 'T';
                grid[5][21] = 'T';
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.BubbleAt(BubbleKind.Ghost, StageBuilder.TierL, 3, 9f, -1);
            b.BubbleAt(BubbleKind.Ghost, StageBuilder.TierL, 28, 9f, 1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierL, 16, 9f, 1);
        }
    }
}
