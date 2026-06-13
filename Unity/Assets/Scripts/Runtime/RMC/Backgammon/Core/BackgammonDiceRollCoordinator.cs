using Runtime.RMC._MyProject_.Dice;
using UnityEngine;

/// <summary>
/// Owns the buffered human-turn dice roll state and all DiceManager reset operations.
/// Previously these fields and methods were inlined on BackgammonGameController.
/// The coordinator never reads or writes GameState — it only manages DiceManager instances.
/// </summary>
internal class BackgammonDiceRollCoordinator
{
    // ── Buffered die results (opening roll: one die per side) ─────────────────

    private int? _bufferedDie0;
    private int? _bufferedDie1;

    // ── Single-manager turn roll tracking ────────────────────────────────────

    private bool _singleManagerRollInProgress;
    private int _singleManagerRollManagerIndex = -1;

    public bool SingleManagerRollInProgress => _singleManagerRollInProgress;
    public int SingleManagerRollManagerIndex => _singleManagerRollManagerIndex;

    // ── Dependencies (DiceManager refs, feedback callback) ───────────────────

    private DiceManager _dm0;
    private DiceManager _dm1;

    private readonly System.Action<DiceFeedbackEventData> _fireImmediateFeedback;
    private readonly System.Func<int> _getCubeValue;

    public BackgammonDiceRollCoordinator(
        System.Action<DiceFeedbackEventData> fireImmediateFeedback,
        System.Func<int> getCubeValue)
    {
        _fireImmediateFeedback = fireImmediateFeedback;
        _getCubeValue = getCubeValue;
    }

    public void RegisterDiceManagers(DiceManager dm0, DiceManager dm1)
    {
        _dm0 = dm0;
        _dm1 = dm1;
    }

    public bool HasTwoDiceManagers() =>
        _dm0 != null && _dm1 != null && _dm0 != _dm1;

    // ── Opening roll ──────────────────────────────────────────────────────────

    public void RequestOpeningRoll()
    {
        if (!HasTwoDiceManagers()) return;
        _bufferedDie0 = null;
        _bufferedDie1 = null;
        _dm0.SetDiceCount(1);
        _dm1.SetDiceCount(1);
        _dm0.RequestRoll();
        _dm1.RequestRoll();
    }

    public void ResetForOpeningReroll()
    {
        if (!HasTwoDiceManagers()) return;
        _dm0.ResetDiceForOpeningReroll();
        _dm1.ResetDiceForOpeningReroll();
    }

    // ── Normal (single-manager) turn roll ─────────────────────────────────────

    public void RequestNormalRoll(bool isPlayerOnRollVisual)
    {
        if (!HasTwoDiceManagers()) return;
        _bufferedDie0 = null;
        _bufferedDie1 = null;
        int managerIndex = isPlayerOnRollVisual ? 1 : 0;
        BeginSingleManagerRoll(managerIndex);
    }

    private void BeginSingleManagerRoll(int managerIndex)
    {
        DiceManager active = managerIndex == 0 ? _dm0 : _dm1;
        DiceManager inactive = managerIndex == 0 ? _dm1 : _dm0;
        if (active == null || inactive == null) return;
        _singleManagerRollInProgress = true;
        _singleManagerRollManagerIndex = managerIndex;
        active.SetDiceCount(2);
        inactive.ResetDiceToIdleBetweenTurns();
        active.RequestRoll();
        Debug.Log($"[Backgammon][Dice] Single-side roll start managerIndex={managerIndex} manager={active.name}");
    }

    // ── Dice finished callbacks (human opening/normal rolls) ──────────────────

    /// <summary>
    /// Routes a dice-finished event from a DiceManager.
    /// Returns true if the coordinator consumed the event (single-manager or buffered opening roll).
    /// Returns false if the caller should keep routing (AI roll or busy guard).
    /// </summary>
    public bool TryHandleManagerFinished(int managerIndex, int d1, int d2,
        out bool openingBufferComplete, out int bufferedDie0, out int bufferedDie1)
    {
        openingBufferComplete = false;
        bufferedDie0 = 0;
        bufferedDie1 = 0;

        if (_singleManagerRollInProgress)
        {
            if (managerIndex != _singleManagerRollManagerIndex) return true; // swallow wrong-side event
            _singleManagerRollInProgress = false;
            _singleManagerRollManagerIndex = -1;
            // Caller applies ApplyNormalRollFromDice(d1, d2)
            bufferedDie0 = d1;
            bufferedDie1 = d2;
            openingBufferComplete = false;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Buffers an opening-roll die result. Returns true when both dice are ready.
    /// </summary>
    public bool TryBufferOpeningDie(int managerIndex, int d1, out int die0, out int die1)
    {
        int value = Mathf.Clamp(d1, 1, 6);
        if (managerIndex == 0) _bufferedDie0 = value;
        else _bufferedDie1 = value;

        if (_bufferedDie0.HasValue && _bufferedDie1.HasValue)
        {
            die0 = _bufferedDie0.Value;
            die1 = _bufferedDie1.Value;
            _bufferedDie0 = null;
            _bufferedDie1 = null;
            return true;
        }

        die0 = 0;
        die1 = 0;
        return false;
    }

    // ── Reset operations ──────────────────────────────────────────────────────

    public void ResetBetweenTurns(string context, bool emitPickupFeedback)
    {
        if (!HasTwoDiceManagers()) return;
        _dm0.ResetDiceToIdleBetweenTurns();
        _dm1.ResetDiceToIdleBetweenTurns();
        if (emitPickupFeedback)
        {
            _fireImmediateFeedback?.Invoke(new DiceFeedbackEventData(
                DiceFeedbackEventType.GeneralDiceResetPickup,
                _getCubeValue?.Invoke() ?? 0));
            Debug.Log($"[Backgammon][DiceFeedback] Fired immediate reset pickup feedback. context={context}");
        }
        Debug.Log($"[Backgammon][Dice] Both managers reset between turns. context={context}");
    }

    public void SyncAiRollVisualsFromState(int die1, int die2)
    {
        if (!HasTwoDiceManagers()) return;
        _dm0.SetDiceCount(1);
        _dm1.SetDiceCount(1);
        _dm0.ApplySettledDisplayValue(die1);
        _dm1.ApplySettledDisplayValue(die2);
        Debug.Log(
            $"[Backgammon][Dice] AI roll visuals synced d1={die1} d2={die2} managers=({_dm0.name},{_dm1.name})");
    }
}
