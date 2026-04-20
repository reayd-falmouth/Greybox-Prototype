namespace Runtime.RMC.Backgammon.Core
{
    public enum BackgammonBoardViewMode
    {
        Horizontal = 0,
        Vertical = 1
    }

    /// <summary>
    /// Maps EngineCore point indices (0–23 board, 24 bar) to this scene's <see cref="BoardPoint.pointIndex"/>.
    /// Horizontal: identity — engine <c>e</c> uses the point with <c>pointIndex == e</c> (matches <see cref="BoardManager.GenerateBoard"/> order).
    /// Vertical: full reverse — board slot <c>23 - e</c> (swap 0↔23, 1↔22, …).
    /// </summary>
    public static class BackgammonBoardLayout
    {
        public const int BarEngineIndex = 24;
        private static BackgammonBoardViewMode _activeViewMode = BackgammonBoardViewMode.Horizontal;

        private static readonly int[] HorizontalEngineToBoard = BuildIdentity();
        private static readonly int[] VerticalEngineToBoard = BuildReverse();

        private static int[] BuildIdentity()
        {
            var a = new int[24];
            for (int i = 0; i < 24; i++)
                a[i] = i;
            return a;
        }

        private static int[] BuildReverse()
        {
            var a = new int[24];
            for (int i = 0; i < 24; i++)
                a[i] = 23 - i;
            return a;
        }

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
