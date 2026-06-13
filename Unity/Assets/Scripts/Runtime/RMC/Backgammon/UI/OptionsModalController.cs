using System.Collections.Generic;
using System.Linq;
using EngineCore;
using Runtime.RMC.Backgammon.Core;
using Runtime.RMC.Backgammon.Settings;
using Runtime.RMC.Backgammon.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Runtime.RMC.Backgammon.UI
{
    /// <summary>
    /// Manages the Options/Hints/Legal Moves modal with tabbed interface.
    /// </summary>
    public class OptionsModalController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BackgammonHudController hudController;
        [SerializeField] private BackgammonGameController gameController;

        // UI Elements
        private VisualElement _modalLayer;
        private VisualElement _modalBackdrop;
        private VisualElement _modalCard;
        private Button _modalCloseButton;
        private Label _modalTitleLabel;

        // Tab buttons
        private Button _gameTabButton;
        private Button _audioTabButton;
        private Button _debugTabButton;
        private Button _statsTabButton;
        private Button _trophiesTabButton;
        private Button _collectionTabButton;
        private Button _graphicsTabButton;
        private Button _creditsTabButton;

        // Tab content
        private VisualElement _settingsContent;
        private VisualElement _hintsContent;
        private VisualElement _legalMovesContent;
        private VisualElement _gameTabContent;
        private VisualElement _audioTabContent;
        private VisualElement _debugTabContent;
        private VisualElement _statsTabContent;
        private VisualElement _trophiesTabContent;
        private VisualElement _collectionTabContent;
        private VisualElement _graphicsTabContent;
        private VisualElement _creditsTabContent;

        // Graphics sub-tab elements
        private VisualElement _graphicsSubTabNavigation;
        private VisualElement _graphicsSubTabContentCamera;
        private VisualElement _graphicsSubTabContentPostFx;
        private VisualElement _graphicsSubTabContentAnimations;
        private VisualElement _graphicsSubTabContentTheme;

        // Graphics – Display sub-tab controls
        private DropdownField _cameraAngleDropdown;
        private Toggle _scanLinesToggle;
        private Toggle _crtBloomToggle;
        private Slider _brightnessSlider;
        private Slider _contrastSlider;
        private DropdownField _popupAnimationDropdown;
        private DropdownField _transitionShapeDropdown;

        // Graphics – Theme sub-tab controls
        private DropdownField _themePresetDropdown;
        private Button _themeLoadPresetButton;
        private Button _themeSaveCustomButton;
        private Button _themeApplyCustomButton;
        private VisualElement _themeEditorContainer;

        private Button _exitToLobbyButton;

        [Header("Theme")]
        [SerializeField] private ThemeManager _themeManager;
        [SerializeField] private BackgammonThemeLibrarySo _themeLibrary;

        // Hints
        private Label _hintsLabel;

        // Legal Moves
        private ScrollView _legalScroll;
        private Label _legalMovesEmptyLabel;
        private int _selectedLegalIndex;
        private string _lastLegalSignature;

        private enum ModalMode
        {
            Settings,
            Hints,
            LegalMoves
        }

        private ModalMode _activeModalMode;

        /// <summary>Binds all UI elements from the root VisualElement.</summary>
        public void Initialize(VisualElement root)
        {
            // Modal container
            _modalLayer = root.Q<VisualElement>("ModalLayer");
            _modalBackdrop = root.Q<VisualElement>("ModalBackdrop");
            _modalCard = root.Q<VisualElement>("ModalCard");
            _modalCloseButton = root.Q<Button>("ModalCloseButton");
            _modalTitleLabel = root.Q<Label>("ModalTitleLabel");

            // Tab buttons
            _gameTabButton = root.Q<Button>("GameTabButton");
            _audioTabButton = root.Q<Button>("AudioTabButton");
            _debugTabButton = root.Q<Button>("DebugTabButton");
            _statsTabButton = root.Q<Button>("StatsTabButton");
            _trophiesTabButton = root.Q<Button>("TrophiesTabButton");
            _collectionTabButton = root.Q<Button>("CollectionTabButton");
            _graphicsTabButton = root.Q<Button>("GraphicsTabButton");
            _creditsTabButton = root.Q<Button>("CreditsTabButton");

            // Tab content
            _settingsContent = root.Q<VisualElement>("SettingsContent");
            _hintsContent = root.Q<VisualElement>("HintsContent");
            _legalMovesContent = root.Q<VisualElement>("LegalMovesContent");
            _gameTabContent = root.Q<VisualElement>("GameTabContent");
            _audioTabContent = root.Q<VisualElement>("AudioTabContent");
            _debugTabContent = root.Q<VisualElement>("DebugTabContent");
            _statsTabContent = root.Q<VisualElement>("StatsTabContent");
            _trophiesTabContent = root.Q<VisualElement>("TrophiesTabContent");
            _collectionTabContent = root.Q<VisualElement>("CollectionTabContent");
            _graphicsTabContent = root.Q<VisualElement>("GraphicsTabContent");
            _creditsTabContent = root.Q<VisualElement>("CreditsTabContent");

            // Graphics sub-tabs
            _graphicsSubTabNavigation        = root.Q<VisualElement>("GraphicsSubTabNavigation");
            _graphicsSubTabContentCamera     = root.Q<VisualElement>("GraphicsSubTabContentCamera");
            _graphicsSubTabContentPostFx     = root.Q<VisualElement>("GraphicsSubTabContentPostFx");
            _graphicsSubTabContentAnimations = root.Q<VisualElement>("GraphicsSubTabContentAnimations");
            _graphicsSubTabContentTheme      = root.Q<VisualElement>("GraphicsSubTabContentTheme");

            // Graphics – Display controls
            _cameraAngleDropdown     = root.Q<DropdownField>("CameraAngleDropdown");
            _scanLinesToggle         = root.Q<Toggle>("ScanLinesToggle");
            _crtBloomToggle          = root.Q<Toggle>("CrtBloomToggle");
            _brightnessSlider        = root.Q<Slider>("BrightnessSlider");
            _contrastSlider          = root.Q<Slider>("ContrastSlider");
            _popupAnimationDropdown  = root.Q<DropdownField>("PopupAnimationDropdown");
            _transitionShapeDropdown = root.Q<DropdownField>("TransitionShapeDropdown");

            // Graphics – Theme controls
            _themePresetDropdown   = root.Q<DropdownField>("ThemePresetDropdown");
            _themeLoadPresetButton = root.Q<Button>("ThemeLoadPresetButton");
            _themeSaveCustomButton = root.Q<Button>("ThemeSaveCustomButton");
            _themeApplyCustomButton = root.Q<Button>("ThemeApplyCustomButton");
            _themeEditorContainer  = root.Q<VisualElement>("ThemeEditorContainer");

            _exitToLobbyButton = root.Q<Button>("OptionsExitToLobbyButton");

            // Hints
            _hintsLabel = root.Q<Label>("HintsLabel");

            // Legal Moves
            _legalScroll = root.Q<ScrollView>("LegalScroll");
            _legalMovesEmptyLabel = root.Q<Label>("LegalMovesEmptyLabel");

            // Wire button clicks
            if (_modalCloseButton != null)
                _modalCloseButton.clicked += HideModal;
            if (_exitToLobbyButton != null)
                _exitToLobbyButton.clicked += OnExitToLobbyClicked;
            if (_modalBackdrop != null)
                _modalBackdrop.RegisterCallback<ClickEvent>(OnModalBackdropClicked);

            if (_gameTabButton != null)
                _gameTabButton.clicked += () => SwitchTab("Game");
            if (_audioTabButton != null)
                _audioTabButton.clicked += () => SwitchTab("Audio");
            if (_debugTabButton != null)
                _debugTabButton.clicked += () => SwitchTab("Debug");
            if (_statsTabButton != null)
                _statsTabButton.clicked += () => SwitchTab("Stats");
            if (_trophiesTabButton != null)
                _trophiesTabButton.clicked += () => SwitchTab("Trophies");
            if (_collectionTabButton != null)
                _collectionTabButton.clicked += () => SwitchTab("Collection");
            if (_graphicsTabButton != null)
                _graphicsTabButton.clicked += () => SwitchTab("Graphics");
            if (_creditsTabButton != null)
                _creditsTabButton.clicked += () => SwitchTab("Credits");

            // Wire Graphics sub-tab buttons
            var graphicsSubContents = new[] {
                _graphicsSubTabContentCamera,
                _graphicsSubTabContentPostFx,
                _graphicsSubTabContentAnimations,
                _graphicsSubTabContentTheme
            };
            var cameraBtn = root.Q<Button>("GraphicsSubTabCameraBtn");
            if (cameraBtn != null)
                cameraBtn.clicked += () =>
                    SwitchSubTab(_graphicsSubTabNavigation, "GraphicsSubTabCameraBtn", graphicsSubContents, _graphicsSubTabContentCamera);
            var postFxBtn = root.Q<Button>("GraphicsSubTabPostFxBtn");
            if (postFxBtn != null)
                postFxBtn.clicked += () =>
                    SwitchSubTab(_graphicsSubTabNavigation, "GraphicsSubTabPostFxBtn", graphicsSubContents, _graphicsSubTabContentPostFx);
            var animationsBtn = root.Q<Button>("GraphicsSubTabAnimationsBtn");
            if (animationsBtn != null)
                animationsBtn.clicked += () =>
                    SwitchSubTab(_graphicsSubTabNavigation, "GraphicsSubTabAnimationsBtn", graphicsSubContents, _graphicsSubTabContentAnimations);
            var themeBtn = root.Q<Button>("GraphicsSubTabThemeBtn");
            if (themeBtn != null)
                themeBtn.clicked += () =>
                    SwitchSubTab(_graphicsSubTabNavigation, "GraphicsSubTabThemeBtn", graphicsSubContents, _graphicsSubTabContentTheme);

            InitGraphicsControls();

            Debug.Log("[OptionsModal] Initialized all UI bindings");
        }

        /// <summary>Shows the modal in Settings mode.</summary>
        public void ShowSettings()
        {
            PlayPopupOpenSound();
            SetModalVisible(true, ModalMode.Settings);
            SwitchTab("Game");  // Always start on Game tab
            Debug.Log("[OptionsModal] Opened modal in Settings mode");
        }

        /// <summary>Shows the modal in Hints mode.</summary>
        public void ShowHints()
        {
            PlayPopupOpenSound();
            SetModalVisible(true, ModalMode.Hints);
            Debug.Log("[OptionsModal] Opened modal in Hints mode");
        }

        /// <summary>Shows the modal in Legal Moves mode.</summary>
        public void ShowLegalMoves()
        {
            PlayPopupOpenSound();
            SetModalVisible(true, ModalMode.LegalMoves);
            Debug.Log("[OptionsModal] Opened modal in Legal Moves mode");
        }

        /// <summary>Hides the modal.</summary>
        public void HideModal()
        {
            if (_modalLayer == null) return;

            PlayPopupCloseSound();
            PopupAnimator.Hide(_modalCard, this, () => _modalLayer.style.display = DisplayStyle.None);
            Debug.Log("[OptionsModal] Closed modal");
        }

        /// <summary>Toggles Settings modal (close if already open).</summary>
        public void ToggleSettings()
        {
            if (_modalLayer == null) return;
            if (_modalLayer.style.display == DisplayStyle.Flex && _settingsContent != null && _settingsContent.style.display == DisplayStyle.Flex)
            {
                Debug.Log("[OptionsModal] ToggleSettings requested close while settings were already visible");
                HideModal();
                return;
            }

            ShowSettings();
        }

        /// <summary>Toggles Legal Moves modal (close if already open).</summary>
        public void ToggleLegalMoves()
        {
            if (_modalLayer == null) return;
            if (_modalLayer.style.display == DisplayStyle.Flex && _activeModalMode == ModalMode.LegalMoves)
            {
                Debug.Log("[OptionsModal] ToggleLegalMoves requested close while legal moves were already visible");
                HideModal();
                return;
            }

            ShowLegalMoves();
        }

        /// <summary>Sets the hints text content.</summary>
        public void SetHintsText(string hintsText)
        {
            if (_hintsLabel == null) return;
            _hintsLabel.text = string.IsNullOrWhiteSpace(hintsText) ? "No hints available yet." : hintsText;
            Debug.Log("[OptionsModal] Updated hints text");
        }

        /// <summary>Rebuilds the legal moves list if the game state changed.</summary>
        public void RebuildLegalListIfChanged(IReadOnlyList<Turn> currentLegalTurns)
        {
            string signature = ComputeLegalSignature(currentLegalTurns);
            if (signature == _lastLegalSignature)
                return;
            _lastLegalSignature = signature;
            RebuildLegalList(currentLegalTurns);
        }

        /// <summary>Clears the legal moves list.</summary>
        public void ClearLegalList()
        {
            if (_legalScroll != null)
                _legalScroll.Clear();
            _lastLegalSignature = null;
            Debug.Log("[OptionsModal] Cleared legal list");
        }

        // ── Private Helpers ───────────────────────────────────────────────────

        private void SetModalVisible(bool isVisible, ModalMode modalMode)
        {
            if (_modalLayer == null) return;

            if (isVisible)
            {
                _modalLayer.style.display = DisplayStyle.Flex;
                PopupAnimator.Show(_modalCard, this);
            }
            else
            {
                PopupAnimator.Hide(_modalCard, this, () => _modalLayer.style.display = DisplayStyle.None);
            }
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

        private void OnExitToLobbyClicked()
        {
            HideModal();
            hudController?.ExitToLobby();
        }

        private void OnModalBackdropClicked(ClickEvent evt)
        {
            if (_modalBackdrop == null) return;
            if (!ReferenceEquals(evt.target, _modalBackdrop))
                return;

            Debug.Log("[OptionsModal] Backdrop click detected. Closing modal");
            HideModal();
            evt.StopPropagation();
        }

        private void SwitchTab(string tabName)
        {
            Debug.Log($"[OptionsModal] SwitchTab called with: {tabName}");

            // Hide all tabs
            if (_gameTabContent != null)
                _gameTabContent.style.display = DisplayStyle.None;
            if (_audioTabContent != null)
                _audioTabContent.style.display = DisplayStyle.None;
            if (_debugTabContent != null)
                _debugTabContent.style.display = DisplayStyle.None;
            if (_statsTabContent != null)
                _statsTabContent.style.display = DisplayStyle.None;
            if (_trophiesTabContent != null)
                _trophiesTabContent.style.display = DisplayStyle.None;
            if (_collectionTabContent != null)
                _collectionTabContent.style.display = DisplayStyle.None;
            if (_graphicsTabContent != null)
                _graphicsTabContent.style.display = DisplayStyle.None;
            if (_creditsTabContent != null)
                _creditsTabContent.style.display = DisplayStyle.None;

            // Remove active class from all buttons
            _gameTabButton?.RemoveFromClassList("modal-tab-button-active");
            _audioTabButton?.RemoveFromClassList("modal-tab-button-active");
            _debugTabButton?.RemoveFromClassList("modal-tab-button-active");
            _statsTabButton?.RemoveFromClassList("modal-tab-button-active");
            _trophiesTabButton?.RemoveFromClassList("modal-tab-button-active");
            _collectionTabButton?.RemoveFromClassList("modal-tab-button-active");
            _graphicsTabButton?.RemoveFromClassList("modal-tab-button-active");
            _creditsTabButton?.RemoveFromClassList("modal-tab-button-active");

            // Show selected tab and activate button
            switch (tabName)
            {
                case "Game":
                    if (_gameTabContent != null)
                        _gameTabContent.style.display = DisplayStyle.Flex;
                    _gameTabButton?.AddToClassList("modal-tab-button-active");
                    break;
                case "Audio":
                    if (_audioTabContent != null)
                        _audioTabContent.style.display = DisplayStyle.Flex;
                    _audioTabButton?.AddToClassList("modal-tab-button-active");
                    break;
                case "Debug":
                    if (_debugTabContent != null)
                        _debugTabContent.style.display = DisplayStyle.Flex;
                    _debugTabButton?.AddToClassList("modal-tab-button-active");
                    break;
                case "Stats":
                    if (_statsTabContent != null)
                        _statsTabContent.style.display = DisplayStyle.Flex;
                    _statsTabButton?.AddToClassList("modal-tab-button-active");
                    if (hudController != null)
                        hudController.RefreshStatsTab();
                    break;
                case "Trophies":
                    if (_trophiesTabContent != null)
                        _trophiesTabContent.style.display = DisplayStyle.Flex;
                    _trophiesTabButton?.AddToClassList("modal-tab-button-active");
                    if (hudController != null)
                        hudController.RefreshTrophiesTab();
                    break;
                case "Collection":
                    if (_collectionTabContent != null)
                        _collectionTabContent.style.display = DisplayStyle.Flex;
                    _collectionTabButton?.AddToClassList("modal-tab-button-active");
                    if (hudController != null)
                        hudController.RefreshCollectionTab();
                    break;
                case "Graphics":
                    if (_graphicsTabContent != null)
                        _graphicsTabContent.style.display = DisplayStyle.Flex;
                    _graphicsTabButton?.AddToClassList("modal-tab-button-active");
                    break;
                case "Credits":
                    if (_creditsTabContent != null)
                        _creditsTabContent.style.display = DisplayStyle.Flex;
                    _creditsTabButton?.AddToClassList("modal-tab-button-active");
                    break;
            }

            Debug.Log($"[OptionsModal] Switched to {tabName} tab");
        }

        private static readonly string[] CameraAngleChoices = { "Top Down", "Angled" };

        internal static void SwitchSubTab(
            VisualElement navBar,
            string activeButtonName,
            VisualElement[] allContents,
            VisualElement activeContent)
        {
            navBar?.Query<Button>().ForEach(b => b.RemoveFromClassList("modal-subtab-button-active"));
            navBar?.Q<Button>(activeButtonName)?.AddToClassList("modal-subtab-button-active");
            foreach (var ve in allContents)
                if (ve != null) ve.style.display = DisplayStyle.None;
            if (activeContent != null) activeContent.style.display = DisplayStyle.Flex;
        }

        private void InitGraphicsControls()
        {
            if (_cameraAngleDropdown != null)
            {
                _cameraAngleDropdown.choices.AddRange(CameraAngleChoices);
                _cameraAngleDropdown.SetValueWithoutNotify(CameraAngleChoices[BackgammonSettings.CameraAngle]);
                _cameraAngleDropdown.RegisterValueChangedCallback(evt =>
                {
                    BackgammonSettings.CameraAngle = System.Array.IndexOf(CameraAngleChoices, evt.newValue);
                    BackgammonSettings.RaiseGraphicsSettingsChanged();
                });
            }

            if (_scanLinesToggle != null)
            {
                _scanLinesToggle.SetValueWithoutNotify(BackgammonSettings.ScanLines);
                _scanLinesToggle.RegisterValueChangedCallback(evt =>
                {
                    BackgammonSettings.ScanLines = evt.newValue;
                    BackgammonSettings.RaiseGraphicsSettingsChanged();
                });
            }

            if (_crtBloomToggle != null)
            {
                _crtBloomToggle.SetValueWithoutNotify(BackgammonSettings.CrtBloom);
                _crtBloomToggle.RegisterValueChangedCallback(evt =>
                {
                    BackgammonSettings.CrtBloom = evt.newValue;
                    BackgammonSettings.RaiseGraphicsSettingsChanged();
                });
            }

            if (_brightnessSlider != null)
            {
                _brightnessSlider.SetValueWithoutNotify(BackgammonSettings.Brightness);
                _brightnessSlider.RegisterValueChangedCallback(evt =>
                {
                    BackgammonSettings.Brightness = evt.newValue;
                    BackgammonSettings.RaiseGraphicsSettingsChanged();
                });
            }

            if (_contrastSlider != null)
            {
                _contrastSlider.SetValueWithoutNotify(BackgammonSettings.Contrast);
                _contrastSlider.RegisterValueChangedCallback(evt =>
                {
                    BackgammonSettings.Contrast = evt.newValue;
                    BackgammonSettings.RaiseGraphicsSettingsChanged();
                });
            }

            if (_popupAnimationDropdown != null)
            {
                var choices = new[] { "None", "Fade", "Scale In", "Slide Down", "Bounce" };
                _popupAnimationDropdown.choices.AddRange(choices);
                _popupAnimationDropdown.SetValueWithoutNotify(choices[BackgammonSettings.PopupAnimation]);
                _popupAnimationDropdown.RegisterValueChangedCallback(evt =>
                {
                    BackgammonSettings.PopupAnimation = System.Array.IndexOf(choices, evt.newValue);
                    BackgammonSettings.RaiseGraphicsSettingsChanged();
                });
            }

            if (_transitionShapeDropdown != null)
            {
                var choices = new[] { "Diamond", "Circle", "Square", "None" };
                _transitionShapeDropdown.choices.AddRange(choices);
                _transitionShapeDropdown.SetValueWithoutNotify(choices[BackgammonSettings.TransitionShape]);
                _transitionShapeDropdown.RegisterValueChangedCallback(evt =>
                {
                    BackgammonSettings.TransitionShape = System.Array.IndexOf(choices, evt.newValue);
                    BackgammonSettings.RaiseGraphicsSettingsChanged();
                });
            }

            InitThemeSubTabControls();

            Debug.Log("[OptionsModal] Graphics controls initialized");
        }

        private void InitThemeSubTabControls()
        {
            if (_themeLibrary == null || _themeLibrary.Count == 0) return;

            var presetNames = _themeLibrary.themes.Select(t => t.displayName).ToList();
            presetNames.Add("Custom");

            if (_themePresetDropdown != null)
            {
                _themePresetDropdown.choices.AddRange(presetNames);
                int currentIdx = Mathf.Clamp(BackgammonSettings.ThemeIndex, 0, presetNames.Count - 1);
                _themePresetDropdown.SetValueWithoutNotify(presetNames[currentIdx]);
            }

            if (_themeLoadPresetButton != null)
            {
                _themeLoadPresetButton.clicked += () =>
                {
                    if (_themePresetDropdown == null) return;
                    int idx = _themePresetDropdown.choices.IndexOf(_themePresetDropdown.value);
                    if (idx < 0) return;
                    BackgammonSettings.ThemeIndex = idx;
                    BackgammonSettings.RaiseGraphicsSettingsChanged();
                };
            }

            if (_themeSaveCustomButton != null)
            {
                _themeSaveCustomButton.clicked += () =>
                {
                    if (_themePresetDropdown == null || _themeLibrary == null) return;
                    int idx = _themePresetDropdown.choices.IndexOf(_themePresetDropdown.value);
                    BackgammonThemeSo source = (idx >= 0 && idx < _themeLibrary.Count)
                        ? _themeLibrary.GetTheme(idx)
                        : _themeLibrary.GetTheme(0);
                    if (source != null)
                    {
                        BackgammonThemeSerializer.SaveCustom(BackgammonThemeSerializer.ToData(source));
                        BackgammonSettings.ThemeIndex = 3;
                        BackgammonSettings.RaiseGraphicsSettingsChanged();
                        if (_themePresetDropdown != null)
                            _themePresetDropdown.SetValueWithoutNotify("Custom");
                    }
                };
            }

            if (_themeApplyCustomButton != null)
            {
                _themeApplyCustomButton.clicked += () =>
                {
                    BackgammonSettings.ThemeIndex = 3;
                    BackgammonSettings.RaiseGraphicsSettingsChanged();
                };
            }

            BuildThemeEditorRows();
        }

        private void BuildThemeEditorRows()
        {
            if (_themeEditorContainer == null || _themeLibrary == null || _themeLibrary.Count == 0) return;

            _themeEditorContainer.Clear();

            // Load current values from active preset or custom slot for display
            BackgammonThemeSo reference = _themeLibrary.GetTheme(
                Mathf.Clamp(BackgammonSettings.ThemeIndex, 0, _themeLibrary.Count - 1));
            if (reference == null) return;

            AddColorPreviewRow(_themeEditorContainer, "P1 Base",       reference.checker1BaseColor);
            AddColorPreviewRow(_themeEditorContainer, "P1 Emission",   reference.checker1EmissionColor);
            AddColorPreviewRow(_themeEditorContainer, "P2 Base",       reference.checker2BaseColor);
            AddColorPreviewRow(_themeEditorContainer, "P2 Emission",   reference.checker2EmissionColor);
            AddColorPreviewRow(_themeEditorContainer, "Highlight",     reference.movableHighlightColor);
            AddColorPreviewRow(_themeEditorContainer, "Point Dark",    reference.boardPointDarkColor);
            AddColorPreviewRow(_themeEditorContainer, "Point Light",   reference.boardPointLightColor);
            AddColorPreviewRow(_themeEditorContainer, "Cube",          reference.doublingCubeColor);
            AddColorPreviewRow(_themeEditorContainer, "Dice Body",     reference.diceBodyColor);
            AddColorPreviewRow(_themeEditorContainer, "Dice Pip",      reference.dicePipColor);
            AddColorPreviewRow(_themeEditorContainer, "Board Surface", reference.boardSurfaceColor);
        }

        private static void AddColorPreviewRow(VisualElement parent, string label, Color color)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 4;
            row.style.alignItems = Align.Center;

            var lbl = new Label(label);
            lbl.style.width = 110;
            lbl.style.fontSize = 16;
            row.Add(lbl);

            var swatch = new VisualElement();
            swatch.style.width = 28;
            swatch.style.height = 18;
            swatch.style.borderTopLeftRadius = 3;
            swatch.style.borderTopRightRadius = 3;
            swatch.style.borderBottomLeftRadius = 3;
            swatch.style.borderBottomRightRadius = 3;
            swatch.style.backgroundColor = color;
            swatch.style.marginLeft = 6;
            row.Add(swatch);

            var hex = new Label($"#{ColorUtility.ToHtmlStringRGB(color)}");
            hex.style.fontSize = 14;
            hex.style.marginLeft = 6;
            row.Add(hex);

            parent.Add(row);
        }

        private void RebuildLegalList(IReadOnlyList<Turn> legals)
        {
            if (_legalScroll == null) return;
            _legalScroll.Clear();

            bool hasMoves = legals.Count > 0;
            if (_legalMovesEmptyLabel != null)
                _legalMovesEmptyLabel.style.display = hasMoves ? DisplayStyle.None : DisplayStyle.Flex;

            if (legals.Count == 0)
            {
                _selectedLegalIndex = 0;
                Debug.LogWarning("[OptionsModal] Rebuild produced no legal moves");
                return;
            }

            if (_selectedLegalIndex >= legals.Count)
                _selectedLegalIndex = 0;

            System.Collections.Generic.List<(Turn turn, float equity)> ranked = null;
            bool hasRanking = false;

            if (gameController != null)
            {
                hasRanking = BackgammonAIService.TryEvaluateAllTurns(
                    gameController.State, gameController.Match, legals, out ranked);
            }

            int count = hasRanking && ranked != null ? ranked.Count : legals.Count;
            for (int i = 0; i < count; i++)
            {
                int idx = i;
                Turn turn = (hasRanking && ranked != null) ? ranked[i].turn : legals[i];
                float equity = (hasRanking && ranked != null) ? ranked[i].equity : float.NaN;

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
                    gameController?.PreviewTurnHighlights(turn);
                }) { text = label };
                btn.style.flexGrow = 1;
                if (i == 0 && hasRanking)
                    btn.style.color = new StyleColor(new Color(0.7f, 1f, 0.7f));
                row.Add(btn);
                _legalScroll.Add(row);
            }

            Debug.Log($"[OptionsModal] Rebuilt {legals.Count} moves (ranked={hasRanking})");
        }

        public static string ComputeLegalSignature(IReadOnlyList<Turn> legals)
        {
            if (legals == null || legals.Count == 0)
                return string.Empty;

            var sb = new System.Text.StringBuilder();
            foreach (var turn in legals)
                sb.Append(turn).Append(';');
            return sb.ToString();
        }

        // ── UI Feedback ───────────────────────────────────────────────────────

        private void PlayPopupOpenSound()
        {
            if (hudController != null)
                hudController.TriggerUiFeedback(UiFeedbackEventType.PopupOpen);
        }

        private void PlayPopupCloseSound()
        {
            if (hudController != null)
                hudController.TriggerUiFeedback(UiFeedbackEventType.PopupClose);
        }
    }
}
