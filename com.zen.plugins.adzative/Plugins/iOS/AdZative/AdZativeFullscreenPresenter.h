// Native Interstitial / Native App Open / Native Video Rewarded, a full-screen view on the overlay (no view controller
// is presented, so Unity keeps running) (Android: render/FullscreenPresenter).
//
// Countdown per ad: see AdZativeWatchTime. POD: 1..3 ads in a row, one per distinct ad unit ID, "Ad i of n".
//   Interstitial: "Next ›" when allowed; ✕ on the LAST ad (or on every ad with podCloseOnEveryAd).
//   Rewarded: each ad must be watched (its countdown, or video end) before Next; the reward comes after the last ad.
//             A click on the ad counts as watched (reward at once, reason "click"). Closing earlier gives no reward.
// Ads of the pod that were never reached go back to the cache. The host can drive ✕ / Next / the countdown chip
// (setButtons / setButtonAlpha / setCountdown, skipToNext).

#import "AdZativeOverlay.h"
#import "AdZativeSlotSources.h"

NS_ASSUME_NONNULL_BEGIN

@class AdZativeFullscreenPresenter;

typedef void (^AdZativeFullscreenClosed)(AdZativeFullscreenPresenter *presenter);

@interface AdZativeFullscreenPresenter : NSObject

/// @param pod the ads, already taken from the slot's pools: the presenter owns them now
- (instancetype)initWithHost:(AdZativeOverlay *)host
                      config:(AdZativeSlotConfig *)config
                     sources:(AdZativeSlotSources *)sources
                         pod:(NSArray<AdZativeTaken *> *)pod
                   requested:(NSInteger)requested
                      layout:(NSString *)layout
                        sink:(AdZativeSink)sink
                    onClosed:(AdZativeFullscreenClosed)onClosed
                   clickHook:(AdZativeClickHook)clickHook NS_DESIGNATED_INITIALIZER;
- (instancetype)init NS_UNAVAILABLE;

- (NSString *)slotId;

/// Host control of ✕ and Next; applies at once to the ad on screen.
- (void)setButtonsClose:(AdZativeButtonMode)closeMode next:(AdZativeButtonMode)nextMode;

/// Host alpha (0..1) of ✕ ("Continue to app" on app open) and of Next.
- (void)setButtonAlphaClose:(float)closeAlpha next:(float)nextAlpha;

/// Host-drawn countdown chip: secondsLeft >= 1 shows "Ns" at alpha, 0 hides it, -1 = the plugin's own countdown.
- (void)setCountdown:(NSInteger)secondsLeft alpha:(float)alpha;

/// Host action: the next ad of the pod now, whatever the countdown or button mode. NO on the last ad.
- (BOOL)skipToNext;

- (void)show;

/// Back / Escape forwarded from Unity: what the visible controls allow. YES if consumed.
- (BOOL)handleBack;

- (void)close;

@end

NS_ASSUME_NONNULL_END
