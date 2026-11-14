using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Pause menu and debug panel. Pause with Esc, P, pad Start or the button in the top right corner; F2 pauses
    /// straight into the debug panel. The game does not tick while paused. Cheats stay on across runs until turned off.
    /// </summary>
    public partial class Game
    {
        struct DebugOptions
        {
            public bool god;          // bubbles pass through Wick
            public bool freezeTimer;  // the storm clock stops
            public bool spikes;       // add Lava spike strips on any world's stage
        }

        DebugOptions debug;
        bool paused, debugPanel;
        int pauseSelected, debugStage = 1;
        Vector2 debugScroll;
        List<DebugEntry> debugEntries = new List<DebugEntry>();

        struct DebugEntry
        {
            public DebugControl control;
            public bool on;
            public System.Action click;
        }
        Rect pauseButtonRect;
        readonly Rect[] pauseRects = new Rect[4];
        static readonly string[] PauseItems = { "RESUME", "RESTART RUN", "QUIT TO TITLE", "DEBUG" };

        bool CanPause => state == State.Intro || state == State.Play;

        /// <summary>Handles pausing and the pause menu. True while paused, so the game skips its tick.</summary>
        bool UpdatePause()
        {
            bool tapPause = input.TapPosition is Vector2 t && pauseButtonRect.Contains(new Vector2(t.x, Screen.height - t.y));
            if (!paused)
            {
                if (!CanPause || !(input.PausePressed || input.DebugPanelPressed || tapPause)) return false;
                Pause(input.DebugPanelPressed);
                return true;
            }

            if (input.PausePressed || tapPause)
            {
                Resume();
                return true; // skip this frame too, so the tap that resumed does not also fire
            }
            if (input.DebugPanelPressed)
            {
                debugPanel = !debugPanel;
                debugScroll = Vector2.zero;
            }

            int dir = input.Move > 0f ? 1 : input.Move < 0f ? -1 : 0;
            if (dir != 0 && dir != lastMoveDir) pauseSelected = (pauseSelected + dir + PauseItems.Length) % PauseItems.Length;
            lastMoveDir = dir;

            if (input.TapPosition is Vector2 tap)
            {
                var gui = new Vector2(tap.x, Screen.height - tap.y);
                for (int i = 0; i < PauseItems.Length; i++)
                    if (pauseRects[i].Contains(gui)) { Activate(i); return paused; }
            }
            if (input.KeyConfirmPressed) Activate(pauseSelected);
            return paused;
        }

        void Pause(bool openDebug)
        {
            paused = true;
            debugPanel = openDebug;
            pauseSelected = openDebug ? 3 : 0;
            debugStage = run.Stage;
            debugScroll = Vector2.zero;
            lastMoveDir = input.Move > 0f ? 1 : input.Move < 0f ? -1 : 0;
            for (int i = 0; i < pauseRects.Length; i++) pauseRects[i] = Rect.zero;
            Sfx.Play(Sound.Turn);
        }

        void Resume()
        {
            paused = false;
            debugPanel = false;
        }

        void Activate(int item)
        {
            switch (item)
            {
                case 0: Resume(); break;
                case 1: Resume(); SaveGame.Delete(); StartRun(run.Character); break;
                case 2: Resume(); SaveGame.UpdateHearts(wick.Hearts); OpenTitle(); break;
                case 3: debugPanel = !debugPanel; debugScroll = Vector2.zero; break;
            }
        }

        /// <summary>Enabled debug options for the HUD, or null when no cheat is on.</summary>
        string DebugSummary()
        {
            string summary = null;
            if (debug.god) summary = "GOD MODE";
            if (debug.freezeTimer) summary = summary == null ? "TIMER FROZEN" : summary + " · TIMER FROZEN";
            if (debug.spikes) summary = summary == null ? "SPIKES" : summary + " · SPIKES";
            if (Unlocks.DebugAll) summary = summary == null ? "ALL CHARACTERS" : summary + " · ALL CHARACTERS";
            if (testLayout)
            {
                string layout = $"TEST LAYOUT: {CurrentTestLayout.name.ToUpperInvariant()}";
                summary = summary == null ? layout : summary + " · " + layout;
            }
            return summary;
        }

        // ------------------------------------------------------------------ drawing

        void DrawPauseButton(Rect r, int k)
        {
            FillRect(r, Palette.Mist);
            FillRect(Grow(r, -k), UiText.CardFill);
            float barW = 2 * k, barH = r.height - 6 * k;
            FillRect(new Rect(r.center.x - 3 * k, r.y + 3 * k, barW, barH), UiText.Primary);
            FillRect(new Rect(r.center.x + k, r.y + 3 * k, barW, barH), UiText.Primary);
        }

        void DrawPause(int k, bool touchUi)
        {
            FillRect(new Rect(0, 0, Screen.width, Screen.height), WithAlpha(Palette.Ink, UiText.DimAlpha));
            DrawRoadStrip(k, run.World);
            float w = 96 * k, h = 22 * k, gap = 8 * k;
            float top, y;
            Rect debugPanelRect = default;
            if (debugPanel)
            {
                // Buttons sit in the gap Panel() leaves above itself. 18k is the title plus a gap.
                debugPanelRect = DebugPanelLayout.Panel(Screen.width, Screen.height, k);
                y = debugPanelRect.y - 4f * k - h;
                top = y - 18f * k;
            }
            else
            {
                top = Screen.height * 0.5f - 40 * k;
                y = top + 20 * k;
            }
            Centered("PAUSED", top, 14 * k, UiText.Primary);

            float x0 = (Screen.width - (PauseItems.Length * w + (PauseItems.Length - 1) * gap)) * 0.5f;
            bool canHover = !Application.isMobilePlatform;
            Vector2 mouse = Event.current.mousePosition;
            bool mouseMoved = (mouse - lastMouse).sqrMagnitude > 0.5f;
            lastMouse = mouse;
            for (int i = 0; i < PauseItems.Length; i++)
            {
                pauseRects[i] = new Rect(x0 + i * (w + gap), y, w, h);
                if (canHover && mouseMoved && pauseRects[i].Contains(mouse)) pauseSelected = i;
            }
            for (int i = 0; i < PauseItems.Length; i++)
            {
                string label = i == 3 ? (debugPanel ? "HIDE DEBUG" : "DEBUG") : PauseItems[i];
                DrawMenuButton(pauseRects[i], label, canHover && i == pauseSelected, k);
            }

            if (debugPanel) DrawDebugPanel(debugPanelRect, k);
            else Centered(touchUi ? "Tap the pause button to resume" : "Esc, P or Start to resume    F2: debug", y + h + 12 * k, 8 * k, UiText.Secondary);
        }

        /// <summary>Same look as the upgrade cards: the whole button glows when lit.</summary>
        void DrawMenuButton(Rect r, string label, bool lit, int k)
        {
            if (lit)
            {
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f);
                for (int g = 3; g >= 1; g--)
                    FillRect(Grow(r, g * k), WithAlpha(Palette.Mint, 0.12f * pulse * (4 - g) / 3f));
            }
            int border = lit ? 2 * k : k;
            FillRect(r, lit ? Palette.Mint : Palette.Lilac);
            FillRect(Grow(r, -border), lit ? UiText.CardFillHover : UiText.CardFill);
            // Middle-centred in the button itself, so the text sits in the middle whatever the font's line height is.
            textStyle.alignment = TextAnchor.MiddleCenter;
            SetTextColor(textStyle, Palette.Ink);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), label, textStyle);
            SetTextColor(textStyle, lit ? UiText.Accent : UiText.Primary);
            GUI.Label(r, label, textStyle);
        }

        // ------------------------------------------------------------------ debug panel

        GUIStyle dbgButton, dbgOn, dbgLabel, dbgTitle;
        int dbgScale;

        /// <summary>A button background: fill with a k-pixel lilac border, so buttons stand out from the panel (3:1).</summary>
        static Texture2D Framed(Color fill, int k)
        {
            int size = 2 * k + 1;
            var t = new Texture2D(size, size) { filterMode = FilterMode.Point };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    t.SetPixel(x, y, x < k || y < k || x >= size - k || y >= size - k ? (Color)Palette.Lilac : fill);
            t.Apply();
            return t;
        }

        void EnsureDebugStyles(int k)
        {
            if (dbgButton == null || dbgScale != k)
            {
                dbgScale = k;
                var dbgFill = Framed(UiText.CardFill, k);
                var dbgFillHover = Framed(UiText.DebugButtonHover, k);
                var dbgFillOn = Framed(UiText.DebugButtonOn, k);
                var dbgFillPressed = Framed(Palette.Mint, k);
                dbgButton = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                dbgButton.normal.background = dbgFill;
                dbgButton.hover.background = dbgFillHover;
                dbgButton.active.background = dbgFillPressed;
                dbgButton.focused.background = dbgFill;
                SetTextColor(dbgButton, UiText.Primary);
                dbgButton.active.textColor = Palette.Ink;
                dbgOn = new GUIStyle(dbgButton);
                dbgOn.normal.background = dbgOn.focused.background = dbgFillOn;
                dbgOn.hover.background = dbgFillOn;
                SetTextColor(dbgOn, Palette.Ink);
                dbgLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft };
                SetTextColor(dbgLabel, UiText.Secondary);
                dbgTitle = new GUIStyle(dbgLabel) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
                SetTextColor(dbgTitle, UiText.Warning);
            }
            foreach (var st in new[] { dbgButton, dbgOn, dbgLabel, dbgTitle })
                DebugPanelLayout.ApplyMetrics(st, k);
        }

        void AddDebugButton(string text, int k, bool rowBreak, System.Action click, bool on = false, float minUnits = 0f)
        {
            debugEntries.Add(new DebugEntry
            {
                control = DebugControl.Button(text, minUnits * k, rowBreak),
                on = on,
                click = click,
            });
        }

        void AddDebugToggle(string text, int k, bool rowBreak, bool on, System.Action<bool> set, float minUnits)
        {
            AddDebugButton(text + (on ? ": ON" : ": OFF"), k, rowBreak, () => set(!on), on, minUnits);
        }

        void AddDebugInline(string text, int k, bool rowBreak, float minUnits) =>
            debugEntries.Add(new DebugEntry { control = DebugControl.Inline(text, minUnits * k, rowBreak) });

        void AddDebugLine(string text, bool title = false) =>
            debugEntries.Add(new DebugEntry { control = DebugControl.Line(text, title) });

        void DrawDebugPanel(Rect panel, int k)
        {
            EnsureDebugStyles(k);
            FillRect(panel, Palette.Lilac);
            FillRect(Grow(panel, -k), WithAlpha(Palette.Ink, 0.95f));

            debugEntries.Clear();
            AddDebugLine("DEBUG MODE  ·  cheats stay on across runs  ·  1-4 spawn XL, 5-8 rollers and ceramic, F1 hitboxes, F3 test layout", true);
            if (balancePage) AddBalancePage(k);
            else AddDebugPage(k);
            DrawDebugEntries(DebugPanelLayout.Content(panel, k), k);
        }

        void AddDebugPage(int k)
        {
            AddDebugToggle("GOD MODE", k, true, debug.god, v => debug.god = v, 100);
            AddDebugToggle("FREEZE TIMER", k, false, debug.freezeTimer, v => debug.freezeTimer = v, 100);
            AddDebugToggle("HITBOXES", k, false, showHitboxes, v => showHitboxes = v, 100);
            AddDebugButton(testLayout ? $"LAYOUT: {CurrentTestLayout.name.ToUpperInvariant()}" : "TEST LAYOUT", k, false, ToggleTestLayout, minUnits: 100);

            AddDebugButton("STORM NOW", k, true, () => { if (!storm && (state == State.Play || state == State.Intro)) StartStorm(); }, minUnits: 80);
            AddDebugButton("CLEAR STAGE", k, false, DebugClearStage, minUnits: 80);
            AddDebugButton("FULL HEARTS", k, false, () => DebugSetHearts(run.MaxHearts, run.MaxHearts), minUnits: 80);
            AddDebugButton("+1 HEART SLOT", k, false, () => DebugSetHearts(run.MaxHearts + 1, wick.Hearts + 1), minUnits: 90);

            AddDebugInline("STAGE", k, true, 40);
            AddDebugButton("-W", k, false, () => debugStage = Mathf.Max(1, debugStage - T.stagesPerWorld), minUnits: 22);
            AddDebugButton("-", k, false, () => debugStage = Mathf.Max(1, debugStage - 1), minUnits: 18);
            AddDebugInline($"{Run.WorldOf(debugStage)}-{Run.StageInWorldOf(debugStage)}", k, false, 28);
            AddDebugButton("+", k, false, () => debugStage = Mathf.Min(T.RunLength, debugStage + 1), minUnits: 18);
            AddDebugButton("+W", k, false, () => debugStage = Mathf.Min(T.RunLength, debugStage + T.stagesPerWorld), minUnits: 22);
            AddDebugButton("GO TO STAGE", k, false, () => DebugGoToStage(debugStage), minUnits: 80);

            AddDebugButton(DebugSurfaceLabel(), k, true, DebugCycleSurface, minUnits: 100);
            AddDebugButton(DebugThemeLabel(), k, false, DebugCycleTheme, minUnits: 120);
            AddDebugButton("BOSS -5 HP", k, false, () => DebugBossDamage(5), minUnits: 90);
            AddDebugButton("BALANCE", k, false, () => { balancePage = true; balanceNote = null; debugScroll = Vector2.zero; }, minUnits: 80);

            if (boss != null)
            {
                AddDebugInline("BOSS", k, true, 40);
                AddDebugButton("AIMED", k, false, DebugBossAimed, minUnits: 60);
                AddDebugButton("BOUNCY", k, false, DebugBossBouncy, minUnits: 60);
                AddDebugButton("BALLOON", k, false, DebugBossBalloon, minUnits: 70);
            }

            AddDebugToggle("SPIKES", k, true, debug.spikes, SetDebugSpikes, 90);
            AddDebugToggle("UNLOCK ALL", k, false, Unlocks.DebugAll, v => Unlocks.DebugAll = v, 100);
            AddDebugButton("RESET UNLOCKS", k, false, Unlocks.Reset, minUnits: 100);

            AddDebugInline("SPAWN XL", k, true, 52);
            foreach (BubbleKind kind in System.Enum.GetValues(typeof(BubbleKind)))
            {
                if (kind > BubbleKind.Lucky) continue;
                var spawn = kind;
                AddDebugButton(spawn.ToString().ToUpperInvariant(), k, false, () => DebugSpawn(spawn), minUnits: 62);
            }
            AddDebugButton(DebugHardLabel(), k, false, DebugCycleHardWorld, minUnits: 110);

            AddDebugInline("SPAWN NEW", k, true, 52);
            foreach (BubbleKind kind in System.Enum.GetValues(typeof(BubbleKind)))
            {
                if (kind <= BubbleKind.Lucky) continue;
                var spawn = kind;
                AddDebugButton(spawn.ToString().ToUpperInvariant(), k, false, () => DebugSpawn(spawn), minUnits: 62);
            }

            AddDebugLine("ADD UPGRADE (no limits)");
            for (int i = 0; i < Upgrade.All.Length; i++)
            {
                var upgrade = Upgrade.All[i];
                int n = run.TimesTaken(upgrade);
                AddDebugButton(n > 0 ? $"{upgrade.Name} ({n})" : upgrade.Name, k, i % 4 == 0, () => DebugTake(upgrade), minUnits: 104);
            }
            AddDebugLine($"BUILD: {run.BuildSummary()}");
        }

        void DrawDebugEntries(Rect view, int k)
        {
            var controls = new DebugControl[debugEntries.Count];
            for (int i = 0; i < debugEntries.Count; i++) controls[i] = debugEntries[i].control;

            var rects = DebugPanelLayout.PlaceInView(controls, view.width, view.height, k, dbgButton, dbgLabel, dbgTitle,
                DebugPanelLayout.ScrollbarWidth(), out float contentWidth, out float contentHeight);
            var content = new Rect(0f, 0f, contentWidth, Mathf.Max(contentHeight, 1f));
            var scrollSkin = GUI.skin.scrollView;
            GUI.skin.scrollView = GUIStyle.none;
            debugScroll = GUI.BeginScrollView(view, debugScroll, content, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
            try
            {
                for (int i = 0; i < debugEntries.Count; i++)
                {
                    var entry = debugEntries[i];
                    var rect = rects[i];
                    if (entry.control.kind == DebugControlKind.Button)
                    {
                        if (GUI.Button(rect, entry.control.text, entry.on ? dbgOn : dbgButton)) entry.click?.Invoke();
                    }
                    else GUI.Label(rect, entry.control.text, entry.control.title ? dbgTitle : dbgLabel);
                }
            }
            finally
            {
                GUI.EndScrollView();
                GUI.skin.scrollView = scrollSkin;
            }
        }

        void DebugClearStage()
        {
            foreach (var b in bubbles) if (b) Destroy(b.gameObject);
            bubbles.Clear();
            hitBubbles.Clear();
            ClearRollers();
            waves = null;
            HideWaveMarkers();
            if (boss != null && boss.Hp > 0) boss.Damage(boss.Hp);
            if (state == State.Intro)
            {
                wick.Waving = false;
                wick.Turn(false);
                SetState(State.Play);
            }
            Resume();
        }

        void DebugSetHearts(int max, int hearts)
        {
            run.MaxHearts = max;
            run.Hearts = Mathf.Clamp(hearts, 1, max);
            wick.SetHearts(run.Hearts);
        }

        string DebugHardLabel() => run.CurrentDebuff == null ? "HARD: OFF" : $"HARD: {run.CurrentDebuff.Name.ToUpperInvariant()}";

        /// <summary>Calm, then each debuff in turn, then calm again. Restarts the stage so the numbers apply.</summary>
        void DebugCycleHardWorld()
        {
            var current = run.CurrentDebuff;
            int next = current == null ? 0 : System.Array.IndexOf(Debuff.All, current) + 1;
            run.SetHard(run.World, next < Debuff.All.Length ? Debuff.All[next] : null);
            DebugGoToStage(run.Stage);
        }

        void DebugGoToStage(int stage)
        {
            run.Hearts = Mathf.Max(1, wick.Hearts);
            run.Stage = Mathf.Clamp(stage, 1, T.RunLength);
            Resume();
            StartStage();
        }

        /// <summary>Applies an upgrade straight away, ignoring its limit.</summary>
        void DebugTake(Upgrade u)
        {
            run.Hearts = wick.Hearts;
            run.Take(u);
            run.Hearts = Mathf.Min(run.Hearts, run.MaxHearts);
            ApplyRunUpgrades();
            wick.SetHearts(run.Hearts);
        }
    }
}
