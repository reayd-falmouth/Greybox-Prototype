using System.Collections.Generic;
using EngineCore;

namespace Runtime.RMC.Backgammon.Core
{
    /// <summary>
    /// First-step destinations for a given origin across legal turns (distinct <see cref="Move.To"/> values).
    /// </summary>
    public static class BackgammonMovableDestinations
    {
        public static void CollectDistinctFirstMoveTos(int fromEngine, IReadOnlyList<Turn> legals, HashSet<int> dest)
        {
            dest.Clear();
            if (legals == null) return;
            for (int i = 0; i < legals.Count; i++)
            {
                Turn t = legals[i];
                if (t?.Moves == null || t.Moves.Count == 0) continue;
                Move m = t.Moves[0];
                if (m.From != fromEngine) continue;
                dest.Add(m.To);
            }
        }
    }
}
