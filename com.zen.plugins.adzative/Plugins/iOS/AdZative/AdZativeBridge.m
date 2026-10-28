// Static facade behind the C ABI (Android: unity/AdZativeSDK.java).
//
// Model: a SLOT is a display position (Canvas rect or anchor + layout + list of ad unit IDs). Slots draw ads from shared
// POOLS (one per ad unit), see AdZativePoolRegistry.

#import "AdZativeBridge.h"

#import <Network/Network.h>

#import "AdZativeFullscreenPresenter.h"
#import "AdZativeInlinePresenter.h"
#import "AdZativeOverlay.h"
#import "AdZativePoolRegistry.h"
#import "AdZativeSlotSources.h"
#import "AdZativeWatchTime.h"

@class AdZativeFullscreenSlot;

static AdZativeOverlay *host;
static AdZativeEventCallback listener;
static BOOL sdkReady;
static AdZativePoolRegistry *registry;
static NSMutableDictionary<NSString *, AdZativeInlinePresenter *> *inlineSlots;
static NSMutableDictionary<NSString *, AdZativeFullscreenSlot *> *fullscreenSlots;
static NSMutableArray<dispatch_block_t> *pendingUntilReady;
static AdZativeFullscreenPresenter *activeFullscreen;
static nw_path_monitor_t pathMonitor;
static BOOL lifecycleRegistered;

// App-open / foreground bookkeeping.
static BOOL wentBackground;
static BOOL clickSuppressesForeground;   // set by an ad click (cleared again when an in-app SDK screen closes)
static BOOL hostSuppressesForeground;    // set by AdZative_SuppressNextForeground
static BOOL appOpenSuppressed;
static int64_t lastAppOpenShownMs;

// ------------------------------------------------------------------ events

static void Emit(NSString *slotId, NSString *event, NSMutableDictionary *payload)
{
    payload[@"ts"] = @(AdZativeNowMs());
    // A click opened an in-app SDK screen (store page) and the app never left: on Android a click-through always leaves
    // the activity and the next foreground consumes the flag; here nothing would, so the next real return would skip
    // its app-open ad.
    if ([event isEqualToString:@"sdk_fullscreen_closed"] && !wentBackground)
        clickSuppressesForeground = NO;   // the host's own SuppressNextForeground stays
    AdZativeEventCallback callback = listener;
    if (callback == NULL)
        return;
    NSString *json = @"{}";
    if ([NSJSONSerialization isValidJSONObject:payload])
    {
        NSData *data = [NSJSONSerialization dataWithJSONObject:payload options:0 error:nil];
        if (data != nil)
            json = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding] ?: @"{}";
    }
    callback((slotId ?: @"").UTF8String, event.UTF8String, json.UTF8String);
}

static AdZativeSink Sink(void)
{
    static AdZativeSink sink;
    static dispatch_once_t once;
    dispatch_once(&once, ^{
        sink = ^(NSString *slotId, NSString *event, NSMutableDictionary *payload) {
            Emit(slotId, event, payload);
        };
    });
    return sink;
}

/// Leaving the app because of an ad click must not trigger an app-open ad on return.
static AdZativeClickHook ClickHook(void)
{
    return ^{
        clickSuppressesForeground = YES;
    };
}

static NSMutableDictionary *Payload(NSDictionary *fields)
{
    return [fields mutableCopy];
}

// ------------------------------------------------------------------ fullscreen slot

/// Fullscreen slot: sources + layout; the presenter is created per show.
@interface AdZativeFullscreenSlot : NSObject <AdZativePoolListener>
@property (nonatomic, readonly) AdZativeSlotConfig *config;
@property (nonatomic, readonly) AdZativeSlotSources *sources;
@property (nonatomic, copy) NSString *layout;
// Host control of ✕ / Next / countdown chip (setFullscreenButtons / ButtonAlpha / Countdown), kept for every show.
@property (nonatomic) AdZativeButtonMode closeMode;
@property (nonatomic) AdZativeButtonMode nextMode;
@property (nonatomic) float closeAlpha;
@property (nonatomic) float nextAlpha;
@property (nonatomic) NSInteger countdownSec;
@property (nonatomic) float countdownAlpha;
- (instancetype)initWithConfig:(AdZativeSlotConfig *)config;
- (void)applyControls:(AdZativeFullscreenPresenter *)presenter;
- (void)destroy;
@end

