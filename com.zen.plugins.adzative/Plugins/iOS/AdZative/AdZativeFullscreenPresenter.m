#import "AdZativeFullscreenPresenter.h"

#import "AdZativeEventBridge.h"
#import "AdZativeLayoutRegistry.h"
#import "AdZativeNativeAdBinder.h"
#import "AdZativeWatchTime.h"

/// App open footer shows the HOST app (like AdMob app open ads): its icon and display name.
static UIImage *HostAppIcon(void)
{
    NSDictionary *icons = NSBundle.mainBundle.infoDictionary[@"CFBundleIcons"];
    NSArray *files = [icons isKindOfClass:[NSDictionary class]] ? icons[@"CFBundlePrimaryIcon"][@"CFBundleIconFiles"] : nil;
    NSString *name = [files isKindOfClass:[NSArray class]] ? files.lastObject : nil;
    return name != nil ? [UIImage imageNamed:name] : nil;
}

static NSString *HostAppName(void)
{
    NSDictionary *info = NSBundle.mainBundle.infoDictionary;
    return info[@"CFBundleDisplayName"] ?: info[@"CFBundleName"] ?: @"";
}

static id ViewOfClass(AdZativeInflated *view, NSString *viewId, Class type)
{
    UIView *found = [view viewWithId:viewId];
    return [found isKindOfClass:type] ? found : nil;
}

@implementation AdZativeFullscreenPresenter
{
    AdZativeOverlay *_host;
    AdZativeSlotConfig *_config;
    AdZativeSlotSources *_sources;
    NSArray<AdZativeTaken *> *_pod;
    NSInteger _requested;
    NSString *_layoutName;
    AdZativeSink _sink;
    AdZativeFullscreenClosed _onClosed;
    AdZativeClickHook _clickHook;

    NSInteger _index;
    AdZativeInflated *_view;
    GADNativeAd *_ad;
    AdZativeAdInfo *_info;
    UILabel *_countdown, *_podLabel;
    UIView *_closeButton, *_nextButton;   // any view: the built-in layouts use an icon (✕) and a chip with a chevron (Next)
    UIView *_continueButton;              // app open: the whole "Continue to <app>" bar
    NSInteger _elapsedSec;                // seconds the current ad has been on screen (drives countdown / close / next)
    NSInteger _rewardRemaining;
    NSInteger _watchSec;                  // countdown of the current ad (AdZativeWatchTime)
    NSInteger _closeAtSec;                // ✕ / Next available after this (interstitial, app open)
    BOOL _videoEnded;                     // the current video ad played to its end: ✕ / Next at once
    NSInteger _prevWatchSec, _usedWatchSec;   // previous ad's countdown / sum over the ads already shown
    BOOL _adComplete;                     // rewarded: current ad watched to the end
    BOOL _rewarded;
    BOOL _closed;
    AdZativeButtonMode _closeMode, _nextMode;
    float _closeAlpha, _nextAlpha;
    NSInteger _hostCountdownSec;          // -1: the plugin draws the countdown chip; >= 0: the host does (0 = hidden)
    float _hostCountdownAlpha;
    int64_t _openedAtMs, _adShownAtMs;
    AdZativeTask *_tick;
}

- (instancetype)initWithHost:(AdZativeOverlay *)host
                      config:(AdZativeSlotConfig *)config
                     sources:(AdZativeSlotSources *)sources
                         pod:(NSArray<AdZativeTaken *> *)pod
                   requested:(NSInteger)requested
                      layout:(NSString *)layout
                        sink:(AdZativeSink)sink
                    onClosed:(AdZativeFullscreenClosed)onClosed
                   clickHook:(AdZativeClickHook)clickHook
{
    if ((self = [super init]))
    {
        _host = host;
        _config = config;
        _sources = sources;
        _pod = [pod copy];
        _requested = requested;
        _layoutName = [layout copy];
        _sink = [sink copy];
        _onClosed = [onClosed copy];
        _clickHook = [clickHook copy];
        _index = -1;
        _closeMode = AdZativeButtonModeAuto;
        _nextMode = AdZativeButtonModeAuto;
        _closeAlpha = 1;
        _nextAlpha = 1;
        _hostCountdownSec = -1;
        _hostCountdownAlpha = 1;
        __weak AdZativeFullscreenPresenter *weakSelf = self;
        _tick = [[AdZativeTask alloc] initWithBlock:^{
            [weakSelf onTick];
        }];
    }
    return self;
}

- (NSString *)slotId
{
    return _config.slotId;
}

