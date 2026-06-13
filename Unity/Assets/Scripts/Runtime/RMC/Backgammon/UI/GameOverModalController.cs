using UnityEngine;
using UnityEngine.UIElements;

namespace Runtime.RMC.Backgammon.UI
{
    /// <summary>
    /// Manages the Game Over popup display and next game button.
    /// </summary>
    public class GameOverModalController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BackgammonHudController hudController;
        [SerializeField] private BackgammonGameController gameController;

        // UI Elements
        private VisualElement _gameOverPopupLayer;
        private VisualElement _gameOverPopupCard;
        private Label _gameOverPopupTitleLabel;
        private Label _gameOverPopupSummaryLabel;
        private Button _gameOverNextGameButton;
        private Button _exitToLobbyButton;

        private bool _gameOverPopupShown;

        /// <summary>Binds all UI elements from the root VisualElement.</summary>
        public void Initialize(VisualElement root)
        {
            _gameOverPopupLayer = root.Q<VisualElement>("GameOverPopupLayer");
            _gameOverPopupCard = root.Q<VisualElement>("GameOverPopupCard");
            _gameOverPopupTitleLabel = root.Q<Label>("GameOverPopupTitleLabel");
            _gameOverPopupSummaryLabel = root.Q<Label>("GameOverPopupSummaryLabel");
            _gameOverNextGameButton = root.Q<Button>("GameOverNextGameButton");
            _exitToLobbyButton      = root.Q<Button>("GameOverExitToLobbyButton");

            // Wire button clicks
            if (_gameOverNextGameButton != null)
                _gameOverNextGameButton.clicked += OnGameOverNextGameClicked;
            if (_exitToLobbyButton != null)
                _exitToLobbyButton.clicked += OnExitToLobbyClicked;

            Debug.Log("[GameOverModal] Initialized all UI bindings");
        }

        /// <summary>Shows the Game Over popup with a summary message.</summary>
        public void ShowGameOver(string summaryText)
        {
            if (_gameOverPopupLayer == null)
                return;

            _gameOverPopupShown = true;
            PlayPopupOpenSound();
            _gameOverPopupLayer.style.display = DisplayStyle.Flex;
            PopupAnimator.Show(_gameOverPopupCard, this);

            if (_gameOverPopupTitleLabel != null)
                _gameOverPopupTitleLabel.text = "Game Over";

            if (_gameOverPopupSummaryLabel != null)
                _gameOverPopupSummaryLabel.text = string.IsNullOrWhiteSpace(summaryText)
                    ? "Game finished."
                    : summaryText;

            Debug.Log($"[GameOverModal] Popup shown with summary=\"{summaryText}\"");
        }

        /// <summary>Hides the Game Over popup.</summary>
        public void HideGameOver()
        {
            if (_gameOverPopupLayer == null)
                return;

            if (_gameOverPopupShown)
            {
                PlayPopupCloseSound();
                _gameOverPopupShown = false;
            }

            PopupAnimator.Hide(_gameOverPopupCard, this, () => _gameOverPopupLayer.style.display = DisplayStyle.None);
            Debug.Log("[GameOverModal] Popup hidden");
        }

        private void OnGameOverNextGameClicked()
        {
            Debug.Log("[GameOverModal] Next game requested from popup");

            gameController?.NewGame();
            HideGameOver();
        }

        private void OnExitToLobbyClicked()
        {
            Debug.Log("[GameOverModal] Exit to lobby requested");
            HideGameOver();
            hudController?.ExitToLobby();
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
