using EngineCore;
using Runtime.RMC._MyProject_.Core;
using Runtime.RMC.Backgammon.Core;
using Runtime.RMC.Backgammon.Stats;
using UnityEngine;
using UnityEngine.UIElements;

namespace Runtime.RMC.Backgammon.UI
{
    /// <summary>
    /// Manages the New Game modal UI: level/stake selection, validation, and game start flow.
    /// Observes prestige and balance events to update locked/unlocked states.
    /// </summary>
    public class NewGameModalController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BackgammonHudController hudController;
        [SerializeField] private BackgammonGameController gameController;
        [SerializeField] private GameModePresetLibrarySo gameModePresets;

        // Selection state
        private int _selectedPresetIndex = 0;
        private int _selectedStakeIndex = 0;

        [SerializeField] private StakeProgressionService stakeProgressionService;

        // UI Elements
        private VisualElement _newGameModalLayer;
        private VisualElement _newGameModalBackdrop;
        private VisualElement _newGameModalCard;
        private Button _newGameModalCloseButton;
        private Button _gameTypePrevButton;
        private Button _gameTypeNextButton;
        private Button _startNewGameButton;

        // World/Stake picker
        private VisualElement _worldChipImage;
        private Label _worldNameLabel;
        private Button _stakePrevButton;
        private Button _stakeNextButton;
        private Label _stakeAmountLabel;
        private Label _stakeLockedLabel;

        // Money Session Settings
        private VisualElement _moneySessionSettings;
        private Toggle _autoDoublesToggle;
        private Toggle _beaversToggle;
        private Toggle _raccoonsToggle;
        private Toggle _ardvarksToggle;
        private Toggle _jacobyToggle;

        // Seed
        private TextField _seedTextField;
        private Button _randomizeSeedButton;

        private void OnEnable()
        {
            PrestigeService.OnPrestigeChanged += RefreshWorldStakePicker;
            MoneyBalanceService.OnBalanceChanged += OnBalanceChanged;
            if (stakeProgressionService != null)
                stakeProgressionService.OnStakeTierUnlocked += OnStakeUnlocked;
        }

        private void OnDisable()
        {
            PrestigeService.OnPrestigeChanged -= RefreshWorldStakePicker;
            MoneyBalanceService.OnBalanceChanged -= OnBalanceChanged;
            if (stakeProgressionService != null)
                stakeProgressionService.OnStakeTierUnlocked -= OnStakeUnlocked;
        }

        private void OnBalanceChanged(int newBalance) => RefreshWorldStakePicker();

        private void OnStakeUnlocked(StakeLevelSo stake) => RefreshWorldStakePicker();

        /// <summary>Binds all UI elements from the root VisualElement.</summary>
        public void Initialize(VisualElement root)
        {
            // Modal container
            _newGameModalLayer = root.Q<VisualElement>("NewGameModalLayer");
            _newGameModalBackdrop = root.Q<VisualElement>("NewGameModalBackdrop");
            _newGameModalCard = root.Q<VisualElement>("NewGameModalCard");
            _newGameModalCloseButton = root.Q<Button>("NewGameModalCloseButton");

            // Level (preset) selector arrows
            _gameTypePrevButton = root.Q<Button>("GameTypePrevButton");
            _gameTypeNextButton = root.Q<Button>("GameTypeNextButton");

            // World chip and name
            _worldChipImage = root.Q<VisualElement>("WorldChipImage");
            _worldNameLabel = root.Q<Label>("WorldNameLabel");

            // Stake picker
            _stakePrevButton = root.Q<Button>("StakePrevButton");
            _stakeNextButton = root.Q<Button>("StakeNextButton");
            _stakeAmountLabel = root.Q<Label>("StakeAmountLabel");
            _stakeLockedLabel = root.Q<Label>("StakeLockedLabel");

            // Money Session Settings
            _moneySessionSettings = root.Q<VisualElement>("MoneySessionSettings");
            _autoDoublesToggle = root.Q<Toggle>("AutoDoublesToggle");
            _beaversToggle = root.Q<Toggle>("BeaversToggle");
            _raccoonsToggle = root.Q<Toggle>("RaccoonsToggle");
            _ardvarksToggle = root.Q<Toggle>("ArdvarksToggle");
            _jacobyToggle = root.Q<Toggle>("JacobyToggle");

            // Seed
            _seedTextField = root.Q<TextField>("SeedTextField");
            _randomizeSeedButton = root.Q<Button>("RandomizeSeedButton");

            // Start button
            _startNewGameButton = root.Q<Button>("StartNewGameButton");

            // Wire button clicks
            if (_newGameModalCloseButton != null)
                _newGameModalCloseButton.clicked += HideModal;
            if (_newGameModalBackdrop != null)
                _newGameModalBackdrop.RegisterCallback<ClickEvent>(OnBackdropClicked);

            if (_gameTypePrevButton != null)
                _gameTypePrevButton.clicked += OnLevelPrev;
            if (_gameTypeNextButton != null)
                _gameTypeNextButton.clicked += OnLevelNext;

            if (_stakePrevButton != null)
                _stakePrevButton.clicked += OnStakePrev;
            if (_stakeNextButton != null)
                _stakeNextButton.clicked += OnStakeNext;

            if (_randomizeSeedButton != null)
                _randomizeSeedButton.clicked += OnRandomizeSeed;

            if (_startNewGameButton != null)
                _startNewGameButton.clicked += OnStartNewGame;

            Debug.Log("[NewGameModal] Initialized all UI bindings");
        }

