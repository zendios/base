// Owns every AdZativePool (Android: core/PoolRegistry). Pools are shared: two slots listing the same ad unit (with the
// same request shape) draw from one cache, so the plugin never holds two copies of the same inventory.
//
// Demand model: each slot declares how many ads it wants preloaded from each pool; pool target = sum of the demands
// (max 3). The global cap bounds cached + in-flight ads.

#import "AdZativePool.h"

NS_ASSUME_NONNULL_BEGIN

@protocol AdZativePoolListener <NSObject>
- (void)onPoolChanged:(AdZativePool *)pool;
@end

@interface AdZativePoolRegistry : NSObject

- (instancetype)initWithSink:(AdZativeSink)sink NS_DESIGNATED_INITIALIZER;
- (instancetype)init NS_UNAVAILABLE;

/// Upper bound of cached + in-flight ads across all pools (memory guard). Default 6, at least 1.
- (void)setMaxCachedAds:(NSInteger)count;

/// The device got a network again: failed pools ask again now instead of waiting up to 64 s.
- (void)onNetworkAvailable;

- (AdZativePool *)obtain:(AdZativeSlotConfig *)config adUnitId:(NSString *)adUnitId;

/// Slot slotId wants count ads preloaded in pool (0 = none).
- (void)setDemand:(AdZativePool *)pool slotId:(NSString *)slotId count:(NSInteger)count;

- (void)clearDemand:(NSString *)slotId;

/// Room left under the global cap.
- (BOOL)canLoad;

- (void)addListener:(id<AdZativePoolListener>)listener;
- (void)removeListener:(id<AdZativePoolListener>)listener;

/// Called by a pool after its content changed: listeners first, then the other pools retry (freed capacity).
- (void)notifyPoolChanged:(AdZativePool *)pool;

- (NSInteger)totalReady;

- (void)destroyAll;

@end

NS_ASSUME_NONNULL_END