@implementation AdZativeFullscreenSlot
{
    __weak AdZativeCachedAd *_lastAnnounced;
}

- (instancetype)initWithConfig:(AdZativeSlotConfig *)config
{
    if ((self = [super init]))
    {
        _config = config;
        _sources = [[AdZativeSlotSources alloc] initWithConfig:config registry:registry];
        _layout = [config.layout copy];
        _closeMode = AdZativeButtonModeAuto;
        _nextMode = AdZativeButtonModeAuto;
        _closeAlpha = 1;
        _nextAlpha = 1;
        _countdownSec = -1;
        _countdownAlpha = 1;
        [registry addListener:self];
    }
    return self;
}

- (void)onPoolChanged:(AdZativePool *)pool
{
    [_sources resolvePending:pool sink:Sink() slotId:_config.slotId];
    [_sources apply];
    // "ad_ready": what this slot would show next (media type, video length, assets).
    AdZativeTaken *next = [_sources peek:nil];
    if (next == nil || next.cached == _lastAnnounced)
        return;
    _lastAnnounced = next.cached;
    NSMutableDictionary *payload = [next.cached.info putInto:[NSMutableDictionary dictionary] ad:next.cached.ad];
    payload[@"ad_unit"] = next.cached.adUnitId;
    payload[@"source"] = @(next.sourceIndex);
    Emit(_config.slotId, @"ad_ready", payload);
}

- (void)applyControls:(AdZativeFullscreenPresenter *)presenter
{
    [presenter setButtonsClose:_closeMode next:_nextMode];
    [presenter setButtonAlphaClose:_closeAlpha next:_nextAlpha];
    [presenter setCountdown:_countdownSec alpha:_countdownAlpha];
}

- (void)destroy
{
    [registry removeListener:self];
    [_sources releaseDemand];
}

@end

// ------------------------------------------------------------------ helpers

static NSString *Str(const char *text)
{
    if (text == NULL)
        return @"";
    return [NSString stringWithUTF8String:text] ?: @"";
}

static char *CopyString(NSString *text)
{
    return strdup(text.UTF8String ?: "");
}

static void SyncOnMain(dispatch_block_t block)
{
    if ([NSThread isMainThread])
        block();
    else
        dispatch_sync(dispatch_get_main_queue(), block);
}

static float Clamp01(float value)
{
    return MAX(0.f, MIN(1.f, value));
}

static void EnsureState(void)
{
    static dispatch_once_t once;
    dispatch_once(&once, ^{
        inlineSlots = [NSMutableDictionary dictionary];
        fullscreenSlots = [NSMutableDictionary dictionary];
        pendingUntilReady = [NSMutableArray array];
    });
}

static void WhenReady(dispatch_block_t block)
{
    dispatch_block_t work = [block copy];
    AdZativePostOnMain(^{
        EnsureState();
        if (sdkReady)
            work();
        else
            [pendingUntilReady addObject:work];
    });
}

/// Unity calls arrive on the main thread; the work runs in a later turn, like on Android (see AdZativeBridge.h).
static void Post(dispatch_block_t block)
{
    AdZativePostOnMain(^{
        EnsureState();
        block();
    });
}

static AdZativeInlinePresenter *Inline(NSString *slotId)
{
    AdZativeInlinePresenter *presenter = inlineSlots[slotId];
    if (presenter == nil)
    {
        NSString *reason = fullscreenSlots[slotId] != nil ? @"slot_is_fullscreen" : @"unknown_slot";
        Emit(slotId, @"show_failed", Payload(@{ @"reason" : reason }));
    }
    return presenter;
}

