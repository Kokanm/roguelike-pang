using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>What BossFight.Hit did: nothing was touched, armour (or a closed core) clinked, or an open core took the damage.</summary>
    public enum HitResult { Miss, Clink, Damage }

    /// <summary>
    /// The rules of a boss fight that do not need the whole Game, so tests can run them: who damages the boss,
    /// when a stage counts as cleared, and what happens to the bubbles when the boss falls.
    /// </summary>
    public static class BossFight
    {
        /// <summary>Score for beating the boss of a world.</summary>
        public const int ScorePerWorld = 2000;

        // ------------------------------------------------------------------ the one damage call

        /// <summary>
        /// The one way anything hurts a boss. The first part (in the order they were added) that area touches decides:
        /// an open core, or an open part with its own health, takes the damage; armour or a closed core clinks (a spark
        /// and a short high sound) and the shot or harpoon is used up; no part touched is a Miss. A boss that is
        /// staggering, dying or dead takes nothing. damage replaces Boss.Hurt as what an open core does (tests use it).
        /// </summary>
        public static HitResult Hit(Boss boss, Rect area, int amount, Vector2 at, Action<int, Vector2> damage = null)
        {
            if (boss == null || boss.Hp <= 0) return HitResult.Miss;
            var part = boss.FirstPartTouching(area);
            return part == null ? HitResult.Miss : HitPart(boss, part, amount, at, damage);
        }

        /// <summary>Hit when the weapon already knows which part it touched.</summary>
        public static HitResult HitPart(Boss boss, BossPart part, int amount, Vector2 at, Action<int, Vector2> damage = null)
        {
            if (boss == null || part == null || boss.Hp <= 0) return HitResult.Miss;
            if (!boss.CanBeHit || !part.Vulnerable)
            {
                boss.Clink(at);
                return HitResult.Clink;
            }
            if (amount > 0)
            {
                if (part.HasHealth) boss.HurtPart(part, amount, at);
                else if (damage != null) damage(amount, at);
                else boss.Hurt(amount, at);
            }
            return HitResult.Damage;
        }

        /// <summary>What Hit would do for this area, without doing it. Weapons that add up fractions of damage ask first.</summary>
        public static HitResult Probe(Boss boss, Rect area)
        {
            if (boss == null || boss.Hp <= 0) return HitResult.Miss;
            var part = boss.FirstPartTouching(area);
            return part == null ? HitResult.Miss : Probe(boss, part);
        }

        public static HitResult Probe(Boss boss, BossPart part) =>
            boss == null || part == null || boss.Hp <= 0 ? HitResult.Miss
            : !boss.CanBeHit || !part.Vulnerable ? HitResult.Clink : HitResult.Damage;

        /// <summary>
        /// Every active harpoon whose cable or tip touches a part of the boss is hidden. An open core takes the harpoon's
        /// damage (once per volley for a harpoon head's cables; damage is called, or Boss.Hurt when it is null);
        /// armour or a closed core clinks and does nothing. Returns the number of harpoons that touched the boss.
        /// </summary>
        public static int HarpoonHits(Boss boss, IList<Harpoon> harpoons, Action<int, Vector2> damage = null)
        {
            if (boss == null || boss.Hp <= 0 || boss.Hidden) return 0; // a boss in a chimney is not there to hit
            int hits = 0;
            foreach (var h in harpoons)
            {
                if (!h.Active) continue;
                BossPart part = null;
                Rect rect = default;
                foreach (var p in boss.Parts)
                {
                    rect = p.World(boss);
                    if (h.Hits(rect)) { part = p; break; }
                }
                if (part == null) continue;
                var cable = h.CableRect;
                var at = new Vector2(Mathf.Clamp(cable.center.x, rect.xMin, rect.xMax), Mathf.Clamp(h.TipRect.center.y, rect.yMin, rect.yMax));
                h.Hide();
                hits++;
                if (Probe(boss, part) != HitResult.Damage)
                {
                    HitPart(boss, part, 0, at); // clinks
                    continue;
                }
                int amount = 1;
                if (h.Volley != null)
                {
                    // A head's volley hurts the boss once, however many of its cables touch.
                    if (h.Volley.BossHit) { hits--; continue; }
                    h.Volley.BossHit = true;
                    amount = h.Volley.BossDamage;
                }
                if (amount > 0) HitPart(boss, part, amount, at, damage);
            }
            return hits;
        }

        /// <summary>A boss stage is cleared when the boss has no health left; any other stage when no bubbles are left.</summary>
        public static bool StageCleared(Boss boss, int bubblesAlive) =>
            boss != null ? boss.Hp <= 0 : bubblesAlive == 0;

        /// <summary>
        /// The boss fell: every bubble still alive pops by itself through popWithoutChildren (its normal pop effect
        /// and score, but no children). Returns how many were popped; the list is empty afterwards.
        /// </summary>
        public static int PopRemaining(List<Bubble> bubbles, Action<Bubble> popWithoutChildren)
        {
            var snapshot = new List<Bubble>(bubbles);
            foreach (var b in snapshot)
            {
                if (b == null) continue;
                popWithoutChildren(b);
                bubbles.Remove(b);
            }
            bubbles.Clear();
            return snapshot.Count;
        }

        // ------------------------------------------------------------------ phases and the bar

        /// <summary>What a phase break gives the arena.</summary>
        public struct PhaseBreakResult
        {
            public bool dropCrate, dropHeart;
        }

        /// <summary>
        /// A phase ended: every boss bullet is cleared (destroy removes each one's object), a weapon crate always drops,
        /// and on the calm road a heart drops when Wick is below his maximum. Never a heart on the hard road.
        /// </summary>
        public static PhaseBreakResult PhaseBreak(List<Bullet> bullets, Action<Bullet> destroy, bool hard, int hearts, int maxHearts)
        {
            ClearBullets(bullets, destroy);
            return new PhaseBreakResult { dropCrate = true, dropHeart = !hard && hearts < maxHearts };
        }

        /// <summary>Removes every boss bullet; destroy removes the object of each.</summary>
        public static void ClearBullets(List<Bullet> bullets, Action<Bullet> destroy)
        {
            foreach (var b in bullets) if (b != null) destroy(b);
            bullets.Clear();
        }

        /// <summary>One segment of the boss's health bar: where it starts, how wide it is, and how full it is (0 to 1).</summary>
        public struct BarSegment
        {
            public float x, width, fill;
        }

        /// <summary>
        /// The health bar's segments across totalWidth: one per phase, equally wide, gap apart, the first phase at the
        /// right end so the whole bar empties from right to left. Segments already spent are empty, the current one
        /// drains (from its right edge) and later ones are full.
        /// </summary>
        public static List<BarSegment> BarSegments(Boss boss, float totalWidth, float gap)
        {
            var list = new List<BarSegment>();
            int n = boss.Phases.Count;
            float width = (totalWidth - gap * (n - 1)) / n;
            for (int i = 0; i < n; i++)
            {
                float fill = i < boss.PhaseIndex ? 0f : i == boss.PhaseIndex ? boss.Phase.Fill(boss) : 1f;
                if (boss.Hp <= 0) fill = 0f;
                list.Add(new BarSegment { x = (n - 1 - i) * (width + gap), width = width, fill = fill });
            }
            return list;
        }
    }
}
