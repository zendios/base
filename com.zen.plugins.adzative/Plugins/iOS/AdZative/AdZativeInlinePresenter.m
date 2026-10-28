#import "AdZativeInlinePresenter.h"

#include <math.h>

#import "AdZativeEventBridge.h"
#import "AdZativeLayoutRegistry.h"
#import "AdZativeLayouts.h"
#import "AdZativeNativeAdBinder.h"

static const int64_t VideoWaitFallbackMs = 60000;   // video length unknown
static const int64_t VideoWaitGraceMs = 5000;
static const int64_t NotReadyRetryMs = 10000;

/// Whole Unity pixels, so the ad lines up with the Canvas rect it was given.
static CGFloat Snap(CGFloat points, CGFloat scale)
{
    return round(points * scale) / scale;
}

@implementation AdZativeInlinePresenter
{
    AdZativeOverlay *_host;
    AdZativeSlotConfig *_config;
    AdZativeSlotSources *_sources;
    __weak AdZativePoolRegistry *_registry;
    AdZativeSink _sink;
    AdZativeClickHook _clickHook;

    // Runtime-tunable (A/B from Unity).
    NSString *_layoutName;
    AdZativeLayoutMode _layoutMode;
    AdZativeVideoMode _requestedVideoMode;   // what Unity asked for
    AdZativeVideoMode _videoMode;            // what applies (see effectiveVideoMode)
    NSInteger _refreshSec;
    BOOL _nextWithoutVideo;                  // one-shot after rejectVideo / replaceAd(allowVideo NO)

    AdZativeInflated *_view;
    AdZativeCachedAd *_current;
    NSInteger _currentSource;
    AdZativeTaken *_pendingVideo;            // GATE: waiting for Unity's accept / reject
    __weak AdZativeCachedAd *_lastAnnounced;

    BOOL _visible, _paused, _covered, _destroyed;
    BOOL _wantVisible;   // show was called while no ad was ready: show as soon as one is
    int64_t _displayedAccumMs, _countingSinceMs, _swapDueSinceMs;
    BOOL _videoPlaying, _swapPending;

    // Geometry.
    BOOL _rectMode;
    float _nx, _ny, _nw, _nh;
    AdZativeAnchor _anchor;
    float _xDp, _yDp, _wDp, _hDp;

    AdZativeTask *_refreshTick;

    // reportLayout de-dup: a moving rect must not flood Unity with events every frame.
    AdZativeInflated *_reportedView;
    CGFloat _reportedWidth, _reportedHeight;
}

- (instancetype)initWithHost:(AdZativeOverlay *)host
                      config:(AdZativeSlotConfig *)config
                    registry:(AdZativePoolRegistry *)registry
                        sink:(AdZativeSink)sink
                   clickHook:(AdZativeClickHook)clickHook
{
    if ((self = [super init]))
    {
        _host = host;
        _config = config;
        _registry = registry;
        _sources = [[AdZativeSlotSources alloc] initWithConfig:config registry:registry];
        _sink = [sink copy];
        _clickHook = [clickHook copy];
        _layoutName = [config.layout copy];
        _layoutMode = config.layoutMode;
        _requestedVideoMode = config.videoMode;
        _videoMode = [self effectiveVideoMode];
        _refreshSec = config.refreshSec;
        _currentSource = -1;
        _anchor = AdZativeAnchorBottom;
        _reportedWidth = -1;
        _reportedHeight = -1;
        __weak AdZativeInlinePresenter *weakSelf = self;
        _refreshTick = [[AdZativeTask alloc] initWithBlock:^{
            [weakSelf tryRefresh];
        }];
        [registry addListener:self];
        [host addObserver:self];
        [self updatePreload];
    }
    return self;
}

- (void)requestUnit:(NSString *)adUnitId
{
    if (!_destroyed)
        [_sources requestUnit:adUnitId blockVideo:_videoMode == AdZativeVideoModeBlock];
}

- (BOOL)isReady
{
    return _current != nil || [_sources hasReady:[self filter]];
}

