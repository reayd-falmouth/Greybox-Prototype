using System;

namespace Runtime.RMC.Backgammon.Core
{
    [Serializable]
    public class WeightedBossVariant
    {
        public BossVariantType Variant = BossVariantType.Standard;
        public int Weight = 10;
        // When true this entry is skipped during random draw until the variant is implemented
        public bool NotYetImplemented = false;
    }
}
