// One display position ("slot"), parsed from the JSON of the Unity AdZativeSlotConfig.ToJson() (Android: core/SlotConfig).
// A slot takes ads from 1..N ad unit IDs (pools) and renders them with one layout, in a Canvas rect or at an anchor.

#import "AdZativeFormat.h"

NS_ASSUME_NONNULL_BEGIN

/// PRIORITY: every ID preloaded, the highest-priority ready ad is shown (max show rate).
/// FALLBACK: only the first ID preloads; the next ID is used while the previous ones fail (least waste).
/// ROUND_ROBIN: alternate IDs on every show / refresh.
typedef NS_ENUM(NSInteger, AdZativeSourcePolicy) {
    AdZativeSourcePolicyPriority,
    AdZativeSourcePolicyFallback,
    AdZativeSourcePolicyRoundRobin,
};

/// Inline slots and VIDEO ads. ALLOW: like any ad. GATE: hold it and emit "video_gate"; Unity answers with
/// acceptVideo / rejectVideo. BLOCK: only non-video ads.
typedef NS_ENUM(NSInteger, AdZativeVideoMode) {
    AdZativeVideoModeAllow,
    AdZativeVideoModeGate,
    AdZativeVideoModeBlock,
};

/// MANUAL: always the configured layout. AUTO: per ad, the built-in style of the format that shows the most of its assets.
typedef NS_ENUM(NSInteger, AdZativeLayoutMode) {
    AdZativeLayoutModeManual,
    AdZativeLayoutModeAuto,
};

/// Fullscreen ✕ / "Next ›". AUTO: the plugin's timing (AdZativeWatchTime). SHOW: available now. HIDE: never shown, the
/// host drives the show (nextFullscreen / closeFullscreen). LOCKED: shown but inert, taps emit close_blocked / next_blocked.
typedef NS_ENUM(NSInteger, AdZativeButtonMode) {
    AdZativeButtonModeAuto,
    AdZativeButtonModeShow,
    AdZativeButtonModeHide,
    AdZativeButtonModeLocked,
};

// Enum value from a config string (case-insensitive); the default when missing or unknown.
AdZativeSourcePolicy AdZativeParseSourcePolicy(NSString *_Nullable name, AdZativeSourcePolicy fallback);
AdZativeVideoMode AdZativeParseVideoMode(NSString *_Nullable name, AdZativeVideoMode fallback);
AdZativeLayoutMode AdZativeParseLayoutMode(NSString *_Nullable name, AdZativeLayoutMode fallback);
AdZativeButtonMode AdZativeParseButtonMode(NSString *_Nullable name, AdZativeButtonMode fallback);

NSString *AdZativeSourcePolicyName(AdZativeSourcePolicy policy);
NSString *AdZativeLayoutModeName(AdZativeLayoutMode mode);

@interface AdZativeSlotConfig : NSObject

@property (nonatomic, readonly, copy) NSString *slotId;
@property (nonatomic, readonly) AdZativeFormat format;
/// Without empty and duplicate IDs, in priority order.
@property (nonatomic, readonly, copy) NSArray<NSString *> *adUnitIds;
@property (nonatomic, readonly) AdZativeSourcePolicy sourcePolicy;
@property (nonatomic, readonly, copy) NSString *layout;
@property (nonatomic, readonly) NSInteger cacheSize;
@property (nonatomic, readonly) int64_t ttlMs;
@property (nonatomic, readonly) BOOL startMuted;
@property (nonatomic, readonly, copy) NSString *mediaAspectRatio;
@property (nonatomic, readonly, copy) NSString *adChoicesPlacement;

// Inline refresh / swap.
@property (nonatomic, readonly) NSInteger refreshSec;
@property (nonatomic, readonly) NSInteger minDisplaySec;
@property (nonatomic, readonly) BOOL waitVideoEnd;
@property (nonatomic, readonly) BOOL preloadWhenHidden;

// Fullscreen timing, see AdZativeWatchTime.
@property (nonatomic, readonly) NSInteger imageFirstSec;
@property (nonatomic, readonly) NSInteger imageNextSec;
@property (nonatomic, readonly) NSInteger closeAfterSec;
@property (nonatomic, readonly) NSInteger videoMaxSec;
@property (nonatomic, readonly) NSInteger podRatio;
@property (nonatomic, readonly) BOOL showCountdown;
@property (nonatomic, readonly) BOOL autoShowOnForeground;
@property (nonatomic, readonly) NSInteger minIntervalSec;

// Pods (interstitial / rewarded): N ads in a row, one per distinct ad unit ID.
@property (nonatomic, readonly) NSInteger podSize;
@property (nonatomic, readonly) BOOL podCloseOnEveryAd;

// Media and layout.
@property (nonatomic, readonly) AdZativeVideoMode videoMode;
@property (nonatomic, readonly) AdZativeLayoutMode layoutMode;
@property (nonatomic, readonly) BOOL mediaCrop;

/// Host-driven loading (zen-unity Base waterfall): no automatic preload / refill, ads load only on loadUnit.
@property (nonatomic, readonly) BOOL manualLoad;
/// Inline: top margin (pt) of the ad_container view set by the host; < 0 keeps the layout's own.
@property (nonatomic, readonly) NSInteger containerMarginTopDp;

/// nil and an error message when the JSON is not an object, slotId is missing, the format is unknown or adUnitIds is empty.
+ (nullable instancetype)configWithJson:(NSString *)json error:(NSString *_Nullable *_Nullable)error;

- (instancetype)init NS_UNAVAILABLE;

/// Ads are interchangeable between slots only when the request shape matches.
- (NSString *)poolKey:(NSString *)adUnitId;

@end

NS_ASSUME_NONNULL_END
