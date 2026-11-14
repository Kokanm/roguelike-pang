using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// The numbers behind one generated stage, for balancing: layout, calm or hard, budget and how it was spent, the
    /// time limit and the hazards. The debug panel's BALANCE page shows it for the current stage, and LOG RUN prints one
    /// tab-separated line per stage of the seed so it can be pasted into a spreadsheet.
    /// </summary>
    public class StageReport
    {
        public int stage;
        public string layout;
        /// <summary>The recipe that shaped the stage, or null when it had none.</summary>
        public string recipe;
        /// <summary>The placement pattern, or null when the stage had no recipe.</summary>
        public string pattern;
        /// <summary>The guest kind, or null when the stage has none.</summary>
        public string guest;
        public bool hard, boss;
        public float budget, bubbleSpend, rollerSpend;
        /// <summary>Bubbles at the start, [kind, tier]. The free Lucky bubble is counted but costs nothing.</summary>
        public int[,] bubbles;
        public int rollers, steelRollers, ceramicRollers, ceramicBubbles;
        public float time;
        /// <summary>Waves that drop in after the start, and the bubbles in them (also counted in bubbles and bubbleSpend).</summary>
        public int waves, waveBubbles;
        public int spikeStrips, gaps, breakables;

        public int World => Run.WorldOf(stage);
        public int StageInWorld => Run.StageInWorldOf(stage);

        public static StageReport From(int stage, StagePlan plan, bool hard)
        {
            var t = Tuning.I;
            int kinds = System.Enum.GetValues(typeof(BubbleKind)).Length;
            var r = new StageReport
            {
                stage = stage,
                layout = plan.layoutName ?? "?",
                recipe = plan.recipeName,
                pattern = plan.patternName,
                guest = plan.guestName,
                hard = hard,
                boss = Run.IsBossStageNumber(stage),
                bubbles = new int[kinds, t.tiers.Length],
                rollerSpend = plan.rollerSpend,
            };
            if (plan.rollers != null)
                foreach (var ro in plan.rollers)
                {
                    if (ro.steel) r.steelRollers++;
                    else
                    {
                        r.rollers++;
                        if (ro.ceramic) r.ceramicRollers++;
                    }
                }
            r.budget = r.boss ? 0f : plan.budget;
            r.time = r.boss ? StageGenerator.BossTime(hard) : StageGenerator.StageTime(stage, hard) * plan.timeScale;
            if (plan.spawns != null)
            {
                foreach (var s in plan.spawns)
                {
                    int tier = Mathf.Clamp(s.tier, 0, t.tiers.Length - 1);
                    r.bubbles[(int)s.kind, tier]++;
                    if (s.ceramic) r.ceramicBubbles++;
                    if (s.kind != BubbleKind.Lucky)
                        r.bubbleSpend += StageGenerator.BubbleCost(s.kind, tier) * (s.ceramic ? t.ceramicCostScale : 1f);
                }
            }
            if (plan.waves != null)
                foreach (var wave in plan.waves)
                {
                    if (wave.Count == 0) continue;
                    r.waves++;
                    foreach (var s in wave)
                    {
                        int tier = Mathf.Clamp(s.tier, 0, t.tiers.Length - 1);
                        r.bubbles[(int)s.kind, tier]++;
                        r.waveBubbles++;
                        if (s.ceramic) r.ceramicBubbles++;
                        r.bubbleSpend += StageGenerator.BubbleCost(s.kind, tier) * (s.ceramic ? t.ceramicCostScale : 1f);
                    }
                }
            var map = plan.map;
            if (map != null)
            {
                r.spikeStrips = map.spikes.Count;
                bool inGap = false;
                for (int c = 0; c < StageMap.Cols; c++)
                {
                    bool g = map.IsGap(c);
                    if (g && !inGap) r.gaps++;
                    inGap = g;
                }
                for (int c = 0; c < StageMap.Cols; c++)
                    for (int row = 0; row < StageMap.Rows; row++)
                        if (map.Get(c, row).kind == TileKind.Breakable) r.breakables++;
            }
            return r;
        }

        /// <summary>"Gloop XL x1, Lead M x2", or "none".</summary>
        public string BubbleSummary()
        {
            var t = Tuning.I;
            var parts = new List<string>();
            for (int k = 0; k < bubbles.GetLength(0); k++)
                for (int tier = 0; tier < bubbles.GetLength(1); tier++)
                    if (bubbles[k, tier] > 0) parts.Add($"{(BubbleKind)k} {t.tiers[tier].name} x{bubbles[k, tier]}");
            return parts.Count == 0 ? "none" : string.Join(", ", parts);
        }

        /// <summary>Column names for Line, tab-separated.</summary>
        public static string Header()
        {
            var t = Tuning.I;
            var sb = new StringBuilder("stage\tworld\tlayout\trecipe\tpattern\tguest\troad\tbudget\tbubble spend\troller spend\tleft");
            foreach (BubbleKind kind in System.Enum.GetValues(typeof(BubbleKind)))
                for (int tier = 0; tier < t.tiers.Length; tier++)
                    sb.Append('\t').Append(kind).Append(' ').Append(t.tiers[tier].name);
            sb.Append("\tceramic bubbles\trollers\tceramic rollers\tsteel rollers\ttime\tspike strips\tgaps\tbreakables\twaves\twave bubbles");
            return sb.ToString();
        }

        /// <summary>One tab-separated line, in the order of Header.</summary>
        public string Line()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append(boss ? $"{World}-BOSS" : $"{World}-{StageInWorld}").Append('\t');
            sb.Append(Run.WorldNameOf(World)).Append('\t');
            sb.Append(layout).Append('\t');
            sb.Append(recipe ?? "").Append('\t');
            sb.Append(pattern ?? "").Append('\t');
            sb.Append(guest ?? "").Append('\t');
            sb.Append(hard ? "hard" : "calm").Append('\t');
            sb.Append(budget.ToString("0.#", inv)).Append('\t');
            sb.Append(bubbleSpend.ToString("0.#", inv)).Append('\t');
            sb.Append(rollerSpend.ToString("0.#", inv)).Append('\t');
            sb.Append(Mathf.Max(0f, budget - bubbleSpend - rollerSpend).ToString("0.#", inv));
            for (int k = 0; k < bubbles.GetLength(0); k++)
                for (int tier = 0; tier < bubbles.GetLength(1); tier++)
                    sb.Append('\t').Append(bubbles[k, tier]);
            sb.Append('\t').Append(ceramicBubbles);
            sb.Append('\t').Append(rollers);
            sb.Append('\t').Append(ceramicRollers);
            sb.Append('\t').Append(steelRollers);
            sb.Append('\t').Append(time.ToString("0.#", inv));
            sb.Append('\t').Append(spikeStrips);
            sb.Append('\t').Append(gaps);
            sb.Append('\t').Append(breakables);
            sb.Append('\t').Append(waves);
            sb.Append('\t').Append(waveBubbles);
            return sb.ToString();
        }

        /// <summary>Every stage of a run's seed, with that run's calm and hard worlds, as a header and one line per stage.</summary>
        public static string LogRun(Run run)
        {
            var sb = new StringBuilder();
            sb.Append("seed ").Append(run.Seed).Append('\n');
            sb.Append(Header());
            for (int s = 1; s <= Tuning.I.RunLength; s++)
            {
                bool hard = run.IsHardWorld(Run.WorldOf(s));
                var plan = StageGenerator.GenerateFor(run, s);
                sb.Append('\n').Append(From(s, plan, hard).Line());
            }
            return sb.ToString();
        }
    }
}
