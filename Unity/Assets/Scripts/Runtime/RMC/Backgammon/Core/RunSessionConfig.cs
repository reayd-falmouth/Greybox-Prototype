using System;

namespace Runtime.RMC.Backgammon.Core
{
    public enum RunSessionType { Small, Big, Boss }

    [Serializable]
    public class RunSessionConfig
    {
        public RunSessionType SessionType;
        public int ScoreThreshold;
        public int MaxGames;
        public int Reward;
        // Set at runtime by BackgammonRunModeManager when the session begins; not authored by designers
        [NonSerialized] public BossVariantType ResolvedBossVariant = BossVariantType.Standard;
    }
}
