using System.Collections.Generic;
using Runtime.RMC.Backgammon.Core;
using Runtime.RMC.Backgammon.Stats;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Collection tab — shows all poker chip collectibles organised into currency sub-tabs.
/// Chips are full-colour when the stake trophy is earned, greyscale when locked.
/// Partial class extension of BackgammonHudController.
/// </summary>
public partial class BackgammonHudController
{
    private Button        _collectionTabButton;
    private VisualElement _collectionTabContent;
    private VisualElement _collectionSubTabNavigation;
    private VisualElement _collectionSubTabContentHost;

    private readonly List<VisualElement> _collectionSubTabContents = new();

    private void InitCollectionTab(VisualElement root)
    {
        _collectionTabButton         = root.Q<Button>("CollectionTabButton");
        _collectionTabContent        = root.Q<VisualElement>("CollectionTabContent");
        _collectionSubTabNavigation  = root.Q<VisualElement>("CollectionSubTabNavigation");
        _collectionSubTabContentHost = root.Q<VisualElement>("CollectionSubTabContentHost");

        if (_collectionTabButton != null)
            _collectionTabButton.clicked += () => SwitchTab("Collection");

        if (trophyService != null)
            trophyService.OnTrophyUnlocked += _ => RefreshCollectionTab();

        PrestigeService.OnPrestigeChanged  += RefreshCollectionTab;
        PrestigeService.OnWorldGraduated   += _ => RefreshCollectionTab();
    }

    internal void RefreshCollectionTab()
    {
        if (_collectionSubTabNavigation == null || _collectionSubTabContentHost == null) return;
        if (gameModePresets?.presets == null) return;

        _collectionSubTabNavigation.Clear();
        _collectionSubTabContentHost.Clear();
        _collectionSubTabContents.Clear();

        int totalChips = 0, unlockedChips = 0;
        CountAllChips(ref totalChips, ref unlockedChips);

        for (int wi = 0; wi < gameModePresets.presets.Count; wi++)
        {
            var preset    = gameModePresets.presets[wi];
            bool isActive   = wi == PrestigeService.CurrentWorldIndex;
            bool isComplete = PrestigeService.IsWorldCompleted(preset.currencyCode);
            bool isLocked   = !isActive && !isComplete;

            string tabLabel = isLocked ? "???" : preset.currencyCode;
            int capturedIndex = wi;

            // Sub-tab button
            var btn = new Button();
            btn.text = tabLabel;
            btn.AddToClassList("modal-subtab-button");
            if (wi == 0) btn.AddToClassList("modal-subtab-button-active");
            btn.name = $"CollectionSubTabBtn_{wi}";
            _collectionSubTabNavigation.Add(btn);

            // Content panel
            var panel = new ScrollView();
            panel.AddToClassList("collection-scroll-view");
            panel.style.display = wi == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _collectionSubTabContents.Add(panel);
            _collectionSubTabContentHost.Add(panel);

            btn.clicked += () =>
            {
                _collectionSubTabNavigation.Query<Button>().ForEach(b => b.RemoveFromClassList("modal-subtab-button-active"));
                btn.AddToClassList("modal-subtab-button-active");
                foreach (var p in _collectionSubTabContents)
                    p.style.display = DisplayStyle.None;
                panel.style.display = DisplayStyle.Flex;
            };

            // Summary header for this currency
            int currTotal = 0, currUnlocked = 0;
            CountChipsForPreset(preset, ref currTotal, ref currUnlocked);
            var summaryLabel = new Label(isLocked
                ? $"??? — Locked"
                : $"{preset.displayName} ({preset.currencySymbol})  {currUnlocked}/{currTotal}");
            summaryLabel.AddToClassList("collection-summary-label");
            panel.Add(summaryLabel);

            // Chip grid
            var chipGrid = new VisualElement();
            chipGrid.AddToClassList("collection-chip-grid");
            panel.Add(chipGrid);

            if (preset.stakes == null) continue;

            foreach (var stake in preset.stakes)
            {
                string trophyId = $"trophy_{preset.currencyCode}_stake_{stake.stakeAmount}";
                bool isEarned   = IsTrophyEarned(trophyId);

                var chipCell = new VisualElement();
                chipCell.AddToClassList("collection-chip-cell");

                var chipImage = new VisualElement();
                chipImage.AddToClassList("collection-chip-image");
                if (preset.chipSprite != null)
                    chipImage.style.backgroundImage = new StyleBackground(preset.chipSprite);
                if (!isEarned || isLocked)
                    chipImage.AddToClassList("collection-chip-locked");
                chipCell.Add(chipImage);

                var stakeLabel = new Label(isEarned && !isLocked
                    ? $"{preset.currencySymbol}{stake.stakeAmount}"
                    : "?");
                stakeLabel.AddToClassList("collection-chip-label");
                if (!isEarned || isLocked)
                    stakeLabel.AddToClassList("collection-chip-label-locked");
                chipCell.Add(stakeLabel);

                chipGrid.Add(chipCell);
            }
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void CountAllChips(ref int total, ref int unlocked)
    {
        if (gameModePresets?.presets == null) return;
        foreach (var preset in gameModePresets.presets)
            CountChipsForPreset(preset, ref total, ref unlocked);
    }

    private void CountChipsForPreset(GameModePresetSo preset, ref int total, ref int unlocked)
    {
        if (preset.stakes == null) return;
        foreach (var stake in preset.stakes)
        {
            total++;
            string id = $"trophy_{preset.currencyCode}_stake_{stake.stakeAmount}";
            if (IsTrophyEarned(id)) unlocked++;
        }
    }

    private bool IsTrophyEarned(string trophyId)
    {
        if (trophyService == null) return false;
        foreach (var t in trophyService.GetAllTrophies())
            if (t.trophyId == trophyId && t.isUnlocked) return true;
        return false;
    }
}
