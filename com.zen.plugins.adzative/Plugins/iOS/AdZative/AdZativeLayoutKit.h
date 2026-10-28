// Building blocks of the built-in layouts (AdZativeLayouts*.m): Android LinearLayout = UIStackView (a hidden arranged
// view collapses like View.GONE), layout_weight = low hugging / compression, dp = pt, sp = pt.

#import <GoogleMobileAds/GoogleMobileAds.h>

#import "AdZativeLayoutRegistry.h"
#import "AdZativeStarsView.h"

NS_ASSUME_NONNULL_BEGIN

/// 0xAARRGGBB, as in the Android XML.
UIColor *AdZativeColor(uint32_t argb);

/// UILabel with padding, a minimum size and ALL CAPS (Android TextView padding / minWidth / textAllCaps).
@interface AdZativeLabel : UILabel
@property (nonatomic) UIEdgeInsets padding;
@property (nonatomic) CGSize minimumSize;
@property (nonatomic) BOOL allCaps;
/// Android layout_margin: outside the drawn frame, part of the alignment rect, so it collapses with the view when hidden.
@property (nonatomic) UIEdgeInsets margins;
@end

AdZativeLabel *AdZativeText(NSString *_Nullable viewId, CGFloat size, BOOL bold, UIColor *color, NSInteger lines);

/// At least the height of a one-line Android TextView (includeFontPadding, Roboto: about 1.327 x the text size) plus
/// the label's vertical padding. SF lines are shorter and would lift everything below them.
void AdZativeAndroidLineHeight(AdZativeLabel *label, CGFloat textSize);

/// ad_attribution, style ZenAdBadge: "Ad", 10 bold, #202124 on #FBBC04, radius 3, at least 16 x 16.
AdZativeLabel *AdZativeBadge(void);

/// ad_call_to_action, style ZenCta: centered bold white single line (no ellipsis: Meta wants the full CTA), radius 8.
AdZativeLabel *AdZativeCta(CGFloat textSize, BOOL allCaps, UIColor *background, CGFloat radius);

UIImageView *AdZativeImage(NSString *_Nullable viewId, CGFloat width, CGFloat height, UIViewContentMode mode);

/// ad_stars: ratingBarStyleSmall, 5 cells of 16 without gaps (80 x 16).
AdZativeStarsView *AdZativeStars(UIColor *fill, UIColor *empty);

/// ad_media.
GADMediaView *AdZativeMediaView(void);

/// ad_native_view, the root of every inline layout (frame-sized by the presenter).
GADNativeAdView *AdZativeNativeView(void);

UIStackView *AdZativeRow(NSArray<UIView *> *views, CGFloat spacing, UIStackViewAlignment alignment);
UIStackView *AdZativeColumn(NSArray<UIView *> *views, CGFloat spacing, UIStackViewAlignment alignment);

/// layout_weight="1" along axis: takes the free space, shrinks first. Only for views with an intrinsic size (labels):
/// a UIStackView has none, so hugging set on it does nothing.
void AdZativeFlexible(UIView *view, UILayoutConstraintAxis axis);

/// An empty weight="1" view: 0 long at priority 2, never negative. Takes the free space only when nothing else can
/// (a label's hugging is 250, a Flexible label's 1), like the empty end of an Android LinearLayout.
UIView *AdZativeFill(UILayoutConstraintAxis axis);

/// Android clipToPadding: a view pinned inside parent at padding that clips its content to it.
UIView *AdZativeClip(UIView *parent, UIEdgeInsets padding);

typedef NS_ENUM(NSInteger, AdZativeMediaAlign) {
    AdZativeMediaAlignCenter,
    AdZativeMediaAlignHug,      // the frame as tall as the media: the stack around it places both
};

/// The area ad_media takes in a layout (as big as the layout leaves); ad_media sits inside it at the creative's aspect
/// ratio (AdZativeSetMediaAspect), as large as fits: the whole creative, no crop, no bars. Without an aspect ratio
/// ad_media fills the area.
UIView *AdZativeMediaFrame(GADMediaView *media, AdZativeMediaAlign align);

/// ad_media's aspect ratio (width / height) inside its media frame; 0 = fill the frame.
void AdZativeSetMediaAspect(GADMediaView *media, CGFloat aspect);

/// Fixed width / height (<= 0: unset), priority 999 so a hidden view in a stack can still collapse.
void AdZativeSize(UIView *view, CGFloat width, CGFloat height);

/// All four edges at insets.
void AdZativePin(UIView *view, UIView *parent, UIEdgeInsets insets);

/// Top, leading, trailing at insets; the bottom may stay above the parent's (Android gravity top, wrap_content). Content
/// taller than the parent overflows (to be clipped) instead of squeezing fixed sizes, as on Android.
void AdZativePinTop(UIView *view, UIView *parent, UIEdgeInsets insets);

/// Adds the ad_container view to root, edges pinned, top at marginTop (constraint AdZativeContainerTopConstraint).
UIView *AdZativeContainer(UIView *root, uint32_t background, CGFloat marginTop);

// Fullscreen layouts (AdZativeLayoutsFullscreen.m).
UIView *AdZativeInterstitialLayout(BOOL mediaCenter, BOOL landscape);
UIView *AdZativeAppOpenLayout(BOOL landscape);

NS_ASSUME_NONNULL_END
