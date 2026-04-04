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
    [SerializeField] private BackgammonHudController hud;
    [Tooltip("When true, DiceManager does not bind its own roll button; use HUD Roll.")]
    [SerializeField] private bool rollDiceViaHudOnly = true;

    public GameState State { get; private set; }
    public MatchState Match { get; private set; }

    public IReadOnlyList<Turn> CurrentLegalTurns => _legalTurns;

    public event Action OnStateChanged;

    private readonly List<Turn> _legalTurns = new();
    private bool _rolledThisTurn;
    private bool _busy;

    private void Awake()
    {
        Match = new MatchState
        {
            MatchLength = 0,
            JacobyRule = true,
            BeaversAllowed = true
        };

        if (diceManager != null && rollDiceViaHudOnly)
            diceManager.SuppressRollButtonBinding = true;
    }

    private void Start()
    {
        if (hud == null)
            hud = FindFirstObjectByType<BackgammonHudController>();
        if (diceManager != null)
            diceManager.OnDiceRollFinished += OnDiceRollFinished;
        NewGame();
    }

    private void OnDestroy()
    {
        if (diceManager != null)
            diceManager.OnDiceRollFinished -= OnDiceRollFinished;
    }

    public void NewGame()
    {
        StopAllCoroutines();
        BackgammonAIService.ClearSearchEngineCache();
        _busy = false;
        State = PositionId.Decode("4HPwATDgc/ABMA");
        State.Dice1 = 0;
        State.Dice2 = 0;
        _rolledThisTurn = false;
        _legalTurns.Clear();
        BackgammonGameRules.SyncBoardArrayFromCheckerArrays(State);
        boardManager?.ClearAllPointHighlights();
        boardManager?.SyncCheckersFromGameState(State);
        OnStateChanged?.Invoke();
        hud?.RefreshAll(this);
    }

    public void RequestRollDice()
    {
        if (_busy || _rolledThisTurn) return;
        diceManager?.RequestRoll();
    }

    private void OnDiceRollFinished(int d1, int d2)
    {
        if (_busy) return;
        State.Dice1 = d1;
        State.Dice2 = d2;
        _rolledThisTurn = true;
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
        ApplyTurnAndAdvance(turn);
    }

    private void PassTurnNoMoves()
    {
        if (!_rolledThisTurn) return;
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
        if (!BackgammonSettings.OpponentIsAi || IsGameOver(out _)) return;
        StartCoroutine(CoAiTurn());
    }

    private IEnumerator CoAiTurn()
    {
        _busy = true;
        yield return new WaitForSeconds(0.35f);
        State.Dice1 = UnityEngine.Random.Range(1, 7);
        State.Dice2 = UnityEngine.Random.Range(1, 7);
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
        _rolledThisTurn = true;
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
}
