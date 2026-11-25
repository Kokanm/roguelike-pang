using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 4 difficulty 4. Two storeys: a glacier spire with B-sealed doors holds the ladder to the crown.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W4, D4, MinStageInWorld 4, MaxStageInWorld 5, Storeys 2.
    /// Concept: A glacier spire splits the lower storey; chip its sealed doors to cross and to reach the ladder. Up in the
    /// crown, Frost bubbles bounce and a Drifter zigzags.
    /// Archetype(s): Storey Tower Climb + Breakable Vault. Structure: mirror-symmetric about x = 0. Storey 1: spire walls
    /// on 13 and 18 to the cap, B doors on their rows 0-1, twin ladders on 15 and 16 inside it with a hatch each. Storey 2:
    /// floor row 14 (ice at both ends), open crown.
    /// Enemies: storey 1, one Frost XL in each court moving outward (the spire keeps them and their L children apart, so
    /// Wick must chip through). Storey 2, two Frost XL near the walls and a Drifter L, all moving away from the hatches.
    /// Pops: 15 + 15 | 15 + 15 + 7 = 67 (D4 band 55-90); shortest hard clock for slots 4-5 in W4 = 72 s -> max 115.
    /// Hitless plan (Moss, Heavy Boots): clear the left court from the normal floor at 10, chip the spire's left door
    /// (2 hits a tile), cross the spire floor, chip the right door and clear the right court. Climb either ladder; the
    /// hatch opens once storey 1 is clear. On the crown, fire at the Drifter as it crosses above and at the XLs as they
    /// come back toward the middle; the ends of the crown floor are ice.
    /// Over-break check: with every B gone the spire is an open archway; both courts and the ladders stay reachable and
    /// Wick can leave every spot.
    /// </remarks>
    [Preserve]
    public sealed class W4_IceSpire : StageSpec
    {
        public override string Id => "w4_ice_spire";
        public override string Name => "Ice Spire";
        public override int World => 4;
        public override int Difficulty => 4;
        public override int MinStageInWorld => 4;
        public override int Storeys => 2;
        public override string Concept =>
            "A glacier spire splits the lower storey; chip its sealed doors to cross and to reach the ladder. Up in the crown, Frost bubbles bounce and a Drifter zigzags.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open14(2);
                // Crown floor: ice at both ends, normal in the middle, ladder tops on 15 and 16.
                StageSketch.Box(grid, 0, 31, 14, 14, '#');
                StageSketch.Box(grid, 0, 8, 14, 14, 'I');
                StageSketch.Box(grid, 23, 31, 14, 14, 'I');
                // Spire: walls on 13 and 18 up to the cap, sealed at the foot by B doors.
                StageSketch.Box(grid, 13, 13, 2, 13, '#');
                StageSketch.Box(grid, 18, 18, 2, 13, '#');
                StageSketch.Box(grid, 13, 13, 0, 1, 'B');
                StageSketch.Box(grid, 18, 18, 0, 1, 'B');
                StageSketch.Box(grid, 14, 17, 13, 13, '#');
                for (int y = 0; y <= 14; y++)
                {
                    grid[y][15] = y == 14 ? 'T' : 'H';
                    grid[y][16] = y == 14 ? 'T' : 'H';
                }
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => "IIIIIIIII" + "NNNNNNNNNNNNNN" + "IIIIIIIII";

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(10);
            b.BubbleAt(BubbleKind.Frost, StageBuilder.TierXL, 5, 9f, -1);
            b.BubbleAt(BubbleKind.Frost, StageBuilder.TierXL, 26, 9f, 1);

            b.BubbleAt(BubbleKind.Frost, StageBuilder.TierXL, 4, 22f, -1);
            b.BubbleAt(BubbleKind.Frost, StageBuilder.TierXL, 27, 22f, 1);
            b.BubbleAt(BubbleKind.Drifter, StageBuilder.TierL, 22, 25f, 1);
        }
    }
}
