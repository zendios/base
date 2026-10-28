#import "AdZativePool.h"

#import <objc/runtime.h>

#import "AdZativePoolRegistry.h"

static const int64_t MaxBackoffMs = 64000;

static char observerKeyStorage;
const void *const AdZativeObserverKey = &observerKeyStorage;

void AdZativeDestroyAd(GADNativeAd *ad)
{
    if (ad == nil)
        return;
    ad.delegate = nil;
    ad.paidEventHandler = nil;
    ad.mediaContent.videoController.delegate = nil;
    objc_setAssociatedObject(ad, AdZativeObserverKey, nil, OBJC_ASSOCIATION_RETAIN_NONATOMIC);
}

AdZativeAdFilter AdZativeNoVideoFilter(void)
{
    return ^BOOL(AdZativeCachedAd *cached) {
        return ![cached.info isVideo];
    };
}

static NSString *Upper(NSString *text)
{
    return [[text stringByTrimmingCharactersInSet:NSCharacterSet.whitespaceAndNewlineCharacterSet] uppercaseString];
}

static GADMediaAspectRatio AspectRatioNamed(NSString *name)
{
    NSString *upper = Upper(name);
    if ([upper isEqualToString:@"LANDSCAPE"])
        return GADMediaAspectRatioLandscape;
    if ([upper isEqualToString:@"PORTRAIT"])
        return GADMediaAspectRatioPortrait;
    if ([upper isEqualToString:@"SQUARE"])
        return GADMediaAspectRatioSquare;
    if ([upper isEqualToString:@"UNKNOWN"])
        return GADMediaAspectRatioUnknown;
    return GADMediaAspectRatioAny;
}

static GADAdChoicesPosition AdChoicesNamed(NSString *name)
{
    NSString *upper = Upper(name);
    if ([upper isEqualToString:@"TOP_LEFT"])
        return GADAdChoicesPositionTopLeftCorner;
    if ([upper isEqualToString:@"BOTTOM_RIGHT"])
        return GADAdChoicesPositionBottomRightCorner;
    if ([upper isEqualToString:@"BOTTOM_LEFT"])
        return GADAdChoicesPositionBottomLeftCorner;
    return GADAdChoicesPositionTopRightCorner;
}

@interface AdZativeCachedAd ()
- (instancetype)initWithAd:(GADNativeAd *)ad adUnitId:(NSString *)adUnitId;
@end

@implementation AdZativeCachedAd

- (instancetype)initWithAd:(GADNativeAd *)ad adUnitId:(NSString *)adUnitId
{
    if ((self = [super init]))
    {
        _ad = ad;
        _adUnitId = [adUnitId copy];
        _loadedAtMs = AdZativeNowMs();
        _info = [AdZativeAdInfo infoForAd:ad];
    }
    return self;
}

- (int64_t)ageMs
{
    return AdZativeNowMs() - _loadedAtMs;
}

@end

@interface AdZativePool ()
- (void)request:(NSInteger)seq didLoad:(GADNativeAd *)ad;
- (void)request:(NSInteger)seq didFailWithCode:(NSInteger)code message:(nullable NSString *)message;
@end

/// One ad request. GADAdLoader keeps only a weak delegate, so the pool holds this object (and the loader).
@interface AdZativeLoadRequest : NSObject <GADNativeAdLoaderDelegate>
@property (nonatomic, readonly) NSInteger seq;
@property (nonatomic, weak, readonly) AdZativePool *pool;
@property (nonatomic, strong, nullable) GADAdLoader *loader;
- (instancetype)initWithSeq:(NSInteger)seq pool:(AdZativePool *)pool;
@end

@implementation AdZativeLoadRequest

- (instancetype)initWithSeq:(NSInteger)seq pool:(AdZativePool *)pool
{
    if ((self = [super init]))
    {
        _seq = seq;
        _pool = pool;
    }
    return self;
}

// Posted, not run inline: the loader finishes its own call before the pool reacts (and maybe replaces it).
- (void)adLoader:(GADAdLoader *)adLoader didReceiveNativeAd:(GADNativeAd *)nativeAd
{
    __weak AdZativePool *pool = _pool;
    NSInteger seq = _seq;
    AdZativePostOnMain(^{
        [pool request:seq didLoad:nativeAd];
    });
}

