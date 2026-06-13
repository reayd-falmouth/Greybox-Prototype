using System.Collections.Generic;
using UnityEngine;

namespace Runtime.RMC.Backgammon.Core
{
    [CreateAssetMenu(fileName = "GameModePreset", menuName = "RMC/Backgammon/Game Mode Preset")]
    public class GameModePresetSo : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Stones X Dice";
        public GameModeType gameModeType = GameModeType.MoneySession;

        [TextArea(2, 4)]
        public string description = "Casual play with customizable stakes and doubling rules.";

        [Header("Variant")]
        public GameVariantType gameVariantType = GameVariantType.Standard;
        public string startingPositionId = "4HPwATDgc/ABMA";

        [Header("Preview Image")]
        public Sprite previewImage;

        [Header("Default Config")]
        public MoneySessionConfig defaultConfig = new MoneySessionConfig();

        [Header("Currency")]
        public string currencyCode = "USD";
        public string currencySymbol = "$";
        [TextArea(2, 3)]
        public string narrativeDescription = "";
        public int startingBalance = 100;
        public Sprite chipSprite;

        [Header("Chip Sprites")]
        public List<Sprite> chipStakeSprites = new();

        [Header("Transition Theme")]
        public Color wipeColor = new Color(0.039f, 0.075f, 0.098f, 1f);
        public Sprite wipeIcon;

        [Header("Stakes")]
        [SerializeField] public List<StakeLevelSo> stakes = new();

        public int StakeCount => stakes?.Count ?? 0;

        public StakeLevelSo GetStake(int index) =>
            (stakes != null && index >= 0 && index < stakes.Count) ? stakes[index] : null;

        public List<StakeLevelSo> GetUnlockedStakes(int balance) =>
            stakes?.FindAll(s => s.IsUnlocked(balance)) ?? new List<StakeLevelSo>();
    }
}
