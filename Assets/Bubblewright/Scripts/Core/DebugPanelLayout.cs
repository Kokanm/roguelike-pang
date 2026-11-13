using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    public enum DebugControlKind { Button, Inline, Line }

    /// <summary>One control in the debug panel. Widths are pixels. A line takes the whole row and wraps.</summary>
    public struct DebugControl
    {
        public DebugControlKind kind;
        public string text;
        public float minWidth;
        public bool rowBreak;
        public bool title;

        public static DebugControl Button(string text, float minWidth, bool rowBreak = false) =>
            new DebugControl { kind = DebugControlKind.Button, text = text ?? "", minWidth = minWidth, rowBreak = rowBreak };

        public static DebugControl Inline(string text, float minWidth, bool rowBreak = false) =>
            new DebugControl { kind = DebugControlKind.Inline, text = text ?? "", minWidth = minWidth, rowBreak = rowBreak };

        public static DebugControl Line(string text, bool title = false) =>
            new DebugControl { kind = DebugControlKind.Line, text = text ?? "", title = title };
    }

    /// <summary>
    /// Places the debug panel inside the screen and wraps its controls so every label stays inside its control.
    /// The pause menu and the fit tests both use this, so a new button cannot draw past the panel edge.
    /// </summary>
    public static class DebugPanelLayout
    {
        static Font font;

        public static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>Same scale as the HUD: how many screen pixels one reference pixel becomes.</summary>
        public static int Scale(float screenWidth, float screenHeight)
        {
            var reference = Tuning.I.referenceResolution;
            return Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(screenWidth / reference.x, screenHeight / reference.y)));
        }

        /// <summary>The panel under the pause buttons, inset from the screen edges.</summary>
        public static Rect Panel(float screenWidth, float screenHeight, int k)
        {
            float panelTop = 56f * k;
            float side = 4f * k;
            return new Rect(side, panelTop, Mathf.Max(0f, screenWidth - 2f * side), Mathf.Max(0f, screenHeight - panelTop - 4f * k));
        }

        /// <summary>Where controls are laid out, inside the panel border.</summary>
        public static Rect Content(Rect panel, int k)
        {
            float inset = 4f * k;
            return new Rect(panel.x + inset, panel.y + inset, Mathf.Max(0f, panel.width - 2f * inset), Mathf.Max(0f, panel.height - 2f * inset));
        }

        public static float ButtonHeight(int k) => 13f * k;

        public static float Gap(int k) => k;

        public static void ApplyMetrics(GUIStyle style, int k)
        {
            style.font = Font;
            style.fontSize = 8 * k;
            style.fixedHeight = 0f;
            style.wordWrap = true;
            style.clipping = TextClipping.Clip;
            style.margin = new RectOffset(0, 0, 0, 0);
            int pad = 3 * k;
            style.padding = new RectOffset(pad, pad, 0, 0);
            style.border = new RectOffset(k, k, k, k);
        }

        /// <summary>Width of the vertical scrollbar, so a scrolling page keeps its buttons clear of the bar.</summary>
        public static float ScrollbarWidth()
        {
            var bar = GUI.skin.verticalScrollbar;
            return bar != null && bar.fixedWidth > 1f ? bar.fixedWidth : 15f;
        }

        public static float Bottom(IReadOnlyList<Rect> rects)
        {
            float bottom = 0f;
            for (int i = 0; i < rects.Count; i++)
                if (rects[i].yMax > bottom) bottom = rects[i].yMax;
            return bottom;
        }

        /// <summary>
        /// Lays the controls out in rows of <paramref name="width"/>. A control that does not fit starts a new row.
        /// A control wider than the row takes the whole row and grows tall enough for its wrapped text.
        /// </summary>
        public static Rect[] Place(IReadOnlyList<DebugControl> controls, float width, int k, GUIStyle button, GUIStyle inline, GUIStyle title)
        {
            width = Mathf.Max(1f, width);
            float gap = Gap(k);
            float buttonH = ButtonHeight(k);
            var rects = new Rect[controls.Count];
            float x = 0f, y = 0f, rowH = 0f;
            bool open = false, gapPending = false;

            void CloseRow()
            {
                if (!open) return;
                y += rowH;
                x = 0f;
                rowH = 0f;
                open = false;
                gapPending = true;
            }

            void ApplyGap()
            {
                if (!gapPending) return;
                y += gap;
                gapPending = false;
            }

            for (int i = 0; i < controls.Count; i++)
            {
                var control = controls[i];
                if (control.kind == DebugControlKind.Line)
                {
                    CloseRow();
                    ApplyGap();
                    var style = control.title ? title : inline;
                    float h = Mathf.Max(buttonH, style.CalcHeight(new GUIContent(control.text), width));
                    rects[i] = new Rect(0f, y, width, h);
                    y += h;
                    gapPending = true;
                    continue;
                }

                if (control.rowBreak) CloseRow();
                var measure = control.kind == DebugControlKind.Button ? button : inline;
                float preferred = measure.CalcSize(new GUIContent(control.text)).x + 1f;
                float w = Mathf.Max(control.minWidth, preferred);
                float hgt = buttonH;
                if (w > width)
                {
                    CloseRow();
                    w = width;
                    hgt = Mathf.Max(buttonH, measure.CalcHeight(new GUIContent(control.text), width));
                }
                else if (open && x + w > width)
                {
                    CloseRow();
                }

                ApplyGap();
                rects[i] = new Rect(x, y, w, hgt);
                x += w + gap;
                rowH = Mathf.Max(rowH, hgt);
                open = true;
            }

            return rects;
        }

        /// <summary>
        /// Places controls in the view. When they are taller than the view, places them again beside a scrollbar.
        /// </summary>
        public static Rect[] PlaceInView(IReadOnlyList<DebugControl> controls, float viewWidth, float viewHeight, int k,
            GUIStyle button, GUIStyle inline, GUIStyle title, float scrollbarWidth, out float contentWidth, out float contentHeight)
        {
            contentWidth = Mathf.Max(1f, viewWidth);
            var rects = Place(controls, contentWidth, k, button, inline, title);
            contentHeight = Bottom(rects);
            if (contentHeight > viewHeight + 0.5f && scrollbarWidth > 0f && viewWidth > scrollbarWidth + 1f)
            {
                contentWidth = viewWidth - scrollbarWidth;
                rects = Place(controls, contentWidth, k, button, inline, title);
                contentHeight = Bottom(rects);
            }
            return rects;
        }

        /// <summary>
        /// Null when every control's text fits inside its rect and inside the view width.
        /// <paramref name="requireNoScroll"/> also requires the page to fit in the view without scrolling.
        /// </summary>
        public static string FitReport(float screenWidth, float screenHeight, IReadOnlyList<DebugControl> controls,
            GUIStyle button, GUIStyle inline, GUIStyle title, bool requireNoScroll, float scrollbarWidth)
        {
            int k = Scale(screenWidth, screenHeight);
            var panel = Panel(screenWidth, screenHeight, k);
            if (panel.xMin < -0.5f || panel.yMin < -0.5f || panel.xMax > screenWidth + 0.5f || panel.yMax > screenHeight + 0.5f)
                return $"Panel {panel} is outside {screenWidth}x{screenHeight}.";
            var view = Content(panel, k);
            var rects = PlaceInView(controls, view.width, view.height, k, button, inline, title, scrollbarWidth, out float contentWidth, out float contentHeight);
            if (requireNoScroll && contentHeight > view.height + 0.5f)
                return $"Debug page is {contentHeight:0}px tall and the panel view is {view.height:0}px at {screenWidth:0}x{screenHeight:0} (scale {k}).";

            for (int i = 0; i < controls.Count; i++)
            {
                var control = controls[i];
                var rect = rects[i];
                if (rect.xMin < -0.5f || rect.yMin < -0.5f || rect.xMax > contentWidth + 0.5f)
                    return $"'{control.text}' is at {rect}, past the content width {contentWidth:0}.";
                if (rect.yMax > contentHeight + 0.5f)
                    return $"'{control.text}' extends below the scroll content.";

                if (control.kind == DebugControlKind.Line)
                {
                    var style = control.title ? title : inline;
                    float need = style.CalcHeight(new GUIContent(control.text), rect.width);
                    if (rect.height + 0.5f < need)
                        return $"'{control.text}' needs {need:0}px of height and has {rect.height:0}.";
                    continue;
                }

                var measure = control.kind == DebugControlKind.Button ? button : inline;
                float textWidth = measure.CalcSize(new GUIContent(control.text)).x;
                float textHeight = measure.CalcSize(new GUIContent("Ag")).y;
                if (rect.width + 0.5f < textWidth)
                {
                    float wrapped = measure.CalcHeight(new GUIContent(control.text), rect.width);
                    if (rect.height + 0.5f < wrapped)
                        return $"'{control.text}' is clipped: text is {textWidth:0}px, control is {rect.width:0}px wide and {rect.height:0}px tall (needs {wrapped:0}).";
                }
                if (rect.height + 0.5f < textHeight)
                    return $"'{control.text}' is shorter than its text ({rect.height:0} < {textHeight:0}).";
            }

            return null;
        }

        /// <summary>
        /// Lays out the debug pages with the longest real labels. Null when every control fits.
        /// The edit-mode test calls this.
        /// </summary>
        public static string ValidateSamples()
        {
            var screens = new[]
            {
                new Vector2(480, 270),
                new Vector2(1280, 720),
                new Vector2(1920, 1080),
                new Vector2(2560, 1080),
            };
            foreach (var screen in screens)
            {
                int k = Scale(screen.x, screen.y);
                var styles = MakeSampleStyles(k);
                string main = FitReport(screen.x, screen.y, SampleMainPage(k, styles), styles.button, styles.label, styles.title, true, 15f);
                if (main != null) return main;
                string balance = FitReport(screen.x, screen.y, SampleBalancePage(k, styles), styles.button, styles.label, styles.title, true, 15f);
                if (balance != null) return balance;
            }

            var narrow = MakeSampleStyles(1);
            string wrapped = FitReport(160, 270, SampleMainPage(1, narrow), narrow.button, narrow.label, narrow.title, false, 15f);
            if (wrapped != null) return wrapped;
            string scrolled = FitReport(480, 220, SampleMainPage(1, narrow), narrow.button, narrow.label, narrow.title, false, 15f);
            return scrolled;
        }

        struct SampleStyles
        {
            public GUIStyle button, label, title;
        }

        static SampleStyles MakeSampleStyles(int k)
        {
            var styles = new SampleStyles
            {
                button = new GUIStyle { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter },
                label = new GUIStyle { alignment = TextAnchor.MiddleLeft },
                title = new GUIStyle { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft },
            };
            ApplyMetrics(styles.button, k);
            ApplyMetrics(styles.label, k);
            ApplyMetrics(styles.title, k);
            return styles;
        }

        static string Widest(GUIStyle style, IEnumerable<string> options)
        {
            string best = "";
            float width = -1f;
            foreach (var option in options)
            {
                if (string.IsNullOrEmpty(option)) continue;
                float measured = style.CalcSize(new GUIContent(option)).x;
                if (measured > width)
                {
                    width = measured;
                    best = option;
                }
            }
            return best;
        }

        static List<DebugControl> SampleMainPage(int k, SampleStyles styles)
        {
            float U(float units) => units * k;
            string layout = Widest(styles.button, Names(StageLayouts.All, item => item.name)).ToUpperInvariant();
            string theme = Widest(styles.button, Names(Themes.All, item => item.name)).ToUpperInvariant();
            string debuff = Widest(styles.button, Names(Debuff.All, item => item.Name)).ToUpperInvariant();
            var page = new List<DebugControl>
            {
                DebugControl.Line("DEBUG MODE  ·  cheats stay on across runs  ·  1-4 spawn XL, 5-8 rollers and ceramic, F1 hitboxes, F3 test layout", true),
                DebugControl.Button("GOD MODE: OFF", U(100), true),
                DebugControl.Button("FREEZE TIMER: OFF", U(100)),
                DebugControl.Button("HITBOXES: OFF", U(100)),
                DebugControl.Button("LAYOUT: " + layout, U(100)),
                DebugControl.Button("STORM NOW", U(80), true),
                DebugControl.Button("CLEAR STAGE", U(80)),
                DebugControl.Button("FULL HEARTS", U(80)),
                DebugControl.Button("+1 HEART SLOT", U(90)),
                DebugControl.Inline("STAGE", U(40), true),
                DebugControl.Button("-W", U(22)),
                DebugControl.Button("-", U(18)),
                DebugControl.Inline("6-6", U(28)),
                DebugControl.Button("+", U(18)),
                DebugControl.Button("+W", U(22)),
                DebugControl.Button("GO TO STAGE", U(80)),
                DebugControl.Button("SURFACE: NORMAL", U(100), true),
                DebugControl.Button("THEME: " + theme, U(120)),
                DebugControl.Button("BOSS -5 HP", U(90)),
                DebugControl.Button("BALANCE", U(80)),
                DebugControl.Inline("BOSS", U(40), true),
                DebugControl.Button("AIMED", U(60)),
                DebugControl.Button("BOUNCY", U(60)),
                DebugControl.Button("BALLOON", U(70)),
                DebugControl.Button("SPIKES: OFF", U(90), true),
                DebugControl.Button("UNLOCK ALL: OFF", U(100)),
                DebugControl.Button("RESET UNLOCKS", U(100)),
                DebugControl.Inline("SPAWN XL", U(52), true),
            };
            foreach (BubbleKind kind in System.Enum.GetValues(typeof(BubbleKind)))
                if (kind <= BubbleKind.Lucky) page.Add(DebugControl.Button(kind.ToString().ToUpperInvariant(), U(62)));
            page.Add(DebugControl.Button("HARD: " + debuff, U(110)));
            page.Add(DebugControl.Inline("SPAWN NEW", U(52), true));
            foreach (BubbleKind kind in System.Enum.GetValues(typeof(BubbleKind)))
                if (kind > BubbleKind.Lucky) page.Add(DebugControl.Button(kind.ToString().ToUpperInvariant(), U(62)));
            page.Add(DebugControl.Line("ADD UPGRADE (no limits)"));
            for (int i = 0; i < Upgrade.All.Length; i++)
            {
                var upgrade = Upgrade.All[i];
                string label = upgrade.Max > 1 ? $"{upgrade.Name} ({upgrade.Max})" : upgrade.Name;
                page.Add(DebugControl.Button(label, U(104), i % 4 == 0));
            }
            page.Add(DebugControl.Line("BUILD: " + SampleBuild()));
            return page;
        }

        static List<DebugControl> SampleBalancePage(int k, SampleStyles styles)
        {
            float U(float units) => units * k;
            string layout = Widest(styles.title, Names(StageLayouts.All, item => item.name)).ToUpperInvariant();
            string world = Widest(styles.title, Tuning.I.worldNames).ToUpperInvariant();
            string recipe = Widest(styles.title, Names(Tuning.I.recipes, item => item.name)).ToUpperInvariant();
            string pattern = "SCATTERED";
            foreach (PlacementPattern value in System.Enum.GetValues(typeof(PlacementPattern)))
            {
                string name = PlacementPatterns.NameOf(value).ToUpperInvariant();
                if (styles.title.CalcSize(new GUIContent(name)).x > styles.title.CalcSize(new GUIContent(pattern)).x)
                    pattern = name;
            }
            var bubbles = new System.Text.StringBuilder();
            foreach (BubbleKind kind in System.Enum.GetValues(typeof(BubbleKind)))
                foreach (var tier in Tuning.I.tiers)
                {
                    if (bubbles.Length > 0) bubbles.Append(", ");
                    bubbles.Append(kind).Append(' ').Append(tier.name).Append(" x9");
                }
            return new List<DebugControl>
            {
                DebugControl.Button("BACK", U(60), true),
                DebugControl.Button("WIN STAGE", U(80)),
                DebugControl.Button("NEXT LAYOUT", U(90)),
                DebugControl.Button("NEXT RECIPE", U(100)),
                DebugControl.Button("NEXT WAVE", U(90)),
                DebugControl.Button("LOG RUN", U(70)),
                DebugControl.Line($"WORLD 6-5 {world}  ·  HARD  ·  LAYOUT {layout}  ·  RECIPE {recipe}  ·  PATTERN {pattern} (re-rolled 12)", true),
                DebugControl.Line("BUDGET 99.5  =  bubbles 40.5  +  rollers 20  +  unspent 39"),
                DebugControl.Line("BUBBLES  " + bubbles),
                DebugControl.Line("BUBBLES CERAMIC 12  ·  ROLLERS 4 big (4 ceramic)  ·  STEEL 4"),
                DebugControl.Line("WAVES 4 (20 bubbles)"),
                DebugControl.Line("TIME LIMIT 99 s  ·  SPIKE STRIPS 5  ·  GAPS 4  ·  BREAKABLE TILES 20"),
                DebugControl.Line($"Jump budget bonus x{Tuning.I.jumpBudgetBonus:0.##}  ·  hard budget x{Tuning.I.hardBudgetScale:0.##}  ·  seed 2147483647"),
                DebugControl.Line($"LOG RUN prints all {Tuning.I.RunLength} stages of this seed to the Console, tab-separated."),
            };
        }

        static string SampleBuild()
        {
            var parts = new List<string>();
            foreach (var upgrade in Upgrade.All)
            {
                if (upgrade.Max == 0) continue;
                parts.Add(upgrade.Max > 1 ? $"{upgrade.Name} x{upgrade.Max}" : upgrade.Name);
            }
            return string.Join(", ", parts);
        }

        static IEnumerable<string> Names<T>(IReadOnlyList<T> items, System.Func<T, string> name)
        {
            if (items == null) yield break;
            foreach (var item in items)
                if (item != null) yield return name(item);
        }
    }
}
