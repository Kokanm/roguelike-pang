using System;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// The look of one world: sky, two silhouette layers, tile colours, ground strip. Values come from
    /// docs/design/expansion-art.md. Every theme is a night scene dark enough for cream text and all four bubble colours
    /// (ThemeTests enforces it). Scenery never uses saffron, mint or the bubble colours.
    /// </summary>
    public class Theme
    {
        public string name;
        /// <summary>Vertical gradient, drawn as 6 flat bands.</summary>
        public Color32 skyTop, skyBottom;
        /// <summary>Two silhouette layers, far then near.</summary>
        public Color32 far, near;
        public Color32 tileFill, tileCap, tileShade, tileLine;
        public Color32 breakFill, breakLine;
        /// <summary>Small lights and details.</summary>
        public Color32 accent;
        /// <summary>Ground strip under the floor, and the camera clear colour.</summary>
        public Color32 strip;
        public Action<PixelCanvas, Theme, System.Random> paintBackground;

        /// <summary>The lighter of skyBottom and far: the brightest thing text can sit on in this theme.</summary>
        public Color32 Brightest => UiText.RelativeLuminance(skyBottom) >= UiText.RelativeLuminance(far) ? skyBottom : far;
    }

    public static class Themes
    {
        /// <summary>Index 0 = world 1.</summary>
        public static readonly Theme[] All =
        {
            // Rooftops uses the existing Palette colours, so the current look does not change.
            Make("Rooftops", Palette.Ink, Palette.Dusk, Palette.Deep, Palette.Ink,
                Palette.Dusk, Palette.Lilac, Palette.Deep, Palette.Ink, Palette.Hex(0x6E67B0), Palette.Cream, Palette.Ink,
                ThemeBackgrounds.Rooftops),
            Make("Forest", 0x0C1C1A, 0x1E3F36, 0x16302A, 0x0A1614,
                0x3B5A3A, 0x6FA35A, 0x24382A, 0x0A1614, 0x5E7F52, 0xD8F27A, 0x0A1614),
            Make("Desert", 0x1E1430, 0x5A2E4A, 0x5C3644, 0x2C1A2C,
                0x8C6A55, 0xB08A72, 0x5E4238, 0x20121F, 0xA88468, 0xF2D9B0, 0x20121F),
            Make("Winter", 0x101A33, 0x1F3358, 0x294168, 0x16264A,
                0x51658F, 0xDCEBFA, 0x34456B, 0x0C1428, 0x7388B0, 0xF2FBFF, 0x0C1428),
            Make("Catacombs", 0x120F1A, 0x2A2233, 0x332A3F, 0x1A1522,
                0x6E6478, 0xA79FB0, 0x463E50, 0x120F1A, 0x8C8296, 0x9FD4FF, 0x120F1A),
            Make("Lava", 0x17090E, 0x4A1620, 0x5C1F26, 0x240C12,
                0x4A3A44, 0x8E7885, 0x2E222A, 0x17090E, 0x66535E, 0xC22A1F, 0x17090E),
        };

        /// <summary>World 1 to 6; anything outside that range is clamped.</summary>
        public static Theme ForWorld(int world) => All[Mathf.Clamp(world, 1, All.Length) - 1];

        static Theme Make(string name, int skyTop, int skyBottom, int far, int near,
            int tileFill, int tileCap, int tileShade, int tileLine, int breakFill, int accent, int strip,
            Action<PixelCanvas, Theme, System.Random> painter = null) =>
            Make(name, Palette.Hex(skyTop), Palette.Hex(skyBottom), Palette.Hex(far), Palette.Hex(near),
                Palette.Hex(tileFill), Palette.Hex(tileCap), Palette.Hex(tileShade), Palette.Hex(tileLine),
                Palette.Hex(breakFill), Palette.Hex(accent), Palette.Hex(strip), painter);

        static Theme Make(string name, Color32 skyTop, Color32 skyBottom, Color32 far, Color32 near,
            Color32 tileFill, Color32 tileCap, Color32 tileShade, Color32 tileLine, Color32 breakFill, Color32 accent, Color32 strip,
            Action<PixelCanvas, Theme, System.Random> painter = null) =>
            new Theme
            {
                name = name, skyTop = skyTop, skyBottom = skyBottom, far = far, near = near,
                tileFill = tileFill, tileCap = tileCap, tileShade = tileShade, tileLine = tileLine,
                breakFill = breakFill, breakLine = tileShade, accent = accent, strip = strip,
                paintBackground = painter ?? ThemeBackgrounds.Simple,
            };
    }
}
