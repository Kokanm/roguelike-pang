using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 4 difficulty 4. A three-room ice citadel under a slotted battlement, with a late wave.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W4, D4, MinStageInWorld 4, MaxStageInWorld 5.
    /// Concept: Wick holds the middle room of an icy citadel and fires through slots at ceramic Frost on the battlement.
    /// Side rooms open with B doors, and a late wave lands up top.
    /// Archetype(s): Slotted Bunker + Divided Twin Chambers. Structure: mirror-symmetric about x = 0. Battlement deck on
    /// row 5 across the screen with 1-tile slots on 4, 11, 20 and 27; ladders on 1 and 30 up to it. Room walls on 8 and 23
    /// (rows 2-4) with B doors on rows 0-1. The citadel floor is ice except Wick's normal patch on 14-17.
    /// Enemies: two ceramic Frost XL on the battlement over slots 11 and 20, moving outward; a Frost L in each side room;
    /// a late wave of two Frost L that drops onto the battlement.
    /// Pops: 16 + 16 + 7 + 7 + wave 7 + 7 = 60 (D4 band 55-90); shortest hard clock for slots 4-5 in W4 = 72 s -> max 115.
    /// Hitless plan (Moss, Heavy Boots): from the middle room fire up slots 11 and 20 as the ceramic XLs cross (2 hits
    /// each); their L children bounce on the battlement and cross the slots too. Chip a door (rows 0-1, 2 hits a tile) and
    /// pop the side room's L from the doorway; then use slot 4 or 27, or climb the ladder on 1 or 30 to the battlement,
    /// for the wave and the last children up there. The ice floor slides: stop on the normal patch or against a wall.
    /// Over-break check: with every B gone the room walls hang from row 2; the rooms join, ladders and slots stay usable.
    /// </remarks>
    [Preserve]
    public sealed class W4_WhiteoutCitadel : StageSpec
    {
        public override string Id => "w4_whiteout_citadel";
        public override string Name => "Whiteout Citadel";
        public override int World => 4;
        public override int Difficulty => 4;
        public override int MinStageInWorld => 4;
        public override string Concept =>
            "Wick holds the middle room of an icy citadel and fires through slots at ceramic Frost on the battlement. Side rooms open with B doors, and a late wave lands up top.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open14();
                StageSketch.Box(grid, 0, 31, 5, 5, '#');
                grid[5][4] = '.';
                grid[5][11] = '.';
                grid[5][20] = '.';
                grid[5][27] = '.';
                StageSketch.Box(grid, 1, 1, 0, 4, 'H');
                StageSketch.Box(grid, 30, 30, 0, 4, 'H');
                grid[5][1] = 'T';
                grid[5][30] = 'T';
                StageSketch.Box(grid, 8, 8, 2, 4, '#');
                StageSketch.Box(grid, 23, 23, 2, 4, '#');
                StageSketch.Box(grid, 8, 8, 0, 1, 'B');
                StageSketch.Box(grid, 23, 23, 0, 1, 'B');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => new string('I', 14) + "NNNN" + new string('I', 14);

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.BubbleAt(BubbleKind.Frost, StageBuilder.TierXL, 11, 10f, -1, ceramic: true);
            b.BubbleAt(BubbleKind.Frost, StageBuilder.TierXL, 20, 10f, 1, ceramic: true);
            b.Bubble(BubbleKind.Frost, StageBuilder.TierL, -10.5f, 2.5f, -1);
            b.Bubble(BubbleKind.Frost, StageBuilder.TierL, 10.5f, 2.5f, 1);
            b.Wave(0, BubbleKind.Frost, StageBuilder.TierL, -6.5f, -1);
            b.Wave(0, BubbleKind.Frost, StageBuilder.TierL, 6.5f, 1);
        }
    }
}
