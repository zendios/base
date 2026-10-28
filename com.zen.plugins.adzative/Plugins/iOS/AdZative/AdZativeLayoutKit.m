#import "AdZativeLayoutKit.h"

#import "AdZativeLayouts.h"

UIColor *AdZativeColor(uint32_t argb)
{
    return [UIColor colorWithRed:((argb >> 16) & 0xFF) / 255.0
                           green:((argb >> 8) & 0xFF) / 255.0
                            blue:(argb & 0xFF) / 255.0
                           alpha:((argb >> 24) & 0xFF) / 255.0];
}

@implementation AdZativeLabel

- (void)setText:(NSString *)text
{
    [super setText:_allCaps ? text.uppercaseString : text];
}

- (void)setPadding:(UIEdgeInsets)padding
{
    _padding = padding;
    [self invalidateIntrinsicContentSize];
}

- (void)setMinimumSize:(CGSize)minimumSize
{
    _minimumSize = minimumSize;
    [self invalidateIntrinsicContentSize];
}

- (CGRect)textRectForBounds:(CGRect)bounds limitedToNumberOfLines:(NSInteger)numberOfLines
{
    CGRect text = [super textRectForBounds:UIEdgeInsetsInsetRect(bounds, _padding) limitedToNumberOfLines:numberOfLines];
    return UIEdgeInsetsInsetRect(text, UIEdgeInsetsMake(-_padding.top, -_padding.left, -_padding.bottom, -_padding.right));
}

- (void)drawTextInRect:(CGRect)rect
{
    [super drawTextInRect:UIEdgeInsetsInsetRect(rect, _padding)];
}

- (void)setMargins:(UIEdgeInsets)margins
{
    _margins = margins;
    [self invalidateIntrinsicContentSize];
}

- (UIEdgeInsets)alignmentRectInsets
{
    return UIEdgeInsetsMake(-_margins.top, -_margins.left, -_margins.bottom, -_margins.right);
}

/// In alignment-rect units: Android minWidth / minHeight apply to the view, the margins come on top.
- (CGSize)intrinsicContentSize
{
    CGSize size = [super intrinsicContentSize];
    return CGSizeMake(MAX(size.width, _minimumSize.width) + _margins.left + _margins.right,
                      MAX(size.height, _minimumSize.height) + _margins.top + _margins.bottom);
}

@end

// Badge, CTA and stars resist below the fixed sizes (999): when a row is too narrow, Android keeps the 60 dp icon and
// measures the CTA at most the remaining width; iOS must break these, not the icon.
static const UILayoutPriority ContentPriority = UILayoutPriorityRequired - 2;

AdZativeLabel *AdZativeText(NSString *viewId, CGFloat size, BOOL bold, UIColor *color, NSInteger lines)
{
    AdZativeLabel *label = [[AdZativeLabel alloc] init];
    label.translatesAutoresizingMaskIntoConstraints = NO;
    label.accessibilityIdentifier = viewId;
    label.font = bold ? [UIFont boldSystemFontOfSize:size] : [UIFont systemFontOfSize:size];
    label.textColor = color;
    label.numberOfLines = lines;
    label.lineBreakMode = NSLineBreakByTruncatingTail;
    return label;
}

void AdZativeAndroidLineHeight(AdZativeLabel *label, CGFloat textSize)
{
    label.minimumSize = CGSizeMake(label.minimumSize.width, round(textSize * 1.327) + label.padding.top + label.padding.bottom);
    [label setContentCompressionResistancePriority:UILayoutPriorityRequired - 1 forAxis:UILayoutConstraintAxisVertical];
}

AdZativeLabel *AdZativeBadge(void)
{
    AdZativeLabel *badge = AdZativeText(AdZativeIdAttribution, 10, YES, AdZativeColor(0xFF202124), 1);
    badge.text = @"Ad";
    badge.textAlignment = NSTextAlignmentCenter;
    badge.backgroundColor = AdZativeColor(0xFFFBBC04);
    badge.layer.cornerRadius = 3;
    badge.layer.masksToBounds = YES;
    badge.padding = UIEdgeInsetsMake(0, 4, 0, 4);
    badge.minimumSize = CGSizeMake(16, 16);
    [badge setContentHuggingPriority:ContentPriority forAxis:UILayoutConstraintAxisHorizontal];
    [badge setContentCompressionResistancePriority:ContentPriority forAxis:UILayoutConstraintAxisHorizontal];
    return badge;
}

