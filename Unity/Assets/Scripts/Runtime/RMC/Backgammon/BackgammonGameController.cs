using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;
using Stopwatch = System.Diagnostics.Stopwatch;
using EngineCore;
using Runtime.RMC._MyProject_.Core;
using Runtime.RMC._MyProject_.Dice;
using Runtime.RMC.Backgammon.Core;
using Runtime.RMC.Backgammon.Settings;
using Runtime.RMC.Backgammon.Stats;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Serialization;

/// <summary>
/// Owns <see cref="GameState"/>, dice → legals → apply → swap (EngineCLI convention).
/// </summary>
[DefaultExecutionOrder(-40)]
public class BackgammonGameController : MonoBehaviour
{
    private enum GameEndReason
    {
        BearOff = 0,
        DoubleDrop = 1
    }

    private enum GameEndScoreKind
    {
        Single = 1,
        Gammon = 2,
        Backgammon = 3
    }

    [Header("Debug")]
    [SerializeField] private bool enableMoveSelectionDebugLogs = true;
    [SerializeField] private bool enableDiceFeedbackDebugLogs;
    [SerializeField] private bool enableAiTimingLogs;
    [SerializeField] private bool enableAiCacheDebugLogs;
    [SerializeField] private BackgammonAiMoveCacheStorageMode aiMoveCacheStorageMode = BackgammonAiMoveCacheStorageMode.Binary;
    [SerializeField] private bool enableAiCubeDecisionDebugLogs = true;
    [SerializeField] private bool enableEventQueueDebugLogs;
    [SerializeField] private bool enableUndoPerformanceLogs;
    [SerializeField] private bool enableBearOffDebugLogs = true;

    [SerializeField] private BoardManager boardManager;
    [Header("Dice (required)")]
    [Tooltip("One die per manager. Opening: P0 vs P1. Later turns: both roll for the current player’s two values.")]
    [FormerlySerializedAs("diceManagerOpeningPlayer0")]
    [SerializeField] private DiceManager diceManagerPlayer0;
    [FormerlySerializedAs("diceManagerOpeningPlayer1")]
    [SerializeField] private DiceManager diceManagerPlayer1;
    [SerializeField] private BackgammonHudController hud;
    [Tooltip("When true, DiceManager does not bind its own roll button; use HUD Roll.")]
    [SerializeField] private bool rollDiceViaHudOnly = true;

    public GameState State { get; private set; }
    public MatchState Match { get; private set; }

    public IReadOnlyList<Turn> CurrentLegalTurns => _legalTurns;

    /// <summary>True after dice are set for the current turn until a move is applied or turn passes.</summary>
    public bool HasRolledThisTurn => _rolledThisTurn;

    public bool IsBusy => _busy;

    public bool CanUndo => !_busy && _undoStack.Count > 0;
    public bool CanFinalizeCurrentTurn => !_busy && _rolledThisTurn && _legalTurns.Count == 0 && !IsGameOver(out _);

    public int RollsThisGame { get; private set; }
    // Only counts rolls made by the human player (LocalPlayerIndex). Used by Run Mode HUD.
    public int PlayerRollsThisGame { get; private set; }

    /// <summary>Increments each <see cref="NewGame"/> (session counter).</summary>
    public int GameRoundIndex { get; private set; }

    /// <summary>Completed turn boundaries this game (move applied or pass).</summary>
    public int TurnsCompletedThisGame { get; private set; }

    /// <summary>Higher of the two players’ match scores (for Run mode uses progression tracker, otherwise uses State).</summary>
    public int CurrentMatchScore
    {
        get
        {
            if (_currentGameMode == GameModeType.Run)
                return Mathf.Max(_player1MatchScore, _player2MatchScore);
            return State != null ? Mathf.Max(State.Player1Score, State.Player2Score) : 0;
        }
    }

    /// <summary>Target score for the current match (mode-dependent).</summary>
    public int CurrentMatchTargetScore => _matchTargetScore;

    /// <summary>Base stake for the current match (from ante progression config).</summary>
    public int CurrentMatchBaseStake
    {
        get
        {
            if (_anteProgression == null || _anteProgression.Count == 0)
                return 100; // Fallback default
            if (_currentAnteIndex >= _anteProgression.Count)
                return 100;
            var ante = _anteProgression[_currentAnteIndex];
            if (_currentMatchIndex >= ante.Length)
                return 100;
            return ante[_currentMatchIndex];
        }
    }

    /// <summary>Maximum games per match (3 for most modes).</summary>
    public int CurrentMatchMaxGames => 3;

    /// <summary>Games completed in the current match.</summary>
    public int CurrentMatchGamesPlayed => _gamesPlayedInCurrentMatch;

    /// <summary>Total run currency accumulated.</summary>
    public int RunCurrency => _runCurrency;

    /// <summary>Current ante number (1-based) for display.</summary>
    public int CurrentAnteNumber => _currentAnteIndex + 1;

    /// <summary>Current match number within ante (1-based): 1=small blind, 2=big blind, 3=boss.</summary>
    public int CurrentMatchNumber => _currentMatchIndex + 1;

    /// <summary>Total number of antes in this run.</summary>
    public int TotalAntes => _anteProgression?.Count ?? 1;

    /// <summary>Total matches completed in this run (across all antes).</summary>
    public int MatchesPlayedInRun => (_currentAnteIndex * 3) + _currentMatchIndex;

    public bool AwaitingDoubleResponse => _cubeNegotiator?.AwaitingDoubleResponse ?? false;
    public bool PlayerOnRollVisual => _isPlayerOnRollVisual;
    public bool IsAwaitingNextGameFromPopup => _gameEndedAwaitingNextGame;
    public string LastGameOverSummary => _lastGameOverSummary;

    /// <summary>False until the first opening roll is resolved (non-tie). After that, normal turns use two dice together.</summary>
    public bool OpeningRollResolved => _openingRollResolved;

    /// <summary>Last opening roll was a tie; player should roll again.</summary>
    public bool OpeningRollAwaitingReroll => !_openingRollResolved && _openingRollTieAwaitingReroll;

    public event Action OnStateChanged;
    public event Action<CheckerSoundEventData> OnCheckerSoundEvent;
    public event Action<DiceFeedbackEventData> OnDiceFeedbackEvent;
    public event Action<DiceFeedbackEventData> OnScreenNotificationEvent;
    public event Action<int> OnCubeRotatedMarker;
    public event Action<int, int> OnDiceRolled;
    public event Action OnNewSessionStarted;
    // Fired once per game end; consumed by BackgammonRunModeManager to accumulate Run Mode scores.
    public event Action<int, int, int, int> OnGameEndedWithScore;

    /// <summary>Opening / player 0 side dice manager (one die each during opening).</summary>
    public DiceManager DiceManagerPlayer0 => diceManagerPlayer0;

    /// <summary>Player 1 side dice manager.</summary>
    public DiceManager DiceManagerPlayer1 => diceManagerPlayer1;

    private readonly List<Turn> _legalTurns = new();
    private readonly HashSet<int> _movableFromScratch = new();
    private readonly HashSet<int> _lastMovableFromPoints = new();
    private readonly Stack<UndoFrame> _undoStack = new();
    private bool _rolledThisTurn;
    private bool _busy;
    private bool _cubeDisabledForSession;
    private bool _doubletsDisabledForSession;
    // Doubling cube negotiation state is managed by BackgammonDoublingCubeNegotiator
    private bool _openingRollResolved;
    private bool _openingRollTieAwaitingReroll;
    private bool _hasLastMovableHighlightState;
    private bool _lastMovableHighlightsVisible;
    private bool _forceMovableHighlightRebuild;
    private int _undoVisualSuccessCount;
    private int _undoFallbackSyncCount;
    private int _undoFallbackNoAppliedMoveCount;
    private static readonly ProfilerMarker UndoTryMarker = new("Backgammon.TryUndoLastMove");
    private static readonly ProfilerMarker UndoRestoreMarker = new("Backgammon.RestoreUndoFrame");
    private static readonly ProfilerMarker UndoRefreshLegalsMarker = new("Backgammon.UndoRefreshLegals");
    private static readonly ProfilerMarker UndoHudRefreshMarker = new("Backgammon.UndoHudRefresh");
    private static readonly ProfilerMarker UndoPushFrameMarker = new("Backgammon.PushUndoFrame");
    private static readonly ProfilerMarker UndoCaptureFrameMarker = new("Backgammon.UndoFrameCapture");
    // AI cache state is managed by BackgammonAiMoveCache (static class)

    // Ante progression system fields
    private List<int[]> _anteProgression;
    private int _currentAnteIndex;
    private int _currentMatchIndex;
    private int _player1MatchScore;
    private int _player2MatchScore;
    private int _runCurrency;
    private bool _runComplete;
    private int _gamesPlayedInCurrentMatch;
    private int _matchTargetScore;
    private bool _shouldLoopAntes;
    private GameModeType _currentGameMode;
    private MoneySessionConfig _moneySessionConfig;

    // Money Session score tracking
    private int _moneySessionPlayer1Score;
    private int _moneySessionPlayer2Score;
    private int _moneySessionGamesPlayed;
    private int _moneySessionBankBalance;

    public GameModeType CurrentGameMode => _currentGameMode;
    public int MoneySessionPlayer1Score => _moneySessionPlayer1Score;
    public int MoneySessionPlayer2Score => _moneySessionPlayer2Score;
    public int MoneySessionGamesPlayed => _moneySessionGamesPlayed;
    public int MoneySessionBankBalance => _moneySessionBankBalance;
    public int MoneySessionBaseStake => _moneySessionConfig?.BaseStake ?? 0;

    /// <summary>Human (local player) pip count. Uses the visual-normalised state so Player1=human always.</summary>
    public int CalculatePipCountPlayer1()
    {
        GameState visual = BuildVisualStateSnapshot();
        if (visual?.Player1Checkers == null) return 0;
        return ComputePipCount(visual.Player1Checkers);
    }

    /// <summary>AI (opponent) pip count. Uses the visual-normalised state so Player2=AI always.</summary>
    public int CalculatePipCountPlayer2()
    {
        GameState visual = BuildVisualStateSnapshot();
        if (visual?.Player2Checkers == null) return 0;
        return ComputePipCount(visual.Player2Checkers);
    }

    private static int ComputePipCount(int[] checkers)
    {
        int pips = 0;
        for (int i = 0; i < 24 && i < checkers.Length; i++)
            pips += checkers[i] * (i + 1);
        if (checkers.Length > 24)
            pips += checkers[24] * 25;
        return pips;
    }
    private static readonly Func<SearchEngine, GameState, MatchState, int, Task<Turn>> AiSearchTaskFactory =
        (engine, state, match, depth) => Task.Run(() => engine.GetBestTurn(state, match, depth));
    private bool _isPlayerOnRollVisual;
    private BackgammonAiTurnManager _aiTurnManager;
    private BackgammonDiceRollCoordinator _diceRollCoordinator;
    private BackgammonDoublingCubeNegotiator _cubeNegotiator;
    private BackgammonEventQueue _presentationEventQueue;
    private bool _presentationQueueDrivenByCoroutine;
    private bool _forcedGameOver;
    private int _forcedWinnerPlayerIndex = -1;
    private bool _gameEndSequenceQueued;
    private bool _gameEndedAwaitingNextGame;
    private string _lastGameOverSummary;

    private void Awake()
    {
        BackgammonAiMoveCache.Configure(aiMoveCacheStorageMode);
        BackgammonAiMoveCache.EnsureLoadedFromDisk();
        _aiTurnManager = new BackgammonAiTurnManager(enableAiTimingLogs);
        _diceRollCoordinator = new BackgammonDiceRollCoordinator(
            FireDiceFeedbackEventImmediate,
            () => State != null ? State.CubeValue : 0);
        _cubeNegotiator = new BackgammonDoublingCubeNegotiator();
        _presentationEventQueue = new BackgammonEventQueue(enableEventQueueDebugLogs);
        Match = new MatchState
        {
            MatchLength = 0,
            JacobyRule = true,
            BeaversAllowed = true
        };
    }

    private void Start()
    {
        if (hud == null)
            hud = FindFirstObjectByType<BackgammonHudController>();
        TryAutoAssignDiceManagersFromBoard();
        if (rollDiceViaHudOnly)
        {
            if (diceManagerPlayer0 != null) diceManagerPlayer0.SuppressRollButtonBinding = true;
            if (diceManagerPlayer1 != null) diceManagerPlayer1.SuppressRollButtonBinding = true;
        }
        if (diceManagerPlayer0 != null)
            diceManagerPlayer0.OnDiceRollFinished += OnDiceManagerPlayer0Finished;
        if (diceManagerPlayer1 != null)
            diceManagerPlayer1.OnDiceRollFinished += OnDiceManagerPlayer1Finished;
        _aiTurnManager.RegisterDiceCallbacks(diceManagerPlayer0, diceManagerPlayer1);
        _diceRollCoordinator.RegisterDiceManagers(diceManagerPlayer0, diceManagerPlayer1);
        if (!HasTwoDiceManagers())
            Debug.LogError("BackgammonGameController: assign Dice Manager Player 0 and Player 1 (two DiceManager instances).");
        SetBoardViewHorizontal(BackgammonSettings.BoardViewIsHorizontal);
        NewGame();
    }

    private void Update()
    {
        if (_presentationEventQueue == null)
            return;
        if (_presentationQueueDrivenByCoroutine)
            return;

        _presentationEventQueue.SetGameSpeedMultiplier(GetPresentationSpeedMultiplier());
        _presentationEventQueue.Tick(Time.unscaledDeltaTime);
    }

    private void OnDestroy()
    {
        if (diceManagerPlayer0 != null)
            diceManagerPlayer0.OnDiceRollFinished -= OnDiceManagerPlayer0Finished;
        if (diceManagerPlayer1 != null)
            diceManagerPlayer1.OnDiceRollFinished -= OnDiceManagerPlayer1Finished;
    }

    private string _pendingStartPositionId;

