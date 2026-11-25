using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 6 difficulty 2. Two furnace walls split the floor into three chambers, each with its own spike strip.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W6, D2, MinStageInWorld 2, MaxStageInWorld 5.
    /// Concept: three chambers, one Scorch in each; the spike strips beat out of step (outer pair together, middle
    ///   opposite), and a patient Clinger Pogo (the guest) waits at the far end of each outer chamber.
    /// Archetype(s): Divided Twin Chambers (three of them) + Floor Clinger Pogo. Structure: mirror-symmetric about x = 0.
    /// Enemies: an L Scorch per chamber, held in it because the bridges over the walls leave only two rows. The guest
    ///   Clinger Pogos (M, patient) sit past the outer strips at columns 1 and 30, so Wick crosses the spikes to wake them.
    /// Pops: 7 + 7 + 7 + 3 + 3 = 27 (D2 band 20-40); shortest hard clock for slots 2-5 in W6 = 78 s -> max 124 pops.
    /// Hitless plan (Moss, Heavy Boots): pop the middle Scorch from the floor, stepping round its strip while it is up.
    ///   Climb the ladder on column 11, cross the bridge, climb down column 9. Pop the left Scorch, cross the left strip
    ///   while it is down, walk at the Pogo until it wobbles, step back and shoot it as it hangs from the top. Back over
    ///   both bridges (ladders 9-11 and 20-22) for the right chamber, the same way.
    /// Over-break check: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W6_FurnaceGates : StageSpec
    {
        public override string Id => "w6_furnace_gates";
        public override string Name => "Furnace Gates";
        public override int World => 6;
        public override int Difficulty => 2;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "Furnace walls split three chambers, each with a Scorch and a spike strip. Patient Clinger Pogos wait past the outer strips.";

        public override string[] Rows
        {
            get
            {
                // Walls on columns 10 and 21 up to row 10, a ladder on each face, and a bridge on row 11 over each wall.
                // Two rows above a bridge let Wick and M or S bubbles cross, never an L.
                var grid = StageSketch.Open14();
                StageSketch.Box(grid, 10, 10, 0, 10, '#');
                StageSketch.Box(grid, 9, 9, 0, 10, 'H');
                StageSketch.Box(grid, 11, 11, 0, 10, 'H');
                StageSketch.Box(grid, 9, 11, 11, 11, '#');
                grid[11][9] = 'T';
                grid[11][11] = 'T';
                StageSketch.Box(grid, 21, 21, 0, 10, '#');
                StageSketch.Box(grid, 20, 20, 0, 10, 'H');
                StageSketch.Box(grid, 22, 22, 0, 10, 'H');
                StageSketch.Box(grid, 20, 22, 11, 11, '#');
                grid[11][20] = 'T';
                grid[11][22] = 'T';
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(12);
            // The outer strips rise together; the middle one is half a cycle out of step.
            b.Spike(3, 4, phase: 0f);
            b.Spike(14, 4, phase: 2.4f);
            b.Spike(25, 4, phase: 0f);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierL, 5, 9f, -1);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierL, 18, 9f, 1);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierL, 26, 9f, 1);
            b.FloorClinger(StageBuilder.TierM, StageMap.CenterX(1), dir: 1);
            b.FloorClinger(StageBuilder.TierM, StageMap.CenterX(30), dir: -1);
        }
    }
}