- (BOOL)hasReadyAd
{
    return [_sources hasReady:[self filter]];
}

- (nullable AdZativeAdFilter)filter
{
    return _videoMode == AdZativeVideoModeBlock || _nextWithoutVideo ? AdZativeNoVideoFilter() : nil;
}

// ------------------------------------------------------------------ geometry

- (void)setRectX:(float)nx y:(float)ny width:(float)nw height:(float)nh
{
    _rectMode = YES;
    _nx = nx;
    _ny = ny;
    _nw = nw;
    _nh = nh;
    [self applyGeometry];
}

- (void)setAnchor:(AdZativeAnchor)anchor x:(float)xDp y:(float)yDp width:(float)wDp height:(float)hDp
{
    _rectMode = NO;
    _anchor = anchor;
    _xDp = xDp;
    _yDp = yDp;
    _wDp = wDp;
    _hDp = hDp;
    [self applyGeometry];
}

- (void)overlayDidResize
{
    [self applyGeometry];
}

- (void)applyGeometry
{
    if (_view == nil || _view.root.superview == nil)
        return;
    CGSize size = _host.container.bounds.size;
    if (_rectMode && (size.width <= 0 || size.height <= 0))
    {
        // Before the overlay's first layout: try again shortly.
        __weak AdZativeInlinePresenter *weakSelf = self;
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, 100 * NSEC_PER_MSEC), dispatch_get_main_queue(), ^{
            [weakSelf applyGeometry];
        });
        return;
    }
    _view.root.frame = _rectMode ? [self rectFrame:size] : [self anchorFrame:size];
    [_view.root layoutIfNeeded];
    __weak AdZativeInlinePresenter *weakSelf = self;
    AdZativePostOnMain(^{
        [weakSelf reportLayout];
    });
}

- (CGRect)rectFrame:(CGSize)size
{
    CGFloat scale = [AdZativeOverlay pixelScale];
    CGFloat width = MAX(1 / scale, Snap(_nw * size.width, scale));
    CGFloat height = MAX(1 / scale, Snap(_nh * size.height, scale));
    return CGRectMake(Snap(_nx * size.width, scale), Snap(_ny * size.height, scale), width, height);
}

- (CGRect)anchorFrame:(CGSize)size
{
    BOOL mrec = _config.format == AdZativeFormatMrec;
    BOOL fullWidth = !mrec && _wDp <= 0 && (_anchor == AdZativeAnchorTop || _anchor == AdZativeAnchorBottom);
    UIEdgeInsets insets = [_host safeInsets];
    CGFloat width = fullWidth ? size.width - insets.left - insets.right : (_wDp > 0 ? _wDp : (mrec ? 300 : 320));
    CGFloat height = _hDp > 0 ? _hDp : (mrec ? 250 : 64);
    CGFloat centerX = (size.width - width) / 2, centerY = (size.height - height) / 2;
    CGFloat left = insets.left, top = insets.top;
    CGFloat right = size.width - insets.right - width, bottom = size.height - insets.bottom - height;
    CGPoint origin;
    switch (_anchor)
    {
        case AdZativeAnchorTop:
            origin = CGPointMake(fullWidth ? left : centerX, top);
            break;
        case AdZativeAnchorTopLeft:
            origin = CGPointMake(left, top);
            break;
        case AdZativeAnchorTopRight:
            origin = CGPointMake(right, top);
            break;
        case AdZativeAnchorBottomLeft:
            origin = CGPointMake(left, bottom);
            break;
        case AdZativeAnchorBottomRight:
            origin = CGPointMake(right, bottom);
            break;
        case AdZativeAnchorCenter:
            origin = CGPointMake(centerX, centerY);
            break;
        case AdZativeAnchorCustom:
            origin = CGPointMake(left + _xDp, top + _yDp);
            break;
        case AdZativeAnchorBottom:
        default:
            origin = CGPointMake(fullWidth ? left : centerX, bottom);
            break;
    }
    CGFloat scale = [AdZativeOverlay pixelScale];
    return CGRectMake(Snap(origin.x, scale), Snap(origin.y, scale), Snap(width, scale), Snap(height, scale));
}

