using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 3 difficulty 4. Sand gaps and a crawlway below, a sealed breakable vault of ceramic Sinkers above.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W3, D4, MinStageInWorld 4, MaxStageInWorld 5, 2 storeys.
    /// Concept: jump the sand gaps and crawl under the wall to the hatch. Upstairs, chip open the sealed vault and pop the
    ///   two ceramic Sinkers inside.
    /// Archetype(s): Crouch Duct Sniping (storey 1) + Breakable Vault (storey 2), joined as a Storey Tower Climb.
    /// Structure: both storeys mirror-symmetric about x = 0 (1-column sand gaps on 6 and 25, divider on 15-16, tunnel
    ///   roof 10-21 with slots on 12 and 19; vault walls 11 and 20, roof row 19). Only the hatch ladder on 28 is
    ///   one-sided: the stage is a left-to-right journey, and CanMirror plays it the other way too.
    /// Enemies and why each one is where it is: storey 1 holds no Sinker, because Wick cannot stand on the tunnel roof
    ///   and a Sinker that rolled there could not be shot. Left court: Gloop XL over Wick's side of the first gap, heading
    ///   away from his start. Right court: Gloop L beside the second gap and a ceramic Gloop L over the hatch ladder,
    ///   so the hatch side is only cleared after the crawl. Storey 2: Gloop XL on the far side and a Sinker L heading
    ///   away from the hatch outside the vault; two ceramic Sinker L sealed inside the B vault. The vault roof is B, so
    ///   a Sinker that settles on it is shot down from inside. Every enemy is at least 5 u from the hatch. No guest.
    /// Pops: 15 + 7 + 8 (storey 1) + 15 + 7 + 8 + 8 (storey 2) = 68 (D4 band 55-90); shortest hard clock for slots 4-5
    ///   in W3 = 75.9 s -> max 121 pops.
    /// Hitless plan for Moss with Heavy Boots and the default harpoon:
    ///   Storey 1: start on column 2, the XL drifts right, away. Shoot it from columns 0-5 as it comes back; jump the
    ///   1-column gap on 6 (2 u centre to centre, reach 2.7 u on sand) and finish its children from 7-9. Crouch into
    ///   the tunnel at 10, shuffle to the slot on 19 and fire up at the right-court bubbles as they cross; crawl out
    ///   at 22, finish them from 22-24 and, over the gap on 25, from 26-31. Climb the ladder on 28.
    ///   Storey 2: arrive on 28 with the Sinker L already heading left. Shoot the Sinker and the XL from the open floor
    ///   on 0-10 and 21-31; Sinkers lose height on sand and end up rolling, so they get easier. Then stand on 21 (or
    ///   10), chip the vault wall on 20 (or 11) twice at rows 15-16, step in under the 4-row roof and pop the two
    ///   ceramic Sinkers (2 hits each); fire on the far side of a rolling one and step back toward the broken wall.
    /// Over-break check: every B is a wall or roof of the vault standing on the solid row 14 floor; breaking all of
    ///   them leaves a flat floor, nothing stranded, no pit. Storey 1 has no B.
    /// </remarks>
    [Preserve]
    public sealed class W3_DuneVault : StageSpec
    {
        public override string Id => "w3_dune_vault";
        public override string Name => "Dune Vault";
        public override int World => 3;
        public override int Difficulty => 4;
        public override int MinStageInWorld => 4;
        public override int Storeys => 2;
        public override string Concept =>
            "Jump the sand gaps and crawl under the wall to the hatch. Upstairs, chip open the sealed vault and pop the two ceramic Sinkers inside.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open14(2);
                // Storey 1: crawl tunnel under row 1 from column 10 to 21, firing slots on 12 and 19, divider wall on 15-16.
                StageSketch.Box(grid, 10, 21, 1, 1, '#');
                grid[1][12] = '.';
                grid[1][19] = '.';
                StageSketch.Box(grid, 15, 16, 2, 13, '#');
                // Floor of storey 2, with the hatch ladder on column 28.
                StageSketch.Box(grid, 0, 31, 14, 14, '#');
                for (int y = 0; y <= 14; y++) grid[y][28] = y == 14 ? 'T' : 'H';
                // Storey 2: a sealed breakable vault, walls on 11 and 20, roof on row 19, four empty rows inside.
                StageSketch.Box(grid, 11, 11, 15, 19, 'B');
                StageSketch.Box(grid, 20, 20, 15, 19, 'B');
                StageSketch.Box(grid, 12, 19, 19, 19, 'B');
                return StageSketch.Lines(grid);
            }
        }

        //                                   01234567890123456789012345678901
        public override string Ground => "======_==================_======";

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(2);
            // Storey 1.
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 8, 9f, 1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierL, 24, 8f, -1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierL, 29, 9f, -1, ceramic: true);
            // Storey 2, outside the vault.
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 4, 22f, -1);
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 24, 22f, -1);
            // Storey 2, sealed in the vault.
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 14, 17.5f, -1, ceramic: true);
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 17, 17.5f, 1, ceramic: true);
        }
    }
}
