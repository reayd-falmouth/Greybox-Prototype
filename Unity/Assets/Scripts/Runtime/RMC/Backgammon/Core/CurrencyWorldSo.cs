using UnityEngine;

namespace Runtime.RMC.Backgammon.Core
{
    [CreateAssetMenu(menuName = "RMC/Backgammon/Currency World", fileName = "CurrencyWorld")]
    public class CurrencyWorldSo : ScriptableObject
    {
        [SerializeField] public string currencyCode;           // e.g. "USD"
        [SerializeField] public string currencySymbol;         // e.g. "$"
        [SerializeField] public string displayName;            // e.g. "US Dollar"
        [SerializeField] public string narrativeDescription;   // shown on graduation / world entry

        [SerializeField] public Color primaryChipColor   = Color.red;
        [SerializeField] public Color secondaryChipColor = Color.white;

        [SerializeField] public StakeLevelLibrarySo stakeLevels;
        [SerializeField] public Sprite worldBadgeSprite;

        /// <summary>Poker chip sprite shown in the Collection tab (e.g. red_poker_chip, gold_poker_chip).</summary>
        [SerializeField] public Sprite chipSprite;

        /// <summary>Starting balance for this world (raw units in this currency).</summary>
        [SerializeField] public int startingBalance = 100;
    }
}