AdZativeLabel *AdZativeCta(CGFloat textSize, BOOL allCaps, UIColor *background, CGFloat radius)
{
    AdZativeLabel *cta = AdZativeText(AdZativeIdCallToAction, textSize, YES, UIColor.whiteColor, 1);
    cta.allCaps = allCaps;
    cta.textAlignment = NSTextAlignmentCenter;
    cta.lineBreakMode = NSLineBreakByClipping;
    cta.backgroundColor = background;
    cta.layer.cornerRadius = radius;
    cta.layer.masksToBounds = YES;
    cta.padding = UIEdgeInsetsMake(0, 12, 0, 12);
    [cta setContentHuggingPriority:ContentPriority forAxis:UILayoutConstraintAxisHorizontal];
    [cta setContentCompressionResistancePriority:ContentPriority forAxis:UILayoutConstraintAxisHorizontal];
    return cta;
}

UIImageView *AdZativeImage(NSString *viewId, CGFloat width, CGFloat height, UIViewContentMode mode)
{
    UIImageView *image = [[UIImageView alloc] init];
    image.translatesAutoresizingMaskIntoConstraints = NO;
    image.accessibilityIdentifier = viewId;
    image.contentMode = mode;
    image.clipsToBounds = YES;
    AdZativeSize(image, width, height);
    return image;
}

AdZativeStarsView *AdZativeStars(UIColor *fill, UIColor *empty)
{
    AdZativeStarsView *stars = [[AdZativeStarsView alloc] initWithFrame:CGRectZero];
    stars.translatesAutoresizingMaskIntoConstraints = NO;
    stars.accessibilityIdentifier = AdZativeIdStars;
    stars.fillColor = fill;
    stars.emptyColor = empty;
    [stars setContentHuggingPriority:ContentPriority forAxis:UILayoutConstraintAxisHorizontal];
    [stars setContentCompressionResistancePriority:ContentPriority forAxis:UILayoutConstraintAxisHorizontal];
    return stars;
}

GADMediaView *AdZativeMediaView(void)
{
    GADMediaView *media = [[GADMediaView alloc] init];
    media.translatesAutoresizingMaskIntoConstraints = NO;
    media.accessibilityIdentifier = AdZativeIdMedia;
    media.contentMode = UIViewContentModeScaleAspectFit;
    media.clipsToBounds = YES;
    return media;
}

GADNativeAdView *AdZativeNativeView(void)
{
    GADNativeAdView *view = [[GADNativeAdView alloc] initWithFrame:CGRectMake(0, 0, 320, 64)];
    view.accessibilityIdentifier = AdZativeIdNativeView;
    view.backgroundColor = UIColor.clearColor;
    return view;
}

static UIStackView *Stack(NSArray<UIView *> *views, UILayoutConstraintAxis axis, CGFloat spacing, UIStackViewAlignment alignment)
{
    UIStackView *stack = [[UIStackView alloc] initWithArrangedSubviews:views];
    stack.translatesAutoresizingMaskIntoConstraints = NO;
    stack.axis = axis;
    stack.spacing = spacing;
    stack.alignment = alignment;
    stack.distribution = UIStackViewDistributionFill;
    // Layout margins are Android margins / paddings: exact dp. By default iOS adds the safe area to them (a landscape
    // banner under the Dynamic Island got its marginStart 4 turned into 31).
    stack.insetsLayoutMarginsFromSafeArea = NO;
    return stack;
}

UIStackView *AdZativeRow(NSArray<UIView *> *views, CGFloat spacing, UIStackViewAlignment alignment)
{
    return Stack(views, UILayoutConstraintAxisHorizontal, spacing, alignment);
}

UIStackView *AdZativeColumn(NSArray<UIView *> *views, CGFloat spacing, UIStackViewAlignment alignment)
{
    return Stack(views, UILayoutConstraintAxisVertical, spacing, alignment);
}

void AdZativeFlexible(UIView *view, UILayoutConstraintAxis axis)
{
    [view setContentHuggingPriority:1 forAxis:axis];
    [view setContentCompressionResistancePriority:UILayoutPriorityDefaultLow - 1 forAxis:axis];
}

