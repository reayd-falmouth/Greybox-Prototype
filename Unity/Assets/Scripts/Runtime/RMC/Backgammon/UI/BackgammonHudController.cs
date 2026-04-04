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
    private Label _gameScoreValue;
    private Label _chipsValue;
    private Label _multiplierValue;
    private Label _gamesValue;
    private Label _rollsValue;
    private Label _stakeValue;
    private Label _anteValue;
    private Label _roundValue;

    private ScrollView _legalScroll;
    private VisualElement _settingsPanel;
    private VisualElement _takeDropPanel;
    private VisualElement _doublePanel;
    private FloatField _moveAnimField;
    private IntegerField _aiDepthField;
    private Toggle _opponentAiToggle;
    private Slider _masterVolSlider;
    private Slider _sfxVolSlider;

    private Button _rollButton;
    private Button _undoButton;
    private Button _playMoveButton;

    private int _selectedLegalIndex;

    private void OnEnable()
    {
        if (gameController == null)
            gameController = FindFirstObjectByType<BackgammonGameController>();

        if (uiDocument == null) return;
        var root = uiDocument.rootVisualElement;

        _statusLabel = root.Q<Label>("StatusLabel");
        _diceLabel = root.Q<Label>("DiceLabel");
        _positionIdLabel = root.Q<Label>("PositionIdLabel");
        _gameScoreValue = root.Q<Label>("GameScoreValue");
        _chipsValue = root.Q<Label>("ChipsValue");
        _multiplierValue = root.Q<Label>("MultiplierValue");
        _gamesValue = root.Q<Label>("GamesValue");
        _rollsValue = root.Q<Label>("RollsValue");
        _stakeValue = root.Q<Label>("StakeValue");
        _anteValue = root.Q<Label>("AnteValue");
        _roundValue = root.Q<Label>("RoundValue");

        _legalScroll = root.Q<ScrollView>("LegalMovesScroll");
        _settingsPanel = root.Q<VisualElement>("SettingsPanel");
        _takeDropPanel = root.Q<VisualElement>("TakeDropPanel");
        _doublePanel = root.Q<VisualElement>("DoublePanel");

        _moveAnimField = root.Q<FloatField>("MoveAnimField");
        _aiDepthField = root.Q<IntegerField>("AiDepthField");
        _opponentAiToggle = root.Q<Toggle>("OpponentAiToggle");
        _masterVolSlider = root.Q<Slider>("MasterVolSlider");
        _sfxVolSlider = root.Q<Slider>("SfxVolSlider");

        _rollButton = root.Q<Button>("RollButton");
        if (_rollButton != null) _rollButton.clicked += OnRollClicked;

        var newBtn = root.Q<Button>("NewGameButton");
        if (newBtn != null) newBtn.clicked += OnNewGameClicked;

        var settingsBtn = root.Q<Button>("SettingsToggleButton");
        if (settingsBtn != null) settingsBtn.clicked += ToggleSettings;

        var optionsBtn = root.Q<Button>("OptionsButton");
        if (optionsBtn != null) optionsBtn.clicked += ToggleSettings;

        var runInfoBtn = root.Q<Button>("RunInfoButton");
        if (runInfoBtn != null) runInfoBtn.clicked += OnRunInfoClicked;

        _playMoveButton = root.Q<Button>("PlayMoveButton");
        if (_playMoveButton != null) _playMoveButton.clicked += OnPlayMoveClicked;

        _undoButton = root.Q<Button>("UndoButton");
        if (_undoButton != null) _undoButton.clicked += OnUndoClicked;

        var viewHoriz = root.Q<Button>("ViewHorizButton");
        if (viewHoriz != null) viewHoriz.clicked += OnViewHorizClicked;

        var viewVert = root.Q<Button>("ViewVertButton");
        if (viewVert != null) viewVert.clicked += OnViewVertClicked;

        var doubleBtn = root.Q<Button>("DoubleButton");
        if (doubleBtn != null) doubleBtn.clicked += OnDoubleClicked;

        var takeBtn = root.Q<Button>("TakeDoubleButton");
        if (takeBtn != null) takeBtn.clicked += OnTakeDoubleClicked;

        var dropBtn = root.Q<Button>("DropDoubleButton");
        if (dropBtn != null) dropBtn.clicked += OnDropDoubleClicked;

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

        SetDoubleOfferVisible(false);
    }

    private void OnDisable()
    {
        if (uiDocument == null) return;
        var root = uiDocument.rootVisualElement;

        if (_rollButton != null) _rollButton.clicked -= OnRollClicked;

        var newBtn = root.Q<Button>("NewGameButton");
        if (newBtn != null) newBtn.clicked -= OnNewGameClicked;

        var settingsBtn = root.Q<Button>("SettingsToggleButton");
        if (settingsBtn != null) settingsBtn.clicked -= ToggleSettings;

        var optionsBtn = root.Q<Button>("OptionsButton");
        if (optionsBtn != null) optionsBtn.clicked -= ToggleSettings;

        var runInfoBtn = root.Q<Button>("RunInfoButton");
        if (runInfoBtn != null) runInfoBtn.clicked -= OnRunInfoClicked;

        if (_playMoveButton != null) _playMoveButton.clicked -= OnPlayMoveClicked;
        if (_undoButton != null) _undoButton.clicked -= OnUndoClicked;

        var doubleBtn = root.Q<Button>("DoubleButton");
        if (doubleBtn != null) doubleBtn.clicked -= OnDoubleClicked;

        var takeBtn = root.Q<Button>("TakeDoubleButton");
        if (takeBtn != null) takeBtn.clicked -= OnTakeDoubleClicked;

        var dropBtn = root.Q<Button>("DropDoubleButton");
        if (dropBtn != null) dropBtn.clicked -= OnDropDoubleClicked;

        var viewHoriz = root.Q<Button>("ViewHorizButton");
        if (viewHoriz != null) viewHoriz.clicked -= OnViewHorizClicked;

        var viewVert = root.Q<Button>("ViewVertButton");
        if (viewVert != null) viewVert.clicked -= OnViewVertClicked;
    }

    private void OnViewHorizClicked()
    {
        gameController?.SetBoardViewHorizontal(true);
    }

    private void OnViewVertClicked()
    {
        gameController?.SetBoardViewHorizontal(false);
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

    public void SetDoubleOfferVisible(bool visible)
    {
        if (_takeDropPanel == null) return;
        _takeDropPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
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

    private void OnRunInfoClicked()
    {
        gameController?.DebugPrintBoardConsole();
    }

    private void OnPlayMoveClicked()
    {
        if (gameController == null) return;
        gameController.TryApplyTurnByIndex(_selectedLegalIndex);
    }

    private void OnUndoClicked()
    {
        gameController?.TryUndoLastMove();
    }

    private void OnDoubleClicked()
    {
        gameController?.OfferDouble();
    }

    private void OnTakeDoubleClicked()
    {
        gameController?.RespondDoubleTake();
    }

    private void OnDropDoubleClicked()
    {
        gameController?.RespondDoubleDrop();
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

        if (_gameScoreValue != null)
            _gameScoreValue.text = $"{ctrl.State.Player1Score}-{ctrl.State.Player2Score}";

        if (_chipsValue != null)
        {
            int o = ctrl.State.CubeOwner;
            _chipsValue.text = o == 3 ? "C" : o.ToString();
        }

        if (_multiplierValue != null)
            _multiplierValue.text = ctrl.State.CubeValue.ToString();

        if (_gamesValue != null)
        {
            int ml = ctrl.Match?.MatchLength ?? 0;
            _gamesValue.text = ml > 0 ? ml.ToString() : "∞";
        }

        if (_rollsValue != null)
            _rollsValue.text = ctrl.RollsThisGame.ToString();

        if (_stakeValue != null)
            _stakeValue.text = ctrl.Match?.MatchLength > 0 ? "Match" : "Money";

        if (_anteValue != null)
        {
            int ml = ctrl.Match?.MatchLength ?? 0;
            _anteValue.text = ml > 0 ? $"{ctrl.State.Player1Score}/{ml}" : "—";
        }

        if (_roundValue != null)
            _roundValue.text = ctrl.TurnsCompletedThisGame.ToString();

        if (_rollButton != null)
            _rollButton.SetEnabled(!ctrl.IsGameOver(out _) && !ctrl.HasRolledThisTurn && !ctrl.IsBusy);

        if (_undoButton != null)
            _undoButton.SetEnabled(ctrl.CanUndo);

        if (_playMoveButton != null)
        {
            bool canPlay = ctrl.HasRolledThisTurn && ctrl.CurrentLegalTurns.Count > 0 && !ctrl.IsGameOver(out _) && !ctrl.IsBusy;
            _playMoveButton.SetEnabled(canPlay);
        }

        if (_doublePanel != null)
        {
            bool showDouble = !ctrl.IsGameOver(out _) && !ctrl.IsBusy && !ctrl.HasRolledThisTurn && !ctrl.AwaitingDoubleResponse && ctrl.State.CubeValue < 64;
            _doublePanel.style.display = showDouble ? DisplayStyle.Flex : DisplayStyle.None;
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
        if (legals.Count == 0)
        {
            _selectedLegalIndex = 0;
            return;
        }

        if (_selectedLegalIndex >= legals.Count)
            _selectedLegalIndex = 0;

        for (int i = 0; i < legals.Count; i++)
        {
            int idx = i;
            Turn t = legals[i];
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 4 } };
            var btn = new Button(() =>
            {
                _selectedLegalIndex = idx;
                ctrl.PreviewTurnHighlights(t);
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
