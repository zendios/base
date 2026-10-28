#import "AdZativeSlotSources.h"

@implementation AdZativeTaken

- (instancetype)initWithCached:(AdZativeCachedAd *)cached sourceIndex:(NSInteger)sourceIndex
{
    if ((self = [super init]))
    {
        _cached = cached;
        _sourceIndex = sourceIndex;
    }
    return self;
}

@end

@implementation AdZativeSlotSources
{
    AdZativeSlotConfig *_config;
    __weak AdZativePoolRegistry *_registry;
    NSMutableArray<AdZativePool *> *_pools;
    NSInteger _roundRobinNext;
    BOOL _wantsPreload;
    NSString *_pendingUnit;   // host-driven request waiting for its answer (resolvePending)
    BOOL _pendingBlockVideo;
}

- (instancetype)initWithConfig:(AdZativeSlotConfig *)config registry:(AdZativePoolRegistry *)registry
{
    if ((self = [super init]))
    {
        _config = config;
        _registry = registry;
        _pools = [NSMutableArray array];
        for (NSString *adUnitId in config.adUnitIds)
            [_pools addObject:[registry obtain:config adUnitId:adUnitId]];
    }
    return self;
}

- (BOOL)hasReady
{
    for (AdZativePool *pool in _pools)
    {
        if ([pool readyCount] > 0)
            return YES;
    }
    return NO;
}

- (BOOL)hasReady:(AdZativeAdFilter)filter
{
    if (filter == nil)
        return [self hasReady];
    for (AdZativePool *pool in _pools)
    {
        if ([pool has:filter])
            return YES;
    }
    return NO;
}

- (void)setPreload:(BOOL)on
{
    _wantsPreload = on;
    [self apply];
}

- (void)apply
{
    AdZativePoolRegistry *registry = _registry;
    NSInteger count = (NSInteger)_pools.count;
    for (NSInteger i = 0; i < count; i++)
    {
        NSInteger demand = _wantsPreload && !_config.manualLoad ? [self demandFor:i] : 0;
        [registry setDemand:_pools[(NSUInteger)i] slotId:_config.slotId count:demand];
    }
}

- (NSInteger)demandFor:(NSInteger)i
{
    // Pods show one ad per distinct ID in a row: the first podSize IDs must all be warm.
    if (_config.podSize > 1)
        return [self podDemandFor:i];
    switch (_config.sourcePolicy)
    {
        case AdZativeSourcePolicyPriority:
            return _config.cacheSize;   // every ID stays warm
        case AdZativeSourcePolicyRoundRobin:
            return i == _roundRobinNext % (NSInteger)_pools.count ? _config.cacheSize : 0;   // only the next ID in turn
        case AdZativeSourcePolicyFallback:
        default:
            // ID i preloads only while every higher-priority ID is failing / empty.
            for (NSInteger j = 0; j < i; j++)
            {
                AdZativePool *higher = _pools[(NSUInteger)j];
                if ([higher readyCount] > 0 || !higher.lastLoadFailed)
                    return 0;
            }
            return _config.cacheSize;
    }
}

/// Pods need podSize ads warm at once. The IDs that currently deliver ("live": an ad ready or no failure yet) share them,
/// in ID order: 1 live ID, pod 2 -> 2 ads in that ID; 2 live IDs -> 1 each. A failing ID among the first podSize keeps
/// its own demand so it retries (with backoff) and takes its share back once it fills again.
- (NSInteger)podDemandFor:(NSInteger)i
{
    NSMutableArray<NSNumber *> *live = [NSMutableArray array];
    for (NSInteger j = 0; j < (NSInteger)_pools.count; j++)
    {
        AdZativePool *pool = _pools[(NSUInteger)j];
        if ([pool readyCount] > 0 || !pool.lastLoadFailed)
            [live addObject:@(j)];
    }
    NSInteger used = MIN(_config.podSize, (NSInteger)live.count);
    NSUInteger found = [live indexOfObject:@(i)];
    NSInteger rank = found == NSNotFound ? -1 : (NSInteger)found;
    if (rank >= 0 && rank < used)
    {
        NSInteger share = _config.podSize / used + (rank < _config.podSize % used ? 1 : 0);
        return MAX(_config.cacheSize, share);
    }
    return i < _config.podSize ? _config.cacheSize : 0;
}

- (void)requestUnit:(NSString *)adUnitId blockVideo:(BOOL)blockVideo
{
    AdZativePool *pool = nil;
    for (AdZativePool *candidate in _pools)
    {
        if ([candidate.adUnitId isEqualToString:adUnitId])
        {
            pool = candidate;
            break;
        }
    }
    if (pool == nil)
    {
        pool = [_registry obtain:_config adUnitId:adUnitId];
        [_pools addObject:pool];
    }
    _pendingUnit = [adUnitId copy];   // before requestOne: a cached ad answers synchronously
    _pendingBlockVideo = blockVideo;
    [pool requestOne:blockVideo];
}

