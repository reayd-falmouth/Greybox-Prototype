using System;
using Runtime.RMC._MyProject_.Core;
using Runtime.RMC.Backgammon.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Runtime.RMC.Backgammon
{
    /// <summary>
    /// Observer that owns Run Mode session logic. Subscribes to BackgammonGameController events
    /// and drives session/ante progression without bloating the controller.
    /// Also implements IHudModeProvider so BackgammonHudController needs no Run-specific coupling.
    /// </summary>
    public class BackgammonRunModeManager : HudModeProviderBase
    {
        [SerializeField] private BackgammonGameController _controller;

        public event Action<RunSessionResult> OnRunSessionComplete;
        public event Action OnRunFailed;
        public event Action<int> OnRunAnteComplete;
        public event Action OnRunWon;

        public RunState RunState { get; private set; }
        public RunConfig ActiveConfig { get; private set; }

        // Set when a session is beaten but the player has not yet clicked Collect.
        public bool HasPendingSessionComplete { get; private set; }

        // ── IHudModeProvider ────────────────────────────────────────────────

        public override GameModeType SupportedMode => GameModeType.Run;

        public override string ScoreDisplay
            => RunState != null ? RunState.RunningScore.ToString() : "0";

        public override string GamesDisplay
        {
            get
            {
                if (RunState == null || ActiveConfig == null) return "—";
                var session = RunState.CurrentSession(ActiveConfig);
                int remaining = Mathf.Max(0, session.MaxGames - RunState.GamesPlayedThisSession);
                return remaining.ToString();
            }
        }

        public override string StakeDisplay
        {
            get
            {
                if (RunState == null || ActiveConfig == null) return "—";
                return RunState.CurrentSession(ActiveConfig).ScoreThreshold.ToString();
            }
        }

        public override string HeadingDisplay
        {
            get
            {
                if (RunState == null || ActiveConfig == null) return "Run";
                var session = RunState.CurrentSession(ActiveConfig);
                string sessionName = RunState.IsBossSession
                    ? $"Boss: {BackgammonHudController.GetBossVariantDisplayName(RunState.ActiveBossVariant)}"
                    : session.SessionType == RunSessionType.Small ? "Small" : "Big";
                return $"Ante {RunState.CurrentAnteIndex + 1} — {sessionName}";
            }
        }

        public override void RefreshModeHud(VisualElement root, BackgammonGameController ctrl)
        {
            if (RunState == null || ActiveConfig == null || root == null) return;

            var chipsValue      = root.Q<Label>("ChipsValue");
            var multiplierValue = root.Q<Label>("MultiplierValue");

            if (chipsValue != null && ctrl != null)
                chipsValue.text = ctrl.MoneySessionBaseStake > 0
                    ? ctrl.MoneySessionBaseStake.ToString()
                    : "1";

            if (multiplierValue != null && ctrl?.State != null)
                multiplierValue.text = ctrl.State.CubeValue.ToString();
        }

        public override void StartGame(string seedString, string startingPositionId)
        {
            if (!string.IsNullOrEmpty(seedString) && DeterministicRNG.Instance != null)
                DeterministicRNG.Instance.SetMasterSeed(seedString);
            StartRun(RunConfig.BuildDefault());
        }

        // ── Run popup button references (bound in BindToHud) ────────────────

        private Button _runCashoutCollectButton;
        private Button _runOverRetryButton;
        private Button _gameOverNextGameButton;

        public override void BindToHud(VisualElement root, BackgammonHudController hud)
        {
            base.BindToHud(root, hud);

            _runCashoutCollectButton = root.Q<Button>("RunCashoutCollectButton");
            _runOverRetryButton      = root.Q<Button>("RunOverRetryButton");
            _gameOverNextGameButton  = root.Q<Button>("GameOverNextGameButton");

            if (_runCashoutCollectButton != null)
                _runCashoutCollectButton.clicked += OnRunCashoutCollectClicked;
            if (_runOverRetryButton != null)
                _runOverRetryButton.clicked += OnRunOverRetryClicked;
            if (_gameOverNextGameButton != null)
                _gameOverNextGameButton.clicked += OnGameOverNextGameClicked;

            OnRunSessionComplete += Hud.ShowRunCashoutPopup;
            OnRunFailed          += OnRunFailedHandler;
        }

        public override void UnbindFromHud()
        {
            if (_runCashoutCollectButton != null)
                _runCashoutCollectButton.clicked -= OnRunCashoutCollectClicked;
            if (_runOverRetryButton != null)
                _runOverRetryButton.clicked -= OnRunOverRetryClicked;
            if (_gameOverNextGameButton != null)
                _gameOverNextGameButton.clicked -= OnGameOverNextGameClicked;

            if (Hud != null) OnRunSessionComplete -= Hud.ShowRunCashoutPopup;
            OnRunFailed          -= OnRunFailedHandler;

            _runCashoutCollectButton = null;
            _runOverRetryButton      = null;
            _gameOverNextGameButton  = null;

            base.UnbindFromHud();
        }

        // ── Controller event subscription ───────────────────────────────────

        private void OnEnable()
        {
            if (_controller != null)
                _controller.OnGameEndedWithScore += HandleGameEnded;
        }

        private void OnDisable()
        {
            if (_controller != null)
                _controller.OnGameEndedWithScore -= HandleGameEnded;
        }

        // ── Public API ─────────────────────────────���────────────────────────

        public void StartRun(RunConfig config)
        {
            ActiveConfig = config ?? RunConfig.BuildDefault();
            RunState = new RunState();

            var bossVariant = ActiveConfig.Antes[0].DrawBossVariant();
            RunState.ResetForNewSession(RunState.IsBossSession ? bossVariant : BossVariantType.Standard);

            Debug.Log($"[Run] StartRun — Ante 1, Small Session, threshold={RunState.CurrentSession(ActiveConfig).ScoreThreshold}");
            _controller.StartRunSession(ActiveConfig, RunState);
        }

        // Called by BackgammonHudController when player clicks "Collect" on the cashout popup.
        public void OnSessionAcknowledged()
        {
            HasPendingSessionComplete = false;

            int prevAnte = RunState.CurrentAnteIndex;
            bool runWon = RunState.AdvanceSession(ActiveConfig);

            if (runWon)
            {
                Debug.Log("[Run] Run Won!");
                OnRunWon?.Invoke();
                return;
            }

            if (RunState.CurrentAnteIndex != prevAnte)
                OnRunAnteComplete?.Invoke(RunState.CurrentAnteIndex + 1);

            Debug.Log($"[Run] Advanced to Ante {RunState.CurrentAnteIndex + 1}, Session {RunState.CurrentSessionIndex + 1}, boss={RunState.ActiveBossVariant}");
            _controller.StartRunSession(ActiveConfig, RunState);
        }

        // ── Private handlers ────────────────────────────────────────────────

        private void HandleGameEnded(int winnerIdx, int baseStake, int cubeValue, int gammonMult)
        {
            if (ActiveConfig == null || RunState == null) return;
            if (RunState.IsRunOver || RunState.IsRunWon) return;

            var sessionCfg = RunState.CurrentSession(ActiveConfig);
            int maxGamesNow = sessionCfg.MaxGames;
            if (RunState.IsBossSession && RunState.ActiveBossVariant == BossVariantType.FewerGames)
                maxGamesNow = Mathf.Max(1, maxGamesNow - 1);

            if (RunState.GamesPlayedThisSession >= maxGamesNow) return;

            RunState.GamesPlayedThisSession++;

            if (winnerIdx == BackgammonPlayerRoles.LocalPlayerIndex)
            {
                int gameScore = baseStake * cubeValue * gammonMult;
                RunState.RunningScore += gameScore;
                Debug.Log($"[Run] Human won — score +{gameScore} → session total {RunState.RunningScore}");
            }
            else
            {
                Debug.Log($"[Run] AI won — session score unchanged at {RunState.RunningScore}");
            }

            int effectiveThreshold = sessionCfg.ScoreThreshold;

            if (RunState.IsBossSession && RunState.ActiveBossVariant == BossVariantType.HigherThreshold)
                effectiveThreshold = Mathf.RoundToInt(effectiveThreshold * 1.5f);

            if (RunState.RunningScore >= effectiveThreshold)
            {
                int effectiveReward = sessionCfg.Reward;
                if (RunState.IsBossSession && RunState.ActiveBossVariant == BossVariantType.HigherThreshold)
                    effectiveReward = Mathf.RoundToInt(effectiveReward * 1.5f);

                RunState.TotalCurrency += effectiveReward;
                HasPendingSessionComplete = true;

                var result = new RunSessionResult(
                    RunState.CurrentAnteIndex,
                    RunState.CurrentSessionIndex,
                    sessionCfg.SessionType,
                    RunState.ActiveBossVariant,
                    RunState.RunningScore,
                    effectiveThreshold,
                    RunState.GamesPlayedThisSession,
                    effectiveReward);

                Debug.Log($"[Run] Session complete! Score {RunState.RunningScore} >= {effectiveThreshold}. Reward ${effectiveReward}");
                OnRunSessionComplete?.Invoke(result);
                return;
            }

            if (RunState.GamesPlayedThisSession >= maxGamesNow)
            {
                RunState.IsRunOver = true;
                Debug.Log($"[Run] Run failed — {RunState.GamesPlayedThisSession} games used, score {RunState.RunningScore} < {effectiveThreshold}");
                OnRunFailed?.Invoke();
            }
        }

        private void OnRunFailedHandler()
        {
            int currency = RunState?.TotalCurrency ?? 0;
            Hud?.HideGameOverPopup();
            Hud?.ShowRunOverPopup(currency);
        }

        private void OnRunCashoutCollectClicked()
        {
            Hud?.HideRunCashoutPopup();
            Hud?.HideGameOverPopup();
            OnSessionAcknowledged();
        }

        private void OnRunOverRetryClicked()
        {
            Hud?.HideRunOverPopup();
            Hud?.HideGameOverPopup();
            if (ActiveConfig != null)
                StartRun(ActiveConfig);
        }

        private void OnGameOverNextGameClicked()
        {
            if (!HasPendingSessionComplete) return;
            // Cashout popup was already shown by OnRunSessionComplete — just hide the game-over popup
            Hud?.HideGameOverPopup();
        }
    }
}