// ------------------------------------------------------------------ show / hide

- (BOOL)show
{
    if (_destroyed)
        return NO;
    _wantVisible = YES;
    [self emit:@"opportunity" payload:[@{ @"ready" : @([self isReady]) } mutableCopy]];
    return [self showNow];
}

- (BOOL)showNow
{
    if (_current == nil)
    {
        if (_pendingVideo != nil)
            return NO;   // Unity is deciding (video_gate)
        AdZativeTaken *taken = [_sources take:[self filter]];
        if (taken == nil)
        {
            _visible = NO;
            [self updatePreloadForced:YES];
            [self emit:@"show_failed" payload:[@{ @"reason" : @"not_ready" } mutableCopy]];
            return NO;
        }
        if ([self holdAtGate:taken trigger:@"show"])
            return NO;
        [self attachAd:taken];
    }
    else if (_view.root.superview == nil)
    {
        [_host add:_view.root];
    }
    [self makeVisible];
    [self emit:@"shown" payload:[self adInfo]];
    return YES;
}

- (void)makeVisible
{
    _view.root.hidden = NO;
    _visible = YES;
    [self startCounting];
    [self applyGeometry];
    [self updatePreload];
    [self scheduleRefresh];
}

- (void)hide
{
    _wantVisible = NO;
    if (_pendingVideo != nil)
    {
        [_sources giveBack:_pendingVideo];   // never shown -> back to the cache, not wasted
        _pendingVideo = nil;
    }
    if (!_visible)
        return;
    _visible = NO;
    [self stopCounting];
    [_refreshTick cancel];
    _view.root.hidden = YES;
    _reportedView = nil;   // the next show reports layout + visible again
    [self updatePreload];
    [self emit:@"hidden" payload:[NSMutableDictionary dictionary]];
}

- (void)setPaused:(BOOL)paused
{
    _paused = paused;
    [self onVisibilityInputsChanged];
}

- (void)setCovered:(BOOL)covered
{
    _covered = covered;
    [self onVisibilityInputsChanged];
}

- (BOOL)onScreen
{
    return _visible && !_paused && !_covered;
}

- (void)onVisibilityInputsChanged
{
    if (![self onScreen])
    {
        [self stopCounting];
        [_refreshTick cancel];
        return;
    }
    [self startCounting];
    [self scheduleRefresh];
    if (_swapPending && !_videoPlaying)
        [self tryRefresh];
}

// ------------------------------------------------------------------ runtime tuning (A/B)

- (void)setLayout:(NSString *)name
{
    if (name.length == 0 || [name isEqualToString:_layoutName])
        return;
    _layoutName = [name copy];
    [self applyVideoMode];
    [self rebind];
}

- (void)setLayoutMode:(AdZativeLayoutMode)mode
{
    if (mode == _layoutMode)
        return;
    _layoutMode = mode;
    [self rebind];
}

- (void)setRefreshSec:(NSInteger)seconds
{
    _refreshSec = seconds <= 0 ? 0 : MAX(30, seconds);
    [_refreshTick cancel];
    [self scheduleRefresh];
    [self updatePreload];
}

- (void)setVideoMode:(AdZativeVideoMode)mode
{
    _requestedVideoMode = mode;
    [self applyVideoMode];
}

/// A banner layout without a MediaView (Native Banner, e.g. zen_banner) cannot show video: always BLOCK.
- (AdZativeVideoMode)effectiveVideoMode
{
    if (_config.format == AdZativeFormatBanner
        && ![[AdZativeLayoutRegistry coverage:_layoutName landscape:[_host isLandscape]] containsObject:AdZativeAssetMedia])
        return AdZativeVideoModeBlock;
    return _requestedVideoMode;
}

- (void)applyVideoMode
{
    AdZativeVideoMode mode = [self effectiveVideoMode];
    _videoMode = mode;
    if (mode != AdZativeVideoModeGate && _pendingVideo != nil)
    {
        AdZativeTaken *taken = _pendingVideo;
        _pendingVideo = nil;
        if (mode == AdZativeVideoModeAllow)
        {
            [self acceptTaken:taken];
        }
        else
        {
            [_sources giveBack:taken];
            [self continueAfterReject];
        }
    }
}

