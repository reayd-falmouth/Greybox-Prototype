using System;
using System.Collections.Generic;
using Runtime.RMC.Backgammon.Core;
using Runtime.RMC.Backgammon.Stats;
using UnityEngine;

/// <summary>
/// Awards trophies when the player wins a game at a new stake level for the first time.
/// Trophy IDs are scoped to the current currency world (e.g. "trophy_USD_stake_1").
/// Persists all trophy state to PlayerPrefs as a single JSON blob.
/// </summary>
public class TrophyService : MonoBehaviour
{
    const string KeyTrophiesJson = "bg_trophies_json";

    [SerializeField] private BackgammonGameController gameController;
    [SerializeField] private GameModePresetLibrarySo  gameModePresets;

    public event Action<TrophyData> OnTrophyUnlocked;

    private TrophyDataList _trophies;

    private void Awake()
    {
        if (gameModePresets != null)
            PrestigeService.Initialise(gameModePresets);

        LoadTrophiesForCurrentWorld();
        PrestigeService.OnPrestigeChanged += HandlePrestigeChanged;
    }

    private void OnDestroy()
    {
        PrestigeService.OnPrestigeChanged -= HandlePrestigeChanged;
    }

    private void OnEnable()
    {
        if (gameController != null)
            gameController.OnGameEndedWithScore += HandleGameEnded;
    }

    private void OnDisable()
    {
        if (gameController != null)
            gameController.OnGameEndedWithScore -= HandleGameEnded;
    }

    private void HandlePrestigeChanged()
    {
        // Reload trophy list for newly active world
        LoadTrophiesForCurrentWorld();
    }

    private void HandleGameEnded(int winnerIdx, int baseStake, int cubeValue, int gammonMultiplier)
    {
        if (winnerIdx != 0) return;

        var preset = PrestigeService.CurrentPreset;
        if (preset == null) return;

        string id = TrophyIdFor(preset.currencyCode, baseStake);
        TrophyData trophy = _trophies.items.Find(t => t.trophyId == id);
        if (trophy == null || trophy.isUnlocked) return;

        trophy.isUnlocked   = true;
        trophy.dateUnlocked = DateTime.Now.ToString("yyyy-MM-dd");
        SaveTrophies();
        Debug.Log($"[Trophy] Unlocked: {trophy.name} ({preset.currencyCode} ${baseStake})");
        OnTrophyUnlocked?.Invoke(trophy);
    }

    public IReadOnlyList<TrophyData> GetAllTrophies() => _trophies.items;

    public bool AreAllTrophiesUnlocked()
    {
        foreach (var t in _trophies.items)
            if (!t.isUnlocked) return false;
        return _trophies.items.Count > 0;
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private void LoadTrophiesForCurrentWorld()
    {
        // Load the full persistent blob (shared across all worlds)
        string json = PlayerPrefs.GetString(KeyTrophiesJson, "");
        _trophies = (!string.IsNullOrEmpty(json))
            ? JsonUtility.FromJson<TrophyDataList>(json) ?? new TrophyDataList()
            : new TrophyDataList();

        var preset = PrestigeService.CurrentPreset;
        if (preset?.stakes == null) return;

        // Ensure entries exist for every stake in the current preset
        foreach (var level in preset.stakes)
        {
            string id = TrophyIdFor(preset.currencyCode, level.stakeAmount);
            if (_trophies.items.Find(t => t.trophyId == id) == null)
            {
                _trophies.items.Add(new TrophyData
                {
                    trophyId    = id,
                    name        = level.trophyName,
                    description = level.trophyDescription,
                    stakeAmount = level.stakeAmount,
                    isUnlocked  = false,
                    dateUnlocked = ""
                });
            }
        }
        SaveTrophies();
    }

    private void SaveTrophies()
    {
        PlayerPrefs.SetString(KeyTrophiesJson, JsonUtility.ToJson(_trophies));
        PlayerPrefs.Save();
    }

    private static string TrophyIdFor(string currencyCode, int stakeAmount) =>
        $"trophy_{currencyCode}_stake_{stakeAmount}";
}
