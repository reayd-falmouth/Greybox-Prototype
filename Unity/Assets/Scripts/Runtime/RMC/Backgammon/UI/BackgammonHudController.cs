using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using EngineCore;
using Runtime.RMC.Backgammon.Core;
using Runtime.RMC.Backgammon.Settings;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.UIElements;

public enum UiFeedbackEventType
{
    PopupOpen,
    PopupClose
}

[System.Serializable]
public class UiFeedbackSlot
{
    public UiFeedbackEventType eventType;
    [Tooltip("MMF_Player component (or a parent GameObject with one underneath).")]
    public Component feedbackPlayer;
}

/// <summary>Binds <see cref="BackgammonHUD.uxml"/> to <see cref="BackgammonGameController"/>.</summary>
public class BackgammonHudController : MonoBehaviour
{
    private enum ModalMode
    {
        Settings,
        Hints,
        LegalMoves
    }

    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private BackgammonGameController gameController;
    [SerializeField] private BackgammonDebugPositionLibrary debugPositionLibrary;

    private Label _statusLabel;
    private Label _diceLabel;
    private Label _positionIdLabel;
    private Label _matchScoreValue;
    private Label _targetMatchScoreLabel;
    private Label _chipsValue;
    private Label _multiplierValue;
    private Label _gamesValue;
    private Label _rollsValue;
    private Label _stakeValue;
    private Label _headingLabel;
    private Label _gameOverPopupTitleLabel;
    private Label _gameOverPopupSummaryLabel;
    private Label _pipCountPlayerValue;
    private Label _pipCountAiValue;

    private ScrollView _legalScroll;
    private VisualElement _settingsPanel;
    private VisualElement _modalLayer;
    private VisualElement _modalBackdrop;
    private VisualElement _settingsContent;
    private VisualElement _hintsContent;
    private VisualElement _legalMovesContent;
    private Label _modalTitleLabel;
    private Label _hintsLabel;
    private Label _legalMovesEmptyLabel;
    private VisualElement _takeDropPanel;
    private VisualElement _doublePanel;
    private VisualElement _gameOverPopupLayer;
    private FloatField _moveAnimField;
    private IntegerField _aiDepthField;
    private DropdownField _aiEngineDropdown;
    private Toggle _opponentAiToggle;
    private Slider _masterVolSlider;
    private Slider _sfxVolSlider;
    private Slider _gameSpeedSlider;
    private DropdownField _debugPositionDropdown;
    private TextField _debugPositionTextField;

    private Button _rollButton;
    private Button _undoButton;
    private Button _playMoveButton;
    private Button _legalMovesButton;
    private Button _modalCloseButton;
    private Button _gameOverNextGameButton;
    private Button _debugPositionApplyButton;
    private Button _debugPositionUseCurrentButton;
    private Button _gameTabButton;
    private Button _audioTabButton;
    private Button _debugTabButton;
    private VisualElement _gameTabContent;
    private VisualElement _audioTabContent;
    private VisualElement _debugTabContent;
    private VisualElement _newGameModalLayer;
    private VisualElement _newGameModalBackdrop;
    private Button _newGameModalCloseButton;
    private Button _gameTypePrevButton;
    private Button _gameTypeNextButton;
    private Button _startNewGameButton;
    private Label _gameTypeLabel;
    private int _selectedGameTypeIndex = 0;

    // Money Session Settings
    private VisualElement _moneySessionSettings;
    private Label _gameModeDescription;
    private IntegerField _baseStakeField;
    private Toggle _autoDoublesToggle;
    private Toggle _beaversToggle;
    private Toggle _raccoonsToggle;
    private Toggle _ardvarksToggle;
    private Toggle _jacobyToggle;

    [Header("Game Mode Presets")]
    [SerializeField] private GameModePresetLibrarySo gameModePresets;

    [Header("UI Feedback")]
    [SerializeField] private List<UiFeedbackSlot> uiFeedbackSlots = new List<UiFeedbackSlot>();

    [Header("Board view")]
    [Tooltip("Log when Horiz/Vert toggles engine→board mapping (identity vs 23−e).")]
    [SerializeField] private bool enableBoardViewDebugLogs;
    [SerializeField] private bool enableUndoPerformanceLogs;

    private int _selectedLegalIndex;
    private ModalMode _activeModalMode = ModalMode.Settings;
    private bool _gameOverPopupShown;
    private string _lastLegalSignature;
    private int _legalListRebuildCount;
    private static readonly ProfilerMarker HudRefreshMarker = new("Backgammon.HUD.RefreshAll");
    private static readonly ProfilerMarker HudRebuildLegalsMarker = new("Backgammon.HUD.RebuildLegalList");
    public int LegalListRebuildCount => _legalListRebuildCount;
    private readonly List<string> _debugPositionChoices = new();

