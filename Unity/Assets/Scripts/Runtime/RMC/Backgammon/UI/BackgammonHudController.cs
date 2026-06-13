using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using EngineCore;
using Runtime.RMC._MyProject_.Core;
using Runtime.RMC.Backgammon;
using Runtime.RMC.Backgammon.Core;
using Runtime.RMC.Backgammon.Settings;
using Runtime.RMC.Backgammon.Stats;
using Runtime.RMC.Backgammon.UI;
using UnityEngine;
using Unity.Profiling;
using TMPro;
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
public partial class BackgammonHudController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private BackgammonGameController gameController;
    [SerializeField] private BackgammonDebugPositionLibrary debugPositionLibrary;

    [Header("Mode Providers")]
    [SerializeField] private List<HudModeProviderBase> _modeProviders = new();

    /// <summary>Exposes the game controller reference for HudModeProviderBase subclasses.</summary>
    public BackgammonGameController GameControllerRef => gameController;

    private IHudModeProvider _activeProvider;

    [Header("World-Space Pip Count")]
    [SerializeField] private TMP_Text pipCountPlayerWorldLabel;
    [SerializeField] private TMP_Text pipCountAiWorldLabel;

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
    private Label _pipCountPlayerValue;
    private Label _pipCountAiValue;

    private VisualElement _settingsPanel;
    private VisualElement _takeDropPanel;
    private VisualElement _doublePanel;
    private FloatField _moveAnimField;
    private IntegerField _aiDepthField;
    private DropdownField _aiEngineDropdown;
    private Toggle _opponentAiToggle;
    private Slider _masterVolSlider;
    private Slider _musicVolSlider;
    private Slider _sfxVolSlider;
    private Slider _gameSpeedSlider;
    private DropdownField _debugPositionDropdown;
    private TextField _debugPositionTextField;

    private Button _rollButton;
    private Button _undoButton;
    private Button _playMoveButton;
    private Button _legalMovesButton;
    private Button _debugPositionApplyButton;
    private Button _debugPositionUseCurrentButton;
    private Button _gameTabButton;
    private Button _audioTabButton;
    private Button _debugTabButton;
    private VisualElement _gameTabContent;
    private VisualElement _audioTabContent;
    private VisualElement _debugTabContent;

    [Header("Game Mode Presets")]
    [SerializeField] private GameModePresetLibrarySo gameModePresets;

    private GameModePresetSo _activePreset =>
        gameModePresets?.presets?.Count > 0 ? gameModePresets.presets[0] : null;

    [Header("Sub-Controllers")]
    [SerializeField] private NewGameModalController newGameModalController;
    [SerializeField] private GameOverModalController gameOverModalController;
    [SerializeField] private OptionsModalController optionsModalController;
    [SerializeField] private ScreenWipeController screenWipeController;

    // Lobby overlay
    private VisualElement _lobbyLayer;
    private VisualElement _lobbyCard;
    private Button _lobbyPlayButton;
    private Button _lobbyOptionsButton;
    private Button _lobbyQuitButton;

    [Header("UI Feedback")]
    [SerializeField] private List<UiFeedbackSlot> uiFeedbackSlots = new List<UiFeedbackSlot>();

    [Header("Board view")]
    [Tooltip("Log when Horiz/Vert toggles engine→board mapping (identity vs 23−e).")]
    [SerializeField] private bool enableBoardViewDebugLogs;
    [SerializeField] private bool enableUndoPerformanceLogs;

    private static readonly ProfilerMarker HudRefreshMarker = new("Backgammon.HUD.RefreshAll");
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
        _pipCountPlayerValue = root.Q<Label>("PipCountPlayerValue");
        _pipCountAiValue = root.Q<Label>("PipCountAiValue");

        _settingsPanel = root.Q<VisualElement>("SettingsPanel");
        _takeDropPanel = root.Q<VisualElement>("TakeDropPanel");
        _doublePanel = root.Q<VisualElement>("DoublePanel");

        _moveAnimField = root.Q<FloatField>("MoveAnimField");
        _aiDepthField = root.Q<IntegerField>("AiDepthField");
        _aiEngineDropdown = root.Q<DropdownField>("AiEngineDropdown");
        _opponentAiToggle = root.Q<Toggle>("OpponentAiToggle");
        _masterVolSlider = root.Q<Slider>("MasterVolSlider");
        _musicVolSlider = root.Q<Slider>("MusicVolSlider");
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

        // ── Mobile layout triggers ────────────────────────────────────────────
        // The mobile HUD uses differently named buttons; bind them to the same
        // modal handlers as desktop. All null-safe, so desktop is unaffected.
        var mobileSettingsBtn = root.Q<Button>("SettingsButton");
        if (mobileSettingsBtn != null) mobileSettingsBtn.clicked += ToggleSettings;

        var mobileHintBtn = root.Q<Button>("HintButton");
        if (mobileHintBtn != null) mobileHintBtn.clicked += OnHintsClicked;

        var mobileRunInfoBtn = root.Q<Button>("RunInfoButtoon");
        if (mobileRunInfoBtn != null) mobileRunInfoBtn.clicked += OnNewGameClicked;

        var mobileMessageBtn = root.Q<Button>("MessageButton");
        if (mobileMessageBtn != null) mobileMessageBtn.clicked += OnMessageClicked;

        var mobileLeftProfileBtn = root.Q<Button>("LeftProfileButton");
        if (mobileLeftProfileBtn != null) mobileLeftProfileBtn.clicked += OnOpponentProfileClicked;

        var mobileRightProfileBtn = root.Q<Button>("RightProfileButton");
        if (mobileRightProfileBtn != null) mobileRightProfileBtn.clicked += OnPlayerProfileClicked;

        foreach (var p in _modeProviders)
            p.BindToHud(root, this);

        if (gameController != null)
            _activeProvider = ProviderFor(gameController.CurrentGameMode);

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

        _playMoveButton = root.Q<Button>("PlayMoveButton");
        if (_playMoveButton != null) _playMoveButton.clicked += OnPlayMoveClicked;

        _undoButton = root.Q<Button>("UndoButton");
        if (_undoButton != null) _undoButton.clicked += OnUndoClicked;

        var viewHoriz = root.Q<Button>("ViewHorizButton");
        if (viewHoriz != null) viewHoriz.clicked += OnBoardViewToggleClicked;

        var viewVert = root.Q<Button>("ViewVertButton");
        if (viewVert != null) viewVert.clicked += OnToggleCameraAngleClicked;

        var doubleBtn = root.Q<Button>("DoubleButton");
        if (doubleBtn != null) doubleBtn.clicked += OnDoubleClicked;

        var takeBtn = root.Q<Button>("TakeDoubleButton");
        if (takeBtn != null) takeBtn.clicked += OnTakeDoubleClicked;

        var dropBtn = root.Q<Button>("DropDoubleButton");
        if (dropBtn != null) dropBtn.clicked += OnDropDoubleClicked;

        var beaverBtn = root.Q<Button>("BeaverDoubleButton");
        if (beaverBtn != null) beaverBtn.clicked += OnBeaverDoubleClicked;

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
        if (_musicVolSlider != null)
            _musicVolSlider.RegisterValueChangedCallback(evt =>
            {
                BackgammonSettings.MusicVolumeLinear = evt.newValue;
                BackgammonSettings.RaiseMusicVolumeChanged(evt.newValue);
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

        InitStatsTab(root);
        InitTrophiesTab(root);
        InitCollectionTab(root);
        InitPrestigeOverlay(root);
        InitDebugPrestigeControls(root);
        SetDoubleOfferVisible(false);

        // Initialize sub-controllers
        if (newGameModalController != null)
            newGameModalController.Initialize(root);
        if (gameOverModalController != null)
            gameOverModalController.Initialize(root);
        if (optionsModalController != null)
            optionsModalController.Initialize(root);
        InitPopups(root);
        if (screenWipeController == null)
            screenWipeController = GetComponent<ScreenWipeController>();

        // Lobby overlay
        _lobbyLayer        = root.Q<VisualElement>("LobbyLayer");
        _lobbyCard         = root.Q<VisualElement>("LobbyCard");
        _lobbyPlayButton   = root.Q<Button>("LobbyPlayButton");
        _lobbyOptionsButton = root.Q<Button>("LobbyOptionsButton");
        _lobbyQuitButton   = root.Q<Button>("LobbyQuitButton");

        if (_lobbyPlayButton    != null) _lobbyPlayButton.clicked    += OnLobbyPlayClicked;
        if (_lobbyOptionsButton != null) _lobbyOptionsButton.clicked += OnLobbyOptionsClicked;
        if (_lobbyQuitButton    != null) _lobbyQuitButton.clicked    += OnLobbyQuitClicked;

        RefreshVersionDebugLabel();
    }

    /// <summary>
    /// Updates the bottom-right corner debug string with the app version and the active
    /// layout (Desktop/Mobile). Safe to call any time; no-ops if the label is absent.
    /// </summary>
    public void RefreshVersionDebugLabel()
    {
        if (uiDocument == null) return;
        var root = uiDocument.rootVisualElement;
        if (root == null) return;

        var label = root.Q<Label>("VersionDebugLabel");
        if (label == null) return;

        label.text = $"v{Application.version} \u2022 {BackgammonSettings.LayoutType}";
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

        // Mobile layout triggers
        var mobileSettingsBtn = root.Q<Button>("SettingsButton");
        if (mobileSettingsBtn != null) mobileSettingsBtn.clicked -= ToggleSettings;

        var mobileHintBtn = root.Q<Button>("HintButton");
        if (mobileHintBtn != null) mobileHintBtn.clicked -= OnHintsClicked;

        var mobileRunInfoBtn = root.Q<Button>("RunInfoButtoon");
        if (mobileRunInfoBtn != null) mobileRunInfoBtn.clicked -= OnNewGameClicked;

        var mobileMessageBtn = root.Q<Button>("MessageButton");
        if (mobileMessageBtn != null) mobileMessageBtn.clicked -= OnMessageClicked;

        var mobileLeftProfileBtn = root.Q<Button>("LeftProfileButton");
        if (mobileLeftProfileBtn != null) mobileLeftProfileBtn.clicked -= OnOpponentProfileClicked;

        var mobileRightProfileBtn = root.Q<Button>("RightProfileButton");
        if (mobileRightProfileBtn != null) mobileRightProfileBtn.clicked -= OnPlayerProfileClicked;

        if (_debugPositionApplyButton != null) _debugPositionApplyButton.clicked -= OnDebugStartPositionApplyClicked;
        if (_debugPositionUseCurrentButton != null) _debugPositionUseCurrentButton.clicked -= OnDebugStartPositionUseCurrentClicked;

        if (_playMoveButton != null) _playMoveButton.clicked -= OnPlayMoveClicked;
        if (_undoButton != null) _undoButton.clicked -= OnUndoClicked;

        var doubleBtn = root.Q<Button>("DoubleButton");
        if (doubleBtn != null) doubleBtn.clicked -= OnDoubleClicked;

        var takeBtn = root.Q<Button>("TakeDoubleButton");
        if (takeBtn != null) takeBtn.clicked -= OnTakeDoubleClicked;

        var dropBtn = root.Q<Button>("DropDoubleButton");
        if (dropBtn != null) dropBtn.clicked -= OnDropDoubleClicked;

        var beaverBtn = root.Q<Button>("BeaverDoubleButton");
        if (beaverBtn != null) beaverBtn.clicked -= OnBeaverDoubleClicked;

        var viewHoriz = root.Q<Button>("ViewHorizButton");
        if (viewHoriz != null) viewHoriz.clicked -= OnBoardViewToggleClicked;

        var viewVert = root.Q<Button>("ViewVertButton");
        if (viewVert != null) viewVert.clicked -= OnToggleCameraAngleClicked;
        if (_debugPositionDropdown != null)
            _debugPositionDropdown.UnregisterValueChangedCallback(OnDebugPositionDropdownChanged);

        if (_lobbyPlayButton    != null) _lobbyPlayButton.clicked    -= OnLobbyPlayClicked;
        if (_lobbyOptionsButton != null) _lobbyOptionsButton.clicked -= OnLobbyOptionsClicked;
        if (_lobbyQuitButton    != null) _lobbyQuitButton.clicked    -= OnLobbyQuitClicked;

        foreach (var p in _modeProviders)
            p.UnbindFromHud();
    }

    private IHudModeProvider ProviderFor(GameModeType mode)
        => _modeProviders.Find(p => p.SupportedMode == mode);

    /// <summary>Called by NewGameModalController to start a game with the selected configuration.</summary>
    public void StartGameWithConfig(GameModeType modeType, MoneySessionConfig config, string seedString, string startingPositionId, GameModePresetSo preset = null)
    {
        void DoStart()
        {
            _activeProvider = ProviderFor(modeType);
            if (_activeProvider is MoneySessionModeManager ms)
                ms.Configure(config);
            _activeProvider?.StartGame(seedString, startingPositionId);
            HideGameOverPopup();
        }

        if (screenWipeController != null && preset != null && BackgammonSettings.TransitionShape != 3)
            screenWipeController.PlayWipe(preset.wipeColor, preset.wipeIcon, DoStart);
        else
            DoStart();
    }

    public void HideLobby(Action onComplete = null)
    {
        if (_lobbyCard == null || _lobbyLayer.style.display == DisplayStyle.None) return;

        _lobbyLayer.style.display = DisplayStyle.None;
        onComplete?.Invoke();
    }

    public void ShowLobby()
    {
        if (_lobbyLayer == null) return;
        _lobbyLayer.style.display = DisplayStyle.Flex;
    }

    public void ExitToLobby()
    {
        if (gameController != null && !gameController.IsGameOver(out _))
            SavedGameService.Save(gameController.BuildSaveData());

        void DoTransition(Action midpoint)
        {
            if (screenWipeController != null && BackgammonSettings.TransitionShape != 3)
                screenWipeController.PlayWipe(midpoint);
            else
                midpoint?.Invoke();
        }

        DoTransition(() =>
        {
            gameOverModalController?.HideGameOver();
            optionsModalController?.HideModal();
            ShowLobby();
        });
    }

    private void OnLobbyPlayClicked()
    {
        void DoTransition(Action midpoint)
        {
            if (screenWipeController != null && BackgammonSettings.TransitionShape != 3)
                screenWipeController.PlayWipe(midpoint);
            else
                midpoint?.Invoke();
        }

        if (SavedGameService.HasSavedGame)
        {
            DoTransition(() =>
            {
                var data = SavedGameService.Load();
                SavedGameService.Delete();
                HideLobby();
                gameController?.RestoreFromSave(data);
            });
        }
        else
        {
            DoTransition(() =>
            {
                HideLobby();
                newGameModalController?.ShowModal();
            });
        }
    }

    private void OnLobbyOptionsClicked() => ToggleSettings();

    private void OnLobbyQuitClicked() => Application.Quit();

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

    private void OnToggleCameraAngleClicked()
    {
        BackgammonSettings.CameraAngle = BackgammonSettings.CameraAngle == 0 ? 1 : 0;
        BackgammonSettings.RaiseGraphicsSettingsChanged();
    }

    private void LoadSettingsIntoFields()
    {
        if (_moveAnimField != null) _moveAnimField.SetValueWithoutNotify(BackgammonSettings.MoveAnimDurationSeconds);
        if (_aiDepthField != null) _aiDepthField.SetValueWithoutNotify(BackgammonSettings.AiSearchDepth);
        if (_aiEngineDropdown != null) _aiEngineDropdown.SetValueWithoutNotify(BackgammonSettings.AiEngineType);
        if (_opponentAiToggle != null) _opponentAiToggle.SetValueWithoutNotify(BackgammonSettings.OpponentIsAi);
        if (_masterVolSlider != null) _masterVolSlider.SetValueWithoutNotify(BackgammonSettings.MasterVolumeLinear);
        if (_musicVolSlider != null) _musicVolSlider.SetValueWithoutNotify(BackgammonSettings.MusicVolumeLinear);
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

    private void InitDebugPrestigeControls(VisualElement root)
    {
        var unlockAllButton = root.Q<Button>("UnlockAllCurrenciesButton");
        if (unlockAllButton != null)
            unlockAllButton.clicked += () =>
            {
                PrestigeService.UnlockAll();
                RefreshAll(gameController);
                RefreshTrophiesTab();
                Debug.Log("[Debug] UnlockAll currencies triggered.");
            };
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
        if (optionsModalController != null)
            optionsModalController.ToggleSettings();
    }

    private void OnHintsClicked()
    {
        if (optionsModalController != null)
            optionsModalController.ShowHints();
    }

    public void ShowHints()
    {
        if (optionsModalController != null)
            optionsModalController.ShowHints();
    }

    private void OnLegalMovesClicked()
    {
        if (optionsModalController != null)
            optionsModalController.ToggleLegalMoves();
    }

    public void SetHintsText(string hintsText)
    {
        if (optionsModalController != null)
            optionsModalController.SetHintsText(hintsText);
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
        if (_statsTabContent != null)
            _statsTabContent.style.display = DisplayStyle.None;
        if (_trophiesTabContent != null)
            _trophiesTabContent.style.display = DisplayStyle.None;
        if (_collectionTabContent != null)
            _collectionTabContent.style.display = DisplayStyle.None;

        // Remove active class from all buttons
        _gameTabButton?.RemoveFromClassList("modal-tab-button-active");
        _audioTabButton?.RemoveFromClassList("modal-tab-button-active");
        _debugTabButton?.RemoveFromClassList("modal-tab-button-active");
        _statsTabButton?.RemoveFromClassList("modal-tab-button-active");
        _trophiesTabButton?.RemoveFromClassList("modal-tab-button-active");
        _collectionTabButton?.RemoveFromClassList("modal-tab-button-active");

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
            case "Stats":
                if (_statsTabContent != null)
                    _statsTabContent.style.display = DisplayStyle.Flex;
                _statsTabButton?.AddToClassList("modal-tab-button-active");
                RefreshStatsTab();
                break;
            case "Trophies":
                if (_trophiesTabContent != null)
                    _trophiesTabContent.style.display = DisplayStyle.Flex;
                _trophiesTabButton?.AddToClassList("modal-tab-button-active");
                RefreshTrophiesTab();
                break;
            case "Collection":
                if (_collectionTabContent != null)
                    _collectionTabContent.style.display = DisplayStyle.Flex;
                _collectionTabButton?.AddToClassList("modal-tab-button-active");
                RefreshCollectionTab();
                break;
        }

        Debug.Log($"[Backgammon][UI][Modal] Switched to {tabName} tab.");
    }

    // New Game Modal methods removed - now handled by NewGameModalController
    // Options Modal methods removed - now handled by OptionsModalController

    public void SetDoubleOfferVisible(bool visible)
    {
        if (_takeDropPanel == null) return;
        _takeDropPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        var beaverBtn = _takeDropPanel.Q<Button>("BeaverDoubleButton");
        if (beaverBtn != null)
        {
            bool canBeaver = visible && (gameController?.CanCurrentPlayerBeaver() ?? false);
            beaverBtn.style.display = canBeaver ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    private void OnRollClicked()
    {
        if (gameController != null)
            gameController.RequestRollDice();
    }

    private void OnNewGameClicked()
    {
        if (newGameModalController != null)
            newGameModalController.ShowModal();
    }

    private void OnMessageClicked()
    {
        ShowMessagePopup();
    }

    private void OnPlayerProfileClicked()
    {
        ShowPlayerProfilePopup();
    }

    private void OnOpponentProfileClicked()
    {
        ShowOpponentProfilePopup();
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

    private void OnBeaverDoubleClicked()
    {
        gameController?.OfferBeaver();
    }

    public void ShowGameOverPopup(string summaryText)
    {
        if (gameOverModalController != null)
            gameOverModalController.ShowGameOver(summaryText);
    }

    public void HideGameOverPopup()
    {
        if (gameOverModalController != null)
            gameOverModalController.HideGameOver();
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

        // Delegate all mode-specific labels to the active provider
        if (_activeProvider != null)
        {
            if (_matchScoreValue != null) _matchScoreValue.text = _activeProvider.ScoreDisplay;
            if (_gamesValue != null)
            {
                string gd = _activeProvider.GamesDisplay;
                _gamesValue.style.display = gd != null ? DisplayStyle.Flex : DisplayStyle.None;
                if (gd != null) _gamesValue.text = gd;
            }
            if (_stakeValue   != null) _stakeValue.text   = _activeProvider.StakeDisplay;
            if (_headingLabel != null) _headingLabel.text = _activeProvider.HeadingDisplay;
            _activeProvider.RefreshModeHud(uiDocument.rootVisualElement, ctrl);
        }

        if (_chipsValue != null)
            _chipsValue.text = ctrl.CurrentMatchBaseStake.ToString();

        if (_multiplierValue != null)
            _multiplierValue.text = ctrl.State.CubeValue.ToString();

        if (_rollsValue != null)
            _rollsValue.text = ctrl.PlayerRollsThisGame.ToString();

        if (_pipCountPlayerValue != null)
            _pipCountPlayerValue.text = ctrl.State != null ? ctrl.CalculatePipCountPlayer1().ToString() : "—";
        if (_pipCountAiValue != null)
            _pipCountAiValue.text = ctrl.State != null ? ctrl.CalculatePipCountPlayer2().ToString() : "—";

        if (pipCountPlayerWorldLabel != null)
            pipCountPlayerWorldLabel.text = ctrl.State != null ? ctrl.CalculatePipCountPlayer1().ToString() : "—";
        if (pipCountAiWorldLabel != null)
            pipCountAiWorldLabel.text = ctrl.State != null ? ctrl.CalculatePipCountPlayer2().ToString() : "—";

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
            if (optionsModalController != null)
                optionsModalController.ClearLegalList();
        }
        else
        {
            if (optionsModalController != null)
                optionsModalController.RebuildLegalListIfChanged(ctrl.CurrentLegalTurns);
        }

        RefreshStatsTab();
    }

    // Legal moves rebuild methods removed - now handled by OptionsModalController

    // ========== UI Feedback System ==========

    internal void PlayPopupOpenSound() => PlayUiFeedback(UiFeedbackEventType.PopupOpen);
    internal void PlayPopupCloseSound() => PlayUiFeedback(UiFeedbackEventType.PopupClose);

    /// <summary>Public method for sub-controllers to trigger UI feedback.</summary>
    public void TriggerUiFeedback(UiFeedbackEventType eventType) => PlayUiFeedback(eventType);

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
