using System;
using System.Collections;
using System.Collections.Generic;
using EngineCore;
using Runtime.RMC._MyProject_.Dice;
using Runtime.RMC.Backgammon.Core;
using Runtime.RMC.Backgammon.Settings;
using UnityEngine;

/// <summary>
/// Owns <see cref="GameState"/>, dice → legals → apply → swap (EngineCLI convention).
/// </summary>
[DefaultExecutionOrder(-40)]
public class BackgammonGameController : MonoBehaviour
{
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private DiceManager diceManager;
    [Header("Opening roll (optional)")]
    [Tooltip("When both are set, opening uses one die each; first index = player 0, second = player 1.")]
    [SerializeField] private DiceManager diceManagerOpeningPlayer0;
    [SerializeField] private DiceManager diceManagerOpeningPlayer1;
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

    public int RollsThisGame { get; private set; }

    /// <summary>Increments each <see cref="NewGame"/> (session counter).</summary>
    public int GameRoundIndex { get; private set; }

    /// <summary>Completed turn boundaries this game (move applied or pass).</summary>
    public int TurnsCompletedThisGame { get; private set; }

    public bool AwaitingDoubleResponse => _awaitingDoubleResponse;

    /// <summary>False until the first opening roll is resolved (non-tie). After that, normal turns use two dice together.</summary>
    public bool OpeningRollResolved => _openingRollResolved;

    /// <summary>Last opening roll was a tie; player should roll again.</summary>
    public bool OpeningRollAwaitingReroll => !_openingRollResolved && _openingRollTieAwaitingReroll;

    public event Action OnStateChanged;

    private readonly List<Turn> _legalTurns = new();
    private readonly Stack<UndoFrame> _undoStack = new();
    private bool _rolledThisTurn;
    private bool _busy;
    private bool _awaitingDoubleResponse;
    private int _doubleOfferedByPlayer;
    private bool _openingRollResolved;
    private bool _openingRollTieAwaitingReroll;
    private int? _openingBufferedDie0;
    private int? _openingBufferedDie1;

    private void Awake()
    {
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
        if (diceManager == null && boardManager != null)
        {
            DiceManager[] found = boardManager.GetComponentsInChildren<DiceManager>(true);
            if (found != null)
            {
                foreach (DiceManager dm in found)
                {
                    if (dm == diceManagerOpeningPlayer0 || dm == diceManagerOpeningPlayer1) continue;
                    diceManager = dm;
                    break;
                }
            }
        }
        if (diceManager == null)
        {
            DiceManager[] all = FindObjectsByType<DiceManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (DiceManager dm in all)
            {
                if (dm == diceManagerOpeningPlayer0 || dm == diceManagerOpeningPlayer1) continue;
                diceManager = dm;
                break;
            }
        }
        if (diceManager != null && rollDiceViaHudOnly)
            diceManager.SuppressRollButtonBinding = true;
        if (diceManagerOpeningPlayer0 != null && rollDiceViaHudOnly)
            diceManagerOpeningPlayer0.SuppressRollButtonBinding = true;
        if (diceManagerOpeningPlayer1 != null && rollDiceViaHudOnly)
            diceManagerOpeningPlayer1.SuppressRollButtonBinding = true;
        if (diceManager != null)
            diceManager.OnDiceRollFinished += OnDiceRollFinished;
        if (diceManagerOpeningPlayer0 != null)
            diceManagerOpeningPlayer0.OnDiceRollFinished += OnOpeningDicePlayer0Finished;
        if (diceManagerOpeningPlayer1 != null)
            diceManagerOpeningPlayer1.OnDiceRollFinished += OnOpeningDicePlayer1Finished;
        NewGame();
    }

    private void OnDestroy()
    {
        if (diceManager != null)
            diceManager.OnDiceRollFinished -= OnDiceRollFinished;
        if (diceManagerOpeningPlayer0 != null)
            diceManagerOpeningPlayer0.OnDiceRollFinished -= OnOpeningDicePlayer0Finished;
        if (diceManagerOpeningPlayer1 != null)
            diceManagerOpeningPlayer1.OnDiceRollFinished -= OnOpeningDicePlayer1Finished;
    }

