#import "AdZativePoolRegistry.h"

@implementation AdZativePoolRegistry
{
    AdZativeSink _sink;
    NSMutableDictionary<NSString *, AdZativePool *> *_pools;
    NSMutableDictionary<NSString *, NSMutableDictionary<NSString *, NSNumber *> *> *_demand;
    NSMutableArray<id<AdZativePoolListener>> *_listeners;
    NSInteger _maxCachedAds;
}

- (instancetype)initWithSink:(AdZativeSink)sink
{
    if ((self = [super init]))
    {
        _sink = [sink copy];
        _pools = [NSMutableDictionary dictionary];
        _demand = [NSMutableDictionary dictionary];
        _listeners = [NSMutableArray array];
        _maxCachedAds = 6;
    }
    return self;
}

- (void)setMaxCachedAds:(NSInteger)count
{
    _maxCachedAds = MAX(1, count);
}

- (void)onNetworkAvailable
{
    for (AdZativePool *pool in _pools.allValues)
        [pool resetBackoff];
}

- (AdZativePool *)obtain:(AdZativeSlotConfig *)config adUnitId:(NSString *)adUnitId
{
    NSString *key = [config poolKey:adUnitId];
    AdZativePool *pool = _pools[key];
    if (pool == nil)
    {
        pool = [[AdZativePool alloc] initWithKey:key adUnitId:adUnitId shape:config sink:_sink registry:self];
        _pools[key] = pool;
    }
    return pool;
}

- (void)setDemand:(AdZativePool *)pool slotId:(NSString *)slotId count:(NSInteger)count
{
    NSMutableDictionary<NSString *, NSNumber *> *demand = _demand[pool.key];
    if (demand == nil)
    {
        demand = [NSMutableDictionary dictionary];
        _demand[pool.key] = demand;
    }
    if (count <= 0)
        [demand removeObjectForKey:slotId];
    else
        demand[slotId] = @(count);
    NSInteger sum = [self sumOf:demand];
    [pool setTargetSize:MIN(3, sum)];
    if (sum > 0)
        [pool fill];
}

- (void)clearDemand:(NSString *)slotId
{
    [_demand enumerateKeysAndObjectsUsingBlock:^(NSString *key, NSMutableDictionary<NSString *, NSNumber *> *demand, BOOL *stop) {
        if (demand[slotId] == nil)
            return;
        [demand removeObjectForKey:slotId];
        [self->_pools[key] setTargetSize:MIN(3, [self sumOf:demand])];
    }];
}

- (NSInteger)sumOf:(NSDictionary<NSString *, NSNumber *> *)demand
{
    NSInteger sum = 0;
    for (NSNumber *count in demand.allValues)
        sum += count.integerValue;
    return sum;
}

- (BOOL)canLoad
{
    NSInteger total = 0;
    for (AdZativePool *pool in _pools.allValues)
        total += [pool readyCount] + [pool inFlight];
    return total < _maxCachedAds;
}

- (void)addListener:(id<AdZativePoolListener>)listener
{
    if (![_listeners containsObject:listener])
        [_listeners addObject:listener];
}

- (void)removeListener:(id<AdZativePoolListener>)listener
{
    [_listeners removeObject:listener];
}

- (void)notifyPoolChanged:(AdZativePool *)pool
{
    for (id<AdZativePoolListener> listener in [_listeners copy])
        [listener onPoolChanged:pool];
    // A slot may have freed capacity under the global cap: let starved pools retry.
    for (AdZativePool *other in _pools.allValues)
    {
        if (other != pool)
            [other fill];
    }
}

- (NSInteger)totalReady
{
    NSInteger total = 0;
    for (AdZativePool *pool in _pools.allValues)
        total += [pool readyCount];
    return total;
}

- (void)destroyAll
{
    for (AdZativePool *pool in _pools.allValues)
        [pool destroy];
    [_pools removeAllObjects];
    [_demand removeAllObjects];
}

@end