- (void)setButtonsClose:(AdZativeButtonMode)closeMode next:(AdZativeButtonMode)nextMode
{
    _closeMode = closeMode;
    _nextMode = nextMode;
    [self refresh];
}

- (void)setButtonAlphaClose:(float)closeAlpha next:(float)nextAlpha
{
    _closeAlpha = closeAlpha;
    _nextAlpha = nextAlpha;
    [self refresh];
}

- (void)setCountdown:(NSInteger)secondsLeft alpha:(float)alpha
{
    _hostCountdownSec = secondsLeft;
    _hostCountdownAlpha = alpha;
    [self refresh];
}

- (void)refresh
{
    if (_view != nil && !_closed)
        [self renderControls];
}

- (BOOL)skipToNext
{
    if (_closed || [self isLast])
        return NO;
    [self goNext:@"host"];
    return YES;
}

- (NSInteger)size
{
    return (NSInteger)_pod.count;
}

- (BOOL)isLast
{
    return _index >= [self size] - 1;
}

- (BOOL)isRewarded
{
    return _config.format == AdZativeFormatRewarded;
}

- (void)emit:(NSString *)event payload:(NSMutableDictionary *)payload
{
    _sink(_config.slotId, event, payload);
}

- (void)show
{
    _openedAtMs = AdZativeNowMs();
    if ([self size] > 1 || _requested > 1)
    {
        [self emit:@"pod_started" payload:[@{
            @"pod_size" : @([self size]),
            @"requested" : @(_requested),
            @"total_sec" : @([self estimateRemainingSec:0]),
        } mutableCopy]];
    }
    [self emit:@"fullscreen_opened" payload:[@{
        @"format" : AdZativeFormatName(_config.format),
        @"image_first_sec" : @(_config.imageFirstSec),
        @"image_next_sec" : @(_config.imageNextSec),
        @"close_after_sec" : @(_config.closeAfterSec),
        @"video_max_sec" : @(_config.videoMaxSec),
        @"pod_ratio" : @(_config.podRatio),
    } mutableCopy]];
    [self showAd:0];
}

// ------------------------------------------------------------------ one ad of the pod

- (void)showAd:(NSInteger)i
{
    AdZativeInflated *oldView = _view;
    GADNativeAd *oldAd = _ad;
    [_tick cancel];

    _index = i;
    AdZativeTaken *taken = _pod[(NSUInteger)i];
    _ad = taken.cached.ad;
    _info = taken.cached.info;
    NSString *name = _config.layoutMode == AdZativeLayoutModeAuto
        ? [AdZativeLayoutRegistry autoSelect:_config.format configured:_layoutName info:_info landscape:[_host isLandscape]]
        : _layoutName;
    AdZativeInflated *inflated = [AdZativeLayoutRegistry inflate:name format:_config.format landscape:[_host isLandscape]];
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
        @"mode" : AdZativeLayoutModeName(_config.layoutMode),
        @"score" : @([AdZativeLayoutRegistry score:inflated.name info:_info landscape:[_host isLandscape]]),
        @"missing_assets" : [[AdZativeLayoutRegistry missing:inflated.name info:_info landscape:[_host isLandscape]] componentsJoinedByString:@","],
        @"media_type" : AdZativeMediaName(_info.media),
        @"ad_unit" : taken.cached.adUnitId,
        @"pod_index" : @(i),
    } mutableCopy]];
    _view = inflated;
    _countdown = ViewOfClass(inflated, AdZativeIdCountdown, [UILabel class]);
    _closeButton = [inflated viewWithId:AdZativeIdClose];
    _nextButton = [inflated viewWithId:AdZativeIdNext];
    _podLabel = ViewOfClass(inflated, AdZativeIdPodLabel, [UILabel class]);
    _continueButton = [inflated viewWithId:AdZativeIdContinue];
    [self bindHostApp:inflated];

    NSInteger boundIndex = i;
    __weak AdZativeFullscreenPresenter *weakSelf = self;
    AdZativeClickHook clickHook = _clickHook;
    [AdZativeEventBridge attachAd:_ad
                           slotId:_config.slotId
                         adUnitId:taken.cached.adUnitId
                             sink:_sink
                     onVideoStart:^{
                         [weakSelf videoDidStartAt:boundIndex];
                     }
                       onVideoEnd:^{
                           [weakSelf videoDidEndAt:boundIndex];
                       }
                          onClick:^{
                              [weakSelf adClickedAt:boundIndex];
                              if (clickHook != nil)
                                  clickHook();
                          }];
    [AdZativeNativeAdBinder bindAdView:inflated.adView ad:_ad info:_info crop:_config.mediaCrop];

    // The root swallows touches, so the game underneath cannot be played through the ad.
    inflated.root.frame = _host.container.bounds;
    inflated.root.autoresizingMask = UIViewAutoresizingFlexibleWidth | UIViewAutoresizingFlexibleHeight;
    [_host addTop:inflated.root];
    if (oldView != nil)   // new ad on top first -> no blank frame
    {
        [_host remove:oldView.root];
        [oldView unbind];
    }
    if (oldAd != nil)
        AdZativeDestroyAd(oldAd);
    [self wireButtons];

    _elapsedSec = 0;
    _watchSec = [self watchSecFor:i prev:_prevWatchSec used:_usedWatchSec];
    _closeAtSec = AdZativeWatchCloseAt(_config, [_info isVideo], _watchSec);
    _videoEnded = NO;
    _adComplete = ![self isRewarded];
    _rewardRemaining = [self isRewarded] ? _watchSec : 0;
    _adShownAtMs = AdZativeNowMs();
    [self renderControls];
    [_tick runAfterMs:1000];

    NSMutableDictionary *payload = [_info putInto:[NSMutableDictionary dictionary] ad:_ad];
    payload[@"ad_unit"] = taken.cached.adUnitId;
    payload[@"source"] = @(taken.sourceIndex);
    payload[@"layout"] = inflated.name;
    payload[@"pod_index"] = @(i);
    payload[@"pod_size"] = @([self size]);
    payload[@"watch_sec"] = @(_watchSec);
    payload[@"close_at_sec"] = @(_closeAtSec);
    [self emit:@"shown" payload:payload];
    AdZativePostOnMain(^{
        AdZativeFullscreenPresenter *presenter = weakSelf;
        if (presenter != nil)
            [presenter emit:@"visible" payload:[@{ @"pod_index" : @(boundIndex) } mutableCopy]];
    });
}

