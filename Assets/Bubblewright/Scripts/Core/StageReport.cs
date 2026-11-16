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
        public string id;
        public int difficulty;
        public string concept;
        public int pops;
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
                id = plan.stageId ?? "",
                difficulty = plan.difficulty,
                concept = (plan.concept ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' '),
                pops = PopsNeeded(plan),
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
            r.time = r.boss ? StagePicker.BossTime(hard) : StagePicker.StageTime(stage, hard) * plan.timeScale;
            if (plan.spawns != null)
            {
                foreach (var s in plan.spawns)
                {
                    int tier = Mathf.Clamp(s.tier, 0, t.tiers.Length - 1);
                    r.bubbles[(int)s.kind, tier]++;
                    if (s.ceramic) r.ceramicBubbles++;
                    if (s.kind != BubbleKind.Lucky)
                        r.bubbleSpend += StagePicker.BubbleCost(s.kind, tier) * (s.ceramic ? t.ceramicCostScale : 1f);
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
                        r.bubbleSpend += StagePicker.BubbleCost(s.kind, tier) * (s.ceramic ? t.ceramicCostScale : 1f);
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
                    for (int row = 0; row < map.Rows; row++)
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
            var sb = new StringBuilder("stage\tworld\tid\tdifficulty\tconcept\tguest\troad\tbudget\tbubble spend\troller spend\tleft");
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
            sb.Append(id ?? "").Append('\t');
            sb.Append(difficulty).Append('\t');
            sb.Append(concept ?? "").Append('\t');
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

        /// <summary>
        /// Harpoon hits that pop one bubble of this kind and tier and all its children, if nothing is wasted: an S takes
        /// one, a bigger one takes one plus its children's. A ceramic bubble takes Tuning.ceramicHits - 1 more (its
        /// children are not ceramic).
        /// </summary>
        public static int PopsNeeded(BubbleKind kind, int tier, bool ceramic = false)
        {
            var t = Tuning.I;
            int children = Mathf.Max(1, t.Kind(kind).children);
            int pops = 1;
            for (int next = t.tiers.Length - 1; next > tier; next--) pops = 1 + children * pops;
            return pops + (ceramic ? Mathf.Max(0, t.ceramicHits - 1) : 0);
        }

        /// <summary>Hits that clear a rolling ball: a big one is itself and two small ones, a ceramic one takes more. Steel takes none.</summary>
        public static int PopsNeeded(RollerSpec roller)
        {
            if (roller.steel) return 0;
            int pops = roller.tier == 0 ? 3 : 1;
            return pops + (roller.ceramic ? Mathf.Max(0, Tuning.I.ceramicHits - 1) : 0);
        }

        /// <summary>Hits that clear a whole plan: start bubbles, waves and rolling balls. Lucky, steel balls and steel Pogos do not count.</summary>
        public static int PopsNeeded(StagePlan plan)
        {
            int pops = 0;
            if (plan.spawns != null)
                foreach (var s in plan.spawns)
                    if (s.kind != BubbleKind.Lucky && !s.steel) pops += PopsNeeded(s.kind, s.tier, s.ceramic);
            if (plan.waves != null)
                foreach (var wave in plan.waves)
                    foreach (var s in wave)
                        if (s.kind != BubbleKind.Lucky && !s.steel) pops += PopsNeeded(s.kind, s.tier, s.ceramic);
            if (plan.rollers != null)
                foreach (var r in plan.rollers) pops += PopsNeeded(r);
            return pops;
        }

        /// <summary>
        /// Every normal stage slot averaged over seeds 1 to seeds on a calm road: a header and 30 lines of the
        /// authored id, difficulty, concept, pops needed, time limit and pops per second.
        /// </summary>
        public static string Summary(int seeds)
        {
            var t = Tuning.I;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            seeds = Mathf.Max(1, seeds);
            var slots = new List<int>();
            for (int s = 1; s <= t.RunLength; s++)
                if (!Run.IsBossStageNumber(s)) slots.Add(s);
            int n = slots.Count;
            var difficulty = new double[n];
            var pops = new double[n];
            var time = new double[n];
            var start = new double[n];
            var waved = new double[n];
            var rollers = new double[n];
            var guests = new double[n];
            var id = new string[n];
            var concept = new string[n];

            for (int seed = 1; seed <= seeds; seed++)
            {
                var run = new Run(seed);
                for (int i = 0; i < n; i++)
                {
                    int stage = slots[i];
                    var plan = StagePicker.Build(run, stage);
                    var r = From(stage, plan, false);
                    difficulty[i] += r.difficulty;
                    pops[i] += r.pops;
                    time[i] += r.time;
                    rollers[i] += r.rollers;
                    if (!string.IsNullOrEmpty(r.guest)) guests[i]++;
                    if (string.IsNullOrEmpty(id[i])) id[i] = r.id;
                    if (string.IsNullOrEmpty(concept[i])) concept[i] = r.concept;
                    if (plan.spawns != null)
                        foreach (var s in plan.spawns)
                            if (s.kind != BubbleKind.Lucky) start[i]++;
                    if (plan.waves != null)
                        foreach (var wave in plan.waves)
                            waved[i] += wave.Count;
                }
            }

            var sb = new StringBuilder("stage\tworld\tid\tdifficulty\tconcept\tpops needed\ttime\tpops per s\tstart bubbles\twave bubbles\trolling balls\tguest");
            string F(double v, string format = "0.##") => v.ToString(format, inv);
            for (int i = 0; i < n; i++)
            {
                int stage = slots[i];
                double avgPops = pops[i] / seeds, avgTime = time[i] / seeds;
                sb.Append('\n').Append(Run.WorldOf(stage)).Append('-').Append(Run.StageInWorldOf(stage));
                sb.Append('\t').Append(Run.WorldNameOf(Run.WorldOf(stage)));
                sb.Append('\t').Append(id[i] ?? "");
                sb.Append('\t').Append(F(difficulty[i] / seeds, "0.#"));
                sb.Append('\t').Append(concept[i] ?? "");
                sb.Append('\t').Append(F(avgPops, "0.#"));
                sb.Append('\t').Append(F(avgTime, "0.#"));
                sb.Append('\t').Append(F(avgTime > 0.0 ? avgPops / avgTime : 0.0, "0.###"));
                sb.Append('\t').Append(F(start[i] / seeds));
                sb.Append('\t').Append(F(waved[i] / seeds));
                sb.Append('\t').Append(F(rollers[i] / seeds));
                sb.Append('\t').Append(F(guests[i] / seeds));
            }
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
                var plan = StagePicker.Build(run, s);
                sb.Append('\n').Append(From(s, plan, hard).Line());
            }
            return sb.ToString();
        }
    }
}
