using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 5 difficulty 1. An open nave and two Ghosts.</summary>
    [Preserve]
    public sealed class W5_HauntedNave : StageSpec
    {
        public override string Id => "w5_haunted_nave";
        public override string Name => "Haunted Nave";
        public override int World => 5;
        public override int Difficulty => 1;
        public override int MaxStageInWorld => 1;
        public override string Concept => "Two Ghosts drift through an open nave.";
        public override string[] Rows => BlankRows();
        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.Bubble(BubbleKind.Ghost, StageBuilder.TierL, -10f, 9f, -1);
            b.Bubble(BubbleKind.Ghost, StageBuilder.TierL, 10f, 9f, 1);
        }
    }
}
