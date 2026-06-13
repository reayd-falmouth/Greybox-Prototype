using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using EngineCore;
using NUnit.Framework;
using Runtime.RMC._MyProject_.Core;
using Runtime.RMC.Backgammon.Core;
using Runtime.RMC.Backgammon.Settings;
using UnityEngine;

public class BackgammonStagedTurnFlowEditModeTests
{
    [Test]
    public void CheckerClickPath_AppliesSingleMove_DoesNotFinalizeTurn()
    {
        var go = new GameObject("BackgammonGameController_Test");
        try
        {
            var ctrl = go.AddComponent<BackgammonGameController>();
            ctrl.NewGame();
            ctrl.DebugSetDiceAndRefresh(6, 1);

            Assert.Greater(ctrl.CurrentLegalTurns.Count, 0);
            int playerOnRollBefore = ctrl.State.PlayerOnRoll;
            int turnsCompletedBefore = ctrl.TurnsCompletedThisGame;
            Move first = ctrl.CurrentLegalTurns[0].Moves[0];

            bool applied = ctrl.TryApplyPreferredTurnForFrom(first.From, true);

            Assert.IsTrue(applied);
            Assert.IsTrue(ctrl.HasRolledThisTurn);
            Assert.AreEqual(playerOnRollBefore, ctrl.State.PlayerOnRoll);
            Assert.AreEqual(turnsCompletedBefore, ctrl.TurnsCompletedThisGame);
            if (ctrl.CurrentLegalTurns.Count > 0)
            {
                bool hasNonNoOpFirstMove = false;
                for (int i = 0; i < ctrl.CurrentLegalTurns.Count; i++)
                {
                    Turn t = ctrl.CurrentLegalTurns[i];
                    if (t?.Moves == null || t.Moves.Count == 0) continue;
                    Move m = t.Moves[0];
                    if (m.From != m.To)
                    {
                        hasNonNoOpFirstMove = true;
                        break;
                    }
                }
                Assert.IsTrue(hasNonNoOpFirstMove, "Remaining staged legal turns should not degrade into from==to no-op moves.");
            }
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void Finalize_BecomesAvailableWhenNoLegalsRemain_ThenAdvancesTurn()
    {
        var go = new GameObject("BackgammonGameController_Test");
        try
        {
            var ctrl = go.AddComponent<BackgammonGameController>();
            ctrl.NewGame();
            ctrl.DebugSetDiceAndRefresh(6, 1);

            int startPlayer = ctrl.State.PlayerOnRoll;
            int guard = 0;
            while (ctrl.CurrentLegalTurns.Count > 0 && guard++ < 8)
            {
                Move m = ctrl.CurrentLegalTurns[0].Moves[0];
                Assert.IsTrue(ctrl.TryApplyPreferredTurnForFrom(m.From, true));
            }

            Assert.AreEqual(0, ctrl.CurrentLegalTurns.Count);
            Assert.IsTrue(ctrl.CanFinalizeCurrentTurn);
            Assert.IsTrue(ctrl.TryFinalizeCurrentTurn());
            Assert.IsFalse(ctrl.HasRolledThisTurn);
            Assert.AreEqual(startPlayer == 0 ? 1 : 0, ctrl.State.PlayerOnRoll);
            Assert.AreEqual(1, ctrl.TurnsCompletedThisGame);
            Assert.IsFalse(ctrl.CanUndo, "Undo stack should clear when the turn completes.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void UndoStack_ClearedAfterHumanFinalizesTurn_WithStagedMoves()
    {
        var go = new GameObject("BackgammonGameController_Test");
        try
        {
            var ctrl = go.AddComponent<BackgammonGameController>();
            ctrl.NewGame();
            ctrl.DebugSetDiceAndRefresh(1, 1);

            Assert.Greater(ctrl.CurrentLegalTurns.Count, 0);
            Move m1 = ctrl.CurrentLegalTurns[0].Moves[0];
            Assert.IsTrue(ctrl.TryApplyPreferredTurnForFrom(m1.From, true));
            Assert.IsTrue(ctrl.CanUndo, "Expected at least one undo frame after a move within the roll.");

            int guard = 0;
            while (ctrl.CurrentLegalTurns.Count > 0 && guard++ < 16)
            {
                Move m = ctrl.CurrentLegalTurns[0].Moves[0];
                Assert.IsTrue(ctrl.TryApplyPreferredTurnForFrom(m.From, true));
            }

            Assert.AreEqual(0, ctrl.CurrentLegalTurns.Count);
            Assert.IsTrue(ctrl.TryFinalizeCurrentTurn());
            Assert.IsFalse(ctrl.CanUndo, "Undo must not span completed turns.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void Undo_IsLifo_PerSingleMove()
    {
        var go = new GameObject("BackgammonGameController_Test");
        try
        {
            var ctrl = go.AddComponent<BackgammonGameController>();
            ctrl.NewGame();
            ctrl.DebugSetDiceAndRefresh(1, 1);

            int[] preP1 = (int[])ctrl.State.Player1Checkers.Clone();
            int[] preP2 = (int[])ctrl.State.Player2Checkers.Clone();

            Move m1 = ctrl.CurrentLegalTurns[0].Moves[0];
            Assert.IsTrue(ctrl.TryApplyPreferredTurnForFrom(m1.From, true));
            int[] midP1 = (int[])ctrl.State.Player1Checkers.Clone();
            int[] midP2 = (int[])ctrl.State.Player2Checkers.Clone();

            Assert.Greater(ctrl.CurrentLegalTurns.Count, 0);
            Move m2 = ctrl.CurrentLegalTurns[0].Moves[0];
            Assert.IsTrue(ctrl.TryApplyPreferredTurnForFrom(m2.From, true));

            Assert.IsTrue(ctrl.TryUndoLastMove());
            CollectionAssert.AreEqual(midP1, ctrl.State.Player1Checkers);
            CollectionAssert.AreEqual(midP2, ctrl.State.Player2Checkers);

            Assert.IsTrue(ctrl.TryUndoLastMove());
            CollectionAssert.AreEqual(preP1, ctrl.State.Player1Checkers);
            CollectionAssert.AreEqual(preP2, ctrl.State.Player2Checkers);
            Assert.IsTrue(ctrl.HasRolledThisTurn);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void UndoFrame_CapturesLegalTurnsSnapshot_ForUndoRestore()
    {
        var go = new GameObject("BackgammonGameController_UndoSnapshot");
        try
        {
            var ctrl = go.AddComponent<BackgammonGameController>();
            ctrl.NewGame();
            ctrl.DebugSetDiceAndRefresh(1, 1);
            Assert.Greater(ctrl.CurrentLegalTurns.Count, 0);

            Move first = ctrl.CurrentLegalTurns[0].Moves[0];
            Assert.IsTrue(ctrl.TryApplyPreferredTurnForFrom(first.From, true));

            object undoStack = GetPrivateField(ctrl, "_undoStack");
            MethodInfo peekMethod = undoStack.GetType().GetMethod("Peek", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(peekMethod, Is.Not.Null, "Expected Stack.Peek to exist.");
            object frame = peekMethod.Invoke(undoStack, null);

            FieldInfo legalSnapshotField = frame.GetType().GetField("LegalTurnsSnapshot", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(legalSnapshotField, Is.Not.Null, "Expected undo frame to capture legal turn snapshots.");
            object snapshot = legalSnapshotField.GetValue(frame);
            Assert.That(snapshot, Is.Not.Null);
            Assert.Greater(((System.Array)snapshot).Length, 0, "Expected captured undo frame to include legal turn entries.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void Undo_AfterSecondMove_RestoresPriorStagedLegalSignature()
    {
        var go = new GameObject("BackgammonGameController_UndoLegalsRestore");
        try
        {
            var ctrl = go.AddComponent<BackgammonGameController>();
            ctrl.NewGame();
            ctrl.DebugSetDiceAndRefresh(1, 1);
            Assert.Greater(ctrl.CurrentLegalTurns.Count, 0);

            Move first = ctrl.CurrentLegalTurns[0].Moves[0];
            Assert.IsTrue(ctrl.TryApplyPreferredTurnForFrom(first.From, true));
            string signatureAfterFirstMove = ComputeLegalSignature(ctrl.CurrentLegalTurns);

            Assert.Greater(ctrl.CurrentLegalTurns.Count, 0, "Expected at least one staged move before testing undo restore.");
            Move second = ctrl.CurrentLegalTurns[0].Moves[0];
            Assert.IsTrue(ctrl.TryApplyPreferredTurnForFrom(second.From, true));

            Assert.IsTrue(ctrl.TryUndoLastMove(), "Expected second move undo to succeed.");
            string signatureAfterUndo = ComputeLegalSignature(ctrl.CurrentLegalTurns);
            Assert.AreEqual(signatureAfterFirstMove, signatureAfterUndo, "Undo should restore the staged legal list from the cached undo frame.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void FinalizeTurn_ClearsMovableHighlightCacheState()
    {
        var go = new GameObject("BackgammonGameController_HighlightCacheFinalize");
        try
        {
            var ctrl = go.AddComponent<BackgammonGameController>();
            ctrl.NewGame();
            ctrl.DebugSetDiceAndRefresh(6, 1);

            int guard = 0;
            while (ctrl.CurrentLegalTurns.Count > 0 && guard++ < 8)
            {
                Move m = ctrl.CurrentLegalTurns[0].Moves[0];
                Assert.IsTrue(ctrl.TryApplyPreferredTurnForFrom(m.From, true));
            }

            Assert.IsTrue(ctrl.TryFinalizeCurrentTurn());

            HashSet<int> movableFromCache = (HashSet<int>)GetPrivateField(ctrl, "_lastMovableFromPoints");
            bool highlightsVisible = (bool)GetPrivateField(ctrl, "_lastMovableHighlightsVisible");
            Assert.AreEqual(0, movableFromCache.Count, "Play Move finalize should clear movable highlight cache entries.");
            Assert.IsFalse(highlightsVisible, "Play Move finalize should leave movable highlights hidden.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void RefreshMovableHighlights_ForceRebuild_DoesNotSkipWhenFromSetMatches()
    {
        var go = new GameObject("BackgammonGameController_ForceHighlightRefresh");
        var boardGo = new GameObject("BoardManager_ForceHighlightRefresh");
        try
        {
            var ctrl = go.AddComponent<BackgammonGameController>();
            var boardManager = boardGo.AddComponent<BoardManager>();
            boardManager.allPoints = new BoardPoint[24];
            boardManager.barWhiteAnchor = new GameObject("BarWhite").transform;
            boardManager.barBlackAnchor = new GameObject("BarBlack").transform;

            int fromBoardIndex = BackgammonBoardLayout.EnginePointToBoardIndex(6);
            BoardPoint fromPoint = CreatePoint("FromPoint");
            boardManager.allPoints[fromBoardIndex] = fromPoint;
            var checkerWithRenderer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            checkerWithRenderer.name = "MovableChecker";
            checkerWithRenderer.AddComponent<Checker>().color = PlayerColor.White;
            fromPoint.AddChecker(checkerWithRenderer, animated: false);

            SetPrivateField(ctrl, "boardManager", boardManager);
            ctrl.NewGame();
            ctrl.DebugSetDiceAndRefresh(6, 1);
            SetPrivateField(ctrl, "_hasLastMovableHighlightState", true);
            SetPrivateField(ctrl, "_lastMovableHighlightsVisible", true);
            SetPrivateField(ctrl, "_forceMovableHighlightRebuild", true);

            var legalTurns = (List<Turn>)GetPrivateField(ctrl, "_legalTurns");
            legalTurns.Clear();
            legalTurns.Add(new Turn
            {
                Moves = new List<Move> { new Move { From = 6, To = 5, IsHit = false } },
                DiceUsed = new List<int> { 1 }
            });

            var lastMovable = (HashSet<int>)GetPrivateField(ctrl, "_lastMovableFromPoints");
            lastMovable.Clear();
            lastMovable.Add(6);

            InvokeNonPublicMethod(ctrl, "RefreshMovableCheckerHighlights");

            bool forceRebuild = (bool)GetPrivateField(ctrl, "_forceMovableHighlightRebuild");
            Assert.IsFalse(forceRebuild, "Forced refresh should apply highlight update even when movable source set matches.");
        }
        finally
        {
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void RefreshMovableHighlights_WhenLegalFromChanges_UpdatesMovableSourceCache()
    {
        var go = new GameObject("BackgammonGameController_StagedMoveHighlightCache");
        var boardGo = new GameObject("BoardManager_StagedMoveHighlightCache");
        try
        {
            var ctrl = go.AddComponent<BackgammonGameController>();
            ctrl.NewGame();
            ctrl.DebugSetDiceAndRefresh(6, 1);

            var boardManager = boardGo.AddComponent<BoardManager>();
            boardManager.allPoints = new BoardPoint[24];
            boardManager.barWhiteAnchor = new GameObject("BarWhite").transform;
            boardManager.barBlackAnchor = new GameObject("BarBlack").transform;
            SetPrivateField(ctrl, "boardManager", boardManager);

            var legalTurns = (List<Turn>)GetPrivateField(ctrl, "_legalTurns");
            legalTurns.Clear();
            legalTurns.Add(new Turn
            {
                Moves = new List<Move> { new Move { From = 6, To = 5, IsHit = false } },
                DiceUsed = new List<int> { 1 }
            });
            InvokeNonPublicMethod(ctrl, "RefreshMovableCheckerHighlights");

            HashSet<int> expectedFrom = new HashSet<int> { 6 };
            HashSet<int> cachedFrom = (HashSet<int>)GetPrivateField(ctrl, "_lastMovableFromPoints");
            CollectionAssert.AreEquivalent(expectedFrom, cachedFrom, "Initial movable source cache should match legal from-points.");

            legalTurns.Clear();
            legalTurns.Add(new Turn
            {
                Moves = new List<Move> { new Move { From = 5, To = 4, IsHit = false } },
                DiceUsed = new List<int> { 1 }
            });
            SetPrivateField(ctrl, "_forceMovableHighlightRebuild", true);
            InvokeNonPublicMethod(ctrl, "RefreshMovableCheckerHighlights");

            expectedFrom = new HashSet<int> { 5 };
            cachedFrom = (HashSet<int>)GetPrivateField(ctrl, "_lastMovableFromPoints");
            CollectionAssert.AreEquivalent(expectedFrom, cachedFrom, "Movable highlight cache should mirror remaining staged legal from-points.");
        }
        finally
        {
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void RefreshMovableHighlights_HidePath_ClearsBoardPulseTargets()
    {
        var ctrlGo = new GameObject("BackgammonGameController_HideClearsPulse");
        var boardGo = new GameObject("BoardManager_HideClearsPulse");
        try
        {
            var ctrl = ctrlGo.AddComponent<BackgammonGameController>();
            var boardManager = boardGo.AddComponent<BoardManager>();
            boardManager.allPoints = new BoardPoint[24];
            boardManager.barWhiteAnchor = new GameObject("BarWhite").transform;
            boardManager.barBlackAnchor = new GameObject("BarBlack").transform;

            int fromBoardIndex = BackgammonBoardLayout.EnginePointToBoardIndex(6);
            BoardPoint point = CreatePoint("Point");
            boardManager.allPoints[fromBoardIndex] = point;
            GameObject checker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            checker.name = "PulseChecker";
            checker.AddComponent<Checker>().color = PlayerColor.White;
            point.AddChecker(checker, animated: false);

            boardManager.ApplyMovableCheckerHighlights(new HashSet<int> { 6 });
            var pulseTargets = (System.Collections.ICollection)GetPrivateField(boardManager, "_movablePulseTargets");
            Assert.Greater(pulseTargets.Count, 0, "Expected precondition: pulse target list contains highlighted checker.");

            SetPrivateField(ctrl, "boardManager", boardManager);
            SetPrivateField(ctrl, "_rolledThisTurn", false);
            SetPrivateField(ctrl, "_busy", false);
            InvokeNonPublicMethod(ctrl, "RefreshMovableCheckerHighlights");

            pulseTargets = (System.Collections.ICollection)GetPrivateField(boardManager, "_movablePulseTargets");
            Assert.AreEqual(0, pulseTargets.Count, "Hide-path refresh should always clear board pulse targets.");
        }
        finally
        {
            Object.DestroyImmediate(boardGo);
            Object.DestroyImmediate(ctrlGo);
        }
    }

    [Test]
    public void BoardManager_Update_WhenPreviewDisabled_ClearsHoverRenderer()
    {
        var boardGo = new GameObject("BoardManager_ClearsHoverWhenPreviewDisabled");
        try
        {
            var boardManager = boardGo.AddComponent<BoardManager>();
            var hoverObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            MeshRenderer hoverRenderer = hoverObj.GetComponent<MeshRenderer>();
            Assert.IsNotNull(hoverRenderer, "Expected primitive to provide MeshRenderer.");

            SetPrivateField(boardManager, "_movableHoverRenderer", hoverRenderer);
            SetPrivateField(boardManager, "enableMovePreviewLines", false);

            InvokeNonPublicMethod(boardManager, "Update");

            MeshRenderer currentHover = (MeshRenderer)GetPrivateField(boardManager, "_movableHoverRenderer");
            Assert.IsNull(currentHover, "Preview-disabled update should clear stale hover renderer tint state.");
        }
        finally
        {
            Object.DestroyImmediate(boardGo);
        }
    }

    [Test]
    public void AiTurn_ClearsUndoStack_OnCompletion()
    {
        var go = new GameObject("BackgammonGameController_AiUndoTest");
        try
        {
            BackgammonSettings.OpponentIsAi = true;
            var ctrl = go.AddComponent<BackgammonGameController>();
            SetPrivateField(ctrl, "<Match>k__BackingField", new MatchState());
            ctrl.NewGame();
            ctrl.DebugSetDiceAndRefresh(6, 1);
            ctrl.State.PlayerOnRoll = 0;

            IEnumerator aiTurn = InvokeNonPublicEnumerator(ctrl, "CoAiTurn");
            int guard = 0;
            while (aiTurn.MoveNext() && guard++ < 128)
            {
            }

            Assert.Less(guard, 128, "Expected AI coroutine to complete within guard limit.");
            Assert.IsFalse(ctrl.CanUndo, "Undo stack should clear when the AI turn completes.");
        }
        finally
        {
            BackgammonSettings.OpponentIsAi = true;
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void VisualPlayerOnRoll_TogglesWhenTurnBoundaryAdvances()
    {
        var go = new GameObject("BackgammonGameController_VisualTurnTest");
        try
        {
            var ctrl = go.AddComponent<BackgammonGameController>();
            ctrl.NewGame();
            Assert.IsTrue(ctrl.PlayerOnRollVisual, "New game should start from player visual perspective.");

            ctrl.DebugSetDiceAndRefresh(6, 1);
            ctrl.DebugForcePassTurn();

            Assert.IsFalse(ctrl.PlayerOnRollVisual, "Passing a turn should flip visual on-roll ownership.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void BoardViewSetting_PersistsRoundTrip()
    {
        bool original = BackgammonSettings.BoardViewIsHorizontal;
        try
        {
            BackgammonSettings.BoardViewIsHorizontal = false;
            Assert.IsFalse(BackgammonSettings.BoardViewIsHorizontal);

            BackgammonSettings.BoardViewIsHorizontal = true;
            Assert.IsTrue(BackgammonSettings.BoardViewIsHorizontal);
        }
        finally
        {
            BackgammonSettings.BoardViewIsHorizontal = original;
        }
    }

    [Test]
    public void AiTurn_UsesGameSpeedDerivedWaitDurations()
    {
        bool prevAi = BackgammonSettings.OpponentIsAi;
        float prevSpeed = BackgammonSettings.GameSpeedSecondsPerStep;
        var go = new GameObject("BackgammonGameController_AiPacingTest");
        try
        {
            BackgammonSettings.OpponentIsAi = true;
            BackgammonSettings.GameSpeedSecondsPerStep = 1.0f;
            var ctrl = go.AddComponent<BackgammonGameController>();
            SetPrivateField(ctrl, "<Match>k__BackingField", new MatchState());
            ctrl.NewGame();
            ctrl.DebugSetDiceAndRefresh(6, 1);
            ctrl.State.PlayerOnRoll = 0;

            IEnumerator aiTurn = InvokeNonPublicEnumerator(ctrl, "CoAiTurn");

            Assert.IsTrue(aiTurn.MoveNext(), "Expected initial pre-roll wait.");
            float preRollWait = ExtractWaitSeconds(aiTurn.Current);
            Assert.AreEqual(1.0f, preRollWait, 0.001f, "Pre-roll wait should track game speed.");

            int guard = 0;
            bool foundRevealWait = false;
            bool foundApplyWait = false;
            while (aiTurn.MoveNext() && guard++ < 256)
            {
                float seconds = ExtractWaitSeconds(aiTurn.Current);
                if (Mathf.Approximately(seconds, 0.8f))
                    foundRevealWait = true;
                if (Mathf.Approximately(seconds, 0.6f))
                    foundApplyWait = true;
            }

            Assert.Less(guard, 256, "Expected AI coroutine to complete within guard limit.");
            Assert.IsTrue(foundRevealWait, "Expected game-speed-based reveal delay.");
            Assert.IsTrue(foundApplyWait, "Expected game-speed-based post-apply delay.");
        }
        finally
        {
            BackgammonSettings.OpponentIsAi = prevAi;
            BackgammonSettings.GameSpeedSecondsPerStep = prevSpeed;
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void AiRollCallbacks_CompleteBufferedDice_WhenInProgress()
    {
        var go = new GameObject("BackgammonGameController_AiRollCallbacks");
        Runtime.RMC._MyProject_.Dice.DiceManager dm0 = null;
        Runtime.RMC._MyProject_.Dice.DiceManager dm1 = null;
        try
        {
            var ctrl = go.AddComponent<BackgammonGameController>();
            dm0 = new GameObject("DM0").AddComponent<Runtime.RMC._MyProject_.Dice.DiceManager>();
            dm1 = new GameObject("DM1").AddComponent<Runtime.RMC._MyProject_.Dice.DiceManager>();
            SetPrivateField(ctrl, "diceManagerPlayer0", dm0);
            SetPrivateField(ctrl, "diceManagerPlayer1", dm1);
            SetPrivateField(ctrl, "_aiRollInProgress", true);
            SetPrivateField(ctrl, "_aiActiveRollToken", 7);

            InvokeNonPublicMethod(ctrl, "OnDiceManagerPlayer0Finished", 4, 4);
            Assert.IsTrue((bool)GetPrivateField(ctrl, "_aiRollInProgress"), "AI roll should still be in progress after first die.");

            InvokeNonPublicMethod(ctrl, "OnDiceManagerPlayer1Finished", 2, 2);
            Assert.IsFalse((bool)GetPrivateField(ctrl, "_aiRollInProgress"), "AI roll should complete after both dice finish.");

            int? d0 = (int?)GetPrivateField(ctrl, "_aiRollBufferedDie0");
            int? d1 = (int?)GetPrivateField(ctrl, "_aiRollBufferedDie1");
            Assert.AreEqual(4, d0);
            Assert.AreEqual(2, d1);
        }
        finally
        {
            if (dm0 != null) Object.DestroyImmediate(dm0.gameObject);
            if (dm1 != null) Object.DestroyImmediate(dm1.gameObject);
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void AiMovePlayback_UsesQueueDrivenPresentationFlag_DuringPlayback()
    {
        float prevSpeed = BackgammonSettings.GameSpeedSecondsPerStep;
        bool prevAi = BackgammonSettings.OpponentIsAi;
        var go = new GameObject("BackgammonGameController_AiMovePlayback");
        try
        {
            BackgammonSettings.GameSpeedSecondsPerStep = 1.0f;
            BackgammonSettings.OpponentIsAi = true;
            var ctrl = go.AddComponent<BackgammonGameController>();
            SetPrivateField(ctrl, "<Match>k__BackingField", new MatchState());
            ctrl.NewGame();

            var pick = new Turn
            {
                Moves = new List<Move>
                {
                    new Move { From = 6, To = 5, IsHit = false },
                    new Move { From = 8, To = 6, IsHit = false }
                }
            };

            IEnumerator playback = InvokeNonPublicEnumerator(ctrl, "CoPlayAiTurnMovesSequentially", pick);

            bool observedQueueDrivenFlag = false;
            int guard = 0;
            while (playback.MoveNext() && guard++ < 128)
            {
                bool queueDriven = (bool)GetPrivateField(ctrl, "_presentationQueueDrivenByCoroutine");
                if (queueDriven)
                {
                    observedQueueDrivenFlag = true;
                    break;
                }
            }

            Assert.Less(guard, 128, "Expected playback coroutine to progress.");
            Assert.IsTrue(observedQueueDrivenFlag, "Expected AI playback to drive presentation queue during move dispatch.");

            guard = 0;
            while (playback.MoveNext() && guard++ < 256)
            {
                // Exhaust coroutine to verify teardown.
            }
            Assert.IsFalse((bool)GetPrivateField(ctrl, "_presentationQueueDrivenByCoroutine"),
                "Queue-driven flag should reset after playback completion.");
        }
        finally
        {
            BackgammonSettings.GameSpeedSecondsPerStep = prevSpeed;
            BackgammonSettings.OpponentIsAi = prevAi;
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void AiMovePlayback_EmitsCheckerSoundEvent_PerAppliedMove()
    {
        var go = new GameObject("BackgammonGameController_AiMovePlayback_SoundEvents");
        try
        {
            var ctrl = go.AddComponent<BackgammonGameController>();
            SetPrivateField(ctrl, "<Match>k__BackingField", new MatchState());
            ctrl.NewGame();

            int raisedCount = 0;
            var raisedTypes = new List<CheckerSoundEventType>();
            ctrl.OnCheckerSoundEvent += evt =>
            {
                raisedCount++;
                raisedTypes.Add(evt.EventType);
            };

            var pick = new Turn
            {
                Moves = new List<Move>
                {
                    new Move { From = 6, To = 5, IsHit = false },
                    new Move { From = 8, To = 6, IsHit = false }
                }
            };

            IEnumerator playback = InvokeNonPublicEnumerator(ctrl, "CoPlayAiTurnMovesSequentially", pick);
            int guard = 0;
            while (playback.MoveNext() && guard++ < 256)
            {
                // Exhaust coroutine.
            }

            Assert.Less(guard, 256, "Expected playback coroutine to complete within guard limit.");
            Assert.AreEqual(2, raisedCount, "Expected one checker event per AI-applied move.");
            Assert.AreEqual(CheckerSoundEventType.Move, raisedTypes[0]);
            Assert.AreEqual(CheckerSoundEventType.Move, raisedTypes[1]);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void AiMovePlayback_UsesVisualTurnStateToDeriveMoverColor()
    {
        var go = new GameObject("BackgammonGameController_AiMovePlayback_VisualMoverColor");
        try
        {
            var ctrl = go.AddComponent<BackgammonGameController>();
            ctrl.NewGame();

            SetPrivateField(ctrl, "_isPlayerOnRollVisual", true);
            object whiteMover = InvokeNonPublicMethod(ctrl, "GetVisualMoverColorForCurrentTurn");
            Assert.AreEqual(PlayerColor.White, (PlayerColor)whiteMover, "Expected player-on-roll visual state to map to white mover color.");

            SetPrivateField(ctrl, "_isPlayerOnRollVisual", false);
            object blackMover = InvokeNonPublicMethod(ctrl, "GetVisualMoverColorForCurrentTurn");
            Assert.AreEqual(PlayerColor.Black, (PlayerColor)blackMover, "Expected opponent-on-roll visual state to map to black mover color.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void AiMovePlayback_BarEntry_AnimatesAndDoesNotFallbackSync()
    {
        var go = new GameObject("BackgammonGameController_AiBarEntry");
        var managerGo = new GameObject("BoardManager_AiBarEntry");
        try
        {
            var ctrl = go.AddComponent<BackgammonGameController>();
            ctrl.NewGame();

            var manager = managerGo.AddComponent<BoardManager>();
            manager.allPoints = new BoardPoint[24];
            manager.barWhiteAnchor = new GameObject("BarWhite").transform;
            manager.barBlackAnchor = new GameObject("BarBlack").transform;
            manager.barBlackPoint = CreatePoint("BarBlackPoint");
            int mappedToEngine = 23 - 5;
            int toBoardIndex = BackgammonBoardLayout.EnginePointToBoardIndex(mappedToEngine);
            BoardPoint toPoint = CreatePoint("ToPoint");
            manager.allPoints[toBoardIndex] = toPoint;

            GameObject moving = CreateChecker("BarBlackChecker", PlayerColor.Black);
            manager.barBlackPoint.AddChecker(moving, animated: false);

            SetPrivateField(ctrl, "boardManager", manager);
            SetPrivateField(ctrl, "_isPlayerOnRollVisual", false);

            Move barEntry = new Move { From = BackgammonBoardLayout.BarEngineIndex, To = 5, IsHit = false };
            bool dispatched = false;
            bool movedVisually = false;
            Checker movedChecker = null;

            InvokeNonPublicMethod(
                ctrl,
                "EnqueueAiVisualMoveEvent",
                barEntry,
                PlayerColor.Black,
                1,
                1,
                new System.Action(() => dispatched = true),
                new System.Action<bool>(success => movedVisually = success),
                new System.Action<Checker>(checker => movedChecker = checker));

            IEnumerator wait = InvokeNonPublicEnumerator(
                ctrl,
                "WaitForAiMovePresentationCompletion",
                barEntry,
                1,
                1,
                new System.Func<bool>(() => dispatched),
                new System.Func<bool>(() => movedVisually),
                new System.Func<Checker>(() => movedChecker));

            int guard = 0;
            while (wait.MoveNext() && guard++ < 128)
            {
            }

            Assert.Less(guard, 128, "Expected queued bar-entry wait to complete.");
            Assert.IsTrue(dispatched, "Expected queue dispatch for bar-entry move.");
            Assert.IsTrue(movedVisually, "Expected bar-entry to apply visually without fallback sync.");
            Assert.IsNotNull(movedChecker, "Expected moved checker reference from bar-entry visual apply.");
            Assert.IsTrue(movedChecker.IsMoving, "Expected bar-entry checker to be in moving state after dispatch.");
            Assert.AreEqual(1, toPoint.checkers.Count);
            Assert.AreSame(moving, toPoint.checkers[0]);
        }
        finally
        {
            Object.DestroyImmediate(managerGo);
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void AiMovePlayback_BarEntryThenFollowupMove_PreservesFirstLegVisibility()
    {
        var go = new GameObject("BackgammonGameController_AiBarFollowup");
        var managerGo = new GameObject("BoardManager_AiBarFollowup");
        try
        {
            var ctrl = go.AddComponent<BackgammonGameController>();
            ctrl.NewGame();

            var manager = managerGo.AddComponent<BoardManager>();
            manager.allPoints = new BoardPoint[24];
            manager.barWhiteAnchor = new GameObject("BarWhite").transform;
            manager.barBlackAnchor = new GameObject("BarBlack").transform;
            manager.barBlackPoint = CreatePoint("BarBlackPoint");
            int firstToBoardIndex = BackgammonBoardLayout.EnginePointToBoardIndex(23 - 5);
            int secondToBoardIndex = BackgammonBoardLayout.EnginePointToBoardIndex(23 - 4);
            BoardPoint firstTo = CreatePoint("FirstTo");
            BoardPoint secondTo = CreatePoint("SecondTo");
            manager.allPoints[firstToBoardIndex] = firstTo;
            manager.allPoints[secondToBoardIndex] = secondTo;

            GameObject moving = CreateChecker("BarBlackChecker", PlayerColor.Black);
            manager.barBlackPoint.AddChecker(moving, animated: false);

            SetPrivateField(ctrl, "boardManager", manager);
            SetPrivateField(ctrl, "_isPlayerOnRollVisual", false);

            Move firstMove = new Move { From = BackgammonBoardLayout.BarEngineIndex, To = 5, IsHit = false };
            Move secondMove = new Move { From = 5, To = 4, IsHit = false };
            Checker firstMovedChecker = null;
            bool firstMovedVisually = false;
            bool secondMovedVisually = false;
            bool firstLegVisibleAtSecondDispatch = false;

            InvokeNonPublicMethod(
                ctrl,
                "EnqueueAiVisualMoveEvent",
                firstMove,
                PlayerColor.Black,
                1,
                2,
                new System.Action(() => { }),
                new System.Action<bool>(success => firstMovedVisually = success),
                new System.Action<Checker>(checker => firstMovedChecker = checker));

            InvokeNonPublicMethod(
                ctrl,
                "EnqueueAiVisualMoveEvent",
                secondMove,
                PlayerColor.Black,
                2,
                2,
                new System.Action(() =>
                {
                    firstLegVisibleAtSecondDispatch = firstMovedChecker != null && firstMovedChecker.IsMoving;
                }),
                new System.Action<bool>(success => secondMovedVisually = success),
                new System.Action<Checker>(_ => { }));

            InvokeNonPublicMethod(ctrl, "TickPresentationQueueFromCoroutine");
            InvokeNonPublicMethod(ctrl, "TickPresentationQueueFromCoroutine");

            Assert.IsTrue(firstMovedVisually, "Expected first bar-entry move visual application.");
            Assert.IsTrue(secondMovedVisually, "Expected second follow-up move visual application.");
            Assert.IsTrue(firstLegVisibleAtSecondDispatch, "Expected first leg motion to still be visible when follow-up move dispatch begins.");
        }
        finally
        {
            Object.DestroyImmediate(managerGo);
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void SingleManagerTurnRoll_FinishedCallback_AppliesBothDiceValues()
    {
        var go = new GameObject("BackgammonGameController_SingleManagerRoll");
        try
        {
            var ctrl = go.AddComponent<BackgammonGameController>();
            ctrl.NewGame();
            SetPrivateField(ctrl, "_openingRollResolved", true);
            SetPrivateField(ctrl, "_singleManagerRollInProgress", true);
            SetPrivateField(ctrl, "_singleManagerRollManagerIndex", 1);

            Assert.IsFalse(ctrl.HasRolledThisTurn);
            InvokeNonPublicMethod(ctrl, "OnDiceManagerPlayer1Finished", 5, 2);

            Assert.IsTrue(ctrl.HasRolledThisTurn);
            Assert.AreEqual(5, ctrl.State.Dice1);
            Assert.AreEqual(2, ctrl.State.Dice2);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void CubeOwnership_PersistsAcrossMoveApplyAndTurnFinalize()
    {
        var go = new GameObject("BackgammonGameController_CubeOwnershipPersists");
        try
        {
            var ctrl = go.AddComponent<BackgammonGameController>();
            ctrl.NewGame();
            SetPrivateField(ctrl, "_openingRollResolved", true);
            SetPrivateField(ctrl, "_rolledThisTurn", false);
            ctrl.State.CubeValue = 4;
            ctrl.State.CubeOwner = 1;
            ctrl.DebugSetDiceAndRefresh(6, 1);

            Assert.Greater(ctrl.CurrentLegalTurns.Count, 0, "Expected legal moves for persistence test.");
            Move first = ctrl.CurrentLegalTurns[0].Moves[0];
            Assert.IsTrue(ctrl.TryApplyPreferredTurnForFrom(first.From, true), "Expected first move to apply.");
            Assert.AreEqual(1, ctrl.State.CubeOwner, "Cube owner must persist after move application.");
            Assert.AreEqual(4, ctrl.State.CubeValue, "Cube value must persist after move application.");

            int guard = 0;
            while (ctrl.CurrentLegalTurns.Count > 0 && guard++ < 12)
            {
                Move m = ctrl.CurrentLegalTurns[0].Moves[0];
                Assert.IsTrue(ctrl.TryApplyPreferredTurnForFrom(m.From, true));
            }

            Assert.IsTrue(ctrl.TryFinalizeCurrentTurn(), "Expected turn to finalize for persistence test.");
            Assert.AreEqual(1, ctrl.State.CubeOwner, "Cube owner must persist after turn finalization.");
            Assert.AreEqual(4, ctrl.State.CubeValue, "Cube value must persist after turn finalization.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static IEnumerator InvokeNonPublicEnumerator(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Expected method '{methodName}' to exist.");
        object result = method.Invoke(target, args);
        Assert.That(result, Is.InstanceOf<IEnumerator>(), $"Expected '{methodName}' to return IEnumerator.");
        return (IEnumerator)result;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Expected field '{fieldName}' to exist.");
        field.SetValue(target, value);
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Expected field '{fieldName}' to exist.");
        return field.GetValue(target);
    }

    private static object InvokeNonPublicMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Expected method '{methodName}' to exist.");
        return method.Invoke(target, args);
    }

    private static float ExtractWaitSeconds(object yielded)
    {
        if (yielded == null) return -1f;
        if (yielded is WaitForSeconds wait)
        {
            FieldInfo secondsField = typeof(WaitForSeconds).GetField("m_Seconds", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(secondsField, Is.Not.Null, "Expected WaitForSeconds.m_Seconds backing field.");
            return (float)secondsField.GetValue(wait);
        }

        return -1f;
    }

    private static string ComputeLegalSignature(IReadOnlyList<Turn> legalTurns)
    {
        if (legalTurns == null || legalTurns.Count == 0)
            return "none";
        var parts = new List<string>(legalTurns.Count);
        for (int i = 0; i < legalTurns.Count; i++)
        {
            Turn t = legalTurns[i];
            if (t?.Moves == null || t.Moves.Count == 0)
            {
                parts.Add("empty");
                continue;
            }

            var moveParts = new List<string>(t.Moves.Count);
            for (int m = 0; m < t.Moves.Count; m++)
            {
                Move mv = t.Moves[m];
                moveParts.Add($"{mv.From}>{mv.To}:{(mv.IsHit ? 1 : 0)}");
            }

            parts.Add(string.Join(",", moveParts));
        }

        return string.Join("|", parts);
    }

    private static BoardPoint CreatePoint(string name)
    {
        var pointGo = new GameObject(name);
        var point = pointGo.AddComponent<BoardPoint>();
        point.pointRenderer = pointGo.AddComponent<MeshRenderer>();
        point.Initialize(0, true, Color.gray, 0.1f, 0.5f);
        return point;
    }

    private static GameObject CreateChecker(string name, PlayerColor color)
    {
        var checker = new GameObject(name);
        checker.AddComponent<Checker>().color = color;
        return checker;
    }
}
