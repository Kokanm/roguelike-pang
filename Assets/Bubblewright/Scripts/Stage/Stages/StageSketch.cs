namespace Bubblewright
{
    /// <summary>
    /// Builds a stage grid by row number (0 is the ground row). Lines() flips it so Row 0 of the
    /// array is the top of the map, which is what StageSpec wants.
    /// </summary>
    static class StageSketch
    {
        public static char[][] Open(int storeys = 1)
        {
            int n = StageMap.StoreyRows * storeys;
            var grid = new char[n][];
            for (int y = 0; y < n; y++)
            {
                grid[y] = new char[StageMap.Cols];
                for (int c = 0; c < StageMap.Cols; c++) grid[y][c] = '.';
            }
            return grid;
        }

        public static void Box(char[][] grid, int c0, int c1, int y0, int y1, char ch)
        {
            for (int y = y0; y <= y1; y++)
                for (int c = c0; c <= c1; c++)
                    grid[y][c] = ch;
        }

        public static string[] Lines(char[][] grid)
        {
            int n = grid.Length;
            var lines = new string[n];
            for (int y = 0; y < n; y++) lines[n - 1 - y] = new string(grid[y]);
            return lines;
        }
    }
}
