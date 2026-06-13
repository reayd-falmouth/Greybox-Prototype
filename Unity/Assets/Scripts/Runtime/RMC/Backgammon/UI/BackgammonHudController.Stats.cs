using Runtime.RMC.Backgammon.Stats;
using Runtime.RMC.Backgammon.UI;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Stats tab wiring — partial class extension of BackgammonHudController.</summary>
public partial class BackgammonHudController
{
    [SerializeField] private SessionStatsTracker statsTracker;

    private Button        _statsTabButton;
    private VisualElement _statsTabContent;

    private VisualElement _statsSubTabNavigation;
    private VisualElement _statsSubTabContentSession;
    private VisualElement _statsSubTabContentDice;
    private VisualElement _statsSubTabContentLifetime;
    private VisualElement _statsSubTabContentWallet;
    private VisualElement _statsSubTabContentGame;

    private Label _statsSessionScoreValue;
    private Label _statsSessionGamesValue;
    private Label _statsSessionRollsValue;

    private readonly Label[] _statsDiceLabels = new Label[7];

    private Label _statsRatingValue;
    private Label _statsHighestScoreValue;
    private Label _statsTotalGamesValue;
    private Label _statsSessionsPlayedValue;
    private Label _statsHighestMoneyValue;

    // New persistent stats
    private Label _statsWalletBalanceValue;
    private Label _statsTotalGammonsValue;
    private Label _statsTotalBackgammonsValue;
    private Label _statsCurrentStreakValue;
    private Label _statsLongestStreakValue;
    private Label _statsMoneyEarnedValue;

    private void InitStatsTab(VisualElement root)
    {
        _statsTabButton  = root.Q<Button>("StatsTabButton");
        _statsTabContent = root.Q<VisualElement>("StatsTabContent");

        _statsSessionScoreValue   = root.Q<Label>("StatsSessionScoreValue");
        _statsSessionGamesValue   = root.Q<Label>("StatsSessionGamesValue");
        _statsSessionRollsValue   = root.Q<Label>("StatsSessionRollsValue");

        for (int i = 1; i <= 6; i++)
            _statsDiceLabels[i] = root.Q<Label>($"StatsDie{i}Value");

        _statsRatingValue         = root.Q<Label>("StatsRatingValue");
        _statsHighestScoreValue   = root.Q<Label>("StatsHighestScoreValue");
        _statsTotalGamesValue     = root.Q<Label>("StatsTotalGamesValue");
        _statsSessionsPlayedValue = root.Q<Label>("StatsSessionsPlayedValue");
        _statsHighestMoneyValue   = root.Q<Label>("StatsHighestMoneyValue");

        _statsWalletBalanceValue    = root.Q<Label>("StatsWalletBalanceValue");
        _statsTotalGammonsValue     = root.Q<Label>("StatsTotalGammonsValue");
        _statsTotalBackgammonsValue = root.Q<Label>("StatsTotalBackgammonsValue");
        _statsCurrentStreakValue    = root.Q<Label>("StatsCurrentStreakValue");
        _statsLongestStreakValue    = root.Q<Label>("StatsLongestStreakValue");
        _statsMoneyEarnedValue      = root.Q<Label>("StatsMoneyEarnedValue");

        _statsSubTabNavigation      = root.Q<VisualElement>("StatsSubTabNavigation");
        _statsSubTabContentSession  = root.Q<VisualElement>("StatsSubTabContentSession");
        _statsSubTabContentDice     = root.Q<VisualElement>("StatsSubTabContentDice");
        _statsSubTabContentLifetime = root.Q<VisualElement>("StatsSubTabContentLifetime");
        _statsSubTabContentWallet   = root.Q<VisualElement>("StatsSubTabContentWallet");
        _statsSubTabContentGame     = root.Q<VisualElement>("StatsSubTabContentGame");

        var statsSubContents = new[] {
            _statsSubTabContentSession, _statsSubTabContentDice,
            _statsSubTabContentLifetime, _statsSubTabContentWallet, _statsSubTabContentGame
        };

        var sessionBtn = root.Q<Button>("StatsSubTabSessionBtn");
        if (sessionBtn != null)
            sessionBtn.clicked += () => OptionsModalController.SwitchSubTab(_statsSubTabNavigation, "StatsSubTabSessionBtn", statsSubContents, _statsSubTabContentSession);
        var diceBtn = root.Q<Button>("StatsSubTabDiceBtn");
        if (diceBtn != null)
            diceBtn.clicked += () => OptionsModalController.SwitchSubTab(_statsSubTabNavigation, "StatsSubTabDiceBtn", statsSubContents, _statsSubTabContentDice);
        var lifetimeBtn = root.Q<Button>("StatsSubTabLifetimeBtn");
        if (lifetimeBtn != null)
            lifetimeBtn.clicked += () => OptionsModalController.SwitchSubTab(_statsSubTabNavigation, "StatsSubTabLifetimeBtn", statsSubContents, _statsSubTabContentLifetime);
        var walletBtn = root.Q<Button>("StatsSubTabWalletBtn");
        if (walletBtn != null)
            walletBtn.clicked += () => OptionsModalController.SwitchSubTab(_statsSubTabNavigation, "StatsSubTabWalletBtn", statsSubContents, _statsSubTabContentWallet);
        var gameBtn = root.Q<Button>("StatsSubTabGameBtn");
        if (gameBtn != null)
            gameBtn.clicked += () => OptionsModalController.SwitchSubTab(_statsSubTabNavigation, "StatsSubTabGameBtn", statsSubContents, _statsSubTabContentGame);

        if (_statsTabButton != null)
            _statsTabButton.clicked += () => SwitchTab("Stats");
    }

