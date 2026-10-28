// Star rating view (Android RatingBar): five stars, the last one partly filled. Usable from a .xib (ad_stars).

#import "AdZativeCore.h"

NS_ASSUME_NONNULL_BEGIN

@interface AdZativeStarsView : UIView

/// 0..5.
@property (nonatomic) double rating;
@property (nonatomic, strong) UIColor *fillColor;
@property (nonatomic, strong) UIColor *emptyColor;
/// Height of one star, points (intrinsic size).
@property (nonatomic) CGFloat starSize;

@end

NS_ASSUME_NONNULL_END