- (void)resolvePending:(AdZativePool *)pool sink:(AdZativeSink)sink slotId:(NSString *)slotId
{
    if (_pendingUnit == nil || ![pool.adUnitId isEqualToString:_pendingUnit] || ![_pools containsObject:pool])
        return;
    AdZativeCachedAd *cached = [pool peek:_pendingBlockVideo ? AdZativeNoVideoFilter() : nil];
    if (cached != nil)
    {
        _pendingUnit = nil;
        NSMutableDictionary *payload = [cached.info putInto:[NSMutableDictionary dictionary] ad:cached.ad];
        payload[@"ad_unit"] = pool.adUnitId;
        sink(slotId, @"unit_loaded", payload);
        return;
    }
    if (pool.isLoading)
        return;
    if (pool.lastLoadFailed)
    {
        _pendingUnit = nil;
        NSMutableDictionary *payload = [NSMutableDictionary dictionary];
        payload[@"ad_unit"] = pool.adUnitId;
        payload[@"code"] = @(pool.lastErrorCode);
        payload[@"message"] = pool.lastErrorMessage;
        sink(slotId, @"unit_failed", payload);
    }
    else if (_pendingBlockVideo && [pool has:nil])
    {
        _pendingUnit = nil;
        NSMutableDictionary *payload = [NSMutableDictionary dictionary];
        payload[@"ad_unit"] = pool.adUnitId;
        payload[@"code"] = @(-3);
        payload[@"message"] = @"video_blocked";
        sink(slotId, @"unit_failed", payload);
    }
}

- (AdZativeTaken *)take:(AdZativeAdFilter)filter
{
    NSInteger count = (NSInteger)_pools.count;
    BOOL roundRobin = _config.sourcePolicy == AdZativeSourcePolicyRoundRobin;
    AdZativeTaken *taken = nil;
    for (NSInteger k = 0; k < count && taken == nil; k++)
    {
        NSInteger i = roundRobin ? (_roundRobinNext + k) % count : k;
        AdZativeCachedAd *cached = [_pools[(NSUInteger)i] take:filter];
        if (cached != nil)
        {
            taken = [[AdZativeTaken alloc] initWithCached:cached sourceIndex:i];
            if (roundRobin)
                _roundRobinNext = (i + 1) % count;
        }
    }
    [self apply];
    return taken;
}

- (AdZativeTaken *)peek:(AdZativeAdFilter)filter
{
    NSInteger count = (NSInteger)_pools.count;
    BOOL roundRobin = _config.sourcePolicy == AdZativeSourcePolicyRoundRobin;
    for (NSInteger k = 0; k < count; k++)
    {
        NSInteger i = roundRobin ? (_roundRobinNext + k) % count : k;
        AdZativeCachedAd *cached = [_pools[(NSUInteger)i] peek:filter];
        if (cached != nil)
            return [[AdZativeTaken alloc] initWithCached:cached sourceIndex:i];
    }
    return nil;
}

- (NSArray<AdZativeTaken *> *)takeDistinct:(NSInteger)max
{
    NSArray<AdZativeTaken *> *picked = [self pick:max];
    for (AdZativeTaken *taken in picked)
    {
        AdZativeCachedAd *cached = taken.cached;
        [_pools[(NSUInteger)taken.sourceIndex] take:^BOOL(AdZativeCachedAd *candidate) {
            return candidate == cached;
        }];
    }
    [self apply];
    return picked;
}

- (NSArray<AdZativeTaken *> *)peekDistinct:(NSInteger)max
{
    return [self pick:max];
}

- (NSArray<AdZativeTaken *> *)pick:(NSInteger)max
{
    NSMutableArray<AdZativeTaken *> *picked = [NSMutableArray array];
    NSMutableSet<AdZativeCachedAd *> *used = [NSMutableSet set];
    AdZativeAdFilter unused = ^BOOL(AdZativeCachedAd *candidate) {
        return ![used containsObject:candidate];
    };
    BOOL found = YES;
    while ((NSInteger)picked.count < max && found)
    {
        found = NO;
        for (NSInteger i = 0; i < (NSInteger)_pools.count && (NSInteger)picked.count < max; i++)
        {
            AdZativeCachedAd *cached = [_pools[(NSUInteger)i] peek:unused];
            if (cached != nil)
            {
                [used addObject:cached];
                [picked addObject:[[AdZativeTaken alloc] initWithCached:cached sourceIndex:i]];
                found = YES;
            }
        }
    }
    return picked;
}

- (void)giveBack:(AdZativeTaken *)taken
{
    if (taken == nil)
        return;
    AdZativePool *owner = taken.cached.owner;
    if (owner != nil)
        [owner giveBack:taken.cached];
    else
        AdZativeDestroyAd(taken.cached.ad);
}

- (void)releaseDemand
{
    [_registry clearDemand:_config.slotId];
}

@end
