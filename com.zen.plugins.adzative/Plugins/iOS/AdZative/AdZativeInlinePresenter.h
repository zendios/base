// An inline slot (Native Banner / Native MREC) placed either in a RECT sent from a Unity Canvas (normalised 0..1,
// origin top-left, layout stretched to it) or at an ANCHOR with a size in points (Android: render/InlineSlotPresenter).
//
// While visible, the next ad is preloaded (same or another ad unit per source policy) and swapped in without a blank
// frame. Refresh counts VISIBLE + FOREGROUND time only; a playing video is never cut (the swap waits for its end, at
// most its length + 5 s); no ready ad -> keep the current one and retry.
//
// Video in small slots (VideoMode): ALLOW shows it, BLOCK never takes video ads, GATE holds the video ad and emits
// "video_gate" so Unity can enlarge the slot (acceptVideo) or refuse it (rejectVideo). replaceAd drops the current ad
// and swaps in the next.

#import "AdZativeOverlay.h"
#import "AdZativeSlotSources.h"

NS_ASSUME_NONNULL_BEGIN

/// AdZativePosition of Unity.
typedef NS_ENUM(NSInteger, AdZativeAnchor) {
    AdZativeAnchorTop = 0,
    AdZativeAnchorBottom = 1,
    AdZativeAnchorTopLeft = 2,
    AdZativeAnchorTopRight = 3,
    AdZativeAnchorBottomLeft = 4,
    AdZativeAnchorBottomRight = 5,
    AdZativeAnchorCenter = 6,
    AdZativeAnchorCustom = 7,
};

@interface AdZativeInlinePresenter : NSObject <AdZativePoolListener, AdZativeOverlayObserver>

- (instancetype)initWithHost:(AdZativeOverlay *)host
                      config:(AdZativeSlotConfig *)config
                    registry:(AdZativePoolRegistry *)registry
                        sink:(AdZativeSink)sink
                   clickHook:(AdZativeClickHook)clickHook NS_DESIGNATED_INITIALIZER;
- (instancetype)init NS_UNAVAILABLE;

/// manualLoad: one host-driven request (see AdZativeSlotSources requestUnit).
- (void)requestUnit:(NSString *)adUnitId;

/// An ad is on screen or cached.
- (BOOL)isReady;

/// A NEXT ad is cached (not counting the one on screen).
- (BOOL)hasReadyAd;

- (void)setRectX:(float)nx y:(float)ny width:(float)nw height:(float)nh;

/// width / height <= 0: default size (banner 320 x 64, full width at Top / Bottom; MREC 300 x 250).
- (void)setAnchor:(AdZativeAnchor)anchor x:(float)xDp y:(float)yDp width:(float)wDp height:(float)hDp;

/// NO when no ad is available yet (or a video ad waits at the GATE): the slot keeps preloading and shows itself as soon
/// as an ad is ready (until hide / destroy).
- (BOOL)show;

- (void)hide;

/// App background / foreground: no refresh off-screen.
- (void)setPaused:(BOOL)paused;

/// A fullscreen ad covers the screen: display time does not count and the slot does not refresh (a new ad attached
/// behind the fullscreen would record an impression nobody saw).
- (void)setCovered:(BOOL)covered;

/// Re-binds the current ad (no new request).
- (void)setLayout:(NSString *)name;
- (void)setLayoutMode:(AdZativeLayoutMode)mode;

/// 0 = off, otherwise at least 30 s; applies to the ad on screen right away.
- (void)setRefreshSec:(NSInteger)seconds;

- (void)setVideoMode:(AdZativeVideoMode)mode;
- (void)acceptVideo;
- (void)rejectVideo;
- (void)replaceAd:(BOOL)allowVideo;

/// dumpState snapshot.
- (NSString *)debugState;

- (void)destroy;

@end

NS_ASSUME_NONNULL_END
