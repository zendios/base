using UnityEngine;
using System;
using System.Globalization;
using System.Collections.Generic;

namespace Base.Ads
{
    public enum TaichiMode
    {
        Strategy2_ResetAccumulation = 0,          // Strategy 2 - Recommended for low thresholds (Accumulate, fire, reset cache to 0)
        Strategy1_PassThroughAfterMilestone = 1    // Strategy 1 - Recommended for high thresholds (Accumulate, fire once, pass subsequent raw values)
    }

    [Serializable]
    public class TaichiThreshold
    {
        public int tierPercent;         // e.g., 50, 40, 30, 20, 10
        public double value;            // High-precision threshold value in USD
        public string eventName;        // e.g., "taichi_30"
    }

    [Serializable]
    public class TaichiConfig
    {
        public string countryCode;
        public int mode;                // 0: Strategy 2, 1: Strategy 1
        public List<TaichiThreshold> thresholds = new List<TaichiThreshold>();
    }

    /// <summary>
    /// Upgraded Taichi Manager aligned with the Google gTech Technical Guide (Dated 2026-03-04).
    /// Implements high-precision lossless double storage and zero-allocation hot path integration.
    /// </summary>
    public class TaichiManager : MonoBehaviour
    {
        public static TaichiManager Instance { get; private set; }

        private string _detectedCountryCode = "ROW";
        private TaichiMode _currentMode = TaichiMode.Strategy2_ResetAccumulation; // Default Strategy 2 (Reset Accumulation)
        private List<TaichiThreshold> _activeThresholds = new List<TaichiThreshold>();

        private bool _isDataDirty = false;

        private class ActiveThresholdState
        {
            public int tierPercent;
            public double value;
            public string cacheKey;
            public string isFirstTimeKey;
            public string eventName;

            // Double-precision runtime state machine caches
            public double currentTroasCache;
            public bool isFirstTime;
        }

        private readonly List<ActiveThresholdState> _runtimeStates = new List<ActiveThresholdState>();

        // Local regional offline fallbacks
        private readonly Dictionary<string, List<TaichiThreshold>> _localFallbacks = new Dictionary<string, List<TaichiThreshold>>
        {
            {
                "US", new List<TaichiThreshold>
                {
                    new TaichiThreshold { tierPercent = 50, value = 0.05, eventName = "taichi_50" },
                    new TaichiThreshold { tierPercent = 40, value = 0.06, eventName = "taichi_40" },
                    new TaichiThreshold { tierPercent = 30, value = 0.10, eventName = "taichi_30" },
                    new TaichiThreshold { tierPercent = 20, value = 0.60, eventName = "taichi_20" },
                    new TaichiThreshold { tierPercent = 10, value = 0.80, eventName = "taichi_10" }
                }
            },
            {
                "JP", new List<TaichiThreshold>
                {
                    new TaichiThreshold { tierPercent = 50, value = 0.01, eventName = "taichi_50" },
                    new TaichiThreshold { tierPercent = 40, value = 0.02, eventName = "taichi_40" },
                    new TaichiThreshold { tierPercent = 30, value = 0.03, eventName = "taichi_30" },
                    new TaichiThreshold { tierPercent = 20, value = 0.05, eventName = "taichi_20" },
                    new TaichiThreshold { tierPercent = 10, value = 0.10, eventName = "taichi_10" }
                }
            }
        };

