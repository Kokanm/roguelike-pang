using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// The debug panel's BALANCE page: the numbers of the current stage, LOG RUN (every stage of the seed to the Console
    /// as tab-separated text), SUMMARY (every stage slot averaged over 100 seeds), WIN STAGE and NEXT STAGE to move through this world's stages. The numbers to change are in
    /// the Tuning asset.
    /// </summary>
    public partial class Game
    {
        StageReport stageReport;
        bool balancePage;
        string balanceNote;

        void AddBalancePage(int k)
        {
            AddDebugButton("BACK", k, true, () => { balancePage = false; debugScroll = Vector2.zero; }, minUnits: 60);
            AddDebugButton("WIN STAGE", k, false, DebugClearStage, minUnits: 80);
            AddDebugButton("NEXT STAGE", k, false, DebugNextStage, minUnits: 90);
            AddDebugButton("NEXT WAVE", k, false, DebugNextWave, minUnits: 90);
            AddDebugButton("LOG RUN", k, false, DebugLogRun, minUnits: 70);
            AddDebugButton("SUMMARY", k, false, DebugLogSummary, minUnits: 70);

            var r = stageReport;
            if (r == null)
            {
                AddDebugLine("No stage yet.");
                return;
            }
            string where = r.boss ? $"WORLD {r.World} BOSS" : $"WORLD {r.World}-{r.StageInWorld}";
            string id = string.IsNullOrEmpty(r.id) ? r.layout : r.id;
            AddDebugLine($"{where} {Run.WorldNameOf(r.World).ToUpperInvariant()}  ·  {(r.hard ? "HARD" : "CALM")}  ·  {id}  ·  D{r.difficulty}", true);
            if (!string.IsNullOrEmpty(r.concept)) AddDebugLine(r.concept);
            if (r.boss)
                AddDebugLine("Boss stage: no stage enemies.");
            else
            {
                AddDebugLine($"POPS NEEDED {r.pops}");
                string summary = "BUBBLES  " + r.BubbleSummary();
                while (summary.Length > 60)
                {
                    int cut = summary.LastIndexOf(", ", 60, System.StringComparison.Ordinal);
                    if (cut < 0) break;
                    AddDebugLine(summary.Substring(0, cut + 1));
                    summary = "         " + summary.Substring(cut + 2);
                }
                AddDebugLine(summary);
                AddDebugLine($"BUBBLES CERAMIC {r.ceramicBubbles}  ·  ROLLERS {r.rollers} big ({r.ceramicRollers} ceramic)  ·  STEEL {r.steelRollers}");
            }
            if (!r.boss) AddDebugLine($"GUEST {(string.IsNullOrEmpty(r.guest) ? "none" : r.guest.ToUpperInvariant())}");
            if (r.waves > 0) AddDebugLine($"WAVES {r.waves} ({r.waveBubbles} bubbles)");
            AddDebugLine($"TIME LIMIT {r.time:0} s  ·  SPIKE STRIPS {r.spikeStrips}  ·  GAPS {r.gaps}  ·  BREAKABLE TILES {r.breakables}");
            AddDebugLine($"seed {run.Seed}");
            AddDebugLine(balanceNote ?? $"LOG RUN prints all {T.RunLength} stages of this seed to the Console, tab-separated.");
        }

        /// <summary>Shows the next authored stage of this world, with no mirror and no Lucky. Does not change the saved pick.</summary>
        void DebugNextStage()
        {
            if (run == null || StageSpec.All.Count == 0) return;
            int start = testStageIndex;
            for (int step = 1; step <= StageSpec.All.Count; step++)
            {
                int index = ((start + step) % StageSpec.All.Count + StageSpec.All.Count) % StageSpec.All.Count;
                if (StageSpec.All[index].World != run.World) continue;
                testStageIndex = index;
                run.Hearts = Mathf.Max(1, wick.Hearts);
                Resume();
                StartStage();
                return;
            }
        }

        /// <summary>Calls the next wave at once and resumes the game. Does nothing on a stage without waves.</summary>
        void DebugNextWave()
        {
            if (waves == null) return;
            waves.CallNow();
            Resume();
        }

        void DebugLogRun()
        {
            if (run == null) return;
            Debug.Log(StageReport.LogRun(run));
            balanceNote = $"Logged {T.RunLength} stages of seed {run.Seed} to the Console. Copy the message into a spreadsheet.";
        }

        /// <summary>Every normal stage slot averaged over 100 calm seeds, to the Console (same as the editor menu item).</summary>
        void DebugLogSummary()
        {
            Debug.Log(StageReport.Summary(100));
            balanceNote = "Logged the 100-seed balance summary to the Console. Copy the message into a spreadsheet.";
        }
    }
}