// ------------------------------------------------------------------ video gate

- (BOOL)holdAtGate:(AdZativeTaken *)taken trigger:(NSString *)trigger
{
    if (_videoMode != AdZativeVideoModeGate || ![taken.cached.info isVideo])
        return NO;
    _pendingVideo = taken;
    NSMutableDictionary *payload = [taken.cached.info putInto:[NSMutableDictionary dictionary] ad:taken.cached.ad];
    payload[@"ad_unit"] = taken.cached.adUnitId;
    payload[@"source"] = @(taken.sourceIndex);
    payload[@"trigger"] = trigger;
    [self emit:@"video_gate" payload:payload];
    return YES;
}

- (void)acceptVideo
{
    if (_pendingVideo == nil)
    {
        [self emit:@"video_gate_ignored" payload:[@{ @"reason" : @"nothing_pending" } mutableCopy]];
        return;
    }
    AdZativeTaken *taken = _pendingVideo;
    _pendingVideo = nil;
    [self acceptTaken:taken];
}

- (void)acceptTaken:(AdZativeTaken *)taken
{
    BOOL refresh = _current != nil;
    int64_t shownFor = [self displayedMs];
    [self attachAd:taken];
    if (refresh || _wantVisible)
    {
        [self makeVisible];
        NSMutableDictionary *payload = [self adInfo];
        if (refresh)
            payload[@"prev_display_ms"] = @(shownFor);
        [self emit:(refresh ? @"refreshed" : @"shown") payload:payload];
    }
}

- (void)rejectVideo
{
    if (_pendingVideo == nil)
    {
        [self emit:@"video_gate_ignored" payload:[@{ @"reason" : @"nothing_pending" } mutableCopy]];
        return;
    }
    [self discard:_pendingVideo.cached reason:@"video_rejected"];
    _pendingVideo = nil;
    [self continueAfterReject];
}

- (void)continueAfterReject
{
    _nextWithoutVideo = YES;
    if (_current != nil)
    {
        [self swapNow:@"refresh"];
        return;
    }
    if (_wantVisible)
        [self showNow];
}

- (void)replaceAd:(BOOL)allowVideo
{
    if (_destroyed)
        return;
    _nextWithoutVideo = !allowVideo;
    if (_current == nil)
    {
        if (_wantVisible)
            [self showNow];
        return;
    }
    AdZativeCachedAd *old = _current;
    AdZativeTaken *next = [_sources take:[self filter]];
    BOOL gated = next != nil && [self holdAtGate:next trigger:@"replace"];
    if (gated)
        next = nil;
    if (next != nil)
    {
        int64_t shownFor = [self displayedMs];
        [self attachAd:next];   // destroys the old ad after the new view is on top
        if (_visible)
            [self makeVisible];
        NSMutableDictionary *payload = [self adInfo];
        payload[@"prev_display_ms"] = @(shownFor);
        payload[@"allow_video"] = @(allowVideo);
        [self emit:@"replaced" payload:payload];
        return;
    }
    // Nothing to swap in: remove the old ad, keep the intent to show.
    BOOL wasVisible = _visible;
    if (_view != nil)
    {
        [_host remove:_view.root];
        [_view unbind];
    }
    _view = nil;
    _current = nil;
    AdZativeDestroyAd(old.ad);
    _visible = NO;
    [self stopCounting];
    [_refreshTick cancel];
    _wantVisible = _wantVisible || wasVisible;
    [self updatePreloadForced:YES];
    [self emit:@"replaced" payload:[@{
        @"ad_unit" : @"",
        @"reason" : gated ? @"video_gate" : @"not_ready",
        @"allow_video" : @(allowVideo),
    } mutableCopy]];
}

// ------------------------------------------------------------------ binding

