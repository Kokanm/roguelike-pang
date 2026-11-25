using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 3 difficulty 1. Two Sinkers lose their bounce on the sand and roll in the valley between two stepped dunes.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W3, D1, MaxStageInWorld 1 (the world's intro).
    /// Concept: two Sinkers lose their bounce on the sand and end up rolling between two stepped dunes.
    /// Archetype(s): Stepped terraces, mirrored.
    /// Structure: mirror-symmetric about x = 0. Each dune rises from the valley in two 5-wide steps against its wall
    ///   (step tops at feet 1 on columns 5-9 and feet 2 on columns 0-4), so every surface a Sinker can roll on is wide
    ///   enough for Wick to stand on it, fire, and hop off.
    /// Enemies and why each one is where it is: one Sinker L over each upper step, heading for its wall, so the first
    ///   bounces happen away from Wick and the player sees them sink on the sand. No guest.
    /// Pops: 7 + 7 = 14 (D1 band 10-25); shortest hard clock for slot 1 in W3 = 65.55 s -> max 104 pops.
    /// Hitless plan for Moss with Heavy Boots and the default harpoon: start in the middle of the valley. Shoot each
    ///   Sinker while it bounces over the valley. Its M and S children sink fast; when one rolls along the valley floor,
    ///   fire in front of it and step back so it rolls into the cable. A child rolling on a dune step: hop onto that
    ///   step at its far end, fire, and drop back down; the child rolls into the cable when it turns at the edge.
    /// Over-break check: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W3_SinkingDunes : StageSpec
    {
        public override string Id => "w3_sinking_dunes";
        public override string Name => "Sinking Dunes";
        public override int World => 3;
        public override int Difficulty => 1;
        public override int MaxStageInWorld => 1;
        public override string Concept =>
            "Two Sinkers lose their bounce on the sand and end up rolling between the dunes. Shoot them as they roll by.";

        public override string[] Rows
        {
            get
            {
                // Two-step dunes against both walls: feet 1 on columns 5-9 and 22-26, feet 2 on columns 0-4 and 27-31.
                var grid = StageSketch.Open14();
                StageSketch.Box(grid, 0, 9, 0, 0, '#');
                StageSketch.Box(grid, 0, 4, 1, 1, '#');
                StageSketch.Box(grid, 22, 31, 0, 0, '#');
                StageSketch.Box(grid, 27, 31, 1, 1, '#');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 2, 9f, -1);
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 29, 9f, 1);
        }
    }
}
