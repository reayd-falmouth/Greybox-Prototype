using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Runtime.RMC.Backgammon.Core
{
    [CreateAssetMenu(menuName = "RMC/Backgammon/Stake Level Library", fileName = "StakeLevelLibrary")]
    public class StakeLevelLibrarySo : ScriptableObject
    {
        [SerializeField] public List<StakeLevelSo> levels = new();

        public List<StakeLevelSo> GetUnlocked(int balance) =>
            levels.Where(l => l.IsUnlocked(balance)).ToList();

        public StakeLevelSo GetByAmount(int amount) =>
            levels.FirstOrDefault(l => l.stakeAmount == amount);

        public StakeLevelSo DefaultLevel =>
            levels.Count > 0 ? levels[0] : null;
    }
}
