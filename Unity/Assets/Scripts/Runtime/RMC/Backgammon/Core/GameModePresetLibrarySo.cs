using System.Collections.Generic;
using UnityEngine;

namespace Runtime.RMC.Backgammon.Core
{
    [CreateAssetMenu(fileName = "GameModePresetLibrary", menuName = "RMC/Backgammon/Game Mode Preset Library")]
    public class GameModePresetLibrarySo : ScriptableObject
    {
        [Tooltip("Ordered list of game modes shown in the New Game modal.")]
        public List<GameModePresetSo> presets = new List<GameModePresetSo>();
    }
}
