using System;
using System.Collections.Generic;

namespace Base.Ads
{
    /// <summary>The five native presentations. Names must match the Java AdFormat enum.</summary>
    public enum AdZativeFormat { BANNER, MREC, INTERSTITIAL, APP_OPEN, REWARDED }

    /// <summary>Anchor for code-positioned inline slots (values match InlineSlotPresenter.POS_*).</summary>
    public enum AdZativePosition { Top = 0, Bottom = 1, TopLeft = 2, TopRight = 3, BottomLeft = 4, BottomRight = 5, Center = 6, Custom = 7 }

    /// <summary>
    /// How a slot with several ad unit IDs picks its source.
    /// Priority: every ID preloaded, show the first ready (highest show rate, more requests).
    /// Fallback: preload only ID #1; the next ID loads only while the previous one fails (least waste).
    /// RoundRobin: alternate IDs on every show/refresh ("load the next ad with a new ID").
    /// </summary>
    public enum AdZativeSourcePolicy { Priority, Fallback, RoundRobin }

    /// <summary>
    /// Built-in layout of the format, or a custom Android layout name. Built-ins: banner zen_banner (portrait +
    /// landscape), MREC zen_mrec, app open zen_appopen (one style each); interstitial and rewarded:
    /// Style1 = zen_inter_media_top, Style2 = zen_inter_media_center. A style the format doesn't have = Style1.
    /// </summary>
    public enum AdZativeLayoutStyle { Style1 = 1, Style2 = 2, Custom = 0 }

    /// <summary>
    /// Manual: always the configured layout. Auto: per ad, the built-in style of the format that shows the
    /// most of that ad's assets (icon, rating, store, price, advertiser, body, media); ties keep the configured one.
    /// Both modes report "layout_selected" with the assets the chosen layout could not show.
    /// </summary>
    public enum AdZativeLayoutMode { Manual, Auto }

    /// <summary>
    /// Inline slots and VIDEO ads (a banner is too small for video: MediaView must be >= 120x120dp).
    /// Allow: show like any ad. Gate: hold it and raise OnVideoGate — resize the slot then AcceptVideo(), or
    /// RejectVideo() (destroys it, shows the next non-video ad). Block: never take video ads for this slot.
    /// </summary>
    public enum AdZativeVideoMode { Allow, Gate, Block }

    /// <summary>Fit keeps the whole creative visible (Meta forbids cropping); Crop fills the box (A/B only).</summary>
    public enum AdZativeMediaScale { Fit, Crop }

    /// <summary>What the loaded ad contains (from the native layer's classification).</summary>
    public enum AdZativeMediaType { None, Image, Video }

    /// <summary>
    /// Full-screen timing (interstitial, app open, rewarded), one per placement / prefab; Remote Config can overwrite it
    /// for A/B tests (same field names in JSON).
    /// Image ad: countdown imageFirstSec for the first (or only) ad, imageNextSec for every later ad of a pod; ✕ / Next
    /// at closeAfterSec (never later than the countdown). e.g. pod 2: 10s (✕ at 5s), then 5s (✕ at 5s).
    /// Video ad: countdown = video length, the whole show within videoMaxSec, every later ad at most 1/podRatio of the
    /// previous one; ✕ / Next (rewarded: the reward) when the countdown or the video ends. e.g. pod 2 of long videos:
    /// 20s then 10s. Rewarded: leaving before the reward (no reward) is possible after closeAfterSec.
    /// </summary>
    [Serializable]
    public class AdZativeTiming
    {
        [UnityEngine.Tooltip("Image ad: countdown of the first (or only) ad of a show, seconds.")]
        public int imageFirstSec = 10;
        [UnityEngine.Tooltip("Image ad: countdown of every later ad of a pod, seconds.")]
        public int imageNextSec = 5;
        [UnityEngine.Tooltip("Image ad: ✕ / Next / \"Continue\" appear after this many seconds (never later than the countdown). Rewarded: leaving early without the reward.")]
        public int closeAfterSec = 5;
        [UnityEngine.Tooltip("Video ad: the countdown is the video length, at most this many seconds for the whole show.")]
        public int videoMaxSec = 30;
        [UnityEngine.Tooltip("Pods: every later ad runs at most 1/podRatio of the previous one (2 = the first ad twice as long).")]
        public int podRatio = 2;
        [UnityEngine.Tooltip("Countdown chip (\"5s\") on the ad.")]
        public bool showCountdown = true;