static void DestroySlotInternal(NSString *slotId, BOOL emit)
{
    AdZativeInlinePresenter *presenter = inlineSlots[slotId];
    if (presenter != nil)
    {
        [inlineSlots removeObjectForKey:slotId];
        [presenter destroy];   // emits "destroyed" itself, also when configureSlot replaces it (as on Android)
    }
    AdZativeFullscreenSlot *slot = fullscreenSlots[slotId];
    if (slot != nil)
    {
        [fullscreenSlots removeObjectForKey:slotId];
        [slot destroy];
        if (emit)
            Emit(slotId, @"destroyed", [NSMutableDictionary dictionary]);
    }
}

// ------------------------------------------------------------------ init

static void OnForeground(void);

static void RegisterLifecycle(void)
{
    if (lifecycleRegistered)
        return;
    lifecycleRegistered = YES;
    NSNotificationCenter *center = NSNotificationCenter.defaultCenter;
    [center addObserverForName:UIApplicationDidEnterBackgroundNotification
                        object:nil
                         queue:NSOperationQueue.mainQueue
                    usingBlock:^(NSNotification *note) {
                        wentBackground = YES;
                        for (AdZativeInlinePresenter *presenter in inlineSlots.allValues)
                            [presenter setPaused:YES];
                    }];
    // didBecomeActive also follows a system alert or Control Center, without leaving the app: only a real return counts.
    [center addObserverForName:UIApplicationDidBecomeActiveNotification
                        object:nil
                         queue:NSOperationQueue.mainQueue
                    usingBlock:^(NSNotification *note) {
                        if (!wentBackground)
                            return;
                        wentBackground = NO;
                        OnForeground();
                    }];
}

/// Network back: pools in backoff ask again at once instead of waiting up to 64 s.
static void RegisterNetworkMonitor(void)
{
    if (pathMonitor != nil)
        return;
    pathMonitor = nw_path_monitor_create();
    nw_path_monitor_set_queue(pathMonitor, dispatch_get_main_queue());
    nw_path_monitor_set_update_handler(pathMonitor, ^(nw_path_t path) {
        if (nw_path_get_status(path) == nw_path_status_satisfied)
            [registry onNetworkAvailable];
    });
    nw_path_monitor_start(pathMonitor);
}

static void SetUp(void)
{
    EnsureState();
    if (host == nil)
        host = [[AdZativeOverlay alloc] init];
    if (registry == nil)
        registry = [[AdZativePoolRegistry alloc] initWithSink:Sink()];
    RegisterLifecycle();
    RegisterNetworkMonitor();
}

static void BecomeReady(NSMutableDictionary *payload)
{
    sdkReady = YES;
    Emit(@"", @"initialized", payload);
    NSArray<dispatch_block_t> *pending = [pendingUntilReady copy];
    [pendingUntilReady removeAllObjects];
    for (dispatch_block_t work in pending)
        work();
}

void AdZative_Initialize(const char *appId, AdZativeEventCallback callback)
{
    listener = callback;
    Post(^{
        SetUp();
        [GADMobileAds.sharedInstance startWithCompletionHandler:^(GADInitializationStatus *status) {
            AdZativeRunOnMain(^{
                Emit(@"", @"adapters_initialized", [NSMutableDictionary dictionary]);
            });
        }];
        BecomeReady([NSMutableDictionary dictionary]);
    });
}

void AdZative_InitializeExternal(AdZativeEventCallback callback)
{
    listener = callback;
    Post(^{
        SetUp();
        BecomeReady(Payload(@{ @"external" : @YES }));
    });
}

void AdZative_SetMaxCachedAds(int count)
{
    Post(^{
        [registry setMaxCachedAds:count];
    });
}

// ------------------------------------------------------------------ slots

