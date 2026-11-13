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

        void DrawBalancePage(int k)
        {
            GUILayout.BeginHorizontal();
            if (DbgButton("BACK", k, 60)) balancePage = false;
            if (DbgButton("WIN STAGE", k, 80)) DebugClearStage();
            if (DbgButton("NEXT LAYOUT", k, 90)) DebugNextLayout();
            if (DbgButton("NEXT RECIPE", k, 100)) DebugNextRecipe();
            if (DbgButton("LOG RUN", k, 70)) DebugLogRun();
            GUILayout.EndHorizontal();

            var r = stageReport;
            if (r == null)
            {
                GUILayout.Label("No stage yet.", dbgLabel);
                return;
            }
            string where = r.boss ? $"WORLD {r.World} BOSS" : $"WORLD {r.World}-{r.StageInWorld}";
            string recipe = string.IsNullOrEmpty(r.recipe) ? "" : $"  ·  RECIPE {r.recipe.ToUpperInvariant()}";
            if (!string.IsNullOrEmpty(r.pattern)) recipe += $"  ·  PATTERN {r.pattern.ToUpperInvariant()}";
            GUILayout.Label($"{where} {Run.WorldNameOf(r.World).ToUpperInvariant()}  ·  {(r.hard ? "HARD" : "CALM")}  ·  LAYOUT {r.layout.ToUpperInvariant()}{recipe}" +
                            (currentReroll > 0 ? $" (re-rolled {currentReroll})" : ""), dbgTitle);
            if (r.boss)
            {
                GUILayout.Label("Boss stage: no generated bubbles and no rollers.", dbgLabel);
            }
            else
            {
                float left = Mathf.Max(0f, r.budget - r.bubbleSpend - r.rollerSpend);
                GUILayout.Label($"BUDGET {r.budget:0.#}  =  bubbles {r.bubbleSpend:0.#}  +  rollers {r.rollerSpend:0.#}  +  unspent {left:0.#}", dbgLabel);
                GUILayout.Label($"BUBBLES  {r.BubbleSummary()}", dbgLabel);
                GUILayout.Label($"BUBBLES CERAMIC {r.ceramicBubbles}  ·  ROLLERS {r.rollers} big ({r.ceramicRollers} ceramic)  ·  STEEL {r.steelRollers}", dbgLabel);
            }
            GUILayout.Label($"TIME LIMIT {r.time:0} s  ·  SPIKE STRIPS {r.spikeStrips}  ·  GAPS {r.gaps}  ·  BREAKABLE TILES {r.breakables}", dbgLabel);
            GUILayout.Label($"Jump budget bonus x{T.jumpBudgetBonus:0.##}  ·  hard budget x{T.hardBudgetScale:0.##}  ·  seed {run.Seed}", dbgLabel);
            GUILayout.Label(balanceNote ?? $"LOG RUN prints all {T.RunLength} stages of this seed to the Console, tab-separated.", dbgLabel);
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

        void DebugLogRun()
        {
            if (run == null) return;
            Debug.Log(StageReport.LogRun(run));
            balanceNote = $"Logged {T.RunLength} stages of seed {run.Seed} to the Console. Copy the message into a spreadsheet.";
        }
    }
}