        public AdZativeTiming Clone() => (AdZativeTiming)MemberwiseClone();
    }

    /// <summary>One display position. Serialised to JSON for the native layer.</summary>
    [Serializable]
    public class AdZativeSlotConfig
    {
        public string slotId = "slot";
        public AdZativeFormat format = AdZativeFormat.MREC;
        public List<string> adUnitIds = new List<string> { AdZativeTestIds.NativeVideo };
        public AdZativeSourcePolicy sourcePolicy = AdZativeSourcePolicy.Fallback;
        public AdZativeLayoutStyle layoutStyle = AdZativeLayoutStyle.Style1;
        public string customLayout = "";            // Android layout name when layoutStyle == Custom

        public int cacheSize = 1;                   // ads kept ready per ID (1..3)
        public int ttlSec = 3300;                   // AdMob & Meta ads expire at 60 min: drop them at 55
        public bool startMuted = true;
        public string mediaAspectRatio = "ANY";      // ANY | LANDSCAPE | PORTRAIT | SQUARE
        public string adChoicesPlacement = "";       // empty = TOP_RIGHT inline, TOP_LEFT fullscreen

        public int refreshSec = 0;                  // inline: swap to a preloaded ad every N s (>=30, 0 = off)
        public int minDisplaySec = 30;              // inline: never swap before this
        public bool waitVideoEnd = true;            // inline: don't cut a playing video
        public bool preloadWhenHidden = true;       // keep one ad ready even while the slot is hidden

        public AdZativeTiming timing = new AdZativeTiming();   // fullscreen: countdown, ✕ / Next, video caps
        public bool autoShowOnForeground = false;   // app open
        public int minIntervalSec = 30;             // app open
        public bool preloadOnRegister = true;

        // Pods (INTERSTITIAL / REWARDED): N ads in a row, one per distinct ad unit ID ("Ad 1 of N").
        // Set it here (not only per call) so every ID of the pod stays preloaded.
        public int podSize = 1;                     // 1..3
        public bool podCloseOnEveryAd = false;      // A/B variant: "✕" on every ad of the pod, not only the last

        public AdZativeVideoMode videoMode = AdZativeVideoMode.Allow;     // inline only
        public AdZativeLayoutMode layoutMode = AdZativeLayoutMode.Manual;
        public AdZativeMediaScale mediaScale = AdZativeMediaScale.Fit;

        /// <summary>Host-driven loading: no automatic preload / refill; ads load only via AdZativeSDK.LoadUnit
        /// (used when the host app runs its own waterfall, e.g. zen-unity Base).</summary>
        public bool manualLoad = false;

        /// <summary>Built-in layouts of a format (style 1..n), same list as AdFormat on the Android side.</summary>
        public static string[] BuiltInLayouts(AdZativeFormat f)
        {
            switch (f)
            {
                case AdZativeFormat.BANNER: return new[] { "zen_banner" };
                case AdZativeFormat.MREC: return new[] { "zen_mrec" };
                case AdZativeFormat.APP_OPEN: return new[] { "zen_appopen" };
                default: return new[] { "zen_inter_media_top", "zen_inter_media_center" };   // interstitial + rewarded
            }
        }

        public static int StyleCount(AdZativeFormat f) => BuiltInLayouts(f).Length;

        public string LayoutName
        {
            get
            {
                if (layoutStyle == AdZativeLayoutStyle.Custom && !string.IsNullOrEmpty(customLayout)) return customLayout;
                var list = BuiltInLayouts(format);
                int n = (int)layoutStyle;
                return n >= 1 && n <= list.Length ? list[n - 1] : list[0];
            }
        }

