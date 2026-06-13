using Runtime.RMC.Backgammon.Stats;
using UnityEngine;

/// <summary>Observes BackgammonGameController events to maintain per-session statistics.</summary>
public class SessionStatsTracker : MonoBehaviour
{
    [SerializeField] private BackgammonGameController gameController;

    private readonly SessionStats _session = new SessionStats();
    private int _lastGamesPlayed;
    private int _lastGammonMultiplier;

    public SessionStats CurrentSession => _session;

    private void OnEnable()
    {
        if (gameController == null) return;
        gameController.OnDiceRolled          += HandleDiceRolled;
        gameController.OnNewSessionStarted   += HandleNewSessionStarted;
        gameController.OnStateChanged        += HandleStateChanged;
        gameController.OnGameEndedWithScore  += HandleGameEndedWithScore;
    }

    private void OnDisable()
    {
        if (gameController == null) return;
        gameController.OnDiceRolled          -= HandleDiceRolled;
        gameController.OnNewSessionStarted   -= HandleNewSessionStarted;
        gameController.OnStateChanged        -= HandleStateChanged;
        gameController.OnGameEndedWithScore  -= HandleGameEndedWithScore;
    }

    private void HandleDiceRolled(int d1, int d2)
    {
        _session.RecordDiceRoll(d1, d2);
        Debug.Log($"[Stats][Session] DiceRolled d1={d1} d2={d2} totalRolls={_session.TotalRollsThisSession}");
    }

    private void HandleNewSessionStarted()
    {
        PlayerStats.RecordSessionStart();
        _session.Reset();
        _lastGamesPlayed = 0;
        _lastGammonMultiplier = 1;
        Debug.Log("[Stats][Session] New session started — stats reset.");
    }

    // Capture the gammon multiplier from the game-end event before HandleStateChanged fires.
    private void HandleGameEndedWithScore(int winnerIdx, int baseStake, int cubeValue, int gammonMultiplier)
    {
        _lastGammonMultiplier = gammonMultiplier;
    }

    private void HandleStateChanged()
    {
        if (gameController == null) return;
        int current = gameController.MoneySessionGamesPlayed;
        if (current <= _lastGamesPlayed) return;

        bool playerWon   = gameController.MoneySessionPlayer1Score > gameController.MoneySessionPlayer2Score;
        bool isGammon    = _lastGammonMultiplier >= 2;
        bool isBackgammon = _lastGammonMultiplier >= 3;

        _session.RecordGameEnd(playerWon, gameController.MoneySessionPlayer1Score, gameController.MoneySessionBankBalance);
        PlayerStats.RecordGameEnd(
            playerWon: playerWon,
            playerScore: gameController.MoneySessionPlayer1Score,
            bankBalance: gameController.MoneySessionBankBalance,
            isGammon: isGammon,
            isBackgammon: isBackgammon);

        _lastGamesPlayed = current;
        _lastGammonMultiplier = 1;
        Debug.Log($"[Stats][Session] GameEnd detected: games={_session.GamesPlayedThisSession} score={_session.SessionScore}");
    }
}
