using Runtime.RMC.Backgammon.Core;
using Runtime.RMC.Backgammon.Stats;
using Runtime.RMC.Backgammon.UI;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Graduation overlay — partial class extension of BackgammonHudController.
/// Shows a full-screen congratulations panel when the player completes a currency world.
/// </summary>
public partial class BackgammonHudController
{
    private VisualElement _graduationOverlay;
    private VisualElement _graduationCard;
    private Label         _graduationTitleLabel;
    private Label         _graduationBodyLabel;
    private Button        _graduationContinueButton;

    private void InitPrestigeOverlay(VisualElement root)
    {
        _graduationOverlay        = root.Q<VisualElement>("GraduationOverlay");
        _graduationCard           = root.Q<VisualElement>("GraduationCard");
        _graduationTitleLabel     = root.Q<Label>("GraduationTitleLabel");
        _graduationBodyLabel      = root.Q<Label>("GraduationBodyLabel");
        _graduationContinueButton = root.Q<Button>("GraduationContinueButton");

        if (_graduationContinueButton != null)
            _graduationContinueButton.clicked += HideGraduationOverlay;

        PrestigeService.OnWorldGraduated += ShowGraduationOverlay;
    }

    private void ShowGraduationOverlay(GameModePresetSo newPreset)
    {
        if (_graduationOverlay == null) return;

        if (_graduationTitleLabel != null)
            _graduationTitleLabel.text = "World Complete!";

        if (_graduationBodyLabel != null)
        {
            string nextDesc = newPreset != null
                ? $"Welcome to {newPreset.displayName} ({newPreset.currencySymbol})\n\n{newPreset.narrativeDescription}"
                : "You've mastered all currency worlds. Congratulations!";
            _graduationBodyLabel.text = nextDesc;
        }

        _graduationOverlay.style.display = DisplayStyle.Flex;
        PopupAnimator.Show(_graduationCard, this);
        Debug.Log($"[Prestige] Showing graduation overlay for new world: {newPreset?.currencyCode}");
    }

    private void HideGraduationOverlay()
    {
        if (_graduationCard != null)
            PopupAnimator.Hide(_graduationCard, this, () => { if (_graduationOverlay != null) _graduationOverlay.style.display = DisplayStyle.None; });
    }
}
