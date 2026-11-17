using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 3 difficulty 1. Open sand and two Sinkers.</summary>
    [Preserve]
    public sealed class W3_SinkingDunes : StageSpec
    {
        public override string Id => "w3_sinking_dunes";
        public override string Name => "Sinking Dunes";
        public override int World => 3;
        public override int Difficulty => 1;
        public override int MaxStageInWorld => 1;
        public override string Concept => "Two Sinkers cross an open dune.";
        public override string[] Rows => BlankRows();
        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.Bubble(BubbleKind.Sinker, StageBuilder.TierL, -10f, 8f, -1);
            b.Bubble(BubbleKind.Sinker, StageBuilder.TierL, 10f, 8f, 1);
        }
    }
}
