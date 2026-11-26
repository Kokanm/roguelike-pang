using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 2 difficulty 4. Three storeys, one forest trick each: floor Pogos, wall Clingers, Drifters.</summary>
    /// <remarks>
    /// Stage Design Brief
    /// World / Difficulty / slots: W2, D4, MinStageInWorld 4, MaxStageInWorld 5, Storeys 3
    /// Concept: Three canopy storeys, one forest trick each: floor Pogos on the ground, wall Clingers on hanging
    ///   trunks, then Drifters in the crown.
    /// Archetype(s): Storey Tower Climb (W1_ThreeRoofs) with a zigzag: Floor Clinger Pogo Gauntlet, wall Clingers, Drifters
    /// Structure: asymmetric on purpose: the climb zigzags. Storey 1's ladder is on column 3 (hatch on row 13), storey 2's
    ///   on column 28 (hatch on row 27), so each storey is crossed end to end. Storey 2 has two hanging trunks
    ///   (columns 9 and 22, rows 17-24) with 2 rows under them to walk through. Storey 3 has two small crown blocks on
    ///   row 35 (columns 10-11 and 20-21) that turn the Drifters.
    /// Enemies and why each one is where it is:
    ///   Storey 1: Wick starts at column 28. Between him and the ladder: floor Clinger Pogos S (23), M (18), S (13), M (8),
    ///     5 u apart; two L Gloops (columns 10 / 20) heading left keep him moving.
    ///   Storey 2: an L wall Clinger on the inner face of each hanging trunk (y = 21), shot from the floor below, never
    ///     woken (Wick's body never gets within 1.5 u of its height); two M Clingers on the ceiling (columns 14 / 17).
    ///   Storey 3: two L and two M Drifters, all on the far side from the hatch (columns 3, 7, 13, 18).
    /// Pops: storey 1: 7 + 7 + 1 + 3 + 1 + 3 = 22; storey 2: 7 + 7 + 3 + 3 = 20; storey 3: 7 + 7 + 3 + 3 = 20; total 62
    ///   (D4 band 55-90); shortest hard clock for slots 4-5 in W2 = 60 s -> max 96.
    /// Hitless plan for Moss with Heavy Boots and the default harpoon:
    ///   1. Start at column 28; the nearest Pogo is 5 u away. Pop the L Gloops from the open right end.
    ///   2. Walk left to each Pogo in turn: wake it, step back 3 u and shoot it on the ceiling, or jump the S ones and fire
    ///      down. Climb the ladder on column 3 when the storey is clear.
    ///   3. Storey 2: arrive at column 3. Stand under each wall Clinger (column 10 / 21) and fire up; step under each
    ///      ceiling M and fire at once. Walk under the trunks (2 rows of room) to the ladder on column 28.
    ///   4. Storey 3: arrive at column 28; every Drifter is 10 u or more away and starts heading away. Hold the right side
    ///      and fire as their diagonals cross the open columns; the crown blocks turn them back toward the middle.
    /// Over-break check: no B tiles. Every enemy starts over floor Wick can stand on in its column, and the hatches are
    ///   the only ways up.
    /// </remarks>
    [Preserve]
    public sealed class W2_CanopyCrown : StageSpec
    {
        public override string Id => "w2_canopy_crown";
        public override string Name => "Canopy Crown";
        public override int World => 2;
        public override int Difficulty => 4;
        public override int MinStageInWorld => 4;
        public override int Storeys => 3;
        public override string Concept =>
            "Three canopy storeys, one forest trick each: floor Pogos on the ground, wall Clingers on hanging trunks, then Drifters in the crown.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open(3);
                StageSketch.Box(grid, 0, 31, 14, 14, '#');
                StageSketch.Box(grid, 0, 31, 28, 28, '#');
                // Zigzag: up on column 3 to storey 2, up on column 28 to storey 3.
                for (int y = 0; y <= 14; y++) grid[y][3] = y == 14 ? 'T' : 'H';
                for (int y = 15; y <= 28; y++) grid[y][28] = y == 28 ? 'T' : 'H';
                // Storey 2: hanging trunks with 2 rows under them.
                StageSketch.Box(grid, 9, 9, 17, 24, '#');
                StageSketch.Box(grid, 22, 22, 17, 24, '#');
                // Storey 3: crown blocks that turn the Drifters.
                StageSketch.Box(grid, 10, 11, 35, 35, '#');
                StageSketch.Box(grid, 20, 21, 35, 35, '#');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(28);
            // Storey 1.
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierL, 20, 9f, -1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierL, 10, 9f, -1);
            b.FloorClinger(StageBuilder.TierS, StageMap.CenterX(23), dir: -1);
            b.FloorClinger(StageBuilder.TierM, StageMap.CenterX(18), dir: -1);
            b.FloorClinger(StageBuilder.TierS, StageMap.CenterX(13), dir: -1);
            b.FloorClinger(StageBuilder.TierM, StageMap.CenterX(8), dir: -1);
            // Storey 2: authored in the column beside each trunk face; each seats itself flush against it.
            b.Clinger(StageBuilder.TierL, StageMap.CenterX(10), 21f, ClingerMount.LeftWall, dir: 1);
            b.Clinger(StageBuilder.TierL, StageMap.CenterX(21), 21f, ClingerMount.RightWall, dir: -1);
            b.Clinger(StageBuilder.TierM, StageMap.CenterX(14), 26f, dir: -1);
            b.Clinger(StageBuilder.TierM, StageMap.CenterX(17), 26f, dir: 1);
            // Storey 3.
            b.BubbleAt(BubbleKind.Drifter, StageBuilder.TierL, 3, 38f, 1);
            b.BubbleAt(BubbleKind.Drifter, StageBuilder.TierL, 13, 38f, -1);
            b.BubbleAt(BubbleKind.Drifter, StageBuilder.TierM, 7, 31f, 1);
            b.BubbleAt(BubbleKind.Drifter, StageBuilder.TierM, 18, 38f, -1);
        }
    }
}