- (void)rebind
{
    if (_current == nil)
        return;
    AdZativeInflated *old = _view;
    // The same ad moves to the new view: release it from the old one first, or unbinding later would unregister it.
    [old unbind];
    _view = [self inflateAndBind:_current];
    _view.root.frame = old != nil ? old.root.frame : CGRectMake(0, 0, 1, 1);
    [_host add:_view.root];
    _view.root.hidden = !_visible;
    if (old != nil)
        [_host remove:old.root];
    [self applyGeometry];
}

- (void)attachAd:(AdZativeTaken *)taken
{
    AdZativeInflated *old = _view;
    AdZativeCachedAd *oldAd = _current;
    CGRect frame = old != nil ? old.root.frame : CGRectMake(0, 0, 1, 1);

    _current = taken.cached;
    _currentSource = taken.sourceIndex;
    _nextWithoutVideo = NO;
    _videoPlaying = NO;
    _swapPending = NO;
    _view = [self inflateAndBind:taken.cached];
    _view.root.frame = frame;
    _view.root.hidden = !_visible;   // a hidden slot (replaceAd, accept) shows the new ad only on show
    [_host add:_view.root];   // new view on top first -> no blank frame
    if (old != nil)
    {
        [_host remove:old.root];
        [old unbind];
    }
    if (oldAd != nil)
        AdZativeDestroyAd(oldAd.ad);
    _displayedAccumMs = 0;
    _countingSinceMs = 0;
    _swapDueSinceMs = 0;
}

- (AdZativeInflated *)inflateAndBind:(AdZativeCachedAd *)cached
{
    AdZativeAdInfo *info = cached.info;
    NSString *name = _layoutMode == AdZativeLayoutModeAuto
        ? [AdZativeLayoutRegistry autoSelect:_config.format configured:_layoutName info:info landscape:[_host isLandscape]]
        : _layoutName;
    AdZativeInflated *inflated = [AdZativeLayoutRegistry inflate:name format:_config.format landscape:[_host isLandscape]];
    if (_config.containerMarginTopDp >= 0)
        AdZativeSetContainerMarginTop(inflated.root, _config.containerMarginTopDp);
    if (inflated.fellBack)
    {
        NSMutableDictionary *payload = [NSMutableDictionary dictionary];
        payload[@"layout"] = name;
        payload[@"reason"] = inflated.problem;
        payload[@"used"] = inflated.name;
        [self emit:@"layout_invalid" payload:payload];
    }
    [self emit:@"layout_selected" payload:[@{
        @"layout" : inflated.name,
        @"mode" : AdZativeLayoutModeName(_layoutMode),
        @"score" : @([AdZativeLayoutRegistry score:inflated.name info:info landscape:[_host isLandscape]]),
        @"missing_assets" : [[AdZativeLayoutRegistry missing:inflated.name info:info landscape:[_host isLandscape]] componentsJoinedByString:@","],
        @"media_type" : AdZativeMediaName(info.media),
        @"ad_unit" : cached.adUnitId,
    } mutableCopy]];
    __weak AdZativeInlinePresenter *weakSelf = self;
    [AdZativeEventBridge attachAd:cached.ad
                           slotId:_config.slotId
                         adUnitId:cached.adUnitId
                             sink:_sink
                     onVideoStart:^{
                         [weakSelf videoDidStart];
                     }
                       onVideoEnd:^{
                           [weakSelf videoDidEnd];
                       }
                          onClick:_clickHook];
    [AdZativeNativeAdBinder bindAdView:inflated.adView ad:cached.ad info:info crop:_config.mediaCrop];
    return inflated;
}

- (void)videoDidStart
{
    _videoPlaying = YES;
}

- (void)videoDidEnd
{
    _videoPlaying = NO;
    if (_swapPending)
        [self tryRefresh];
}

// ------------------------------------------------------------------ refresh / swap

- (void)startCounting
{
    if (_countingSinceMs == 0 && [self onScreen])
        _countingSinceMs = AdZativeNowMs();
}

- (void)stopCounting
{
    if (_countingSinceMs != 0)
        _displayedAccumMs += AdZativeNowMs() - _countingSinceMs;
    _countingSinceMs = 0;
}