- (void)videoDidStartAt:(NSInteger)boundIndex
{
    // The video length is often unknown at load time: now it is, so the watch time can shrink to it.
    if (boundIndex != _index || _closed)
        return;
    _watchSec = MIN(_watchSec, [self watchSecFor:_index prev:_prevWatchSec used:_usedWatchSec]);
    _closeAtSec = AdZativeWatchCloseAt(_config, [_info isVideo], _watchSec);
    if ([self isRewarded] && !_adComplete)
        _rewardRemaining = MAX(0, _watchSec - _elapsedSec);
    [self renderControls];
}

- (void)videoDidEndAt:(NSInteger)boundIndex
{
    if (boundIndex != _index || _closed)
        return;
    if ([self isRewarded])
    {
        [self markAdComplete];
    }
    else
    {
        _videoEnded = YES;
        [self renderControls];
    }
}

- (void)adClickedAt:(NSInteger)boundIndex
{
    if (boundIndex == _index)
        [self onAdClicked];
}

/// Countdown of pod ad k after an ad of prev seconds, used seconds already spent.
- (NSInteger)watchSecFor:(NSInteger)k prev:(NSInteger)prev used:(NSInteger)used
{
    AdZativeTaken *taken = _pod[(NSUInteger)k];
    BOOL video = [taken.cached.info isVideo];
    float duration = video ? [AdZativeAdInfo videoDurationOfAd:taken.cached.ad] : 0.f;
    return AdZativeWatchCountdown(_config, video, duration, k, [self size], prev, used);
}

/// Seconds still to watch from ad `from` on (the current ad excluded when from > index).
- (NSInteger)estimateRemainingSec:(NSInteger)from
{
    NSInteger total = 0;
    NSInteger prev = from == 0 ? 0 : (from == _index + 1 ? _watchSec : _prevWatchSec);
    NSInteger used = from == 0 ? 0 : _usedWatchSec + (from == _index + 1 ? _watchSec : 0);
    for (NSInteger k = from; k < [self size]; k++)
    {
        NSInteger watch = [self watchSecFor:k prev:prev used:used];
        total += watch;
        used += watch;
        prev = watch;
    }
    return total;
}

- (void)bindHostApp:(AdZativeInflated *)view
{
    UIImageView *icon = ViewOfClass(view, AdZativeIdHostAppIcon, [UIImageView class]);
    UILabel *name = ViewOfClass(view, AdZativeIdHostAppName, [UILabel class]);
    icon.image = HostAppIcon();
    name.text = HostAppName();
}

- (void)wireButtons
{
    [self addTap:_closeButton action:@selector(closeTapped)];
    [self addTap:_continueButton action:@selector(closeTapped)];
    [self addTap:_nextButton action:@selector(nextTapped)];
}

