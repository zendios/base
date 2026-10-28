// Wires the SDK ad + video callbacks to the sink: impression, click, paid, sdk_fullscreen_*, video_*
// (Android: render/AdEventBridge). Every event carries ad_unit, so revenue / CTR split per slot x ad unit.

#import "AdZativePool.h"

NS_ASSUME_NONNULL_BEGIN

@interface AdZativeEventBridge : NSObject

/// Replaces any earlier attachment of the ad. Hooks run on the main thread before the event is emitted.
/// @param onVideoStart first play of the video (presenters: video length known now, a playing video is not cut)
/// @param onVideoEnd the video played to its end
/// @param onClick the user tapped the ad (it may leave the app)
+ (void)attachAd:(GADNativeAd *)ad
          slotId:(NSString *)slotId
        adUnitId:(NSString *)adUnitId
            sink:(AdZativeSink)sink
    onVideoStart:(nullable dispatch_block_t)onVideoStart
      onVideoEnd:(nullable dispatch_block_t)onVideoEnd
         onClick:(nullable AdZativeClickHook)onClick;

@end

NS_ASSUME_NONNULL_END
