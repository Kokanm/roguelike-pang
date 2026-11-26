using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// The two doors of the road choice (step 14), 30 x 45 px each, painted from the NEXT world's theme: an arched stone
    /// frame (tileCap rim, tileFill stone, tileShade joints) around a dark opening in skyTop, 1 px ink outline outside.
    /// The calm door has a small cream lantern above the arch. The hard door has three steel spikes along the top of the
    /// frame and a lemon warning triangle with an ink exclamation mark above them. Values from docs/design/expansion-art.md.
    /// The pivot is bottom centre, so a door stands on the floor at its x.
    /// </summary>
    public static class DoorArt
    {
        public const int Width = 30, Height = 45;

        // Rows trimmed from each side of the outer arch and of the opening, counted from the top row down.
        static readonly int[] OuterCut = { 7, 4, 2, 1 };
        static readonly int[] InnerCut = { 6, 3, 1 };
        const int OuterTop = 30, InnerTop = 26;
        const int OuterLeft = 1, OuterRight = 28, InnerLeft = 5, InnerRight = 24;

        public static bool InOuter(int x, int y)
        {
            if (y < 1 || y > OuterTop) return false;
            int cut = OuterTop - y < OuterCut.Length ? OuterCut[OuterTop - y] : 0;
            return x >= OuterLeft + cut && x <= OuterRight - cut;
        }

        public static bool InOpening(int x, int y)
        {
            if (y < 1 || y > InnerTop) return false;
            int cut = InnerTop - y < InnerCut.Length ? InnerCut[InnerTop - y] : 0;
            return x >= InnerLeft + cut && x <= InnerRight - cut;
        }

        public static PixelCanvas Paint(Theme theme, bool hard)
        {
            var c = new PixelCanvas(Width, Height);
            for (int y = 1; y <= OuterTop; y++)
            {
                for (int x = OuterLeft; x <= OuterRight; x++)
                {
                    if (!InOuter(x, y)) continue;
                    if (InOpening(x, y)) { c.Set(x, y, theme.skyTop); continue; }
                    bool rim = !InOuter(x - 1, y) || !InOuter(x + 1, y) || !InOuter(x, y + 1);
                    bool joint = !rim && (y < 24 ? (y + (x < 15 ? 0 : 3)) % 6 == 0 : x % 6 == 0);
                    c.Set(x, y, rim ? theme.tileCap : joint ? theme.tileShade : theme.tileFill);
                }
            }

            if (hard)
            {
                // Three steel spikes on the crown of the frame, left edge light.
                for (int i = 0; i < 3; i++)
                {
                    int x0 = 9 + i * 4;
                    c.Set(x0, 31, Palette.SteelLight);
                    c.Set(x0 + 1, 31, Palette.Steel);
                    c.Set(x0 + 2, 31, Palette.Steel);
                    c.Set(x0 + 1, 32, Palette.SteelLight);
                }
                // Warning triangle, 7 px wide at the base and 6 tall (9 x 8 with its outline).
                int[] widths = { 7, 7, 5, 5, 3, 1 };
                for (int r = 0; r < widths.Length; r++)
                    for (int x = 14 - widths[r] / 2; x <= 14 + widths[r] / 2; x++)
                        c.Set(x, 37 + r, Palette.Lemon);
                for (int y = 39; y <= 41; y++) c.Set(14, y, Palette.Ink); // exclamation line
                c.Set(14, 37, Palette.Ink);                              // and its dot
            }
            else
            {
                // A small cream lantern on a steel pole: cap, glass with darker side bars, base.
                var glassShade = Palette.Shade(Palette.Cream, 0.75f);
                for (int y = 31; y <= 33; y++) c.Set(14, y, Palette.Steel);
                for (int x = 12; x <= 16; x++) c.Set(x, 34, Palette.Steel);
                for (int y = 35; y <= 40; y++)
                    for (int x = 12; x <= 16; x++)
                        c.Set(x, y, x == 12 || x == 16 ? glassShade : Palette.Cream);
                for (int x = 12; x <= 16; x++) c.Set(x, 41, Palette.Steel);
                c.Set(14, 42, Palette.Steel);
            }

            c.Outline(Palette.Ink);
            return c;
        }
    }
}
