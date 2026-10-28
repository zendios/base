#import "AdZativeEventBridge.h"

#import <objc/runtime.h>

#include <math.h>

/// "UNKNOWN" | "ESTIMATED" | "PUBLISHER_PROVIDED" | "PRECISE", the Android PrecisionType names read by Unity.
static NSString *PrecisionName(GADAdValuePrecision precision)
{
    switch (precision)
    {
        case GADAdValuePrecisionEstimated:
            return @"ESTIMATED";
        case GADAdValuePrecisionPublisherProvided:
            return @"PUBLISHER_PROVIDED";
        case GADAdValuePrecisionPrecise:
            return @"PRECISE";
        case GADAdValuePrecisionUnknown:
        default:
            return @"UNKNOWN";
    }
}

/// Delegate of one ad and its video. The SDK holds delegates weakly: the ad keeps this object alive (associated object).
@interface AdZativeAdObserver : NSObject <GADNativeAdDelegate, GADVideoControllerDelegate>
@property (nonatomic, copy) NSString *slotId;
@property (nonatomic, copy) NSString *adUnitId;
@property (nonatomic, copy) NSString *network;
@property (nonatomic, copy) AdZativeSink sink;
@property (nonatomic, copy, nullable) dispatch_block_t onVideoStart;
@property (nonatomic, copy, nullable) dispatch_block_t onVideoEnd;
@property (nonatomic, copy, nullable) AdZativeClickHook onClick;
@property (nonatomic) float duration;
@property (nonatomic) BOOL videoStarted;
@end

@implementation AdZativeAdObserver

- (void)emit:(NSString *)event
{
    [self emit:event payload:[NSMutableDictionary dictionary]];
}

- (void)emit:(NSString *)event payload:(NSMutableDictionary *)payload
{
    payload[@"ad_unit"] = self.adUnitId;
    self.sink(self.slotId, event, payload);
}

- (void)nativeAdDidRecordImpression:(GADNativeAd *)nativeAd
{
    AdZativeRunOnMain(^{
        [self emit:@"impression"];
    });
}

- (void)nativeAdDidRecordClick:(GADNativeAd *)nativeAd
{
    AdZativeRunOnMain(^{
        if (self.onClick != nil)
            self.onClick();
        [self emit:@"clicked"];
    });
}

// SDK-owned screens (click-through store page, expanded video).
- (void)nativeAdWillPresentScreen:(GADNativeAd *)nativeAd
{
    AdZativeRunOnMain(^{
        [self emit:@"sdk_fullscreen_opened"];
    });
}

- (void)nativeAdDidDismissScreen:(GADNativeAd *)nativeAd
{
    AdZativeRunOnMain(^{
        [self emit:@"sdk_fullscreen_closed"];
    });
}

// iOS has no "video start" callback: the first play is the start.
- (void)videoControllerDidPlayVideo:(GADVideoController *)videoController
{
    AdZativeRunOnMain(^{
        if (self.videoStarted)
        {
            [self emit:@"video_play"];
            return;
        }
        self.videoStarted = YES;
        if (self.onVideoStart != nil)
            self.onVideoStart();
        NSMutableDictionary *payload = [NSMutableDictionary dictionary];
        payload[@"duration_s"] = AdZativeNumber(self.duration);
        [self emit:@"video_start" payload:payload];
    });
}

- (void)videoControllerDidPauseVideo:(GADVideoController *)videoController
{
    AdZativeRunOnMain(^{
        [self emit:@"video_pause"];
    });
}

- (void)videoControllerDidEndVideoPlayback:(GADVideoController *)videoController
{
    AdZativeRunOnMain(^{
        if (self.onVideoEnd != nil)
            self.onVideoEnd();
        [self emit:@"video_end"];
    });
}

// Unity uses video_mute to duck the game music while the ad has sound.
- (void)videoControllerDidMuteVideo:(GADVideoController *)videoController
{
    [self emitMuted:YES];
}

- (void)videoControllerDidUnmuteVideo:(GADVideoController *)videoController
{
    [self emitMuted:NO];
}

- (void)emitMuted:(BOOL)muted
{
    AdZativeRunOnMain(^{
        NSMutableDictionary *payload = [NSMutableDictionary dictionary];
        payload[@"muted"] = @(muted);
        [self emit:@"video_mute" payload:payload];
    });
}

@end

@implementation AdZativeEventBridge

+ (void)attachAd:(GADNativeAd *)ad
          slotId:(NSString *)slotId
        adUnitId:(NSString *)adUnitId
            sink:(AdZativeSink)sink
    onVideoStart:(dispatch_block_t)onVideoStart
      onVideoEnd:(dispatch_block_t)onVideoEnd
         onClick:(AdZativeClickHook)onClick
{
    AdZativeAdObserver *observer = [[AdZativeAdObserver alloc] init];
    observer.slotId = slotId;
    observer.adUnitId = adUnitId;
    observer.network = [AdZativeAdInfo networkOfAd:ad];
    observer.sink = sink;
    observer.onVideoStart = onVideoStart;
    observer.onVideoEnd = onVideoEnd;
    observer.onClick = onClick;
    objc_setAssociatedObject(ad, AdZativeObserverKey, observer, OBJC_ASSOCIATION_RETAIN_NONATOMIC);
    ad.delegate = observer;

    __weak AdZativeAdObserver *weakObserver = observer;
    ad.paidEventHandler = ^(GADAdValue *value) {
        long long micros = llround(value.value.doubleValue * 1000000.0);
        NSString *currency = value.currencyCode;
        NSString *precision = PrecisionName(value.precision);
        AdZativeRunOnMain(^{
            AdZativeAdObserver *strongObserver = weakObserver;
            if (strongObserver == nil)
                return;
            NSMutableDictionary *payload = [NSMutableDictionary dictionary];
            payload[@"value_micros"] = @(micros);
            payload[@"currency"] = currency;
            payload[@"precision"] = precision;
            payload[@"network"] = strongObserver.network;
            [strongObserver emit:@"paid" payload:payload];
        });
    };

    GADMediaContent *content = ad.mediaContent;
    if (!content.hasVideoContent)
        return;
    // Read at attach time, like Android: often 0 here (metadata still loading); presenters read the live length.
    observer.duration = (float)content.duration;
    content.videoController.delegate = observer;
}

@end
