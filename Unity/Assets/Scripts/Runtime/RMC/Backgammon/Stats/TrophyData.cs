using System;
using System.Collections.Generic;

namespace Runtime.RMC.Backgammon.Stats
{
    [Serializable]
    public class TrophyData
    {
        public string trophyId;
        public string name;
        public string description;
        public int    stakeAmount;
        public bool   isUnlocked;
        public string dateUnlocked;
    }

    [Serializable]
    public class TrophyDataList
    {
        public List<TrophyData> items = new();
    }
}
