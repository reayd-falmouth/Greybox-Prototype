namespace Runtime.RMC.Backgammon.Core
{
    /// <summary>
    /// Defines the available game modes for backgammon gameplay.
    /// </summary>
    public enum GameModeType
    {
        /// <summary>Money session with variable stakes and no fixed endpoint.</summary>
        MoneySession = 0,

        /// <summary>Match play with a fixed target score.</summary>
        MatchPlay = 1,

        /// <summary>Run mode with ante-based progression (small blind, big blind, boss).</summary>
        Run = 2
    }
}
