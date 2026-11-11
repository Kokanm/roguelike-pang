using System;

namespace Bubblewright
{
    /// <summary>
    /// Reads a 32 x 14 ASCII stage diagram (top row first, row 13 down to row 0) into a StageMap.
    /// </summary>
    public static class StageLayout
    {
        public static StageMap Parse(string[] rows)
        {
            if (rows == null || rows.Length != StageMap.Rows)
                throw new ArgumentException($"Stage layout must have {StageMap.Rows} rows, got {(rows == null ? 0 : rows.Length)}.", nameof(rows));

            var map = StageMap.Flat();
            for (int i = 0; i < StageMap.Rows; i++)
            {
                string line = rows[i];
                int r = StageMap.Rows - 1 - i;
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
                            map.Set(c, r, new Cell { kind = TileKind.Solid });
                            break;
                        case 'B':
                            map.Set(c, r, new Cell { kind = TileKind.Breakable, hp = 2 });
                            break;
                        case 'H':
                            map.Set(c, r, new Cell { kind = TileKind.Empty, ladder = true });
                            break;
                        case 'T':
                            map.Set(c, r, new Cell { kind = TileKind.Solid, ladder = true });
                            break;
                        default:
                            throw new ArgumentException($"Unknown stage character '{ch}' at column {c}, row {r}.", nameof(rows));
                    }
                }
            }
            return map;
        }
    }
}
