using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 3 difficulty 4. A full-width slotted roof: ceramic Trigloops and a wave above, a steel ball sweeping it.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W3, D4, MinStageInWorld 4, MaxStageInWorld 5.
    /// Concept: Wick holds a slotted sandstone bunker. Ceramic Trigloops and a late wave bounce on the roof, a steel ball
    ///   sweeps it, and two bricks can be shot out to widen the centre slots.
    /// Archetype(s): Slotted Bunker (W1_SlottedBunker), with the whole sky above the roof.
    /// Structure: mirror-symmetric about x = 0. A roof on row 4 from wall to wall, so nothing big ever comes down and
    ///   the steel ball turns at the arena walls instead of rolling off. Six 1-tile firing slots on 3, 8, 13 and 18, 23,
    ///   28. The bricks on 14 and 17 sit beside the two centre slots: shooting one out makes a 2-tile sieve that lets M
    ///   children rain in, a faster but riskier fight. Wick has 4 rows under the roof.
    /// Enemies and why each one is where it is: a ceramic Trigloop XL over slot 13 and a ceramic Trigloop L over each
    ///   outer slot (3 and 28), so each starts where a shot can reach it. One wave of two Gloop L drops from the
    ///   ceiling later. The steel ball on the roof is not required. No Sinkers: a rolling Sinker never crosses a slot,
    ///   so one on this roof could not be shot.
    /// Pops: 41 + 14 + 14 + wave 7 + 7 = 83 (D4 band 55-90); shortest hard clock for slots 4-5 in W3 = 75.9 s -> max
    ///   121 pops.
    /// Hitless plan for Moss with Heavy Boots and the default harpoon: start under the middle of the roof. Walk to a slot
    ///   and fire up as a bubble crosses it, two hits for each ceramic one; move between slots as they bounce. S children
    ///   fall through the slots now and then: pop them under the roof (an S bounces 6 u, the roof holds it to 4 rows,
    ///   so it passes over a standing Wick only if he jumps). Open a centre sieve when ready to take M children inside.
    /// Over-break check: the two B tiles are in the roof. With both gone the centre slots are 2 wide; the roof still
    ///   spans the arena, nothing is stranded, and Wick stands on the ground as before.
    /// </remarks>
    [Preserve]
    public sealed class W3_SandstormBunker : StageSpec
    {
        public override string Id => "w3_sandstorm_bunker";
        public override string Name => "Sandstorm Bunker";
        public override int World => 3;
        public override int Difficulty => 4;
        public override int MinStageInWorld => 4;
        public override string Concept =>
            "Hold a slotted sandstone bunker. Ceramic Trigloops and a late wave bounce on the roof while a steel ball sweeps across it.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 0, 31, 4, 4, '#');
                grid[4][3] = '.';
                grid[4][8] = '.';
                grid[4][13] = '.';
                grid[4][18] = '.';
                grid[4][23] = '.';
                grid[4][28] = '.';
                grid[4][14] = 'B';
                grid[4][17] = 'B';
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.BubbleAt(BubbleKind.Trigloop, StageBuilder.TierXL, 13, 9.5f, 1, ceramic: true);
            b.BubbleAt(BubbleKind.Trigloop, StageBuilder.TierL, 3, 9f, -1, ceramic: true);
            b.BubbleAt(BubbleKind.Trigloop, StageBuilder.TierL, 28, 9f, 1, ceramic: true);
            b.Roller(StageMap.CenterX(20), 5f, 1, steel: true);
            b.Wave(0, BubbleKind.Gloop, StageBuilder.TierL, -8f, 1);
            b.Wave(0, BubbleKind.Gloop, StageBuilder.TierL, 8f, -1);
        }
    }
}
