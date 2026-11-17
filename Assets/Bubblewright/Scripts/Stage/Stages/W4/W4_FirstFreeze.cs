using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 4 difficulty 1. Ice on the sides, normal ground under Wick, two Frost bubbles.</summary>
    [Preserve]
    public sealed class W4_FirstFreeze : StageSpec
    {
        public override string Id => "w4_first_freeze";
        public override string Name => "First Freeze";
        public override int World => 4;
        public override int Difficulty => 1;
        public override int MaxStageInWorld => 1;
        public override string Concept => "Two Frost bubbles bounce over ice, with normal ground under Wick.";
        public override string[] Rows => BlankRows();
        public override string Ground => new string('I', 12) + new string('N', 8) + new string('I', 12);

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.Bubble(BubbleKind.Frost, StageBuilder.TierL, -10f, 9f, -1);
            b.Bubble(BubbleKind.Frost, StageBuilder.TierL, 10f, 9f, 1);
        }
    }
}