void AdZativeSize(UIView *view, CGFloat width, CGFloat height)
{
    NSMutableArray<NSLayoutConstraint *> *constraints = [NSMutableArray array];
    if (width > 0)
        [constraints addObject:[view.widthAnchor constraintEqualToConstant:width]];
    if (height > 0)
        [constraints addObject:[view.heightAnchor constraintEqualToConstant:height]];
    for (NSLayoutConstraint *constraint in constraints)
        constraint.priority = UILayoutPriorityRequired - 1;
    [NSLayoutConstraint activateConstraints:constraints];
}

void AdZativePin(UIView *view, UIView *parent, UIEdgeInsets insets)
{
    view.translatesAutoresizingMaskIntoConstraints = NO;
    if (view.superview != parent)
        [parent addSubview:view];
    [NSLayoutConstraint activateConstraints:@[
        [view.topAnchor constraintEqualToAnchor:parent.topAnchor constant:insets.top],
        [view.leadingAnchor constraintEqualToAnchor:parent.leadingAnchor constant:insets.left],
        [view.trailingAnchor constraintEqualToAnchor:parent.trailingAnchor constant:-insets.right],
        [view.bottomAnchor constraintEqualToAnchor:parent.bottomAnchor constant:-insets.bottom],
    ]];
}

void AdZativePinTop(UIView *view, UIView *parent, UIEdgeInsets insets)
{
    view.translatesAutoresizingMaskIntoConstraints = NO;
    if (view.superview != parent)
        [parent addSubview:view];
    [NSLayoutConstraint activateConstraints:@[
        [view.topAnchor constraintEqualToAnchor:parent.topAnchor constant:insets.top],
        [view.leadingAnchor constraintEqualToAnchor:parent.leadingAnchor constant:insets.left],
        [view.trailingAnchor constraintEqualToAnchor:parent.trailingAnchor constant:-insets.right],
    ]];
    // Below the fixed sizes (999): a parent shorter than the content clips it (AdZativeClip), as Android does.
    NSLayoutConstraint *bottom = [view.bottomAnchor constraintLessThanOrEqualToAnchor:parent.bottomAnchor constant:-insets.bottom];
    bottom.priority = UILayoutPriorityRequired - 2;
    bottom.active = YES;
}

UIView *AdZativeFill(UILayoutConstraintAxis axis)
{
    UIView *fill = [[UIView alloc] init];
    fill.translatesAutoresizingMaskIntoConstraints = NO;
    NSLayoutDimension *length = axis == UILayoutConstraintAxisHorizontal ? fill.widthAnchor : fill.heightAnchor;
    NSLayoutConstraint *zero = [length constraintEqualToConstant:0];
    zero.priority = 2;
    [NSLayoutConstraint activateConstraints:@[ zero, [length constraintGreaterThanOrEqualToConstant:0] ]];
    return fill;
}

UIView *AdZativeClip(UIView *parent, UIEdgeInsets padding)
{
    UIView *clip = [[UIView alloc] init];
    clip.clipsToBounds = YES;
    AdZativePin(clip, parent, padding);
    return clip;
}

UIView *AdZativeContainer(UIView *root, uint32_t background, CGFloat marginTop)
{
    UIView *container = [[UIView alloc] init];
    container.translatesAutoresizingMaskIntoConstraints = NO;
    container.accessibilityIdentifier = AdZativeIdContainer;
    container.backgroundColor = AdZativeColor(background);
    [root addSubview:container];
    NSLayoutConstraint *top = [container.topAnchor constraintEqualToAnchor:root.topAnchor constant:marginTop];
    top.identifier = AdZativeContainerTopConstraint;
    [NSLayoutConstraint activateConstraints:@[
        top,
        [container.leadingAnchor constraintEqualToAnchor:root.leadingAnchor],
        [container.trailingAnchor constraintEqualToAnchor:root.trailingAnchor],
        [container.bottomAnchor constraintEqualToAnchor:root.bottomAnchor],
    ]];
    return container;
}

static NSString *const MediaAspectConstraint = @"media_aspect";
static NSString *const FrameAspectConstraint = @"media_frame_aspect";
static const NSInteger MediaFrameHugTag = 1;   // AdZativeMediaAlignHug: AdZativeSetMediaAspect sizes the frame too
static const CGFloat MediaMinSize = 120;   // AdMob: a MediaView with video is at least 120 x 120