        public bool IsFullscreen => format == AdZativeFormat.INTERSTITIAL || format == AdZativeFormat.APP_OPEN || format == AdZativeFormat.REWARDED;

        public AdZativeSlotConfig Clone()
        {
            var c = (AdZativeSlotConfig)MemberwiseClone();
            c.adUnitIds = new List<string>(adUnitIds);
            c.timing = timing.Clone();
            return c;
        }

        internal string ToJson()
        {
            var dto = new Dto
            {
                slotId = slotId, format = format.ToString(), adUnitIds = new List<string>(adUnitIds),
                sourcePolicy = sourcePolicy == AdZativeSourcePolicy.RoundRobin ? "ROUND_ROBIN" : sourcePolicy.ToString().ToUpperInvariant(),
                layout = LayoutName, cacheSize = cacheSize, ttlSec = ttlSec, startMuted = startMuted,
                mediaAspectRatio = mediaAspectRatio, refreshSec = refreshSec, minDisplaySec = minDisplaySec,
                waitVideoEnd = waitVideoEnd, preloadWhenHidden = preloadWhenHidden,
                imageFirstSec = timing.imageFirstSec, imageNextSec = timing.imageNextSec, closeAfterSec = timing.closeAfterSec,
                videoMaxSec = timing.videoMaxSec, podRatio = timing.podRatio, showCountdown = timing.showCountdown,
                autoShowOnForeground = autoShowOnForeground, minIntervalSec = minIntervalSec,
                podSize = podSize, podCloseOnEveryAd = podCloseOnEveryAd, videoMode = videoMode.ToString().ToUpperInvariant(),
                layoutMode = layoutMode.ToString().ToUpperInvariant(), mediaScale = mediaScale.ToString().ToUpperInvariant(),
                manualLoad = manualLoad
            };
            string json = UnityEngine.JsonUtility.ToJson(dto);
            if (!string.IsNullOrEmpty(adChoicesPlacement))
                json = json.TrimEnd('}') + ",\"adChoicesPlacement\":\"" + adChoicesPlacement + "\"}";
            return json;
        }

        [Serializable]
        private class Dto
        {
            public string slotId, format, sourcePolicy, layout, mediaAspectRatio, videoMode, layoutMode, mediaScale;
            public List<string> adUnitIds;
            public bool startMuted, waitVideoEnd, preloadWhenHidden, autoShowOnForeground, podCloseOnEveryAd, manualLoad, showCountdown;
            public int cacheSize, ttlSec, refreshSec, minDisplaySec, imageFirstSec, imageNextSec, closeAfterSec, videoMaxSec, podRatio, minIntervalSec, podSize;
        }
    }

    /// <summary>Google's public test IDs. Never ship them; never test with production IDs.</summary>
    public static class AdZativeTestIds
    {
        public const string AndroidAppId = "ca-app-pub-3940256099942544~3347511713";
        public const string Native = "ca-app-pub-3940256099942544/2247696110";
        public const string NativeVideo = "ca-app-pub-3940256099942544/1044960115";
    }