    public void NewGame()
    {
        string posId = string.IsNullOrEmpty(_pendingStartPositionId)
            ? "4HPwATDgc/ABMA"
            : _pendingStartPositionId;
        _pendingStartPositionId = null;
        StartNewGameFromPositionId(posId, "new-game-default");
    }

    // ── Run Mode session controls ─────────────────────────────────────────────

    public void SetCubeDisabled(bool disabled)
    {
        _cubeDisabledForSession = disabled;
        Debug.Log($"[Backgammon][Run] Cube disabled: {disabled}");
    }

    public void SetDoubletsDisabled(bool disabled)
    {
        _doubletsDisabledForSession = disabled;
        Debug.Log($"[Backgammon][Run] Doublets disabled: {disabled}");
    }

    public void StartRunSession(RunConfig cfg, RunState state)
    {
        _currentGameMode = GameModeType.Run;
        _runCurrency = state.TotalCurrency;

        var session = state.CurrentSession(cfg);
        int baseStake = 1; // Threshold drives the goal; base stake stays at 1 for run mode
        if (_moneySessionConfig == null)
            _moneySessionConfig = new MoneySessionConfig { BaseStake = baseStake };
        else
            _moneySessionConfig.BaseStake = baseStake;

        // Apply boss variant constraints
        SetCubeDisabled(state.ActiveBossVariant == BossVariantType.NoCube);
        SetDoubletsDisabled(state.ActiveBossVariant == BossVariantType.NoDoublets);

        Debug.Log($"[Backgammon][Run] StartRunSession A{state.CurrentAnteIndex + 1} S{state.CurrentSessionIndex + 1} boss={state.ActiveBossVariant} threshold={session.ScoreThreshold}");
        NewGame();
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts a new game with a specific game mode, initializing ante progression for Run mode.
    /// </summary>
    public void StartNewGameWithMode(GameModeType mode)
    {
        _currentGameMode = mode;

        // Initialize ante progression and match state based on game mode
        switch (mode)
        {
            case GameModeType.Run:
                // Default Run mode configuration: 3 antes with doubling pattern
                _anteProgression = new List<int[]>
                {
                    new[] { 5, 10, 20 },      // Ante 1: Small=5, Big=10, Boss=20
                    new[] { 10, 20, 40 },     // Ante 2: Small=10, Big=20, Boss=40
                    new[] { 20, 40, 80 },     // Ante 3: Small=20, Big=40, Boss=80
                };
                _matchTargetScore = 5; // Fast matches by default
                _shouldLoopAntes = false;
                _currentAnteIndex = 0;
                _currentMatchIndex = 0;
                _player1MatchScore = 0;
                _player2MatchScore = 0;
                _runCurrency = 0;
                _runComplete = false;
                _gamesPlayedInCurrentMatch = 0;
                Debug.Log("[Backgammon][Run] Initialized Run mode: 3 antes, target score 5");
                break;

            case GameModeType.MatchPlay:
                _matchTargetScore = 1000; // TODO: Make configurable
                _anteProgression = null; // No ante progression for Match Play
                Debug.Log("[Backgammon][MatchPlay] Initialized Match Play mode");
                break;

            case GameModeType.MoneySession:
            default:
                _matchTargetScore = int.MaxValue; // No target for Money Session
                _anteProgression = null;
                Debug.Log("[Backgammon][MoneySession] Initialized Money Session mode");
                break;
        }

        // Start the actual game
        NewGame();
    }

    /// <summary>
    /// Starts a new game with a specific game mode and optional Money Session configuration.
    /// </summary>
    public void StartNewGameWithConfig(GameModeType mode, MoneySessionConfig config,
        string startingPositionId = null, string seedString = null)
    {
        if (!string.IsNullOrEmpty(seedString) && DeterministicRNG.Instance != null)
            DeterministicRNG.Instance.SetMasterSeed(seedString);
        _pendingStartPositionId = startingPositionId;

        _currentGameMode = mode;

        // Store configuration for Money Session mode
        if (mode == GameModeType.MoneySession && config != null)
        {
            _moneySessionConfig = config;
            Match.JacobyRule = config.JacobyRule;
            Match.BeaversAllowed = config.BeaversAllowed;
            // Reset session tracking
            _moneySessionPlayer1Score = 0;
            _moneySessionPlayer2Score = 0;
            _moneySessionGamesPlayed = 0;
            _moneySessionBankBalance = 0;
            OnNewSessionStarted?.Invoke();
            // Raccoons and Ardvarks will be used by cube evaluator in future
            Debug.Log($"[Backgammon][MoneySession] Config applied: BaseStake={config.BaseStake}, Jacoby={config.JacobyRule}, Beavers={config.BeaversAllowed}, Raccoons={config.RaccoonsAllowed}, Ardvarks={config.ArdvarksAllowed}");
        }

        // Initialize ante progression and match state based on game mode
        switch (mode)
        {
            case GameModeType.Run:
                _anteProgression = new List<int[]>
                {
                    new[] { 5, 10, 20 },
                    new[] { 10, 20, 40 },
                    new[] { 20, 40, 80 },
                };
                _matchTargetScore = 5;
                _shouldLoopAntes = false;
                _currentAnteIndex = 0;
                _currentMatchIndex = 0;
                _player1MatchScore = 0;
                _player2MatchScore = 0;
                _runCurrency = 0;
                _runComplete = false;
                _gamesPlayedInCurrentMatch = 0;
                Debug.Log("[Backgammon][Run] Initialized Run mode");
                break;

            case GameModeType.MatchPlay:
                _matchTargetScore = 1000;
                _anteProgression = null;
                Debug.Log("[Backgammon][MatchPlay] Initialized Match Play mode");
                break;

            case GameModeType.MoneySession:
            default:
                _matchTargetScore = int.MaxValue;
                _anteProgression = null;
                Debug.Log("[Backgammon][MoneySession] Initialized Money Session mode");
                break;
        }

        NewGame();
    }

    public SavedGameData BuildSaveData()
    {
        if (State == null) return null;
        return new SavedGameData
        {
            positionId               = PositionId.Encode(State),
            gameModeType             = (int)_currentGameMode,
            moneySessionConfig       = _moneySessionConfig,
            moneySessionPlayer1Score = _moneySessionPlayer1Score,
            moneySessionPlayer2Score = _moneySessionPlayer2Score,
            moneySessionGamesPlayed  = _moneySessionGamesPlayed,
            moneySessionBankBalance  = _moneySessionBankBalance,
            player1MatchScore        = _player1MatchScore,
            player2MatchScore        = _player2MatchScore,
            matchTargetScore         = _matchTargetScore,
        };
    }

    public void RestoreFromSave(SavedGameData data)
    {
        if (data == null) return;
        var mode = (GameModeType)data.gameModeType;
        // Set up mode and config (resets scores internally, we restore them after)
        StartNewGameWithConfig(mode, data.moneySessionConfig, data.positionId);
        // Restore session/match scores that were reset by StartNewGameWithConfig
        _moneySessionPlayer1Score = data.moneySessionPlayer1Score;
        _moneySessionPlayer2Score = data.moneySessionPlayer2Score;
        _moneySessionGamesPlayed  = data.moneySessionGamesPlayed;
        _moneySessionBankBalance  = data.moneySessionBankBalance;
        _player1MatchScore        = data.player1MatchScore;
        _player2MatchScore        = data.player2MatchScore;
        _matchTargetScore         = data.matchTargetScore;
        hud?.RefreshAll(this);
        Debug.Log($"[SavedGame] Restored mode={mode} pid={data.positionId}");
    }

    public bool TryStartFromPositionId(string positionId)
    {
        if (string.IsNullOrWhiteSpace(positionId))
        {
            Debug.LogWarning("[Backgammon][DebugStart] Empty PositionId provided; ignoring request.");
            return false;
        }

        try
        {
            StartNewGameFromPositionId(positionId.Trim(), "debug-position-id");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Backgammon][DebugStart] Failed to start from PositionId pid={positionId} error={ex.Message}");
            return false;
        }
    }

    private void StartNewGameFromPositionId(string positionId, string context)
    {
        StopAllCoroutines();
        BackgammonAIService.ClearSearchEngineCache();
        _busy = false;
        _cubeNegotiator.Reset();
        _undoStack.Clear();
        State = PositionId.Decode(positionId);
        _isPlayerOnRollVisual = true;
        State.Dice1 = 0;
        State.Dice2 = 0;
        _rolledThisTurn = false;
        _openingRollResolved = false;
        _openingRollTieAwaitingReroll = false;
        _legalTurns.Clear();
        _forcedGameOver = false;
        _forcedWinnerPlayerIndex = -1;
        _gameEndSequenceQueued = false;
        _gameEndedAwaitingNextGame = false;
        _lastGameOverSummary = null;
        InvalidateMovableHighlightCache(context);
        RollsThisGame = 0;
        PlayerRollsThisGame = 0;
        TurnsCompletedThisGame = 0;
        GameRoundIndex++;
        BackgammonGameRules.SyncBoardArrayFromCheckerArrays(State);
        SyncMatchFromState();
        boardManager?.ClearAllPointHighlights();
        boardManager?.EnsureBoardGenerated();
        SyncBoardForVisualState(context);
        hud?.SetDoubleOfferVisible(false);
        ResetBothDiceManagersBetweenTurns(context, shouldEmitResetPickupFeedback: false);
        Debug.Log($"[Backgammon][DebugStart] Loaded start state context={context} pid={positionId}");
        OnStateChanged?.Invoke();
        hud?.RefreshAll(this);
        RefreshMovableCheckerHighlights();
    }

    public static void ClearPersistentAiMoveCache() => BackgammonAiMoveCache.Clear();

    public void RequestRollDice()
    {
        if (_busy || _rolledThisTurn || _cubeNegotiator.AwaitingDoubleResponse) return;
        if (!_diceRollCoordinator.HasTwoDiceManagers()) return;

        if (!_openingRollResolved && _openingRollTieAwaitingReroll)
            _diceRollCoordinator.ResetForOpeningReroll();

        if (!_openingRollResolved)
        {
            _diceRollCoordinator.RequestOpeningRoll();
            return;
        }

        _diceRollCoordinator.RequestNormalRoll(IsPlayerOnRollVisual());
    }

    private bool HasTwoDiceManagers() => _diceRollCoordinator.HasTwoDiceManagers();

