using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>
    /// World 1 boss arena, placeholder: today's flat floor under one screen, until the world's boss has an arena of its
    /// own (a BossDef names it with ArenaId). Boss arenas are never picked for an ordinary stage slot.
    /// </summary>
    [Preserve]
    public sealed class W1_BossArena : StageSpec
    {
        public override string Id => "w1_boss_arena";
        public override string Name => "W1 Boss Arena";
        public override int World => 1;
        public override int Difficulty => 4;
        public override bool IsBossArena => true;
        public override bool CanMirror => false;
        public override string Concept => "A flat floor under one open screen for the world 1 boss.";

        public override string[] Rows => BlankRows();
        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
        }
    }
}
