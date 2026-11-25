using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 2 difficulty 4. Two storeys: rollers circle the low boughs, then Clingers and a Lead wave in the canopy.</summary>
    /// <remarks>
    /// Stage Design Brief
    /// World / Difficulty / slots: W2, D4, MinStageInWorld 3, MaxStageInWorld 5, Storeys 2
    /// Concept: Pearl rollers circle the low boughs. Climb a twin ladder to the canopy, where Clingers hang from the
    ///   branches and a Lead wave drops in.
    /// Archetype(s): Storey Tower Climb (W1_BellTower) + Roller Orbit Loop
    /// Structure: mirror-symmetric about x = 0. Twin ladders on columns 14 and 17, each with its own hatch on row 13 and a
    ///   T in the canopy floor (row 14). Storey 1: boughs on row 5, columns 4-9 and 22-27, a roller loop each. Storey 2:
    ///   boughs on row 20, columns 3-9 and 22-28, with 5 rows under them.
    /// Enemies and why each one is where it is:
    ///   Storey 1: a big roller on each low bough (bottom at 3.8 u on the underside, over Wick's head), and two XL Gloops
    ///     in the corners (columns 2 / 29) heading inward, the storey's real pressure.
    ///   Storey 2: an L Clinger under each bough (columns 6 / 25), an M Clinger on the treetop ceiling at columns 10 / 21,
    ///     4 u from either hatch, so neither wakes on arrival. One wave of two M Leads keeps the canopy busy.
    /// Pops: storey 1: 15 + 15 + 3 + 3 = 36; storey 2: 7 + 7 + 3 + 3 + wave 3 + 3 = 26; total 62 (D4 band 55-90);
    ///   shortest hard clock for slots 3-5 in W2 = 57 s -> max 91.
    /// Hitless plan for Moss with Heavy Boots and the default harpoon:
    ///   1. Start at x = 0 between the ladders. The XLs start 13.5 u away; pop them and their children in the open middle.
    ///   2. Stand under a bough and fire as its roller rolls along the underside (or one column outside an end as it
    ///      rolls down the end face). The rollers can never reach the floor while the boughs stand.
    ///   3. When storey 1 is clear, climb either ladder through its hatch (no enemy within 3 u of it).
    ///   4. Storey 2: walk under each bough and fire at its L Clinger as it wakes (it hangs at 18.75, 3.75 u over his
    ///      feet); step under each treetop M and fire at once. Pop the wave Leads as they come down, using the boughs
    ///      as cover from their low bounces.
    /// Over-break check: no B tiles. Every enemy starts over floor Wick can stand on in its column.
    /// </remarks>
    [Preserve]
    public sealed class W2_TreetopAscent : StageSpec
    {
        public override string Id => "w2_treetop_ascent";
        public override string Name => "Treetop Ascent";
        public override int World => 2;
        public override int Difficulty => 4;
        public override int MinStageInWorld => 3;
        public override int Storeys => 2;
        public override string Concept =>
            "Pearl rollers circle the low boughs. Climb a twin ladder to the canopy, where Clingers hang from the branches and a Lead wave drops in.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open14(2);
                StageSketch.Box(grid, 0, 31, 14, 14, '#');
                for (int y = 0; y <= 14; y++)
                {
                    grid[y][14] = y == 14 ? 'T' : 'H';
                    grid[y][17] = y == 14 ? 'T' : 'H';
                }
                // Storey 1: low boughs the rollers circle.
                StageSketch.Box(grid, 4, 9, 5, 5, '#');
                StageSketch.Box(grid, 22, 27, 5, 5, '#');
                // Storey 2: canopy boughs with 5 rows under them.
                StageSketch.Box(grid, 3, 9, 20, 20, '#');
                StageSketch.Box(grid, 22, 28, 20, 20, '#');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 2, 9f, 1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 29, 9f, -1);
            b.Roller(StageMap.CenterX(6), 6f, 1);
            b.Roller(StageMap.CenterX(25), 6f, -1);

            b.Clinger(StageBuilder.TierL, StageMap.CenterX(6), 18.5f, dir: 1);
            b.Clinger(StageBuilder.TierL, StageMap.CenterX(25), 18.5f, dir: -1);
            b.Clinger(StageBuilder.TierM, StageMap.CenterX(10), 26f, dir: -1);
            b.Clinger(StageBuilder.TierM, StageMap.CenterX(21), 26f, dir: 1);
            b.Wave(0, BubbleKind.Lead, StageBuilder.TierM, -8f, 1);
            b.Wave(0, BubbleKind.Lead, StageBuilder.TierM, 8f, -1);
        }
    }
}
