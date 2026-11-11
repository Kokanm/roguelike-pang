namespace Bubblewright
{
    public partial class Game
    {
        public static StageMap Map { get; private set; }

        static readonly string[] TowerLayout =
        {
            "................................",
            "................................",
            "................................",
            "................................",
            "..........#########T##..........",
            "...................H............",
            "...................H............",
            "...................H............",
            "...................H............",
            "....##T##################T##....",
            "......H..................H......",
            "......H..................H......",
            "......H..................H......",
            "......H..................H......",
        };

        bool testLayout;

        StageMap BuildStageMap() => testLayout ? StageLayout.Parse(TowerLayout) : StageMap.Flat();

        void ToggleTestLayout()
        {
            testLayout = !testLayout;
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