void AdZative_ConfigureSlot(const char *json)
{
    NSString *text = Str(json);
    WhenReady(^{
        NSString *error = nil;
        AdZativeSlotConfig *config = [AdZativeSlotConfig configWithJson:text error:&error];
        if (config == nil)
        {
            NSMutableDictionary *payload = [NSMutableDictionary dictionary];
            payload[@"message"] = error;
            payload[@"json"] = text;
            Emit(@"", @"config_error", payload);
            return;
        }
        DestroySlotInternal(config.slotId, NO);
        if (AdZativeFormatIsFullscreen(config.format))
        {
            AdZativeFullscreenSlot *slot = [[AdZativeFullscreenSlot alloc] initWithConfig:config];
            fullscreenSlots[config.slotId] = slot;
            [slot.sources setPreload:YES];
        }
        else
        {
            AdZativeInlinePresenter *presenter = [[AdZativeInlinePresenter alloc] initWithHost:host
                                                                                        config:config
                                                                                      registry:registry
                                                                                          sink:Sink()
                                                                                     clickHook:ClickHook()];
            if (activeFullscreen != nil)
                [presenter setCovered:YES];
            inlineSlots[config.slotId] = presenter;
        }
        Emit(config.slotId, @"configured", Payload(@{
            @"format" : AdZativeFormatName(config.format),
            @"ids" : @(config.adUnitIds.count),
            @"policy" : AdZativeSourcePolicyName(config.sourcePolicy),
            @"layout" : config.layout,
        }));
    });
}

void AdZative_LoadUnit(const char *slotId, const char *adUnitId)
{
    NSString *sid = Str(slotId), *unit = Str(adUnitId);
    WhenReady(^{
        AdZativeInlinePresenter *presenter = inlineSlots[sid];
        if (presenter != nil)
        {
            [presenter requestUnit:unit];
            return;
        }
        AdZativeFullscreenSlot *slot = fullscreenSlots[sid];
        if (slot != nil)
        {
            [slot.sources requestUnit:unit blockVideo:NO];
            return;
        }
        Emit(sid, @"load_failed", Payload(@{ @"ad_unit" : unit, @"code" : @(-2), @"message" : @"unknown_slot" }));
    });
}

int AdZative_HasReadyAd(const char *slotId)
{
    NSString *sid = Str(slotId);
    __block BOOL ready = NO;
    SyncOnMain(^{
        EnsureState();
        AdZativeInlinePresenter *presenter = inlineSlots[sid];
        if (presenter != nil)
            ready = [presenter hasReadyAd];
        else
            ready = [fullscreenSlots[sid].sources hasReady];
    });
    return ready ? 1 : 0;
}

int AdZative_IsReady(const char *slotId)
{
    NSString *sid = Str(slotId);
    __block BOOL ready = NO;
    SyncOnMain(^{
        EnsureState();
        AdZativeInlinePresenter *presenter = inlineSlots[sid];
        if (presenter != nil)
            ready = [presenter isReady];
        else
            ready = [fullscreenSlots[sid].sources hasReady];
    });
    return ready ? 1 : 0;
}

void AdZative_ShowSlotInRect(const char *slotId, float nx, float ny, float nw, float nh)
{
    NSString *sid = Str(slotId);
    WhenReady(^{
        AdZativeInlinePresenter *presenter = Inline(sid);
        if (presenter == nil)
            return;
        [presenter setRectX:nx y:ny width:nw height:nh];
        [presenter show];
    });
}

/// Called by AdZativeSlot only when the RectTransform actually moved / resized.
void AdZative_UpdateSlotRect(const char *slotId, float nx, float ny, float nw, float nh)
{
    NSString *sid = Str(slotId);
    Post(^{
        [inlineSlots[sid] setRectX:nx y:ny width:nw height:nh];
    });
}

void AdZative_ShowSlotAtAnchor(const char *slotId, int position, float xDp, float yDp, float wDp, float hDp)
{
    NSString *sid = Str(slotId);
    WhenReady(^{
        AdZativeInlinePresenter *presenter = Inline(sid);
        if (presenter == nil)
            return;
        [presenter setAnchor:(AdZativeAnchor)position x:xDp y:yDp width:wDp height:hDp];
        [presenter show];
    });
}

// ------------------------------------------------------------------ inline: video gate / replace / A-B tuning

void AdZative_AcceptVideo(const char *slotId)
{
    NSString *sid = Str(slotId);
    Post(^{
        [Inline(sid) acceptVideo];
    });
}

void AdZative_RejectVideo(const char *slotId)
{
    NSString *sid = Str(slotId);
    Post(^{
        [Inline(sid) rejectVideo];
    });
}

void AdZative_ReplaceAd(const char *slotId, int allowVideo)
{
    NSString *sid = Str(slotId);
    Post(^{
        [Inline(sid) replaceAd:allowVideo != 0];
    });
}

