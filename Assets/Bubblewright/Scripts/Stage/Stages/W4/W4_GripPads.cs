using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 4 difficulty 2. An all-ice rink with three raised normal-grip pads.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W4, D2, MinStageInWorld 2, MaxStageInWorld 5.
    /// Concept: The rink floor is all ice. Three raised pads give grip to aim from, until a Frost lands on one and
    /// freezes it for a few seconds.
    /// Archetype(s): Pads. Structure: mirror-symmetric about x = 0. Pads one row high on 3-6, 14-17 and 25-28.
    /// Enemies: a Frost L over each side pad, and two more over the gaps between pads, all moving outward.
    /// Pops: 7 + 7 + 7 + 7 = 28 (D2 band 20-40); shortest hard clock for slots 2-5 in W4 = 66 s -> max 105.
    /// Hitless plan (Moss, Heavy Boots): from the middle pad shoot the inner Ls as they come back over it; hop down and
    /// skate to a side pad (one-row jump up), plant there and shoot the outer L and the children. A frozen pad lasts 4 s:
    /// wait on the next pad instead.
    /// Over-break check: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W4_GripPads : StageSpec
    {
        public override string Id => "w4_grip_pads";
        public override string Name => "Grip Pads";
        public override int World => 4;
        public override int Difficulty => 2;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "The rink floor is all ice. Three raised pads give grip to aim from, until a Frost lands on one and freezes it for a few seconds.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open14();
                StageSketch.Box(grid, 3, 6, 0, 0, '#');
                StageSketch.Box(grid, 14, 17, 0, 0, '#');
                StageSketch.Box(grid, 25, 28, 0, 0, '#');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => new string('I', StageMap.Cols);

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f, 1f);
            b.Bubble(BubbleKind.Frost, StageBuilder.TierL, -11f, 9f, -1);
            b.Bubble(BubbleKind.Frost, StageBuilder.TierL, 11f, 9f, 1);
            b.Bubble(BubbleKind.Frost, StageBuilder.TierL, -5.5f, 10f, -1);
            b.Bubble(BubbleKind.Frost, StageBuilder.TierL, 5.5f, 10f, 1);
        }
    }
}
