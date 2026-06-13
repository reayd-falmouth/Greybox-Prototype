using Runtime.RMC.Backgammon.Settings;
using Runtime.RMC.Backgammon.Theme;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Runtime.RMC.Backgammon.UI
{
    /// <summary>
    /// Applies graphics settings from <see cref="BackgammonSettings"/> to a URP Global Volume.
    /// Assign the Global Volume reference in the Inspector, then attach this component to any
    /// persistent GameObject in the scene (e.g., the Global Volume GameObject itself).
    /// </summary>
    public class GraphicsSettingsController : MonoBehaviour
    {
        [SerializeField] private Volume globalVolume;
        [SerializeField] private ThemeManager themeManager;

        private ColorAdjustments _colorAdjustments;
        private Bloom _bloom;
        private FilmGrain _filmGrain;

        private void OnEnable()
        {
            BackgammonSettings.OnGraphicsSettingsChanged += ApplyAll;
            CacheVolumeComponents();
            ApplyAll();
        }

        private void OnDisable()
        {
            BackgammonSettings.OnGraphicsSettingsChanged -= ApplyAll;
        }

        private void CacheVolumeComponents()
        {
            if (globalVolume == null || globalVolume.profile == null)
            {
                Debug.LogWarning("[GraphicsSettings] No Global Volume assigned — post-processing controls inactive.");
                return;
            }

            globalVolume.profile.TryGet(out _colorAdjustments);
            globalVolume.profile.TryGet(out _bloom);
            globalVolume.profile.TryGet(out _filmGrain);
        }

        private void ApplyAll()
        {
            ApplyBrightness();
            ApplyContrast();
            ApplyCrtBloom();
            ApplyScanLines();
            themeManager?.ApplyActiveTheme();
            Debug.Log("[Event][Graphics] ApplyAll fired");
        }

        private void ApplyBrightness()
        {
            if (_colorAdjustments == null) return;
            // postExposure range: drives perceived brightness; 0 = neutral, map slider 0.5–1.5 → -0.5..+0.5 EV
            _colorAdjustments.postExposure.value = BackgammonSettings.Brightness - 1f;
        }

        private void ApplyContrast()
        {
            if (_colorAdjustments == null) return;
            // URP contrast is -100..+100; map slider 0.5–1.5 → -50..+50
            _colorAdjustments.contrast.value = (BackgammonSettings.Contrast - 1f) * 100f;
        }

        private void ApplyCrtBloom()
        {
            if (_bloom == null) return;
            _bloom.active = BackgammonSettings.CrtBloom;
            if (BackgammonSettings.CrtBloom)
            {
                _bloom.threshold.value = 0.8f;
                _bloom.intensity.value = 0.6f;
            }
        }

        private void ApplyScanLines()
        {
            if (_filmGrain == null) return;
            // FilmGrain is a placeholder stand-in for scan lines until a custom shader effect is added
            _filmGrain.active = BackgammonSettings.ScanLines;
            _filmGrain.intensity.value = BackgammonSettings.ScanLines ? 0.3f : 0f;
        }
    }
}
