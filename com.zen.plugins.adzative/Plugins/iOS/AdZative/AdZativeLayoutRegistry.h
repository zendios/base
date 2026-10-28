// Resolves a layout by NAME so Unity can pick a layout per slot (Android: render/LayoutRegistry): the built-in layouts
// (AdZativeLayouts) or a .xib the game adds to the app bundle (UINib named like the layout).
//
// View IDs are the Android R.id names, set as accessibilityIdentifier. Contract checked at inflate time (falls back to
// the format's style 1 and reports "layout_invalid"):
//   ad_native_view  GADNativeAdView containing every asset
//   ad_headline     UILabel
//   ad_attribution  the static "Ad" badge
//   ad_media        GADMediaView, required for MREC and the fullscreen formats

#import <GoogleMobileAds/GoogleMobileAds.h>

#import "AdZativeAdInfo.h"
#import "AdZativeFormat.h"

NS_ASSUME_NONNULL_BEGIN

// Ad assets (inside ad_native_view).
extern NSString *const AdZativeIdNativeView;
extern NSString *const AdZativeIdHeadline;
extern NSString *const AdZativeIdAttribution;
extern NSString *const AdZativeIdMedia;
extern NSString *const AdZativeIdBody;
extern NSString *const AdZativeIdCallToAction;
extern NSString *const AdZativeIdAppIcon;
extern NSString *const AdZativeIdAdvertiser;
extern NSString *const AdZativeIdStars;
extern NSString *const AdZativeIdStore;
extern NSString *const AdZativeIdPrice;
extern NSString *const AdZativeIdContainer;

// Fullscreen controls (OUTSIDE ad_native_view: AdMob forbids own click handlers within the native ad view).
extern NSString *const AdZativeIdCountdown;
extern NSString *const AdZativeIdClose;
extern NSString *const AdZativeIdNext;
extern NSString *const AdZativeIdPodLabel;
extern NSString *const AdZativeIdContinue;
extern NSString *const AdZativeIdContinueLabel;
extern NSString *const AdZativeIdHostAppIcon;
extern NSString *const AdZativeIdHostAppName;
extern NSString *const AdZativeIdTopBar;

// Markers (Android android:tag), set as restorationIdentifier.
/// On ad_advertiser: hidden when the ad has stars (small layouts show rating OR advertiser).
extern NSString *const AdZativeTagCompact;
/// On ad_app_icon: shown only when the ad has no media or the layout has no MediaView.
extern NSString *const AdZativeTagIconFallback;
/// On the view that wraps ad_media and its margins (built-in fullscreen layouts): hidden with the media.
extern NSString *const AdZativeTagMediaBox;
/// On the view that holds ad_media at the creative's aspect ratio (AdZativeMediaFrame).
extern NSString *const AdZativeTagMediaFrame;

/// View of a layout by its ID (accessibilityIdentifier), depth-first; nil if absent.
UIView *_Nullable AdZativeFindView(UIView *root, NSString *viewId);

@interface AdZativeInflated : NSObject

/// What goes on the overlay.
@property (nonatomic, readonly) UIView *root;
/// == root for the inline layouts.
@property (nonatomic, readonly) GADNativeAdView *adView;
@property (nonatomic, readonly, copy) NSString *name;
@property (nonatomic, readonly) BOOL fellBack;
@property (nonatomic, readonly, copy, nullable) NSString *problem;

- (instancetype)init NS_UNAVAILABLE;

- (nullable __kindof UIView *)viewWithId:(NSString *)viewId;

/// Releases the ad bound to the view (the view is leaving the screen).
- (void)unbind;

@end

@interface AdZativeLayoutRegistry : NSObject

/// The named layout, or the format's default layout when it is missing or breaks the contract (fellBack, problem).
/// @param landscape the landscape variant of a built-in layout (Android res/layout-land)
+ (AdZativeInflated *)inflate:(NSString *)name format:(AdZativeFormat)format landscape:(BOOL)landscape;

/// Assets the layout can display (built once, cached). Empty when the layout does not exist.
+ (NSSet<NSString *> *)coverage:(NSString *)name landscape:(BOOL)landscape;

/// Assets this ad has that the layout would not show (reported for A/B insight).
+ (NSArray<NSString *> *)missing:(NSString *)name info:(AdZativeAdInfo *)info landscape:(BOOL)landscape;

+ (NSInteger)score:(NSString *)name info:(AdZativeAdInfo *)info landscape:(BOOL)landscape;

/// AUTO: the built-in style of the format (or the configured layout) with the best asset coverage; ties keep configured.
+ (NSString *)autoSelect:(AdZativeFormat)format
                configured:(NSString *)configured
                      info:(AdZativeAdInfo *)info
                 landscape:(BOOL)landscape;

@end

NS_ASSUME_NONNULL_END
