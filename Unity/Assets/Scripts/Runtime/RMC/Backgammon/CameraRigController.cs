using Cinemachine;
using Runtime.RMC.Backgammon.Settings;
using Runtime.RMC.Backgammon.UI;
using UnityEngine;

namespace Runtime.RMC.Backgammon
{
    /// <summary>
    /// Drives which Cinemachine virtual camera is live based on the current HUD
    /// layout (Desktop / Mobile) and the chosen camera angle (Top Down / Angled).
    ///
    /// Four explicit virtual cameras are referenced so each layout × angle framing
    /// can be positioned and tuned visually in the Scene view. The active camera is
    /// given <see cref="ActivePriority"/> while all others drop to
    /// <see cref="InactivePriority"/>; the <c>CinemachineBrain</c> blends between
    /// them smoothly using its configured default blend.
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraRigController : MonoBehaviour
    {
        [Header("Virtual Cameras")]
        [Tooltip("Desktop layout, Top Down angle.")]
        [SerializeField] private CinemachineVirtualCamera desktopTopDown;
        [Tooltip("Desktop layout, Angled angle.")]
        [SerializeField] private CinemachineVirtualCamera desktopAngled;
        [Tooltip("Mobile layout, Top Down angle.")]
        [SerializeField] private CinemachineVirtualCamera mobileTopDown;
        [Tooltip("Mobile layout, Angled angle.")]
        [SerializeField] private CinemachineVirtualCamera mobileAngled;

        [Header("Priorities")]
        [SerializeField] private int activePriority = 20;
        [SerializeField] private int inactivePriority = 0;

        // CameraAngle indices (see BackgammonSettings.CameraAngle): 0 = Top Down, 1 = Angled.
        private const int AngleTopDown = 0;
        private const int AngleAngled = 1;

        private void OnEnable()
        {
            BackgammonSettings.OnGraphicsSettingsChanged += ApplyCameraView;
            BackgammonSettings.OnLayoutChanged += ApplyCameraView;
            ApplyCameraView();
        }

        private void OnDisable()
        {
            BackgammonSettings.OnGraphicsSettingsChanged -= ApplyCameraView;
            BackgammonSettings.OnLayoutChanged -= ApplyCameraView;
        }

        /// <summary>Selects and activates the camera matching the current settings.</summary>
        public void ApplyCameraView()
        {
            bool isMobile = BackgammonSettings.LayoutType == BackgammonLayoutType.Mobile;
            bool isAngled = BackgammonSettings.CameraAngle == AngleAngled;

            CinemachineVirtualCamera target = isMobile
                ? (isAngled ? mobileAngled : mobileTopDown)
                : (isAngled ? desktopAngled : desktopTopDown);

            SetActive(target);
        }

        private void SetActive(CinemachineVirtualCamera target)
        {
            ApplyPriority(desktopTopDown, target);
            ApplyPriority(desktopAngled, target);
            ApplyPriority(mobileTopDown, target);
            ApplyPriority(mobileAngled, target);
        }

        private void ApplyPriority(CinemachineVirtualCamera cam, CinemachineVirtualCamera target)
        {
            if (cam == null) return;
            cam.Priority = cam == target ? activePriority : inactivePriority;
        }
    }
}