/// Time the current ad has actually been on screen (visible + foreground).
- (int64_t)displayedMs
{
    return _displayedAccumMs + (_countingSinceMs != 0 ? AdZativeNowMs() - _countingSinceMs : 0);
}

- (int64_t)dueMs
{
    return (int64_t)MAX(_refreshSec, _config.minDisplaySec) * 1000;
}

- (void)scheduleRefresh
{
    [_refreshTick cancel];
    if (_refreshSec <= 0 || ![self onScreen] || _current == nil)
        return;
    [_refreshTick runAfterMs:MAX((int64_t)0, [self dueMs] - [self displayedMs])];
}

- (void)tryRefresh
{
    if (_destroyed || ![self onScreen] || _refreshSec <= 0 || _current == nil)
        return;
    if ([self displayedMs] < [self dueMs])
    {
        [self scheduleRefresh];
        return;
    }
    int64_t now = AdZativeNowMs();
    if (_swapDueSinceMs == 0)
        _swapDueSinceMs = now;

    if (_config.waitVideoEnd && _videoPlaying)
    {
        float duration = [AdZativeAdInfo videoDurationOfAd:_current.ad];
        int64_t cap = duration > 0 ? (int64_t)(duration * 1000.f) + VideoWaitGraceMs : VideoWaitFallbackMs;
        if (now - _swapDueSinceMs < cap)
        {
            _swapPending = YES;   // videoDidEnd calls us again
            [_refreshTick runAfterMs:cap - (now - _swapDueSinceMs)];
            return;
        }
        [self emit:@"video_wait_timeout" payload:[@{ @"waited_ms" : @(now - _swapDueSinceMs) } mutableCopy]];
    }
    if (_pendingVideo != nil)
        return;   // Unity is deciding on the next (video) ad
    [self swapNow:@"refresh"];
}

- (void)swapNow:(NSString *)trigger
{
    AdZativeTaken *next = [_sources take:[self filter]];
    if (next == nil)
    {
        [self emit:@"refresh_skipped" payload:[@{ @"reason" : @"not_ready" } mutableCopy]];
        [_refreshTick runAfterMs:NotReadyRetryMs];
        return;
    }
    if ([self holdAtGate:next trigger:trigger])
        return;   // the current ad stays until accept / reject
    int64_t shownFor = [self displayedMs];
    [self attachAd:next];
    [self makeVisible];
    NSMutableDictionary *payload = [self adInfo];
    payload[@"prev_display_ms"] = @(shownFor);
    [self emit:@"refreshed" payload:payload];
}

- (void)onPoolChanged:(AdZativePool *)pool
{
    if (_destroyed)
        return;
    [_sources resolvePending:pool sink:_sink slotId:_config.slotId];
    [_sources apply];   // FALLBACK: start / stop preloading the next ID when the previous one fails / recovers
    [self announceReady];
    if (_wantVisible && !_visible && !_covered && _current == nil && _pendingVideo == nil && [_sources hasReady:[self filter]])
        [self showNow];
}

/// "ad_ready": the ad this slot will show next changed - Unity learns the media type / video before showing.
- (void)announceReady
{
    AdZativeTaken *next = [_sources peek:[self filter]];
    if (next == nil || next.cached == _lastAnnounced)
        return;
    _lastAnnounced = next.cached;
    NSMutableDictionary *payload = [next.cached.info putInto:[NSMutableDictionary dictionary] ad:next.cached.ad];
    payload[@"ad_unit"] = next.cached.adUnitId;
    payload[@"source"] = @(next.sourceIndex);
    [self emit:@"ad_ready" payload:payload];
}

- (void)updatePreload
{
    [self updatePreloadForced:NO];
}

- (void)updatePreloadForced:(BOOL)force
{
    BOOL on = force || _config.preloadWhenHidden || (_visible && _refreshSec > 0) || _current == nil;
    [_sources setPreload:on];
}

