namespace Runtime.RMC.Backgammon.Core
{
    public readonly struct RunSessionResult
    {
        public readonly int AnteIndex;
        public readonly int SessionIndex;
        public readonly RunSessionType SessionType;
        public readonly BossVariantType BossVariant;
        public readonly int Score;
        public readonly int Threshold;
        public readonly int GamesPlayed;
        public readonly int Reward;

        public RunSessionResult(int anteIndex, int sessionIndex, RunSessionType sessionType,
            BossVariantType bossVariant, int score, int threshold, int gamesPlayed, int reward)
        {
            AnteIndex    = anteIndex;
            SessionIndex = sessionIndex;
            SessionType  = sessionType;
            BossVariant  = bossVariant;
            Score        = score;
            Threshold    = threshold;
            GamesPlayed  = gamesPlayed;
            Reward       = reward;
        }
    }
}
