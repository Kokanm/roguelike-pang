using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// The rules of a boss fight that do not need the whole Game, so tests can run them: who damages the boss,
    /// when a stage counts as cleared, and what happens to the bubbles when the boss falls.
    /// </summary>
    public static class BossFight
    {
        /// <summary>Score for beating the boss of a world.</summary>
        public const int ScorePerWorld = 2000;

        /// <summary>
        /// Every active harpoon whose cable or tip touches the boss's body is hidden and calls damage(1, point) once.
        /// Returns the number of harpoons that hit. Later weapons call the same damage method.
        /// </summary>
        public static int HarpoonHits(Boss boss, IList<Harpoon> harpoons, Action<int, Vector2> damage)
        {
            if (boss == null || boss.Hp <= 0) return 0;
            int hits = 0;
            var body = boss.Body;
            foreach (var h in harpoons)
            {
                if (!h.Active || !h.Hits(body)) continue;
                var cable = h.CableRect;
                var at = new Vector2(Mathf.Clamp(cable.center.x, body.xMin, body.xMax), Mathf.Clamp(h.TipRect.center.y, body.yMin, body.yMax));
                h.Hide();
                damage(1, at);
                hits++;
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
    }
}
