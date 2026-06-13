using UnityEngine;
using UnityEngine.UIElements;
using Runtime.RMC.Backgammon.Settings;

namespace Runtime.RMC.Backgammon.UI
{
    /// <summary>
    /// Startup menu that precedes every other screen and lets the player choose the
    /// Desktop or Mobile HUD layout. Both layouts are functionally identical for now;
    /// they differ only by their UXML source asset and the version/layout debug string.
    ///
    /// This controller lives on its own GameObject with its own <see cref="UIDocument"/>
    /// (rendered above the HUD via a higher sorting order). The HUD itself stays active
    /// and bound to the default (Desktop) layout so dependent controllers
    /// (e.g. DeterministicRNG, ScreenWipe) initialise normally. When the player picks a
    /// layout we swap the HUD's source asset (only if it actually changes) and toggle the
    /// HUD GameObject off/on to force every HUD controller to re-bind against the new tree.
    /// </summary>
    [DisallowMultipleComponent]
    public class LayoutSelectController : MonoBehaviour
    {
        [Header("Selector")]
        [Tooltip("UIDocument that hosts LayoutSelect.uxml (usually on this GameObject).")]
        [SerializeField] private UIDocument selectorDocument;

        [Header("HUD targets")]
        [Tooltip("Root GameObject carrying the HUD UIDocument and HUD controllers (BackgammonHud).")]
        [SerializeField] private GameObject hudRoot;
        [Tooltip("The HUD UIDocument whose source asset is swapped per layout.")]
        [SerializeField] private UIDocument hudDocument;
        [Tooltip("HUD controller, used to refresh the version/layout debug string.")]
        [SerializeField] private BackgammonHudController hudController;

        [Header("Layout assets")]
        [SerializeField] private VisualTreeAsset desktopLayout;
        [SerializeField] private VisualTreeAsset mobileLayout;

        private Button _desktopButton;
        private Button _mobileButton;

        private void OnEnable()
        {
            if (selectorDocument == null)
                selectorDocument = GetComponent<UIDocument>();
            if (selectorDocument == null) return;

            var root = selectorDocument.rootVisualElement;
            if (root == null) return;

            _desktopButton = root.Q<Button>("SelectDesktopButton");
            _mobileButton = root.Q<Button>("SelectMobileButton");

            if (_desktopButton != null) _desktopButton.clicked += OnDesktopClicked;
            if (_mobileButton != null) _mobileButton.clicked += OnMobileClicked;

            var versionLabel = root.Q<Label>("VersionDebugLabel");
            if (versionLabel != null)
                versionLabel.text = $"v{Application.version} \u2022 Select";
        }

        private void OnDisable()
        {
            if (_desktopButton != null) _desktopButton.clicked -= OnDesktopClicked;
            if (_mobileButton != null) _mobileButton.clicked -= OnMobileClicked;
        }

        private void OnDesktopClicked() => ApplyLayout(BackgammonLayoutType.Desktop);
        private void OnMobileClicked() => ApplyLayout(BackgammonLayoutType.Mobile);

        private void ApplyLayout(BackgammonLayoutType layout)
        {
            BackgammonSettings.LayoutType = layout;

            var asset = layout == BackgammonLayoutType.Mobile ? mobileLayout : desktopLayout;

            if (hudDocument != null && asset != null && hudDocument.visualTreeAsset != asset)
            {
                hudDocument.visualTreeAsset = asset;

                // Force every HUD controller (which queries the tree in OnEnable) to
                // re-bind against the freshly built visual tree.
                if (hudRoot != null && hudRoot.activeSelf)
                {
                    hudRoot.SetActive(false);
                    hudRoot.SetActive(true);
                }
            }

            // Ensure the corner debug string reflects the active layout even when the
            // asset did not change (e.g. choosing the default Desktop layout).
            if (hudController != null)
                hudController.RefreshVersionDebugLabel();

            HideSelector();
        }

        private void HideSelector()
        {
            // Disable the whole selector GameObject so its UIDocument stops rendering
            // and stops capturing input.
            gameObject.SetActive(false);
        }
    }
}