    public void NewGame()
    {
        StopAllCoroutines();
        BackgammonAIService.ClearSearchEngineCache();
        _busy = false;
        _awaitingDoubleResponse = false;
        _undoStack.Clear();
        State = PositionId.Decode("4HPwATDgc/ABMA");
        State.Dice1 = 0;
        State.Dice2 = 0;
        _rolledThisTurn = false;
        _openingRollResolved = false;
        _openingRollTieAwaitingReroll = false;
        _openingBufferedDie0 = null;
        _openingBufferedDie1 = null;
        _legalTurns.Clear();
        RollsThisGame = 0;
        TurnsCompletedThisGame = 0;
        GameRoundIndex++;
        BackgammonGameRules.SyncBoardArrayFromCheckerArrays(State);
        SyncMatchFromState();
        boardManager?.ClearAllPointHighlights();
        boardManager?.EnsureBoardGenerated();
        boardManager?.SyncCheckersFromGameState(State);
        hud?.SetDoubleOfferVisible(false);
        OnStateChanged?.Invoke();
        hud?.RefreshAll(this);
    }

    public void RequestRollDice()
    {
        if (_busy || _rolledThisTurn || _awaitingDoubleResponse) return;

        if (!_openingRollResolved && UsesDualOpeningDiceManagers())
        {
            _openingBufferedDie0 = null;
            _openingBufferedDie1 = null;
            diceManagerOpeningPlayer0.SetDiceCount(1);
            diceManagerOpeningPlayer1.SetDiceCount(1);
            diceManagerOpeningPlayer0.RequestRoll();
            diceManagerOpeningPlayer1.RequestRoll();
            return;
        }

        if (!_openingRollResolved && diceManager != null)
            diceManager.SetDiceCount(2);

        diceManager?.RequestRoll();
    }

    private bool UsesDualOpeningDiceManagers()
    {
        return diceManagerOpeningPlayer0 != null
               && diceManagerOpeningPlayer1 != null
               && diceManagerOpeningPlayer0 != diceManagerOpeningPlayer1;
    }

    private void OnOpeningDicePlayer0Finished(int d1, int d2)
    {
        if (_busy || _openingRollResolved || !UsesDualOpeningDiceManagers()) return;
        _openingBufferedDie0 = d1;
        TryCompleteBufferedOpeningRoll();
    }

    private void OnOpeningDicePlayer1Finished(int d1, int d2)
    {
        if (_busy || _openingRollResolved || !UsesDualOpeningDiceManagers()) return;
        _openingBufferedDie1 = d1;
        TryCompleteBufferedOpeningRoll();
    }

    private void TryCompleteBufferedOpeningRoll()
    {
        if (!_openingBufferedDie0.HasValue || !_openingBufferedDie1.HasValue) return;
        int v0 = _openingBufferedDie0.Value;
        int v1 = _openingBufferedDie1.Value;
        _openingBufferedDie0 = null;
        _openingBufferedDie1 = null;
        ApplyOpeningRollFromDice(v0, v1);
    }

    private void ApplyOpeningRollFromDice(int dieForPlayer0, int dieForPlayer1)
    {
        if (!BackgammonOpeningRollRules.TryApplyOpeningDice(dieForPlayer0, dieForPlayer1, State))
        {
            _openingRollTieAwaitingReroll = true;
            State.Dice1 = 0;
            State.Dice2 = 0;
            OnStateChanged?.Invoke();
            hud?.RefreshAll(this);
            return;
        }

        _openingRollTieAwaitingReroll = false;
        _openingRollResolved = true;
        _rolledThisTurn = true;
        RollsThisGame++;
        BackgammonGameRules.SyncBoardArrayFromCheckerArrays(State);
        SyncMatchFromState();
        RefreshLegals();
        if (_legalTurns.Count == 0)
            PassTurnNoMoves();
        else
        {
            OnStateChanged?.Invoke();
            hud?.RefreshAll(this);
            MaybeStartAiTurn();
        }
    }

