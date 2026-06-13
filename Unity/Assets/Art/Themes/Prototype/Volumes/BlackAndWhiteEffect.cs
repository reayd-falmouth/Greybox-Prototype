using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace App.Themes.Prototype.Volumes
{
    public class BlackAndWhiteEffect : MonoBehaviour
    {
        public Volume volume;
        private ColorAdjustments colorAdjustments;

        void Start()
        {
            if (volume.profile.TryGet(out colorAdjustments))
            {
                colorAdjustments.saturation.value = -100f; // Make grayscale
            }
        }

        public void SetGrayscale(bool enabled)
        {
            if (colorAdjustments != null)
                colorAdjustments.saturation.value = enabled ? -100f : 0f;
        }
    }
}