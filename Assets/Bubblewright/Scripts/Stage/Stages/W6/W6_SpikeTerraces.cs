using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 6 difficulty 3. Four one-row terraces climb to the right; a spike strip on each rises in turn, up the steps.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W6, D3, MinStageInWorld 2, MaxStageInWorld 5.
    /// Concept: the spike strips rise one terrace after another, so the safe step moves up the slope like a wave.
    /// Archetype(s): Stepped terraces (W1_TerraceClimb). Structure: a deliberate climb to the right (mirrors to the left).
    /// Enemies: an XL Scorch over each of the three upper terraces and a Lead L over the bottom one, moving away from
    ///   Wick's start. Flames land on whichever step a bubble hits, so a step can be blocked by fire as well as spikes.
    /// Pops: 7 + 15 + 15 + 15 = 52 (D3 band 35-65); shortest hard clock for slots 2-5 in W6 = 78 s -> max 124 pops.
    /// Hitless plan (Moss, Heavy Boots): pop the Lead L from the bottom landing off its strip (columns 0-2 or 6-7). Hop
    ///   up one row per terrace; on each one stand off the strip (columns 8-9 or 14-15, 16-17 or 22-23, 24-25 or 30-31),
    ///   wait for the strip to drop when you must cross it, and shoot the XL over that terrace.
    /// Over-break check: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W6_SpikeTerraces : StageSpec
    {
        public override string Id => "w6_spike_terraces";
        public override string Name => "Spike Terraces";
        public override int World => 6;
        public override int Difficulty => 3;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "Four terraces climb to the right, each with a spike strip. The strips rise in turn up the steps, under a Scorch per landing.";

        public override string[] Rows
        {
            get
            {
                // Landings: ground 0-7, then rows 0, 1 and 2 as the tops of 8-15, 16-23 and 24-31. One row per hop.
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 8, 31, 0, 0, '#');
                StageSketch.Box(grid, 16, 31, 1, 1, '#');
                StageSketch.Box(grid, 24, 31, 2, 2, '#');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(1);
            // One strip per landing, in climbing order, so each rises 0.8 s after the one below it.
            b.Spike(3, 3);
            b.Spike(10, 4, 0);
            b.Spike(18, 4, 1);
            b.Spike(26, 4, 2);
            b.BubbleAt(BubbleKind.Lead, StageBuilder.TierL, 5, 8f, 1);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierXL, 12, 9f, 1);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierXL, 20, 10f, 1);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierXL, 28, 10f, 1);
        }
    }
}