void AdZative_SetVideoMode(const char *slotId, const char *mode)
{
    NSString *sid = Str(slotId), *name = Str(mode);
    Post(^{
        [Inline(sid) setVideoMode:AdZativeParseVideoMode(name, AdZativeVideoModeAllow)];
    });
}

void AdZative_SetRefreshSec(const char *slotId, int seconds)
{
    NSString *sid = Str(slotId);
    Post(^{
        [Inline(sid) setRefreshSec:seconds];
    });
}

/// MANUAL | AUTO (inline; fullscreen slots take layoutMode from their config).
void AdZative_SetLayoutMode(const char *slotId, const char *mode)
{
    NSString *sid = Str(slotId), *name = Str(mode);
    Post(^{
        [inlineSlots[sid] setLayoutMode:AdZativeParseLayoutMode(name, AdZativeLayoutModeManual)];
    });
}

void AdZative_SetSlotLayout(const char *slotId, const char *layoutName)
{
    NSString *sid = Str(slotId), *layout = Str(layoutName);
    Post(^{
        [inlineSlots[sid] setLayout:layout];
        fullscreenSlots[sid].layout = layout;
    });
}

void AdZative_HideSlot(const char *slotId)
{
    NSString *sid = Str(slotId);
    Post(^{
        [inlineSlots[sid] hide];
    });
}

void AdZative_DestroySlot(const char *slotId)
{
    NSString *sid = Str(slotId);
    Post(^{
        DestroySlotInternal(sid, YES);
    });
}

// ------------------------------------------------------------------ fullscreen

static NSInteger EffectivePod(AdZativeSlotConfig *config, NSInteger podSize)
{
    if (config.format != AdZativeFormatInterstitial && config.format != AdZativeFormatRewarded)
        return 1;
    return MAX(1, MIN(3, podSize > 0 ? podSize : config.podSize));
}

static BOOL ShowFullscreenInternal(NSString *slotId, NSInteger podSize)
{
    AdZativeFullscreenSlot *slot = fullscreenSlots[slotId];
    if (slot == nil)
    {
        NSString *reason = inlineSlots[slotId] != nil ? @"slot_is_inline" : @"unknown_slot";
        Emit(slotId, @"show_failed", Payload(@{ @"reason" : reason }));
        return NO;
    }
    Emit(slotId, @"opportunity", Payload(@{ @"ready" : @([slot.sources hasReady]) }));
    if (activeFullscreen != nil)
    {
        Emit(slotId, @"show_failed", Payload(@{ @"reason" : @"another_fullscreen_showing" }));
        return NO;
    }
    NSInteger count = EffectivePod(slot.config, podSize);
    NSMutableArray<AdZativeTaken *> *ads = [NSMutableArray array];
    if (count > 1)
    {
        [ads addObjectsFromArray:[slot.sources takeDistinct:count]];
    }
    else
    {
        AdZativeTaken *taken = [slot.sources take:nil];
        if (taken != nil)
            [ads addObject:taken];
    }
    if (ads.count == 0)
    {
        Emit(slotId, @"show_failed", Payload(@{ @"reason" : @"not_ready" }));
        return NO;
    }
    AdZativeFullscreenPresenter *presenter = [[AdZativeFullscreenPresenter alloc]
        initWithHost:host
              config:slot.config
             sources:slot.sources
                 pod:ads
           requested:count
              layout:slot.layout
                sink:Sink()
            onClosed:^(AdZativeFullscreenPresenter *closed) {
                if (activeFullscreen == closed)
                    activeFullscreen = nil;
                for (AdZativeInlinePresenter *inlinePresenter in inlineSlots.allValues)
                    [inlinePresenter setCovered:NO];
            }
           clickHook:ClickHook()];
    [slot applyControls:presenter];
    for (AdZativeInlinePresenter *inlinePresenter in inlineSlots.allValues)
        [inlinePresenter setCovered:YES];
    activeFullscreen = presenter;
    [presenter show];
    if (slot.config.format == AdZativeFormatAppOpen)
        lastAppOpenShownMs = AdZativeNowMs();
    return YES;
}

