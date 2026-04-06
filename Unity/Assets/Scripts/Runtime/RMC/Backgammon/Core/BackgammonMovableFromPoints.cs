using System.Collections.Generic;
using EngineCore;

namespace Runtime.RMC.Backgammon.Core
{
    /// <summary>
    /// Derives which engine "from" points (0–23 board, 24 bar) can start at least one legal full turn
    /// (first step of each <see cref="Turn"/> in the legal list).
    /// </summary>
    public static class BackgammonMovableFromPoints
    {
        public static void CollectMovableFromEnginePoints(IReadOnlyList<Turn> legals, HashSet<int> dest)
        {
            dest.Clear();
            if (legals == null) return;
            for (int i = 0; i < legals.Count; i++)
            {
                Turn t = legals[i];
                if (t?.Moves == null || t.Moves.Count == 0) continue;
                dest.Add(t.Moves[0].From);
            }
        }
    }
}