    private void OnEnable()
    {
        if (gameController == null)
            gameController = FindFirstObjectByType<BackgammonGameController>();

        if (uiDocument == null) return;
        var root = uiDocument.rootVisualElement;

        _statusLabel = root.Q<Label>("StatusLabel");
        _diceLabel = root.Q<Label>("DiceLabel");
        _positionIdLabel = root.Q<Label>("PositionIdLabel");
        _matchScoreValue = root.Q<Label>("MatchScoreValue");
        _targetMatchScoreLabel = root.Q<Label>("TargetMatchScoreLabel");
        _chipsValue = root.Q<Label>("ChipsValue");
        _multiplierValue = root.Q<Label>("MultiplierValue");
        _gamesValue = root.Q<Label>("GamesValue");
        _rollsValue = root.Q<Label>("RollsValue");
        _stakeValue = root.Q<Label>("StakeValue");
        _headingLabel = root.Q<Label>("HeadingLabel");
        _gameOverPopupTitleLabel = root.Q<Label>("GameOverPopupTitleLabel");
        _gameOverPopupSummaryLabel = root.Q<Label>("GameOverPopupSummaryLabel");
        _pipCountPlayerValue = root.Q<Label>("PipCountPlayerValue");
        _pipCountAiValue = root.Q<Label>("PipCountAiValue");

        _legalScroll = root.Q<ScrollView>("LegalMovesScroll");
        _settingsPanel = root.Q<VisualElement>("SettingsPanel");
        _modalLayer = root.Q<VisualElement>("ModalLayer");
        _modalBackdrop = root.Q<VisualElement>("ModalBackdrop");
        _settingsContent = root.Q<VisualElement>("SettingsContent");
        _hintsContent = root.Q<VisualElement>("HintsContent");
        _legalMovesContent = root.Q<VisualElement>("LegalMovesContent");
        _modalTitleLabel = root.Q<Label>("ModalTitleLabel");
        _hintsLabel = root.Q<Label>("HintsLabel");
        _legalMovesEmptyLabel = root.Q<Label>("LegalMovesEmptyLabel");
        _takeDropPanel = root.Q<VisualElement>("TakeDropPanel");
        _doublePanel = root.Q<VisualElement>("DoublePanel");
        _gameOverPopupLayer = root.Q<VisualElement>("GameOverPopupLayer");

        _moveAnimField = root.Q<FloatField>("MoveAnimField");
        _aiDepthField = root.Q<IntegerField>("AiDepthField");
        _aiEngineDropdown = root.Q<DropdownField>("AiEngineDropdown");
        _opponentAiToggle = root.Q<Toggle>("OpponentAiToggle");
        _masterVolSlider = root.Q<Slider>("MasterVolSlider");
        _sfxVolSlider = root.Q<Slider>("SfxVolSlider");
        _gameSpeedSlider = root.Q<Slider>("GameSpeedSlider");
        _debugPositionDropdown = root.Q<DropdownField>("DebugStartPositionDropdown");
        _debugPositionTextField = root.Q<TextField>("DebugStartPositionField");

        _rollButton = root.Q<Button>("RollButton");
        if (_rollButton != null) _rollButton.clicked += OnRollClicked;

        var newBtn = root.Q<Button>("NewGameButton");
        if (newBtn != null) newBtn.clicked += OnNewGameClicked;

        var settingsBtn = root.Q<Button>("SettingsToggleButton");
        if (settingsBtn != null) settingsBtn.clicked += ToggleSettings;

        _legalMovesButton = root.Q<Button>("LegalMovesButton");
        if (_legalMovesButton != null) _legalMovesButton.clicked += OnLegalMovesClicked;

        var runInfoBtn = root.Q<Button>("RunInfoButton");
        if (runInfoBtn != null) runInfoBtn.clicked += ToggleSettings;

        _modalCloseButton = root.Q<Button>("ModalCloseButton");
        if (_modalCloseButton != null) _modalCloseButton.clicked += HideModal;
        _gameOverNextGameButton = root.Q<Button>("GameOverNextGameButton");
        if (_gameOverNextGameButton != null) _gameOverNextGameButton.clicked += OnGameOverNextGameClicked;
        _debugPositionApplyButton = root.Q<Button>("DebugStartPositionApplyButton");
        if (_debugPositionApplyButton != null) _debugPositionApplyButton.clicked += OnDebugStartPositionApplyClicked;
        _debugPositionUseCurrentButton = root.Q<Button>("DebugStartPositionUseCurrentButton");
        if (_debugPositionUseCurrentButton != null) _debugPositionUseCurrentButton.clicked += OnDebugStartPositionUseCurrentClicked;

        _gameTabButton = root.Q<Button>("GameTabButton");
        _audioTabButton = root.Q<Button>("AudioTabButton");
        _debugTabButton = root.Q<Button>("DebugTabButton");

        _gameTabContent = root.Q<VisualElement>("GameTabContent");
        _audioTabContent = root.Q<VisualElement>("AudioTabContent");
        _debugTabContent = root.Q<VisualElement>("DebugTabContent");

        if (_gameTabButton != null) _gameTabButton.clicked += () => SwitchTab("Game");
        if (_audioTabButton != null) _audioTabButton.clicked += () => SwitchTab("Audio");
        if (_debugTabButton != null) _debugTabButton.clicked += () => SwitchTab("Debug");

        _newGameModalLayer = root.Q<VisualElement>("NewGameModalLayer");
        _newGameModalBackdrop = root.Q<VisualElement>("NewGameModalBackdrop");
        _newGameModalCloseButton = root.Q<Button>("NewGameModalCloseButton");
        _gameTypePrevButton = root.Q<Button>("GameTypePrevButton");
        _gameTypeNextButton = root.Q<Button>("GameTypeNextButton");
        _startNewGameButton = root.Q<Button>("StartNewGameButton");
        _gameTypeLabel = root.Q<Label>("GameTypeLabel");

        // Money Session Settings
        _moneySessionSettings = root.Q<VisualElement>("MoneySessionSettings");
        _gameModeDescription = root.Q<Label>("GameModeDescription");
        _baseStakeField = root.Q<IntegerField>("BaseStakeField");
        _autoDoublesToggle = root.Q<Toggle>("AutoDoublesToggle");
        _beaversToggle = root.Q<Toggle>("BeaversToggle");
        _raccoonsToggle = root.Q<Toggle>("RaccoonsToggle");
        _ardvarksToggle = root.Q<Toggle>("ArdvarksToggle");
        _jacobyToggle = root.Q<Toggle>("JacobyToggle");

        if (_newGameModalCloseButton != null) _newGameModalCloseButton.clicked += HideNewGameModal;
        if (_gameTypePrevButton != null) _gameTypePrevButton.clicked += OnGameTypePrev;
        if (_gameTypeNextButton != null) _gameTypeNextButton.clicked += OnGameTypeNext;
        if (_startNewGameButton != null) _startNewGameButton.clicked += OnStartNewGame;

        if (_newGameModalBackdrop != null)
            _newGameModalBackdrop.RegisterCallback<ClickEvent>(OnNewGameModalBackdropClicked);

        if (_modalBackdrop != null)
            _modalBackdrop.RegisterCallback<ClickEvent>(OnModalBackdropClicked);

        _playMoveButton = root.Q<Button>("PlayMoveButton");
        if (_playMoveButton != null) _playMoveButton.clicked += OnPlayMoveClicked;

        _undoButton = root.Q<Button>("UndoButton");
        if (_undoButton != null) _undoButton.clicked += OnUndoClicked;

        var viewHoriz = root.Q<Button>("ViewHorizButton");
        if (viewHoriz != null) viewHoriz.clicked += OnBoardViewToggleClicked;

        var viewVert = root.Q<Button>("ViewVertButton");
        if (viewVert != null) viewVert.clicked += OnBoardViewToggleClicked;

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
        if (_aiEngineDropdown != null)
        {
            _aiEngineDropdown.choices = new System.Collections.Generic.List<string> { "LocalNeuralNet", "GnubgPython" };
            _aiEngineDropdown.RegisterValueChangedCallback(evt =>
            {
                BackgammonSettings.AiEngineType = evt.newValue;
                BackgammonAIEvaluatorFactory.ClearCache();
            });
            _aiEngineDropdown.value = BackgammonSettings.AiEngineType;
        }
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
        if (_gameSpeedSlider != null)
            _gameSpeedSlider.RegisterValueChangedCallback(evt =>
            {
                BackgammonSettings.GameSpeedSecondsPerStep = evt.newValue;
                gameController?.SetPresentationGameSpeed(evt.newValue);
            });
        if (_debugPositionDropdown != null)
            _debugPositionDropdown.RegisterValueChangedCallback(OnDebugPositionDropdownChanged);

        LoadSettingsIntoFields();
        AudioListener.volume = BackgammonSettings.MasterVolumeLinear;

        SetDoubleOfferVisible(false);
    }

