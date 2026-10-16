using UnityEngine;

namespace StarterGame
{
    /// <summary>Keep new, playing and delayed weapon sounds in sync with the gun slider.</summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class GunAudioVolume : MonoBehaviour
    {
        private AudioSource source;
        private float originalVolume;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            originalVolume = source.volume;
        }

        private void OnEnable()
        {
            AudioVolumeSettings.GunVolumeChanged += Apply;
            Apply(AudioVolumeSettings.GunVolume);
        }

        public void SetBaseVolume(float volume)
        {
            originalVolume = Mathf.Clamp01(volume);
            Apply(AudioVolumeSettings.GunVolume);
        }

        private void OnDisable() { AudioVolumeSettings.GunVolumeChanged -= Apply; }
        private void Apply(float volume) { if (source) source.volume = originalVolume * volume; }
    }
}
