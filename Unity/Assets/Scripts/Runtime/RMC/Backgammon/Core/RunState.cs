namespace Runtime.RMC.Backgammon.Core
{
    public class RunState
    {
        public int CurrentAnteIndex    { get; set; }
        public int CurrentSessionIndex { get; set; }
        public int RunningScore        { get; set; }
        public int GamesPlayedThisSession { get; set; }
        public int TotalCurrency       { get; set; }
        public BossVariantType ActiveBossVariant { get; set; } = BossVariantType.Standard;
        public bool IsRunOver { get; set; }
        public bool IsRunWon  { get; set; }

        public RunSessionConfig CurrentSession(RunConfig cfg)
            => cfg.Antes[CurrentAnteIndex].GetSession(CurrentSessionIndex);

        public bool IsBossSession => CurrentSessionIndex == 2;
        public bool IsLastSession(RunConfig cfg)
            => CurrentAnteIndex == cfg.Antes.Count - 1 && CurrentSessionIndex == 2;

        public void ResetForNewSession(BossVariantType bossVariant = BossVariantType.Standard)
        {
            RunningScore = 0;
            GamesPlayedThisSession = 0;
            ActiveBossVariant = bossVariant;
        }

        // Advances to the next session (or next ante). Returns true if the run is now won.
        public bool AdvanceSession(RunConfig cfg)
        {
            CurrentSessionIndex++;
            if (CurrentSessionIndex > 2)
            {
                CurrentSessionIndex = 0;
                CurrentAnteIndex++;
            }

            if (CurrentAnteIndex >= cfg.Antes.Count)
            {
                IsRunWon = true;
                return true;
            }

            BossVariantType nextBoss = IsBossSession
                ? cfg.Antes[CurrentAnteIndex].DrawBossVariant()
                : BossVariantType.Standard;

            ResetForNewSession(nextBoss);
            return false;
        }
    }
}
