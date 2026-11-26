using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 3 difficulty 4. A three-storey tower; each floor adds one desert threat.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W3, D4, MinStageInWorld 4, MaxStageInWorld 5, 3 storeys.
    /// Concept: climb a three-floor tower. Sinkers hold the ground floor, a steel Pogo guards the middle one, and ceramic
    ///   bubbles wait at the top.
    /// Archetype(s): Storey Tower Climb (W1_ThreeRoofs), with the ladders zig-zagging so each floor is crossed.
    /// Structure: three open floors. The ladder from floor 1 is on column 28, the one from floor 2 on column 3, so Wick
    ///   arrives at one end of each floor and leaves from the other. Floor 2 has a steel Pogo M in the middle as a timed
    ///   gate; floor 3 has two pillars hanging from the ceiling (columns 8 and 23, down to row 31) with 2 rows under them:
    ///   Wick, M and S pass underneath, L and XL stay in their own section. The pillars have no tops a Sinker could
    ///   settle on, and every floor is flat and wide, so rolling Sinkers are always in reach.
    /// Enemies and why each one is where it is: floor 1, two Sinker L heading right (away from Wick) and a Gloop XL in
    ///   the middle. Floor 2, a ceramic Gloop L in each half, so he must cross the Pogo gate. Floor 3, a ceramic
    ///   Trigloop L and a Sinker L on the far side from the hatch. Every enemy is more than 3 u from a hatch. No guest.
    /// Pops: 7 + 7 + 15 (floor 1) + 8 + 8 (floor 2) + 14 + 7 (floor 3) = 66 (D4 band 55-90); shortest hard clock for
    ///   slots 4-5 in W3 = 75.9 s -> max 121 pops.
    /// Hitless plan for Moss with Heavy Boots and the default harpoon: start at column 3 on floor 1. Pop the Sinkers and
    ///   the XL family on the open floor; rolling children: fire in front, step back. Climb column 28. On floor 2 pop
    ///   the right ceramic Gloop near the hatch, pass the steel Pogo while it is high, pop the left one and climb
    ///   column 3. On floor 3 the L bubbles cannot reach the hatch section: walk under the left pillar, pop the ceramic
    ///   Trigloop in the middle section, then the Sinker in the right one; M and S children that slip under a pillar
    ///   are popped where they land.
    /// Over-break check: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W3_SunspireTower : StageSpec
    {
        public override string Id => "w3_sunspire_tower";
        public override string Name => "Sunspire Tower";
        public override int World => 3;
        public override int Difficulty => 4;
        public override int MinStageInWorld => 4;
        public override int Storeys => 3;
        public override string Concept =>
            "Climb a three-floor tower. Sinkers hold the ground floor, a steel Pogo guards the middle one, and ceramic bubbles wait at the top.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open(3);
                StageSketch.Box(grid, 0, 31, 14, 14, '#');
                StageSketch.Box(grid, 0, 31, 28, 28, '#');
                for (int y = 0; y <= 14; y++) grid[y][28] = y == 14 ? 'T' : 'H';
                for (int y = 15; y <= 28; y++) grid[y][3] = y == 28 ? 'T' : 'H';
                StageSketch.Box(grid, 8, 8, 31, 41, '#');
                StageSketch.Box(grid, 23, 23, 31, 41, '#');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(3);
            // Floor 1.
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 10, 9f, 1);
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 21, 9f, 1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 16, 9f, 1);
            // Floor 2.
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierL, 8, 22f, -1, ceramic: true);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierL, 22, 22f, -1, ceramic: true);
            b.SteelPogo(StageBuilder.TierM, 0f, 15f);
            // Floor 3.
            b.BubbleAt(BubbleKind.Trigloop, StageBuilder.TierL, 16, 37f, 1, ceramic: true);
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 27, 37f, 1);
        }
    }
}
