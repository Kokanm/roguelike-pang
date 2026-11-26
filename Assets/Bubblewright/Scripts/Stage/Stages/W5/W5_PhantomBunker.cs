using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 5 difficulty 3. A slotted bunker roof stops the Trigloop but not the Ghosts.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W5, D3, MinStageInWorld 2.
    /// Concept: the slotted roof holds the huge Trigloop overhead, but the Ghosts sink through it; time each shot to
    /// a Ghost's solid phase.
    /// Archetype: Slotted Bunker. Structure: mirror-symmetric roof and walls; the enemies are lopsided on purpose
    /// (the game mirrors the stage at random).
    /// Enemies: Trigloop XL over slot 20 bouncing on the roof; Ghost L over slot 11 that sinks into the bunker at
    /// once; Ghost L outside on the right that wanders in later.
    /// Pops: 40 + 7 + 7 = 54 (D3 band 35-65); shortest hard clock for slots 2-5 = 72 s, max 115.
    /// Hitless plan: stay under the roof, fire up through slot 20 as the Trigloop crosses it, pop its L and M children
    /// the same way (they cannot fall through a 1-tile slot; S children drop slowly and are shot inside). Shoot each
    /// Ghost inside the bunker while it is solid. Chip a wall foot (column 8 or 23) for the children that bounce off
    /// the roof ends, and for the outside Ghost.
    /// Over-break: with both feet broken the bunker has two doors; the roof stays.
    /// </remarks>
    [Preserve]
    public sealed class W5_PhantomBunker : StageSpec
    {
        public override string Id => "w5_phantom_bunker";
        public override string Name => "Phantom Bunker";
        public override int World => 5;
        public override int Difficulty => 3;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "A slotted roof holds a huge Trigloop overhead, but the Ghosts sink straight through it. Time each shot to a Ghost's solid phase.";

        public override string[] Rows
        {
            get
            {
                // Roof on row 4 from column 8 to 23 with one-tile slots at 11 and 20. Walls on 8 and 23 with breakable feet.
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 8, 23, 4, 4, '#');
                grid[4][11] = '.';
                grid[4][20] = '.';
                StageSketch.Box(grid, 8, 8, 2, 3, '#');
                StageSketch.Box(grid, 23, 23, 2, 3, '#');
                StageSketch.Box(grid, 8, 8, 0, 1, 'B');
                StageSketch.Box(grid, 23, 23, 0, 1, 'B');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.BubbleAt(BubbleKind.Trigloop, StageBuilder.TierXL, 20, 9f, 1);
            b.BubbleAt(BubbleKind.Ghost, StageBuilder.TierL, 11, 10f, -1);
            b.BubbleAt(BubbleKind.Ghost, StageBuilder.TierL, 28, 9f, 1);
        }
    }
}
