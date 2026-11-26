using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 4 difficulty 3. An icy deck with two orbiting pearl rollers and a steel Pogo under it.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W4, D3, MinStageInWorld 2, MaxStageInWorld 5.
    /// Concept: Two pearl rollers orbit a raised ice rink while Frost bubbles bounce above. A steel Pogo guards the lane
    /// under the rink.
    /// Archetype(s): Roller Orbit Loop. Structure: mirror-symmetric about x = 0. Deck on row 6 from 9 to 22, ice from 10 to
    /// 21; ladders on both ends (9 and 22) from the ground to the T ends of the deck. Six rows under it, seven above.
    /// Enemies: two Frost XL outside the deck, moving outward; a Frost L above the middle of the rink; two big pearl
    /// rollers start on the rink top rolling apart and then orbit the whole deck. A steel M Pogo bounces in place under
    /// the middle of the deck (not required): Wick passes it high in its hop, or goes over the rink.
    /// Pops: 15 + 15 + 7 + 3 + 3 = 43 (D3 band 35-65); shortest hard clock for slots 2-5 in W4 = 66 s -> max 105.
    /// Hitless plan (Moss, Heavy Boots): start under the deck at 12, shoot the rollers as they pass along the underside
    /// (or jump them). Step out past the ladder at 9 to shoot the left XL as it comes back, climb either ladder to the
    /// rink for the L and anything bouncing over it; the rink slides, so stop on a T end before aiming.
    /// Over-break check: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W4_RinkAndRollers : StageSpec
    {
        public override string Id => "w4_rink_and_rollers";
        public override string Name => "Rink and Rollers";
        public override int World => 4;
        public override int Difficulty => 3;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "Two pearl rollers orbit a raised ice rink while Frost bubbles bounce above. A steel Pogo guards the lane under the rink.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 10, 21, 6, 6, 'I');
                StageSketch.Box(grid, 9, 9, 0, 5, 'H');
                StageSketch.Box(grid, 22, 22, 0, 5, 'H');
                grid[6][9] = 'T';
                grid[6][22] = 'T';
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(12);
            b.BubbleAt(BubbleKind.Frost, StageBuilder.TierXL, 4, 9f, -1);
            b.BubbleAt(BubbleKind.Frost, StageBuilder.TierXL, 27, 9f, 1);
            b.Bubble(BubbleKind.Frost, StageBuilder.TierL, 0f, 11f, 1);
            b.Roller(-3f, 7f, -1);
            b.Roller(3f, 7f, 1);
            b.SteelPogo(StageBuilder.TierM, 0f);
        }
    }
}