- (void)discard:(AdZativeCachedAd *)cached reason:(NSString *)reason
{
    NSMutableDictionary *payload = [cached.info putInto:[NSMutableDictionary dictionary] ad:cached.ad];
    payload[@"ad_unit"] = cached.adUnitId;
    payload[@"reason"] = reason;
    [self emit:@"discarded" payload:payload];
    AdZativeDestroyAd(cached.ad);
}

// ------------------------------------------------------------------ reporting

- (void)emit:(NSString *)event payload:(NSMutableDictionary *)payload
{
    _sink(_config.slotId, event, payload);
}

- (NSMutableDictionary *)adInfo
{
    NSMutableDictionary *payload = _current != nil
        ? [_current.info putInto:[NSMutableDictionary dictionary] ad:_current.ad]
        : [NSMutableDictionary dictionary];
    payload[@"ad_unit"] = _current != nil ? _current.adUnitId : @"";
    payload[@"source"] = @(_currentSource);
    payload[@"layout"] = _view != nil ? _view.name : _layoutName;
    return payload;
}

/// Unity-pixel rect (origin top-left) + a warning when the MediaView is too small for video.
- (void)reportLayout
{
    if (_view == nil || !_visible)
        return;
    CGRect frame = _view.root.frame;
    BOOL newView = _view != _reportedView;
    if (!newView && frame.size.width == _reportedWidth && frame.size.height == _reportedHeight)
        return;   // pure move: Unity already knows the rect
    _reportedView = _view;
    _reportedWidth = frame.size.width;
    _reportedHeight = frame.size.height;
    CGFloat scale = [AdZativeOverlay pixelScale];
    [self emit:@"layout" payload:[@{
        @"x" : @(lround(frame.origin.x * scale)),
        @"y" : @(lround(frame.origin.y * scale)),
        @"width" : @(lround(frame.size.width * scale)),
        @"height" : @(lround(frame.size.height * scale)),
        @"screen_height" : @(lround(_host.container.bounds.size.height * scale)),
    } mutableCopy]];
    if (newView)
        [self emit:@"visible" payload:[NSMutableDictionary dictionary]];

    // Warn whenever a VIDEO ad sits in a MediaView < 120 x 120 pt (banner thumbnails of image ads are fine).
    UIView *media = [_view viewWithId:AdZativeIdMedia];
    if ([media isKindOfClass:[GADMediaView class]] && !media.hidden && _current != nil && [_current.info isVideo])
    {
        long width = lround(media.bounds.size.width), height = lround(media.bounds.size.height);
        if (width < 120 || height < 120)
        {
            [self emit:@"media_too_small" payload:[@{
                @"width_dp" : @(width),
                @"height_dp" : @(height),
                @"message" : @"MediaView < 120x120dp: video ads will not play",
            } mutableCopy]];
        }
    }
}

- (NSString *)debugState
{
    return [NSString stringWithFormat:@"%@%@%@%@%@%@%@ shown=%llds/%lds", _visible ? @"VISIBLE" : @"HIDDEN",
                                      [self isReady] ? @"/READY" : @"/EMPTY", _paused ? @"/PAUSED" : @"",
                                      _covered ? @"/COVERED" : @"", _pendingVideo != nil ? @"/GATE" : @"",
                                      _videoPlaying ? @"/VIDEO" : @"", _swapPending ? @"/SWAP_PENDING" : @"",
                                      (long long)([self displayedMs] / 1000), (long)_refreshSec];
}

- (void)destroy
{
    _destroyed = YES;
    _visible = NO;
    _wantVisible = NO;
    [_refreshTick cancel];
    [_registry removeListener:self];
    [_host removeObserver:self];
    if (_pendingVideo != nil)
    {
        [_sources giveBack:_pendingVideo];
        _pendingVideo = nil;
    }
    [_sources releaseDemand];
    if (_view != nil)
    {
        [_host remove:_view.root];
        [_view unbind];
    }
    _view = nil;
    if (_current != nil)
        AdZativeDestroyAd(_current.ad);
    _current = nil;
    [self emit:@"destroyed" payload:[NSMutableDictionary dictionary]];
}

@end
