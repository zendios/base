// Populates any AdZative layout (Android: render/NativeAdBinder). Every layout shares the same view IDs, so one binder
// serves banner, MREC, interstitial, app open and rewarded. Views a layout does not declare are skipped.
//
// Policy (AdMob native guidelines):
//  - the headline is required and always shown; other assets are hidden when the ad lacks them (no empty box);
//  - every asset view lives INSIDE the GADNativeAdView and is registered, so the SDK handles clicks and impressions:
//    no own click handlers on ad assets;
//  - the "Ad" attribution badge (ad_attribution) is static in every layout.

#import "AdZativeLayoutRegistry.h"

NS_ASSUME_NONNULL_BEGIN

@interface AdZativeNativeAdBinder : NSObject

/// @param info classification from load time: a NO-media ad (Meta Native Banner) hides the MediaView and registers the
///             icon instead (Meta records the impression on it)
/// @param crop aspect-fill instead of aspect-fit (A/B only: Meta forbids cropping media)
/// @return the MediaView used (nil when the layout has none or the ad has no media)
+ (nullable GADMediaView *)bindAdView:(GADNativeAdView *)adView
                                   ad:(GADNativeAd *)ad
                                 info:(AdZativeAdInfo *)info
                                 crop:(BOOL)crop;

@end

NS_ASSUME_NONNULL_END
