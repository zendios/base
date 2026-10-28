using UnityEngine;

namespace Base.Ads
{
    /// <summary>
    /// This player's click-through rate per ad unit ID: clicks / shows, counted by Base (AdBase states Show and Click)
    /// and saved in PlayerPrefs. Min / Max come from AdConfig.adCtrMin / adCtrMax (Remote Config) and drive the
    /// full-screen ✕ / Next rules of AdZativeCloseTimer.
    /// </summary>
    public static class AdCtr
    {
        private const string ShowKey = "AdCtr_show_";
        private const string ClickKey = "AdCtr_click_";

        /// <summary>CTR at or below this: ✕ / Next locked AdZativeTiming.closeLockedSec after the countdown.</summary>
        public static float Min { get; set; } = 0.1f;
        /// <summary>CTR above this after a click on the ad: the next ad of the pod, or close, when the player comes back.</summary>
        public static float Max { get; set; } = 0.35f;

        public static void RecordShow(string adUnitId) => Add(ShowKey, adUnitId);
        public static void RecordClick(string adUnitId) => Add(ClickKey, adUnitId);

        /// <summary>Clicks / shows of this ID on this device; 0 before its first show.</summary>
        public static float Get(string adUnitId)
        {
            if (string.IsNullOrEmpty(adUnitId))
                return 0f;
            int shows = PlayerPrefs.GetInt(ShowKey + adUnitId, 0);
            return shows == 0 ? 0f : (float)PlayerPrefs.GetInt(ClickKey + adUnitId, 0) / shows;
        }

        private static void Add(string key, string adUnitId)
        {
            if (string.IsNullOrEmpty(adUnitId))
                return;
            PlayerPrefs.SetInt(key + adUnitId, PlayerPrefs.GetInt(key + adUnitId, 0) + 1);
            PlayerPrefs.Save();   // a click usually sends the app to the background
        }
    }
}
