using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 4 difficulty 3. Icy steps rise from a grippy valley floor to both walls.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W4, D3, MinStageInWorld 2, MaxStageInWorld 5.
    /// Concept: Icy steps rise from a grippy valley floor to both walls. Each Frost bounces over its own height, so Wick
    /// climbs, slides and plants to aim.
    /// Archetype(s): Stepped terraces. Structure: mirror-symmetric about x = 0. Four steps a side, each one row up and
    /// three columns wide (9-11, 6-8, 3-5, 0-2 and their mirrors); the top tile of every step is ice. Valley floor 12-19
    /// is normal ground.
    /// Enemies: a Frost XL over each first step and a Frost L over each third step, all moving outward and away from Wick.
    /// Pops: 15 + 15 + 7 + 7 = 44 (D3 band 35-65); shortest hard clock for slots 2-5 in W4 = 66 s -> max 105.
    /// Hitless plan (Moss, Heavy Boots): shoot the XLs from the valley as they come back over it; their children fall to
    /// the valley or the low steps. Climb a step at a time (one-row jumps) to reach the Ls over the third steps; an ice
    /// top slides, so face the next riser and let it stop you. Sliding off a step only drops one row.
    /// Over-break check: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W4_GlacierSteps : StageSpec
    {
        public override string Id => "w4_glacier_steps";
        public override string Name => "Glacier Steps";
        public override int World => 4;
        public override int Difficulty => 3;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "Icy steps rise from a grippy valley floor to both walls. Each Frost bounces over its own height, so Wick climbs, slides and plants to aim.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open();
                // Step k (1-4) covers columns 12 - 3k to 14 - 3k (and the mirror), rows 0 to k - 1; its top tile is ice.
                for (int k = 1; k <= 4; k++)
                {
                    int lo = 12 - 3 * k;
                    StageSketch.Box(grid, lo, lo + 2, 0, k - 1, '#');
                    StageSketch.Box(grid, lo, lo + 2, k - 1, k - 1, 'I');
                    StageSketch.Box(grid, 29 - lo, 31 - lo, 0, k - 1, '#');
                    StageSketch.Box(grid, 29 - lo, 31 - lo, k - 1, k - 1, 'I');
                }
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.BubbleAt(BubbleKind.Frost, StageBuilder.TierXL, 9, 9f, -1);
            b.BubbleAt(BubbleKind.Frost, StageBuilder.TierXL, 22, 9f, 1);
            b.BubbleAt(BubbleKind.Frost, StageBuilder.TierL, 4, 10f, -1);
            b.BubbleAt(BubbleKind.Frost, StageBuilder.TierL, 27, 10f, 1);
        }
    }
}