- (void)addTap:(UIView *)view action:(SEL)action
{
    if (view == nil)
        return;
    view.userInteractionEnabled = YES;
    [view addGestureRecognizer:[[UITapGestureRecognizer alloc] initWithTarget:self action:action]];
}

- (void)closeTapped
{
    [self tapClose:@"button"];
}

- (void)nextTapped
{
    [self tapNext:@"button"];
}

- (void)onTick
{
    if (_closed)
        return;
    _elapsedSec++;
    if ([self isRewarded] && !_adComplete)
    {
        if (_rewardRemaining > 0)
            _rewardRemaining--;
        // Watched at the end of its countdown (a long video is capped, see WatchTime) or of the video, whichever first.
        if (_rewardRemaining == 0)
            [self markAdComplete];
    }
    [self renderControls];
    [_tick runAfterMs:1000];
}

// ------------------------------------------------------------------ controls

/// Next is on screen (LOCKED: shown but inert, see tapNext).
- (BOOL)canNext
{
    if ([self isLast] || _nextMode == AdZativeButtonModeHide)
        return NO;
    if (_nextMode != AdZativeButtonModeAuto)
        return YES;
    if (!_adComplete)
        return NO;
    return [self isRewarded] || _elapsedSec >= _closeAtSec || _videoEnded;
}

/// ✕ / "Continue to app" is on screen (LOCKED: shown but inert, see tapClose).
- (BOOL)canClose
{
    if (_closeMode != AdZativeButtonModeAuto)
        return _closeMode != AdZativeButtonModeHide;
    if ([self isRewarded] && _rewarded)
        return YES;   // reward granted: leave at once
    // Rewarded: leaving early (no reward) after closeAfterSec. Interstitial / app open: see AdZativeWatchCloseAt.
    BOOL early = [self isRewarded] ? _elapsedSec < _config.closeAfterSec : _elapsedSec < _closeAtSec && !_videoEnded;
    if (early)
        return NO;
    return [self isLast] || _config.podCloseOnEveryAd;
}

- (void)renderControls
{
    BOOL close = [self canClose], next = [self canNext];
    if (_countdown != nil)
    {
        // Counts down this ad (rewarded: until it counts as watched); gone at 0 or when a video ended early.
        // The host may draw it instead (setCountdown).
        BOOL hostDrawn = _hostCountdownSec >= 0;
        NSInteger left = hostDrawn ? _hostCountdownSec
            : !_config.showCountdown || _videoEnded ? 0
            : [self isRewarded] ? (_adComplete ? 0 : _rewardRemaining)
            : _watchSec - _elapsedSec;
        _countdown.hidden = left <= 0;
        _countdown.alpha = hostDrawn ? _hostCountdownAlpha : 1;
        if (left > 0)
            _countdown.text = [NSString stringWithFormat:@"%lds", (long)left];
    }
    if (_closeButton != nil)
    {
        _closeButton.hidden = !(close && _config.format != AdZativeFormatAppOpen);
        _closeButton.alpha = _closeAlpha;
    }
    if (_continueButton != nil)
    {
        // AdMob app open: the bar is always there, "Continue to app"; before closeAfterSec it is dimmed and inactive.
        // It keeps taking taps (tapClose ignores them until then): a disabled Android view still swallows them, the
        // ad below must not get an accidental click.
        _continueButton.alpha = close ? _closeAlpha : 0.6f;
    }
    if (_nextButton != nil)
    {
        _nextButton.hidden = !next;
        _nextButton.alpha = _nextAlpha;
    }
    if (_podLabel != nil)
    {
        // Pods: "Ad 1 of 2". Rewarded: the reward status in the same chip ("Ad 1 of 2  ·  Reward in 12s").
        NSString *pod = [self size] > 1 ? [NSString stringWithFormat:@"Ad %ld of %ld", (long)(_index + 1), (long)[self size]] : nil;
        NSString *reward = nil;
        if ([self isRewarded])
        {
            NSInteger left = (_adComplete ? 0 : _rewardRemaining) + [self estimateRemainingSec:_index + 1];
            reward = _rewarded ? @"Reward earned"
                : left > 0 ? [NSString stringWithFormat:@"Reward in %lds", (long)left]
                : @"Almost there…";
        }
        NSString *text = pod != nil && reward != nil ? [NSString stringWithFormat:@"%@  ·  %@", pod, reward]
            : pod != nil ? pod : reward;
        _podLabel.hidden = text == nil;
        if (text != nil)
            _podLabel.text = text;
    }
}

