using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 3 difficulty 4. Climb a stepped pyramid past ceramic rollers, then face Sinkers and guest Clingers upstairs.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W3, D4, MinStageInWorld 3, MaxStageInWorld 5, 2 storeys.
    /// Concept: climb a stepped pyramid over sand gaps past ceramic rollers; up top, Sinkers bounce while guest Clingers
    ///   wait on the ceiling.
    /// Archetype(s): Stepped terraces (a pyramid) + Storey Tower Climb. Guest: Clinger (World 2), on storey 2 only.
    /// Structure: mirror-symmetric about x = 0. Storey 1: a three-tier pyramid (feet 1 on columns 6-25, feet 2 on 10-21,
    ///   feet 3 on 14-17), every terrace 4 wide. One-column sand gaps on 5 and 26 fence the two ground courts (0-4 and
    ///   27-31), where the rollers turn. Twin ladders on 14 and 17 run from the pyramid top through hatches on row 13.
    ///   Storey 2 is an open hall.
    /// Enemies and why each one is where it is: storey 1, a ceramic big roller in each ground court (the gaps keep them
    ///   off the pyramid), a ceramic Gloop L over each court, and a Gloop XL over the pyramid top heading away from
    ///   Wick. Storey 2, a Sinker L near each wall heading outward and two patient ceiling Clingers L (the guest) over
    ///   columns 8 and 23, 6 u from either hatch: they drop when Wick walks under them.
    /// Pops: 4 + 4 + 8 + 8 + 15 (storey 1) + 7 + 7 + 7 + 7 (storey 2) = 67 (D4 band 55-90); shortest hard clock for
    ///   slots 3-5 in W3 = 72.45 s -> max 115 pops.
    /// Hitless plan for Moss with Heavy Boots and the default harpoon: start on the second terrace at column 11. Split
    ///   the XL as it bounces on the pyramid and pop its family from the terraces (each step is 1 row). Drop to the
    ///   lowest terrace, wait for the left roller to turn at the gap, jump the 1-column gap to the court (2 u), fire
    ///   in front of the roller twice (ceramic) and jump back. Pop the ceramic Gloop L over the court the same way.
    ///   Then the right side. Climb a ladder from the pyramid top. Upstairs: shoot the Sinkers as they bounce; walk
    ///   under a Clinger only when ready, step back as it wobbles, and shoot it as it drops.
    /// Over-break check: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W3_MirageZiggurat : StageSpec
    {
        public override string Id => "w3_mirage_ziggurat";
        public override string Name => "Mirage Ziggurat";
        public override int World => 3;
        public override int Difficulty => 4;
        public override int MinStageInWorld => 3;
        public override int Storeys => 2;
        public override string Concept =>
            "Climb a stepped pyramid over sand gaps past ceramic rollers. Up top, Sinkers bounce while guest Clingers wait on the ceiling.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open14(2);
                StageSketch.Box(grid, 6, 25, 0, 0, '#');
                StageSketch.Box(grid, 10, 21, 1, 1, '#');
                StageSketch.Box(grid, 14, 17, 2, 2, '#');
                // Floor of storey 2, and twin ladders from the pyramid top through the hatches.
                StageSketch.Box(grid, 0, 31, 14, 14, '#');
                for (int y = 3; y <= 14; y++)
                {
                    grid[y][14] = y == 14 ? 'T' : 'H';
                    grid[y][17] = y == 14 ? 'T' : 'H';
                }
                return StageSketch.Lines(grid);
            }
        }

        //                                   01234567890123456789012345678901
        public override string Ground => "=====_====================_=====";

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(11, 2);
            // Storey 1.
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 15, 8f, 1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierL, 2, 9f, 1, ceramic: true);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierL, 29, 9f, -1, ceramic: true);
            b.Roller(StageMap.CenterX(1), 0f, 1, ceramic: true);
            b.Roller(StageMap.CenterX(30), 0f, -1, ceramic: true);
            // Storey 2.
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 3, 22f, -1);
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 28, 22f, 1);
            b.Clinger(StageBuilder.TierL, StageMap.CenterX(8), 26f, ClingerMount.Ceiling, -1);
            b.Clinger(StageBuilder.TierL, StageMap.CenterX(23), 26f, ClingerMount.Ceiling, 1);
        }
    }
}
