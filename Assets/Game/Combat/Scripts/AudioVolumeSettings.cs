using System;
using UnityEngine;

namespace StarterGame
{
    public static class AudioVolumeSettings
    {
        private const string MasterKey = "StarterGame.Audio.MasterVolume";
        private const string GunKey = "StarterGame.Audio.GunVolume";
        private static bool loaded;
        private static float masterVolume = 1f;
        private static float gunVolume = 1f;
        public static event Action<float> GunVolumeChanged;
        public static float MasterVolume { get { EnsureLoaded(); return masterVolume; } }
        public static float GunVolume { get { EnsureLoaded(); return gunVolume; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            loaded = false;
            masterVolume = gunVolume = 1f;
            GunVolumeChanged = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadPreferences()
        {
            masterVolume = SafeVolume(PlayerPrefs.GetFloat(MasterKey, 1f));
            gunVolume = SafeVolume(PlayerPrefs.GetFloat(GunKey, 1f));
            loaded = true;
            AudioListener.volume = masterVolume;
            GunVolumeChanged?.Invoke(gunVolume);
        }

        private static void EnsureLoaded() { if (!loaded) LoadPreferences(); }
        private static float SafeVolume(float value) => float.IsNaN(value) ? 1f : Mathf.Clamp01(value);

        public static void SetMasterVolume(float value, bool remember = true)
        {
            EnsureLoaded();
            masterVolume = SafeVolume(value);
            AudioListener.volume = masterVolume;
            if (remember) PlayerPrefs.SetFloat(MasterKey, masterVolume);
        }

        public static void SetGunVolume(float value, bool remember = true)
        {
            EnsureLoaded();
            gunVolume = SafeVolume(value);
            GunVolumeChanged?.Invoke(gunVolume);
            if (remember) PlayerPrefs.SetFloat(GunKey, gunVolume);
        }
    }
}
