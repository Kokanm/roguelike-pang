using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 2 difficulty 1. Two patient ceiling Clingers, the forest's own enemy.</summary>
    [Preserve]
    public sealed class W2_CanopyAmbush : StageSpec
    {
        public override string Id => "w2_canopy_ambush";
        public override string Name => "Canopy Ambush";
        public override int World => 2;
        public override int Difficulty => 1;
        public override int MaxStageInWorld => 1;
        public override string Concept => "Two patient Clingers wait on the ceiling.";
        public override string[] Rows => BlankRows();
        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.Clinger(StageBuilder.TierL, -8f, 11f, dir: 1);
            b.Clinger(StageBuilder.TierL, 8f, 11f, dir: -1);
        }
    }
}
