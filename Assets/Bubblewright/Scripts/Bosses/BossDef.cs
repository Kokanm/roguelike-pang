using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// One boss. Each boss is a sealed [Preserve] subclass in its own file under Bosses/Defs/W1 to W6, found by
    /// reflection like StageSpec and WeaponSpec, so two bosses never share a list. A def names the world, the boss,
    /// the arena stage, its phases and the fight's clock, and builds the boss's parts. A world with no def of its own
    /// yet gets the LegacyBoss adapter.
    /// </summary>
    public abstract class BossDef
    {
        /// <summary>World 1 to 6.</summary>
        public abstract int World { get; }
        public abstract string Name { get; }
        /// <summary>Body size in units; the boss's start box. Parts decide what a weapon can hurt.</summary>
        public abstract Vector2 Size { get; }
        /// <summary>The id of the authored arena StageSpec this boss fights in; null for the plain boss arena.</summary>
        public virtual string ArenaId => null;
        /// <summary>Where the boss's centre starts in its arena. Null (the legacy boss) skips the validator's start check.</summary>
        public virtual Vector2? StartSpot => null;
        /// <summary>
        /// World points of the cores Wick must be able to hit from the floor or a platform with the base moves.
        /// The validator checks each one is reachable. Cores a boss brings to Wick (it lands next to him) are not listed.
        /// </summary>
        public virtual IReadOnlyList<Vector2> CoreSpots => System.Array.Empty<Vector2>();
        /// <summary>
        /// True when Wick is hurt only by the boss's parts, not by its whole Size rectangle: a boss he can walk under
        /// (Sootjack on its stilts) sets this. The legacy boss hurts by its body.
        /// </summary>
        public virtual bool ContactFromParts => false;
        /// <summary>
        /// Where the boss's centre is k of the way (0 to 1, eased) through the stage intro, or null for the default slide
        /// down from above the ceiling. A boss with its own entrance (it bursts out of a chimney) overrides this; at k = 1
        /// it should return its StartSpot.
        /// </summary>
        public virtual Vector2? EntrancePosition(Boss boss, float k) => null;
        /// <summary>The storm clock of the fight in seconds. Tuning.bossTime unless the boss overrides it.</summary>
        public virtual float Clock => Tuning.I.bossTime;
        /// <summary>The clock on the hard road, which scales it like any stage.</summary>
        public float ClockFor(bool hard) => hard ? Clock * Tuning.I.hardTimeScale : Clock;
        /// <summary>True when hard road health scales each phase segment by Tuning.hardBossHpScale.</summary>
        public virtual bool ScalesHardHealth => true;

        /// <summary>The phases in order, each with its health segment. Build fresh ones on every call.</summary>
        protected abstract BossPhase[] BuildPhases(bool hard);

        /// <summary>Fresh phases for one fight; on the hard road each segment has hardBossHpScale more health.</summary>
        public BossPhase[] CreatePhases(bool hard)
        {
            var phases = BuildPhases(hard);
            if (hard && ScalesHardHealth)
                foreach (var p in phases)
                    if (p.hp > 0) p.hp = Mathf.CeilToInt(p.hp * Tuning.I.hardBossHpScale);
            return phases;
        }

        /// <summary>Adds the boss's armour, cores and parts with health.</summary>
        public virtual void BuildParts(Boss boss, bool hard) { }
        /// <summary>The hard road's extra moves: each boss adds its own here.</summary>
        public virtual void ApplyHard(Boss boss) { }
        /// <summary>The boss's sprites; a boss with its own painter overrides this.</summary>
        public virtual PlaceholderArt.BossSprites Sprites(Theme theme) =>
            PlaceholderArt.I.BossFor(theme, new BossSpec { name = Name, size = Size, shape = BossShape.Block });

        static BossDef[] all;
        /// <summary>Every boss def found in the assembly, by world.</summary>
        public static IReadOnlyList<BossDef> All => all ??= Discover();

        /// <summary>The new boss of a world, or today's boss (the LegacyBoss adapter) when the world has none yet.</summary>
        public static BossDef ForWorld(int world)
        {
            world = Mathf.Clamp(world, 1, 6);
            foreach (var def in All)
                if (def.World == world) return def;
            return new LegacyBoss(world);
        }

        static BossDef[] Discover()
        {
            var list = new List<BossDef>();
            foreach (var type in typeof(BossDef).Assembly.GetTypes())
            {
                if (type.IsAbstract || !type.IsSealed || !typeof(BossDef).IsAssignableFrom(type) || type == typeof(LegacyBoss)) continue;
                if (type.GetConstructor(Type.EmptyTypes) == null) continue;
                list.Add((BossDef)Activator.CreateInstance(type));
            }
            list.Sort((a, b) => a.World.CompareTo(b.World));
            return list.ToArray();
        }
    }
}