        /// <summary>Shows the New Game modal and initializes default selections.</summary>
        public void ShowModal()
        {
            if (_newGameModalLayer == null) return;

            PlayPopupOpenSound();
            _newGameModalLayer.style.display = DisplayStyle.Flex;
            PopupAnimator.Show(_newGameModalCard, this);

            _selectedPresetIndex = PrestigeService.CurrentWorldIndex;
            _selectedStakeIndex = 0;

            if (DeterministicRNG.Instance != null && _seedTextField != null)
                _seedTextField.value = DeterministicRNG.Instance.GenerateRandomSeedString(8);

            RefreshWorldStakePicker();

            Debug.Log($"[NewGameModal] Opened modal with preset={PickedPreset()?.displayName}, stake={PickedStake()?.stakeAmount}, seed={_seedTextField?.value}");
        }

        /// <summary>Hides the New Game modal.</summary>
        public void HideModal()
        {
            if (_newGameModalLayer == null) return;

            PlayPopupCloseSound();
            PopupAnimator.Hide(_newGameModalCard, this,
                () => _newGameModalLayer.style.display = DisplayStyle.None);

            Debug.Log("[NewGameModal] Closed modal");
        }

        // ── Level (Preset) Selection ──────────────────────────────────────────

        private void OnLevelPrev()
        {
            if (gameModePresets == null || gameModePresets.presets.Count == 0) return;
            _selectedPresetIndex = (_selectedPresetIndex - 1 + gameModePresets.presets.Count) % gameModePresets.presets.Count;
            _selectedStakeIndex = 0;
            RefreshWorldStakePicker();
            Debug.Log($"[NewGameModal] Level changed to index {_selectedPresetIndex}");
        }

        private void OnLevelNext()
        {
            if (gameModePresets == null || gameModePresets.presets.Count == 0) return;
            _selectedPresetIndex = (_selectedPresetIndex + 1) % gameModePresets.presets.Count;
            _selectedStakeIndex = 0;
            RefreshWorldStakePicker();
            Debug.Log($"[NewGameModal] Level changed to index {_selectedPresetIndex}");
        }

        // ── Preset / Stake Picker ─────────────────────────────────────────────

        private GameModePresetSo PickedPreset()
        {
            if (gameModePresets?.presets == null || gameModePresets.presets.Count == 0) return null;
            int idx = Mathf.Clamp(_selectedPresetIndex, 0, gameModePresets.presets.Count - 1);
            return gameModePresets.presets[idx];
        }

        private StakeLevelSo PickedStake()
        {
            var stakes = PickedPreset()?.stakes;
            if (stakes == null || stakes.Count == 0) return null;
            int idx = Mathf.Clamp(_selectedStakeIndex, 0, stakes.Count - 1);
            return stakes[idx];
        }

        private bool IsPresetAccessible(int presetIndex) =>
            presetIndex <= PrestigeService.CurrentWorldIndex;

        private bool IsStakeUnlocked(StakeLevelSo stake) =>
            stake != null && IsPresetAccessible(_selectedPresetIndex)
            && MoneyBalanceService.Balance >= stake.unlockThreshold;

        private void OnStakePrev()
        {
            var stakes = PickedPreset()?.stakes;
            if (stakes == null || stakes.Count == 0) return;
            _selectedStakeIndex = (_selectedStakeIndex - 1 + stakes.Count) % stakes.Count;
            RefreshWorldStakePicker();
            Debug.Log($"[NewGameModal] Stake selection changed to index {_selectedStakeIndex}");
        }

        private void OnStakeNext()
        {
            var stakes = PickedPreset()?.stakes;
            if (stakes == null || stakes.Count == 0) return;
            _selectedStakeIndex = (_selectedStakeIndex + 1) % stakes.Count;
            RefreshWorldStakePicker();
            Debug.Log($"[NewGameModal] Stake selection changed to index {_selectedStakeIndex}");
        }

