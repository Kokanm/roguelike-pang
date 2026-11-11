namespace Bubblewright
{
    public partial class Game
    {
        public static StageMap Map { get; private set; }

        int testLayoutIndex = -1;
        bool testLayout => testLayoutIndex >= 0;
        LayoutDef CurrentTestLayout => testLayout ? StageLayouts.All[testLayoutIndex] : null;

        void ToggleTestLayout()
        {
            testLayoutIndex = (testLayoutIndex + 1) % StageLayouts.All.Length;
            run.Hearts = wick.Hearts;
            StartStage();
        }

        void DrawStageHitboxes()
        {
            for (int r = 0; r < StageMap.Rows; r++)
                for (int c = 0; c < StageMap.Cols; c++)
                    if (Map.IsSolid(c, r))
                        DrawWorldRect(StageMap.CellRect(c, r), Palette.Mint);
        }
    }
}