- (void)adLoader:(GADAdLoader *)adLoader didFailToReceiveAdWithError:(NSError *)error
{
    __weak AdZativePool *pool = _pool;
    NSInteger seq = _seq;
    NSInteger code = error.code;
    NSString *message = error.localizedDescription;
    AdZativePostOnMain(^{
        [pool request:seq didFailWithCode:code message:message];
    });
}

@end

@implementation AdZativePool
{
    GADMediaAspectRatio _aspectRatio;
    GADAdChoicesPosition _adChoices;
    BOOL _startMuted;
    int64_t _ttlMs;
    AdZativeSink _sink;
    __weak AdZativePoolRegistry *_registry;

    NSMutableArray<AdZativeCachedAd *> *_cache;
    NSInteger _targetSize;
    NSInteger _failCount;
    int64_t _requestStartMs;
    int64_t _nextLoadAtMs;   // end of the current backoff; 0 = none
    NSInteger _requestSeq;
    BOOL _destroyed;
    BOOL _paused;       // demand dropped to 0 (no slot wants preloading)
    BOOL _capBlocked;   // load_skipped already reported for the current starvation
    AdZativeTask *_retry;
    AdZativeTask *_purge;
    AdZativeLoadRequest *_request;
}

- (instancetype)initWithKey:(NSString *)key
                   adUnitId:(NSString *)adUnitId
                      shape:(AdZativeSlotConfig *)shape
                       sink:(AdZativeSink)sink
                   registry:(AdZativePoolRegistry *)registry
{
    if ((self = [super init]))
    {
        _key = [key copy];
        _adUnitId = [adUnitId copy];
        _aspectRatio = AspectRatioNamed(shape.mediaAspectRatio);
        _adChoices = AdChoicesNamed(shape.adChoicesPlacement);
        _startMuted = shape.startMuted;
        _ttlMs = shape.ttlMs;
        _sink = [sink copy];
        _registry = registry;
        _cache = [NSMutableArray array];
        _targetSize = 1;
        _lastErrorMessage = @"";
        __weak AdZativePool *weakSelf = self;
        _retry = [[AdZativeTask alloc] initWithBlock:^{
            [weakSelf fill];
        }];
        _purge = [[AdZativeTask alloc] initWithBlock:^{
            [weakSelf purgeExpired];
        }];
    }
    return self;
}

- (NSInteger)readyCount
{
    return (NSInteger)_cache.count;
}

- (NSInteger)inFlight
{
    return _loading ? 1 : 0;
}

- (void)setTargetSize:(NSInteger)size
{
    _targetSize = MAX(0, MIN(3, size));
    _paused = _targetSize == 0;
}

- (void)fill
{
    if (_destroyed || _loading || _paused)
        return;
    [_retry cancel];
    [self purgeExpired];
    if (_loading)
        return;   // purgeExpired refilled already (it calls fill): never a second request in flight
    if ((NSInteger)_cache.count >= _targetSize)
        return;
    int64_t wait = _nextLoadAtMs - AdZativeNowMs();
    if (wait > 0)
    {
        [_retry runAfterMs:wait];   // re-armed: the cancel above may have dropped the pending retry
        return;
    }
    if (![_registry canLoad])
    {
        if (!_capBlocked)
        {
            NSMutableDictionary *payload = [self payload];
            payload[@"reason"] = @"global_cap";
            _sink(@"", @"load_skipped", payload);
        }
        _capBlocked = YES;
        return;
    }
    _capBlocked = NO;
    [self startLoad];
}