        private void RefreshWorldStakePicker()
        {
            var preset = PickedPreset();
            bool presetAccessible = IsPresetAccessible(_selectedPresetIndex);
            var stake = PickedStake();
            bool stakeUnlocked = IsStakeUnlocked(stake);

            // Update world chip (locked/unlocked visual)
            if (_worldChipImage != null)
            {
                Sprite chipSprite = null;
                if (preset?.chipStakeSprites != null && _selectedStakeIndex < preset.chipStakeSprites.Count)
                    chipSprite = preset.chipStakeSprites[_selectedStakeIndex];
                chipSprite ??= preset?.previewImage;

                _worldChipImage.style.backgroundImage = chipSprite != null
                    ? new StyleBackground(chipSprite)
                    : new StyleBackground();
                if (presetAccessible)
                    _worldChipImage.RemoveFromClassList("ng-world-chip-locked");
                else
                    _worldChipImage.AddToClassList("ng-world-chip-locked");
            }

            // Update world name
            if (_worldNameLabel != null)
                _worldNameLabel.text = preset != null
                    ? $"{preset.displayName} ({preset.currencySymbol})"
                    : "—";

            // Update stake amount (locked/unlocked visual)
            if (_stakeAmountLabel != null)
            {
                _stakeAmountLabel.text = stake != null
                    ? $"{preset?.currencySymbol}{stake.stakeAmount:N0}"
                    : "—";
                if (stakeUnlocked)
                    _stakeAmountLabel.RemoveFromClassList("ng-stake-label--locked");
                else
                    _stakeAmountLabel.AddToClassList("ng-stake-label--locked");
            }

            // Update locked message
            if (_stakeLockedLabel != null)
            {
                bool showLocked = stake != null && !stakeUnlocked;
                _stakeLockedLabel.style.display = showLocked ? DisplayStyle.Flex : DisplayStyle.None;
                if (showLocked && preset != null && stake != null)
                    _stakeLockedLabel.text = presetAccessible
                        ? $"Locked — need {preset.currencySymbol}{stake.unlockThreshold:N0}"
                        : "Locked — complete previous currency first";
            }

            // Enable/disable Start Game button
            if (_startNewGameButton != null)
                _startNewGameButton.SetEnabled(presetAccessible && stakeUnlocked);
        }

        // ── Seed Randomization ────────────────────────────────────────────────

        private void OnRandomizeSeed()
        {
            if (DeterministicRNG.Instance == null || _seedTextField == null) return;
            _seedTextField.value = DeterministicRNG.Instance.GenerateRandomSeedString(8);
            Debug.Log($"[NewGameModal] Seed randomized to: {_seedTextField.value}");
        }

        // ── Start New Game ────────────────────────────────────────────────────

        private void OnStartNewGame()
        {
            var preset = PickedPreset();
            var stake = PickedStake();

            if (preset == null || stake == null)
            {
                Debug.LogWarning("[NewGameModal] Cannot start game: missing preset or stake");
                return;
            }

            if (!IsPresetAccessible(_selectedPresetIndex) || !IsStakeUnlocked(stake))
            {
                Debug.LogWarning("[NewGameModal] Cannot start game: preset or stake locked");
                return;
            }

            // Build Money Session config from preset + toggles
            var defaults = preset.defaultConfig ?? new MoneySessionConfig();
            var config = new MoneySessionConfig
            {
                BaseStake = stake.stakeAmount,
                AutoDoublesEnabled = _autoDoublesToggle?.value ?? defaults.AutoDoublesEnabled,
                BeaversAllowed = _beaversToggle?.value ?? defaults.BeaversAllowed,
                RaccoonsAllowed = _raccoonsToggle?.value ?? defaults.RaccoonsAllowed,
                ArdvarksAllowed = _ardvarksToggle?.value ?? defaults.ArdvarksAllowed,
                JacobyRule = _jacobyToggle?.value ?? defaults.JacobyRule
            };

            config.CurrencyCode = preset.currencyCode;
            config.CurrencySymbol = preset.currencySymbol;

            string seedString = _seedTextField?.value?.Trim().ToUpper();
            if (string.IsNullOrEmpty(seedString) && DeterministicRNG.Instance != null)
                seedString = DeterministicRNG.Instance.GenerateRandomSeedString(8);

            string startingPositionId = preset.startingPositionId;

            Debug.Log($"[NewGameModal] Start game: preset={preset.displayName}, stake={config.BaseStake}, seed={seedString}, jacoby={config.JacobyRule}");

            if (hudController != null)
                hudController.StartGameWithConfig(preset.gameModeType, config, seedString, startingPositionId, preset);

            HideModal();
        }

        private void OnBackdropClicked(ClickEvent evt)
        {
            if (!ReferenceEquals(evt.target, _newGameModalBackdrop)) return;
            HideModal();
            evt.StopPropagation();
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
