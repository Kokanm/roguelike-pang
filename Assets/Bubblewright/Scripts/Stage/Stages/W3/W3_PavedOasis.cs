using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 3 difficulty 3. A raised stone plaza in the sand: Sinkers keep their bounce on stone and lose it on sand.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W3, D3, MinStageInWorld 2, MaxStageInWorld 5.
    /// Concept: a raised stone plaza in the sand. Sinkers keep their bounce on the stone and lose it fast on the sand,
    ///   so fight them at the edges.
    /// Archetype(s): Pads (one wide plaza), using normal stone (N) against World 3 sand.
    /// Structure: mirror-symmetric about x = 0. A one-row plaza of N stone on columns 10-21 (feet 1, 12 wide); sand
    ///   courts on 0-9 and 22-31 (10 wide). Every surface is wide and Wick can stand on it.
    /// Enemies and why each one is where it is: a Sinker L over each sand court near the wall heading inward, a ceramic
    ///   Sinker L over each court near the plaza heading outward, and a Trigloop M over each plaza edge heading outward,
    ///   so the plaza keeps something bouncing while the courts slow everything down. No guest.
    /// Pops: 7 + 7 + 8 + 8 + 4 + 4 = 38 (D3 band 35-65); shortest hard clock for slots 2-5 in W3 = 69 s -> max 110 pops.
    /// Hitless plan for Moss with Heavy Boots and the default harpoon: start in the middle of the plaza. Pop the
    ///   Trigloops from the plaza as they come back. Step down into a court and split the Sinkers there, where their
    ///   bounce dies fast; their children roll along the court floor and turn at the plaza step, so fire in front of
    ///   one and hop up onto the plaza. A Sinker child bouncing on the plaza stays high: pop it from below as it crosses.
    /// Over-break check: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W3_PavedOasis : StageSpec
    {
        public override string Id => "w3_paved_oasis";
        public override string Name => "Paved Oasis";
        public override int World => 3;
        public override int Difficulty => 3;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "A raised stone plaza in the sand. Sinkers keep their bounce on the stone and lose it fast on the sand, so fight them at the edges.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open14();
                StageSketch.Box(grid, 10, 21, 0, 0, 'N');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f, 1f);
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 2, 10f, 1);
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 29, 10f, -1);
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 7, 10f, -1, ceramic: true);
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 24, 10f, 1, ceramic: true);
            b.BubbleAt(BubbleKind.Trigloop, StageBuilder.TierM, 12, 10f, -1);
            b.BubbleAt(BubbleKind.Trigloop, StageBuilder.TierM, 19, 10f, 1);
        }
    }
}
