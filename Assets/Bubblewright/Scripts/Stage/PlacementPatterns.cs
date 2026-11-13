using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Where a stage's bubbles start. Scattered is the generator's own random placement; the others are drawn by recipe.
    /// Append new ids at the end.
    /// </summary>
    public enum PlacementPattern
    {
        Scattered,
        MirroredPair,
        TopRow,
        OnePerFloor,
    }

    /// <summary>
    /// The placement patterns. Each one takes the picks (biggest first) and returns the spawns it could place. Every
    /// pattern keeps the usual rules: a bubble starts clear of tiles and at least 5 units sideways from Wick's start
    /// (x = 0). A bubble a pattern cannot place falls back to a scattered spot, and is dropped only if even that fails.
    /// All random numbers come from the stage's own generator, so a continued run rebuilds the same stage.
    /// </summary>
    public static class PlacementPatterns
    {
        /// <summary>Bubble margin added to its radius when testing against tiles, as the generator does.</summary>
        const float Margin = 0.25f;
        /// <summary>Wick starts at x = 0; bubbles start at least this far sideways from him.</summary>
        const float WickGap = 5f;
        const int Draws = 8;

        // Height bands of OnePerFloor, as { low, high } of the bubble centre.
        static readonly Vector2 LowBand = new Vector2(1.5f, 3.5f);
        static readonly Vector2 MidBand = new Vector2(6f, 8.5f);
        static readonly Vector2 HighBand = new Vector2(11f, 12.5f);

        public static string NameOf(PlacementPattern pattern) => pattern switch
        {
            PlacementPattern.MirroredPair => "Mirrored pair",
            PlacementPattern.TopRow => "Top row",
            PlacementPattern.OnePerFloor => "One per floor",
            _ => "Scattered",
        };

        /// <summary>Places the picks with a pattern. Scattered returns null: the generator places those itself.</summary>
        public static List<SpawnSpec> Place(PlacementPattern pattern, StageMap map,
            IList<(BubbleKind kind, int tier, bool ceramic)> picks, float bubbleMinY, System.Random rng)
        {
            switch (pattern)
            {
                case PlacementPattern.MirroredPair: return MirroredPair(map, picks, bubbleMinY, rng);
                case PlacementPattern.TopRow: return TopRow(map, picks, bubbleMinY, rng);
                case PlacementPattern.OnePerFloor: return OnePerFloor(map, picks, bubbleMinY, rng);
                default: return null;
            }
        }

        /// <summary>Picks two at a time, one on each side at the same height, moving apart. A last single pick is scattered.</summary>
        public static List<SpawnSpec> MirroredPair(StageMap map, IList<(BubbleKind kind, int tier, bool ceramic)> picks,
            float bubbleMinY, System.Random rng)
        {
            var t = Tuning.I;
            var placed = new List<SpawnSpec>();
            for (int i = 0; i < picks.Count; i += 2)
            {
                if (i + 1 >= picks.Count)
                {
                    Fallback(map, picks[i], bubbleMinY, rng, placed);
                    break;
                }
                var a = picks[i];
                var b = picks[i + 1];
                float clearanceA = Clearance(a.tier);
                float clearanceB = Clearance(b.tier);
                float widest = Mathf.Max(clearanceA, clearanceB);
                bool done = false;
                for (int draw = 0; draw < Draws && !done; draw++)
                {
                    float x = WickGap + (float)rng.NextDouble() * (t.HalfWidth - widest - WickGap);
                    float yLow = bubbleMinY + widest;
                    float yHigh = Mathf.Min(12.5f, t.arenaHeight - widest);
                    float y = Mathf.Lerp(yLow, Mathf.Max(yLow, yHigh), (float)rng.NextDouble());
                    var left = new Vector2(-x, y);
                    var right = new Vector2(x, y);
                    if (yHigh < yLow || t.HalfWidth - widest < WickGap) continue;
                    if (!IsClear(map, left, clearanceA) || !IsClear(map, right, clearanceB)) continue;
                    // The bigger bubble goes to a random side so a pair is not always big-left.
                    bool bigLeft = rng.NextDouble() < 0.5;
                    placed.Add(Make(a, bigLeft ? left : right, bigLeft ? -1 : 1));
                    placed.Add(Make(b, bigLeft ? right : left, bigLeft ? 1 : -1));
                    done = true;
                }
                if (!done)
                {
                    Fallback(map, a, bubbleMinY, rng, placed);
                    Fallback(map, b, bubbleMinY, rng, placed);
                }
            }
            return placed;
        }

        /// <summary>One row along the top, spread across the width and moving in alternating directions.</summary>
        public static List<SpawnSpec> TopRow(StageMap map, IList<(BubbleKind kind, int tier, bool ceramic)> picks,
            float bubbleMinY, System.Random rng)
        {
            var t = Tuning.I;
            var placed = new List<SpawnSpec>();
            int n = picks.Count;
            if (n == 0) return placed;
            float y = Mathf.Min(12.5f, t.arenaHeight - Clearance(picks[0].tier));
            float w = t.HalfWidth - 2f;
            int direction = rng.NextDouble() < 0.5 ? -1 : 1;
            for (int i = 0; i < n; i++)
            {
                float x = -w + (i + 0.5f) * (2f * w / n);
                if (Mathf.Abs(x) < WickGap) x = x < 0f ? -WickGap : WickGap;
                var position = new Vector2(x, y);
                float clearance = Clearance(picks[i].tier);
                if (IsClear(map, position, clearance) && !Overlaps(placed, position, picks[i].tier))
                    placed.Add(Make(picks[i], position, direction));
                else
                    Fallback(map, picks[i], bubbleMinY, rng, placed);
                direction = -direction;
            }
            return placed;
        }

        /// <summary>Picks dealt to a high, a middle and a low height band in turn, so every level of the stage has a bubble.</summary>
        public static List<SpawnSpec> OnePerFloor(StageMap map, IList<(BubbleKind kind, int tier, bool ceramic)> picks,
            float bubbleMinY, System.Random rng)
        {
            var t = Tuning.I;
            var bands = new[] { HighBand, MidBand, LowBand };
            var placed = new List<SpawnSpec>();
            int turn = 0;
            foreach (var pick in picks)
            {
                int band = turn % 3;
                bool low = band == 2;
                // An XL bubble is wider than the low band can hold: it takes the next band in the deal.
                if (low && t.tiers[pick.tier].diameter > 2.5f)
                {
                    band = (band + 1) % 3;
                    low = false;
                    turn += 2;
                }
                else turn++;

                float clearance = Clearance(pick.tier);
                float minAbs = low ? 7f : WickGap;
                float yLow = Mathf.Max(bands[band].x, clearance + 0.02f);
                float yHigh = Mathf.Min(bands[band].y, t.arenaHeight - clearance);
                bool done = false;
                if (yLow <= yHigh && t.HalfWidth - clearance > minAbs)
                {
                    for (int draw = 0; draw < Draws && !done; draw++)
                    {
                        float y = Mathf.Lerp(yLow, yHigh, (float)rng.NextDouble());
                        float x = minAbs + (float)rng.NextDouble() * (t.HalfWidth - clearance - minAbs);
                        if (rng.NextDouble() < 0.5) x = -x;
                        var position = new Vector2(x, y);
                        if (!IsClear(map, position, clearance) || Overlaps(placed, position, pick.tier)) continue;
                        placed.Add(Make(pick, position, rng.NextDouble() < 0.5 ? -1 : 1));
                        done = true;
                    }
                }
                if (!done) Fallback(map, pick, bubbleMinY, rng, placed);
            }
            return placed;
        }

        static float Clearance(int tier) => Tuning.I.tiers[tier].diameter * 0.5f + Margin;

        static SpawnSpec Make((BubbleKind kind, int tier, bool ceramic) pick, Vector2 position, int direction) =>
            new SpawnSpec { kind = pick.kind, tier = pick.tier, ceramic = pick.ceramic, position = position, direction = direction };

        static bool IsClear(StageMap map, Vector2 candidate, float clearance)
        {
            if (Mathf.Abs(candidate.x) < WickGap) return false;
            Vector2 resolved = candidate;
            Contact contact = map.ResolveCircle(ref resolved, clearance);
            return StageGenerator.IsClear(candidate, resolved, contact);
        }

        /// <summary>True when a bubble of this tier at this spot would touch one already placed.</summary>
        static bool Overlaps(List<SpawnSpec> placed, Vector2 position, int tier)
        {
            var t = Tuning.I;
            float radius = t.tiers[tier].diameter * 0.5f;
            foreach (var other in placed)
            {
                float gap = radius + t.tiers[other.tier].diameter * 0.5f;
                if ((other.position - position).sqrMagnitude < gap * gap) return true;
            }
            return false;
        }

        /// <summary>A scattered spot that does not touch what is placed. The bubble is dropped if six tries all fail.</summary>
        static void Fallback(StageMap map, (BubbleKind kind, int tier, bool ceramic) pick, float bubbleMinY,
            System.Random rng, List<SpawnSpec> placed)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                if (!StageGenerator.TryPosition(map, Clearance(pick.tier), bubbleMinY, 0f, rng, out Vector2 position)) return;
                if (Overlaps(placed, position, pick.tier)) continue;
                placed.Add(Make(pick, position, rng.NextDouble() < 0.5 ? -1 : 1));
                return;
            }
        }
    }
}