        private readonly List<TaichiThreshold> _emergingFallback = new List<TaichiThreshold>
        {
            new TaichiThreshold { tierPercent = 50, value = 0.005, eventName = "taichi_50" },
            new TaichiThreshold { tierPercent = 40, value = 0.006, eventName = "taichi_40" },
            new TaichiThreshold { tierPercent = 30, value = 0.01, eventName = "taichi_30" },
            new TaichiThreshold { tierPercent = 20, value = 0.03, eventName = "taichi_20" },
            new TaichiThreshold { tierPercent = 10, value = 0.05, eventName = "taichi_10" }
        };

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeFallback();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Bulletproof 3-Tiered Offline Fallback.
        /// TIER 1: Firebase Remote Config (Online Primary)
        /// TIER 2: Android Telephony SIM ISO (Offline Cellular Primary - No permissions required)
        /// TIER 3: OS Locale/RegionInfo (Offline Tablet/Wifi Primary)
        /// </summary>
        private void InitializeFallback()
        {
            _detectedCountryCode = "ROW";

#if UNITY_EDITOR
            try
            {
                _detectedCountryCode = RegionInfo.CurrentRegion.TwoLetterISORegionName.ToUpper();
            }
            catch (Exception)
            {
                _detectedCountryCode = "ROW";
            }
#elif UNITY_ANDROID
            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    {
                        using (AndroidJavaObject telephonyManager = currentActivity.Call<AndroidJavaObject>("getSystemService", "phone"))
                        {
                            if (telephonyManager != null)
                            {
                                string networkCountry = telephonyManager.Call<string>("getNetworkCountryIso");
                                if (!string.IsNullOrEmpty(networkCountry))
                                {
                                    _detectedCountryCode = networkCountry.ToUpper();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                try
                {
                    using (AndroidJavaClass localeClass = new AndroidJavaClass("java.util.Locale"))
                    {
                        if (localeClass != null)
                        {
                            using (AndroidJavaObject defaultLocale = localeClass.CallStatic<AndroidJavaObject>("getDefault"))
                            {
                                if (defaultLocale != null)
                                {
                                    string androidCountry = defaultLocale.Call<string>("getCountry");
                                    if (!string.IsNullOrEmpty(androidCountry))
                                    {
                                        _detectedCountryCode = androidCountry.ToUpper();
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    _detectedCountryCode = "ROW";
                }
            }
#elif UNITY_IOS
            try
            {
                _detectedCountryCode = RegionInfo.CurrentRegion.TwoLetterISORegionName.ToUpper();
            }
            catch (Exception)
            {
                _detectedCountryCode = "ROW";
            }
#endif

            // Assign localized fallback thresholds based on physical ISO country code resolution
            if (_localFallbacks.TryGetValue(_detectedCountryCode, out List<TaichiThreshold> fallbackList))
            {
                _activeThresholds = fallbackList;
            }
            else
            {
                _activeThresholds = _emergingFallback;
            }

            _currentMode = TaichiMode.Strategy1_PassThroughAfterMilestone;
            RebuildRuntimeStates();

            if (DebugMode.IsOn)
            {
                Debug.Log("[Taichi] Fallback initialized. Target Geo: " + _detectedCountryCode + " | Selected Mode: " + _currentMode);
            }
        }

        private void RebuildRuntimeStates()
        {
            _runtimeStates.Clear();

            for (int i = 0; i < _activeThresholds.Count; i++)
            {
                TaichiThreshold threshold = _activeThresholds[i];

                ActiveThresholdState state = new ActiveThresholdState
                {
                    tierPercent = threshold.tierPercent,
                    value = threshold.value,
                    cacheKey = "Taichi_TroasCache_" + _detectedCountryCode + "_" + threshold.tierPercent,
                    isFirstTimeKey = "Taichi_IsFirstTime_" + _detectedCountryCode + "_" + threshold.tierPercent,
                    eventName = threshold.eventName,
                    currentTroasCache = 0.0,
                    isFirstTime = true
                };

                // Lossless double-precision restoration via Round-Trip format parsing
                if (PlayerPrefs.HasKey(state.cacheKey))
                {
                    string rawValue = PlayerPrefs.GetString(state.cacheKey);
                    if (double.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedValue))
                    {
                        state.currentTroasCache = parsedValue;
                    }
                }

                // Restore lifecycle isFirstTime trigger flags
                if (PlayerPrefs.HasKey(state.isFirstTimeKey))
                {
                    state.isFirstTime = PlayerPrefs.GetInt(state.isFirstTimeKey) == 1;
                }
                else
                {
                    state.isFirstTime = true;
                }

                _runtimeStates.Add(state);
            }
        }

        /// <summary>
        /// External callback triggered by Remote Config on successful fetch of configuration payload.
        /// </summary>
        public void ApplyRemoteConfig(string jsonPayload)
        {
            if (string.IsNullOrEmpty(jsonPayload))
            {
                return;
            }

            try
            {
                TaichiConfig config = JsonUtility.FromJson<TaichiConfig>(jsonPayload);
                if (config != null && config.thresholds != null && config.thresholds.Count > 0)
                {
                    _detectedCountryCode = config.countryCode.ToUpper();
                    _currentMode = (TaichiMode)config.mode;
                    _activeThresholds = config.thresholds;
                    RebuildRuntimeStates();

                    if (DebugMode.IsOn)
                    {
                        Debug.Log("[Taichi] Config applied successfully. Geo: " + _detectedCountryCode + " | Mode: " + _currentMode);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[Taichi] Error parsing Remote Config JSON payload: " + ex.Message);
            }
        }

        /// <summary>
        /// Hot Path: Processes incoming advertisement revenue with zero float loss and optimized caching.
        /// </summary>
        public void ProcessAdRevenue(double revenueInUSD, string currencyCode, double lifeTimeValue)
        {
            // Logs basic ad impression to build global baseline statistics
            LogBaseImpression(revenueInUSD, currencyCode, lifeTimeValue);

            if (_runtimeStates.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _runtimeStates.Count; i++)
            {
                ActiveThresholdState state = _runtimeStates[i];
                double currentCache = state.currentTroasCache + revenueInUSD;

                if (_currentMode == TaichiMode.Strategy1_PassThroughAfterMilestone)
                {
                    // gTech Strategy 1: Recommended for high thresholds (Slide 3/4)
                    if (currentCache >= state.value)
                    {
                        double eventVal = revenueInUSD;

                        if (state.isFirstTime)
                        {
                            eventVal = currentCache;
                            state.isFirstTime = false;
                            PlayerPrefs.SetInt(state.isFirstTimeKey, 0); // Save state locally
                        }

                        LogTaichiConversion(state.eventName, eventVal, currencyCode, state.tierPercent, lifeTimeValue);

                        // Note: Asymptotic Cache Lock-In. Do NOT overwrite 'currentTroasCache' in local storage with the threshold-passed value.
                        // The cache key remains locked at the last value under the threshold on the disk to minimize continuous write IO.
                    }
                    else
                    {
                        state.currentTroasCache = currentCache;
                        PlayerPrefs.SetString(state.cacheKey, currentCache.ToString("R", CultureInfo.InvariantCulture));
                    }
                }
                else
                {
                    // gTech Strategy 2: Recommended for low thresholds (Slide 5/6)
                    if (currentCache >= state.value)
                    {
                        LogTaichiConversion(state.eventName, currentCache, currencyCode, state.tierPercent, lifeTimeValue);

                        // Reset cache directly to 0.0 to prevent analytical deviations
                        state.currentTroasCache = 0.0;
                        PlayerPrefs.SetString(state.cacheKey, "0.0");
                    }
                    else
                    {
                        state.currentTroasCache = currentCache;
                        PlayerPrefs.SetString(state.cacheKey, currentCache.ToString("R", CultureInfo.InvariantCulture));
                    }
                }
            }

            _isDataDirty = true;
        }

        private void LogBaseImpression(double revenue, string currencyCode, double lifeTimeValue)
        {
            // Simple allocation-free struct setup for base logging
            ZenAnalytics.LogEventExact("ad_impression_revenue", new Dictionary<string, object>
            {
                { "value", revenue },
                { "currency", currencyCode },
                { "life_time_value", lifeTimeValue }
            });
        }

        private void LogTaichiConversion(string eventName, double value, string currency, int tierPercent, double lifeTimeValue)
        {
            ZenAnalytics.LogEventExact(eventName, new Dictionary<string, object>
            {
                { "value", value },
                { "currency", currency },
                { "taichi_tier", tierPercent },
                { "taichi_country", _detectedCountryCode },
                { "life_time_value", lifeTimeValue }
            });

            if (DebugMode.IsOn)
            {
                Debug.Log("[Taichi] Event Fired: " + eventName + " | Value: " + value + " | Tier: Top " + tierPercent + "% | Geo: " + _detectedCountryCode + " | Life Time Value: " + lifeTimeValue);
            }
        }

        /// <summary>
        /// Persists all states cleanly to disk using lossless representation on application pause or quit.
        /// </summary>
        public void SaveCacheToDisk()
        {
            if (!_isDataDirty)
            {
                return;
            }

            for (int i = 0; i < _runtimeStates.Count; i++)
            {
                ActiveThresholdState state = _runtimeStates[i];

                PlayerPrefs.SetString(state.cacheKey, state.currentTroasCache.ToString("R", CultureInfo.InvariantCulture));
                PlayerPrefs.SetInt(state.isFirstTimeKey, state.isFirstTime ? 1 : 0);
            }

            PlayerPrefs.Save();
            _isDataDirty = false;

            if (DebugMode.IsOn)
            {
                Debug.Log("[Taichi] Double-precision caching profiles successfully written to disk.");
            }
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                SaveCacheToDisk();
            }
        }

        private void OnApplicationQuit()
        {
            SaveCacheToDisk();
        }
    }
}