    /// <summary>Superset of every payload field the native layer sends (JsonUtility-friendly).</summary>
    [Serializable]
    public class AdZativePayload
    {
        public long ts;
        public string ad_unit;
        public string pool;
        public int source = -1;
        public string layout;
        public long latency_ms;
        public int attempt;
        public int ready_count;
        public int code;
        public string message;
        public string reason;
        public string used;
        public long retry_in_ms;
        public bool has_video;
        public float aspect;
        public bool ready;
        public long value_micros;
        public string currency;
        public string precision;
        public int x, y, width, height, screen_height;
        public int width_dp, height_dp;
        public long prev_display_ms;
        public bool rewarded;
        public long duration_ms;
        public long watched_ms;
        public bool muted;
        public float duration_s;
        public bool app_open_allowed;
        public string format;
        public string policy;
        public int ids;
        // ad classification (loaded / ad_ready / video_gate / shown / discarded)
        public string media_type;
        public float video_duration_s;
        public string network;
        public string adapter;
        public bool has_body, has_cta, has_icon, has_stars, has_store, has_price, has_advertiser;
        public float star_rating;
        public string assets;
        public int asset_count;
        // layout_selected
        public string mode;
        public int score;
        public string missing_assets;
        // video gate / replace
        public string trigger;
        public bool allow_video;
        public long waited_ms;
        // pods
        public int pod_index;
        public int pod_size;
        public int requested;
        public int ads_watched;
        public int total_sec;
        public int from_index;
        public long shown_ms;
        // GetPodInfo per-ad
        public int watch_sec;
        public bool duration_known;

        public AdZativeAdInfo ToAdInfo() => new AdZativeAdInfo(this);
    }

    /// <summary>What a loaded ad contains — raised with OnAdReady / OnVideoGate and in GetPodInfo.</summary>
    public sealed class AdZativeAdInfo
    {
        public readonly string AdUnitId;
        public readonly int Source;
        public readonly AdZativeMediaType Media;
        public readonly float VideoDurationSec;   // 0 = unknown yet (video metadata still loading) or not a video
        public readonly float Aspect;
        public readonly string Network;           // e.g. "AdMob Network", "Meta Audience Network"
        public readonly bool HasBody, HasCta, HasIcon, HasStars, HasStore, HasPrice, HasAdvertiser;
        public readonly float StarRating;
        public readonly int AssetCount;

        public bool IsVideo => Media == AdZativeMediaType.Video;

        internal AdZativeAdInfo(AdZativePayload p)
        {
            AdUnitId = p.ad_unit; Source = p.source;
            Media = p.media_type == "VIDEO" ? AdZativeMediaType.Video : p.media_type == "IMAGE" ? AdZativeMediaType.Image : AdZativeMediaType.None;
            VideoDurationSec = p.video_duration_s; Aspect = p.aspect; Network = p.network;
            HasBody = p.has_body; HasCta = p.has_cta; HasIcon = p.has_icon; HasStars = p.has_stars;
            HasStore = p.has_store; HasPrice = p.has_price; HasAdvertiser = p.has_advertiser;
            StarRating = p.star_rating; AssetCount = p.asset_count;
        }

        public override string ToString() =>
            $"{Media}{(IsVideo ? $" {VideoDurationSec:F0}s" : "")} {Network} assets={AssetCount} aspect={Aspect:F2}";
    }

    /// <summary>Snapshot before showing a (pod) fullscreen ad — for the game's own "watch N ads (~T s)?" UI.</summary>
    [Serializable]
    public class AdZativePodInfo
    {
        public string slot;
        public int requested;     // pod size asked for
        public int ready;         // ads that would be shown now (<= requested; one per distinct ad unit ID)
        public int total_sec;     // sum of the countdowns of the ads (see AdZativeTiming)
        public List<AdZativePayload> ads = new List<AdZativePayload>();

        public bool IsReady => ready > 0;
    }

    /// <summary>One lifecycle event from the native layer, already on Unity's main thread.</summary>
    public readonly struct AdZativeEvent
    {
        /// <summary>Slot id ("" for pool-level / global events such as load_request).</summary>
        public readonly string SlotId;
        public readonly string Name;
        public readonly string Json;
        private readonly AdZativePayload _payload;

        public AdZativeEvent(string slotId, string name, string json)
        {
            SlotId = slotId;
            Name = name;
            Json = json;
            _payload = null;
            try { _payload = UnityEngine.JsonUtility.FromJson<AdZativePayload>(json); } catch { }
            if (_payload == null) _payload = new AdZativePayload();
        }

        public AdZativePayload Payload => _payload;
        public override string ToString() => $"[{SlotId}] {Name} {Json}";
    }
}