    internal void RefreshStatsTab()
    {
        SessionStats session = statsTracker != null ? statsTracker.CurrentSession : null;

        if (_statsSessionScoreValue != null)
            _statsSessionScoreValue.text = session != null ? session.SessionScore.ToString("N0") : "0";
        if (_statsSessionGamesValue != null)
            _statsSessionGamesValue.text = session != null ? session.GamesPlayedThisSession.ToString() : "0";
        if (_statsSessionRollsValue != null)
            _statsSessionRollsValue.text = session != null ? session.TotalRollsThisSession.ToString() : "0";

        for (int i = 1; i <= 6; i++)
        {
            if (_statsDiceLabels[i] != null)
            {
                float pct = session != null ? session.GetDiceFacePercent(i) : 0f;
                _statsDiceLabels[i].text = $"{pct:F1}%";
            }
        }

        if (_statsRatingValue != null)
            _statsRatingValue.text = Mathf.RoundToInt(PlayerStats.Rating).ToString();
        if (_statsHighestScoreValue != null)
            _statsHighestScoreValue.text = PlayerStats.HighestScore.ToString("N0");
        if (_statsTotalGamesValue != null)
            _statsTotalGamesValue.text = PlayerStats.TotalGamesPlayed.ToString("N0");
        if (_statsSessionsPlayedValue != null)
            _statsSessionsPlayedValue.text = PlayerStats.TotalSessionsPlayed.ToString();
        if (_statsHighestMoneyValue != null)
            _statsHighestMoneyValue.text = $"${PlayerStats.HighestMoneyEarned:N0}";

        if (_statsWalletBalanceValue != null)
            _statsWalletBalanceValue.text = $"${MoneyBalanceService.Balance:N0}";
        if (_statsTotalGammonsValue != null)
            _statsTotalGammonsValue.text = PlayerStats.TotalGammons.ToString();
        if (_statsTotalBackgammonsValue != null)
            _statsTotalBackgammonsValue.text = PlayerStats.TotalBackgammons.ToString();
        if (_statsCurrentStreakValue != null)
            _statsCurrentStreakValue.text = PlayerStats.CurrentWinStreak.ToString();
        if (_statsLongestStreakValue != null)
            _statsLongestStreakValue.text = PlayerStats.LongestWinStreak.ToString();
        if (_statsMoneyEarnedValue != null)
            _statsMoneyEarnedValue.text = $"${PlayerStats.TotalMoneyEarned:N0}";
    }
}
