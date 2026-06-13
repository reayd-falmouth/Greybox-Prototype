namespace Runtime.RMC.Backgammon.Core
{
    public enum BossVariantType
    {
        Standard,       // Normal rules, higher threshold only
        NoCube,         // Doubling cube disabled — score from gammon/backgammon only
        NoDoublets,     // Rolling doublets gives only 2 moves instead of 4
        HigherThreshold,// Threshold +50% but reward increased proportionally
        FewerGames,     // Only 2 games instead of the default max
        Nackgammon,     // Nackgammon starting position (2 extra checkers on bar)
        Hypergammon,    // 1 checker per home point — fast and volatile
        AceyDeucey,     // Acey-Deucey rules variant
    }
}
