using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Today's boss of a world, wrapped as a BossDef: one phase with the health of BossSpec.HpForWorld, one open core
    /// (the whole body) and the walk and attacks from its BossSpec. BossDef.ForWorld returns it for a world with no
    /// new boss yet, so the game plays as before until each new boss lands. It is not found by reflection, and goes
    /// with BossSpec in Task 10.
    /// </summary>
    public sealed class LegacyBoss : BossDef
    {
        readonly int world;

        public BossSpec Spec { get; }

        public LegacyBoss(int world) : this(world, BossSpec.ForWorld(world)) { }

        public LegacyBoss(int world, BossSpec spec)
        {
            this.world = world;
            Spec = spec;
        }

        public override int World => world;
        public override string Name => Spec.name;
        public override Vector2 Size => Spec.size;
        public override float Clock => Tuning.I.legacyBossTime;
        // BossSpec.HpForWorld already applies the hard road's scale.
        public override bool ScalesHardHealth => false;

        protected override BossPhase[] BuildPhases(bool hard) =>
            new BossPhase[] { new LegacyPhase { name = "Legacy", hp = BossSpec.HpForWorld(world, hard) } };

        /// <summary>The whole body is one core that is always open: any harpoon or shot touch hurts.</summary>
        public override void BuildParts(Boss boss, bool hard) =>
            boss.AddPart(new BossPart { Name = "body", Local = new Rect(-Spec.size.x * 0.5f, -Spec.size.y * 0.5f, Spec.size.x, Spec.size.y), IsCore = true, Open = true });

        public override PlaceholderArt.BossSprites Sprites(Theme theme) => PlaceholderArt.I.BossFor(theme, Spec);

        sealed class LegacyPhase : BossPhase
        {
            public override void Tick(Boss boss, float dt, Vector2 target, float speedScale, int bubblesAlive) =>
                boss.TickLegacy(dt, target, speedScale, bubblesAlive);

            public override void Idle(Boss boss, float dt) => boss.IdleLegacy();
        }
    }
}
