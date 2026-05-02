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

    public bool AwaitingDoubleResponse => _awaitingDoubleResponse;
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
    private bool _awaitingDoubleResponse;
    private int _doubleOfferedByPlayer;
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
    private const string AiMoveCacheVersion = "v1";
    private const int AiMoveCacheCapacity = 512;
    private const string AiCubeDecisionCacheVersion = "v1";
    private const int AiCubeDecisionCacheCapacity = 512;
    private static readonly Dictionary<string, Turn> AiMoveCache = new(StringComparer.Ordinal);
    private static readonly Queue<string> AiMoveCacheKeyOrder = new();
    private static readonly Dictionary<string, AiCubeDecision> AiCubeOfferCache = new(StringComparer.Ordinal);
    private static readonly Queue<string> AiCubeOfferCacheKeyOrder = new();
    private static readonly Dictionary<string, AiDoubleResponseDecision> AiCubeResponseCache = new(StringComparer.Ordinal);
    private static readonly Queue<string> AiCubeResponseCacheKeyOrder = new();
    private static int _aiMoveCacheHitCount;
    private static int _aiMoveCacheMissCount;
    private static int _aiCubeOfferCacheHitCount;
    private static int _aiCubeOfferCacheMissCount;
    private static int _aiCubeResponseCacheHitCount;
    private static int _aiCubeResponseCacheMissCount;
    private static bool _aiMoveCacheLoadedFromDisk;
    private static bool _aiMoveCacheLoadAttempted;

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
    private static BackgammonAiMoveCacheStorageMode _activeAiMoveCacheStorageMode = BackgammonAiMoveCacheStorageMode.None;
    private static Func<SearchEngine, GameState, MatchState, int, Task<Turn>> AiSearchTaskFactory =
        (engine, state, match, depth) => Task.Run(() => engine.GetBestTurn(state, match, depth));
    private int? _diceBufferedDie0;
    private int? _diceBufferedDie1;
    private bool _singleManagerRollInProgress;
    private int _singleManagerRollManagerIndex = -1;
    private bool _isPlayerOnRollVisual;
    private bool _aiRollInProgress;
    private int _aiRollToken;
    private int _aiActiveRollToken;
    private int _aiActiveRollManagerIndex = -1;
    private int? _aiRollBufferedDie0;
    private int? _aiRollBufferedDie1;
    private BackgammonEventQueue _presentationEventQueue;
    private bool _presentationQueueDrivenByCoroutine;
    private bool _forcedGameOver;
    private int _forcedWinnerPlayerIndex = -1;
    private bool _gameEndSequenceQueued;
    private bool _gameEndedAwaitingNextGame;
    private string _lastGameOverSummary;

    private void Awake()
    {
        ConfigureAiMoveCacheStorageMode(aiMoveCacheStorageMode);
        EnsureAiMoveCacheLoadedFromDisk();
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

    public void NewGame()
    {
        StartNewGameFromPositionId("4HPwATDgc/ABMA", "new-game-default");
    }

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
    public void StartNewGameWithConfig(GameModeType mode, MoneySessionConfig config)
    {
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
        _awaitingDoubleResponse = false;
        _undoStack.Clear();
        State = PositionId.Decode(positionId);
        _isPlayerOnRollVisual = true;
        State.Dice1 = 0;
        State.Dice2 = 0;
        _rolledThisTurn = false;
        _openingRollResolved = false;
        _openingRollTieAwaitingReroll = false;
        _diceBufferedDie0 = null;
        _diceBufferedDie1 = null;
        _singleManagerRollInProgress = false;
        _singleManagerRollManagerIndex = -1;
        _legalTurns.Clear();
        _forcedGameOver = false;
        _forcedWinnerPlayerIndex = -1;
        _gameEndSequenceQueued = false;
        _gameEndedAwaitingNextGame = false;
        _lastGameOverSummary = null;
        InvalidateMovableHighlightCache(context);
        RollsThisGame = 0;
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

    public static void ClearPersistentAiMoveCache()
    {
        AiMoveCache.Clear();
        AiMoveCacheKeyOrder.Clear();
        AiCubeOfferCache.Clear();
        AiCubeOfferCacheKeyOrder.Clear();
        AiCubeResponseCache.Clear();
        AiCubeResponseCacheKeyOrder.Clear();
        _aiMoveCacheHitCount = 0;
        _aiMoveCacheMissCount = 0;
        _aiCubeOfferCacheHitCount = 0;
        _aiCubeOfferCacheMissCount = 0;
        _aiCubeResponseCacheHitCount = 0;
        _aiCubeResponseCacheMissCount = 0;
        BackgammonAiMoveDiskCache.ClearFiles();
        Debug.Log("[Backgammon][AI][Cache] Cleared persistent move + cube caches via explicit call.");
    }

    public void RequestRollDice()
    {
        if (_busy || _rolledThisTurn || _awaitingDoubleResponse) return;
        if (!HasTwoDiceManagers()) return;

        if (!_openingRollResolved && _openingRollTieAwaitingReroll)
            ResetBothOpeningDiceManagersForReroll();

        _diceBufferedDie0 = null;
        _diceBufferedDie1 = null;
        if (!_openingRollResolved)
        {
            // Opening roll: one die per side.
            diceManagerPlayer0.SetDiceCount(1);
            diceManagerPlayer1.SetDiceCount(1);
            diceManagerPlayer0.RequestRoll();
            diceManagerPlayer1.RequestRoll();
            return;
        }

        // Normal turns: current side rolls two dice from its own manager.
        int managerIndex = IsPlayerOnRollVisual() ? 1 : 0;
        BeginSingleManagerTurnRoll(managerIndex);
    }

    private bool HasTwoDiceManagers()
    {
        return diceManagerPlayer0 != null
               && diceManagerPlayer1 != null
               && diceManagerPlayer0 != diceManagerPlayer1;
    }

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
        if (TryHandleSingleManagerTurnRollFinished(0, d1, d2))
            return;
        if (_aiRollInProgress)
        {
            HandleAiDiceRollFinished(0, d1, d2);
            return;
        }
        if (_busy) return;
        _diceBufferedDie0 = d1;
        TryCompleteBufferedDiceRoll();
    }

    private void OnDiceManagerPlayer1Finished(int d1, int d2)
    {
        if (!HasTwoDiceManagers()) return;
        if (TryHandleSingleManagerTurnRollFinished(1, d1, d2))
            return;
        if (_aiRollInProgress)
        {
            HandleAiDiceRollFinished(1, d1, d2);
            return;
        }
        if (_busy) return;
        _diceBufferedDie1 = d1;
        TryCompleteBufferedDiceRoll();
    }

    private void BeginSingleManagerTurnRoll(int managerIndex)
    {
        DiceManager active = GetDiceManagerByIndex(managerIndex);
        DiceManager inactive = GetDiceManagerByIndex(managerIndex == 0 ? 1 : 0);
        if (active == null || inactive == null) return;

        _singleManagerRollInProgress = true;
        _singleManagerRollManagerIndex = managerIndex;
        active.SetDiceCount(2);
        inactive.ResetDiceToIdleBetweenTurns();
        active.RequestRoll();
        Debug.Log($"[Backgammon][Dice] Single-side roll start managerIndex={managerIndex} manager={active.name}");
    }

    private bool TryHandleSingleManagerTurnRollFinished(int managerIndex, int d1, int d2)
    {
        if (!_singleManagerRollInProgress) return false;
        if (managerIndex != _singleManagerRollManagerIndex) return true;

        _singleManagerRollInProgress = false;
        _singleManagerRollManagerIndex = -1;
        ApplyNormalRollFromDice(d1, d2);
        return true;
    }

    private DiceManager GetDiceManagerByIndex(int managerIndex)
    {
        return managerIndex == 0 ? diceManagerPlayer0 : diceManagerPlayer1;
    }

    private void BeginAiPhysicalRoll()
    {
        if (!HasTwoDiceManagers()) return;
        _aiRollToken++;
        _aiActiveRollToken = _aiRollToken;
        _aiRollInProgress = true;
        _aiActiveRollManagerIndex = -1;
        _aiRollBufferedDie0 = null;
        _aiRollBufferedDie1 = null;
        if (_openingRollResolved)
        {
            int managerIndex = IsPlayerOnRollVisual() ? 1 : 0;
            _aiActiveRollManagerIndex = managerIndex;
            DiceManager active = GetDiceManagerByIndex(managerIndex);
            DiceManager inactive = GetDiceManagerByIndex(managerIndex == 0 ? 1 : 0);
            if (active == null || inactive == null)
            {
                _aiRollInProgress = false;
                return;
            }

            active.SetDiceCount(2);
            inactive.ResetDiceToIdleBetweenTurns();
            Debug.Log($"[Backgammon][AI][Dice] Roll start token={_aiActiveRollToken} mode=single managerIndex={managerIndex} manager={active.name}");
            active.RequestRoll();
            return;
        }

        // Opening mode fallback: one die per side.
        diceManagerPlayer0.SetDiceCount(1);
        diceManagerPlayer1.SetDiceCount(1);
        Debug.Log($"[Backgammon][AI][Dice] Roll start token={_aiActiveRollToken} mode=opening managers=({diceManagerPlayer0.name},{diceManagerPlayer1.name})");
        diceManagerPlayer0.RequestRoll();
        diceManagerPlayer1.RequestRoll();
    }

    private void HandleAiDiceRollFinished(int managerIndex, int d1, int d2)
    {
        if (!_aiRollInProgress) return;
        if (_aiActiveRollManagerIndex >= 0)
        {
            if (managerIndex != _aiActiveRollManagerIndex) return;
            _aiRollBufferedDie0 = Mathf.Clamp(d1, 1, 6);
            _aiRollBufferedDie1 = Mathf.Clamp(d2, 1, 6);
            Debug.Log(
                $"[Backgammon][AI][Dice] Dice finished token={_aiActiveRollToken} manager={managerIndex} d1={_aiRollBufferedDie0} d2={_aiRollBufferedDie1}");
            TryCompleteAiRollIfReady();
            return;
        }

        int clamped = Mathf.Clamp(d1, 1, 6);
        if (managerIndex == 0) _aiRollBufferedDie0 = clamped;
        else _aiRollBufferedDie1 = clamped;
        Debug.Log($"[Backgammon][AI][Dice] Opening die finished token={_aiActiveRollToken} manager={managerIndex} value={clamped} buffered=({_aiRollBufferedDie0?.ToString() ?? "-"}, {_aiRollBufferedDie1?.ToString() ?? "-"})");
        TryCompleteAiRollIfReady();
    }

    private void TryCompleteAiRollIfReady()
    {
        if (!_aiRollInProgress) return;
        if (!_aiRollBufferedDie0.HasValue || !_aiRollBufferedDie1.HasValue) return;
        _aiRollInProgress = false;
        Debug.Log(
            $"[Backgammon][AI][Dice] Roll complete token={_aiActiveRollToken} d1={_aiRollBufferedDie0.Value} d2={_aiRollBufferedDie1.Value}");
    }

    private void TryCompleteBufferedDiceRoll()
    {
        if (!_diceBufferedDie0.HasValue || !_diceBufferedDie1.HasValue) return;
        int v0 = _diceBufferedDie0.Value;
        int v1 = _diceBufferedDie1.Value;
        _diceBufferedDie0 = null;
        _diceBufferedDie1 = null;
        if (!_openingRollResolved)
            ApplyOpeningRollFromDice(v0, v1);
        else
            ApplyNormalRollFromDice(v0, v1);
    }

    private void ApplyNormalRollFromDice(int d1, int d2)
    {
        State.Dice1 = d1;
        State.Dice2 = d2;
        _rolledThisTurn = true;
        RollsThisGame++;
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
            EmitDiceFeedbackEvent(new DiceFeedbackEventData(
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

    private void ResetBothOpeningDiceManagersForReroll()
    {
        if (!HasTwoDiceManagers()) return;
        diceManagerPlayer0.ResetDiceForOpeningReroll();
        diceManagerPlayer1.ResetDiceForOpeningReroll();
    }

    /// <summary>Hides and re-poses 3D dice after a turn boundary so they match cleared <see cref="GameState"/> dice.</summary>
    private void ResetBothDiceManagersBetweenTurns(string context, bool shouldEmitResetPickupFeedback)
    {
        if (!HasTwoDiceManagers()) return;
        diceManagerPlayer0.ResetDiceToIdleBetweenTurns();
        diceManagerPlayer1.ResetDiceToIdleBetweenTurns();
        if (shouldEmitResetPickupFeedback)
        {
            EmitDiceFeedbackEvent(new DiceFeedbackEventData(
                DiceFeedbackEventType.GeneralDiceResetPickup,
                State != null ? State.CubeValue : 0));
            Debug.Log($"[Backgammon][DiceFeedback] Emitted generalized reset pickup feedback. context={context}");
        }
        Debug.Log($"[Backgammon][Dice] Both managers reset between turns. context={context}");
    }

    /// <summary>Shows AI roll on both dice managers (one die each) without firing roll-finished events.</summary>
    private void SyncAiRollDiceVisualsFromState()
    {
        if (!HasTwoDiceManagers()) return;
        diceManagerPlayer0.SetDiceCount(1);
        diceManagerPlayer1.SetDiceCount(1);
        diceManagerPlayer0.ApplySettledDisplayValue(State.Dice1);
        diceManagerPlayer1.ApplySettledDisplayValue(State.Dice2);
        Debug.Log(
            $"[Backgammon][Dice] AI roll visuals synced d1={State.Dice1} d2={State.Dice2} managers=({diceManagerPlayer0.name},{diceManagerPlayer1.name})");
    }

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
        _awaitingDoubleResponse = false;
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

        _doubleOfferedByPlayer = State.PlayerOnRoll;
        _awaitingDoubleResponse = true;
        SyncMatchFromState();
        hud?.SetDoubleOfferVisible(true);
        hud?.RefreshAll(this);

        int responder = OpponentIndex(_doubleOfferedByPlayer);
        if (BackgammonSettings.OpponentIsAi && responder == 0)
            StartCoroutine(CoAiRespondDouble());
    }

    public bool CanCurrentPlayerOfferDouble()
    {
        if (State == null) return false;
        if (!_openingRollResolved || _busy || IsGameOver(out _) || State.CubeValue >= 64 || _awaitingDoubleResponse || _rolledThisTurn)
            return false;

        int cubeOwner = State.CubeOwner;
        bool cubeIsCentered = cubeOwner == 3 || cubeOwner < 0;
        bool cubeOwnedByCurrentPlayer = cubeOwner == State.PlayerOnRoll;
        bool cubeOwnedByLocalPlayer = cubeOwner == BackgammonPlayerRoles.LocalPlayerIndex;
        bool localCanOfferByOwnership = cubeIsCentered || cubeOwnedByLocalPlayer;
        return (cubeIsCentered || cubeOwnedByCurrentPlayer) && localCanOfferByOwnership;
    }

    public void RespondDoubleTake()
    {
        if (!_awaitingDoubleResponse || _busy) return;

        int newVal = Mathf.Min(64, State.CubeValue * 2);
        State.CubeValue = newVal;
        int responder = OpponentIndex(_doubleOfferedByPlayer);
        State.CubeOwner = responder;
        Match.Cube = newVal;
        Match.CubeOwner = responder;
        Match.Doubled = true;
        _awaitingDoubleResponse = false;
        hud?.SetDoubleOfferVisible(false);
        OnStateChanged?.Invoke();
        hud?.RefreshAll(this);
        // Show notification first, then cube animation
        EmitQueuedScreenNotificationEvent(new DiceFeedbackEventData(
            DiceFeedbackEventType.CubeValueChanged,
            State != null ? State.CubeValue : 0));
        EnqueueCubeRotatedMarkerEvent("double-take");
    }

    public void RespondDoubleDrop()
    {
        if (!_awaitingDoubleResponse || _busy) return;
        _awaitingDoubleResponse = false;
        hud?.SetDoubleOfferVisible(false);
        int winner = _doubleOfferedByPlayer;
        FinalizeGameAndQueuePresentation(
            winnerPlayerIndex: winner,
            reason: GameEndReason.DoubleDrop,
            scoreKindOverride: GameEndScoreKind.Single);
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
        if (!_awaitingDoubleResponse) yield break;
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
        Stopwatch aiTurnStopwatch = enableAiTimingLogs ? Stopwatch.StartNew() : null;
        float preRollDelay = GetAiPreRollDelaySeconds();
        float postRollRevealDelay = GetAiPostRollRevealDelaySeconds();
        float postApplyDelay = GetAiPostApplyDelaySeconds();
        Debug.Log(
            $"[Backgammon][AI] pacing speedStep={BackgammonSettings.GameSpeedSecondsPerStep:F2} preRoll={preRollDelay:F2}s postRollReveal={postRollRevealDelay:F2}s postApply={postApplyDelay:F2}s");
        yield return new WaitForSeconds(preRollDelay);
        LogAiTiming("pre-roll-wait", preRollDelay * 1000f, $"depth={BackgammonSettings.AiSearchDepth} speedStep={BackgammonSettings.GameSpeedSecondsPerStep:F2}");

        bool shouldOfferDouble = false;
        yield return CoShouldAiOfferDoubleBeforeRoll(result => shouldOfferDouble = result);

        if (shouldOfferDouble)
        {
            Debug.Log("[Backgammon][AI] Offering double before roll (human responds).");
            OfferDouble();
            float waitSeconds = 0f;
            while (_awaitingDoubleResponse && waitSeconds < 120f && !IsGameOver(out _))
            {
                waitSeconds += Time.deltaTime;
                yield return null;
            }

            if (_awaitingDoubleResponse && !IsGameOver(out _))
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
                float timeoutSeconds = Mathf.Clamp(GetAiPacingBaseSeconds() * 8f, 0.5f, 6f);
                float waited = 0f;
                while (_aiRollInProgress && waited < timeoutSeconds)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }
                LogAiTiming("dice-roll-wait", waited * 1000f, $"timeoutMs={timeoutSeconds * 1000f:F0} token={_aiActiveRollToken}");

                if (_aiRollInProgress)
                {
                    _aiRollInProgress = false;
                    Debug.LogWarning(
                        $"[Backgammon][AI][Dice] Roll timeout token={_aiActiveRollToken} waited={waited:F2}s fallback=random");
                }
            }

            if (_aiRollBufferedDie0.HasValue && _aiRollBufferedDie1.HasValue)
            {
                State.Dice1 = _aiRollBufferedDie0.Value;
                State.Dice2 = _aiRollBufferedDie1.Value;
            }
            else
            {
                State.Dice1 = UnityEngine.Random.Range(1, 7);
                State.Dice2 = UnityEngine.Random.Range(1, 7);
                Debug.LogWarning(
                    $"[Backgammon][AI][Dice] Missing physical roll result token={_aiActiveRollToken}; using fallback d1={State.Dice1} d2={State.Dice2}");
            }

            _rolledThisTurn = true;
            RollsThisGame++;
            _aiActiveRollManagerIndex = -1;
            _aiRollBufferedDie0 = null;
            _aiRollBufferedDie1 = null;
        }
        RefreshLegals();
        OnStateChanged?.Invoke();
        hud?.RefreshAll(this);
        yield return new WaitForSeconds(postRollRevealDelay);
        LogAiTiming("post-roll-reveal-wait", postRollRevealDelay * 1000f, $"legalTurns={_legalTurns.Count}");

        if (_legalTurns.Count == 0)
        {
            Debug.Log("[Backgammon][AI] No legal moves available; skipping AI evaluation.");
            PassTurnNoMoves();
            if (aiTurnStopwatch != null)
            {
                aiTurnStopwatch.Stop();
                LogAiTiming("total-ai-turn", aiTurnStopwatch.ElapsedMilliseconds, "reason=no-legal-moves");
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
            Stopwatch searchStopwatch = enableAiTimingLogs ? Stopwatch.StartNew() : null;
            GameState stateSnapshot = CloneGameState(State);
            MatchState matchSnapshot = CloneMatchState(Match, State);
            string cacheKey = BuildAiMoveCacheKey(
                stateSnapshot,
                matchSnapshot,
                BackgammonSettings.AiSearchDepth,
                SearchEngine.SearchQualityPreset.Balanced,
                true,
                false);
            if (TryGetCachedAiTurn(cacheKey, out Turn cachedPick))
            {
                pick = cachedPick;
                LogAiCacheDecision("get", cacheKey, hit: true);
            }
            else
            {
                LogAiCacheDecision("get", cacheKey, hit: false);
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
                        CacheAiTurn(cacheKey, pick);
                        LogAiCacheDecision("store", cacheKey, hit: false);
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
            Stopwatch playbackStopwatch = enableAiTimingLogs ? Stopwatch.StartNew() : null;
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

    private static bool TryGetCachedAiTurn(string cacheKey, out Turn cachedTurn)
    {
        cachedTurn = null;
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            _aiMoveCacheMissCount++;
            return false;
        }

        if (!AiMoveCache.TryGetValue(cacheKey, out Turn stored))
        {
            _aiMoveCacheMissCount++;
            return false;
        }

        cachedTurn = CloneTurn(stored);
        _aiMoveCacheHitCount++;
        return cachedTurn != null;
    }

    private static void CacheAiTurn(string cacheKey, Turn turn)
    {
        if (string.IsNullOrWhiteSpace(cacheKey) || turn == null || turn.Moves == null || turn.ResultingState == null)
        {
            return;
        }

        AiMoveCache[cacheKey] = CloneTurn(turn);
        AiMoveCacheKeyOrder.Enqueue(cacheKey);
        while (AiMoveCache.Count > AiMoveCacheCapacity && AiMoveCacheKeyOrder.Count > 0)
        {
            string evicted = AiMoveCacheKeyOrder.Dequeue();
            if (!AiMoveCache.ContainsKey(evicted))
            {
                continue;
            }

            // Keep last write for duplicate keys while preserving bounded memory.
            if (!string.Equals(evicted, cacheKey, StringComparison.Ordinal))
            {
                AiMoveCache.Remove(evicted);
            }
        }

        PersistAiMoveCacheToDisk("store");
    }

    private static bool TryGetCachedAiCubeOfferDecision(string cacheKey, out AiCubeDecision decision)
    {
        decision = default;
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            _aiCubeOfferCacheMissCount++;
            // #region agent log
            WriteAgentDebugLog(
                "run1",
                "H3",
                "BackgammonGameController.TryGetCachedAiCubeOfferDecision:emptyKey",
                "offer cache miss due empty key",
                "{\"cacheKeyEmpty\":true}");
            // #endregion
            return false;
        }

        if (!AiCubeOfferCache.TryGetValue(cacheKey, out decision))
        {
            _aiCubeOfferCacheMissCount++;
            // #region agent log
            WriteAgentDebugLog(
                "run1",
                "H3",
                "BackgammonGameController.TryGetCachedAiCubeOfferDecision:miss",
                "offer cache miss",
                $"{{\"cacheKeyHash\":\"{cacheKey.GetHashCode():X8}\",\"size\":{AiCubeOfferCache.Count}}}");
            // #endregion
            LogAiCubeCacheDecision("offer-get", cacheKey, hit: false, decisionSummary: "none");
            return false;
        }

        _aiCubeOfferCacheHitCount++;
        // #region agent log
        WriteAgentDebugLog(
            "run1",
            "H3",
            "BackgammonGameController.TryGetCachedAiCubeOfferDecision:hit",
            "offer cache hit",
            $"{{\"cacheKeyHash\":\"{cacheKey.GetHashCode():X8}\",\"size\":{AiCubeOfferCache.Count},\"shouldOffer\":{decision.ShouldOffer.ToString().ToLowerInvariant()}}}");
        // #endregion
        LogAiCubeCacheDecision("offer-get", cacheKey, hit: true, decisionSummary: $"offer={decision.ShouldOffer}");
        return true;
    }

    private static void CacheAiCubeOfferDecision(string cacheKey, AiCubeDecision decision)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
            return;

        AiCubeOfferCache[cacheKey] = decision;
        AiCubeOfferCacheKeyOrder.Enqueue(cacheKey);
        while (AiCubeOfferCache.Count > AiCubeDecisionCacheCapacity && AiCubeOfferCacheKeyOrder.Count > 0)
        {
            string evicted = AiCubeOfferCacheKeyOrder.Dequeue();
            if (!AiCubeOfferCache.ContainsKey(evicted))
                continue;
            if (!string.Equals(evicted, cacheKey, StringComparison.Ordinal))
                AiCubeOfferCache.Remove(evicted);
        }

        LogAiCubeCacheDecision("offer-store", cacheKey, hit: false, decisionSummary: $"offer={decision.ShouldOffer}");
        PersistAiMoveCacheToDisk("cube-offer-store");
    }

    private static bool TryGetCachedAiCubeResponseDecision(string cacheKey, out AiDoubleResponseDecision decision)
    {
        decision = default;
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            _aiCubeResponseCacheMissCount++;
            return false;
        }

        if (!AiCubeResponseCache.TryGetValue(cacheKey, out decision))
        {
            _aiCubeResponseCacheMissCount++;
            LogAiCubeCacheDecision("response-get", cacheKey, hit: false, decisionSummary: "none");
            return false;
        }

        _aiCubeResponseCacheHitCount++;
        LogAiCubeCacheDecision("response-get", cacheKey, hit: true, decisionSummary: $"action={decision.Action}");
        return true;
    }

    private static void CacheAiCubeResponseDecision(string cacheKey, AiDoubleResponseDecision decision)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
            return;

        AiCubeResponseCache[cacheKey] = decision;
        AiCubeResponseCacheKeyOrder.Enqueue(cacheKey);
        while (AiCubeResponseCache.Count > AiCubeDecisionCacheCapacity && AiCubeResponseCacheKeyOrder.Count > 0)
        {
            string evicted = AiCubeResponseCacheKeyOrder.Dequeue();
            if (!AiCubeResponseCache.ContainsKey(evicted))
                continue;
            if (!string.Equals(evicted, cacheKey, StringComparison.Ordinal))
                AiCubeResponseCache.Remove(evicted);
        }

        LogAiCubeCacheDecision("response-store", cacheKey, hit: false, decisionSummary: $"action={decision.Action}");
        PersistAiMoveCacheToDisk("cube-response-store");
    }

    private static Turn CloneTurn(Turn turn)
    {
        if (turn == null || turn.Moves == null || turn.ResultingState == null)
        {
            return null;
        }

        var clonedMoves = new List<Move>(turn.Moves.Count);
        for (int i = 0; i < turn.Moves.Count; i++)
        {
            Move move = turn.Moves[i];
            clonedMoves.Add(new Move
            {
                From = move.From,
                To = move.To,
                IsHit = move.IsHit
            });
        }

        return new Turn
        {
            Moves = clonedMoves,
            ResultingState = CloneGameState(turn.ResultingState)
        };
    }

    private static string BuildAiMoveCacheKey(
        GameState state,
        MatchState match,
        int depth,
        SearchEngine.SearchQualityPreset qualityPreset,
        bool stagedPruneFilteringEnabled,
        bool forceFullTurnEvaluation)
    {
        if (state == null || match == null)
        {
            return string.Empty;
        }

        string snapshotId = BuildGnubgSnapshotId(state, match);
        if (string.IsNullOrWhiteSpace(snapshotId))
        {
            return string.Empty;
        }

        return $"{AiMoveCacheVersion}:{snapshotId}:d={depth}:q={(int)qualityPreset}:prune={stagedPruneFilteringEnabled}:full={forceFullTurnEvaluation}";
    }

    private static string BuildAiCubeDecisionCacheKey(GameState state, MatchState match, string decisionKind)
    {
        if (state == null || match == null || string.IsNullOrWhiteSpace(decisionKind))
        {
            // #region agent log
            WriteAgentDebugLog(
                "run1",
                "H2",
                "BackgammonGameController.BuildAiCubeDecisionCacheKey:nullInput",
                "cube cache key skipped due invalid input",
                $"{{\"hasState\":{(state != null).ToString().ToLowerInvariant()},\"hasMatch\":{(match != null).ToString().ToLowerInvariant()},\"decisionKind\":\"{EscapeAgentLog(decisionKind)}\"}}");
            // #endregion
            return string.Empty;
        }

        string snapshotId = BuildGnubgSnapshotId(state, match);
        if (string.IsNullOrWhiteSpace(snapshotId))
        {
            // #region agent log
            WriteAgentDebugLog(
                "run1",
                "H2",
                "BackgammonGameController.BuildAiCubeDecisionCacheKey:emptySnapshot",
                "cube cache key skipped due empty snapshot",
                $"{{\"decisionKind\":\"{EscapeAgentLog(decisionKind)}\",\"playerOnRoll\":{state.PlayerOnRoll},\"cubeValue\":{state.CubeValue},\"cubeOwner\":{state.CubeOwner}}}");
            // #endregion
            return string.Empty;
        }

        return $"{AiCubeDecisionCacheVersion}:{decisionKind}:{snapshotId}";
    }

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

    private static string BuildGnubgSnapshotId(GameState state, MatchState match)
    {
        if (state == null || match == null)
        {
            return string.Empty;
        }

        var oracleMatch = new MatchState
        {
            Cube = match.Cube,
            CubeOwner = match.CubeOwner,
            PlayerOnRoll = state.PlayerOnRoll,
            IsCrawford = match.IsCrawford,
            GameState = 0,
            Turn = 0,
            Doubled = false,
            Resigned = 0,
            MatchLength = match.MatchLength,
            Player0Score = match.Player0Score,
            Player1Score = match.Player1Score,
            JacobyRule = match.JacobyRule,
            BeaversAllowed = match.BeaversAllowed
        };
        oracleMatch.Dice[0] = state.Dice1;
        oracleMatch.Dice[1] = state.Dice2;
        return $"{PositionId.Encode(state)}:{MatchId.Encode(oracleMatch)}";
    }

    private static float GetAiPacingBaseSeconds()
    {
        return Mathf.Clamp(BackgammonSettings.GameSpeedSecondsPerStep, 0.05f, 2f);
    }

    private static float GetAiPreRollDelaySeconds()
    {
        return Mathf.Clamp(GetAiPacingBaseSeconds() * 1.0f, 0.05f, 2.5f);
    }

    private static float GetAiPostRollRevealDelaySeconds()
    {
        return Mathf.Clamp(GetAiPacingBaseSeconds() * 0.8f, 0.05f, 2.0f);
    }

    private static float GetAiPostApplyDelaySeconds()
    {
        return Mathf.Clamp(GetAiPacingBaseSeconds() * 0.6f, 0.05f, 1.5f);
    }

    private static float GetAiBetweenMovesDelaySeconds()
    {
        return Mathf.Clamp(GetAiPacingBaseSeconds() * 0.5f, 0.03f, 1.0f);
    }

    private IEnumerator CoPlayAiTurnMovesSequentially(Turn pick)
    {
        if (pick?.Moves == null || pick.Moves.Count == 0)
            yield break;

        Stopwatch playbackStopwatch = enableAiTimingLogs ? Stopwatch.StartNew() : null;
        float betweenMovesDelay = GetAiBetweenMovesDelaySeconds();
        Debug.Log($"[Backgammon][AI][MovePlayback] start moveCount={pick.Moves.Count} betweenDelay={betweenMovesDelay:F2}s queueDriven=true");
        _presentationQueueDrivenByCoroutine = true;

        try
        {
            for (int i = 0; i < pick.Moves.Count; i++)
            {
                Stopwatch moveStopwatch = enableAiTimingLogs ? Stopwatch.StartNew() : null;
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

    private static string BuildAiTimingLogLine(string phase, double elapsedMs, string extra)
    {
        string suffix = string.IsNullOrWhiteSpace(extra) ? string.Empty : $" {extra}";
        return $"[Backgammon][AI][Timing] phase={phase} ms={elapsedMs:F1}{suffix}";
    }

    private void LogAiTiming(string phase, double elapsedMs, string extra)
    {
        if (!enableAiTimingLogs) return;
        Debug.Log(BuildAiTimingLogLine(phase, elapsedMs, extra));
    }

    private void LogAiCacheDecision(string phase, string cacheKey, bool hit)
    {
        if (!enableAiCacheDebugLogs)
            return;

        Debug.Log(
            $"[Backgammon][AI][Cache] phase={phase} hit={hit} key={cacheKey} hits={_aiMoveCacheHitCount} misses={_aiMoveCacheMissCount} size={AiMoveCache.Count}");
    }

    private static void LogAiCubeCacheDecision(string phase, string cacheKey, bool hit, string decisionSummary)
    {
        string compactKey = string.IsNullOrWhiteSpace(cacheKey) ? "<none>" : cacheKey.GetHashCode().ToString("X8");
        Debug.Log(
            $"[Backgammon][AI][Cube][Cache] phase={phase} hit={hit} keyHash={compactKey} decision={decisionSummary} " +
            $"offerHits={_aiCubeOfferCacheHitCount} offerMisses={_aiCubeOfferCacheMissCount} offerSize={AiCubeOfferCache.Count} " +
            $"responseHits={_aiCubeResponseCacheHitCount} responseMisses={_aiCubeResponseCacheMissCount} responseSize={AiCubeResponseCache.Count}");
    }

    private static void ConfigureAiMoveCacheStorageMode(BackgammonAiMoveCacheStorageMode mode)
    {
        _activeAiMoveCacheStorageMode = mode;
    }

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

    private static void EnsureAiMoveCacheLoadedFromDisk()
    {
        if (_aiMoveCacheLoadAttempted)
            return;
        _aiMoveCacheLoadAttempted = true;
        // #region agent log
        WriteAgentDebugLog(
            "run1",
            "H1",
            "BackgammonGameController.EnsureAiMoveCacheLoadedFromDisk:entry",
            "startup cache load begin",
            $"{{\"mode\":\"{_activeAiMoveCacheStorageMode}\",\"attempted\":{_aiMoveCacheLoadAttempted.ToString().ToLowerInvariant()}}}");
        // #endregion

        if (_activeAiMoveCacheStorageMode == BackgammonAiMoveCacheStorageMode.None)
        {
            Debug.Log("[Backgammon][AI][Cache] Startup mode=memory-only loaded=0 discarded=0");
            return;
        }

        var orderedKeys = new List<string>();
        var orderedCubeOfferKeys = new List<string>();
        var orderedCubeResponseKeys = new List<string>();
        bool loadedOk = BackgammonAiMoveDiskCache.TryLoad(
            _activeAiMoveCacheStorageMode,
            orderedKeys,
            AiMoveCache,
            out int loadedCount,
            out int discardedCount,
            out string message,
            orderedCubeOfferKeys,
            AiCubeOfferCache,
            orderedCubeResponseKeys,
            AiCubeResponseCache,
            out int loadedCubeOfferCount,
            out int discardedCubeOfferCount,
            out int loadedCubeResponseCount,
            out int discardedCubeResponseCount);
        // #region agent log
        WriteAgentDebugLog(
            "run1",
            "H1",
            "BackgammonGameController.EnsureAiMoveCacheLoadedFromDisk:afterLoad",
            "startup cache load complete",
            $"{{\"loadedOk\":{loadedOk.ToString().ToLowerInvariant()},\"moveLoaded\":{loadedCount},\"moveDiscarded\":{discardedCount},\"offerLoaded\":{loadedCubeOfferCount},\"offerDiscarded\":{discardedCubeOfferCount},\"responseLoaded\":{loadedCubeResponseCount},\"responseDiscarded\":{discardedCubeResponseCount},\"state\":\"{EscapeAgentLog(message)}\"}}");
        // #endregion

        AiMoveCacheKeyOrder.Clear();
        for (int i = 0; i < orderedKeys.Count; i++)
            AiMoveCacheKeyOrder.Enqueue(orderedKeys[i]);
        AiCubeOfferCacheKeyOrder.Clear();
        for (int i = 0; i < orderedCubeOfferKeys.Count; i++)
            AiCubeOfferCacheKeyOrder.Enqueue(orderedCubeOfferKeys[i]);
        AiCubeResponseCacheKeyOrder.Clear();
        for (int i = 0; i < orderedCubeResponseKeys.Count; i++)
            AiCubeResponseCacheKeyOrder.Enqueue(orderedCubeResponseKeys[i]);

        _aiMoveCacheLoadedFromDisk = loadedOk;
        string path = BackgammonAiMoveDiskCache.GetCachePath(_activeAiMoveCacheStorageMode);
        string modeLabel = _activeAiMoveCacheStorageMode == BackgammonAiMoveCacheStorageMode.Json ? "disk-json" : "disk-binary";
        if (loadedOk)
        {
            Debug.Log($"[Backgammon][AI][Cache] Startup mode={modeLabel} path={path} loaded={loadedCount} discarded={discardedCount} size={AiMoveCache.Count} state={message}");
            Debug.Log($"[Backgammon][AI][Cube][Cache] Startup mode={modeLabel} path={path} offerLoaded={loadedCubeOfferCount} offerDiscarded={discardedCubeOfferCount} offerSize={AiCubeOfferCache.Count} responseLoaded={loadedCubeResponseCount} responseDiscarded={discardedCubeResponseCount} responseSize={AiCubeResponseCache.Count} state={message}");
        }
        else
        {
            Debug.LogWarning($"[Backgammon][AI][Cache] Startup load failed mode={modeLabel} path={path} loaded={loadedCount} discarded={discardedCount} state={message}; using memory cache.");
            Debug.LogWarning($"[Backgammon][AI][Cube][Cache] Startup load failed mode={modeLabel} path={path} offerLoaded={loadedCubeOfferCount} offerDiscarded={discardedCubeOfferCount} responseLoaded={loadedCubeResponseCount} responseDiscarded={discardedCubeResponseCount} state={message}; using memory cache.");
        }
    }

    private static void PersistAiMoveCacheToDisk(string context)
    {
        if (_activeAiMoveCacheStorageMode == BackgammonAiMoveCacheStorageMode.None)
            return;

        var ordered = BuildOrderedAiCacheEntries();
        var orderedCubeOffer = BuildOrderedAiCubeOfferCacheEntries();
        var orderedCubeResponse = BuildOrderedAiCubeResponseCacheEntries();
        bool saved = BackgammonAiMoveDiskCache.TrySave(_activeAiMoveCacheStorageMode, ordered, out string message, orderedCubeOffer, orderedCubeResponse);
        if (!saved)
        {
            string path = BackgammonAiMoveDiskCache.GetCachePath(_activeAiMoveCacheStorageMode);
            Debug.LogWarning($"[Backgammon][AI][Cache] Persist failed context={context} path={path} state={message}");
            Debug.LogWarning($"[Backgammon][AI][Cube][Cache] Persist failed context={context} path={path} state={message}");
        }
    }

    private static List<KeyValuePair<string, Turn>> BuildOrderedAiCacheEntries()
    {
        var ordered = new List<KeyValuePair<string, Turn>>(AiMoveCache.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string key in AiMoveCacheKeyOrder)
        {
            if (string.IsNullOrWhiteSpace(key) || seen.Contains(key))
                continue;
            if (!AiMoveCache.TryGetValue(key, out Turn turn) || turn == null)
                continue;
            seen.Add(key);
            ordered.Add(new KeyValuePair<string, Turn>(key, CloneTurn(turn)));
        }

        foreach (KeyValuePair<string, Turn> kvp in AiMoveCache)
        {
            if (seen.Contains(kvp.Key) || kvp.Value == null)
                continue;
            seen.Add(kvp.Key);
            ordered.Add(new KeyValuePair<string, Turn>(kvp.Key, CloneTurn(kvp.Value)));
        }

        return ordered;
    }

    private static List<KeyValuePair<string, AiCubeDecision>> BuildOrderedAiCubeOfferCacheEntries()
    {
        var ordered = new List<KeyValuePair<string, AiCubeDecision>>(AiCubeOfferCache.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string key in AiCubeOfferCacheKeyOrder)
        {
            if (string.IsNullOrWhiteSpace(key) || seen.Contains(key))
                continue;
            if (!AiCubeOfferCache.TryGetValue(key, out AiCubeDecision decision))
                continue;
            seen.Add(key);
            ordered.Add(new KeyValuePair<string, AiCubeDecision>(key, decision));
        }

        foreach (KeyValuePair<string, AiCubeDecision> kvp in AiCubeOfferCache)
        {
            if (seen.Contains(kvp.Key))
                continue;
            seen.Add(kvp.Key);
            ordered.Add(new KeyValuePair<string, AiCubeDecision>(kvp.Key, kvp.Value));
        }

        return ordered;
    }

    private static List<KeyValuePair<string, AiDoubleResponseDecision>> BuildOrderedAiCubeResponseCacheEntries()
    {
        var ordered = new List<KeyValuePair<string, AiDoubleResponseDecision>>(AiCubeResponseCache.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string key in AiCubeResponseCacheKeyOrder)
        {
            if (string.IsNullOrWhiteSpace(key) || seen.Contains(key))
                continue;
            if (!AiCubeResponseCache.TryGetValue(key, out AiDoubleResponseDecision decision))
                continue;
            seen.Add(key);
            ordered.Add(new KeyValuePair<string, AiDoubleResponseDecision>(key, decision));
        }

        foreach (KeyValuePair<string, AiDoubleResponseDecision> kvp in AiCubeResponseCache)
        {
            if (seen.Contains(kvp.Key))
                continue;
            seen.Add(kvp.Key);
            ordered.Add(new KeyValuePair<string, AiDoubleResponseDecision>(kvp.Key, kvp.Value));
        }

        return ordered;
    }

    private void OnApplicationQuit()
    {
        PersistAiMoveCacheToDisk("application-quit");
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
        _awaitingDoubleResponse = false;
        hud?.SetDoubleOfferVisible(false);
        ClearUndoStackAfterTurnCompleted("game-end");

        int pointsAwarded = ApplyGameEndScore(_forcedWinnerPlayerIndex, reason, scoreKindOverride, out GameEndScoreKind scoreKind);

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
        }

        // Update ante progression for Run mode
        if (_currentGameMode == GameModeType.Run)
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
