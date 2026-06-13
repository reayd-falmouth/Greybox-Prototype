using Runtime.RMC.Backgammon.Stats;
using UnityEngine;

/// <summary>
/// Checks after every trophy unlock whether all trophies in the current
/// currency world are complete, and triggers world graduation if so.
/// </summary>
public class PrestigeCompletionChecker : MonoBehaviour
{
    [SerializeField] private TrophyService trophyService;

    private void OnEnable()
    {
        if (trophyService != null)
            trophyService.OnTrophyUnlocked += HandleTrophyUnlocked;
    }

    private void OnDisable()
    {
        if (trophyService != null)
            trophyService.OnTrophyUnlocked -= HandleTrophyUnlocked;
    }

    private void HandleTrophyUnlocked(TrophyData _)
    {
        if (trophyService.AreAllTrophiesUnlocked())
        {
            Debug.Log("[PrestigeChecker] All trophies earned — graduating world.");
            PrestigeService.CompleteCurrentWorld();
        }
    }
}