    private void OnDiceRollFinished(int d1, int d2)
    {
        if (_busy) return;

        if (!_openingRollResolved)
        {
            if (UsesDualOpeningDiceManagers())
                return;

            ApplyOpeningRollFromDice(d1, d2);
            return;
        }

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
        }
    }

    private void RefreshLegals()
    {
        _legalTurns.Clear();
        _legalTurns.AddRange(MoveGenerator.GenerateLegalTurns(State));
    }

    /// <summary>Apply a full legal turn by index from <see cref="CurrentLegalTurns"/>.</summary>
    public void TryApplyTurnByIndex(int index)
    {
        if (_busy || !_rolledThisTurn || index < 0 || index >= _legalTurns.Count) return;
        Turn turn = _legalTurns[index];
        PushUndoFrame();
        ApplyTurnAndAdvance(turn);
    }

    /// <summary>Revert the last player-visible state change (move or forced pass).</summary>
    public bool TryUndoLastMove()
    {
        if (_busy || _undoStack.Count == 0) return false;
        UndoFrame f = _undoStack.Pop();
        RestoreUndoFrame(f);
        RefreshLegals();
        boardManager?.ClearAllPointHighlights();
        boardManager?.SyncCheckersFromGameState(State);
        _awaitingDoubleResponse = false;
        hud?.SetDoubleOfferVisible(false);
        TurnsCompletedThisGame = Mathf.Max(0, TurnsCompletedThisGame - 1);
        OnStateChanged?.Invoke();
        hud?.RefreshAll(this);
        return true;
    }

    /// <summary>Offer double before rolling. Opponent responds via HUD (or AI auto-responds).</summary>
    public void OfferDouble()
    {
        if (!_openingRollResolved || _busy || IsGameOver(out _) || State.CubeValue >= 64 || _awaitingDoubleResponse || _rolledThisTurn) return;

        _doubleOfferedByPlayer = State.PlayerOnRoll;
        _awaitingDoubleResponse = true;
        SyncMatchFromState();
        hud?.SetDoubleOfferVisible(true);
        hud?.RefreshAll(this);

        if (BackgammonSettings.OpponentIsAi)
            StartCoroutine(CoAiRespondDouble());
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
    }

    public void RespondDoubleDrop()
    {
        if (!_awaitingDoubleResponse || _busy) return;

        Debug.LogWarning($"[Backgammon] Double dropped — player {_doubleOfferedByPlayer}'s opponent declined. Starting new game.");
        _awaitingDoubleResponse = false;
        hud?.SetDoubleOfferVisible(false);
        NewGame();
    }

    public void SetBoardViewHorizontal(bool horizontal)
    {
        boardManager?.SetBoardViewHorizontal(horizontal);
    }

    private IEnumerator CoAiRespondDouble()
    {
        _busy = true;
        yield return new WaitForSeconds(0.4f);
        _busy = false;
        if (!_awaitingDoubleResponse) yield break;
        if (UnityEngine.Random.value < 0.72f)
            RespondDoubleTake();
        else
            RespondDoubleDrop();
    }

    private static int OpponentIndex(int playerOnRoll) => playerOnRoll == 0 ? 1 : 0;

    private void PushUndoFrame()
    {
        _undoStack.Push(UndoFrame.Capture(State, _rolledThisTurn));
    }

    private void RestoreUndoFrame(UndoFrame f)
    {
        f.ApplyTo(State);
        _rolledThisTurn = f.RolledThisTurn;
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
        PushUndoFrame();
        _busy = true;
        BackgammonGameRules.SwapSidesForNextTurn(State);
        BackgammonGameRules.SyncBoardArrayFromCheckerArrays(State);
        State.Dice1 = 0;
        State.Dice2 = 0;
        _rolledThisTurn = false;
        _legalTurns.Clear();
        boardManager?.ClearAllPointHighlights();
        boardManager?.SyncCheckersFromGameState(State);
        _busy = false;
        SyncMatchFromState();
        TurnsCompletedThisGame++;
        OnStateChanged?.Invoke();
        hud?.RefreshAll(this);
        MaybeStartAiTurn();
    }

    private void ApplyTurnAndAdvance(Turn turn)
    {
        _busy = true;
        State = MoveGenerator.ApplyTurn(State, turn);
        BackgammonGameRules.SwapSidesForNextTurn(State);
        BackgammonGameRules.SyncBoardArrayFromCheckerArrays(State);
        State.Dice1 = 0;
        State.Dice2 = 0;
        _rolledThisTurn = false;
        _legalTurns.Clear();
        boardManager?.ClearAllPointHighlights();
        boardManager?.SyncCheckersFromGameState(State);
        _busy = false;
        SyncMatchFromState();
        TurnsCompletedThisGame++;

        if (IsGameOver(out _))
        {
            OnStateChanged?.Invoke();
            hud?.RefreshAll(this);
            return;
        }

        OnStateChanged?.Invoke();
        hud?.RefreshAll(this);
        MaybeStartAiTurn();
    }

    public bool IsGameOver(out string winnerLabel)
    {
        winnerLabel = null;
        if (State == null) return false;
        bool p1Empty = BackgammonGameRules.HasWon(State.Player1Checkers);
        bool p2Empty = BackgammonGameRules.HasWon(State.Player2Checkers);
        if (!p1Empty && !p2Empty) return false;
        winnerLabel = p1Empty ? "Player 2 wins" : "Player 1 wins";
        return true;
    }

    private void MaybeStartAiTurn()
    {
        if (IsGameOver(out _)) return;
        if (!BackgammonSettings.OpponentIsAi || State.PlayerOnRoll != 1) return;
        StartCoroutine(CoAiTurn());
    }

    private IEnumerator CoAiTurn()
    {
        _busy = true;
        yield return new WaitForSeconds(0.35f);
        bool needNewRoll = State.Dice1 <= 0 || State.Dice2 <= 0 || !_rolledThisTurn;
        if (needNewRoll)
        {
            State.Dice1 = UnityEngine.Random.Range(1, 7);
            State.Dice2 = UnityEngine.Random.Range(1, 7);
            _rolledThisTurn = true;
            RollsThisGame++;
        }
        RefreshLegals();
        OnStateChanged?.Invoke();
        hud?.RefreshAll(this);
        yield return new WaitForSeconds(0.25f);

        if (_legalTurns.Count == 0)
        {
            BackgammonGameRules.SwapSidesForNextTurn(State);
            BackgammonGameRules.SyncBoardArrayFromCheckerArrays(State);
        }
        else
        {
            Turn pick = null;
            if (BackgammonAIService.TryGetSearchEngine(out SearchEngine se))
                pick = se.GetBestTurn(State, Match, BackgammonSettings.AiSearchDepth);

            if (pick == null || pick.ResultingState == null)
                pick = _legalTurns[0];

            State = MoveGenerator.ApplyTurn(State, pick);
            BackgammonGameRules.SwapSidesForNextTurn(State);
            BackgammonGameRules.SyncBoardArrayFromCheckerArrays(State);
        }

        State.Dice1 = 0;
        State.Dice2 = 0;
        _rolledThisTurn = false;
        _legalTurns.Clear();
        boardManager?.ClearAllPointHighlights();
        boardManager?.SyncCheckersFromGameState(State);
        _busy = false;
        SyncMatchFromState();
        TurnsCompletedThisGame++;
        OnStateChanged?.Invoke();
        hud?.RefreshAll(this);
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
    }

    public void DebugForcePassTurn()
    {
        if (_busy) return;
        PassTurnNoMoves();
    }

    public void DebugPrintBoardConsole()
    {
        if (State == null) return;
        GameStateExtensions.PrintBoard(State);
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

        private UndoFrame(int[] p1, int[] p2, int d1, int d2, bool rolled, int por, int cv, int co, int s1, int s2)
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
        }

        public static UndoFrame Capture(GameState s, bool rolledThisTurn)
        {
            var p1 = new int[25];
            var p2 = new int[25];
            Array.Copy(s.Player1Checkers, p1, 25);
            Array.Copy(s.Player2Checkers, p2, 25);
            return new UndoFrame(p1, p2, s.Dice1, s.Dice2, rolledThisTurn, s.PlayerOnRoll, s.CubeValue, s.CubeOwner, s.Player1Score, s.Player2Score);
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
}
