namespace Bubblewright
{
    public partial class Game
    {
        public static StageMap Map { get; private set; }

        static Theme currentTheme;
        /// <summary>The look of the current world: sky, tiles and ground strip colours. Rooftops until a stage starts.</summary>
        public static Theme CurrentTheme
        {
            get => currentTheme ?? Themes.ForWorld(1);
            set => currentTheme = value;
        }

        SpriteRenderer background;

        /// <summary>Shows a theme: background sprite and camera clear colour now, tiles on the next StageView.Tick.</summary>
        void ApplyTheme(Theme theme)
        {
            CurrentTheme = theme;
            if (background != null) background.sprite = PlaceholderArt.I.BackgroundFor(theme);
            if (cam != null) cam.backgroundColor = theme.strip;
        }

        // Debug: -1 = the world's own theme, then 0..5 = that theme's look on the current stage.
        int debugTheme = -1;

        string DebugThemeLabel() => debugTheme < 0 ? "THEME" : $"THEME: {Themes.All[debugTheme].name.ToUpperInvariant()}";

        /// <summary>Debug panel: cycles the look of the current stage through the six themes without changing the world.</summary>
        void DebugCycleTheme()
        {
            debugTheme = (debugTheme + 1) % Themes.All.Length;
            ApplyTheme(Themes.All[debugTheme]);
        }

        int testLayoutIndex = -1;
        bool testLayout => testLayoutIndex >= 0;
        LayoutDef CurrentTestLayout => testLayout ? StageLayouts.All[testLayoutIndex] : null;

        void ToggleTestLayout()
        {
            testLayoutIndex = (testLayoutIndex + 1) % StageLayouts.All.Length;
            run.Hearts = wick.Hearts;
            StartStage();
        }

        // Debug: -1 = as generated, then 0 = all normal, 1 = all ice, 2 = all sand.
        int debugSurface = -1;
        static readonly Surface[] DebugSurfaces = { Surface.Normal, Surface.Ice, Surface.Sand };

        string DebugSurfaceLabel() => debugSurface < 0 ? "SURFACE" : $"SURFACE: {DebugSurfaces[debugSurface].ToString().ToUpperInvariant()}";

        /// <summary>Debug panel: the whole current stage becomes normal, then all ice, then all sand, to tune the feel in any world.</summary>
        void DebugCycleSurface()
        {
            if (Map == null) return;
            debugSurface = (debugSurface + 1) % DebugSurfaces.Length;
            WorldRules.PaintAll(Map, DebugSurfaces[debugSurface]);
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
