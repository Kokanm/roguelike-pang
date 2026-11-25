using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 5 difficulty 4. Two storeys: ceramic rollers round a crypt slab below, a sealed sarcophagus above.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W5, D4, MinStageInWorld 4, Storeys 2.
    /// Concept: clear the crypt where ceramic rollers circle a slab, climb a corner ladder, and chip open the sealed
    /// sarcophagus while Ghosts drift through its lid.
    /// Archetype: Storey Tower Climb + Roller Orbit Loop (storey 1) and Breakable Vault (storey 2).
    /// Structure: mirror-symmetric about x = 0, twin corner ladders and hatches.
    /// Enemies: storey 1 has a Gloop XL (column 6) and a Ghost XL (column 25) in the open, a ceramic roller orbiting
    /// the slab and a ceramic roller on the floor. Storey 2 has two ceramic Gloop L sealed in the sarcophagus and a
    /// Ghost L on each side of it, drifting inward, away from both hatches. Ghosts stay in their own storey.
    /// Pops: storey 1 15 + 15 + 4 + 4 = 38, storey 2 8 + 8 + 7 + 7 = 30, total 68 (D4 band 55-90); slot 4-5 hard
    /// clock 78 s allows 124.
    /// Hitless plan: start under the slab (5 rows of headroom; the orbiting roller passes over his head). Jump the floor
    /// roller and shoot it from behind. Shoot the Gloop and its children in the open, the Ghost while solid, and the
    /// slab roller as it comes down a side of the slab. Climb the ladder at column 3 or 28. On storey 2, shoot the
    /// Ghosts, chip a sarcophagus wall at its foot from outside (two hits a tile), step in, and pop the ceramic Gloops.
    /// Over-break: with every B gone the sarcophagus is open floor; nothing is stranded.
    /// </remarks>
    [Preserve]
    public sealed class W5_DeepCatacomb : StageSpec
    {
        public override string Id => "w5_deep_catacomb";
        public override string Name => "Deep Catacomb";
        public override int World => 5;
        public override int Difficulty => 4;
        public override int MinStageInWorld => 4;
        public override int Storeys => 2;
        public override string Concept =>
            "Clear the crypt where ceramic rollers circle a slab, climb a corner ladder, and chip open the sealed sarcophagus above.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open14(2);
                // Storey 2's floor on row 14, with hatches over the corner ladders on columns 3 and 28.
                StageSketch.Box(grid, 0, 31, 14, 14, '#');
                for (int y = 0; y <= 14; y++)
                {
                    grid[y][3] = y == 14 ? 'T' : 'H';
                    grid[y][28] = y == 14 ? 'T' : 'H';
                }
                // Storey 1: a floating slab on row 5 from column 11 to 20 for a roller to orbit.
                StageSketch.Box(grid, 11, 20, 5, 5, '#');
                // Storey 2: the sarcophagus, breakable walls on 11 and 20 and lid on row 18. Inside: columns 12-19, rows 15-17.
                StageSketch.Box(grid, 11, 11, 15, 18, 'B');
                StageSketch.Box(grid, 20, 20, 15, 18, 'B');
                StageSketch.Box(grid, 12, 19, 18, 18, 'B');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            // Storey 1.
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 6, 9f, -1);
            b.BubbleAt(BubbleKind.Ghost, StageBuilder.TierXL, 25, 9f, 1);
            b.Roller(-3f, 6f, -1, 0, ceramic: true);
            b.Roller(11.5f, 0f, -1, 0, ceramic: true);
            // Storey 2.
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierL, 14, 16.5f, -1, ceramic: true);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierL, 17, 16.5f, 1, ceramic: true);
            b.BubbleAt(BubbleKind.Ghost, StageBuilder.TierL, 8, 23f, 1);
            b.BubbleAt(BubbleKind.Ghost, StageBuilder.TierL, 23, 23f, -1);
        }
    }
}
