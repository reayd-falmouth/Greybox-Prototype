namespace Runtime.RMC.Backgammon.Core
{
    public enum BackgammonBoardViewMode
    {
        Horizontal = 0,
        Vertical = 1
    }

    /// <summary>
    /// Maps EngineCore point indices (0–23 board, 24 bar) to this scene's <see cref="BoardPoint.pointIndex"/>.
    /// Derived from aligning GNUBG standard start with the scene BoardManager checker layout.
    /// </summary>
    public static class BackgammonBoardLayout
    {
        public const int BarEngineIndex = 24;
        private static BackgammonBoardViewMode _activeViewMode = BackgammonBoardViewMode.Horizontal;

        // Existing board mapping (kept as default to preserve current behavior).
        private static readonly int[] HorizontalEngineToBoard =
        {
            23, 22, 21, 20, 19, 18,
            17, 16, 15, 14, 13, 12,
            11, 10,  9,  8,  7,  6,
             5,  4,  3,  2,  1,  0
        };

        // Alternate mapping used by HUD Vertical view. This is a full permutation table so
        // visual placement can be changed without rotating checker game objects.
        private static readonly int[] VerticalEngineToBoard =
        {
            17, 16, 15, 14, 13, 12,
            23, 22, 21, 20, 19, 18,
             5,  4,  3,  2,  1,  0,
            11, 10,  9,  8,  7,  6
        };

        public static BackgammonBoardViewMode ActiveViewMode => _activeViewMode;

        public static void SetViewMode(BackgammonBoardViewMode viewMode)
        {
            _activeViewMode = viewMode;
        }

        public static void SetHorizontal(bool horizontal)
        {
            _activeViewMode = horizontal
                ? BackgammonBoardViewMode.Horizontal
                : BackgammonBoardViewMode.Vertical;
        }

        /// <summary>Engine board point (0–23) → <c>BoardPoint.pointIndex</c>.</summary>
        public static int EnginePointToBoardIndex(int enginePoint)
        {
            if (enginePoint < 0 || enginePoint > 23) return -1;
            return GetActiveEngineToBoard()[enginePoint];
        }

        /// <summary><c>BoardPoint.pointIndex</c> → engine board point (0–23).</summary>
        public static int BoardIndexToEnginePoint(int boardPointIndex)
        {
            if (boardPointIndex < 0 || boardPointIndex > 23) return -1;
            int[] map = GetActiveEngineToBoard();
            for (int e = 0; e < map.Length; e++)
            {
                if (map[e] == boardPointIndex)
                    return e;
            }

            return -1;
        }

        private static int[] GetActiveEngineToBoard()
        {
            return _activeViewMode == BackgammonBoardViewMode.Horizontal
                ? HorizontalEngineToBoard
                : VerticalEngineToBoard;
        }
    }
}