    private void OnDisable()
    {
        if (uiDocument == null) return;
        var root = uiDocument.rootVisualElement;
        if (root == null) return;

        if (_rollButton != null) _rollButton.clicked -= OnRollClicked;

        var newBtn = root.Q<Button>("NewGameButton");
        if (newBtn != null) newBtn.clicked -= OnNewGameClicked;

        var settingsBtn = root.Q<Button>("SettingsToggleButton");
        if (settingsBtn != null) settingsBtn.clicked -= ToggleSettings;

        if (_legalMovesButton != null) _legalMovesButton.clicked -= OnLegalMovesClicked;

        var runInfoBtn = root.Q<Button>("RunInfoButton");
        if (runInfoBtn != null) runInfoBtn.clicked -= ToggleSettings;

        if (_modalCloseButton != null) _modalCloseButton.clicked -= HideModal;
        if (_gameOverNextGameButton != null) _gameOverNextGameButton.clicked -= OnGameOverNextGameClicked;
        if (_debugPositionApplyButton != null) _debugPositionApplyButton.clicked -= OnDebugStartPositionApplyClicked;
        if (_debugPositionUseCurrentButton != null) _debugPositionUseCurrentButton.clicked -= OnDebugStartPositionUseCurrentClicked;

        if (_modalBackdrop != null)
            _modalBackdrop.UnregisterCallback<ClickEvent>(OnModalBackdropClicked);

        if (_playMoveButton != null) _playMoveButton.clicked -= OnPlayMoveClicked;
        if (_undoButton != null) _undoButton.clicked -= OnUndoClicked;

        var doubleBtn = root.Q<Button>("DoubleButton");
        if (doubleBtn != null) doubleBtn.clicked -= OnDoubleClicked;

        var takeBtn = root.Q<Button>("TakeDoubleButton");
        if (takeBtn != null) takeBtn.clicked -= OnTakeDoubleClicked;

        var dropBtn = root.Q<Button>("DropDoubleButton");
        if (dropBtn != null) dropBtn.clicked -= OnDropDoubleClicked;

        var viewHoriz = root.Q<Button>("ViewHorizButton");
        if (viewHoriz != null) viewHoriz.clicked -= OnBoardViewToggleClicked;

        var viewVert = root.Q<Button>("ViewVertButton");
        if (viewVert != null) viewVert.clicked -= OnBoardViewToggleClicked;
        if (_debugPositionDropdown != null)
            _debugPositionDropdown.UnregisterValueChangedCallback(OnDebugPositionDropdownChanged);
    }

    /// <summary>
    /// Both buttons flip between identity mapping and 23−e reverse (no stuck state when already in one mode).
    /// </summary>
    private void OnBoardViewToggleClicked()
    {
        if (gameController == null) return;
        bool wasIdentityMapping = BackgammonBoardLayout.ActiveViewMode == BackgammonBoardViewMode.Horizontal;
        bool nextWantsIdentityMapping = !wasIdentityMapping;
        gameController.SetBoardViewHorizontal(nextWantsIdentityMapping);

        if (enableBoardViewDebugLogs)
        {
            bool nowIdentity = BackgammonBoardLayout.ActiveViewMode == BackgammonBoardViewMode.Horizontal;
            Debug.Log(
                $"[Backgammon][View] toggled board mapping: wasIdentity={wasIdentityMapping}, nowIdentity={nowIdentity}, " +
                $"paramSent={nextWantsIdentityMapping}");
        }
    }

