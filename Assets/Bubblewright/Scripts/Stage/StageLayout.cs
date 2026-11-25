using System;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Reads an ASCII stage diagram (top row first) into a StageMap. A stage is 1, 2 or 3 storeys of 18 rows.
    /// A diagram still drawn with the old 14-row storeys is padded to 18 first (see PadLegacy).
    /// Tile characters: '.' empty, '#' solid (the default surface), 'N' solid normal, 'I' solid ice, 'S' solid sand,
    /// 'B' breakable, 'H' empty ladder, 'T' solid ladder. An optional ground string, or one extra row on the end, is
    /// the floor under row 0: '=' solid (the default surface), 'N', 'I', 'S', '_' a gap.
    /// </summary>
    public static class StageLayout
    {
        public static StageMap Parse(string[] rows, string ground = null, Surface defaultSurface = Surface.Normal)
        {
            if (rows == null)
                throw new ArgumentException("Stage row 0 is missing.", nameof(rows));
            rows = PadLegacy(rows, ground == null);

            string groundLine = ground;
            int tileRows;
            int inlineIndex = -1;
            if (ground == null && IsInlineGround(rows.Length))
            {
                tileRows = rows.Length - 1;
                inlineIndex = tileRows;
                groundLine = rows[inlineIndex];
            }
            else if (rows.Length == StageMap.StoreyRows || rows.Length == StageMap.StoreyRows * 2 || rows.Length == StageMap.StoreyRows * 3)
                tileRows = rows.Length;
            else
                throw new ArgumentException(
                    $"Stage row {Mathf.Max(0, rows.Length - 1)} does not fit a map of 1 to 3 storeys ({StageMap.StoreyRows} rows each, plus an optional ground row), got {rows.Length}.",
                    nameof(rows));

            var map = StageMap.Flat(tileRows / StageMap.StoreyRows);
            for (int i = 0; i < tileRows; i++)
            {
                string line = rows[i];
                int r = tileRows - 1 - i;
                if (line == null || line.Length != StageMap.Cols)
                    throw new ArgumentException($"Stage row {r} must have {StageMap.Cols} characters, got {(line == null ? 0 : line.Length)}.", nameof(rows));

                for (int c = 0; c < StageMap.Cols; c++)
                {
                    char ch = line[c];
                    switch (ch)
                    {
                        case '.':
                            break;
                        case '#':
                            map.Set(c, r, new Cell { kind = TileKind.Solid, surface = defaultSurface });
                            break;
                        case 'N':
                        case 'I':
                        case 'S':
                            map.Set(c, r, new Cell { kind = TileKind.Solid, surface = SurfaceOf(ch) });
                            break;
                        case 'B':
                            map.Set(c, r, new Cell
                            {
                                kind = TileKind.Breakable,
                                hp = (byte)Mathf.Clamp(Tuning.I.breakableHp, 1, byte.MaxValue),
                            });
                            break;
                        case 'H':
                            map.Set(c, r, new Cell { kind = TileKind.Empty, ladder = true });
                            break;
                        case 'T':
                            map.Set(c, r, new Cell { kind = TileKind.Solid, ladder = true, surface = defaultSurface });
                            break;
                        default:
                            throw new ArgumentException($"Unknown stage character '{ch}' at column {c}, row {r}.", nameof(rows));
                    }
                }
            }

            if (groundLine != null) ParseGround(map, groundLine, inlineIndex >= 0 ? inlineIndex : rows.Length, defaultSurface);
            else if (defaultSurface != Surface.Normal)
            {
                for (int c = 0; c < StageMap.Cols; c++) map.SetGround(c, defaultSurface);
            }
            return map;
        }

        /// <summary>
        /// True for a diagram drawn with 14-row storeys: 14, 28 or 42 lines, or one more with an inline ground row
        /// when <paramref name="inlineGroundAllowed"/>.
        /// </summary>
        public static bool IsLegacy(int length, bool inlineGroundAllowed = true)
        {
            int n = StageMap.LegacyStoreyRows;
            if (length == n || length == 2 * n || length == 3 * n) return true;
            return inlineGroundAllowed && (length == n + 1 || length == 2 * n + 1 || length == 3 * n + 1);
        }

        /// <summary>Extra rows each legacy storey gets: 18 - 14.</summary>
        public const int LegacyPadRows = StageMap.StoreyRows - StageMap.LegacyStoreyRows;

        /// <summary>
        /// Turns a 14-row-per-storey diagram into an 18-row one. Rows 0 to 12 of each storey keep their place; the
        /// storey's top row (its cap, row 13) moves up to row 17, and the 4 new rows go between them. A new row copies
        /// the cap's character in each column, so a wall or a hatch ladder that ran up to the ceiling still does, and
        /// is open everywhere else: the old stage with 4 more rows of headroom. Returns the input unchanged when it is
        /// not a legacy diagram.
        /// </summary>
        public static string[] PadLegacy(string[] rows, bool inlineGroundAllowed = true)
        {
            if (rows == null || !IsLegacy(rows.Length, inlineGroundAllowed)) return rows;
            int n = StageMap.LegacyStoreyRows;
            int tileRows = rows.Length - rows.Length % n;
            int storeys = tileRows / n;
            var padded = new string[rows.Length + storeys * LegacyPadRows];
            int o = 0;
            // Top row first: walk storeys from the top one down.
            for (int s = storeys - 1; s >= 0; s--)
            {
                int capIndex = tileRows - 1 - (s * n + n - 1);
                string cap = rows[capIndex];
                padded[o++] = cap;
                string fill = cap;
                if (cap != null)
                {
                    var chars = cap.ToCharArray();
                    for (int c = 0; c < chars.Length; c++)
                        if (chars[c] == 'T') chars[c] = 'H'; // a solid ladder top on the cap becomes the ladder under it
                    fill = new string(chars);
                }
                for (int i = 0; i < LegacyPadRows; i++) padded[o++] = fill;
                for (int r = n - 2; r >= 0; r--) padded[o++] = rows[tileRows - 1 - (s * n + r)];
            }
            for (int i = tileRows; i < rows.Length; i++) padded[o++] = rows[i]; // the inline ground row
            return padded;
        }

        /// <summary>Where a y in a legacy (14-row storey) stage ends up after PadLegacy: up 4 units per storey below it.</summary>
        public static float LegacyY(float y)
        {
            int storey = Mathf.Clamp(Mathf.FloorToInt(y / StageMap.LegacyStoreyRows), 0, 2);
            return y + storey * LegacyPadRows;
        }

        /// <summary>Same for a row number.</summary>
        public static int LegacyRow(int row) => row < 0 ? row : row + Mathf.Clamp(row / StageMap.LegacyStoreyRows, 0, 2) * LegacyPadRows;

        static bool IsInlineGround(int length) =>
            length == StageMap.StoreyRows + 1 || length == StageMap.StoreyRows * 2 + 1 || length == StageMap.StoreyRows * 3 + 1;

        static Surface SurfaceOf(char ch) => ch switch
        {
            'I' => Surface.Ice,
            'S' => Surface.Sand,
            _ => Surface.Normal,
        };

        /// <summary>The ground under row 0. rowIndex is named in the error when the string is the wrong width.</summary>
        static void ParseGround(StageMap map, string line, int rowIndex, Surface defaultSurface)
        {
            if (line == null || line.Length != StageMap.Cols)
                throw new ArgumentException($"Stage row {rowIndex} must have {StageMap.Cols} characters, got {(line == null ? 0 : line.Length)}.", nameof(line));
            for (int c = 0; c < StageMap.Cols; c++)
            {
                char ch = line[c];
                switch (ch)
                {
                    case '=':
                        map.SetGround(c, defaultSurface);
                        break;
                    case 'N':
                    case 'I':
                    case 'S':
                        map.SetGround(c, SurfaceOf(ch));
                        break;
                    case '_':
                        map.SetGap(c, true);
                        break;
                    default:
                        throw new ArgumentException($"Unknown ground character '{ch}' at column {c}, row {rowIndex}.", nameof(line));
                }
            }
        }
    }
}
