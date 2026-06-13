using UnityEngine;

namespace Runtime.RMC.Backgammon.Core
{
    [CreateAssetMenu(menuName = "RMC/Backgammon/Stake Level", fileName = "StakeLevel")]
    public class StakeLevelSo : ScriptableObject
    {
        [SerializeField] public int stakeAmount;
        [SerializeField] public int unlockThreshold;
        [SerializeField] public string trophyName;
        [SerializeField] public string trophyDescription;
        [SerializeField] public Sprite trophyIcon;

        public bool IsUnlocked(int balance) => unlockThreshold == 0 || balance >= unlockThreshold;
    }
}
