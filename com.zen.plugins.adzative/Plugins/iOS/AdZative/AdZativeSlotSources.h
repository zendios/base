// The ad unit list of one slot and its selection policy (Android: core/SlotSources). Decides which pools preload for
// this slot and which pool the next ad is taken from.

#import "AdZativePoolRegistry.h"

NS_ASSUME_NONNULL_BEGIN

@interface AdZativeTaken : NSObject

@property (nonatomic, readonly) AdZativeCachedAd *cached;
/// Index of the ad unit ID in the slot's list (0 = highest priority).
@property (nonatomic, readonly) NSInteger sourceIndex;

- (instancetype)initWithCached:(AdZativeCachedAd *)cached sourceIndex:(NSInteger)sourceIndex NS_DESIGNATED_INITIALIZER;
- (instancetype)init NS_UNAVAILABLE;

@end

@interface AdZativeSlotSources : NSObject

- (instancetype)initWithConfig:(AdZativeSlotConfig *)config registry:(AdZativePoolRegistry *)registry NS_DESIGNATED_INITIALIZER;
- (instancetype)init NS_UNAVAILABLE;

- (BOOL)hasReady;
- (BOOL)hasReady:(nullable AdZativeAdFilter)filter;

/// Turns preloading for this slot on / off and (re)applies the policy. Cheap; call on any change.
- (void)setPreload:(BOOL)on;
- (void)apply;

/// Host-driven load (manualLoad slots): ONE ad from adUnitId now, even without preload demand. Unknown IDs (e.g. test IDs
/// swapped in by the host) get a pool on the fly. The answer is a SLOT event: "unit_loaded" / "unit_failed".
/// @param blockVideo the slot cannot show video ads: a cached video ad never answers this request.
- (void)requestUnit:(NSString *)adUnitId blockVideo:(BOOL)blockVideo;

/// Call from the slot's onPoolChanged: answers a pending requestUnit with a slot-scoped event, so a slot never mistakes
/// another slot's fill of the same (shared) ad unit for its own.
- (void)resolvePending:(AdZativePool *)pool sink:(AdZativeSink)sink slotId:(NSString *)slotId;

/// Ownership of the ad moves to the caller; the pool refills automatically. filter nil = any ad.
- (nullable AdZativeTaken *)take:(nullable AdZativeAdFilter)filter;

/// The ad take: would return next, without taking it.
- (nullable AdZativeTaken *)peek:(nullable AdZativeAdFilter)filter;

/// Up to max ads: one per ad unit ID in priority order, then a second (third) round over the same IDs.
- (NSArray<AdZativeTaken *> *)takeDistinct:(NSInteger)max;

/// Peek variant of takeDistinct: (pod info before showing).
- (NSArray<AdZativeTaken *> *)peekDistinct:(NSInteger)max;

/// Puts an ad that was taken but never shown back into its pool.
- (void)giveBack:(nullable AdZativeTaken *)taken;

/// Drops this slot's preload demand in every pool.
- (void)releaseDemand;

@end

NS_ASSUME_NONNULL_END
