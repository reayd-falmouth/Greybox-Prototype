using System;
using System.Collections.Generic;
using UnityEngine;

namespace Runtime.RMC.Backgammon.Core
{
    [Serializable]
    public class RunAnteConfig
    {
        public RunSessionConfig SmallSession = new RunSessionConfig { SessionType = RunSessionType.Small };
        public RunSessionConfig BigSession   = new RunSessionConfig { SessionType = RunSessionType.Big };
        public RunSessionConfig BossSession  = new RunSessionConfig { SessionType = RunSessionType.Boss };

        [SerializeField]
        public List<WeightedBossVariant> BossVariantPool = new List<WeightedBossVariant>();

        public RunSessionConfig GetSession(int sessionIndex)
        {
            return sessionIndex switch
            {
                0 => SmallSession,
                1 => BigSession,
                _ => BossSession,
            };
        }

        public BossVariantType DrawBossVariant()
        {
            int totalWeight = 0;
            foreach (var entry in BossVariantPool)
            {
                if (!entry.NotYetImplemented)
                    totalWeight += entry.Weight;
            }

            if (totalWeight <= 0)
                return BossVariantType.Standard;

            int roll = UnityEngine.Random.Range(0, totalWeight);
            int cumulative = 0;
            foreach (var entry in BossVariantPool)
            {
                if (entry.NotYetImplemented) continue;
                cumulative += entry.Weight;
                if (roll < cumulative)
                    return entry.Variant;
            }

            return BossVariantType.Standard;
        }
    }
}
