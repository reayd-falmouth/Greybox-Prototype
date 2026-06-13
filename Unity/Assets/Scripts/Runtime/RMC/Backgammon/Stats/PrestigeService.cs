using System;
using System.Collections.Generic;
using Runtime.RMC.Backgammon.Core;
using UnityEngine;

namespace Runtime.RMC.Backgammon.Stats
{
    /// <summary>
    /// Tracks which game mode preset (currency act) is active and which are completed.
    /// Must be initialised via PrestigeService.Initialise(library) before use.
    /// </summary>
    public static class PrestigeService
    {
        const string KeyCurrentWorld  = "bg_prestige_current_world";
        const string KeyCompletedJson = "bg_prestige_completed_json";

        public static event Action<GameModePresetSo> OnWorldGraduated;
        public static event Action                   OnPrestigeChanged;

        private static GameModePresetLibrarySo _library;

        public static void Initialise(GameModePresetLibrarySo library)
        {
            _library = library;
        }

        public static int CurrentWorldIndex
        {
            get => PlayerPrefs.GetInt(KeyCurrentWorld, 0);
            private set
            {
                PlayerPrefs.SetInt(KeyCurrentWorld, value);
                PlayerPrefs.Save();
            }
        }

        public static GameModePresetSo CurrentPreset =>
            (_library != null && _library.presets?.Count > 0)
                ? _library.presets[Mathf.Clamp(CurrentWorldIndex, 0, _library.presets.Count - 1)]
                : null;

        public static bool IsWorldCompleted(string currencyCode)
        {
            var list = LoadCompletedList();
            return list.completedCodes.Contains(currencyCode);
        }

        /// <summary>
        /// Called when all trophies in the current preset are earned.
        /// Archives the preset, advances to next, and resets balance.
        /// </summary>
        public static void CompleteCurrentWorld()
        {
            if (_library == null) return;

            var current = CurrentPreset;
            if (current == null) return;

            // Archive
            var list = LoadCompletedList();
            if (!list.completedCodes.Contains(current.currencyCode))
            {
                list.completedCodes.Add(current.currencyCode);
                SaveCompletedList(list);
            }

            // Advance
            int nextIndex = Mathf.Min(CurrentWorldIndex + 1, _library.presets.Count - 1);
            CurrentWorldIndex = nextIndex;

            var nextPreset = _library.presets[nextIndex];

            // Reset balance to new preset's starting amount
            MoneyBalanceService.ResetToDefault(nextPreset?.startingBalance ?? 100);

            Debug.Log($"[Prestige] Graduated from {current.currencyCode} → {nextPreset?.currencyCode}");
            OnWorldGraduated?.Invoke(nextPreset);
            OnPrestigeChanged?.Invoke();
        }

        /// <summary>Dev tool: unlock all presets and advance to the final one.</summary>
        public static void UnlockAll()
        {
            if (_library == null) return;

            var list = new PrestigeCompletedList();
            foreach (var preset in _library.presets)
                list.completedCodes.Add(preset.currencyCode);

            SaveCompletedList(list);
            CurrentWorldIndex = _library.presets.Count - 1;

            var finalPreset = CurrentPreset;
            MoneyBalanceService.ResetToDefault(finalPreset?.startingBalance ?? 100);

            Debug.Log("[Prestige] UnlockAll: all presets marked complete.");
            OnPrestigeChanged?.Invoke();
        }

        public static IReadOnlyList<string> GetCompletedCodes() =>
            LoadCompletedList().completedCodes;

        private static PrestigeCompletedList LoadCompletedList()
        {
            string json = PlayerPrefs.GetString(KeyCompletedJson, "");
            if (!string.IsNullOrEmpty(json))
            {
                var result = JsonUtility.FromJson<PrestigeCompletedList>(json);
                if (result != null) return result;
            }
            return new PrestigeCompletedList();
        }

        private static void SaveCompletedList(PrestigeCompletedList list)
        {
            PlayerPrefs.SetString(KeyCompletedJson, JsonUtility.ToJson(list));
            PlayerPrefs.Save();
        }
    }

    [Serializable]
    public class PrestigeCompletedList
    {
        public List<string> completedCodes = new();
    }
}
