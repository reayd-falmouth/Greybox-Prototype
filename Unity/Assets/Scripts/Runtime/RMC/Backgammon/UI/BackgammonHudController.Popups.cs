using Runtime.RMC.Backgammon.UI;
using UnityEngine.UIElements;

/// <summary>
/// Popup popups (Message / Player Profile / Opponent Profile) handled directly by
/// the HUD controller so they require no extra scene-assigned components. Bound by
/// element name from whichever HUD layout (Desktop/Mobile) is active.
/// </summary>
public partial class BackgammonHudController
{
    // Message popup
    private VisualElement _messagePopupLayer;
    private VisualElement _messagePopupCard;
    private VisualElement _messagePopupBackdrop;
    private Button _messagePopupCloseButton;

    // Player profile popup
    private VisualElement _playerProfileLayer;
    private VisualElement _playerProfileCard;
    private VisualElement _playerProfileBackdrop;
    private Button _playerProfileCloseButton;

    // Opponent profile popup
    private VisualElement _opponentProfileLayer;
    private VisualElement _opponentProfileCard;
    private VisualElement _opponentProfileBackdrop;
    private Button _opponentProfileCloseButton;

    /// <summary>Binds the popup layers and wires their close/backdrop interactions.</summary>
    private void InitPopups(VisualElement root)
    {
        if (root == null) return;

        // Message popup
        _messagePopupLayer = root.Q<VisualElement>("MessagePopupLayer");
        _messagePopupCard = root.Q<VisualElement>("MessagePopupCard");
        _messagePopupBackdrop = root.Q<VisualElement>("MessagePopupBackdrop");
        _messagePopupCloseButton = root.Q<Button>("MessagePopupCloseButton");
        if (_messagePopupCloseButton != null)
            _messagePopupCloseButton.clicked += HideMessagePopup;
        if (_messagePopupBackdrop != null)
            _messagePopupBackdrop.RegisterCallback<ClickEvent>(OnMessageBackdropClicked);

        // Player profile popup
        _playerProfileLayer = root.Q<VisualElement>("PlayerProfilePopupLayer");
        _playerProfileCard = root.Q<VisualElement>("PlayerProfileCard");
        _playerProfileBackdrop = root.Q<VisualElement>("PlayerProfileBackdrop");
        _playerProfileCloseButton = root.Q<Button>("PlayerProfileCloseButton");
        if (_playerProfileCloseButton != null)
            _playerProfileCloseButton.clicked += HidePlayerProfilePopup;
        if (_playerProfileBackdrop != null)
            _playerProfileBackdrop.RegisterCallback<ClickEvent>(OnPlayerProfileBackdropClicked);

        // Opponent profile popup
        _opponentProfileLayer = root.Q<VisualElement>("OpponentProfilePopupLayer");
        _opponentProfileCard = root.Q<VisualElement>("OpponentProfileCard");
        _opponentProfileBackdrop = root.Q<VisualElement>("OpponentProfileBackdrop");
        _opponentProfileCloseButton = root.Q<Button>("OpponentProfileCloseButton");
        if (_opponentProfileCloseButton != null)
            _opponentProfileCloseButton.clicked += HideOpponentProfilePopup;
        if (_opponentProfileBackdrop != null)
            _opponentProfileBackdrop.RegisterCallback<ClickEvent>(OnOpponentProfileBackdropClicked);
    }

    // ── Message ───────────────────────────────────────────────────────────────

    public void ShowMessagePopup()
    {
        if (_messagePopupLayer == null) return;
        PlayPopupOpenSound();
        _messagePopupLayer.style.display = DisplayStyle.Flex;
        PopupAnimator.Show(_messagePopupCard, this);
    }

    public void HideMessagePopup()
    {
        if (_messagePopupLayer == null) return;
        PlayPopupCloseSound();
        PopupAnimator.Hide(_messagePopupCard, this, () => _messagePopupLayer.style.display = DisplayStyle.None);
    }

    private void OnMessageBackdropClicked(ClickEvent evt)
    {
        if (_messagePopupBackdrop == null || !ReferenceEquals(evt.target, _messagePopupBackdrop)) return;
        HideMessagePopup();
        evt.StopPropagation();
    }

    // ── Player profile ──────────────────────────────────────────────────────────

    public void ShowPlayerProfilePopup()
    {
        if (_playerProfileLayer == null) return;
        PlayPopupOpenSound();
        _playerProfileLayer.style.display = DisplayStyle.Flex;
        PopupAnimator.Show(_playerProfileCard, this);
    }

    public void HidePlayerProfilePopup()
    {
        if (_playerProfileLayer == null) return;
        PlayPopupCloseSound();
        PopupAnimator.Hide(_playerProfileCard, this, () => _playerProfileLayer.style.display = DisplayStyle.None);
    }

    private void OnPlayerProfileBackdropClicked(ClickEvent evt)
    {
        if (_playerProfileBackdrop == null || !ReferenceEquals(evt.target, _playerProfileBackdrop)) return;
        HidePlayerProfilePopup();
        evt.StopPropagation();
    }

    // ── Opponent profile ────────────────────────────────────────────────────────

    public void ShowOpponentProfilePopup()
    {
        if (_opponentProfileLayer == null) return;
        PlayPopupOpenSound();
        _opponentProfileLayer.style.display = DisplayStyle.Flex;
        PopupAnimator.Show(_opponentProfileCard, this);
    }

    public void HideOpponentProfilePopup()
    {
        if (_opponentProfileLayer == null) return;
        PlayPopupCloseSound();
        PopupAnimator.Hide(_opponentProfileCard, this, () => _opponentProfileLayer.style.display = DisplayStyle.None);
    }

    private void OnOpponentProfileBackdropClicked(ClickEvent evt)
    {
        if (_opponentProfileBackdrop == null || !ReferenceEquals(evt.target, _opponentProfileBackdrop)) return;
        HideOpponentProfilePopup();
        evt.StopPropagation();
    }
}
