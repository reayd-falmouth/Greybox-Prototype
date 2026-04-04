namespace Runtime.RMC.Backgammon.Core
{
    /// <summary>
    /// Maps EngineCore point indices (0–23 board, 24 bar) to this scene's <see cref="BoardPoint.pointIndex"/>.
    /// Derived from aligning GNUBG standard start with the scene BoardManager checker layout.
    /// </summary>
    public static class BackgammonBoardLayout
    {
        public const int BarEngineIndex = 24;

        /// <summary>Engine board point (0–23) → <c>BoardPoint.pointIndex</c>.</summary>
        public static int EnginePointToBoardIndex(int enginePoint)
        {
            return 23 - enginePoint;
        }

        /// <summary><c>BoardPoint.pointIndex</c> → engine board point (0–23).</summary>
        public static int BoardIndexToEnginePoint(int boardPointIndex)
        {
            return 23 - boardPointIndex;
        }
    }
}
