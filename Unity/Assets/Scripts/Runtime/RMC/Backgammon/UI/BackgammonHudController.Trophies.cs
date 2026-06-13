using Runtime.RMC.Backgammon.Core;
using Runtime.RMC.Backgammon.Stats;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Trophies tab wiring — partial class extension of BackgammonHudController.</summary>
public partial class BackgammonHudController
{
    [SerializeField] private TrophyService trophyService;

    private Button        _trophiesTabButton;
    private VisualElement _trophiesTabContent;
    private ScrollView    _trophiesScrollView;

    private void InitTrophiesTab(VisualElement root)
    {
        _trophiesTabButton  = root.Q<Button>("TrophiesTabButton");
        _trophiesTabContent = root.Q<VisualElement>("TrophiesTabContent");
        _trophiesScrollView = root.Q<ScrollView>("TrophiesScrollView");

        if (_trophiesTabButton != null)
            _trophiesTabButton.clicked += () => SwitchTab("Trophies");

        if (trophyService != null)
            trophyService.OnTrophyUnlocked += _ => RefreshTrophiesTab();

        PrestigeService.OnPrestigeChanged += RefreshTrophiesTab;
    }

    internal void RefreshTrophiesTab()
    {
        if (_trophiesScrollView == null) return;
        _trophiesScrollView.Clear();

        // ── Preset progress strip ───────────────────────────────────────────
        if (gameModePresets?.presets != null)
        {
            var worldStrip = new VisualElement();
            worldStrip.AddToClassList("prestige-world-strip");

            for (int i = 0; i < gameModePresets.presets.Count; i++)
            {
                var preset = gameModePresets.presets[i];
                bool isCurrent   = i == PrestigeService.CurrentWorldIndex;
                bool isCompleted = PrestigeService.IsWorldCompleted(preset.currencyCode);

                var badge = new VisualElement();
                badge.AddToClassList("prestige-world-badge");
                if (isCurrent)   badge.AddToClassList("prestige-world-current");
                if (isCompleted) badge.AddToClassList("prestige-world-completed");
                if (!isCurrent && !isCompleted) badge.AddToClassList("prestige-world-locked");

                var codeLabel = new Label(preset.currencySymbol);
                codeLabel.AddToClassList("prestige-world-symbol");
                badge.Add(codeLabel);

                var nameLabel = new Label(preset.currencyCode);
                nameLabel.AddToClassList("prestige-world-code");
                badge.Add(nameLabel);

                worldStrip.Add(badge);
            }
            _trophiesScrollView.Add(worldStrip);
        }

        // ── Current preset header ───────────────────────────────────────────
        var currentPreset = PrestigeService.CurrentPreset;
        if (currentPreset != null && trophyService != null)
        {
            int total    = trophyService.GetAllTrophies().Count;
            int unlocked = 0;
            foreach (var t in trophyService.GetAllTrophies())
                if (t.isUnlocked) unlocked++;

            var header = new Label($"{currentPreset.displayName} — {unlocked} / {total} trophies");
            header.AddToClassList("stats-section-header");
            _trophiesScrollView.Add(header);

            if (!string.IsNullOrEmpty(currentPreset.narrativeDescription))
            {
                var narrative = new Label(currentPreset.narrativeDescription);
                narrative.AddToClassList("prestige-narrative");
                _trophiesScrollView.Add(narrative);
            }
        }

        // ── Trophy list for current preset ──────────────────────────────────
        if (trophyService == null) return;

        foreach (var trophy in trophyService.GetAllTrophies())
        {
            var entry = new VisualElement();
            entry.AddToClassList("trophy-entry");
            if (!trophy.isUnlocked)
                entry.AddToClassList("trophy-locked");

            var nameLabel = new Label(trophy.isUnlocked ? trophy.name : "???");
            nameLabel.AddToClassList("trophy-name");

            var sym = PrestigeService.CurrentPreset?.currencySymbol ?? "$";
            var descLabel = new Label(trophy.isUnlocked
                ? trophy.description
                : $"Win at {sym}{trophy.stakeAmount} stake to unlock");
            descLabel.AddToClassList("trophy-desc");

            var stakeLabel = new Label($"{sym}{trophy.stakeAmount} stake");
            stakeLabel.AddToClassList("trophy-stake");

            entry.Add(nameLabel);
            entry.Add(descLabel);
            entry.Add(stakeLabel);

            if (trophy.isUnlocked && !string.IsNullOrEmpty(trophy.dateUnlocked))
            {
                var dateLabel = new Label(trophy.dateUnlocked);
                dateLabel.AddToClassList("trophy-date");
                entry.Add(dateLabel);
            }

            _trophiesScrollView.Add(entry);
        }
    }
}
