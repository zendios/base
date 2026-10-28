// Cache of ready ads for ONE ad unit (+ request shape), shared by every slot that lists this ID (Android: core/AdPool).
//
//  - at most one request in flight, fills up to targetSize;
//  - respects the global cap of AdZativePoolRegistry, so video ads cannot blow up memory;
//  - TTL: ads older than ttl are dropped (Google: reload after one hour);
//  - exponential backoff (2..64 s) after a failed request: fill (preload) waits until it ends, also when another pool
//    changes; reset when the network comes back. requestOne (host waterfall) is never gated.
//
// Main thread only.

#import "AdZativeAdInfo.h"
#import "AdZativeSlotConfig.h"

NS_ASSUME_NONNULL_BEGIN

@class AdZativePool;
@class AdZativePoolRegistry;

/// Key of the per-ad observer (AdZativeEventBridge), kept alive by the ad itself.
extern const void *const AdZativeObserverKey;

/// Releases an ad for good (Android NativeAd.destroy): no callback of it reaches the plugin any more.
void AdZativeDestroyAd(GADNativeAd *_Nullable ad);

@interface AdZativeCachedAd : NSObject

@property (nonatomic, readonly) GADNativeAd *ad;
@property (nonatomic, readonly, copy) NSString *adUnitId;
@property (nonatomic, readonly) AdZativeAdInfo *info;
@property (nonatomic, readonly) int64_t loadedAtMs;
@property (nonatomic, weak, nullable) AdZativePool *owner;

- (instancetype)init NS_UNAVAILABLE;

/// Time since load (the TTL counts from here).
- (int64_t)ageMs;

@end

typedef BOOL (^AdZativeAdFilter)(AdZativeCachedAd *cached);

/// Only non-video ads.
AdZativeAdFilter AdZativeNoVideoFilter(void);

@interface AdZativePool : NSObject

@property (nonatomic, readonly, copy) NSString *key;
@property (nonatomic, readonly, copy) NSString *adUnitId;
@property (nonatomic, readonly, getter=isLoading) BOOL loading;
@property (nonatomic, readonly) BOOL lastLoadFailed;
@property (nonatomic, readonly) NSInteger lastErrorCode;
@property (nonatomic, readonly, copy) NSString *lastErrorMessage;

- (instancetype)initWithKey:(NSString *)key
                   adUnitId:(NSString *)adUnitId
                      shape:(AdZativeSlotConfig *)shape
                       sink:(AdZativeSink)sink
                   registry:(AdZativePoolRegistry *)registry NS_DESIGNATED_INITIALIZER;
- (instancetype)init NS_UNAVAILABLE;

- (NSInteger)readyCount;

/// 1 while a request is in flight.
- (NSInteger)inFlight;

/// 0..3; 0 pauses preloading.
- (void)setTargetSize:(NSInteger)size;

/// Requests until readyCount == targetSize. Idempotent; safe to call often.
- (void)fill;

/// One request on demand (host-driven waterfall), whatever the preload demand. A cached ad answers at once with "loaded"
/// (cached = true); a request already in flight answers when it completes.
/// @param blockVideo the requesting slot cannot show video ads: cached video ads are dropped and a new request is made.
- (void)requestOne:(BOOL)blockVideo;

/// Ends the current backoff (network came back) and asks again if the preload still wants ads.
- (void)resetBackoff;

/// Takes the oldest fresh ad matching filter (nil = any); ownership moves to the caller, the pool refills.
- (nullable AdZativeCachedAd *)take:(nullable AdZativeAdFilter)filter;

/// The ad take: would return, without taking it.
- (nullable AdZativeCachedAd *)peek:(nullable AdZativeAdFilter)filter;

- (BOOL)has:(nullable AdZativeAdFilter)filter;

/// Returns an ad that was taken but never shown (a pod ad never reached, a rejected gate) to the FRONT of the cache.
/// Expired ads are dropped.
- (void)giveBack:(AdZativeCachedAd *)cached;

- (void)destroy;

@end

NS_ASSUME_NONNULL_END
