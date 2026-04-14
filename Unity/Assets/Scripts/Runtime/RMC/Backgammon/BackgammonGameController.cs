using System;
using System.Collections;
using System.Collections.Generic;
using EngineCore;
using Runtime.RMC._MyProject_.Dice;
using Runtime.RMC.Backgammon.Core;
using Runtime.RMC.Backgammon.Settings;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Owns <see cref="GameState"/>, dice → legals → apply → swap (EngineCLI convention).
/// </summary>
[DefaultExecutionOrder(-40)]
public class BackgammonGameController : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool enableMoveSelectionDebugLogs = true;

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

    public bool AwaitingDoubleResponse => _awaitingDoubleResponse;

    /// <summary>False until the first opening roll is resolved (non-tie). After that, normal turns use two dice together.</summary>
    public bool OpeningRollResolved => _openingRollResolved;

    /// <summary>Last opening roll was a tie; player should roll again.</summary>
    public bool OpeningRollAwaitingReroll => !_openingRollResolved && _openingRollTieAwaitingReroll;

    public event Action OnStateChanged;
    public event Action<CheckerSoundEventData> OnCheckerSoundEvent;

    private readonly List<Turn> _legalTurns = new();
    private readonly HashSet<int> _movableFromScratch = new();
    private readonly Stack<UndoFrame> _undoStack = new();
    private bool _rolledThisTurn;
    private bool _busy;
    private bool _awaitingDoubleResponse;
    private int _doubleOfferedByPlayer;
    private bool _openingRollResolved;
    private bool _openingRollTieAwaitingReroll;
    private int? _diceBufferedDie0;
    private int? _diceBufferedDie1;

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
        NewGame();
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
        _diceBufferedDie0 = null;
        _diceBufferedDie1 = null;
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
        RefreshMovableCheckerHighlights();
    }

    public void RequestRollDice()
    {
        if (_busy || _rolledThisTurn || _awaitingDoubleResponse) return;
        if (!HasTwoDiceManagers()) return;

        _diceBufferedDie0 = null;
        _diceBufferedDie1 = null;
        diceManagerPlayer0.SetDiceCount(1);
        diceManagerPlayer1.SetDiceCount(1);
        diceManagerPlayer0.RequestRoll();
        diceManagerPlayer1.RequestRoll();
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
        if (_busy || !HasTwoDiceManagers()) return;
        _diceBufferedDie0 = d1;
        TryCompleteBufferedDiceRoll();
    }

    private void OnDiceManagerPlayer1Finished(int d1, int d2)
    {
        if (_busy || !HasTwoDiceManagers()) return;
        _diceBufferedDie1 = d1;
        TryCompleteBufferedDiceRoll();
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
            State.Dice1 = 0;
            State.Dice2 = 0;
            SyncMatchFromState();
            OnStateChanged?.Invoke();
            hud?.RefreshAll(this);
            RefreshMovableCheckerHighlights();
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
            RefreshMovableCheckerHighlights();
            MaybeStartAiTurn();
        }
    }

    private void RefreshLegals()
    {
        _legalTurns.Clear();
        _legalTurns.AddRange(MoveGenerator.GenerateLegalTurns(State));
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
        if (BackgammonSettings.OpponentIsAi && State.PlayerOnRoll == 0)
            return false;
        return true;
    }

    private void RefreshMovableCheckerHighlights()
    {
        if (boardManager == null) return;
        if (!ShouldShowMovableCheckerHighlights())
        {
            boardManager.ClearMovableCheckerHighlights();
            return;
        }

        BackgammonMovableFromPoints.CollectMovableFromEnginePoints(_legalTurns, _movableFromScratch);
        boardManager.ApplyMovableCheckerHighlights(_movableFromScratch);
    }

    /// <summary>Apply a full legal turn by index from <see cref="CurrentLegalTurns"/>.</summary>
    public void TryApplyTurnByIndex(int index)
    {
        if (_busy || !_rolledThisTurn || index < 0 || index >= _legalTurns.Count) return;
        Turn turn = _legalTurns[index];
        if (turn == null || turn.Moves == null || turn.Moves.Count == 0) return;
        PushUndoFrame();
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
        PushUndoFrame(turn.Moves[0]);
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
        PushUndoFrame();
        ApplySingleMoveAndContinue(turn.Moves[0]);
        return true;
    }

    public bool TryFinalizeCurrentTurn()
    {
        if (_busy || !_rolledThisTurn || _legalTurns.Count > 0) return false;
        PushUndoFrame();
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

    /// <summary>Revert the last player-visible state change (move or forced pass).</summary>
    public bool TryUndoLastMove()
    {
        if (_busy || _undoStack.Count == 0) return false;
        UndoFrame f = _undoStack.Pop();
        bool animatedUndo = false;
        if (boardManager != null && f.AppliedMove.HasValue)
        {
            animatedUndo = boardManager.TryApplySingleVisualUndoMove(f.AppliedMove.Value);
            if (enableMoveSelectionDebugLogs)
                Debug.Log($"[Backgammon][Undo] Attempt reverse visual move {FormatMove(f.AppliedMove.Value)} success={animatedUndo}");
        }

        RestoreUndoFrame(f);
        RefreshLegals();
        boardManager?.ClearAllPointHighlights();
        if (!animatedUndo)
            boardManager?.SyncCheckersFromGameState(State);
        _awaitingDoubleResponse = false;
        hud?.SetDoubleOfferVisible(false);
        TurnsCompletedThisGame = f.TurnsCompletedThisGame;
        OnStateChanged?.Invoke();
        hud?.RefreshAll(this);
        RefreshMovableCheckerHighlights();
        EmitCheckerSoundEventForUndo(f.AppliedMove);
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
        BackgammonBoardLayout.SetHorizontal(horizontal);
        boardManager?.SetBoardViewHorizontal(horizontal);
        if (State != null)
        {
            boardManager?.ClearAllPointHighlights();
            boardManager?.SyncCheckersFromGameState(State);
            RefreshMovableCheckerHighlights();
        }
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

    private void PushUndoFrame(Move? appliedMove = null)
    {
        _undoStack.Push(UndoFrame.Capture(State, _rolledThisTurn, TurnsCompletedThisGame, appliedMove));
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
        RefreshMovableCheckerHighlights();
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
        State = MoveGenerator.ApplyTurn(State, singleMoveTurn);
        BackgammonGameRules.SyncBoardArrayFromCheckerArrays(State);
        AdvanceStagedLegalTurnsAfterMove(legalBeforeMove, move);
        if (enableMoveSelectionDebugLogs)
            DebugLogLegalTurnFirstMoves("AdvanceStagedLegalTurnsAfterMove");
        boardManager?.ClearAllPointHighlights();
        bool movedVisually = boardManager != null && boardManager.TryApplySingleVisualMove(move);
        if (!movedVisually)
            boardManager?.SyncCheckersFromGameState(State);
        _busy = false;
        SyncMatchFromState();
        EmitCheckerSoundEventForAppliedMove(move);
        OnStateChanged?.Invoke();
        hud?.RefreshAll(this);
        RefreshMovableCheckerHighlights();
    }

    private void AdvanceStagedLegalTurnsAfterMove(IReadOnlyList<Turn> legalBeforeMove, Move appliedMove)
    {
        _legalTurns.Clear();
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
        EmitCheckerSoundEvent(
            ClassifyCheckerSoundEventForAppliedMove(move),
            move,
            isUndo: false);
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
        OnCheckerSoundEvent?.Invoke(new CheckerSoundEventData(
            eventType,
            State != null ? State.PlayerOnRoll : -1,
            move.From,
            move.To,
            move.IsHit,
            isUndo));
    }

    private void FinalizeTurnAndAdvance()
    {
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
        RefreshMovableCheckerHighlights();
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

    public void DebugPrintBoardConsole()
    {
        if (State == null) return;
        GameStateExtensions.PrintBoard(State);
    }

    private static string FormatMove(Move move)
    {
        return $"{move.From}->{move.To}";
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
        public readonly Move? AppliedMove;

        private UndoFrame(int[] p1, int[] p2, int d1, int d2, bool rolled, int por, int cv, int co, int s1, int s2, int turnsCompletedThisGame, Move? appliedMove)
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
            AppliedMove = appliedMove;
        }

        public static UndoFrame Capture(GameState s, bool rolledThisTurn, int turnsCompletedThisGame, Move? appliedMove)
        {
            var p1 = new int[25];
            var p2 = new int[25];
            Array.Copy(s.Player1Checkers, p1, 25);
            Array.Copy(s.Player2Checkers, p2, 25);
            return new UndoFrame(p1, p2, s.Dice1, s.Dice2, rolledThisTurn, s.PlayerOnRoll, s.CubeValue, s.CubeOwner, s.Player1Score, s.Player2Score, turnsCompletedThisGame, appliedMove);
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
