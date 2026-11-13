using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// The debug panel's BALANCE page: the numbers of the current stage, LOG RUN (every stage of the seed to the Console
    /// as tab-separated text), WIN STAGE and NEXT LAYOUT to move through stages quickly. The numbers to change are in
    /// the Tuning asset.
    /// </summary>
    public partial class Game
    {
        StageReport stageReport;
        int currentReroll;     // how many times NEXT LAYOUT re-rolled this stage's layout
        int forcedRecipeIndex = -1; // -1: the stage's own recipe. Otherwise an index into Tuning.recipes.
        bool balancePage;
        string balanceNote;

        void AddBalancePage(int k)
        {
            AddDebugButton("BACK", k, true, () => { balancePage = false; debugScroll = Vector2.zero; }, minUnits: 60);
            AddDebugButton("WIN STAGE", k, false, DebugClearStage, minUnits: 80);
            AddDebugButton("NEXT LAYOUT", k, false, DebugNextLayout, minUnits: 90);
            AddDebugButton("NEXT RECIPE", k, false, DebugNextRecipe, minUnits: 100);
            AddDebugButton("NEXT WAVE", k, false, DebugNextWave, minUnits: 90);
            AddDebugButton("LOG RUN", k, false, DebugLogRun, minUnits: 70);

            var r = stageReport;
            if (r == null)
            {
                AddDebugLine("No stage yet.");
                return;
            }
            string where = r.boss ? $"WORLD {r.World} BOSS" : $"WORLD {r.World}-{r.StageInWorld}";
            string recipe = string.IsNullOrEmpty(r.recipe) ? "" : $"  ·  RECIPE {r.recipe.ToUpperInvariant()}";
            if (!string.IsNullOrEmpty(r.pattern)) recipe += $"  ·  PATTERN {r.pattern.ToUpperInvariant()}";
            AddDebugLine($"{where} {Run.WorldNameOf(r.World).ToUpperInvariant()}  ·  {(r.hard ? "HARD" : "CALM")}  ·  LAYOUT {r.layout.ToUpperInvariant()}{recipe}" +
                         (currentReroll > 0 ? $" (re-rolled {currentReroll})" : ""), true);
            if (r.boss)
            {
                AddDebugLine("Boss stage: no generated bubbles and no rollers.");
            }
            else
            {
                float left = Mathf.Max(0f, r.budget - r.bubbleSpend - r.rollerSpend);
                AddDebugLine($"BUDGET {r.budget:0.#}  =  bubbles {r.bubbleSpend:0.#}  +  rollers {r.rollerSpend:0.#}  +  unspent {left:0.#}");
                AddDebugLine($"BUBBLES  {r.BubbleSummary()}");
                AddDebugLine($"BUBBLES CERAMIC {r.ceramicBubbles}  ·  ROLLERS {r.rollers} big ({r.ceramicRollers} ceramic)  ·  STEEL {r.steelRollers}");
            }
            if (r.waves > 0) AddDebugLine($"WAVES {r.waves} ({r.waveBubbles} bubbles)");
            AddDebugLine($"TIME LIMIT {r.time:0} s  ·  SPIKE STRIPS {r.spikeStrips}  ·  GAPS {r.gaps}  ·  BREAKABLE TILES {r.breakables}");
            AddDebugLine($"Jump budget bonus x{T.jumpBudgetBonus:0.##}  ·  hard budget x{T.hardBudgetScale:0.##}  ·  seed {run.Seed}");
            AddDebugLine(balanceNote ?? $"LOG RUN prints all {T.RunLength} stages of this seed to the Console, tab-separated.");
        }

        /// <summary>Same stage, same bubbles drawn from the stage's random numbers, a different layout.</summary>
        void DebugNextLayout()
        {
            if (run == null || run.IsBossStage) return;
            run.Hearts = Mathf.Max(1, wick.Hearts);
            Resume();
            StartStage(currentReroll + 1);
        }

        /// <summary>Same stage, forced to the next row of Tuning.recipes. Does nothing on a boss stage.</summary>
        void DebugNextRecipe()
        {
            if (run == null || run.IsBossStage || T.recipes == null || T.recipes.Length == 0) return;
            forcedRecipeIndex = (forcedRecipeIndex + 1) % T.recipes.Length;
            run.Hearts = Mathf.Max(1, wick.Hearts);
            Resume();
            StartStage(currentReroll);
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
    }
}
