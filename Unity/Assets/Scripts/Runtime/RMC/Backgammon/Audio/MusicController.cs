using MoreMountains.Tools;
using Runtime.RMC.Backgammon.Settings;
using UnityEngine;

namespace Runtime.RMC.Backgammon.Audio
{
    public class MusicController : MonoBehaviour
    {
        [SerializeField] private AudioClip musicClip;
        [SerializeField] private float fadeInDuration = 2f;
        [SerializeField] [Range(0.5f, 2f)] private float pitch = 1f;

        private AudioSource _musicSource;

        private void OnEnable()
        {
            BackgammonSettings.OnMusicVolumeChanged += OnMusicVolumeChanged;
        }

        private void OnDisable()
        {
            BackgammonSettings.OnMusicVolumeChanged -= OnMusicVolumeChanged;
        }

        private void Start()
        {
            if (musicClip == null) return;

            var options = MMSoundManagerPlayOptions.Default;
            options.MmSoundManagerTrack = MMSoundManager.MMSoundManagerTracks.Music;
            options.Loop = true;
            options.Volume = BackgammonSettings.MusicVolumeLinear;
            options.Pitch = pitch;
            options.Fade = true;
            options.FadeInitialVolume = 0f;
            options.FadeDuration = fadeInDuration;

            _musicSource = MMSoundManagerSoundPlayEvent.Trigger(musicClip, options);
        }

        private void OnValidate()
        {
            if (_musicSource != null)
                _musicSource.pitch = pitch;
        }

        private void OnMusicVolumeChanged(float volume)
        {
            if (MMSoundManager.Instance != null)
                MMSoundManager.Instance.SetVolumeMusic(volume);
        }
    }
}
