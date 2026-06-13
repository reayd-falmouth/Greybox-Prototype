using System;
using System.Collections.Generic;
using Runtime.RMC.Backgammon.Core;
using Runtime.RMC.Backgammon.Stats;
using UnityEngine;

/// <summary>
/// Observes game-end events, updates the player's money balance,
/// and fires events when new stake tiers become available.
/// Resets unlocked tier tracking when the prestige world changes.
/// </summary>
public class StakeProgressionService : MonoBehaviour
{
    [SerializeField] private BackgammonGameController gameController;

    public event Action<StakeLevelSo> OnStakeTierUnlocked;

    private HashSet<int> _previouslyUnlocked = new();

    private void OnEnable()
    {
        if (gameController != null)
            gameController.OnGameEndedWithScore += HandleGameEnded;

        PrestigeService.OnPrestigeChanged += HandlePrestigeChanged;
        RefreshUnlockedSet(MoneyBalanceService.Balance);
    }

    private void OnDisable()
    {
        if (gameController != null)
            gameController.OnGameEndedWithScore -= HandleGameEnded;

        PrestigeService.OnPrestigeChanged -= HandlePrestigeChanged;
    }

    private void HandlePrestigeChanged()
    {
        // New world — re-seed from fresh balance (already reset by PrestigeService)
        _previouslyUnlocked.Clear();
        RefreshUnlockedSet(MoneyBalanceService.Balance);
        Debug.Log("[Stakes] Prestige world changed — unlocked tiers refreshed.");
    }

    private void HandleGameEnded(int winnerIdx, int baseStake, int cubeValue, int gammonMultiplier)
    {
        bool playerWon = winnerIdx == 0;
        int delta = baseStake * cubeValue * gammonMultiplier;

        if (playerWon)
        {
            MoneyBalanceService.AddWinnings(delta);
            PlayerStats.RecordMoneyResult(true, delta);
        }
        else
        {
            MoneyBalanceService.DeductLoss(delta);
            PlayerStats.RecordMoneyResult(false, delta);
        }

        CheckForNewlyUnlockedTiers(MoneyBalanceService.Balance);
    }

    private void CheckForNewlyUnlockedTiers(int balance)
    {
        var stakes = PrestigeService.CurrentPreset?.stakes;
        if (stakes == null) return;

        foreach (var level in stakes)
        {
            if (level.IsUnlocked(balance) && !_previouslyUnlocked.Contains(level.stakeAmount))
            {
                _previouslyUnlocked.Add(level.stakeAmount);
                Debug.Log($"[Stakes] New tier unlocked: {PrestigeService.CurrentPreset?.currencySymbol}{level.stakeAmount}");
                OnStakeTierUnlocked?.Invoke(level);
            }
        }
    }

    private void RefreshUnlockedSet(int balance)
    {
        _previouslyUnlocked.Clear();
        var stakes = PrestigeService.CurrentPreset?.stakes;
        if (stakes == null) return;

        foreach (var level in stakes)
        {
            if (level.IsUnlocked(balance))
                _previouslyUnlocked.Add(level.stakeAmount);
        }
    }

    public IReadOnlyList<StakeLevelSo> GetAvailableStakes()
    {
        var preset = PrestigeService.CurrentPreset;
        return preset != null
            ? preset.GetUnlockedStakes(MoneyBalanceService.Balance)
            : new List<StakeLevelSo>();
    }

    public IReadOnlyList<StakeLevelSo> GetAllStakes()
    {
        var stakes = PrestigeService.CurrentPreset?.stakes;
        return stakes ?? new List<StakeLevelSo>();
    }
}