    private void LoadSettingsIntoFields()
    {
        if (_moveAnimField != null) _moveAnimField.SetValueWithoutNotify(BackgammonSettings.MoveAnimDurationSeconds);
        if (_aiDepthField != null) _aiDepthField.SetValueWithoutNotify(BackgammonSettings.AiSearchDepth);
        if (_aiEngineDropdown != null) _aiEngineDropdown.SetValueWithoutNotify(BackgammonSettings.AiEngineType);
        if (_opponentAiToggle != null) _opponentAiToggle.SetValueWithoutNotify(BackgammonSettings.OpponentIsAi);
        if (_masterVolSlider != null) _masterVolSlider.SetValueWithoutNotify(BackgammonSettings.MasterVolumeLinear);
        if (_sfxVolSlider != null) _sfxVolSlider.SetValueWithoutNotify(BackgammonSettings.SfxVolumeLinear);
        if (_gameSpeedSlider != null) _gameSpeedSlider.SetValueWithoutNotify(BackgammonSettings.GameSpeedSecondsPerStep);
        RefreshDebugPositionChoices();
    }

    private void RefreshDebugPositionChoices()
    {
        if (_debugPositionDropdown == null)
            return;

        _debugPositionChoices.Clear();
        if (debugPositionLibrary != null && debugPositionLibrary.Entries != null)
        {
            for (int i = 0; i < debugPositionLibrary.Entries.Count; i++)
            {
                BackgammonDebugPositionLibrary.Entry entry = debugPositionLibrary.Entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.positionId))
                    continue;
                string label = string.IsNullOrWhiteSpace(entry.label) ? $"PID {i + 1}" : entry.label.Trim();
                _debugPositionChoices.Add($"{label} | {entry.positionId.Trim()}");
            }
        }

        if (_debugPositionChoices.Count == 0)
            _debugPositionChoices.Add("(No saved debug positions)");

        _debugPositionDropdown.choices = _debugPositionChoices;
        _debugPositionDropdown.SetValueWithoutNotify(_debugPositionChoices[0]);
    }

    private void OnDebugPositionDropdownChanged(ChangeEvent<string> evt)
    {
        if (_debugPositionTextField == null || string.IsNullOrWhiteSpace(evt.newValue))
            return;
        string pid = ExtractPositionIdFromDropdownValue(evt.newValue);
        if (!string.IsNullOrWhiteSpace(pid))
            _debugPositionTextField.SetValueWithoutNotify(pid);
    }

    private void OnDebugStartPositionUseCurrentClicked()
    {
        if (_debugPositionTextField == null || gameController?.State == null)
            return;
        try
        {
            BackgammonGameRules.SyncBoardArrayFromCheckerArrays(gameController.State);
            string pid = PositionId.Encode(gameController.State);
            _debugPositionTextField.SetValueWithoutNotify(pid);
            Debug.Log($"[Backgammon][DebugStart] Captured current PositionId into debug field pid={pid}");
        }
        catch
        {
            Debug.LogWarning("[Backgammon][DebugStart] Could not encode current PositionId for debug field.");
        }
    }

    private void OnDebugStartPositionApplyClicked()
    {
        if (_debugPositionTextField == null || gameController == null)
            return;
        string pid = _debugPositionTextField.value != null ? _debugPositionTextField.value.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(pid))
        {
            Debug.LogWarning("[Backgammon][DebugStart] Debug start PositionId is empty.");
            return;
        }

        bool ok = gameController.TryStartFromPositionId(pid);
        if (!ok)
            Debug.LogWarning($"[Backgammon][DebugStart] Failed to apply debug start PositionId pid={pid}");
    }

    private static string ExtractPositionIdFromDropdownValue(string value)
    {
        const string separator = " | ";
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        int idx = value.LastIndexOf(separator);
        if (idx < 0)
            return string.Empty;
        return value.Substring(idx + separator.Length).Trim();
    }

    private void ToggleSettings()
    {
        if (_modalLayer == null) return;
        if (_modalLayer.style.display == DisplayStyle.Flex && _settingsContent != null && _settingsContent.style.display == DisplayStyle.Flex)
        {
            Debug.Log("[Backgammon][UI][Modal] ToggleSettings requested close while settings were already visible.");
            HideModal();
            return;
        }

        ShowSettings();
    }

    private void ShowSettings()
    {
        PlayPopupOpenSound();
        SetModalVisible(true, ModalMode.Settings);
        SwitchTab("Game");  // Always start on Game tab after modal is visible
        Debug.Log("[Backgammon][UI][Modal] Opened modal in Options mode.");
    }

    private void OnHintsClicked()
    {
        ShowHints();
    }

    public void ShowHints()
    {
        PlayPopupOpenSound();
        SetModalVisible(true, ModalMode.Hints);
        Debug.Log("[Backgammon][UI][Modal] Opened modal in Hints mode.");
    }

    private void OnLegalMovesClicked()
    {
        ToggleLegalMoves();
    }

    private void ToggleLegalMoves()
    {
        if (_modalLayer == null) return;
        if (_modalLayer.style.display == DisplayStyle.Flex && _activeModalMode == ModalMode.LegalMoves)
        {
            Debug.Log("[Backgammon][UI][Modal] ToggleLegalMoves requested close while legal moves were already visible.");
            HideModal();
            return;
        }

        ShowLegalMoves();
    }

    private void ShowLegalMoves()
    {
        PlayPopupOpenSound();
        SetModalVisible(true, ModalMode.LegalMoves);
        Debug.Log("[Backgammon][UI][Modal] Opened modal in Legal Moves mode.");
    }

    public void SetHintsText(string hintsText)
    {
        if (_hintsLabel == null) return;
        _hintsLabel.text = string.IsNullOrWhiteSpace(hintsText) ? "No hints available yet." : hintsText;
        Debug.Log("[Backgammon][UI][Modal] Updated hints text.");
    }

    private void HideModal()
    {
        if (_modalLayer == null) return;
        PlayPopupCloseSound();
        _modalLayer.style.display = DisplayStyle.None;
        Debug.Log("[Backgammon][UI][Modal] Closed modal.");
    }

    private void SwitchTab(string tabName)
    {
        Debug.Log($"[Backgammon][UI][Modal] SwitchTab called with: {tabName}");
        Debug.Log($"[Backgammon][UI][Modal] Tab elements null check - Game: {_gameTabContent == null}, Audio: {_audioTabContent == null}, Debug: {_debugTabContent == null}");

        // Hide all tabs
        if (_gameTabContent != null)
        {
            _gameTabContent.style.display = DisplayStyle.None;
            Debug.Log("[Backgammon][UI][Modal] Hid Game tab");
        }
        if (_audioTabContent != null)
        {
            _audioTabContent.style.display = DisplayStyle.None;
            Debug.Log("[Backgammon][UI][Modal] Hid Audio tab");
        }
        if (_debugTabContent != null)
        {
            _debugTabContent.style.display = DisplayStyle.None;
            Debug.Log("[Backgammon][UI][Modal] Hid Debug tab");
        }

        // Remove active class from all buttons
        _gameTabButton?.RemoveFromClassList("modal-tab-button-active");
        _audioTabButton?.RemoveFromClassList("modal-tab-button-active");
        _debugTabButton?.RemoveFromClassList("modal-tab-button-active");

        // Show selected tab and activate button
        switch (tabName)
        {
            case "Game":
                if (_gameTabContent != null)
                {
                    _gameTabContent.style.display = DisplayStyle.Flex;
                    Debug.Log("[Backgammon][UI][Modal] Showing Game tab");
                }
                _gameTabButton?.AddToClassList("modal-tab-button-active");
                break;
            case "Audio":
                if (_audioTabContent != null)
                {
                    _audioTabContent.style.display = DisplayStyle.Flex;
                    Debug.Log("[Backgammon][UI][Modal] Showing Audio tab");
                }
                _audioTabButton?.AddToClassList("modal-tab-button-active");
                break;
            case "Debug":
                if (_debugTabContent != null)
                {
                    _debugTabContent.style.display = DisplayStyle.Flex;
                    Debug.Log("[Backgammon][UI][Modal] Showing Debug tab");
                }
                _debugTabButton?.AddToClassList("modal-tab-button-active");
                break;
        }

        Debug.Log($"[Backgammon][UI][Modal] Switched to {tabName} tab.");
    }

    private void ShowNewGameModal()
    {
        if (_newGameModalLayer == null) return;
        PlayPopupOpenSound();
        _newGameModalLayer.style.display = DisplayStyle.Flex;
        _selectedGameTypeIndex = 0;
        UpdateGameTypeLabel();
        Debug.Log("[Backgammon][UI][NewGameModal] Opened New Game modal.");
    }

    private void HideNewGameModal()
    {
        if (_newGameModalLayer == null) return;
        PlayPopupCloseSound();
        _newGameModalLayer.style.display = DisplayStyle.None;
        Debug.Log("[Backgammon][UI][NewGameModal] Closed New Game modal.");
    }

    private int PresetCount => gameModePresets != null ? gameModePresets.presets.Count : 0;

    private GameModePresetSo CurrentPreset()
    {
        if (gameModePresets == null || gameModePresets.presets.Count == 0) return null;
        int idx = Mathf.Clamp(_selectedGameTypeIndex, 0, gameModePresets.presets.Count - 1);
        return gameModePresets.presets[idx];
    }

    private void OnGameTypePrev()
    {
        _selectedGameTypeIndex--;
        if (_selectedGameTypeIndex < 0)
            _selectedGameTypeIndex = Mathf.Max(0, PresetCount - 1);
        UpdateGameTypeLabel();
        Debug.Log($"[Backgammon][UI][NewGameModal] Selected game type index: {_selectedGameTypeIndex}");
    }

    private void OnGameTypeNext()
    {
        _selectedGameTypeIndex++;
        if (_selectedGameTypeIndex >= PresetCount)
            _selectedGameTypeIndex = 0;
        UpdateGameTypeLabel();
        Debug.Log($"[Backgammon][UI][NewGameModal] Selected game type index: {_selectedGameTypeIndex}");
    }

    private void UpdateGameTypeLabel()
    {
        var preset = CurrentPreset();
        if (preset == null) return;

        if (_gameTypeLabel != null)
            _gameTypeLabel.text = preset.displayName;

        if (_gameModeDescription != null)
            _gameModeDescription.text = preset.description;

        bool isMoneySession = preset.gameModeType == GameModeType.MoneySession;
        if (_moneySessionSettings != null)
            _moneySessionSettings.style.display = isMoneySession ? DisplayStyle.Flex : DisplayStyle.None;

        if (isMoneySession && preset.defaultConfig != null)
        {
            if (_baseStakeField != null) _baseStakeField.value = preset.defaultConfig.BaseStake;
            if (_autoDoublesToggle != null) _autoDoublesToggle.value = preset.defaultConfig.AutoDoublesEnabled;
            if (_beaversToggle != null) _beaversToggle.value = preset.defaultConfig.BeaversAllowed;
            if (_raccoonsToggle != null) _raccoonsToggle.value = preset.defaultConfig.RaccoonsAllowed;
            if (_ardvarksToggle != null) _ardvarksToggle.value = preset.defaultConfig.ArdvarksAllowed;
            if (_jacobyToggle != null) _jacobyToggle.value = preset.defaultConfig.JacobyRule;
        }
    }

    private void OnStartNewGame()
    {
        var preset = CurrentPreset();
        GameModeType modeType = preset != null ? preset.gameModeType : (GameModeType)_selectedGameTypeIndex;
        string displayName = preset != null ? preset.displayName : modeType.ToString();

        Debug.Log($"[Backgammon][UI][NewGameModal] Starting new game: {displayName}");

        MoneySessionConfig config = null;
        if (modeType == GameModeType.MoneySession)
        {
            var defaults = preset?.defaultConfig ?? new MoneySessionConfig();
            config = new MoneySessionConfig
            {
                BaseStake = _baseStakeField?.value ?? defaults.BaseStake,
                AutoDoublesEnabled = _autoDoublesToggle?.value ?? defaults.AutoDoublesEnabled,
                BeaversAllowed = _beaversToggle?.value ?? defaults.BeaversAllowed,
                RaccoonsAllowed = _raccoonsToggle?.value ?? defaults.RaccoonsAllowed,
                ArdvarksAllowed = _ardvarksToggle?.value ?? defaults.ArdvarksAllowed,
                JacobyRule = _jacobyToggle?.value ?? defaults.JacobyRule
            };
            Debug.Log($"[Backgammon][UI][NewGameModal] Money Session config: BaseStake={config.BaseStake}, Jacoby={config.JacobyRule}, Beavers={config.BeaversAllowed}");
        }

        HideNewGameModal();
        gameController?.StartNewGameWithConfig(modeType, config);
        HideGameOverPopup();

        if (_headingLabel != null)
            _headingLabel.text = displayName;
    }

    private void OnNewGameModalBackdropClicked(ClickEvent evt)
    {
        if (_newGameModalBackdrop == null) return;
        if (!ReferenceEquals(evt.target, _newGameModalBackdrop))
            return;
        HideNewGameModal();
        Debug.Log("[Backgammon][UI][NewGameModal] Closed modal via backdrop click.");
    }

    private void SetModalVisible(bool isVisible, ModalMode modalMode)
    {
        if (_modalLayer == null) return;

        _modalLayer.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        _activeModalMode = modalMode;

        bool showSettings = modalMode == ModalMode.Settings;
        bool showHints = modalMode == ModalMode.Hints;
        bool showLegalMoves = modalMode == ModalMode.LegalMoves;

        if (_modalTitleLabel != null)
            _modalTitleLabel.text = showSettings ? "Options" : showHints ? "Hints" : "Legal Moves";
        if (_settingsContent != null)
            _settingsContent.style.display = showSettings ? DisplayStyle.Flex : DisplayStyle.None;
        if (_hintsContent != null)
            _hintsContent.style.display = showHints ? DisplayStyle.Flex : DisplayStyle.None;
        if (_legalMovesContent != null)
            _legalMovesContent.style.display = showLegalMoves ? DisplayStyle.Flex : DisplayStyle.None;

        if (showHints && _hintsLabel != null && string.IsNullOrWhiteSpace(_hintsLabel.text))
            _hintsLabel.text = "Hints appear here.";
    }

    private void OnModalBackdropClicked(ClickEvent evt)
    {
        if (_modalBackdrop == null) return;
        if (!ReferenceEquals(evt.target, _modalBackdrop))
            return;

        Debug.Log("[Backgammon][UI][Modal] Backdrop click detected. Closing modal.");
        HideModal();
        evt.StopPropagation();
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
        ShowNewGameModal();
    }

    private void OnPlayMoveClicked()
    {
        if (gameController == null) return;
        gameController.TryFinalizeCurrentTurn();
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

    private void OnGameOverNextGameClicked()
    {
        Debug.Log("[Backgammon][UI][GameEnd] Next game requested from popup.");
        gameController?.NewGame();
        HideGameOverPopup();
    }

    public void ShowGameOverPopup(string summaryText)
    {
        if (_gameOverPopupLayer == null)
            return;

        _gameOverPopupShown = true;
        PlayPopupOpenSound();
        _gameOverPopupLayer.style.display = DisplayStyle.Flex;
        if (_gameOverPopupTitleLabel != null)
            _gameOverPopupTitleLabel.text = "Game Over";
        if (_gameOverPopupSummaryLabel != null)
            _gameOverPopupSummaryLabel.text = string.IsNullOrWhiteSpace(summaryText) ? "Game finished." : summaryText;
        Debug.Log($"[Backgammon][UI][GameEnd] Popup shown summary=\"{summaryText}\"");
    }

    public void HideGameOverPopup()
    {
        if (_gameOverPopupLayer == null)
            return;
        if (_gameOverPopupShown)
        {
            PlayPopupCloseSound();
            _gameOverPopupShown = false;
        }
        _gameOverPopupLayer.style.display = DisplayStyle.None;
    }

    public static bool ShouldEnableRollButton(bool isGameOver, bool hasRolledThisTurn, bool isBusy, bool canPlayerAct)
    {
        return !isGameOver && !hasRolledThisTurn && !isBusy && canPlayerAct;
    }

    public static bool ShouldEnableRollButtonForPhase(
        bool isOpeningRollPhase,
        bool isGameOver,
        bool hasRolledThisTurn,
        bool isBusy,
        bool awaitingDoubleResponse,
        bool canPlayerAct)
    {
        if (isOpeningRollPhase)
            return !isGameOver && !isBusy && !awaitingDoubleResponse;
        return ShouldEnableRollButton(isGameOver, hasRolledThisTurn, isBusy, canPlayerAct);
    }

    public static bool ShouldEnableDoubleButton(
        bool openingRollResolved,
        bool isGameOver,
        bool isBusy,
        bool hasRolledThisTurn,
        bool awaitingDoubleResponse,
        int cubeValue,
        int cubeOwner,
        int playerOnRoll,
        int localPlayerIndex,
        bool canPlayerAct)
    {
        bool cubeIsCentered = cubeOwner == 3 || cubeOwner < 0;
        bool cubeOwnedByCurrentPlayer = cubeOwner == playerOnRoll;
        bool canCurrentPlayerOfferByOwnership = cubeIsCentered || cubeOwnedByCurrentPlayer;
        bool cubeOwnedByLocalPlayer = cubeOwner == localPlayerIndex;
        bool localCanOfferByOwnership = cubeIsCentered || cubeOwnedByLocalPlayer;

        return openingRollResolved
               && !isGameOver
               && !isBusy
               && !hasRolledThisTurn
               && !awaitingDoubleResponse
               && cubeValue < 64
               && canCurrentPlayerOfferByOwnership
               && localCanOfferByOwnership
               && canPlayerAct;
    }

    public void RefreshAll(BackgammonGameController ctrl)
    {
        using var refreshScope = HudRefreshMarker.Auto();
        gameController = ctrl;
        if (ctrl == null || ctrl.State == null) return;
        if (!ctrl.IsAwaitingNextGameFromPopup)
            HideGameOverPopup();
        bool isGameOver = ctrl.IsGameOver(out _);
        bool isOpeningRollPhase = !ctrl.OpeningRollResolved;
        // Pre-roll interaction must stay enabled on the local player's turn.
        // CanShowMovableCheckerInteraction() is intentionally post-roll/move-highlight oriented.
        bool canPlayerAct = !BackgammonSettings.OpponentIsAi
                            || !BackgammonPlayerRoles.IsAiTurnInOpponentAiMode(ctrl.State.PlayerOnRoll);

        if (_statusLabel != null)
        {
            if (ctrl.IsGameOver(out string w))
                _statusLabel.text = "Game over — " + w;
            else if (!ctrl.OpeningRollResolved)
            {
                if (ctrl.OpeningRollAwaitingReroll)
                    _statusLabel.text = "Opening: tied — roll again";
                else
                    _statusLabel.text = "Opening: each player one die — roll";
            }
            else
                _statusLabel.text = ctrl.State.PlayerOnRoll == 0 ? "Turn: Player 0" : "Turn: Player 1";
        }

        if (_diceLabel != null)
        {
            if (!ctrl.OpeningRollResolved)
            {
                if (ctrl.OpeningRollAwaitingReroll)
                    _diceLabel.text = "Dice: tie";
                else
                    _diceLabel.text = "Dice: — (opening roll)";
            }
            else if (ctrl.State.Dice1 > 0 && ctrl.State.Dice2 > 0)
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

        if (_matchScoreValue != null)
        {
            if (ctrl.CurrentGameMode == GameModeType.MoneySession)
            {
                int p1 = ctrl.MoneySessionPlayer1Score;
                int p2 = ctrl.MoneySessionPlayer2Score;
                _matchScoreValue.text = $"${p1} vs ${p2}";
            }
            else
            {
                _matchScoreValue.text = $"{ctrl.CurrentMatchScore}";
            }
        }

        if (_targetMatchScoreLabel != null)
            _targetMatchScoreLabel.text = $"Target Score: {ctrl.CurrentMatchTargetScore}";

        if (_chipsValue != null)
        {
            _chipsValue.text = $"{ctrl.CurrentMatchBaseStake}";
        }

        if (_multiplierValue != null)
            _multiplierValue.text = ctrl.State.CubeValue.ToString();

        if (_gamesValue != null)
        {
            if (ctrl.CurrentGameMode == GameModeType.MoneySession)
            {
                _gamesValue.text = $"{ctrl.MoneySessionGamesPlayed}";
            }
            else
            {
                int maxGames = Mathf.Max(1, ctrl.CurrentMatchMaxGames);
                int gamesLeft = Mathf.Max(0, maxGames - ctrl.CurrentMatchGamesPlayed);
                _gamesValue.text = $"{gamesLeft}/{maxGames}";
            }
        }

        if (_rollsValue != null)
            _rollsValue.text = ctrl.RollsThisGame.ToString();

        if (_headingLabel != null)
            _headingLabel.text = "Money Session";

        if (_stakeValue != null)
        {
            if (ctrl.CurrentGameMode == GameModeType.MoneySession)
                _stakeValue.text = ctrl.MoneySessionBaseStake > 0 ? $"${ctrl.MoneySessionBaseStake}" : "—";
            else
                _stakeValue.text = $"${ctrl.RunCurrency}";
        }

        if (_pipCountPlayerValue != null)
            _pipCountPlayerValue.text = ctrl.State != null ? ctrl.CalculatePipCountPlayer1().ToString() : "—";
        if (_pipCountAiValue != null)
            _pipCountAiValue.text = ctrl.State != null ? ctrl.CalculatePipCountPlayer2().ToString() : "—";

        if (_rollButton != null)
            _rollButton.SetEnabled(ShouldEnableRollButtonForPhase(
                isOpeningRollPhase,
                isGameOver,
                ctrl.HasRolledThisTurn,
                ctrl.IsBusy,
                ctrl.AwaitingDoubleResponse,
                canPlayerAct));

        if (_undoButton != null)
            _undoButton.SetEnabled(ctrl.CanUndo);

        if (_playMoveButton != null)
        {
            bool canPlay = ctrl.CanFinalizeCurrentTurn;
            _playMoveButton.SetEnabled(canPlay);
        }

        if (_doublePanel != null)
        {
            _doublePanel.style.display = DisplayStyle.Flex;
            bool canDouble = ShouldEnableDoubleButton(
                ctrl.OpeningRollResolved,
                isGameOver,
                ctrl.IsBusy,
                ctrl.HasRolledThisTurn,
                ctrl.AwaitingDoubleResponse,
                ctrl.State.CubeValue,
                ctrl.State.CubeOwner,
                ctrl.State.PlayerOnRoll,
                BackgammonPlayerRoles.LocalPlayerIndex,
                canPlayerAct);
            Button doubleButton = _doublePanel.Q<Button>("DoubleButton");
            if (doubleButton != null)
                doubleButton.SetEnabled(canDouble);
        }

        if (isGameOver)
        {
            _legalScroll?.Clear();
            _lastLegalSignature = null;
        }
        else
            RebuildLegalListIfChanged(ctrl);
    }

    private void RebuildLegalListIfChanged(BackgammonGameController ctrl)
    {
        string signature = ComputeLegalSignature(ctrl.CurrentLegalTurns);
        if (signature == _lastLegalSignature)
            return;
        _lastLegalSignature = signature;
        RebuildLegalList(ctrl);
    }

    public static string ComputeLegalSignature(IReadOnlyList<Turn> legals)
    {
        if (legals == null || legals.Count == 0) return "none";
        var sb = new StringBuilder(legals.Count * 8);
        for (int i = 0; i < legals.Count; i++)
        {
            Turn t = legals[i];
            if (t == null || t.Moves == null || t.Moves.Count == 0)
            {
                sb.Append("e;");
                continue;
            }

            Move first = t.Moves[0];
            sb.Append(first.From).Append('>').Append(first.To);
            if (first.IsHit) sb.Append('h');
            sb.Append(';');
        }

        return sb.ToString();
    }

    private void RebuildLegalList(BackgammonGameController ctrl)
    {
        using var rebuildScope = HudRebuildLegalsMarker.Auto();
        if (_legalScroll == null) return;
        _legalListRebuildCount++;
        _legalScroll.Clear();
        IReadOnlyList<Turn> legals = ctrl.CurrentLegalTurns;
        bool hasMoves = legals.Count > 0;
        if (_legalMovesEmptyLabel != null)
            _legalMovesEmptyLabel.style.display = hasMoves ? DisplayStyle.None : DisplayStyle.Flex;

        if (legals.Count == 0)
        {
            _selectedLegalIndex = 0;
            Debug.LogWarning($"[Backgammon][UI][LegalMoves] Rebuild produced no legal moves. playerOnRoll={ctrl.State.PlayerOnRoll}, hasRolled={ctrl.HasRolledThisTurn}");
            return;
        }

        if (_selectedLegalIndex >= legals.Count)
            _selectedLegalIndex = 0;

        bool hasRanking = BackgammonAIService.TryEvaluateAllTurns(
            ctrl.State, ctrl.Match, legals, out var ranked);

        int count = hasRanking ? ranked.Count : legals.Count;
        for (int i = 0; i < count; i++)
        {
            int idx = i;
            Turn turn = hasRanking ? ranked[i].turn : legals[i];
            float equity = hasRanking ? ranked[i].equity : float.NaN;

            string label;
            if (hasRanking)
            {
                string star = i == 0 ? "★ " : "   ";
                string scoreStr = $"{equity:+0.00;-0.00}";
                label = $"{star}{scoreStr}  {turn}";
            }
            else
            {
                label = turn.ToString();
            }

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 4 } };
            var btn = new Button(() =>
            {
                _selectedLegalIndex = idx;
                ctrl.PreviewTurnHighlights(turn);
            }) { text = label };
            btn.style.flexGrow = 1;
            if (i == 0 && hasRanking)
                btn.style.color = new StyleColor(new Color(0.7f, 1f, 0.7f));
            row.Add(btn);
            _legalScroll.Add(row);
        }

        Debug.Log($"[Backgammon][UI][LegalMoves] Rebuilt {legals.Count} moves (ranked={hasRanking}).");
        if (enableUndoPerformanceLogs)
            Debug.Log($"[Backgammon][Undo][Perf] legalListRebuildCount={_legalListRebuildCount} legalCount={legals.Count}");
    }

    // ========== UI Feedback System ==========

    private void PlayPopupOpenSound() => PlayUiFeedback(UiFeedbackEventType.PopupOpen);
    private void PlayPopupCloseSound() => PlayUiFeedback(UiFeedbackEventType.PopupClose);

    private void PlayUiFeedback(UiFeedbackEventType eventType)
    {
        foreach (var slot in uiFeedbackSlots)
        {
            if (slot.eventType != eventType) continue;
            if (slot.feedbackPlayer == null) continue;
            Component playable = ResolvePlayableTarget(slot.feedbackPlayer);
            if (playable != null)
                TryInvokePlayFeedbacks(playable);
            return;
        }
    }

    private static Component ResolvePlayableTarget(Component candidate)
    {
        if (candidate == null) return null;
        if (HasPlayFeedbacksMethod(candidate)) return candidate;
        return FindPlayableOnOrUnder(candidate.transform);
    }

    private static Component FindPlayableOnOrUnder(Transform root)
    {
        if (root == null) return null;
        Component[] components = root.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (HasPlayFeedbacksMethod(components[i]))
                return components[i];
        }
        return null;
    }

    private static bool HasPlayFeedbacksMethod(Component candidate)
    {
        if (candidate == null) return false;
        Type t = candidate.GetType();
        return t.GetMethod("PlayFeedbacks", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null) != null
               || t.GetMethod("PlayFeedbacks", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(Vector3) }, null) != null
               || t.GetMethod("PlayFeedbacks", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(Vector3), typeof(float) }, null) != null;
    }

    private static bool TryInvokePlayFeedbacks(Component target)
    {
        Type t = target.GetType();
        MethodInfo m0 = t.GetMethod("PlayFeedbacks", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
        if (m0 != null)
        {
            try
            {
                m0.Invoke(target, null);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Backgammon][UI][PopupAudio] PlayFeedbacks threw: {ex.Message}");
                return false;
            }
        }

        MethodInfo m1 = t.GetMethod("PlayFeedbacks", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(Vector3) }, null);
        if (m1 != null)
        {
            try
            {
                m1.Invoke(target, new object[] { Vector3.zero });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Backgammon][UI][PopupAudio] PlayFeedbacks(Vector3) threw: {ex.Message}");
                return false;
            }
        }

        MethodInfo m2 = t.GetMethod("PlayFeedbacks", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(Vector3), typeof(float) }, null);
        if (m2 != null)
        {
            try
            {
                m2.Invoke(target, new object[] { Vector3.zero, 1f });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Backgammon][UI][PopupAudio] PlayFeedbacks(Vector3,float) threw: {ex.Message}");
                return false;
            }
        }

        return false;
    }
}
