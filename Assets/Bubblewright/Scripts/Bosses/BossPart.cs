using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// A named rectangle on a boss, in units relative to the boss's centre, so it moves with the boss. A part is
    /// armour (nothing gets through), a core (hurts the boss while Open) or a part with health of its own (an arm,
    /// a bellows, a valve: it takes damage while Open and is broken when Hp reaches 0). Parts can be added or
    /// removed per phase.
    /// </summary>
    public sealed class BossPart
    {
        public string Name;
        /// <summary>The rectangle, relative to the boss's centre.</summary>
        public Rect Local;
        /// <summary>A core takes the boss's health when it is Open. A closed core is armour.</summary>
        public bool IsCore;
        /// <summary>Whether shots get through right now. Armour never opens; cores and parts with health do.</summary>
        public bool Open;
        /// <summary>Health of a part with its own bar (arms, bellows, valves); 0 for armour and plain cores.</summary>
        public int MaxHp;
        /// <summary>True for a part Wick can pass by without being hurt (a stilt he walks past to get under the belly). Weapons still clink off it.</summary>
        public bool Harmless;
        public int Hp;

        public bool HasHealth => MaxHp > 0;
        public bool Broken => HasHealth && Hp <= 0;
        /// <summary>True when a hit lands: it is open and is a core or has health of its own.</summary>
        public bool Vulnerable => Open && !Broken && (IsCore || HasHealth);

        public static BossPart WithHealth(string name, Rect local, int hp) =>
            new BossPart { Name = name, Local = local, MaxHp = hp, Hp = hp };

        public Rect World(Boss boss) => new Rect(boss.Centre + Local.position, Local.size);

        /// <summary>Takes damage on a part with health; returns true if it just broke.</summary>
        public bool Damage(int amount)
        {
            if (!HasHealth || Hp <= 0 || amount <= 0) return false;
            Hp = Mathf.Max(0, Hp - amount);
            return Hp == 0;
        }
    }
}
