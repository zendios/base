// THE key to "native ads without the game losing focus" (Android: render/OverlayHost).
//
// Every ad (inline and fullscreen) is a plain UIView inside ONE transparent view that sits on top of the Unity view,
// in the same view controller. No view controller is presented, so:
//   - Unity never receives OnApplicationPause / OnApplicationFocus(false) and keeps rendering; music continues unless
//     the game ducks it;
//   - touches outside the ads fall through to Unity (hitTest returns nil for the overlay itself);
//   - inline ads stay BELOW any fullscreen ad, even when added (refreshed) while one is open.

#import "AdZativeCore.h"

NS_ASSUME_NONNULL_BEGIN

@protocol AdZativeOverlayObserver <NSObject>
/// The overlay changed size or safe area (rotation, split view): re-apply geometry.
- (void)overlayDidResize;
@end

@interface AdZativeOverlay : NSObject

@property (nonatomic, readonly) UIView *container;

/// Unity screen pixels per point (UnityView contentScaleFactor, which follows Unity's resolution scaling).
+ (CGFloat)pixelScale;

/// Inline ad: below every fullscreen ad.
- (void)add:(UIView *)view;

/// Fullscreen ad: top layer.
- (void)addTop:(UIView *)view;

- (void)remove:(nullable UIView *)view;

/// Safe area of the screen (notch, Dynamic Island, home indicator), in points.
- (UIEdgeInsets)safeInsets;

- (BOOL)isLandscape;

/// Weakly held.
- (void)addObserver:(id<AdZativeOverlayObserver>)observer;
- (void)removeObserver:(id<AdZativeOverlayObserver>)observer;

@end

NS_ASSUME_NONNULL_END
