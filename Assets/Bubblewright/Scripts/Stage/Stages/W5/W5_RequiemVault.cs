using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 5 difficulty 4. Wick starts between two breakable vaults; Ghosts walk through them, Clingers wait inside.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W5, D4, MinStageInWorld 4.
    /// Concept: two breakable vaults hold ceramic Trigloops under patient Clinger guests; the Ghosts pass through the
    /// vault walls into Wick's narrow court.
    /// Archetype: Breakable Vault (twin). Guest: Clinger (World 2), named in Concept; its job is the ambush: a patient
    /// ceiling Clinger hangs inside each vault and drops when Wick steps in under it.
    /// Structure: mirror-symmetric about x = 0.
    /// Enemies: ceramic Trigloop L inside each vault (columns 6 and 25); Clinger M on each vault ceiling (columns 9
    /// and 22, nearest the court, so it is the first thing he meets); Ghost XL over each vault roof (columns 7 and 24),
    /// drifting outward first.
    /// Pops: 14 + 14 + 3 + 3 + 15 + 15 = 64 (D4 band 55-90); slot 4-5 hard clock 78 s allows 124.
    /// Hitless plan: shoot the Ghosts as they sink into the court while solid. Chip a vault wall at its foot (column 12
    /// or 19), step back, let the Clinger drop and shoot it, then go in and pop the ceramic Trigloop (two hits) and its
    /// children in the 5-row vault. Same on the other side. The outer strips (columns 0-2, 29-31) open once a vault's
    /// outer wall is chipped.
    /// Over-break: with every B gone the stage is one open floor; nothing is stranded.
    /// </remarks>
    [Preserve]
    public sealed class W5_RequiemVault : StageSpec
    {
        public override string Id => "w5_requiem_vault";
        public override string Name => "Requiem Vault";
        public override int World => 5;
        public override int Difficulty => 4;
        public override int MinStageInWorld => 4;
        public override string Concept =>
            "Two breakable vaults hold ceramic Trigloops under patient Clinger guests. The Ghosts pass through the vault walls into your court.";

        public override string[] Rows
        {
            get
            {
                // Left vault: walls on 3 and 12, lid on row 5. Right vault: walls on 19 and 28, lid on row 5. All breakable.
                // Insides: columns 4-11 and 20-27, rows 0-4. Wick's court is columns 13-18.
                var grid = StageSketch.Open14();
                StageSketch.Box(grid, 3, 3, 0, 5, 'B');
                StageSketch.Box(grid, 12, 12, 0, 5, 'B');
                StageSketch.Box(grid, 4, 11, 5, 5, 'B');
                StageSketch.Box(grid, 19, 19, 0, 5, 'B');
                StageSketch.Box(grid, 28, 28, 0, 5, 'B');
                StageSketch.Box(grid, 20, 27, 5, 5, 'B');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.BubbleAt(BubbleKind.Trigloop, StageBuilder.TierL, 6, 2.5f, -1, ceramic: true);
            b.BubbleAt(BubbleKind.Trigloop, StageBuilder.TierL, 25, 2.5f, 1, ceramic: true);
            b.Clinger(StageBuilder.TierM, StageMap.CenterX(9), 4.2f, ClingerMount.Ceiling, -1);
            b.Clinger(StageBuilder.TierM, StageMap.CenterX(22), 4.2f, ClingerMount.Ceiling, 1);
            b.BubbleAt(BubbleKind.Ghost, StageBuilder.TierXL, 7, 10f, -1);
            b.BubbleAt(BubbleKind.Ghost, StageBuilder.TierXL, 24, 10f, 1);
        }
    }
}