UIView *AdZativeMediaFrame(GADMediaView *media, AdZativeMediaAlign align)
{
    UIView *frame = [[UIView alloc] init];
    frame.translatesAutoresizingMaskIntoConstraints = NO;
    frame.restorationIdentifier = AdZativeTagMediaFrame;
    [frame addSubview:media];
    // The media's own (image) size must not compete with the frame's.
    AdZativeFlexible(media, UILayoutConstraintAxisHorizontal);
    AdZativeFlexible(media, UILayoutConstraintAxisVertical);
    // The frame takes the free space of its stack (weight 1), before a fill spacer (priority 2), whatever the media's size.
    NSLayoutConstraint *growWidth = [frame.widthAnchor constraintEqualToConstant:10000];
    growWidth.priority = 5;
    NSLayoutConstraint *growHeight = [frame.heightAnchor constraintEqualToConstant:10000];
    growHeight.priority = 5;
    // As wide as the frame, else as tall: with the aspect ratio set, the one that fits wins (Android fitCenter). Below
    // the frame's growth, so the media follows the frame, not the other way round.
    NSLayoutConstraint *wide = [media.widthAnchor constraintEqualToAnchor:frame.widthAnchor];
    wide.priority = 4;
    NSLayoutConstraint *tall = [media.heightAnchor constraintEqualToAnchor:frame.heightAnchor];
    tall.priority = 3;
    [NSLayoutConstraint activateConstraints:@[
        [media.centerXAnchor constraintEqualToAnchor:frame.centerXAnchor],
        [media.centerYAnchor constraintEqualToAnchor:frame.centerYAnchor],
        [media.widthAnchor constraintLessThanOrEqualToAnchor:frame.widthAnchor],
        [media.heightAnchor constraintLessThanOrEqualToAnchor:frame.heightAnchor],
        wide, tall, growWidth, growHeight,
    ]];
    // At least 120 x 120 (the aspect ratio gives way: a narrow video gets bars from the player inside).
    NSLayoutConstraint *minWidth = [media.widthAnchor constraintGreaterThanOrEqualToConstant:MediaMinSize];
    minWidth.priority = UILayoutPriorityRequired - 1;
    NSLayoutConstraint *minHeight = [media.heightAnchor constraintGreaterThanOrEqualToConstant:MediaMinSize];
    minHeight.priority = UILayoutPriorityRequired - 1;
    [NSLayoutConstraint activateConstraints:@[ minWidth, minHeight ]];
    if (align == AdZativeMediaAlignHug)
    {
        frame.tag = MediaFrameHugTag;
        NSLayoutConstraint *minFrame = [frame.heightAnchor constraintGreaterThanOrEqualToConstant:MediaMinSize];
        minFrame.priority = UILayoutPriorityRequired - 1;
        minFrame.active = YES;
    }
    return frame;
}

void AdZativeSetMediaAspect(GADMediaView *media, CGFloat aspect)
{
    UIView *frame = media.superview;
    for (NSLayoutConstraint *constraint in [media.constraints arrayByAddingObjectsFromArray:frame.constraints])
    {
        if ([constraint.identifier isEqualToString:MediaAspectConstraint] ||
            [constraint.identifier isEqualToString:FrameAspectConstraint])
            constraint.active = NO;
    }
    if (aspect <= 0)
        return;
    NSLayoutConstraint *ratio = [media.widthAnchor constraintEqualToAnchor:media.heightAnchor multiplier:aspect];
    ratio.identifier = MediaAspectConstraint;
    ratio.priority = UILayoutPriorityRequired - 2;   // below the 120 x 120 minimum
    ratio.active = YES;
    // Hug: the frame as tall as its width at the creative's ratio, above its growth (5), so a short creative leaves the
    // free height to the stack (Android MediaFrame zenHugHeight). Taken from the frame's own width, not from the media
    // view: with the frame tied to GADMediaView's height the app stalled on the iPhone once the ad was shown.
    if (frame.tag != MediaFrameHugTag)
        return;
    NSLayoutConstraint *hug = [frame.heightAnchor constraintEqualToAnchor:frame.widthAnchor multiplier:1 / aspect];
    hug.identifier = FrameAspectConstraint;
    hug.priority = 6;
    hug.active = YES;
}
