using System;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Reads an ASCII stage diagram (top row first) into a StageMap. A stage is 1 to 5 storeys of 14 rows (ordinary stages stay at 3 or fewer).
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

            string groundLine = ground;
            int tileRows;
            int inlineIndex = -1;
            if (ground == null && IsInlineGround(rows.Length))
            {
                tileRows = rows.Length - 1;
                inlineIndex = tileRows;
                groundLine = rows[inlineIndex];
            }
            else if (rows.Length % StageMap.StoreyRows == 0 && rows.Length / StageMap.StoreyRows <= StageMap.MaxStoreys && rows.Length > 0)
                tileRows = rows.Length;
            else
                throw new ArgumentException(
                    $"Stage row {Mathf.Max(0, rows.Length - 1)} does not fit a map of 1 to {StageMap.MaxStoreys} storeys ({StageMap.StoreyRows} rows each, plus an optional ground row), got {rows.Length}.",
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

        static bool IsInlineGround(int length) =>
            length > 1 && (length - 1) % StageMap.StoreyRows == 0 && (length - 1) / StageMap.StoreyRows <= StageMap.MaxStoreys;

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