- (void)requestOne:(BOOL)blockVideo
{
    if (_destroyed)
        return;
    [self purgeExpired];
    AdZativeCachedAd *found = nil;
    for (AdZativeCachedAd *cached in _cache)
    {
        if (!blockVideo || ![cached.info isVideo])
        {
            found = cached;
            break;
        }
    }
    if (found == nil && blockVideo && _cache.count > 0)
    {
        // Only video ads cached and this slot cannot show them: drop them so they don't pile up under the global cap.
        NSArray<AdZativeCachedAd *> *videos = [_cache copy];
        [_cache removeAllObjects];
        for (AdZativeCachedAd *cached in videos)
        {
            AdZativeDestroyAd(cached.ad);
            NSMutableDictionary *payload = [self payload];
            payload[@"reason"] = @"video_blocked";
            _sink(@"", @"discarded", payload);
        }
    }
    if (found != nil)
    {
        NSMutableDictionary *payload = [found.info putInto:[self payload] ad:found.ad];
        payload[@"latency_ms"] = @0;
        payload[@"cached"] = @YES;
        payload[@"ready_count"] = @(_cache.count);
        _sink(@"", @"loaded", payload);
        [_registry notifyPoolChanged:self];
        return;
    }
    if (_loading)
        return;
    if (![_registry canLoad])
    {
        _lastLoadFailed = YES;
        _lastErrorCode = -1;
        _lastErrorMessage = @"global_cap";
        NSMutableDictionary *payload = [self payload];
        payload[@"code"] = @(-1);
        payload[@"message"] = @"global_cap";
        payload[@"latency_ms"] = @0;
        payload[@"retry_in_ms"] = @0;
        _sink(@"", @"load_failed", payload);
        [_registry notifyPoolChanged:self];
        return;
    }
    [_retry cancel];
    [self startLoad];
}

- (void)startLoad
{
    _loading = YES;
    _requestStartMs = AdZativeNowMs();
    NSInteger seq = ++_requestSeq;
    NSMutableDictionary *payload = [self payload];
    payload[@"attempt"] = @(_failCount + 1);
    _sink(@"", @"load_request", payload);

    GADNativeAdMediaAdLoaderOptions *mediaOptions = [[GADNativeAdMediaAdLoaderOptions alloc] init];
    mediaOptions.mediaAspectRatio = _aspectRatio;
    GADNativeAdViewAdOptions *adChoicesOptions = [[GADNativeAdViewAdOptions alloc] init];
    adChoicesOptions.preferredAdChoicesPosition = _adChoices;
    GADVideoOptions *videoOptions = [[GADVideoOptions alloc] init];
    videoOptions.startMuted = _startMuted;

    AdZativeLoadRequest *request = [[AdZativeLoadRequest alloc] initWithSeq:seq pool:self];
    request.loader = [[GADAdLoader alloc] initWithAdUnitID:_adUnitId
                                        rootViewController:AdZativeRootViewController()
                                                   adTypes:@[ GADAdLoaderAdTypeNative ]
                                                   options:@[ mediaOptions, adChoicesOptions, videoOptions ]];
    request.loader.delegate = request;
    _request = request;

    GADRequest *adRequest = [GADRequest request];
    adRequest.requestAgent = @"zen-native-unity";
    [request.loader loadRequest:adRequest];
}

- (void)request:(NSInteger)seq didLoad:(GADNativeAd *)ad
{
    if (_destroyed || seq != _requestSeq)
    {
        AdZativeDestroyAd(ad);
        return;
    }
    _loading = NO;
    _lastLoadFailed = NO;
    _failCount = 0;
    _nextLoadAtMs = 0;
    AdZativeCachedAd *cached = [[AdZativeCachedAd alloc] initWithAd:ad adUnitId:_adUnitId];
    cached.owner = self;
    [_cache addObject:cached];
    [self schedulePurge];
    NSMutableDictionary *payload = [cached.info putInto:[self payload] ad:ad];
    payload[@"latency_ms"] = @(AdZativeNowMs() - _requestStartMs);
    payload[@"ready_count"] = @(_cache.count);
    _sink(@"", @"loaded", payload);
    [_registry notifyPoolChanged:self];
    [self fill];   // continue up to targetSize
}

- (void)request:(NSInteger)seq didFailWithCode:(NSInteger)code message:(NSString *)message
{
    if (_destroyed || seq != _requestSeq)
        return;
    _loading = NO;
    _lastLoadFailed = YES;
    _lastErrorCode = code;
    _lastErrorMessage = message ?: @"";
    _failCount++;
    int64_t backoff = MIN(MaxBackoffMs, ((int64_t)1 << MIN(_failCount, 6)) * 1000);
    _nextLoadAtMs = AdZativeNowMs() + backoff;
    NSMutableDictionary *payload = [self payload];
    payload[@"code"] = @(code);
    payload[@"message"] = message;
    payload[@"latency_ms"] = @(AdZativeNowMs() - _requestStartMs);
    payload[@"retry_in_ms"] = @(backoff);
    _sink(@"", @"load_failed", payload);
    [_registry notifyPoolChanged:self];   // lets FALLBACK slots start preloading the next ID
    [_retry runAfterMs:backoff];
}

