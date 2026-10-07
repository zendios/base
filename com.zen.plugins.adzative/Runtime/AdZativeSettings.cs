using System.Collections.Generic;
using UnityEngine;

namespace Base.Ads
{
    /// <summary>
    /// Project settings (Assets/AdZative/Resources/AdZativeSettings.asset).
    /// Standalone plugin use only (Base games do not use it: AdZative configures the plugin in code). Canvas slots are configured on their AdZativeSlot component;
    /// list here the slots you drive from code (fullscreen formats, anchored banners).
    /// </summary>
    public class AdZativeSettings : ScriptableObject
    {
        public const string ResourcePath = "AdZativeSettings";

        [Header("AdMob")]
        [Tooltip("AdMob Android App ID (ca-app-pub-xxx~yyy). Also written into the manifest for UMP.")]
        public string androidAppId = AdZativeTestIds.AndroidAppId;

        [Tooltip("The host app initialises MobileAds itself (consent + MobileAds.Initialize); the plugin never does. " +
                 "Call AdZativeSDK.Initialize only after that initialisation completed.")]
        public bool externalSdkInit = false;

        [Tooltip("Replace every ad unit ID with Google's test IDs (debug builds).")]
        public bool useTestAdUnits = true;

        [Tooltip("Upper bound of cached + in-flight native ads across all IDs (memory guard for video).")]
        [Range(1, 12)] public int maxCachedAds = 6;

        [Header("Game experience")]
        public bool duckAudioDuringFullscreen = true;
        public bool pauseTimeScaleDuringFullscreen = true;
        public bool routeBackKeyToAd = true;
        public bool verboseLogging = true;

        [Header("Code-driven slots")]
        public List<AdZativeSlotConfig> slots = new List<AdZativeSlotConfig>
        {
            new AdZativeSlotConfig { slotId = "home_banner", format = AdZativeFormat.BANNER, layoutStyle = AdZativeLayoutStyle.Style1,
                adUnitIds = new List<string> { AdZativeTestIds.Native }, refreshSec = 60 },
            new AdZativeSlotConfig { slotId = "level_end_inter", format = AdZativeFormat.INTERSTITIAL, layoutStyle = AdZativeLayoutStyle.Style1,
                adUnitIds = new List<string> { AdZativeTestIds.NativeVideo, AdZativeTestIds.Native }, sourcePolicy = AdZativeSourcePolicy.Fallback },
            new AdZativeSlotConfig { slotId = "app_open", format = AdZativeFormat.APP_OPEN, layoutStyle = AdZativeLayoutStyle.Style1,
                adUnitIds = new List<string> { AdZativeTestIds.Native }, timing = new AdZativeTiming { closeAfterSec = 0 }, autoShowOnForeground = true, minIntervalSec = 60 },
            new AdZativeSlotConfig { slotId = "revive_rewarded", format = AdZativeFormat.REWARDED, layoutStyle = AdZativeLayoutStyle.Style2,
                adUnitIds = new List<string> { AdZativeTestIds.NativeVideo, AdZativeTestIds.Native }, sourcePolicy = AdZativeSourcePolicy.Priority,
                startMuted = false, timing = new AdZativeTiming { imageFirstSec = 15, imageNextSec = 7 } },
        };
    }
}
