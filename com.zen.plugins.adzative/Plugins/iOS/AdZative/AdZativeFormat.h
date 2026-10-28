// The five native presentations (Android: core/AdFormat). Layouts are resolved by name, see AdZativeLayoutRegistry.

#import "AdZativeCore.h"

NS_ASSUME_NONNULL_BEGIN

/// Names match the Unity AdZativeFormat enum and the Android AdFormat enum.
typedef NS_ENUM(NSInteger, AdZativeFormat) {
    AdZativeFormatBanner,
    AdZativeFormatMrec,
    AdZativeFormatInterstitial,
    AdZativeFormatAppOpen,
    AdZativeFormatRewarded,
};

/// "BANNER" ... "REWARDED", case-insensitive. NO for an unknown name.
BOOL AdZativeFormatParse(NSString *_Nullable name, AdZativeFormat *format);

NSString *AdZativeFormatName(AdZativeFormat format);

BOOL AdZativeFormatIsFullscreen(AdZativeFormat format);

/// Rewarded plays with sound (the user opted in); everything else starts muted.
BOOL AdZativeFormatDefaultStartMuted(AdZativeFormat format);

/// The layout must contain a GADMediaView (ad_media).
BOOL AdZativeFormatRequiresMedia(AdZativeFormat format);

/// Built-in layouts of the format: style 1..n. Rewarded reuses the interstitial layouts.
NSArray<NSString *> *AdZativeFormatLayouts(AdZativeFormat format);

NSString *AdZativeFormatDefaultLayout(AdZativeFormat format);

/// Built-in layout of style n (1-based); out-of-range styles fall back to style 1.
NSString *AdZativeFormatLayout(AdZativeFormat format, NSInteger style);

NS_ASSUME_NONNULL_END
