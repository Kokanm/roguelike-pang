using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 2 difficulty 4. Drifters ricochet through a lattice of floating blocks over two big Leads.</summary>
    /// <remarks>
    /// Stage Design Brief
    /// World / Difficulty / slots: W2, D4, MinStageInWorld 4, MaxStageInWorld 5
    /// Concept: Four Drifters ricochet through a lattice of floating blocks while two big Leads bounce underneath.
    ///   Shoot up the open columns as they cross.
    /// Archetype(s): rhythmic columns (a lattice) with Drifters
    /// Structure: mirror-symmetric about x = 0. 2-wide blocks on row 9 at columns 3, 9, 15, 21, 27 and on row 5 at columns
    ///   6, 12, 18, 24: every third column (2, 5, 8, 11, 14, 17, 20, 23, 26, 29) and the edges are open from floor to
    ///   ceiling, so the firing lanes read at a glance. Between the rows there are 3 rows of air, enough for an L.
    /// Enemies and why each one is where it is: L Drifters start in the top band at columns 1 / 30 and 8 / 23 (y = 12, all
    ///   over open columns) and dive into the lattice, where every block turns them. XL Leads start low under the lattice
    ///   (columns 4 / 27, y = 3) heading outward; they bounce into the row-5 blocks and stay low.
    /// Pops: 7 + 7 + 7 + 7 + 15 + 15 = 58 (D4 band 55-90); shortest hard clock for slots 4-5 in W2 = 60 s -> max 96.
    /// Hitless plan for Moss with Heavy Boots and the default harpoon:
    ///   1. Start at x = 0 under the centre block of row 9; every Drifter starts 8.5 u or more away and high up, the Leads
    ///      11.5 u away going out.
    ///   2. Stand in an open column (14 or 17 next to the start) and fire as a Drifter crosses it; Drifters fly at
    ///      5 u/s on fixed diagonals, so the lattice makes their path readable. Step to the next open column, three over.
    ///   3. Pop the XL Leads when they bounce near an open column; their low bounce (under 9.1 u, and less under the
    ///      blocks) leaves the upper lattice to the Drifters.
    ///   4. M and S children drift the same diagonals: keep to the open columns and fire.
    /// Over-break check: no B tiles. Every enemy starts in an open column over floor Wick can stand on.
    /// </remarks>
    [Preserve]
    public sealed class W2_HornetLattice : StageSpec
    {
        public override string Id => "w2_hornet_lattice";
        public override string Name => "Hornet Lattice";
        public override int World => 2;
        public override int Difficulty => 4;
        public override int MinStageInWorld => 4;
        public override string Concept =>
            "Four Drifters ricochet through a lattice of floating blocks while two big Leads bounce underneath. Shoot up the open columns as they cross.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open14();
                for (int c = 3; c <= 27; c += 6) StageSketch.Box(grid, c, c + 1, 9, 9, '#');
                for (int c = 6; c <= 24; c += 6) StageSketch.Box(grid, c, c + 1, 5, 5, '#');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.BubbleAt(BubbleKind.Drifter, StageBuilder.TierL, 1, 12f, 1);
            b.BubbleAt(BubbleKind.Drifter, StageBuilder.TierL, 30, 12f, -1);
            b.BubbleAt(BubbleKind.Drifter, StageBuilder.TierL, 8, 12f, 1);
            b.BubbleAt(BubbleKind.Drifter, StageBuilder.TierL, 23, 12f, -1);
            b.BubbleAt(BubbleKind.Lead, StageBuilder.TierXL, 4, 3f, -1);
            b.BubbleAt(BubbleKind.Lead, StageBuilder.TierXL, 27, 3f, 1);
        }
    }
}