    private void TryAutoAssignDiceManagersFromBoard()
    {
        if (HasTwoDiceManagers()) return;
        if (boardManager == null) return;
        DiceManager[] found = boardManager.GetComponentsInChildren<DiceManager>(true);
        if (found == null || found.Length < 2) return;
        if (diceManagerPlayer0 == null) diceManagerPlayer0 = found[0];
        if (diceManagerPlayer1 == null)
        {
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null && found[i] != diceManagerPlayer0)
                {
                    diceManagerPlayer1 = found[i];
                    break;
                }
            }
        }
    }

    private void OnDiceManagerPlayer0Finished(int d1, int d2)
    {
        if (!HasTwoDiceManagers()) return;
        if (_diceRollCoordinator.TryHandleManagerFinished(0, d1, d2,
                out bool _, out int sd0, out int sd1))
        {
            if (sd0 > 0) ApplyNormalRollFromDice(sd0, sd1);
            return;
        }
        if (_aiTurnManager != null && _aiTurnManager.RollInProgress)
        {
            HandleAiDiceRollFinished(0, d1, d2);
            return;
        }
        if (_busy) return;
        if (_diceRollCoordinator.TryBufferOpeningDie(0, d1, out int die0a, out int die1a))
            RouteBufferedOpeningRoll(die0a, die1a);
    }

    private void OnDiceManagerPlayer1Finished(int d1, int d2)
    {
        if (!HasTwoDiceManagers()) return;
        if (_diceRollCoordinator.TryHandleManagerFinished(1, d1, d2,
                out bool _, out int sd0, out int sd1))
        {
            ApplyNormalRollFromDice(sd0, sd1);
            return;
        }
        if (_aiTurnManager != null && _aiTurnManager.RollInProgress)
        {
            HandleAiDiceRollFinished(1, d1, d2);
            return;
        }
        if (_busy) return;
        if (_diceRollCoordinator.TryBufferOpeningDie(1, d1, out int die0b, out int die1b))
            RouteBufferedOpeningRoll(die0b, die1b);
    }

    private void RouteBufferedOpeningRoll(int die0, int die1)
    {
        if (!_openingRollResolved)
            ApplyOpeningRollFromDice(die0, die1);
        else
            ApplyNormalRollFromDice(die0, die1);
    }

    private void BeginAiPhysicalRoll()
    {
        if (!HasTwoDiceManagers()) return;
        _aiTurnManager.BeginPhysicalRoll(_openingRollResolved, IsPlayerOnRollVisual());
    }

    private void HandleAiDiceRollFinished(int managerIndex, int d1, int d2)
    {
        _aiTurnManager.HandleDiceManagerFinished(managerIndex, d1, d2);
    }

    private void ApplyNormalRollFromDice(int d1, int d2)
    {
        // NoDoublets boss variant: break doublets so only 2 moves are available
        if (_doubletsDisabledForSession && d1 == d2)
            d2 = d1 == 6 ? 5 : d1 + 1;

        State.Dice1 = d1;
        State.Dice2 = d2;
        _rolledThisTurn = true;
        RollsThisGame++;
        if (_isPlayerOnRollVisual)
            PlayerRollsThisGame++;
        OnDiceRolled?.Invoke(State.Dice1, State.Dice2);
        RefreshLegals();
        if (_legalTurns.Count == 0)
            PassTurnNoMoves();
        else
        {
            OnStateChanged?.Invoke();
            hud?.RefreshAll(this);
            RefreshMovableCheckerHighlights();
        }
    }

    private void ApplyOpeningRollFromDice(int dieForPlayer0, int dieForPlayer1)
    {
        if (!BackgammonOpeningRollRules.TryApplyOpeningDice(dieForPlayer0, dieForPlayer1, State))
        {
            BackgammonOpeningRollRules.ApplyOpeningTieAutodouble(State);
            _openingRollTieAwaitingReroll = true;
            ResetBothOpeningDiceManagersForReroll();
            DiceFeedbackEventData autoDoubleEvent = new DiceFeedbackEventData(
                DiceFeedbackEventType.OpeningRollTieAutodouble,
                State != null ? State.CubeValue : 0,
                dieForPlayer0,
                dieForPlayer1);
            EmitDiceFeedbackEvent(autoDoubleEvent);
            EmitQueuedScreenNotificationEvent(autoDoubleEvent);
            EnqueueCubeRotatedMarkerEvent("opening-roll-autodouble", customDelaySeconds: 0.50f);
            FireDiceFeedbackEventImmediate(new DiceFeedbackEventData(
                DiceFeedbackEventType.OpeningRollTieDiceResetPickup,
                State != null ? State.CubeValue : 0,
                dieForPlayer0,
                dieForPlayer1));
            State.Dice1 = 0;
            State.Dice2 = 0;
            SyncMatchFromState();
            hud?.RefreshAll(this);
            RefreshMovableCheckerHighlights();
            return;
        }

        _openingRollTieAwaitingReroll = false;
        _openingRollResolved = true;
        _rolledThisTurn = true;
        RollsThisGame++;
        // Sync visual player flag to whoever won the opening roll.
        // In AI mode: human=P1, so _isPlayerOnRollVisual = (State.PlayerOnRoll == LocalPlayerIndex).
        // In hotseat mode the flag still toggles correctly via AdvanceVisualTurnAfterEngineSwap.
        if (BackgammonSettings.OpponentIsAi)
            _isPlayerOnRollVisual = State.PlayerOnRoll == BackgammonPlayerRoles.LocalPlayerIndex;
        if (_isPlayerOnRollVisual)
            PlayerRollsThisGame++;
        OnDiceRolled?.Invoke(State.Dice1, State.Dice2);
        EmitDiceFeedbackEvent(new DiceFeedbackEventData(
            DiceFeedbackEventType.OpeningRollWinnerResolved,
            State != null ? State.CubeValue : 0,
            dieForPlayer0,
            dieForPlayer1,
            State != null ? State.PlayerOnRoll : -1));
        BackgammonGameRules.SyncBoardArrayFromCheckerArrays(State);
        SyncMatchFromState();
        RefreshLegals();
        if (_legalTurns.Count == 0)
            PassTurnNoMoves();
        else
        {
            OnStateChanged?.Invoke();
            hud?.RefreshAll(this);
            RefreshMovableCheckerHighlights();
            MaybeStartAiTurn();
        }
    }

    private void ResetBothOpeningDiceManagersForReroll() =>
        _diceRollCoordinator.ResetForOpeningReroll();

    private void ResetBothDiceManagersBetweenTurns(string context, bool shouldEmitResetPickupFeedback) =>
        _diceRollCoordinator.ResetBetweenTurns(context, shouldEmitResetPickupFeedback);

    private void SyncAiRollDiceVisualsFromState() =>
        _diceRollCoordinator.SyncAiRollVisualsFromState(State.Dice1, State.Dice2);

    private void RefreshLegals()
    {
        _legalTurns.Clear();
        _legalTurns.AddRange(MoveGenerator.GenerateLegalTurns(State));
        InvalidateMovableHighlightCache("refresh-legals");
        if (enableMoveSelectionDebugLogs)
            DebugLogLegalTurnFirstMoves("RefreshLegals");
    }

    /// <summary>Movable base tint applies to logical P1 pieces (current mover). Hidden only while the AI is on roll in opponent-AI mode.</summary>
    private bool ShouldShowMovableCheckerHighlights()
    {
        return CanShowMovableCheckerInteraction();
    }

    /// <summary>True when the local player may interact with movable P1 checkers (after roll, has legals, not AI turn in AI mode).</summary>
    public bool CanShowMovableCheckerInteraction()
    {
        if (State == null || !_rolledThisTurn || _busy || _legalTurns.Count == 0) return false;
        if (BackgammonSettings.OpponentIsAi && !_isPlayerOnRollVisual)
            return false;
        return true;
    }

    private void RefreshMovableCheckerHighlights()
    {
        if (boardManager == null) return;
        if (!ShouldShowMovableCheckerHighlights())
        {
            // Always clear visual highlight state when interaction is disabled; cache flags can be stale after invalidation.
            boardManager.ClearMovableCheckerHighlights();
            _lastMovableHighlightsVisible = false;
            _hasLastMovableHighlightState = true;
            _lastMovableFromPoints.Clear();
            _forceMovableHighlightRebuild = false;
            return;
        }

        BackgammonMovableFromPoints.CollectMovableFromEnginePoints(_legalTurns, _movableFromScratch);
        bool canSkipRebuild = !_forceMovableHighlightRebuild
                              && _hasLastMovableHighlightState
                              && _lastMovableHighlightsVisible
                              && HasSameMovablePoints(_movableFromScratch);
        if (enableMoveSelectionDebugLogs)
            Debug.Log(
                $"[Backgammon][Highlights] refresh requested force={_forceMovableHighlightRebuild} prevCount={_lastMovableFromPoints.Count} newCount={_movableFromScratch.Count} canSkip={canSkipRebuild}");
        if (canSkipRebuild)
            return;

        boardManager.ApplyMovableCheckerHighlights(_movableFromScratch);
        _lastMovableFromPoints.Clear();
        _lastMovableFromPoints.UnionWith(_movableFromScratch);
        _lastMovableHighlightsVisible = true;
        _hasLastMovableHighlightState = true;
        _forceMovableHighlightRebuild = false;
    }

    private bool HasSameMovablePoints(HashSet<int> candidate)
    {
        if (candidate.Count != _lastMovableFromPoints.Count) return false;
        foreach (int point in candidate)
        {
            if (!_lastMovableFromPoints.Contains(point))
                return false;
        }

        return true;
    }

    private void InvalidateMovableHighlightCache(string reason)
    {
        _lastMovableFromPoints.Clear();
        // Keep visibility bookkeeping intact until the next refresh/hide pass performs mandatory visual clear.
        _forceMovableHighlightRebuild = true;
        if (enableMoveSelectionDebugLogs)
            Debug.Log($"[Backgammon][Highlights] Invalidated movable highlight cache reason={reason}");
    }

    private bool IsPlayerTurnVisual() => _isPlayerOnRollVisual;

    private bool IsPlayerOnRollVisual() => _isPlayerOnRollVisual;

    /// <summary>
    /// Creates a visual snapshot where Player1/Player2 arrays are always decoded as player/opponent respectively.
    /// Engine normalization (mover as logical P1) remains internal to <see cref="State"/>.
    /// </summary>
    private GameState BuildVisualStateSnapshot()
    {
        if (State == null) return null;
        if (!BackgammonSettings.OpponentIsAi || IsPlayerOnRollVisual())
            return State;

        // When AI is on roll, engine has P1=AI, P2=human. Flip directly so visual board always
        // shows P1=human (White) and P2=AI (Black). Avoids PositionId encode/decode which can
        // overflow the 80-bit budget when bar checkers are present.
        var visual = new GameState();
        visual.PlayerOnRoll = 1;
        visual.Player1Checkers = (int[])State.Player2Checkers.Clone();
        visual.Player2Checkers = (int[])State.Player1Checkers.Clone();
        BackgammonGameRules.SyncBoardArrayFromCheckerArrays(visual);
        return visual;
    }

    private void SyncBoardForVisualState(string context)
    {
        if (boardManager == null || State == null) return;
        GameState visual = BuildVisualStateSnapshot();
        boardManager.SyncCheckersFromGameState(visual);
        Debug.Log($"[Backgammon][Visual] Synced visual board context={context} playerTurn={IsPlayerTurnVisual()} playerOnRoll={IsPlayerOnRollVisual()}");
    }

    /// <summary>Apply a full legal turn by index from <see cref="CurrentLegalTurns"/>.</summary>
    public void TryApplyTurnByIndex(int index)
    {
        if (_busy || !_rolledThisTurn || index < 0 || index >= _legalTurns.Count) return;
        Turn turn = _legalTurns[index];
        if (turn == null || turn.Moves == null || turn.Moves.Count == 0) return;
        PushUndoFrame(turn.Moves[0], GetVisualMoverColorForCurrentTurn());
        ApplySingleMoveAndContinue(turn.Moves[0]);
    }

    /// <summary>Apply one legal first move whose source matches <paramref name="from"/> and preferred destination ordering.</summary>
    public bool TryApplyPreferredFirstMoveForFrom(int from, bool preferHighestTo)
    {
        if (_busy || !_rolledThisTurn || _legalTurns.Count == 0) return false;
        if (!TrySelectPreferredFirstMoveTurnIndex(_legalTurns, from, preferHighestTo, out int selectedIdx))
            return false;
        Turn turn = _legalTurns[selectedIdx];
        if (turn == null || turn.Moves == null || turn.Moves.Count == 0) return false;
        if (enableMoveSelectionDebugLogs)
            Debug.Log($"[Backgammon][MoveSelect] from={from} preferHighest={preferHighestTo} selectedIdx={selectedIdx} firstMove={FormatMove(turn.Moves[0])}");
        PushUndoFrame(turn.Moves[0], GetVisualMoverColorForCurrentTurn());
        ApplySingleMoveAndContinue(turn.Moves[0]);
        return true;
    }

    /// <summary>Backwards-compatible alias; now applies only the first move, not a full turn.</summary>
    public bool TryApplyPreferredTurnForFrom(int from, bool preferHighestTo)
    {
        return TryApplyPreferredFirstMoveForFrom(from, preferHighestTo);
    }

    /// <summary>Apply highest legal first-move destination across all legal turns.</summary>
    public bool TryApplyHighestLegalTurn()
    {
        if (_busy || !_rolledThisTurn || _legalTurns.Count == 0) return false;
        int bestIdx = -1;
        int bestTo = int.MinValue;
        for (int i = 0; i < _legalTurns.Count; i++)
        {
            Turn t = _legalTurns[i];
            if (t == null || t.Moves == null || t.Moves.Count == 0) continue;
            int to = t.Moves[0].To;
            if (bestIdx < 0 || to > bestTo)
            {
                bestIdx = i;
                bestTo = to;
            }
        }

        if (bestIdx < 0) return false;
        Turn turn = _legalTurns[bestIdx];
        if (turn == null || turn.Moves == null || turn.Moves.Count == 0) return false;
        PushUndoFrame(turn.Moves[0], GetVisualMoverColorForCurrentTurn());
        ApplySingleMoveAndContinue(turn.Moves[0]);
        return true;
    }

    public bool TryFinalizeCurrentTurn()
    {
        if (_busy || !_rolledThisTurn || _legalTurns.Count > 0) return false;
        FinalizeTurnAndAdvance();
        return true;
    }

    public static bool TrySelectPreferredFirstMoveTurnIndex(IReadOnlyList<Turn> legalTurns, int from, bool preferHighestTo, out int selectedIdx)
    {
        selectedIdx = -1;
        if (legalTurns == null || legalTurns.Count == 0) return false;
        int bestDistance = preferHighestTo ? int.MinValue : int.MaxValue;
        int bestTo = preferHighestTo ? int.MinValue : int.MaxValue;
        for (int i = 0; i < legalTurns.Count; i++)
        {
            Turn t = legalTurns[i];
            if (t == null || t.Moves == null || t.Moves.Count == 0) continue;
            Move first = t.Moves[0];
            if (first.From != from) continue;
            int firstDistance = Mathf.Abs(first.From - first.To);
            int firstTo = first.To;
            if (selectedIdx < 0 ||
                (preferHighestTo && (firstDistance > bestDistance || (firstDistance == bestDistance && firstTo > bestTo))) ||
                (!preferHighestTo && (firstDistance < bestDistance || (firstDistance == bestDistance && firstTo < bestTo))))
            {
                selectedIdx = i;
                bestDistance = firstDistance;
                bestTo = firstTo;
            }
        }

        return selectedIdx >= 0;
    }

    /// <summary>Revert the last player-visible state change (single checker move within the current roll).</summary>
    public bool TryUndoLastMove()
    {
        using var undoScope = UndoTryMarker.Auto();
        if (_busy || _undoStack.Count == 0) return false;
        UndoFrame f = _undoStack.Pop();
        bool animatedUndo = false;
        if (boardManager != null && f.AppliedMove.HasValue)
        {
            animatedUndo = boardManager.TryApplySingleVisualUndoMove(f.AppliedMove.Value, f.AppliedMoveColor, out string undoFailReason);
            if (enableMoveSelectionDebugLogs)
                Debug.Log($"[Backgammon][Undo] Attempt reverse visual move {FormatMove(f.AppliedMove.Value)} mover={f.AppliedMoveColor} success={animatedUndo} reason={(animatedUndo ? "ok" : undoFailReason)}");
            if (animatedUndo)
                _undoVisualSuccessCount++;
        }
        else if (!f.AppliedMove.HasValue)
        {
            _undoFallbackNoAppliedMoveCount++;
        }

        using (UndoRestoreMarker.Auto())
            RestoreUndoFrame(f);
        using (UndoRefreshLegalsMarker.Auto())
            RestoreLegalsFromUndoFrame(f);
        boardManager?.ClearAllPointHighlights();
        if (!animatedUndo)
        {
            _undoFallbackSyncCount++;
            boardManager?.SyncCheckersFromGameState(State);
            _forceMovableHighlightRebuild = true;
        }
        _cubeNegotiator.CancelPendingOffer();
        hud?.SetDoubleOfferVisible(false);
        TurnsCompletedThisGame = f.TurnsCompletedThisGame;
        OnStateChanged?.Invoke();
        using (UndoHudRefreshMarker.Auto())
            hud?.RefreshAll(this);
        RefreshMovableCheckerHighlights();
        EmitCheckerSoundEventForUndo(f.AppliedMove);
        if (enableUndoPerformanceLogs)
        {
            Debug.Log(
                $"[Backgammon][Undo][Perf] success={animatedUndo} legalCount={_legalTurns.Count} visualSuccess={_undoVisualSuccessCount} fallbackSync={_undoFallbackSyncCount} noAppliedMove={_undoFallbackNoAppliedMoveCount}");
        }
        return true;
    }

    /// <summary>Offer double before rolling. Opponent responds via HUD (or AI auto-responds).</summary>
    public void OfferDouble()
    {
        if (!CanCurrentPlayerOfferDouble()) return;
        int responder = _cubeNegotiator.BeginOffer(State);
        SyncMatchFromState();
        hud?.SetDoubleOfferVisible(true);
        hud?.RefreshAll(this);
        OnDiceFeedbackEvent?.Invoke(new DiceFeedbackEventData(DiceFeedbackEventType.CubeOffered, State != null ? State.CubeValue : 0));
        if (BackgammonSettings.OpponentIsAi && responder == 0)
            StartCoroutine(CoAiRespondDouble());
    }

    public bool CanCurrentPlayerOfferDouble() =>
        !_cubeDisabledForSession &&
        _cubeNegotiator.CanOffer(State, _openingRollResolved, _busy, IsGameOver(out _), _rolledThisTurn);

    public void RespondDoubleTake()
    {
        if (!_cubeNegotiator.AwaitingDoubleResponse || _busy) return;
        _cubeNegotiator.ApplyTake(State, Match);
        hud?.SetDoubleOfferVisible(false);
        OnStateChanged?.Invoke();
        hud?.RefreshAll(this);
        EmitQueuedScreenNotificationEvent(new DiceFeedbackEventData(
            DiceFeedbackEventType.CubeValueChanged,
            State != null ? State.CubeValue : 0));
        EnqueueCubeRotatedMarkerEvent("double-take");
    }

    public void RespondDoubleDrop()
    {
        if (!_cubeNegotiator.AwaitingDoubleResponse || _busy) return;
        int winner = _cubeNegotiator.ApplyDrop();
        hud?.SetDoubleOfferVisible(false);
        FinalizeGameAndQueuePresentation(
            winnerPlayerIndex: winner,
            reason: GameEndReason.DoubleDrop,
            scoreKindOverride: GameEndScoreKind.Single);
    }

    public bool CanCurrentPlayerBeaver() =>
        _cubeNegotiator.CanBeaver(State, Match?.BeaversAllowed ?? false);

    /// <summary>Beaver: responder immediately re-doubles. Ownership stays with the responder. Original offerer must now take/drop.</summary>
    public void OfferBeaver()
    {
        if (!CanCurrentPlayerBeaver() || _busy) return;
        _cubeNegotiator.ApplyBeaver(State, Match);
        SyncMatchFromState();
        hud?.SetDoubleOfferVisible(true);
        hud?.RefreshAll(this);
        OnStateChanged?.Invoke();
        EmitQueuedScreenNotificationEvent(new DiceFeedbackEventData(DiceFeedbackEventType.BeaverOffered, State != null ? State.CubeValue : 0));
        EnqueueCubeRotatedMarkerEvent("beaver");
        // If the original offerer is now AI (player index 0), trigger AI response
        if (BackgammonSettings.OpponentIsAi && _cubeNegotiator.DoubleOfferedByPlayer == BackgammonPlayerRoles.LocalPlayerIndex)
            StartCoroutine(CoAiRespondDouble());
    }

    public void SetBoardViewHorizontal(bool horizontal)
    {
        BackgammonSettings.BoardViewIsHorizontal = horizontal;
        BackgammonBoardLayout.SetHorizontal(horizontal);
        boardManager?.SetBoardViewHorizontal(horizontal);
        if (State != null)
        {
            boardManager?.ClearAllPointHighlights();
            SyncBoardForVisualState("set-board-view");
            RefreshMovableCheckerHighlights();
        }
    }

    private IEnumerator CoAiRespondDouble()
    {
        _busy = true;
        yield return new WaitForSeconds(0.4f);
        _busy = false;
        if (!_cubeNegotiator.AwaitingDoubleResponse) yield break;
        string cacheKey = BuildAiCubeDecisionCacheKey(State, Match, "response");
        bool hit = TryGetCachedAiCubeResponseDecision(cacheKey, out AiDoubleResponseDecision decision);
        bool aiEvaluated = false;
        // #region agent log
        WriteAgentDebugLog(
            "run1",
            "H4",
            "BackgammonGameController.CoAiRespondDouble:preEval",
            "response decision evaluation starting",
            $"{{\"cacheHit\":{hit.ToString().ToLowerInvariant()},\"cacheKeyEmpty\":{string.IsNullOrWhiteSpace(cacheKey).ToString().ToLowerInvariant()},\"playerOnRoll\":{State?.PlayerOnRoll ?? -1},\"cubeValue\":{State?.CubeValue ?? -1},\"cubeOwner\":{State?.CubeOwner ?? -1}}}");
        // #endregion
        if (!hit)
        {
            IBackgammonAIEvaluator evaluator = BackgammonAIEvaluatorFactory.GetEvaluator();
            if (evaluator != null)
            {
                AiDoubleResponseDecision result = default;
                yield return evaluator.EvaluateDoubleResponseAsync(State, Match)
                    .AsCoroutine(r => result = r);

                decision = result;
                aiEvaluated = result.FromAiEvaluator;
                CacheAiCubeResponseDecision(cacheKey, decision);
            }
        }
        bool shouldTake = decision.Action == AiDoubleResponseAction.Take;
        if (enableAiCubeDecisionDebugLogs || !aiEvaluated)
        {
            Debug.Log(
                $"[Backgammon][AI][Cube][Response] cacheHit={hit} evaluated={aiEvaluated} action={decision.Action} reason={decision.Reason} " +
                $"playerOnRoll={State?.PlayerOnRoll ?? -1} cube={State?.CubeValue ?? -1} owner={State?.CubeOwner ?? -1}");
        }

        if (shouldTake)
            RespondDoubleTake();
        else
            RespondDoubleDrop();
    }

    private static int OpponentIndex(int playerOnRoll) => playerOnRoll == 0 ? 1 : 0;

    private void AdvanceVisualTurnAfterEngineSwap()
    {
        _isPlayerOnRollVisual = !_isPlayerOnRollVisual;
    }

    private void PushUndoFrame(Move? appliedMove = null, PlayerColor appliedMoveColor = PlayerColor.White)
    {
        using (UndoPushFrameMarker.Auto())
            _undoStack.Push(UndoFrame.Capture(State, _rolledThisTurn, TurnsCompletedThisGame, _legalTurns, appliedMove, appliedMoveColor));
    }

    private void ClearUndoStackAfterTurnCompleted(string reason)
    {
        int dropped = _undoStack.Count;
        if (dropped == 0) return;
        _undoStack.Clear();
        Debug.Log($"[Backgammon][Undo] Cleared undo stack after turn completed reason={reason} droppedFrames={dropped}");
    }

    private void RestoreUndoFrame(UndoFrame f)
    {
        f.ApplyTo(State);
        _rolledThisTurn = f.RolledThisTurn;
    }

    private void RestoreLegalsFromUndoFrame(UndoFrame f)
    {
        _legalTurns.Clear();
        if (!f.HasLegalTurnsSnapshot)
        {
            RefreshLegals();
            return;
        }

        _legalTurns.AddRange(CloneLegalTurns(f.LegalTurnsSnapshot));
        InvalidateMovableHighlightCache("undo-restore-legals");
        if (enableMoveSelectionDebugLogs)
            Debug.Log($"[Backgammon][Undo] Restored cached legal turns count={_legalTurns.Count}");
    }

    private void SyncMatchFromState()
    {
        if (State == null || Match == null) return;
        Match.Cube = State.CubeValue;
        Match.CubeOwner = State.CubeOwner == 3 ? -1 : State.CubeOwner;
        Match.Player0Score = State.Player1Score;
        Match.Player1Score = State.Player2Score;
        Match.PlayerOnRoll = State.PlayerOnRoll;
        Match.MatchLength = State.MatchLength;
    }

    private void PassTurnNoMoves()
    {
        if (!_rolledThisTurn) return;
        _busy = true;

        EmitQueuedScreenNotificationEvent(new DiceFeedbackEventData(
            DiceFeedbackEventType.NoLegalMoves,
            State != null ? State.CubeValue : 0));

        BackgammonGameRules.SwapSidesForNextTurn(State);
        AdvanceVisualTurnAfterEngineSwap();
        BackgammonGameRules.SyncBoardArrayFromCheckerArrays(State);
        State.Dice1 = 0;
        State.Dice2 = 0;
        _rolledThisTurn = false;
        _legalTurns.Clear();
        InvalidateMovableHighlightCache("pass-no-moves");
        boardManager?.ClearAllPointHighlights();
        SyncBoardForVisualState("pass-no-moves");
        _busy = false;
        SyncMatchFromState();
        TurnsCompletedThisGame++;
        OnStateChanged?.Invoke();
        hud?.RefreshAll(this);
        RefreshMovableCheckerHighlights();
        ClearUndoStackAfterTurnCompleted("pass-no-moves");
        ResetBothDiceManagersBetweenTurns("pass-no-moves", shouldEmitResetPickupFeedback: true);
        MaybeStartAiTurn();
    }

    private void ApplySingleMoveAndContinue(Move move)
    {
        _busy = true;
        List<Turn> legalBeforeMove = new List<Turn>(_legalTurns);
        var singleMoveTurn = new Turn
        {
            Moves = new List<Move> { move }
        };
        GameState stateBeforeApply = State;
        State = MoveGenerator.ApplyTurn(State, singleMoveTurn);
        PreservePersistentCubeState(stateBeforeApply, State, "human-move-apply");
        BackgammonGameRules.SyncBoardArrayFromCheckerArrays(State);
        LogBearOffPositionSnapshot(move, "human");

        if (!BackgammonGameRules.ValidateCheckerCounts(State, out string validateError))
        {
            Debug.LogError($"[Backgammon][VALIDATION] Checker count invalid after move {move.From}→{move.To}:\n{validateError}\n{BackgammonGameRules.GetCheckerDistribution(State)}");
        }

        AdvanceStagedLegalTurnsAfterMove(legalBeforeMove, move);
        if (enableMoveSelectionDebugLogs)
            DebugLogLegalTurnFirstMoves("AdvanceStagedLegalTurnsAfterMove");
        boardManager?.ClearAllPointHighlights();
        PlayerColor moverColor = GetVisualMoverColorForCurrentTurn();
        bool movedVisually = boardManager != null && boardManager.TryApplySingleVisualMove(move, moverColor);
        if (!movedVisually)
            SyncBoardForVisualState("apply-single-fallback-sync");
        _forceMovableHighlightRebuild = true;
        SyncMatchFromState();
        EmitCheckerSoundEventForAppliedMove(move);

        // Check for game end after every move (critical for detecting bear-off wins)
        // Must check BEFORE setting _busy = false and refreshing UI
        if (TryFinalizeBearOffGameEnd())
        {
            // Game ended, bear-off winner detected
            // Note: TryFinalizeBearOffGameEnd handles its own state updates and UI refresh
            return;
        }

        _busy = false;
        OnStateChanged?.Invoke();
        hud?.RefreshAll(this);
        RefreshMovableCheckerHighlights();
    }

    private void AdvanceStagedLegalTurnsAfterMove(IReadOnlyList<Turn> legalBeforeMove, Move appliedMove)
    {
        _legalTurns.Clear();
        InvalidateMovableHighlightCache("advance-staged-legals");
        if (legalBeforeMove == null || legalBeforeMove.Count == 0)
            return;

        for (int i = 0; i < legalBeforeMove.Count; i++)
        {
            Turn t = legalBeforeMove[i];
            if (t?.Moves == null || t.Moves.Count == 0) continue;
            Move first = t.Moves[0];
            if (!AreMovesEquivalent(first, appliedMove)) continue;

            if (t.Moves.Count == 1)
                continue;

            var remainingMoves = new List<Move>(t.Moves.Count - 1);
            for (int mi = 1; mi < t.Moves.Count; mi++)
                remainingMoves.Add(t.Moves[mi]);

            var remainingDiceUsed = new List<int>();
            if (t.DiceUsed != null && t.DiceUsed.Count > 1)
            {
                for (int di = 1; di < t.DiceUsed.Count; di++)
                    remainingDiceUsed.Add(t.DiceUsed[di]);
            }

            _legalTurns.Add(new Turn
            {
                Moves = remainingMoves,
                DiceUsed = remainingDiceUsed,
                ResultingState = null
            });
        }
    }

    private static bool AreMovesEquivalent(Move a, Move b)
    {
        return a.From == b.From && a.To == b.To && a.IsHit == b.IsHit;
    }

    public static CheckerSoundEventType ClassifyCheckerSoundEventForAppliedMove(Move move)
    {
        if (move.IsHit) return CheckerSoundEventType.HitToBar;
        if (move.From == BackgammonBoardLayout.BarEngineIndex) return CheckerSoundEventType.EnterFromBar;
        if (move.To < 0) return CheckerSoundEventType.BearOff;
        return CheckerSoundEventType.Move;
    }

    private void EmitCheckerSoundEventForAppliedMove(Move move)
    {
        if (move.From == BackgammonBoardLayout.BarEngineIndex && move.IsHit)
        {
            // Combined move: entering from bar and hitting a blot should emit both cues.
            EmitCheckerSoundEvent(CheckerSoundEventType.EnterFromBar, move, isUndo: false);
            EmitCheckerSoundEvent(CheckerSoundEventType.HitToBar, move, isUndo: false);
            return;
        }

        EmitCheckerSoundEvent(ClassifyCheckerSoundEventForAppliedMove(move), move, isUndo: false);
    }

    private void EmitCheckerSoundEventForUndo(Move? appliedMove)
    {
        Move move = appliedMove ?? default;
        EmitCheckerSoundEvent(
            CheckerSoundEventType.Undo,
            move,
            isUndo: true);
    }

    private void EmitCheckerSoundEvent(CheckerSoundEventType eventType, Move move, bool isUndo)
    {
        CheckerSoundEventData data = new CheckerSoundEventData(
            eventType,
            State != null ? State.PlayerOnRoll : -1,
            move.From,
            move.To,
            move.IsHit,
            isUndo);
        EnqueuePresentationEvent(
            eventName: $"checker-{eventType}",
            blocking: true,
            minDelaySeconds: BackgammonSettings.EventQueueBaseGapSeconds + BackgammonSettings.EventQueueAudioLeadInSeconds,
            clockDomain: BackgammonEventClockDomain.ScaledGameplay,
            dispatch: () => OnCheckerSoundEvent?.Invoke(data));
    }

    // Fires a dice feedback event immediately (no queue) so audio coincides with the visual action.
    private void FireDiceFeedbackEventImmediate(DiceFeedbackEventData data)
    {
        Debug.Log($"[Backgammon][DiceFeedback] Immediate fire event={data.EventType}");
        OnDiceFeedbackEvent?.Invoke(data);
    }

    private void EmitDiceFeedbackEvent(DiceFeedbackEventData data)
    {
        Debug.Log(
            $"[Backgammon][DiceFeedback] Emit event={data.EventType} cubeAfter={data.CubeValueAfter} openingP0={data.OpeningDiePlayer0} openingP1={data.OpeningDiePlayer1} openingResolved={_openingRollResolved} openingTieAwaitingReroll={_openingRollTieAwaitingReroll}");
        if (enableDiceFeedbackDebugLogs)
            Debug.Log(
                $"[Backgammon][DiceFeedback] subscribers={(OnDiceFeedbackEvent != null ? OnDiceFeedbackEvent.GetInvocationList().Length : 0)}");

        EnqueuePresentationEvent(
            eventName: $"dice-{data.EventType}",
            blocking: true,
            minDelaySeconds: BackgammonSettings.EventQueueBaseGapSeconds,
            clockDomain: BackgammonEventClockDomain.ScaledGameplay,
            dispatch: () => OnDiceFeedbackEvent?.Invoke(data));
    }

    private void EmitQueuedScreenNotificationEvent(DiceFeedbackEventData data, float minDelaySeconds = 0.6f)
    {
        EnqueuePresentationEvent(
            eventName: $"screen-notification-{data.EventType}",
            blocking: true,
            minDelaySeconds: minDelaySeconds,
            clockDomain: BackgammonEventClockDomain.ScaledGameplay,
            dispatch: () => OnScreenNotificationEvent?.Invoke(data));
    }

    private void EnqueueCubeRotatedMarkerEvent(string context, float customDelaySeconds = -1f)
    {
        float delaySeconds = customDelaySeconds >= 0f
            ? customDelaySeconds
            : BackgammonSettings.EventQueueCubeRotateHoldSeconds;

        EnqueuePresentationEvent(
            eventName: "cube-rotated",
            blocking: true,
            minDelaySeconds: delaySeconds,
            clockDomain: BackgammonEventClockDomain.ScaledGameplay,
            dispatch: () =>
            {
                int cubeValue = State?.CubeValue ?? 0;
                if (enableEventQueueDebugLogs)
                {
                    Debug.Log(
                        $"[Backgammon][EventQueue] cube-rotated marker context={context} cube={cubeValue} owner={State?.CubeOwner ?? -1}");
                }
                OnCubeRotatedMarker?.Invoke(cubeValue);
            });
    }

    private void EnqueuePresentationEvent(
        string eventName,
        bool blocking,
        float minDelaySeconds,
        BackgammonEventClockDomain clockDomain,
        Action dispatch)
    {
        if (_presentationEventQueue == null)
        {
            dispatch?.Invoke();
            return;
        }

        _presentationEventQueue.Enqueue(new BackgammonPresentationEvent(
            eventName,
            dispatch,
            blocking,
            minDelaySeconds,
            clockDomain));
    }

    private static float GetPresentationSpeedMultiplier()
    {
        return GetPresentationSpeedMultiplier(BackgammonSettings.GameSpeedSecondsPerStep);
    }

    public void SetPresentationGameSpeed(float secondsPerStep)
    {
        if (_presentationEventQueue == null)
            return;

        _presentationEventQueue.SetGameSpeedMultiplier(GetPresentationSpeedMultiplier(secondsPerStep));
    }

    private static float GetPresentationSpeedMultiplier(float secondsPerStep)
    {
        const float defaultStepSeconds = 0.35f;
        float step = Mathf.Clamp(secondsPerStep, 0.05f, 2f);
        return Mathf.Clamp(defaultStepSeconds / step, 0.25f, 6f);
    }

    private void FinalizeTurnAndAdvance()
    {
        _busy = true;
        if (TryFinalizeBearOffGameEnd())
        {
            _busy = false;
            return;
        }

        BackgammonGameRules.SwapSidesForNextTurn(State);
        AdvanceVisualTurnAfterEngineSwap();
        BackgammonGameRules.SyncBoardArrayFromCheckerArrays(State);
        State.Dice1 = 0;
        State.Dice2 = 0;
        _rolledThisTurn = false;
        _legalTurns.Clear();
        InvalidateMovableHighlightCache("finalize-turn");
        boardManager?.ClearAllPointHighlights();
        SyncBoardForVisualState("finalize-turn");
        _busy = false;
        SyncMatchFromState();
        TurnsCompletedThisGame++;
        ClearUndoStackAfterTurnCompleted("finalize-turn");
        ResetBothDiceManagersBetweenTurns("finalize-turn", shouldEmitResetPickupFeedback: true);

        if (IsGameOver(out _))
        {
            OnStateChanged?.Invoke();
            hud?.RefreshAll(this);
            RefreshMovableCheckerHighlights();
            return;
        }

        OnStateChanged?.Invoke();
        hud?.RefreshAll(this);
        RefreshMovableCheckerHighlights();
        MaybeStartAiTurn();
    }

    public bool IsGameOver(out string winnerLabel)
    {
        winnerLabel = null;
        if (State == null) return false;
        if (_forcedGameOver)
        {
            winnerLabel = _forcedWinnerPlayerIndex == 0 ? "Player 1 wins" : "Player 2 wins";
            return true;
        }
        bool p1Empty = BackgammonGameRules.HasWon(State.Player1Checkers);
        bool p2Empty = BackgammonGameRules.HasWon(State.Player2Checkers);
        if (!p1Empty && !p2Empty) return false;
        winnerLabel = p1Empty ? "Player 2 wins" : "Player 1 wins";
        return true;
    }

    private void MaybeStartAiTurn()
    {
        if (IsGameOver(out _)) return;
        if (!BackgammonSettings.OpponentIsAi || !BackgammonPlayerRoles.IsAiTurnInOpponentAiMode(State.PlayerOnRoll)) return;
        StartCoroutine(CoAiTurn());
    }

    private IEnumerator CoAiTurn()
    {
        Stopwatch aiTurnStopwatch = _aiTurnManager.StartTimingStopwatch();
        float preRollDelay = BackgammonAiTurnManager.GetPreRollDelaySeconds();
        float postRollRevealDelay = BackgammonAiTurnManager.GetPostRollRevealDelaySeconds();
        float postApplyDelay = BackgammonAiTurnManager.GetPostApplyDelaySeconds();
        Debug.Log(
            $"[Backgammon][AI] pacing speedStep={BackgammonSettings.GameSpeedSecondsPerStep:F2} preRoll={preRollDelay:F2}s postRollReveal={postRollRevealDelay:F2}s postApply={postApplyDelay:F2}s");
        yield return new WaitForSeconds(preRollDelay);
        _aiTurnManager.LogAiTiming("pre-roll-wait", preRollDelay * 1000f, $"depth={BackgammonSettings.AiSearchDepth} speedStep={BackgammonSettings.GameSpeedSecondsPerStep:F2}");

        bool shouldOfferDouble = false;
        yield return CoShouldAiOfferDoubleBeforeRoll(result => shouldOfferDouble = result);

        if (shouldOfferDouble)
        {
            Debug.Log("[Backgammon][AI] Offering double before roll (human responds).");
            OfferDouble();
            float waitSeconds = 0f;
            while (_cubeNegotiator.AwaitingDoubleResponse && waitSeconds < 120f && !IsGameOver(out _))
            {
                waitSeconds += Time.deltaTime;
                yield return null;
            }

            if (_cubeNegotiator.AwaitingDoubleResponse && !IsGameOver(out _))
            {
                Debug.LogWarning("[Backgammon][AI] Double offer wait exceeded 120s; auto-take to unblock.");
                RespondDoubleTake();
            }

            if (IsGameOver(out _))
                yield break;
        }

        _busy = true;
        bool needNewRoll = State.Dice1 <= 0 || State.Dice2 <= 0 || !_rolledThisTurn;
        if (needNewRoll)
        {
            if (HasTwoDiceManagers())
            {
                BeginAiPhysicalRoll();
                float timeoutSeconds = Mathf.Clamp(BackgammonAiTurnManager.GetPacingBaseSeconds() * 8f, 0.5f, 6f);
                float waited = 0f;
                while (_aiTurnManager.RollInProgress && waited < timeoutSeconds)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }
                _aiTurnManager.LogAiTiming("dice-roll-wait", waited * 1000f, $"timeoutMs={timeoutSeconds * 1000f:F0} token={_aiTurnManager.ActiveRollToken}");

                if (_aiTurnManager.RollInProgress)
                {
                    _aiTurnManager.ForceRollTimeout();
                    Debug.LogWarning(
                        $"[Backgammon][AI][Dice] Roll timeout token={_aiTurnManager.ActiveRollToken} waited={waited:F2}s fallback=random");
                }
            }

            if (_aiTurnManager.BufferedDie0.HasValue && _aiTurnManager.BufferedDie1.HasValue)
            {
                State.Dice1 = _aiTurnManager.BufferedDie0.Value;
                State.Dice2 = _aiTurnManager.BufferedDie1.Value;
            }
            else
            {
                State.Dice1 = UnityEngine.Random.Range(1, 7);
                State.Dice2 = UnityEngine.Random.Range(1, 7);
                Debug.LogWarning(
                    $"[Backgammon][AI][Dice] Missing physical roll result token={_aiTurnManager.ActiveRollToken}; using fallback d1={State.Dice1} d2={State.Dice2}");
            }

            _rolledThisTurn = true;
            RollsThisGame++;
            OnDiceRolled?.Invoke(State.Dice1, State.Dice2);
            _aiTurnManager.ConsumeBufferedRoll();
        }
        RefreshLegals();
        OnStateChanged?.Invoke();
        hud?.RefreshAll(this);
        yield return new WaitForSeconds(postRollRevealDelay);
        _aiTurnManager.LogAiTiming("post-roll-reveal-wait", postRollRevealDelay * 1000f, $"legalTurns={_legalTurns.Count}");

        if (_legalTurns.Count == 0)
        {
            Debug.Log("[Backgammon][AI] No legal moves available; skipping AI evaluation.");
            PassTurnNoMoves();
            if (aiTurnStopwatch != null)
            {
                aiTurnStopwatch.Stop();
                _aiTurnManager.LogAiTiming("total-ai-turn", aiTurnStopwatch.ElapsedMilliseconds, "reason=no-legal-moves");
            }
            yield break;
        }

        bool aiTurnResolvedBySearch = false;
        Turn pick = null;
        IBackgammonAIEvaluator evaluator = BackgammonAIEvaluatorFactory.GetEvaluator();
        if (evaluator != null)
        {
            // Configure SearchEngine if using local neural net
            if (BackgammonAIService.TryGetSearchEngine(out SearchEngine se))
            {
                se.EnableDecisionLogging = enableMoveSelectionDebugLogs;
                se.EnablePruneComparisonDebug = false;
                se.EnableStagedPruneFiltering = true;
                se.ForceFullTurnEvaluation = false;
                se.QualityPreset = SearchEngine.SearchQualityPreset.Balanced;
            }

            Debug.Log(
                $"[Backgammon][AI][Search] invoking evaluator depth={BackgammonSettings.AiSearchDepth} " +
                $"engine={BackgammonSettings.AiEngineType} playerOnRoll={State.PlayerOnRoll} dice={State.Dice1}/{State.Dice2}");
            Stopwatch searchStopwatch = _aiTurnManager.StartTimingStopwatch();
            GameState stateSnapshot = CloneGameState(State);
            MatchState matchSnapshot = CloneMatchState(Match, State);
            string cacheKey = BackgammonAiMoveCache.BuildMoveKey(
                stateSnapshot,
                matchSnapshot,
                BackgammonSettings.AiSearchDepth,
                SearchEngine.SearchQualityPreset.Balanced,
                true,
                false);
            if (BackgammonAiMoveCache.TryGetTurn(cacheKey, out Turn cachedPick))
            {
                pick = cachedPick;
                BackgammonAiMoveCache.LogMoveDecision("get", cacheKey, hit: true, enableAiCacheDebugLogs);
            }
            else
            {
                BackgammonAiMoveCache.LogMoveDecision("get", cacheKey, hit: false, enableAiCacheDebugLogs);
                Task<Turn> searchTask = evaluator.EvaluateBestTurnAsync(
                    stateSnapshot, matchSnapshot, BackgammonSettings.AiSearchDepth);

                while (!searchTask.IsCompleted)
                    yield return null;

                if (searchTask.IsFaulted)
                {
                    Debug.LogError($"[Backgammon][AI][Search] Evaluation failed: {searchTask.Exception?.GetBaseException().Message}");
                }
                else if (searchTask.IsCanceled)
                {
                    Debug.LogError("[Backgammon][AI][Search] Evaluation was canceled.");
                }
                else
                {
                    pick = searchTask.Result;
                    if (pick?.Moves != null && pick.ResultingState != null)
                    {
                        BackgammonAiMoveCache.StoreTurn(cacheKey, pick);
                        BackgammonAiMoveCache.LogMoveDecision("store", cacheKey, hit: false, enableAiCacheDebugLogs);
                    }
                }
            }

            if (searchStopwatch != null)
            {
                searchStopwatch.Stop();
                string timingDetails = se != null
                    ? $"depth={BackgammonSettings.AiSearchDepth} quality={se.QualityPreset} legal={se.LastLegalTurnCount} unique={se.LastUniqueLegalTurnCount} finalCandidates={se.LastTelemetry.FinalCandidates} candidateIds={se.LastFinalCandidatePositionIds.Count}"
                    : $"depth={BackgammonSettings.AiSearchDepth} engine={BackgammonSettings.AiEngineType}";
                LogAiTiming("search", searchStopwatch.ElapsedMilliseconds, timingDetails);
            }
            if (pick?.Moves != null)
            {
                string moveSummary = string.Join(";", pick.Moves.Select(m => $"{m.From}->{m.To}" + (m.IsHit ? "x" : string.Empty)));
                string moveSummaryOneBased = string.Join(";", pick.Moves.Select(m => $"{m.From + 1}->{m.To + 1}" + (m.IsHit ? "x" : string.Empty)));
                if (se != null)
                {
                    Debug.Log(
                        $"[Backgammon][AI][Search] selected moveCount={pick.Moves.Count} legal={se.LastLegalTurnCount} " +
                        $"unique={se.LastUniqueLegalTurnCount} duplicates={se.LastDuplicateLegalTurnCount} " +
                        $"movePath0={moveSummary} movePath1={moveSummaryOneBased}");
                    Debug.Log($"[Backgammon][AI][Search] telemetry stages={se.LastTelemetry.StageSummary} finalCandidates={se.LastTelemetry.FinalCandidates}");
                }
                else
                {
                    Debug.Log(
                        $"[Backgammon][AI][Search] selected moveCount={pick.Moves.Count} " +
                        $"movePath0={moveSummary} movePath1={moveSummaryOneBased}");
                }
            }
            aiTurnResolvedBySearch = true;
        }

        if (!aiTurnResolvedBySearch)
        {
            Debug.LogError("[Backgammon][AI][Search] SearchEngine unavailable during AI turn; forcing no-move turn swap.");
        }

        if (pick == null || pick.ResultingState == null)
        {
            Debug.LogWarning(
                $"[Backgammon][AI][Search] Engine returned no move. legalTurnsCached={_legalTurns.Count} " +
                $"playerOnRoll={State.PlayerOnRoll} dice={State.Dice1}/{State.Dice2}. Treating as no-legal/pass.");
            BackgammonGameRules.SwapSidesForNextTurn(State);
            AdvanceVisualTurnAfterEngineSwap();
            BackgammonGameRules.SyncBoardArrayFromCheckerArrays(State);
        }
        else
        {
            Stopwatch playbackStopwatch = _aiTurnManager.StartTimingStopwatch();
            yield return CoPlayAiTurnMovesSequentially(pick);
            if (playbackStopwatch != null)
            {
                playbackStopwatch.Stop();
                LogAiTiming("move-playback", playbackStopwatch.ElapsedMilliseconds, $"moveCount={(pick?.Moves != null ? pick.Moves.Count : 0)}");
            }
            BackgammonGameRules.SwapSidesForNextTurn(State);
            AdvanceVisualTurnAfterEngineSwap();
            BackgammonGameRules.SyncBoardArrayFromCheckerArrays(State);
        }

        if (TryFinalizeBearOffGameEnd())
        {
            _busy = false;
            yield break;
        }

        yield return new WaitForSeconds(postApplyDelay);
        LogAiTiming("post-apply-wait", postApplyDelay * 1000f, string.Empty);

        State.Dice1 = 0;
        State.Dice2 = 0;
        _rolledThisTurn = false;
        _legalTurns.Clear();
        InvalidateMovableHighlightCache("ai-turn-complete");
        boardManager?.ClearAllPointHighlights();
        SyncBoardForVisualState("ai-turn-complete");
        _busy = false;
        SyncMatchFromState();
        TurnsCompletedThisGame++;
        ClearUndoStackAfterTurnCompleted("ai-turn-complete");
        ResetBothDiceManagersBetweenTurns("ai-turn-complete", shouldEmitResetPickupFeedback: true);
        OnStateChanged?.Invoke();
        hud?.RefreshAll(this);
        RefreshMovableCheckerHighlights();
        if (aiTurnStopwatch != null)
        {
            aiTurnStopwatch.Stop();
            LogAiTiming(
                "ai-turn-total",
                aiTurnStopwatch.ElapsedMilliseconds,
                $"depth={BackgammonSettings.AiSearchDepth} speedStep={BackgammonSettings.GameSpeedSecondsPerStep:F2} legalAfter={_legalTurns.Count}");
        }
    }

    private static GameState CloneGameState(GameState state)
    {
        if (state == null) return null;
        return new GameState
        {
            Player1Checkers = (int[])state.Player1Checkers.Clone(),
            Player2Checkers = (int[])state.Player2Checkers.Clone(),
            CubeValue = state.CubeValue,
            CubeOwner = state.CubeOwner,
            PlayerOnRoll = state.PlayerOnRoll,
            PlayerToDecide = state.PlayerToDecide,
            Dice1 = state.Dice1,
            Dice2 = state.Dice2,
            MatchLength = state.MatchLength,
            Player1Score = state.Player1Score,
            Player2Score = state.Player2Score
        };
    }

    private static MatchState CloneMatchState(MatchState match, GameState state)
    {
        if (match == null) return null;
        var clone = new MatchState
        {
            Cube = match.Cube,
            CubeOwner = match.CubeOwner,
            PlayerOnRoll = match.PlayerOnRoll,
            IsCrawford = match.IsCrawford,
            GameState = match.GameState,
            Turn = match.Turn,
            Doubled = match.Doubled,
            Resigned = match.Resigned,
            MatchLength = match.MatchLength,
            Player0Score = match.Player0Score,
            Player1Score = match.Player1Score,
            JacobyRule = match.JacobyRule,
            BeaversAllowed = match.BeaversAllowed
        };
        if (state != null && clone.Dice != null && clone.Dice.Length >= 2)
        {
            clone.Dice[0] = state.Dice1;
            clone.Dice[1] = state.Dice2;
        }

        return clone;
    }

    private static List<Turn> CloneLegalTurns(IReadOnlyList<Turn> source)
    {
        var clone = new List<Turn>(source?.Count ?? 0);
        if (source == null)
            return clone;

        for (int i = 0; i < source.Count; i++)
        {
            Turn turn = source[i];
            if (turn == null)
                continue;
            var moves = new List<Move>(turn.Moves?.Count ?? 0);
            if (turn.Moves != null)
            {
                for (int m = 0; m < turn.Moves.Count; m++)
                    moves.Add(turn.Moves[m]);
            }

            var diceUsed = new List<int>(turn.DiceUsed?.Count ?? 0);
            if (turn.DiceUsed != null)
            {
                for (int d = 0; d < turn.DiceUsed.Count; d++)
                    diceUsed.Add(turn.DiceUsed[d]);
            }

            clone.Add(new Turn
            {
                Moves = moves,
                DiceUsed = diceUsed,
                ResultingState = turn.ResultingState
            });
        }

        return clone;
    }

    // Forwarding shims — bodies moved to BackgammonAiMoveCache
    private static bool TryGetCachedAiTurn(string k, out Turn t) => BackgammonAiMoveCache.TryGetTurn(k, out t);
    private static void CacheAiTurn(string k, Turn t) => BackgammonAiMoveCache.StoreTurn(k, t);
    private static bool TryGetCachedAiCubeOfferDecision(string k, out AiCubeDecision d) => BackgammonAiMoveCache.TryGetCubeOffer(k, out d);
    private static void CacheAiCubeOfferDecision(string k, AiCubeDecision d) => BackgammonAiMoveCache.StoreCubeOffer(k, d);
    private static bool TryGetCachedAiCubeResponseDecision(string k, out AiDoubleResponseDecision d) => BackgammonAiMoveCache.TryGetCubeResponse(k, out d);
    private static void CacheAiCubeResponseDecision(string k, AiDoubleResponseDecision d) => BackgammonAiMoveCache.StoreCubeResponse(k, d);
    private static string BuildAiMoveCacheKey(GameState s, MatchState m, int depth, SearchEngine.SearchQualityPreset q, bool prune, bool full)
        => BackgammonAiMoveCache.BuildMoveKey(s, m, depth, q, prune, full);
    private static string BuildAiCubeDecisionCacheKey(GameState s, MatchState m, string kind)
        => BackgammonAiMoveCache.BuildCubeDecisionKey(s, m, kind);

    private static Task<string> InvokeGnubgBridgeAsync(
        string matchRef,
        string gameId,
        string variation,
        bool jacoby,
        string action)
    {
        const string BridgeTypeName = "Gnubg.Unity.Runtime.Bridge.GnubgPythonBridge";
        Type bridgeType = null;
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length && bridgeType == null; i++)
        {
            bridgeType = assemblies[i].GetType(BridgeTypeName, throwOnError: false);
        }

        if (bridgeType == null)
        {
            return null;
        }

        MethodInfo runAsync = bridgeType.GetMethod(
            "RunAsync",
            BindingFlags.Public | BindingFlags.Static);
        if (runAsync == null)
        {
            return null;
        }

        object taskObj = runAsync.Invoke(
            null,
            new object[] { matchRef, gameId, variation, jacoby, action });
        return taskObj as Task<string>;
    }

    // Forwarding shims for pacing helpers — implementations moved to BackgammonAiTurnManager
    private static float GetAiPacingBaseSeconds() => BackgammonAiTurnManager.GetPacingBaseSeconds();
    private static float GetAiPreRollDelaySeconds() => BackgammonAiTurnManager.GetPreRollDelaySeconds();
    private static float GetAiPostRollRevealDelaySeconds() => BackgammonAiTurnManager.GetPostRollRevealDelaySeconds();
    private static float GetAiPostApplyDelaySeconds() => BackgammonAiTurnManager.GetPostApplyDelaySeconds();
    private static float GetAiBetweenMovesDelaySeconds() => BackgammonAiTurnManager.GetBetweenMovesDelaySeconds();

    private IEnumerator CoPlayAiTurnMovesSequentially(Turn pick)
    {
        if (pick?.Moves == null || pick.Moves.Count == 0)
            yield break;

        Stopwatch playbackStopwatch = _aiTurnManager.StartTimingStopwatch();
        float betweenMovesDelay = GetAiBetweenMovesDelaySeconds();
        Debug.Log($"[Backgammon][AI][MovePlayback] start moveCount={pick.Moves.Count} betweenDelay={betweenMovesDelay:F2}s queueDriven=true");
        _presentationQueueDrivenByCoroutine = true;

        try
        {
            for (int i = 0; i < pick.Moves.Count; i++)
            {
                Stopwatch moveStopwatch = _aiTurnManager.StartTimingStopwatch();
                Move move = pick.Moves[i];
                PlayerColor moverColor = GetVisualMoverColorForCurrentTurn();
                Debug.Log(
                    $"[Backgammon][AI][MovePlayback] apply idx={i + 1}/{pick.Moves.Count} move={move.From}->{move.To} hit={move.IsHit} moverColor={moverColor} visualPlayerOnRoll={IsPlayerOnRollVisual()} enginePlayerOnRoll={State?.PlayerOnRoll ?? -1}");

                var singleMoveTurn = new Turn
                {
                    Moves = new List<Move> { move }
                };
                GameState stateBeforeApply = State;
                // Gameplay boundary: commit rules/state first; presentation queue only visualizes that committed state.
                State = MoveGenerator.ApplyTurn(State, singleMoveTurn);
                PreservePersistentCubeState(stateBeforeApply, State, "ai-move-apply");
                BackgammonGameRules.SyncBoardArrayFromCheckerArrays(State);
                LogBearOffPositionSnapshot(move, "ai");

                bool moveEventDispatched = false;
                bool movedVisually = false;
                Checker movedChecker = null;
                EnqueueAiVisualMoveEvent(
                    move,
                    moverColor,
                    i + 1,
                    pick.Moves.Count,
                    () => moveEventDispatched = true,
                    success => movedVisually = success,
                    checker => movedChecker = checker);

                yield return WaitForAiMovePresentationCompletion(move, i + 1, pick.Moves.Count, () => moveEventDispatched, () => movedVisually, () => movedChecker);

                EmitCheckerSoundEventForAppliedMove(move);
                Debug.Log(
                    $"[Backgammon][AI][MovePlayback] emitted checker event type={ClassifyCheckerSoundEventForAppliedMove(move)} idx={i + 1}/{pick.Moves.Count}");
                if (moveStopwatch != null)
                {
                    moveStopwatch.Stop();
                    LogAiTiming("move-apply", moveStopwatch.ElapsedMilliseconds, $"idx={i + 1}/{pick.Moves.Count} from={move.From} to={move.To} hit={move.IsHit}");
                }

                if (i < pick.Moves.Count - 1)
                {
                    float elapsed = 0f;
                    while (elapsed < betweenMovesDelay)
                    {
                        float dt = TickPresentationQueueFromCoroutine();
                        elapsed += dt;
                        yield return null;
                    }
                    LogAiTiming("between-moves-wait", betweenMovesDelay * 1000f, $"idx={i + 1}/{pick.Moves.Count}");
                }
            }
        }
        finally
        {
            _presentationQueueDrivenByCoroutine = false;
        }

        Debug.Log("[Backgammon][AI][MovePlayback] complete");
        if (playbackStopwatch != null)
        {
            playbackStopwatch.Stop();
            LogAiTiming("move-playback-total", playbackStopwatch.ElapsedMilliseconds, $"moveCount={pick.Moves.Count}");
        }
    }

    private void PreservePersistentCubeState(GameState previousState, GameState nextState, string context)
    {
        if (previousState == null || nextState == null)
            return;

        int previousCubeOwner = previousState.CubeOwner;
        int previousCubeValue = previousState.CubeValue;
        if (nextState.CubeOwner != previousCubeOwner || nextState.CubeValue != previousCubeValue)
        {
            Debug.LogWarning(
                $"[Backgammon][Cube] Restoring persistent cube metadata after state apply context={context} " +
                $"before(owner={previousCubeOwner},value={previousCubeValue}) " +
                $"after(owner={nextState.CubeOwner},value={nextState.CubeValue})");
        }

        nextState.CubeOwner = previousCubeOwner;
        nextState.CubeValue = previousCubeValue;
    }

    // Forwarding shim kept so remaining inline call sites in coroutines compile unchanged.
    private void LogAiTiming(string phase, double elapsedMs, string extra)
        => _aiTurnManager?.LogAiTiming(phase, elapsedMs, extra);

    private static void LogAiCubeCacheDecision(string phase, string cacheKey, bool hit, string decisionSummary)
        => BackgammonAiMoveCache.LogCubeDecision(phase, cacheKey, hit, decisionSummary);


    private static string EscapeAgentLog(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static void WriteAgentDebugLog(string runId, string hypothesisId, string location, string message, string dataJson)
    {
        try
        {
            string payload =
                $"{{\"sessionId\":\"a6d9e6\",\"runId\":\"{EscapeAgentLog(runId)}\",\"hypothesisId\":\"{EscapeAgentLog(hypothesisId)}\",\"location\":\"{EscapeAgentLog(location)}\",\"message\":\"{EscapeAgentLog(message)}\",\"data\":{dataJson},\"timestamp\":{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}";
            File.AppendAllText("debug-a6d9e6.log", payload + Environment.NewLine);
        }
        catch
        {
            // Ignore debug logging failures.
        }
    }

    private void OnApplicationQuit()
    {
        BackgammonAiMoveCache.PersistToDisk("application-quit");
    }

    private PlayerColor GetVisualMoverColorForCurrentTurn()
    {
        return IsPlayerOnRollVisual() ? PlayerColor.White : PlayerColor.Black;
    }

    private void EnqueueAiVisualMoveEvent(
        Move move,
        PlayerColor moverColor,
        int moveIndexOneBased,
        int moveCount,
        Action onDispatched,
        Action<bool> onVisualApplyResult,
        Action<Checker> onMovedCheckerResolved)
    {
        bool isBarEntry = move.From == BackgammonBoardLayout.BarEngineIndex;
        EnqueuePresentationEvent(
            eventName: $"checker-move-visual-{moveIndexOneBased}",
            blocking: true,
            minDelaySeconds: 0f,
            clockDomain: BackgammonEventClockDomain.ScaledGameplay,
            dispatch: () =>
            {
                Debug.Log(
                    $"[Backgammon][EventQueue] checker-move-visual dispatch idx={moveIndexOneBased}/{moveCount} move={move.From}->{move.To} moverColor={moverColor} barEntry={isBarEntry} visualPlayerOnRoll={IsPlayerOnRollVisual()} enginePlayerOnRoll={State?.PlayerOnRoll ?? -1}");
                onDispatched?.Invoke();
                Checker movedChecker = null;
                bool movedVisually = boardManager != null && boardManager.TryApplySingleVisualMove(move, out movedChecker, moverColor);
                onVisualApplyResult?.Invoke(movedVisually);
                onMovedCheckerResolved?.Invoke(movedChecker);
                if (!movedVisually)
                {
                    Debug.LogWarning(
                        $"[Backgammon][AI][MovePlayback] visual fallback sync idx={moveIndexOneBased}/{moveCount} move={move.From}->{move.To} hit={move.IsHit} moverColor={moverColor} visualPlayerOnRoll={IsPlayerOnRollVisual()} enginePlayerOnRoll={State?.PlayerOnRoll ?? -1}");
                    SyncBoardForVisualState("ai-move-fallback-sync");
                    return;
                }

                Debug.Log(
                    $"[Backgammon][AI][MovePlayback] visual move applied idx={moveIndexOneBased}/{moveCount} move={move.From}->{move.To} moverColor={moverColor} barEntry={isBarEntry} movedChecker={(movedChecker != null ? movedChecker.name : "<null>")}");
                // Presentation boundary: UI refresh here reflects already-committed gameplay state; it must not mutate rules.
                OnStateChanged?.Invoke();
                hud?.RefreshAll(this);
            });
    }

    private IEnumerator WaitForAiMovePresentationCompletion(
        Move move,
        int moveIndexOneBased,
        int moveCount,
        Func<bool> wasMoveDispatched,
        Func<bool> wasVisualApplySuccessful,
        Func<Checker> resolveMovedChecker)
    {
        bool isBarEntry = move.From == BackgammonBoardLayout.BarEngineIndex;
        float dispatchTimeout = Mathf.Max(0.20f, BackgammonSettings.MoveAnimDurationSeconds * 2f);
        float elapsedDispatch = 0f;
        while (!wasMoveDispatched.Invoke() && elapsedDispatch < dispatchTimeout)
        {
            float dt = TickPresentationQueueFromCoroutine();
            elapsedDispatch += dt;
            yield return null;
        }

        if (!wasMoveDispatched.Invoke())
        {
            Debug.LogError(
                $"[Backgammon][AI][MovePlayback] queue dispatch timeout idx={moveIndexOneBased}/{moveCount} move={move.From}->{move.To} timeout={dispatchTimeout:F2}s");
            yield break;
        }

        if (!wasVisualApplySuccessful.Invoke())
            yield break;

        Checker movedChecker = resolveMovedChecker.Invoke();
        if (movedChecker == null)
        {
            if (move.From == BackgammonBoardLayout.BarEngineIndex)
            {
                Debug.LogWarning(
                    $"[Backgammon][AI][MovePlayback] bar-entry visual success without moved checker idx={moveIndexOneBased}/{moveCount} move={move.From}->{move.To}");
            }
            yield break;
        }

        float moveTimeout = Mathf.Max(0.10f, BackgammonSettings.MoveAnimDurationSeconds * 1.5f);
        if (isBarEntry)
        {
            Debug.Log(
                $"[Backgammon][AI][MovePlayback] bar-entry wait-start idx={moveIndexOneBased}/{moveCount} move={move.From}->{move.To} checker={movedChecker.name} moveAnimSeconds={BackgammonSettings.MoveAnimDurationSeconds:F3} waitTimeoutSeconds={moveTimeout:F3} checkerPos={movedChecker.transform.position}");
        }
        float elapsedMove = 0f;
        while (movedChecker.IsMoving && elapsedMove < moveTimeout)
        {
            float dt = TickPresentationQueueFromCoroutine();
            elapsedMove += dt;
            yield return null;
        }
        if (isBarEntry)
        {
            Debug.Log(
                $"[Backgammon][AI][MovePlayback] bar-entry wait-end idx={moveIndexOneBased}/{moveCount} move={move.From}->{move.To} elapsedSeconds={elapsedMove:F3} stillMoving={movedChecker.IsMoving} checkerPos={movedChecker.transform.position}");
        }
        if (movedChecker.IsMoving)
        {
            Debug.LogWarning(
                $"[Backgammon][AI][MovePlayback] checker animation wait timeout idx={moveIndexOneBased}/{moveCount} move={move.From}->{move.To} timeout={moveTimeout:F2}s");
        }
    }

    private float TickPresentationQueueFromCoroutine()
    {
        if (_presentationEventQueue == null)
            return 1f / 60f;

        _presentationEventQueue.SetGameSpeedMultiplier(GetPresentationSpeedMultiplier());
        float dt = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : (1f / 60f);
        _presentationEventQueue.Tick(dt);
        return dt;
    }

    private IEnumerator CoShouldAiOfferDoubleBeforeRoll(System.Action<bool> callback)
    {
        if (!CanCurrentPlayerOfferDouble())
        {
            callback(false);
            yield break;
        }

        if (!BackgammonPlayerRoles.IsAiTurnInOpponentAiMode(State.PlayerOnRoll))
        {
            callback(false);
            yield break;
        }

        string cacheKey = BuildAiCubeDecisionCacheKey(State, Match, "offer");
        bool hit = TryGetCachedAiCubeOfferDecision(cacheKey, out Runtime.RMC.Backgammon.Core.AiCubeDecision decision);
        bool aiEvaluated = false;
        if (!hit)
        {
            IBackgammonAIEvaluator evaluator = BackgammonAIEvaluatorFactory.GetEvaluator();
            if (evaluator != null)
            {
                AiCubeDecision result = default;
                yield return evaluator.EvaluateDoubleOfferAsync(State, Match)
                    .AsCoroutine(r => result = r);

                decision = result;
                aiEvaluated = result.FromAiEvaluator;
                CacheAiCubeOfferDecision(cacheKey, decision);
            }
        }
        if (enableAiCubeDecisionDebugLogs || !aiEvaluated)
        {
            Debug.Log(
                $"[Backgammon][AI][Cube][Offer] cacheHit={hit} evaluated={aiEvaluated} offer={decision.ShouldOffer} reason={decision.Reason} " +
                $"playerOnRoll={State.PlayerOnRoll} cube={State.CubeValue} owner={State.CubeOwner}");
        }

        callback(decision.ShouldOffer);
    }

    public void PreviewTurnHighlights(Turn turn)
    {
        boardManager?.ClearAllPointHighlights();
        if (turn == null || boardManager == null) return;
        foreach (Move m in turn.Moves)
        {
            if (m.To >= 0 && m.To < 24)
            {
                int b = BackgammonBoardLayout.EnginePointToBoardIndex(m.To);
                if (b >= 0 && b < boardManager.allPoints.Length && boardManager.allPoints[b] != null)
                    boardManager.allPoints[b].SetHighlighted(true);
            }
        }
    }

    public void ClearMovePreview()
    {
        boardManager?.ClearAllPointHighlights();
    }

    /// <summary>Debug / forced play: set dice without physics and refresh legals.</summary>
    public void DebugSetDiceAndRefresh(int d1, int d2)
    {
        if (_busy) return;
        State.Dice1 = Mathf.Clamp(d1, 1, 6);
        State.Dice2 = Mathf.Clamp(d2, 1, 6);
        _openingRollResolved = true;
        _openingRollTieAwaitingReroll = false;
        _rolledThisTurn = true;
        RollsThisGame++;
        OnDiceRolled?.Invoke(State.Dice1, State.Dice2);
        RefreshLegals();
        OnStateChanged?.Invoke();
        hud?.RefreshAll(this);
        RefreshMovableCheckerHighlights();
    }

    public void DebugForcePassTurn()
    {
        if (_busy) return;
        PassTurnNoMoves();
    }

    public void DebugSetPlayerOnRollVisual(bool playerOnRollVisual)
    {
        _isPlayerOnRollVisual = playerOnRollVisual;
        if (State != null)
            SyncBoardForVisualState("debug-set-player-on-roll-visual");
    }

    public void DebugPrintBoardConsole()
    {
        if (State == null) return;
        GameStateExtensions.PrintBoard(State);
    }

    private static string FormatMove(Move move)
    {
        return $"{move.From}->{move.To}";
    }

    private void LogBearOffPositionSnapshot(Move move, string source)
    {
        if (!enableBearOffDebugLogs || move.To != -1 || State == null)
            return;

        try
        {
            string positionId = PositionId.Encode(State);
            Debug.Log(
                $"[Backgammon][BearOffDebug] source={source} move={move.From}->{move.To} hit={move.IsHit} " +
                $"playerOnRoll={State.PlayerOnRoll} dice={State.Dice1}/{State.Dice2} cube={State.CubeValue} pid={positionId}");
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"[Backgammon][BearOffDebug] Failed to encode position after bear-off. source={source} move={move.From}->{move.To} error={ex.Message}");
        }
    }

    private void DebugLogLegalTurnFirstMoves(string sourceTag)
    {
        if (_legalTurns == null || _legalTurns.Count == 0)
        {
            Debug.Log($"[Backgammon][MoveSelect] {sourceTag}: no legal turns");
            return;
        }

        var parts = new List<string>(_legalTurns.Count);
        for (int i = 0; i < _legalTurns.Count; i++)
        {
            Turn t = _legalTurns[i];
            if (t?.Moves == null || t.Moves.Count == 0)
            {
                parts.Add($"{i}:<empty>");
                continue;
            }

            Move m = t.Moves[0];
            int distance = Mathf.Abs(m.From - m.To);
            parts.Add($"{i}:{m.From}->{m.To}(d={distance})");
        }

        Debug.Log($"[Backgammon][MoveSelect] {sourceTag}: {string.Join(", ", parts)}");
    }

    private bool TryFinalizeBearOffGameEnd()
    {
        if (State == null)
            return false;

        bool p1Won = BackgammonGameRules.HasWon(State.Player1Checkers);
        bool p2Won = BackgammonGameRules.HasWon(State.Player2Checkers);
        if (!p1Won && !p2Won)
            return false;

        int winner = p1Won ? 1 : 0;
        FinalizeGameAndQueuePresentation(winner, GameEndReason.BearOff, scoreKindOverride: null);
        return true;
    }

    private void FinalizeGameAndQueuePresentation(int winnerPlayerIndex, GameEndReason reason, GameEndScoreKind? scoreKindOverride)
    {
        if (_gameEndSequenceQueued)
            return;

        _forcedGameOver = true;
        _forcedWinnerPlayerIndex = Mathf.Clamp(winnerPlayerIndex, 0, 1);
        _gameEndSequenceQueued = true;
        _gameEndedAwaitingNextGame = true;
        _rolledThisTurn = false;
        _legalTurns.Clear();
        InvalidateMovableHighlightCache("game-end");
        boardManager?.ClearAllPointHighlights();
        _cubeNegotiator.CancelPendingOffer();
        hud?.SetDoubleOfferVisible(false);
        ClearUndoStackAfterTurnCompleted("game-end");

        int pointsAwarded = ApplyGameEndScore(_forcedWinnerPlayerIndex, reason, scoreKindOverride, out GameEndScoreKind scoreKind);

        // Notify Run Mode manager (winnerIdx, baseStake, cubeValue, gammonMultiplier)
        if (OnGameEndedWithScore != null)
        {
            int baseStakeForEvent = _moneySessionConfig?.BaseStake ?? 1;
            int cubeValEvent   = Mathf.Max(1, State?.CubeValue ?? 1);
            int gammonMultEvent = (int)scoreKind; // Single=1, Gammon=2, Backgammon=3
            OnGameEndedWithScore.Invoke(_forcedWinnerPlayerIndex, baseStakeForEvent, cubeValEvent, gammonMultEvent);
        }

        // Update Money Session score tracking
        if (_currentGameMode == GameModeType.MoneySession)
        {
            _moneySessionGamesPlayed++;
            int baseStake = _moneySessionConfig?.BaseStake ?? 1;
            int sessionPoints = baseStake * pointsAwarded; // pointsAwarded already includes cubeValue

            if (_forcedWinnerPlayerIndex == 0)
                _moneySessionPlayer1Score += sessionPoints;
            else
                _moneySessionPlayer2Score += sessionPoints;

            Debug.Log($"[MoneySession] Game {_moneySessionGamesPlayed}: P1=${_moneySessionPlayer1Score} P2=${_moneySessionPlayer2Score} (awarded {sessionPoints} to P{_forcedWinnerPlayerIndex + 1})");

            // Bank accumulates winnings for the human player (index 0)
            if (_forcedWinnerPlayerIndex == 0)
                _moneySessionBankBalance += sessionPoints;
            // PlayerStats.RecordGameEnd is called by SessionStatsTracker (observer) to avoid double-counting.
        }

        // Update ante progression for Run mode (legacy path — bypassed when BackgammonRunModeManager is subscribed)
        if (_currentGameMode == GameModeType.Run && OnGameEndedWithScore == null)
        {
            bool playerWon = _forcedWinnerPlayerIndex == 0;

            // Update run currency using the computed score kind
            int currencyGain = ComputeScoreGainForWinner(scoreKind);
            _runCurrency += currencyGain;

            // Advance ante progression
            AdvanceAnteProgression(playerWon);

            Debug.Log($"[Backgammon][Run] Game end - Currency gain: {currencyGain}, Total: {_runCurrency}, Match: {_currentMatchIndex + 1}, Ante: {_currentAnteIndex + 1}");
        }

        SyncMatchFromState();
        OnStateChanged?.Invoke();
        hud?.RefreshAll(this);
        RefreshMovableCheckerHighlights();
        string winnerText = _forcedWinnerPlayerIndex == 0 ? "Player 1 wins" : "Player 2 wins";
        _lastGameOverSummary =
            $"{winnerText} by {reason} ({scoreKind}) for {pointsAwarded} point{(pointsAwarded == 1 ? string.Empty : "s")} at {Mathf.Max(1, State?.CubeValue ?? 1)}x cube.";

        Debug.Log(
            $"[Backgammon][GameEnd] settled winner={_forcedWinnerPlayerIndex} reason={reason} scoreKind={scoreKind} points={pointsAwarded} cube={State?.CubeValue ?? 0} p1Score={State?.Player1Score ?? 0} p2Score={State?.Player2Score ?? 0}");

        var gameEndedNotification = new DiceFeedbackEventData(
            DiceFeedbackEventType.GameEnded,
            State != null ? State.CubeValue : 0,
            gameWinnerPlayerIndex: _forcedWinnerPlayerIndex,
            gamePointsAwarded: pointsAwarded,
            gameEndReason: reason.ToString());
        EmitQueuedScreenNotificationEvent(gameEndedNotification);
        EnqueuePresentationEvent(
            eventName: "game-end-popup",
            blocking: true,
            minDelaySeconds: 0f,
            clockDomain: BackgammonEventClockDomain.UnscaledReal,
            dispatch: () =>
            {
                Debug.Log($"[Backgammon][GameEnd] popup dispatch summary=\"{_lastGameOverSummary}\"");
                hud?.ShowGameOverPopup(_lastGameOverSummary);
                OnStateChanged?.Invoke();
                hud?.RefreshAll(this);
            });
    }

    private int ApplyGameEndScore(
        int winnerPlayerIndex,
        GameEndReason reason,
        GameEndScoreKind? scoreKindOverride,
        out GameEndScoreKind resolvedScoreKind)
    {
        resolvedScoreKind = scoreKindOverride ?? ResolveGameEndScoreKindForBearOff(winnerPlayerIndex);
        int cubeValue = Mathf.Max(1, State != null ? State.CubeValue : 1);
        int pointsAwarded = cubeValue * (int)resolvedScoreKind;
        if (State != null)
        {
            if (winnerPlayerIndex == 0)
                State.Player1Score += pointsAwarded;
            else
                State.Player2Score += pointsAwarded;
        }

        Debug.Log(
            $"[Backgammon][GameEnd] score applied winner={winnerPlayerIndex} reason={reason} kind={resolvedScoreKind} cube={cubeValue} points={pointsAwarded}");
        return pointsAwarded;
    }

    private GameEndScoreKind ResolveGameEndScoreKindForBearOff(int winnerPlayerIndex)
    {
        int loserIndex = OpponentIndex(winnerPlayerIndex);
        int[] loserCheckers = loserIndex == 0 ? State.Player1Checkers : State.Player2Checkers;
        if (BackgammonGameRules.IsBackgammonLoss(loserCheckers, BackgammonBoardLayout.BarEngineIndex))
            return GameEndScoreKind.Backgammon;
        if (BackgammonGameRules.IsGammonLoss(loserCheckers))
            return GameEndScoreKind.Gammon;
        return GameEndScoreKind.Single;
    }

    private readonly struct UndoFrame
    {
        public readonly int[] P1;
        public readonly int[] P2;
        public readonly int Dice1;
        public readonly int Dice2;
        public readonly bool RolledThisTurn;
        public readonly int PlayerOnRoll;
        public readonly int CubeValue;
        public readonly int CubeOwner;
        public readonly int Player1Score;
        public readonly int Player2Score;
        public readonly int TurnsCompletedThisGame;
        public readonly Turn[] LegalTurnsSnapshot;
        public readonly Move? AppliedMove;
        public readonly PlayerColor AppliedMoveColor;
        public bool HasLegalTurnsSnapshot => LegalTurnsSnapshot != null;

        private UndoFrame(int[] p1, int[] p2, int d1, int d2, bool rolled, int por, int cv, int co, int s1, int s2, int turnsCompletedThisGame, Turn[] legalTurnsSnapshot, Move? appliedMove, PlayerColor appliedMoveColor)
        {
            P1 = p1;
            P2 = p2;
            Dice1 = d1;
            Dice2 = d2;
            RolledThisTurn = rolled;
            PlayerOnRoll = por;
            CubeValue = cv;
            CubeOwner = co;
            Player1Score = s1;
            Player2Score = s2;
            TurnsCompletedThisGame = turnsCompletedThisGame;
            LegalTurnsSnapshot = legalTurnsSnapshot;
            AppliedMove = appliedMove;
            AppliedMoveColor = appliedMoveColor;
        }

        public static UndoFrame Capture(GameState s, bool rolledThisTurn, int turnsCompletedThisGame, IReadOnlyList<Turn> legalTurns, Move? appliedMove, PlayerColor appliedMoveColor)
        {
            using var captureScope = UndoCaptureFrameMarker.Auto();
            var p1 = new int[25];
            var p2 = new int[25];
            Array.Copy(s.Player1Checkers, p1, 25);
            Array.Copy(s.Player2Checkers, p2, 25);
            List<Turn> legalTurnsClone = CloneLegalTurns(legalTurns);
            Turn[] legalTurnsSnapshot = legalTurnsClone.Count == 0 ? Array.Empty<Turn>() : legalTurnsClone.ToArray();
            return new UndoFrame(p1, p2, s.Dice1, s.Dice2, rolledThisTurn, s.PlayerOnRoll, s.CubeValue, s.CubeOwner, s.Player1Score, s.Player2Score, turnsCompletedThisGame, legalTurnsSnapshot, appliedMove, appliedMoveColor);
        }

        public void ApplyTo(GameState s)
        {
            Array.Copy(P1, s.Player1Checkers, 25);
            Array.Copy(P2, s.Player2Checkers, 25);
            s.Dice1 = Dice1;
            s.Dice2 = Dice2;
            s.PlayerOnRoll = PlayerOnRoll;
            s.CubeValue = CubeValue;
            s.CubeOwner = CubeOwner;
            s.Player1Score = Player1Score;
            s.Player2Score = Player2Score;
        }
    }

    #region Ante Progression System

    /// <summary>
    /// Computes the score gain for the winner based on base stake, cube value, and game outcome.
    /// Formula: baseStake × cubeValue × resultMultiplier
    /// </summary>
    private int ComputeScoreGainForWinner(GameEndScoreKind scoreKind)
    {
        if (State == null || _anteProgression == null || _anteProgression.Count == 0)
            return 1;

        int baseStake = CurrentMatchBaseStake;
        int cubeValue = Mathf.Max(1, State.CubeValue);
        int resultMultiplier = (int)scoreKind; // 1=Single, 2=Gammon, 3=Backgammon

        return baseStake * cubeValue * resultMultiplier;
    }

    /// <summary>
    /// Computes score gain from a summary string (for test compatibility).
    /// </summary>
    private int ComputeScoreGainForWinner(string gameEndSummary)
    {
        GameEndScoreKind scoreKind = GameEndScoreKind.Single;

        if (gameEndSummary.Contains("Backgammon"))
            scoreKind = GameEndScoreKind.Backgammon;
        else if (gameEndSummary.Contains("Gammon"))
            scoreKind = GameEndScoreKind.Gammon;

        return ComputeScoreGainForWinner(scoreKind);
    }

    /// <summary>
    /// Advances the ante progression after a match is won.
    /// Checks if target score reached, advances match/ante indices, and handles run completion.
    /// </summary>
    private void AdvanceAnteProgression(bool playerWon)
    {
        if (_anteProgression == null || _currentGameMode != GameModeType.Run)
            return;

        // Increment the winner's match score
        if (playerWon)
            _player1MatchScore++;
        else
            _player2MatchScore++;

        _gamesPlayedInCurrentMatch++;

        // Check if target score reached
        int highestScore = Mathf.Max(_player1MatchScore, _player2MatchScore);
        if (highestScore >= _matchTargetScore)
        {
            // Match complete - advance to next match or ante
            _currentMatchIndex++;
            _player1MatchScore = 0;
            _player2MatchScore = 0;
            _gamesPlayedInCurrentMatch = 0;

            // Check if ante complete (all 3 matches done)
            if (_currentMatchIndex >= 3)
            {
                _currentMatchIndex = 0;
                _currentAnteIndex++;

                // Check if run complete
                if (_currentAnteIndex >= _anteProgression.Count)
                {
                    if (_shouldLoopAntes)
                    {
                        _currentAnteIndex = 0; // Loop back
                        Debug.Log("[Backgammon][Run] Ante loop complete - returning to Ante 1");
                    }
                    else
                    {
                        _runComplete = true;
                        _currentAnteIndex = _anteProgression.Count - 1; // Stay on last ante
                        _currentMatchIndex = 0;
                        Debug.Log("[Backgammon][Run] Run complete!");
                    }
                }
                else
                {
                    Debug.Log($"[Backgammon][Run] Ante complete - advancing to Ante {_currentAnteIndex + 1}");
                }
            }
            else
            {
                Debug.Log($"[Backgammon][Run] Match complete - advancing to Match {_currentMatchIndex + 1}");
            }
        }
    }

    #endregion

    #region Debug Test Methods

    /// <summary>
    /// Debug method for test configuration of ante progression.
    /// </summary>
    private void DebugConfigureAnteProgressionForTests(List<int[]> anteConfig, bool shouldLoop)
    {
        if (anteConfig == null || anteConfig.Count == 0)
        {
            // Use default single ante
            _anteProgression = new List<int[]> { new[] { 100, 100, 100 } };
        }
        else
        {
            _anteProgression = anteConfig;
        }

        _shouldLoopAntes = shouldLoop;
        _currentAnteIndex = 0;
        _currentMatchIndex = 0;
        _player1MatchScore = 0;
        _player2MatchScore = 0;
        _runCurrency = 0;
        _runComplete = false;
        _gamesPlayedInCurrentMatch = 0;
        _matchTargetScore = 1; // Default test target
        _currentGameMode = GameModeType.Run;
    }

    /// <summary>
    /// Debug method to manually set ante progression state for tests.
    /// </summary>
    private void DebugSetAnteProgressForTests(int anteIdx, int matchIdx, int p1Score, int p2Score)
    {
        _currentAnteIndex = anteIdx;
        _currentMatchIndex = matchIdx;
        _player1MatchScore = p1Score;
        _player2MatchScore = p2Score;
    }

    /// <summary>
    /// Debug method to simulate applying a game winner and advancing progression.
    /// </summary>
    private void DebugApplyWinnerToAnteProgressForTests(bool playerWon)
    {
        AdvanceAnteProgression(playerWon);
    }

    /// <summary>
    /// Debug method to configure run currency tracking for tests.
    /// </summary>
    private void DebugConfigureRunCurrencyForTests(int initialCurrency, int perGameReward, int perMatchBonus, int perAnteBonus, int unknown)
    {
        _runCurrency = initialCurrency;
        // TODO: Implement bonus tracking when tests require it
    }

    /// <summary>
    /// Debug method to configure ante progression with score arrays for tests.
    /// </summary>
    private void DebugConfigureAnteProgressionForScoreTests(List<int[]> anteConfig, List<int[]> targetScores, List<int[]> maxGames, bool shouldLoop)
    {
        DebugConfigureAnteProgressionForTests(anteConfig, shouldLoop);

        // Use first target score if provided
        if (targetScores != null && targetScores.Count > 0 && targetScores[0].Length > 0)
        {
            _matchTargetScore = targetScores[0][0];
        }
    }

    #endregion
}
