using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 1 difficulty 1. The open roof the later rooftop stages grow out of.</summary>
    [Preserve]
    public sealed class W1_FirstPop : StageSpec
    {
        public override string Id => "w1_first_pop";
        public override string Name => "First Pop";
        public override int World => 1;
        public override int Difficulty => 1;
        public override int MaxStageInWorld => 1;
        public override string Concept => "Two large Gloops drift apart over an open court, with a low step on each side.";
        public override string[] Rows
        {
            get
            {
                var rows = BlankRows14();
                rows[rows.Length - 1] = "..######................######..";
                return rows;
            }
        }
        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.Bubble(BubbleKind.Gloop, StageBuilder.TierL, -10f, 9f, -1);
            b.Bubble(BubbleKind.Gloop, StageBuilder.TierL, 6f, 9f, 1);
        }
    }
}
