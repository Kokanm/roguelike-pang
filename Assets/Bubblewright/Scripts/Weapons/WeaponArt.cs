using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Weapon pixel art painted from each spec's pixel maps, so a new weapon needs no art code: its icon (crate, button,
    /// HUD), what Wick holds, the crate, the weapon button and the shared Shot. Everything is built once and cached.
    /// </summary>
    public static class WeaponArt
    {
        static int Ppu => Tuning.I.pixelsPerUnit;

        /// <summary>The colour of a pixel-map letter, or null for an empty pixel.</summary>
        public static Color32? ColorOf(char ch)
        {
            switch (ch)
            {
                case 'S': return Palette.Steel;
                case 'L': return Palette.SteelLight;
                case 'D': return Palette.SteelDark;
                case 'R': return Palette.Spark;
                case 'Y': return Palette.SparkCore;
                case 'W': return new Color32(255, 255, 255, 255);
                case 'C': return Palette.Cream;
                case 'O': return Palette.Gas;
                case 'o': return Palette.GasDark;
                case 'M': return Palette.Mint;
                case 'K': return Palette.Ink;
                default: return null;
            }
        }

        public static int WidthOf(string[] rows)
        {
            int w = 0;
            if (rows != null) foreach (var r in rows) w = Mathf.Max(w, r.Length);
            return w;
        }

        /// <summary>Paints rows (top row first) with its bottom-left pixel at (x, y), y up.</summary>
        public static void Paint(PixelCanvas c, string[] rows, int x, int y)
        {
            if (rows == null) return;
            for (int row = 0; row < rows.Length; row++)
            {
                int py = y + rows.Length - 1 - row;
                for (int col = 0; col < rows[row].Length; col++)
                {
                    var color = ColorOf(rows[row][col]);
                    if (color.HasValue) c.Set(x + col, py, color.Value);
                }
            }
        }

        /// <summary>The rows with a 1 px ink outline on the outside.</summary>
        public static PixelCanvas Outlined(string[] rows)
        {
            int w = WidthOf(rows), h = rows?.Length ?? 0;
            var c = new PixelCanvas(w + 2, h + 2);
            Paint(c, rows, 1, 1);
            c.Outline(Palette.Ink);
            return c;
        }

        static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Texture2D> iconTextures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Sprite> held = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Sprite> heldFlash = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Sprite> crates = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Sprite> buttons = new Dictionary<string, Sprite>();
        static readonly Dictionary<int, Sprite> numbers = new Dictionary<int, Sprite>();

        static bool Fresh<T>(Dictionary<string, T> cache, string key, out T value) where T : Object =>
            cache.TryGetValue(key, out value) && value != null;

        /// <summary>The icon with its outline, pivot in the middle.</summary>
        public static Sprite Icon(WeaponSpec spec)
        {
            if (spec == null) return null;
            if (Fresh(icons, spec.Id, out var s)) return s;
            var c = Outlined(spec.IconRows);
            return icons[spec.Id] = c.ToSprite("WeaponIcon_" + spec.Id, new Vector2(c.Width * 0.5f, c.Height * 0.5f), Ppu);
        }

        /// <summary>The icon as a texture for the OnGUI HUD.</summary>
        public static Texture2D IconTexture(WeaponSpec spec)
        {
            if (spec == null) return null;
            if (Fresh(iconTextures, spec.Id, out var t)) return t;
            return iconTextures[spec.Id] = Outlined(spec.IconRows).ToTexture("WeaponIconTex_" + spec.Id);
        }

        /// <summary>
        /// What Wick holds up in his raised hand instead of the launcher, or null to keep the launcher. The pivot puts the
        /// middle of the art where the middle of the launcher is (3.5 px into its sprite) and its foot on the launcher's,
        /// so Wick.LauncherTip still sits at the top and the wall-pose mirroring still lines up.
        /// </summary>
        public static Sprite Held(WeaponSpec spec, bool flash = false)
        {
            if (spec?.HeldRows == null) return null;
            var cache = flash ? heldFlash : held;
            if (Fresh(cache, spec.Id, out var s)) return s;
            var c = Outlined(spec.HeldRows);
            if (flash) c = c.Silhouette(Palette.Cream);
            var pivot = new Vector2(c.Width * 0.5f - 3.5f, 0f);
            return cache[spec.Id] = c.ToSprite((flash ? "WeaponHeldFlash_" : "WeaponHeld_") + spec.Id, pivot, Ppu);
        }

        /// <summary>The crate: a 17 x 17 box with a 1 px mint ring, ink inside and the icon, plus the outline. Pivot bottom middle.</summary>
        public static Sprite Crate(WeaponSpec spec)
        {
            if (spec == null) return null;
            if (Fresh(crates, spec.Id, out var s)) return s;
            var c = new PixelCanvas(19, 19);
            c.Rect(2, 1, 15, 17, Palette.Mint);
            c.Rect(1, 2, 17, 15, Palette.Mint);
            c.Rect(2, 2, 15, 15, Palette.Ink);
            c.Set(2, 2, Palette.Mint);
            c.Set(16, 2, Palette.Mint);
            c.Set(2, 16, Palette.Mint);
            c.Set(16, 16, Palette.Mint);
            var rows = spec.IconRows;
            int w = WidthOf(rows), h = rows?.Length ?? 0;
            Paint(c, rows, 1 + (17 - w) / 2, 1 + (17 - h) / 2);
            c.Outline(Palette.Ink);
            return crates[spec.Id] = c.ToSprite("WeaponCrate_" + spec.Id, new Vector2(9.5f, 0f), Ppu);
        }

        /// <summary>The weapon button: the mint disc of the fire and jump buttons with an ink circle and the icon in it.</summary>
        public static Sprite Button(WeaponSpec spec, bool pressed)
        {
            if (spec == null) return null;
            string key = spec.Id + (pressed ? "_down" : "_up");
            if (Fresh(buttons, key, out var s)) return s;
            var c = new PixelCanvas(46, 46);
            var face = pressed ? Palette.Shade(Palette.Mint, 0.8f) : Palette.Mint;
            c.Circle(23f, 23f, 21f, Palette.Shade(Palette.Mint, 0.62f));
            c.Circle(23f, pressed ? 22f : 24f, 20f, face);
            float cy = ButtonIconCentreY(pressed);
            c.Circle(23f, cy, 10f, Palette.Ink);
            var rows = spec.IconRows;
            int w = WidthOf(rows), h = rows?.Length ?? 0;
            Paint(c, rows, 23 - w / 2, Mathf.RoundToInt(cy - h * 0.5f));
            c.Outline(Palette.Ink);
            return buttons[key] = c.ToSprite("WeaponButton_" + key, new Vector2(23f, 23f), Ppu);
        }

        /// <summary>Height of the icon circle's centre in the 46 px button.</summary>
        public static float ButtonIconCentreY(bool pressed) => pressed ? 26f : 28f;

        /// <summary>Where the ammo number's middle sits, in button pixels from its centre (down is negative).</summary>
        public static float ButtonNumberOffsetPixels(bool pressed) => pressed ? -14f : -12f;

        static readonly string[] Digits =
        {
            "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001",
            "111100111001111", "111100111101111", "111001001001001", "111101111101111", "111101111001111",
        };

        /// <summary>A number in the 3 x 5 pixel font, in ink with no outline, pivot in the middle. Used under the button icon.</summary>
        public static Sprite Number(int n)
        {
            n = Mathf.Max(0, n);
            if (numbers.TryGetValue(n, out var s) && s != null) return s;
            string text = n.ToString();
            var c = new PixelCanvas(text.Length * 4 - 1, 5);
            for (int i = 0; i < text.Length; i++)
            {
                string d = Digits[text[i] - '0'];
                for (int k = 0; k < 15; k++)
                    if (d[k] == '1') c.Set(i * 4 + k % 3, 4 - k / 3, Palette.Ink);
            }
            return numbers[n] = c.ToSprite("AmmoNumber_" + n, new Vector2(Mathf.Floor(c.Width * 0.5f), 2f), Ppu); // whole pixels, so it stays on the grid
        }

        static Sprite shot;

        /// <summary>The shared Shot: 3 x 6 px of spark red with a yellow core, ink outline, pivot in the middle.</summary>
        public static Sprite ShotSprite
        {
            get
            {
                if (shot != null && shot.texture != null) return shot;
                var c = new PixelCanvas(5, 8);
                c.Rect(1, 1, 3, 6, Palette.Spark);
                c.Rect(2, 2, 1, 4, Palette.SparkCore);
                c.Set(2, 5, new Color32(255, 255, 255, 255));
                c.Outline(Palette.Ink);
                shot = c.ToSprite("Shot", new Vector2(2.5f, 4f), Ppu);
                return shot;
            }
        }
    }
}