void AdZative_ShowFullscreen(const char *slotId, int podSize)
{
    NSString *sid = Str(slotId);
    WhenReady(^{
        ShowFullscreenInternal(sid, podSize);
    });
}

/// Synchronous snapshot for the game's own "watch N ads (~T s) for a reward?" UI, BEFORE showing:
/// {"slot","requested","ready","total_sec","ads":[{ad_unit, media_type, video_duration_s, watch_sec, network, ...}]}
char *AdZative_GetPodInfo(const char *slotId, int podSize)
{
    NSString *sid = Str(slotId);
    __block NSString *json = @"{\"ready\":0}";
    SyncOnMain(^{
        EnsureState();
        NSMutableDictionary *info = Payload(@{ @"slot" : sid });
        AdZativeFullscreenSlot *slot = fullscreenSlots[sid];
        if (slot != nil)
        {
            NSInteger count = EffectivePod(slot.config, podSize);
            NSMutableArray<AdZativeTaken *> *ads = [NSMutableArray array];
            if (count > 1)
            {
                [ads addObjectsFromArray:[slot.sources peekDistinct:count]];
            }
            else
            {
                AdZativeTaken *taken = [slot.sources peek:nil];
                if (taken != nil)
                    [ads addObject:taken];
            }
            NSMutableArray<NSDictionary *> *list = [NSMutableArray array];
            NSInteger total = 0, prev = 0;
            for (NSUInteger k = 0; k < ads.count; k++)
            {
                AdZativeTaken *taken = ads[k];
                BOOL video = [taken.cached.info isVideo];
                float duration = video ? [AdZativeAdInfo videoDurationOfAd:taken.cached.ad] : 0.f;
                NSInteger watch = AdZativeWatchCountdown(slot.config, video, duration, (NSInteger)k, (NSInteger)ads.count,
                                                         prev, total);
                total += watch;
                prev = watch;
                NSMutableDictionary *ad = [taken.cached.info putInto:[NSMutableDictionary dictionary] ad:taken.cached.ad];
                ad[@"ad_unit"] = taken.cached.adUnitId;
                ad[@"source"] = @(taken.sourceIndex);
                ad[@"watch_sec"] = @(watch);
                ad[@"duration_known"] = @(!video || duration > 0);
                [list addObject:ad];
            }
            info[@"requested"] = @(count);
            info[@"ready"] = @(ads.count);
            info[@"total_sec"] = @(total);
            info[@"ads"] = list;
        }
        if ([NSJSONSerialization isValidJSONObject:info])
        {
            NSData *data = [NSJSONSerialization dataWithJSONObject:info options:0 error:nil];
            if (data != nil)
                json = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding] ?: json;
        }
    });
    return CopyString(json);
}

void AdZative_CloseFullscreen(void)
{
    Post(^{
        [activeFullscreen close];
    });
}

/// Next ad of the pod on screen now, whatever its countdown (the host's own Next control).
/// Result: "pod_next" (trigger "host"), or "next_ignored" (reason "no_fullscreen" / "last_ad").
void AdZative_NextFullscreen(void)
{
    Post(^{
        if (activeFullscreen == nil)
            Emit(@"", @"next_ignored", Payload(@{ @"reason" : @"no_fullscreen" }));
        else if (![activeFullscreen skipToNext])
            Emit([activeFullscreen slotId], @"next_ignored", Payload(@{ @"reason" : @"last_ad" }));
    });
}

// The three setters below are kept for every later show of the slot (until configureSlot replaces it) and apply at once
// when the slot is on screen. Unknown slot: ignored. Defaults (AUTO, alpha 1, countdown -1) = plugin behaviour.

static void UpdateControls(NSString *slotId, void (^change)(AdZativeFullscreenSlot *slot))
{
    Post(^{
        AdZativeFullscreenSlot *slot = fullscreenSlots[slotId];
        if (slot == nil)
            return;
        change(slot);
        if (activeFullscreen != nil && [[activeFullscreen slotId] isEqualToString:slotId])
            [slot applyControls:activeFullscreen];
    });
}

