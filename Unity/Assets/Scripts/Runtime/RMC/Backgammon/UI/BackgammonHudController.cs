using System.Collections.Generic;
using EngineCore;
using Runtime.RMC.Backgammon.Core;
using Runtime.RMC.Backgammon.Settings;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Binds <see cref="BackgammonHUD.uxml"/> to <see cref="BackgammonGameController"/>.</summary>
public class BackgammonHudController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private BackgammonGameController gameController;

    private Label _statusLabel;
    private Label _diceLabel;
    private Label _positionIdLabel;
    private ScrollView _legalScroll;
    private VisualElement _settingsPanel;
    private FloatField _moveAnimField;
    private IntegerField _aiDepthField;
    private Toggle _opponentAiToggle;
    private Slider _masterVolSlider;
    private Slider _sfxVolSlider;

    private void OnEnable()
    {
        if (gameController == null)
            gameController = FindFirstObjectByType<BackgammonGameController>();

        if (uiDocument == null) return;
        var root = uiDocument.rootVisualElement;
        _statusLabel = root.Q<Label>("StatusLabel");
        _diceLabel = root.Q<Label>("DiceLabel");
        _positionIdLabel = root.Q<Label>("PositionIdLabel");
        _legalScroll = root.Q<ScrollView>("LegalMovesScroll");
        _settingsPanel = root.Q<VisualElement>("SettingsPanel");

        var rollBtn = root.Q<Button>("RollButton");
        if (rollBtn != null) rollBtn.clicked += OnRollClicked;
        var newBtn = root.Q<Button>("NewGameButton");
        if (newBtn != null) newBtn.clicked += OnNewGameClicked;
        var settingsBtn = root.Q<Button>("SettingsToggleButton");
        if (settingsBtn != null) settingsBtn.clicked += ToggleSettings;

        _moveAnimField = root.Q<FloatField>("MoveAnimField");
        _aiDepthField = root.Q<IntegerField>("AiDepthField");
        _opponentAiToggle = root.Q<Toggle>("OpponentAiToggle");
        _masterVolSlider = root.Q<Slider>("MasterVolSlider");
        _sfxVolSlider = root.Q<Slider>("SfxVolSlider");

        if (_moveAnimField != null)
            _moveAnimField.RegisterValueChangedCallback(evt => BackgammonSettings.MoveAnimDurationSeconds = evt.newValue);
        if (_aiDepthField != null)
            _aiDepthField.RegisterValueChangedCallback(evt => BackgammonSettings.AiSearchDepth = evt.newValue);
        if (_opponentAiToggle != null)
            _opponentAiToggle.RegisterValueChangedCallback(evt => BackgammonSettings.OpponentIsAi = evt.newValue);
        if (_masterVolSlider != null)
            _masterVolSlider.RegisterValueChangedCallback(evt =>
            {
                BackgammonSettings.MasterVolumeLinear = evt.newValue;
                AudioListener.volume = evt.newValue;
            });
        if (_sfxVolSlider != null)
            _sfxVolSlider.RegisterValueChangedCallback(evt => BackgammonSettings.SfxVolumeLinear = evt.newValue);

        LoadSettingsIntoFields();
        AudioListener.volume = BackgammonSettings.MasterVolumeLinear;
    }

    private void OnDisable()
    {
        if (uiDocument == null) return;
        var root = uiDocument.rootVisualElement;
        var rollBtn = root.Q<Button>("RollButton");
        if (rollBtn != null) rollBtn.clicked -= OnRollClicked;
        var newBtn = root.Q<Button>("NewGameButton");
        if (newBtn != null) newBtn.clicked -= OnNewGameClicked;
        var settingsBtn = root.Q<Button>("SettingsToggleButton");
        if (settingsBtn != null) settingsBtn.clicked -= ToggleSettings;
    }

    private void LoadSettingsIntoFields()
    {
        if (_moveAnimField != null) _moveAnimField.SetValueWithoutNotify(BackgammonSettings.MoveAnimDurationSeconds);
        if (_aiDepthField != null) _aiDepthField.SetValueWithoutNotify(BackgammonSettings.AiSearchDepth);
        if (_opponentAiToggle != null) _opponentAiToggle.SetValueWithoutNotify(BackgammonSettings.OpponentIsAi);
        if (_masterVolSlider != null) _masterVolSlider.SetValueWithoutNotify(BackgammonSettings.MasterVolumeLinear);
        if (_sfxVolSlider != null) _sfxVolSlider.SetValueWithoutNotify(BackgammonSettings.SfxVolumeLinear);
    }

    private void ToggleSettings()
    {
        if (_settingsPanel == null) return;
        bool on = _settingsPanel.style.display == DisplayStyle.None;
        _settingsPanel.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void OnRollClicked()
    {
        if (gameController != null)
            gameController.RequestRollDice();
    }

    private void OnNewGameClicked()
    {
        gameController?.NewGame();
    }

    public void RefreshAll(BackgammonGameController ctrl)
    {
        gameController = ctrl;
        if (ctrl == null || ctrl.State == null) return;

        if (_statusLabel != null)
        {
            if (ctrl.IsGameOver(out string w))
                _statusLabel.text = "Game over — " + w;
            else
                _statusLabel.text = ctrl.State.PlayerOnRoll == 0 ? "Turn: Player 0" : "Turn: Player 1";
        }

        if (_diceLabel != null)
        {
            if (ctrl.State.Dice1 > 0 && ctrl.State.Dice2 > 0)
                _diceLabel.text = $"Dice: {ctrl.State.Dice1}-{ctrl.State.Dice2}";
            else
                _diceLabel.text = "Dice: — (roll)";
        }

        if (_positionIdLabel != null)
        {
            try
            {
                BackgammonGameRules.SyncBoardArrayFromCheckerArrays(ctrl.State);
                _positionIdLabel.text = "PID: " + PositionId.Encode(ctrl.State);
            }
            catch
            {
                _positionIdLabel.text = "PID: (encode failed)";
            }
        }

        if (ctrl.IsGameOver(out _))
            _legalScroll?.Clear();
        else
            RebuildLegalList(ctrl);
    }

    private void RebuildLegalList(BackgammonGameController ctrl)
    {
        if (_legalScroll == null) return;
        _legalScroll.Clear();
        IReadOnlyList<Turn> legals = ctrl.CurrentLegalTurns;
        for (int i = 0; i < legals.Count; i++)
        {
            int idx = i;
            Turn t = legals[i];
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 4 } };
            var btn = new Button(() =>
            {
                ctrl.PreviewTurnHighlights(t);
                ctrl.TryApplyTurnByIndex(idx);
            })
            {
                text = t.ToString()
            };
            btn.style.flexGrow = 1;
            row.Add(btn);
            _legalScroll.Add(row);
        }
    }
}