- (void)markAdComplete
{
    if (_closed || _adComplete)
        return;
    _adComplete = YES;
    _rewardRemaining = 0;
    [self emit:@"pod_ad_complete" payload:[@{
        @"pod_index" : @(_index),
        @"pod_size" : @([self size]),
        @"watched_ms" : @(AdZativeNowMs() - _adShownAtMs),
    } mutableCopy]];
    if ([self isLast] && [self isRewarded])
        [self grantReward:@"watched"];
    [self renderControls];
}

/// Rewarded: a click on the ad counts as the whole show watched, the reward is granted at once.
- (void)onAdClicked
{
    if (_closed || ![self isRewarded] || _rewarded)
        return;
    _adComplete = YES;
    _rewardRemaining = 0;
    [self grantReward:@"click"];
}

- (void)grantReward:(NSString *)reason
{
    if (_rewarded || _closed)
        return;
    _rewarded = YES;
    [self renderControls];
    [self emit:@"reward_earned" payload:[@{
        @"reason" : reason,
        @"watched_ms" : @(AdZativeNowMs() - _openedAtMs),
        @"ads_watched" : @(_index + 1),
        @"pod_size" : @([self size]),
        @"has_video" : @([_info isVideo]),
    } mutableCopy]];
}

/// Next tapped, or Back: next ad, or "next_blocked" while the host keeps Next LOCKED.
- (void)tapNext:(NSString *)trigger
{
    if (_closed || ![self canNext])
        return;
    if (_nextMode == AdZativeButtonModeLocked)
    {
        [self emitBlocked:@"next_blocked" trigger:trigger];
        return;
    }
    [self goNext:@"user"];
}

/// ✕ / "Continue to app" tapped, or Back: close, or "close_blocked" while the host keeps ✕ LOCKED.
- (void)tapClose:(NSString *)trigger
{
    if (_closed || ![self canClose])
        return;
    if (_closeMode == AdZativeButtonModeLocked)
    {
        [self emitBlocked:@"close_blocked" trigger:trigger];
        return;
    }
    [self requestClose];
}

- (void)emitBlocked:(NSString *)event trigger:(NSString *)trigger
{
    [self emit:event payload:[@{ @"elapsed_ms" : @(AdZativeNowMs() - _adShownAtMs), @"trigger" : trigger } mutableCopy]];
}

/// @param trigger "user" (Next button / Back) or "host" (nextFullscreen from Unity)
- (void)goNext:(NSString *)trigger
{
    [self emit:@"pod_next" payload:[@{
        @"from_index" : @(_index),
        @"pod_size" : @([self size]),
        @"shown_ms" : @(AdZativeNowMs() - _adShownAtMs),
        @"trigger" : trigger,
    } mutableCopy]];
    _usedWatchSec += _watchSec;
    _prevWatchSec = _watchSec;
    [self showAd:_index + 1];
}

- (void)requestClose
{
    if ([self isRewarded] && !_rewarded)
    {
        // Closed before the end (the close control only appears after closeAfterSec): no reward.
        [self emit:@"reward_skipped" payload:[@{
            @"pod_index" : @(_index),
            @"watched_ms" : @(AdZativeNowMs() - _openedAtMs),
        } mutableCopy]];
    }
    [self close];
}

- (BOOL)handleBack
{
    if (_closed)
        return NO;
    // Back does what the visible controls allow; before ✕ / Next are available (or while LOCKED) it is swallowed.
    if ([self canClose])
        [self tapClose:@"back"];
    else if ([self canNext])
        [self tapNext:@"back"];
    return YES;
}

- (void)close
{
    if (_closed)
        return;
    _closed = YES;
    [_tick cancel];
    if (_view != nil)
    {
        [_host remove:_view.root];
        [_view unbind];
    }
    _view = nil;
    AdZativeDestroyAd(_ad);
    _ad = nil;
    // Pod ads never reached: back to the cache.
    for (NSInteger k = _index + 1; k < [self size]; k++)
        [_sources giveBack:_pod[(NSUInteger)k]];
    [self emit:@"closed" payload:[@{
        @"rewarded" : @(_rewarded),
        @"ads_watched" : @(_index + 1),
        @"pod_size" : @([self size]),
        @"duration_ms" : @(AdZativeNowMs() - _openedAtMs),
    } mutableCopy]];
    [self emit:@"fullscreen_closed" payload:[@{ @"format" : AdZativeFormatName(_config.format) } mutableCopy]];
    if (_onClosed != nil)
        _onClosed(self);
}

@end
