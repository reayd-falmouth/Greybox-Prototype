using EngineCore;
using Runtime.RMC.Backgammon.Core;
using UnityEngine;

/// <summary>
/// Owns doubling-cube negotiation state and rules.
/// The controller retains GameState/MatchState mutations and MonoBehaviour coroutines;
/// this class owns the offer/response guard state and the ownership logic.
/// </summary>
internal class BackgammonDoublingCubeNegotiator
{
    private bool _awaitingDoubleResponse;
    private int _doubleOfferedByPlayer;

    public bool AwaitingDoubleResponse => _awaitingDoubleResponse;
    public int DoubleOfferedByPlayer => _doubleOfferedByPlayer;

    public void Reset()
    {
        _awaitingDoubleResponse = false;
        _doubleOfferedByPlayer = -1;
    }

    public bool CanOffer(GameState state, bool openingRollResolved, bool busy, bool gameOver, bool rolledThisTurn)
    {
        if (state == null) return false;
        if (!openingRollResolved || busy || gameOver || state.CubeValue >= 64 || _awaitingDoubleResponse || rolledThisTurn)
            return false;

        int cubeOwner = state.CubeOwner;
        bool cubeIsCentered = cubeOwner == 3 || cubeOwner < 0;
        bool cubeOwnedByCurrentPlayer = cubeOwner == state.PlayerOnRoll;
        bool cubeOwnedByLocalPlayer = cubeOwner == BackgammonPlayerRoles.LocalPlayerIndex;
        bool localCanOfferByOwnership = cubeIsCentered || cubeOwnedByLocalPlayer;
        return (cubeIsCentered || cubeOwnedByCurrentPlayer) && localCanOfferByOwnership;
    }

    /// <summary>
    /// Records that the current player has offered the cube.
    /// Returns the responder's player index.
    /// </summary>
    public int BeginOffer(GameState state)
    {
        _doubleOfferedByPlayer = state.PlayerOnRoll;
        _awaitingDoubleResponse = true;
        return OpponentIndex(_doubleOfferedByPlayer);
    }

    /// <summary>
    /// Applies a take response: mutates state/match and clears the offer flag.
    /// Returns the new cube value.
    /// </summary>
    public int ApplyTake(GameState state, MatchState match)
    {
        int newVal = Mathf.Min(64, state.CubeValue * 2);
        state.CubeValue = newVal;
        int responder = OpponentIndex(_doubleOfferedByPlayer);
        state.CubeOwner = responder;
        match.Cube = newVal;
        match.CubeOwner = responder;
        match.Doubled = true;
        _awaitingDoubleResponse = false;
        return newVal;
    }

    /// <summary>
    /// Clears the offer flag for a drop. Returns the winner (the offerer).
    /// </summary>
    public int ApplyDrop()
    {
        _awaitingDoubleResponse = false;
        return _doubleOfferedByPlayer;
    }

    /// <summary>Clears the awaiting flag without applying a result (e.g. on undo or game end).</summary>
    public void CancelPendingOffer()
    {
        _awaitingDoubleResponse = false;
    }

    /// <summary>
    /// Returns true if the responder can immediately beaver (re-double) after being offered.
    /// Ownership after a beaver stays with the responder; the original offerer must then take/drop.
    /// </summary>
    public bool CanBeaver(GameState state, bool beaversAllowed)
    {
        if (!beaversAllowed || !_awaitingDoubleResponse) return false;
        if (state == null || state.CubeValue >= 64) return false;
        return true;
    }

    /// <summary>
    /// Applies a beaver: the responder re-doubles immediately.
    /// Cube value doubles, ownership stays with the responder.
    /// The original offerer now becomes the responder and must take/drop.
    /// </summary>
    public int ApplyBeaver(GameState state, MatchState match)
    {
        int newVal = Mathf.Min(64, state.CubeValue * 2);
        state.CubeValue = newVal;
        int responder = OpponentIndex(_doubleOfferedByPlayer); // the beaver-er keeps the cube
        state.CubeOwner = responder;
        match.Cube = newVal;
        match.CubeOwner = responder;
        match.Doubled = true;
        // Flip roles: original offerer must now respond
        _doubleOfferedByPlayer = responder;
        // _awaitingDoubleResponse stays true
        return newVal;
    }

    private static int OpponentIndex(int playerIndex) => playerIndex == 0 ? 1 : 0;
}
