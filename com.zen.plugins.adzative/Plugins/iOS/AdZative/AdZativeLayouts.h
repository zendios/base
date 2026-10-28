// Built-in layouts, built in code with the sizes, colours and texts of the Android res/layout files:
// zen_banner, zen_banner_media, zen_mrec, zen_inter_media_top, zen_inter_media_center, zen_appopen.

#import "AdZativeCore.h"

NS_ASSUME_NONNULL_BEGIN

/// Identifier of the constraint between the top of ad_container and its parent (a .xib layout sets it too).
extern NSString *const AdZativeContainerTopConstraint;

/// Root view of a built-in layout, nil when the name is not built in.
/// @param landscape the landscape variant (Android res/layout-land); layouts without one use the portrait variant
UIView *_Nullable AdZativeBuiltInLayout(NSString *name, BOOL landscape);

/// Host offset (Unity adBannerOffset, containerMarginTopDp): top margin of ad_container. A layout without that
/// constraint is left as is.
void AdZativeSetContainerMarginTop(UIView *root, CGFloat marginTop);

NS_ASSUME_NONNULL_END
