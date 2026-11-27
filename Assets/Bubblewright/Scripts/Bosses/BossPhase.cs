using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// One phase of a boss fight. A phase owns a segment of the health bar (hp), runs its own behaviour in Tick and
    /// ends when Done says so: by default when its segment is empty; a phase won by breaking parts overrides Done.
    /// When it ends the boss staggers, bullets clear, OnBreak makes the arena change for the next phase and a crate
    /// (and on the calm road maybe a heart) falls.
    /// </summary>
    public class BossPhase
    {
        public string name;
        /// <summary>Health of this phase's bar segment; 0 for a phase won by parts.</summary>
        public int hp;
        /// <summary>
        /// The storey whose hatch (the one up out of that storey) opens when this phase ends, or -1 for none. Only boss
        /// arenas open hatches this way: the storey's bubbles being cleared does nothing there.
        /// </summary>
        public int opensHatch = -1;

        /// <summary>The phase starts: place the boss, open and close its cores.</summary>
        public virtual void Enter(Boss boss) { }
        /// <summary>One tick of the phase while it is being fought (not while the boss staggers).</summary>
        public virtual void Tick(Boss boss, float dt, Vector2 target, float speedScale, int bubblesAlive) { }
        /// <summary>Called instead of Tick while the boss is not being fought (the intro is over but play is paused or ending).</summary>
        public virtual void Idle(Boss boss, float dt) { }
        /// <summary>The phase ends, before the stagger.</summary>
        public virtual void Exit(Boss boss) { }
        /// <summary>The arena change that follows this phase: open a hatch, break the floor, and so on.</summary>
        public virtual void OnBreak(Boss boss) { }

        /// <summary>True when the phase is over. By default its health segment is empty.</summary>
        public virtual bool Done(Boss boss) => boss.PhaseHp <= 0;

        /// <summary>How full this phase's bar segment is, 0 to 1. A phase won by parts shows its parts' health.</summary>
        public virtual float Fill(Boss boss) => hp > 0 ? Mathf.Clamp01(boss.PhaseHp / (float)hp) : 1f;
    }
}
