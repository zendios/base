using System;
using System.Collections.Generic;
using UnityEngine;

namespace Base
{
    /// <summary>
    /// The player's settings every game has: sound, music (on / off and volume), vibration, graphics quality. Sound and music are saved in Base's save file
    /// (section "settings", registered by itself); vibration is HapticManager.IsOn. SoundManager, SFXSampleManager and
    /// MusicManager follow them; a game's own audio listens to the events, which are also raised when the save loads.
    ///   GameSettings.OnSoundChanged += on => sfxSource.mute = !on;
    ///   GameSettings.MusicOn = false;
    /// </summary>
    public static class GameSettings
    {
        [Serializable]
        private class Data
        {
            public bool soundOn = true;
            public bool musicOn = true;
            public float soundVolume = 1f;
            public float musicVolume = 1f;
            public int quality = QualityAuto;
        }

        private class Section : ISaveSection
        {
            public string SaveKey => "settings";

            public string ToJson() => JsonUtility.ToJson(data);

            public void FromJson(string json)
            {
                JsonUtility.FromJsonOverwrite(json, data);
                OnSoundChanged?.Invoke(data.soundOn);
                OnMusicChanged?.Invoke(data.musicOn);
                OnSoundVolumeChanged?.Invoke(data.soundVolume);
                OnMusicVolumeChanged?.Invoke(data.musicVolume);
                ApplyQuality();
            }
        }

        private static Data data = new Data();
        private static readonly Section section = new Section();

        /// <summary>Sound effects on or off (also raised when the save loads).</summary>
        public static event Action<bool> OnSoundChanged;
        /// <summary>Music on or off (also raised when the save loads).</summary>
        public static event Action<bool> OnMusicChanged;
        /// <summary>Vibration on or off.</summary>
        public static event Action<bool> OnVibrationChanged;

        /// <summary>Sound effects volume, 0 to 1 (also raised when the save loads).</summary>
        public static event Action<float> OnSoundVolumeChanged;

        /// <summary>Music volume, 0 to 1 (also raised when the save loads).</summary>
        public static event Action<float> OnMusicVolumeChanged;

        /// <summary>Applied QualitySettings level (0 = lowest), raised when Quality changes and when the save loads.</summary>
        public static event Action<int> OnQualityChanged;

        /// <summary>Quality = this: the level picked from the device (memory).</summary>
        public const int QualityAuto = -1;

        /// <summary>Sound effects on or off.</summary>
        public static bool SoundOn
        {
            get => data.soundOn;
            set
            {
                if (data.soundOn == value)
                    return;
                data.soundOn = value;
                Log("sound", value);
                OnSoundChanged?.Invoke(value);
            }
        }

        /// <summary>Music on or off.</summary>
        public static bool MusicOn
        {
            get => data.musicOn;
            set
            {
                if (data.musicOn == value)
                    return;
                data.musicOn = value;
                Log("music", value);
                OnMusicChanged?.Invoke(value);
            }
        }

        /// <summary>Vibration on or off: HapticManager.IsOn, which keeps it itself.</summary>
        public static bool VibrationOn
        {
            get => HapticManager.IsOn > 0;
            set
            {
                if (VibrationOn == value)
                    return;
                HapticManager.IsOn = value ? 1 : 0;
                Log("vibration", value);
                OnVibrationChanged?.Invoke(value);
            }
        }

        /// <summary>Sound effects volume, 0 to 1; 0 also turns SoundOn off, above 0 turns it on.</summary>
        public static float SoundVolume
        {
            get => data.soundVolume;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(data.soundVolume, value))
                    return;
                data.soundVolume = value;
                SoundOn = value > 0f;
                OnSoundVolumeChanged?.Invoke(value);
            }
        }

        /// <summary>Music volume, 0 to 1; 0 also turns MusicOn off, above 0 turns it on.</summary>
        public static float MusicVolume
        {
            get => data.musicVolume;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(data.musicVolume, value))
                    return;
                data.musicVolume = value;
                MusicOn = value > 0f;
                OnMusicVolumeChanged?.Invoke(value);
            }
        }

        /// <summary>Graphics quality: an index of QualitySettings.names (0 = lowest), or QualityAuto. Applied at once and
        /// when the save loads.</summary>
        public static int Quality
        {
            get => data.quality;
            set
            {
                data.quality = Mathf.Clamp(value, QualityAuto, QualitySettings.names.Length - 1);
                ApplyQuality();
            }
        }

        /// <summary>The level QualityAuto picks on this device: the lowest under 3 GB of memory, the middle one under
        /// 6 GB, else the highest.</summary>
        public static int AutoQualityLevel
        {
            get
            {
                int levels = QualitySettings.names.Length;
                int memory = SystemInfo.systemMemorySize;
                return Mathf.Max(0, memory < 3000 ? 0 : memory < 6000 ? (levels - 1) / 2 : levels - 1);
            }
        }

        private static void ApplyQuality()
        {
            int levels = QualitySettings.names.Length;
            if (levels == 0)
                return;
            int level = Mathf.Clamp(data.quality >= 0 ? data.quality : AutoQualityLevel, 0, levels - 1);
            QualitySettings.SetQualityLevel(level, true);
            OnQualityChanged?.Invoke(level);
        }

        internal static ISaveSection SaveSection => section;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() => DataManager.RegisterSection(section);

        // Statics survive Enter Play Mode without domain reload (DataManager resets its own the same way).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStatics()
        {
            data = new Data();
            OnSoundChanged = null;
            OnMusicChanged = null;
            OnVibrationChanged = null;
            OnSoundVolumeChanged = null;
            OnMusicVolumeChanged = null;
            OnQualityChanged = null;
        }

        private static void Log(string setting, bool on) =>
            AnalyticsManager.LogEvent("settings_change", new Dictionary<string, object> { { "setting", setting }, { "on", on } });
    }
}
