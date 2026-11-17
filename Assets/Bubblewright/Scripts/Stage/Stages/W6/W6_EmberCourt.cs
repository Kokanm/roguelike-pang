using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 6 difficulty 1. Open stone and two Scorch bubbles. The full court, with spikes, comes later.</summary>
    [Preserve]
    public sealed class W6_EmberCourt : StageSpec
    {
        public override string Id => "w6_ember_court";
        public override string Name => "Ember Court";
        public override int World => 6;
        public override int Difficulty => 1;
        public override int MaxStageInWorld => 1;
        public override string Concept => "Two Scorch bubbles bounce over open stone.";
        public override string[] Rows => BlankRows();
        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.Bubble(BubbleKind.Scorch, StageBuilder.TierL, -10f, 9f, -1);
            b.Bubble(BubbleKind.Scorch, StageBuilder.TierL, 10f, 9f, 1);
        }
    }
}
