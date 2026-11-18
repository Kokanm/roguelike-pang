using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Where the chosen items go now that the footer is gone: a small see-through strip of half-size icons in the
    /// top right corner during play (under the score), and a full list with names on the pause screen.
    /// Pixels are screen pixels; k is the HUD scale (DebugPanelLayout.Scale). The fit tests use the same numbers.
    /// </summary>
    public static class ItemsLayout
    {
        // ---------------------------------------------------------------- play HUD strip

        /// <summary>Most upgrades the strip can hold: one icon for each upgrade that is listed in the build.</summary>
        public static int MaxUpgradeItems
        {
            get
            {
                int n = 0;
                foreach (var u in Upgrade.All) if (u.Max != 0) n++;
                return n;
            }
        }

        /// <summary>One icon cell of the strip: the icon, a 1 px gap, then stack pips (one per copy taken) under it.</summary>
        public static float StripCell(int k) => (PlaceholderArt.UpgradeMiniIconSize + 1) * k;

        public static float StripHeight(int k) => (PlaceholderArt.UpgradeMiniIconSize + 3) * k;

        /// <summary>Icon rects, right-aligned to <paramref name="right"/>, first item leftmost.</summary>
        public static Rect[] Strip(float right, float top, int count, int k)
        {
            var rects = new Rect[Mathf.Max(0, count)];
            float cell = StripCell(k), size = PlaceholderArt.UpgradeMiniIconSize * k;
            float x0 = right - count * cell + k;
            for (int i = 0; i < rects.Length; i++) rects[i] = new Rect(x0 + i * cell, top, size, size);
            return rects;
        }

        /// <summary>The strip's row: under the score, the same row the HARD ROAD label uses on the left.</summary>
        public static float StripTop(float hudTop, float pad, float hudDrop, int k) => hudTop + pad + 16 * k + hudDrop;

        // ---------------------------------------------------------------- pause screen

        public static float PauseTop(float screenHeight, int k) => screenHeight * 0.5f - 40 * k;
        public static float PauseButtonsY(float screenHeight, int k) => PauseTop(screenHeight, k) + 20 * k;
        public static float PauseButtonHeight(int k) => 22 * k;
        public static float PauseHintY(float screenHeight, int k) => PauseButtonsY(screenHeight, k) + PauseButtonHeight(k) + 12 * k;
        public static float PauseItemsTitleY(float screenHeight, int k) => PauseHintY(screenHeight, k) + 14 * k;
        public static float PauseGridTop(float screenHeight, int k) => PauseItemsTitleY(screenHeight, k) + 12 * k;

        /// <summary>Lines of keyboard and pad controls at the foot of the pause screen (desktop only).</summary>
        public static readonly string[] ControlLines =
        {
            "Move A/D or arrows  ·  Climb W/S  ·  Fire Space  ·  Jump L/Shift  ·  Weapon I/U",
            "Pad: stick + A fire, B/Y jump, RT/RB weapon  ·  F1 hitboxes  ·  F3 next stage",
        };

        public static float PauseControlsY(float screenHeight, int k) => screenHeight - (4 + 11 * ControlLines.Length) * k;

        /// <summary>A pause list cell: the 18 px icon box, then the name (and "x2" or ammo) beside it.</summary>
        public static Vector2 PauseCell(int k) => new Vector2(112 * k, 20 * k);
        public static float PauseGap(int k) => 4 * k;

        public static int PauseColumns(float screenWidth, int count, int k)
        {
            var cell = PauseCell(k);
            float usable = Mathf.Min(screenWidth, Tuning.I.referenceResolution.x * k) - 12 * k;
            int fit = Mathf.Max(1, Mathf.FloorToInt((usable + PauseGap(k)) / (cell.x + PauseGap(k))));
            return Mathf.Clamp(count, 1, fit);
        }

        /// <summary>Cells centred on the screen, in rows from <paramref name="top"/>.</summary>
        public static Rect[] PauseGrid(float screenWidth, float top, int count, int k)
        {
            var rects = new Rect[Mathf.Max(0, count)];
            if (count == 0) return rects;
            int cols = PauseColumns(screenWidth, count, k);
            var cell = PauseCell(k);
            float gap = PauseGap(k);
            for (int i = 0; i < count; i++)
            {
                int row = i / cols, col = i % cols;
                int inRow = Mathf.Min(cols, count - row * cols);
                float rowWidth = inRow * cell.x + (inRow - 1) * gap;
                float x0 = Mathf.Round((screenWidth - rowWidth) * 0.5f);
                rects[i] = new Rect(x0 + col * (cell.x + gap), top + row * (cell.y + gap * 0.5f), cell.x, cell.y);
            }
            return rects;
        }

        /// <summary>
        /// Null when, with every upgrade taken and both weapon slots full, the strip stays in the right half of the
        /// arena and the pause list fits between its title and the controls lines. The edit-mode test calls this.
        /// </summary>
        public static string ValidateSamples()
        {
            var screens = new[] { new Vector2(480, 270), new Vector2(1280, 720), new Vector2(1920, 1080), new Vector2(2560, 1080) };
            int upgrades = MaxUpgradeItems;
            int all = upgrades + 2; // weapon and harpoon head
            foreach (var s in screens)
            {
                int k = DebugPanelLayout.Scale(s.x, s.y);
                float arenaW = Tuning.I.referenceResolution.x * k;
                float arenaLeft = (s.x - arenaW) * 0.5f, arenaRight = arenaLeft + arenaW;
                float pad = 6 * k;

                var strip = Strip(arenaRight - pad, StripTop(0f, pad, 0f, k), upgrades, k);
                if (strip.Length > 0)
                {
                    float centre = s.x * 0.5f;
                    if (strip[0].xMin < centre + 40 * k)
                        return $"Item strip starts at x {strip[0].xMin:0}, too close to the timer in the middle ({centre:0}) at {s.x}x{s.y}.";
                    if (strip[strip.Length - 1].xMax > arenaRight + 0.5f)
                        return $"Item strip ends past the arena edge at {s.x}x{s.y}.";
                }

                var grid = PauseGrid(s.x, PauseGridTop(s.y, k), all, k);
                float bottom = PauseControlsY(s.y, k) - 2 * k;
                foreach (var r in grid)
                {
                    if (r.xMin < -0.5f || r.xMax > s.x + 0.5f)
                        return $"Pause item {r} is off the {s.x}x{s.y} screen.";
                    if (r.yMax > bottom + 0.5f)
                        return $"Pause item list reaches y {r.yMax:0}, over the controls lines at {bottom:0} ({s.x}x{s.y}, scale {k}).";
                }
            }
            return null;
        }
    }
}