- (void)resetBackoff
{
    if (_destroyed || _nextLoadAtMs == 0)
        return;
    _nextLoadAtMs = 0;
    _failCount = 0;
    [self fill];
}

- (AdZativeCachedAd *)take:(AdZativeAdFilter)filter
{
    [self purgeExpired];
    AdZativeCachedAd *found = [self firstMatching:filter];
    if (found != nil)
    {
        [_cache removeObjectIdenticalTo:found];
        [self fill];
    }
    return found;
}

- (AdZativeCachedAd *)peek:(AdZativeAdFilter)filter
{
    [self purgeExpired];
    return [self firstMatching:filter];
}

- (BOOL)has:(AdZativeAdFilter)filter
{
    return [self peek:filter] != nil;
}

- (nullable AdZativeCachedAd *)firstMatching:(nullable AdZativeAdFilter)filter
{
    for (AdZativeCachedAd *cached in _cache)
    {
        if (filter == nil || filter(cached))
            return cached;
    }
    return nil;
}

- (void)giveBack:(AdZativeCachedAd *)cached
{
    if (cached == nil)
        return;
    if (_destroyed || [cached ageMs] >= _ttlMs)
    {
        AdZativeDestroyAd(cached.ad);
        NSMutableDictionary *payload = [self payload];
        payload[@"reason"] = _destroyed ? @"pool_destroyed" : @"expired";
        _sink(@"", @"discarded", payload);
        return;
    }
    [_cache insertObject:cached atIndex:0];
    [self schedulePurge];
    [self postPoolChanged];
}

- (void)schedulePurge
{
    [_purge cancel];
    if (_cache.count == 0)
        return;
    int64_t oldestLoadedAt = INT64_MAX;
    for (AdZativeCachedAd *cached in _cache)
        oldestLoadedAt = MIN(oldestLoadedAt, cached.loadedAtMs);
    [_purge runAfterMs:MAX((int64_t)0, oldestLoadedAt + _ttlMs - AdZativeNowMs()) + 50];
}

- (void)purgeExpired
{
    int64_t now = AdZativeNowMs();
    NSMutableArray<AdZativeCachedAd *> *expired = [NSMutableArray array];
    for (AdZativeCachedAd *cached in _cache)
    {
        if (now - cached.loadedAtMs >= _ttlMs)
            [expired addObject:cached];
    }
    if (expired.count == 0)
        return;
    for (AdZativeCachedAd *cached in expired)
    {
        [_cache removeObjectIdenticalTo:cached];
        AdZativeDestroyAd(cached.ad);
        // Loaded but never shown: this is the show-rate loss to watch per ad unit.
        NSMutableDictionary *payload = [self payload];
        payload[@"reason"] = @"ttl";
        payload[@"media_type"] = AdZativeMediaName(cached.info.media);
        _sink(@"", @"expired", payload);
    }
    [self schedulePurge];
    [self fill];
    [self postPoolChanged];
}

- (void)postPoolChanged
{
    __weak AdZativePool *weakSelf = self;
    AdZativePostOnMain(^{
        AdZativePool *pool = weakSelf;
        if (pool != nil && !pool->_destroyed)
            [pool->_registry notifyPoolChanged:pool];
    });
}

- (void)destroy
{
    _destroyed = YES;
    _requestSeq++;
    [_retry cancel];
    [_purge cancel];
    for (AdZativeCachedAd *cached in _cache)
        AdZativeDestroyAd(cached.ad);
    [_cache removeAllObjects];
    _request.loader.delegate = nil;
    _request = nil;
}

- (NSMutableDictionary *)payload
{
    return [@{ @"ad_unit" : _adUnitId, @"pool" : _key } mutableCopy];
}

@end
