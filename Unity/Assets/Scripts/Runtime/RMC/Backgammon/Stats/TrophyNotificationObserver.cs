using System.Collections;
using Runtime.RMC.Backgammon.Core;
using Runtime.RMC.Backgammon.Stats;
using UnityEngine;

/// <summary>
/// Listens for trophy and stake-unlock events and shows toast notifications
/// after a short delay to avoid colliding with the game-end notification.
/// </summary>
public class TrophyNotificationObserver : MonoBehaviour
{
    [SerializeField] private TrophyService            trophyService;
    [SerializeField] private StakeProgressionService  stakeProgressionService;
    [SerializeField] private ScreenNotificationController notificationController;

    [Tooltip("Seconds to wait after a game ends before showing the trophy/stake toast.")]
    [SerializeField] private float delayAfterGameEnd = 2.5f;

    private void OnEnable()
    {
        if (trophyService != null)
            trophyService.OnTrophyUnlocked += HandleTrophyUnlocked;
        if (stakeProgressionService != null)
            stakeProgressionService.OnStakeTierUnlocked += HandleStakeTierUnlocked;
    }

    private void OnDisable()
    {
        if (trophyService != null)
            trophyService.OnTrophyUnlocked -= HandleTrophyUnlocked;
        if (stakeProgressionService != null)
            stakeProgressionService.OnStakeTierUnlocked -= HandleStakeTierUnlocked;
    }

    private void HandleTrophyUnlocked(TrophyData trophy)
    {
        string msg = $"Trophy: {trophy.name}!";
        StartCoroutine(ShowDelayed(msg));
        Debug.Log($"[TrophyObserver] Queued trophy toast: {msg}");
    }

    private void HandleStakeTierUnlocked(StakeLevelSo level)
    {
        string msg = $"Unlocked ${level.stakeAmount} stakes!";
        StartCoroutine(ShowDelayed(msg));
        Debug.Log($"[TrophyObserver] Queued stake toast: {msg}");
    }

    private IEnumerator ShowDelayed(string message)
    {
        yield return new WaitForSeconds(delayAfterGameEnd);
        if (notificationController != null)
            notificationController.ShowCustomNotification(message);
    }
}
