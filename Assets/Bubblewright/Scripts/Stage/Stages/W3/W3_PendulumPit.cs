using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 3 difficulty 2. A steel ball swings across a sunken basin between two balconies.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W3, D2, MinStageInWorld 2, MaxStageInWorld 5.
    /// Concept: a steel ball swings across the sunken basin; Wick works from the balconies and drops in to finish Sinkers.
    /// Archetype(s): Pads (two wide balconies) with a steel roller as the moving hazard.
    /// Structure: mirror-symmetric about x = 0. Balconies on columns 0-8 and 23-31, three rows high (feet 3). A ladder
    ///   on each inner face (columns 9 and 22, a T on row 2) climbs back out of the 14-wide basin. No ground gaps: the
    ///   basin is the pit.
    /// Enemies and why each one is where it is: Gloop XL over the middle of the basin, heading away from Wick. One
    ///   Sinker L over each balcony: rolling Sinkers end up on the balconies (10 wide, Wick stands there) or in the
    ///   basin, where the steel ball swings. The steel ball is not required. No guest.
    /// Pops: 15 + 7 + 7 = 29 (D2 band 20-40); shortest hard clock for slots 2-5 in W3 = 69 s -> max 110 pops.
    /// Hitless plan for Moss with Heavy Boots and the default harpoon: start on the left balcony at column 2. Pop the
    ///   near Sinker as it bounces over the balcony and the XL when it crosses the balcony edge. Cross by dropping into
    ///   the basin behind the steel ball, jumping it (1.2 u, jump 1.9 u) when it swings back, and climbing the far
    ///   ladder. Rolling children in the basin: drop in while the ball is on the other side, fire in front of the child,
    ///   step back to the ladder and climb.
    /// Over-break check: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W3_PendulumPit : StageSpec
    {
        public override string Id => "w3_pendulum_pit";
        public override string Name => "Pendulum Pit";
        public override int World => 3;
        public override int Difficulty => 2;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "A steel ball swings back and forth across a sunken basin. Work from the two balconies and drop in to finish what rolls down.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 0, 8, 0, 2, '#');
                StageSketch.Box(grid, 23, 31, 0, 2, '#');
                StageSketch.Box(grid, 9, 9, 0, 1, 'H');
                StageSketch.Box(grid, 22, 22, 0, 1, 'H');
                grid[2][9] = 'T';
                grid[2][22] = 'T';
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(2, 3);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 15, 9f, 1);
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 7, 10f, 1);
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 27, 10f, 1);
            b.Roller(StageMap.CenterX(13), 0f, 1, steel: true);
        }
    }
}