void AdZative_SetFullscreenButtons(const char *slotId, const char *closeMode, const char *nextMode)
{
    NSString *close = Str(closeMode), *next = Str(nextMode);
    UpdateControls(Str(slotId), ^(AdZativeFullscreenSlot *slot) {
        slot.closeMode = AdZativeParseButtonMode(close, AdZativeButtonModeAuto);
        slot.nextMode = AdZativeParseButtonMode(next, AdZativeButtonModeAuto);
    });
}

void AdZative_SetFullscreenButtonAlpha(const char *slotId, float closeAlpha, float nextAlpha)
{
    UpdateControls(Str(slotId), ^(AdZativeFullscreenSlot *slot) {
        slot.closeAlpha = Clamp01(closeAlpha);
        slot.nextAlpha = Clamp01(nextAlpha);
    });
}

void AdZative_SetFullscreenCountdown(const char *slotId, int secondsLeft, float alpha)
{
    UpdateControls(Str(slotId), ^(AdZativeFullscreenSlot *slot) {
        slot.countdownSec = MAX(-1, secondsLeft);
        slot.countdownAlpha = Clamp01(alpha);
    });
}

/// Unity forwards Escape / back here while a fullscreen ad is showing (no back key on iPhone; kept for parity).
void AdZative_HandleBack(void)
{
    Post(^{
        [activeFullscreen handleBack];
    });
}

// ------------------------------------------------------------------ app open

void AdZative_SetAppOpenSuppressed(int suppressed)
{
    appOpenSuppressed = suppressed != 0;
}

void AdZative_SuppressNextForeground(void)
{
    hostSuppressesForeground = YES;
}

static void OnForeground(void)
{
    for (AdZativeInlinePresenter *presenter in inlineSlots.allValues)
        [presenter setPaused:NO];
    BOOL skipped = clickSuppressesForeground || hostSuppressesForeground || appOpenSuppressed || activeFullscreen != nil;
    clickSuppressesForeground = NO;   // both are one-shot
    hostSuppressesForeground = NO;
    Emit(@"", @"app_foreground", Payload(@{ @"app_open_allowed" : @(!skipped) }));
    if (skipped || !sdkReady)
        return;
    for (AdZativeFullscreenSlot *slot in fullscreenSlots.allValues)
    {
        AdZativeSlotConfig *config = slot.config;
        if (config.format != AdZativeFormatAppOpen || !config.autoShowOnForeground)
            continue;
        int64_t since = AdZativeNowMs() - lastAppOpenShownMs;
        if (lastAppOpenShownMs != 0 && since < config.minIntervalSec * 1000)
        {
            Emit(config.slotId, @"show_skipped", Payload(@{ @"reason" : @"min_interval" }));
            continue;
        }
        ShowFullscreenInternal(config.slotId, 0);
        break;
    }
}

// ------------------------------------------------------------------ debug

char *AdZative_DumpState(void)
{
    __block NSString *json = @"{}";
    SyncOnMain(^{
        EnsureState();
        NSMutableDictionary *state = [NSMutableDictionary dictionary];
        [inlineSlots enumerateKeysAndObjectsUsingBlock:^(NSString *slotId, AdZativeInlinePresenter *presenter, BOOL *stop) {
            state[slotId] = [presenter debugState];
        }];
        [fullscreenSlots enumerateKeysAndObjectsUsingBlock:^(NSString *slotId, AdZativeFullscreenSlot *slot, BOOL *stop) {
            state[slotId] = [slot.sources hasReady] ? @"READY" : @"EMPTY";
        }];
        state[@"cached_total"] = @(registry != nil ? [registry totalReady] : 0);
        state[@"fullscreen_showing"] = @(activeFullscreen != nil);
        state[@"sdk_ready"] = @(sdkReady);
        state[@"went_background"] = @(wentBackground);
        NSData *data = [NSJSONSerialization dataWithJSONObject:state options:0 error:nil];
        if (data != nil)
            json = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding] ?: json;
    });
    return CopyString(json);
}

float AdZative_GetScale(void)
{
    __block CGFloat scale = 1;
    SyncOnMain(^{
        scale = [AdZativeOverlay pixelScale];
    });
    return (float)scale;
}
