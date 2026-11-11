using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// The text colours the HUD and menus use, the backgrounds they sit on, and the WCAG contrast maths.
    /// The game draws with these, and the accessibility tests check every pair, so a palette change that makes
    /// text hard to read fails a test instead of shipping.
    /// </summary>
    public static class UiText
    {
        // Text roles
        public static readonly Color32 Primary = Palette.Cream;   // banners, titles, numbers
        public static readonly Color32 Secondary = Palette.Mist;  // prompts, hints, descriptions
        public static readonly Color32 Accent = Palette.Mint;     // score lines, the highlighted card
        public static readonly Color32 Warning = Palette.Lemon;   // storm timer, storm banner, active cheats

        // How dark the backing panels are. Plates sit behind banner text; Dim covers the stage under the upgrade cards.
        public const float PlateAlpha = 0.8f;
        public const float DimAlpha = 0.7f;
        public const float TimerTrackAlpha = 0.6f;

        // Backgrounds. Text sits on the brightest thing a theme has behind it: the lighter of its skyBottom and far layer.
        // The Theme overloads give each theme's own backgrounds; the parameterless ones are the worst case over all themes.
        public static Color SkyOf(Theme theme) => theme.Brightest;
        public static Color Plate(Theme theme) => Over(Palette.Ink, PlateAlpha, SkyOf(theme));
        public static Color Dimmed(Theme theme) => Over(Palette.Ink, DimAlpha, SkyOf(theme));
        public static Color GroundStripOf(Theme theme) => theme.strip;
        public static Color TimerTrack(Theme theme) => Over(Palette.Ink, TimerTrackAlpha, SkyOf(theme));
        public static Color DebugPanel(Theme theme) => Over(Palette.Ink, 0.95f, Dimmed(theme));

        /// <summary>The brightest sky over every theme (Rooftops dusk today): the hardest background for light text.</summary>
        public static Color SkyWorst
        {
            get
            {
                Color worst = SkyOf(Themes.All[0]);
                foreach (var t in Themes.All)
                    if (RelativeLuminance(SkyOf(t)) > RelativeLuminance(worst)) worst = SkyOf(t);
                return worst;
            }
        }

        // Backgrounds that do not change with the theme.
        public static Color CardFill => Palette.Deep;
        public static Color CardFillHover => Palette.Mix(Palette.Deep, Palette.Mint, 0.18f);
        public static Color DebugButtonHover => Palette.Dusk;
        public static Color DebugButtonOn => Palette.Mint;

        /// <summary>WCAG AA: 4.5:1 for text, 3:1 for the edges of controls such as a card's border.</summary>
        public const float MinTextContrast = 4.5f;
        public const float MinControlContrast = 3f;

        /// <summary>Every text colour on every background it is drawn on, for every theme, named for test output.</summary>
        public static IEnumerable<(string name, Color text, Color background)> TextPairs()
        {
            foreach (var theme in Themes.All)
                foreach (var (name, text, background) in TextPairs(theme))
                    yield return ($"{name} [{theme.name}]", text, background);
        }

        public static IEnumerable<(string name, Color text, Color background)> TextPairs(Theme theme)
        {
            Color sky = SkyOf(theme), plate = Plate(theme), dimmed = Dimmed(theme), strip = GroundStripOf(theme), panel = DebugPanel(theme);
            yield return ("HUD stage and score on the sky", Primary, sky);
            yield return ("Score line on the sky", Accent, sky);
            yield return ("Banner on a plate", Primary, plate);
            yield return ("Score line on a plate", Accent, plate);
            yield return ("Prompt on a plate", Secondary, plate);
            yield return ("Pick title on the dimmed stage", Primary, dimmed);
            yield return ("Pick hints on the dimmed stage", Secondary, dimmed);
            yield return ("Card title", Primary, CardFill);
            yield return ("Card description", Secondary, CardFill);
            yield return ("Card number", Secondary, CardFill);
            yield return ("Hovered card title", Accent, CardFillHover);
            yield return ("Hovered card description", Primary, CardFillHover);
            yield return ("Desktop help in the ground strip", Secondary, strip);
            yield return ("Build line in the ground strip", Primary, strip);
            yield return ("Timer warning and cheats on the sky", Warning, sky);
            yield return ("Storm banner on a plate", Warning, plate);
            yield return ("Hard road line on a plate", Warning, plate);
            yield return ("Hard road title on a card", Warning, CardFill);
            yield return ("Hard road title on a lit card", Warning, CardFillHover);
            yield return ("Reward line on a card", Accent, CardFill);
            yield return ("Reward line on a lit card", Accent, CardFillHover);
            yield return ("Road hint in the ground strip", Secondary, strip);
            yield return ("RARE label on a card", Warning, CardFill);
            yield return ("RARE label on a hovered card", Warning, CardFillHover);
            yield return ("Pause button label", Primary, CardFill);
            yield return ("Debug panel title", Warning, panel);
            yield return ("Debug panel labels", Secondary, panel);
            yield return ("Debug button", Primary, CardFill);
            yield return ("Debug button under the mouse", Primary, DebugButtonHover);
            yield return ("Debug toggle that is on", Palette.Ink, DebugButtonOn);
        }

        /// <summary>Card borders and bars against the backgrounds around them, for every theme.</summary>
        public static IEnumerable<(string name, Color edge, Color background)> ControlPairs()
        {
            foreach (var theme in Themes.All)
                foreach (var (name, edge, background) in ControlPairs(theme))
                    yield return ($"{name} [{theme.name}]", edge, background);
        }

        public static IEnumerable<(string name, Color edge, Color background)> ControlPairs(Theme theme)
        {
            Color sky = SkyOf(theme), plate = Plate(theme), dimmed = Dimmed(theme), track = TimerTrack(theme);
            yield return ("Card border", Palette.Lilac, dimmed);
            yield return ("Rare card border", Palette.Lemon, dimmed);
            yield return ("Hovered card border", Palette.Mint, dimmed);
            yield return ("Menu button border on a plate", Palette.Lilac, plate);
            yield return ("Hovered menu button border on a plate", Palette.Mint, plate);
            yield return ("Pause button border on the sky", Palette.Mist, sky);
            yield return ("Timer bar", Palette.Mint, track);
            yield return ("Timer bar, last seconds", Palette.Lemon, track);
            yield return ("Timer bar, storm", Palette.Coral, track);
            yield return ("Debug button border on the panel", Palette.Lilac, DebugPanel(theme));
            yield return ("Filled stat pip on a character card", Palette.Mint, CardFill);
            yield return ("Empty stat pip outline on a character card", Palette.Mist, CardFill);
            yield return ("Filled stat pip on a lit character card", Palette.Mint, CardFillHover);
            yield return ("Empty stat pip outline on a lit character card", Palette.Mist, CardFillHover);
        }

        /// <summary>Colour a with alpha laid over an opaque background.</summary>
        public static Color Over(Color a, float alpha, Color background) => Color.Lerp(background, a, alpha);

        public static float RelativeLuminance(Color c)
        {
            static float Lin(float v) => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
            return 0.2126f * Lin(c.r) + 0.7152f * Lin(c.g) + 0.0722f * Lin(c.b);
        }

        /// <summary>WCAG contrast ratio, from 1 (same colour) to 21 (black on white).</summary>
        public static float Contrast(Color a, Color b)
        {
            float la = RelativeLuminance(a), lb = RelativeLuminance(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }
    }
}
