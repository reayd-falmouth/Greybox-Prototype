using System;
using System.Collections.Generic;
using UnityEngine;

namespace Runtime.RMC.Backgammon.Core
{
    [Serializable]
    public class RunConfig
    {
        [SerializeField] private List<RunAnteConfig> _antes = new List<RunAnteConfig>();
        [SerializeField] public float ScaleFactor = 1.5f;
        [SerializeField] public int BaseSmallThreshold = 50;
        [SerializeField] public float RewardFraction = 0.4f;
        [SerializeField] public int MaxGamesPerSession = 3;

        public IReadOnlyList<RunAnteConfig> Antes => _antes;

        public static RunConfig BuildDefault(int anteCount = 3, float scaleFactor = 1.5f, int baseSmall = 50, int maxGames = 3)
        {
            var cfg = new RunConfig
            {
                ScaleFactor = scaleFactor,
                BaseSmallThreshold = baseSmall,
                MaxGamesPerSession = maxGames,
                RewardFraction = 0.4f,
            };

            for (int a = 0; a < anteCount; a++)
            {
                var ante = new RunAnteConfig();

                for (int s = 0; s < 3; s++)
                {
                    int k = a * 3 + s;
                    int threshold = Mathf.RoundToInt(baseSmall * Mathf.Pow(scaleFactor, k));
                    int reward    = RoundToNearest5(threshold * cfg.RewardFraction);
                    var sessionType = s == 0 ? RunSessionType.Small : s == 1 ? RunSessionType.Big : RunSessionType.Boss;

                    int fewerGames = maxGames > 1 ? maxGames - 1 : 1;
                    var session = new RunSessionConfig
                    {
                        SessionType    = sessionType,
                        ScoreThreshold = threshold,
                        MaxGames       = sessionType == RunSessionType.Boss ? maxGames : maxGames,
                        Reward         = reward,
                    };

                    switch (s)
                    {
                        case 0: ante.SmallSession = session; break;
                        case 1: ante.BigSession   = session; break;
                        case 2: ante.BossSession  = session; break;
                    }
                }

                ante.BossVariantPool = BuildBossPool(a);
                cfg._antes.Add(ante);
            }

            return cfg;
        }

        private static List<WeightedBossVariant> BuildBossPool(int anteIndex)
        {
            if (anteIndex == 0)
            {
                return new List<WeightedBossVariant>
                {
                    new WeightedBossVariant { Variant = BossVariantType.Standard,    Weight = 60 },
                    new WeightedBossVariant { Variant = BossVariantType.FewerGames,  Weight = 40 },
                };
            }

            if (anteIndex == 1)
            {
                return new List<WeightedBossVariant>
                {
                    new WeightedBossVariant { Variant = BossVariantType.Standard,        Weight = 20 },
                    new WeightedBossVariant { Variant = BossVariantType.FewerGames,      Weight = 20 },
                    new WeightedBossVariant { Variant = BossVariantType.NoCube,          Weight = 30 },
                    new WeightedBossVariant { Variant = BossVariantType.NoDoublets,      Weight = 20 },
                    new WeightedBossVariant { Variant = BossVariantType.Nackgammon,      Weight = 10, NotYetImplemented = true },
                };
            }

            // Ante 3+
            return new List<WeightedBossVariant>
            {
                new WeightedBossVariant { Variant = BossVariantType.NoCube,          Weight = 20 },
                new WeightedBossVariant { Variant = BossVariantType.NoDoublets,      Weight = 20 },
                new WeightedBossVariant { Variant = BossVariantType.HigherThreshold, Weight = 20 },
                new WeightedBossVariant { Variant = BossVariantType.Nackgammon,      Weight = 20, NotYetImplemented = true },
                new WeightedBossVariant { Variant = BossVariantType.Hypergammon,     Weight = 10, NotYetImplemented = true },
                new WeightedBossVariant { Variant = BossVariantType.AceyDeucey,      Weight = 10, NotYetImplemented = true },
            };
        }

        private static int RoundToNearest5(float value)
        {
            return Mathf.Max(5, Mathf.RoundToInt(value / 5f) * 5);
        }
    }
}
