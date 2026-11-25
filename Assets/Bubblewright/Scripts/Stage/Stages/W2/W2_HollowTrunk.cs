using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 2 difficulty 3. Wall Clingers guard the ladders inside two hollow trunks; a breakable root vault joins them.</summary>
    /// <remarks>
    /// Stage Design Brief
    /// World / Difficulty / slots: W2, D3, MinStageInWorld 2, MaxStageInWorld 5
    /// Concept: Wick starts in a hollow trunk. Wall Clingers guard both trunk ladders, and a breakable root vault with
    ///   two Gloops is the only way across.
    /// Archetype(s): Breakable Vault + hollow trunks (ladder shafts with wall Clingers)
    /// Structure: mirror-symmetric about x = 0. Each trunk uses the arena wall as its outer wall: inside is columns 0-2
    ///   (29-31), a ladder on column 1 (30) up to a crown on row 9, and a 2-row door under the inner wall (column 3 / 28,
    ///   rows 2-8). The root vault is B walls on columns 11 and 20 (rows 0-4) and a B roof on row 4.
    /// Enemies and why each one is where it is: a patient M Clinger on each arena wall at y = 5, right beside the trunk
    ///   ladder, so climbing past it wakes it (it pushes off across the 3-wide trunk); shot from the trunk floor below
    ///   it never wakes. Two L Gloops sealed in the vault. Two XL Gloops start over the open groves (columns 7 / 24),
    ///   heading inward.
    /// Pops: 3 + 3 + 7 + 7 + 15 + 15 = 50 (D3 band 35-65); shortest hard clock for slots 2-5 in W2 = 54 s -> max 86.
    /// Hitless plan for Moss with Heavy Boots and the default harpoon:
    ///   1. Start inside the left trunk at column 2, under its crown; the Clinger is 5 u above (it wakes only when Wick is
    ///      within 1.5 u of its height). Step to column 0 and fire straight up: it pops before the ladder is ever used.
    ///   2. Walk out of the door (column 3) and pop the left XL and its children in the grove (columns 4-10), stepping
    ///      back into the trunk door for cover; the crown (via the ladder) is a perch over the grove.
    ///   3. Chip the vault's left wall (column 11, 2 hits per tile) from column 10, step in and pop the two L Gloops
    ///      (the vault roof keeps their bounce under 4 u). Chip the right wall from inside and walk out.
    ///   4. Pop the right XL from the right grove, then enter the right trunk by its door and shoot its Clinger from column
    ///      31 below it.
    /// Over-break check: with every B gone the vault is open floor; both trunks have doors and ladders, the crowns drop
    ///   to the groves, so every spot can still be left and every enemy is over floor Wick can stand on.
    /// </remarks>
    [Preserve]
    public sealed class W2_HollowTrunk : StageSpec
    {
        public override string Id => "w2_hollow_trunk";
        public override string Name => "Hollow Trunk";
        public override int World => 2;
        public override int Difficulty => 3;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "Wick starts in a hollow trunk. Wall Clingers guard both trunk ladders, and a breakable root vault with two Gloops is the only way across.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open14();
                // Trunks: inner walls with a 2-row door under them, crowns on row 9, ladders on 1 and 30.
                StageSketch.Box(grid, 3, 3, 2, 8, '#');
                StageSketch.Box(grid, 28, 28, 2, 8, '#');
                StageSketch.Box(grid, 0, 3, 9, 9, '#');
                StageSketch.Box(grid, 28, 31, 9, 9, '#');
                for (int y = 0; y <= 9; y++)
                {
                    grid[y][1] = y == 9 ? 'T' : 'H';
                    grid[y][30] = y == 9 ? 'T' : 'H';
                }
                // Root vault: B walls and a B roof, as in W1_BreakVault.
                StageSketch.Box(grid, 11, 11, 0, 4, 'B');
                StageSketch.Box(grid, 20, 20, 0, 4, 'B');
                StageSketch.Box(grid, 12, 19, 4, 4, 'B');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(2);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 7, 9f, 1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 24, 9f, -1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierL, 14, 2.5f, -1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierL, 17, 2.5f, 1);
            // Authored in the column next to the arena wall; the Clinger seats itself flush against it.
            b.Clinger(StageBuilder.TierM, StageMap.CenterX(0), 5f, ClingerMount.LeftWall, dir: 1);
            b.Clinger(StageBuilder.TierM, StageMap.CenterX(31), 5f, ClingerMount.RightWall, dir: -1);
        }
    }
}
