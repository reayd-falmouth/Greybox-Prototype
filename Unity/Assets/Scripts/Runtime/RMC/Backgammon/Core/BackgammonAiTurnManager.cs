using Runtime.RMC._MyProject_.Dice;
using Runtime.RMC.Backgammon.Settings;
using Stopwatch = System.Diagnostics.Stopwatch;
using UnityEngine;

/// <summary>
/// Manages AI-turn dice rolling and pacing delays.
/// Owns the buffered physical-roll state that previously lived as fields on BackgammonGameController.
/// Timing log helpers live here so tests can verify them without a full MonoBehaviour setup.
/// </summary>
internal class BackgammonAiTurnManager
{
    // ── AI roll state ─────────────────────────────────────────────────────────

    private bool _rollInProgress;
    private int _rollToken;
    private int _activeRollToken;
    private int _activeRollManagerIndex = -1;
    private int? _bufferedDie0;
    private int? _bufferedDie1;

    public bool RollInProgress => _rollInProgress;
    public int ActiveRollToken => _activeRollToken;
    public int? BufferedDie0 => _bufferedDie0;
    public int? BufferedDie1 => _bufferedDie1;

    private DiceManager _dm0;
    private DiceManager _dm1;

    // Debug flags injected at construction

    private readonly bool _enableTimingLogs;

    public BackgammonAiTurnManager(bool enableTimingLogs)
    {
        _enableTimingLogs = enableTimingLogs;
    }

    // ── Dice manager wiring ───────────────────────────────────────────────────

    public void RegisterDiceCallbacks(DiceManager dm0, DiceManager dm1)
    {
        _dm0 = dm0;
        _dm1 = dm1;
    }

    // ── Physical roll lifecycle ───────────────────────────────────────────────

    public void BeginPhysicalRoll(bool openingRollResolved, bool isPlayerOnRollVisual)
    {
        if (_dm0 == null || _dm1 == null) return;
        _rollToken++;
        _activeRollToken = _rollToken;
        _rollInProgress = true;
        _activeRollManagerIndex = -1;
        _bufferedDie0 = null;
        _bufferedDie1 = null;

        if (openingRollResolved)
        {
            int managerIndex = isPlayerOnRollVisual ? 1 : 0;
            _activeRollManagerIndex = managerIndex;
            DiceManager active = managerIndex == 0 ? _dm0 : _dm1;
            DiceManager inactive = managerIndex == 0 ? _dm1 : _dm0;
            if (active == null || inactive == null) { _rollInProgress = false; return; }
            active.SetDiceCount(2);
            inactive.ResetDiceToIdleBetweenTurns();
            Debug.Log($"[Backgammon][AI][Dice] Roll start token={_activeRollToken} mode=single managerIndex={managerIndex} manager={active.name}");
            active.RequestRoll();
            return;
        }

        _dm0.SetDiceCount(1);
        _dm1.SetDiceCount(1);
        Debug.Log($"[Backgammon][AI][Dice] Roll start token={_activeRollToken} mode=opening managers=({_dm0.name},{_dm1.name})");
        _dm0.RequestRoll();
        _dm1.RequestRoll();
    }

    public void HandleDiceManagerFinished(int managerIndex, int d1, int d2)
    {
        if (!_rollInProgress) return;
        if (_activeRollManagerIndex >= 0)
        {
            if (managerIndex != _activeRollManagerIndex) return;
            _bufferedDie0 = Mathf.Clamp(d1, 1, 6);
            _bufferedDie1 = Mathf.Clamp(d2, 1, 6);
            Debug.Log($"[Backgammon][AI][Dice] Dice finished token={_activeRollToken} manager={managerIndex} d1={_bufferedDie0} d2={_bufferedDie1}");
            TryCompleteRollIfReady();
            return;
        }

        int clamped = Mathf.Clamp(d1, 1, 6);
        if (managerIndex == 0) _bufferedDie0 = clamped;
        else _bufferedDie1 = clamped;
        Debug.Log($"[Backgammon][AI][Dice] Opening die finished token={_activeRollToken} manager={managerIndex} value={clamped} buffered=({_bufferedDie0?.ToString() ?? "-"}, {_bufferedDie1?.ToString() ?? "-"})");
        TryCompleteRollIfReady();
    }

    public void ForceRollTimeout()
    {
        _rollInProgress = false;
    }

    public void ConsumeBufferedRoll()
    {
        _activeRollManagerIndex = -1;
        _bufferedDie0 = null;
        _bufferedDie1 = null;
    }

    private void TryCompleteRollIfReady()
    {
        if (!_rollInProgress || !_bufferedDie0.HasValue || !_bufferedDie1.HasValue) return;
        _rollInProgress = false;
        Debug.Log($"[Backgammon][AI][Dice] Roll complete token={_activeRollToken} d1={_bufferedDie0.Value} d2={_bufferedDie1.Value}");
    }

    // ── Pacing helpers ────────────────────────────────────────────────────────

    public static float GetPacingBaseSeconds()
    {
        return Mathf.Clamp(BackgammonSettings.GameSpeedSecondsPerStep, 0.05f, 2f);
    }

    public static float GetPreRollDelaySeconds()
    {
        return Mathf.Clamp(GetPacingBaseSeconds() * 1.0f, 0.05f, 2.5f);
    }

    public static float GetPostRollRevealDelaySeconds()
    {
        return Mathf.Clamp(GetPacingBaseSeconds() * 0.8f, 0.05f, 2.0f);
    }

    public static float GetPostApplyDelaySeconds()
    {
        return Mathf.Clamp(GetPacingBaseSeconds() * 0.6f, 0.05f, 1.5f);
    }

    public static float GetBetweenMovesDelaySeconds()
    {
        return Mathf.Clamp(GetPacingBaseSeconds() * 0.5f, 0.03f, 1.0f);
    }

    // ── Timing log helpers ────────────────────────────────────────────────────

    public static string BuildAiTimingLogLine(string phase, double elapsedMs, string extra)
    {
        string suffix = string.IsNullOrWhiteSpace(extra) ? string.Empty : $" {extra}";
        return $"[Backgammon][AI][Timing] phase={phase} ms={elapsedMs:F1}{suffix}";
    }

    public void LogAiTiming(string phase, double elapsedMs, string extra)
    {
        if (!_enableTimingLogs) return;
        Debug.Log(BuildAiTimingLogLine(phase, elapsedMs, extra));
    }

    public Stopwatch StartTimingStopwatch() => _enableTimingLogs ? Stopwatch.StartNew() : null;
}
