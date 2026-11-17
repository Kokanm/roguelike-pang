using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 6 difficulty 1. A wide stone dais in the middle, spike strips on the two wings, two Scorch bubbles.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W6, D1, MaxStageInWorld 1.
    /// Concept: the lava intro. Scorch flames and spike strips, each met once, with a safe dais to stand on.
    /// Archetype(s): Pads (one wide dais). Structure: mirror-symmetric about x = 0.
    /// Enemies: one L Scorch over each wing, moving outward, so the first flames land on the wings, not the dais.
    /// Pops: 7 + 7 = 14 (D1 band 10-25); shortest hard clock for slot 1 in W6 = 75 s -> max 120 pops.
    /// Hitless plan (Moss, Heavy Boots): stand on the dais under a Scorch as it comes in and shoot; step down to a wing
    ///   for an L child only while that wing's strip is down (the two strips are 0.8 s apart), never through a flame.
    /// Over-break check: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W6_EmberCourt : StageSpec
    {
        public override string Id => "w6_ember_court";
        public override string Name => "Ember Court";
        public override int World => 6;
        public override int Difficulty => 1;
        public override int MaxStageInWorld => 1;
        public override string Concept =>
            "Two Scorch bubbles leave flames where they land. Spike strips cycle on the wings; the stone dais in the middle stays safe.";

        public override string[] Rows
        {
            get
            {
                // Dais one row high on columns 11-20: Wick hops on and off it. Wings 0-10 and 21-31 are open ground.
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 11, 20, 0, 0, '#');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(15, 1);
            b.Spike(3, 5);
            b.Spike(24, 5);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierL, 5, 9f, -1);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierL, 26, 9f, 1);
        }
    }
}
