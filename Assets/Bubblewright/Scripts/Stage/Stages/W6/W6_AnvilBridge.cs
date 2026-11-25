using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 6 difficulty 3. A long bridge with four staggered spike strips over a floor patrolled by a steel ball.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W6, D3, MinStageInWorld 2, MaxStageInWorld 5.
    /// Concept: the bridge is where Wick shoots from, but its spike strips rise in turn along it; below, a steel ball rolls
    ///   the floor and cannot be popped.
    /// Archetype(s): Pads (one long bridge) + rolling ball floor. Structure: mirror-symmetric about x = 0.
    /// Enemies: two ceramic XL Scorches over the bridge, moving outward (two hits each to start). Two M Scorches start
    ///   over the open floor beyond the bridge ends and bounce under it. The steel ball is not required.
    /// Pops: 16 + 16 + 3 + 3 = 38 (D3 band 35-65); shortest hard clock for slots 2-5 in W6 = 78 s -> max 124 pops.
    /// Hitless plan (Moss, Heavy Boots): from the bridge's safe middle (columns 14-17) shoot each ceramic XL twice as it
    ///   comes back in; step along the bridge between strips (each strip rises 0.8 s after the one before). Climb down a
    ///   ladder at an end (column 6 or 25) for the M Scorches and the children that fall off, jumping the steel ball as it
    ///   passes, and climb back up.
    /// Over-break check: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W6_AnvilBridge : StageSpec
    {
        public override string Id => "w6_anvil_bridge";
        public override string Name => "Anvil Bridge";
        public override int World => 6;
        public override int Difficulty => 3;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "Spike strips rise in turn along a long bridge under two ceramic Scorches. A steel ball rolls the floor below.";

        public override string[] Rows
        {
            get
            {
                // Bridge on row 5 from column 6 to 25, with a ladder up each end. Five rows under it for the M bubbles and the ball.
                var grid = StageSketch.Open14();
                StageSketch.Box(grid, 6, 25, 5, 5, '#');
                StageSketch.Box(grid, 6, 6, 0, 4, 'H');
                StageSketch.Box(grid, 25, 25, 0, 4, 'H');
                grid[5][6] = 'T';
                grid[5][25] = 'T';
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(15, 6);
            // Four strips on the bridge top, rising one after another; columns 14-17 stay clear.
            b.Spike(8, 3, 5);
            b.Spike(12, 2, 5);
            b.Spike(18, 2, 5);
            b.Spike(21, 3, 5);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierXL, 9, 10f, -1, ceramic: true);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierXL, 22, 10f, 1, ceramic: true);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierM, 2, 5f, 1);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierM, 29, 5f, -1);
            b.Roller(StageMap.CenterX(15), 0f, 1, steel: true);
        }
    }
}
