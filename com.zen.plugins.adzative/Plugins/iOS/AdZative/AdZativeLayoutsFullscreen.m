// Fullscreen layouts: res/layout(-land)/zen_inter_media_top.xml, zen_inter_media_center.xml, zen_appopen.xml.
//
// Dark full-screen root (app open: a light card over the dimmed game). The native ad view and every asset sit in the
// safe area: Google ads draw AdChoices in its corner (preferredAdChoicesPosition), under the screen's rounded corner on
// a full-screen view, and ignore a GADAdChoicesView (measured); the AdMob validator rejects assets outside the native
// ad view ("Advertiser assets outside native ad view"). The media shows the whole creative at its own aspect ratio
// (AdZativeMediaFrame) on the dark root. The controls are OUTSIDE the native ad view: AdMob forbids own click
// handlers within it.

#import "AdZativeLayoutKit.h"

static const uint32_t FullscreenBackground = 0xFF0E0E10;
static const uint32_t TextPrimary = 0xFFFFFFFF;
static const uint32_t TextSecondary = 0xB3FFFFFF;
static const uint32_t ChipBackground = 0x80000000;   // dark: readable on a light creative under the top bar
static const uint32_t MediaPlaceholder = 0xFF202124;
static const uint32_t Star = 0xFFFBBC04;
static const uint32_t CtaBlue = 0xFF1A73E8;
static const uint32_t CtaGreen = 0xFF188038;   // white text 5.0:1 (WCAG AA)
static const uint32_t BarBackground = 0xFFFFFFFF;
static const uint32_t BarText = 0xFF202124;
static const uint32_t BarLabel = 0xFF5F6368;
static const uint32_t BarAction = 0xFF1A73E8;
// App open card (zen_appopen.xml): light, like the inline cards.
static const uint32_t AppOpenScrim = 0x66000000;
static const uint32_t AppOpenCard = 0xFFFFFFFF;
static const uint32_t AppOpenTextPrimary = 0xFF1F1F1F;
static const uint32_t AppOpenTextSecondary = 0xFF5F6368;
static const uint32_t AppOpenMediaBackground = 0xFFF1F3F4;
static const uint32_t AppOpenStarEmpty = 0x33000000;

static const CGFloat TopBarHeight = 42;   // zen_top_bar_height
static const CGFloat CloseSize = 28;      // zen_close_size
static const CGFloat CtaHeight = 52;      // zen_fs_cta_height
static const CGFloat AppOpenBarTop = 8;   // "Continue to app" bar below the safe area's top
static const CGFloat AppOpenCardTop = 52; // the card below the bar (32) with a gap of 12

typedef NS_ENUM(NSInteger, MaterialIcon) {
    MaterialIconClose,
    MaterialIconChevronRight,
};

/// Material icons of the Android drawables (ic_close, ic_chevron_right), 24 x 24 viewport.
static UIImage *IconImage(MaterialIcon icon, CGFloat size, UIColor *color)
{
    UIGraphicsImageRenderer *renderer = [[UIGraphicsImageRenderer alloc] initWithSize:CGSizeMake(size, size)];
    return [renderer imageWithActions:^(UIGraphicsImageRendererContext *context) {
        CGFloat k = size / 24;
        UIBezierPath *path = [UIBezierPath bezierPath];
        CGFloat closePoints[] = { 19, 6.41, 17.59, 5, 12, 10.59, 6.41, 5, 5, 6.41, 10.59, 12, 5, 17.59, 6.41, 19, 12, 13.41,
                                  17.59, 19, 19, 17.59, 13.41, 12 };
        CGFloat chevronPoints[] = { 10, 6, 8.59, 7.41, 13.17, 12, 8.59, 16.59, 10, 18, 16, 12 };
        CGFloat *points = icon == MaterialIconClose ? closePoints : chevronPoints;
        int count = icon == MaterialIconClose ? 12 : 6;
        for (int i = 0; i < count; i++)
        {
            CGPoint point = CGPointMake(points[2 * i] * k, points[2 * i + 1] * k);
            if (i == 0)
                [path moveToPoint:point];
            else
                [path addLineToPoint:point];
        }
        [path closePath];
        [color setFill];
        [path fill];
    }];
}


static AdZativeLabel *Line(AdZativeLabel *label, CGFloat textSize)
{
    AdZativeAndroidLineHeight(label, textSize);
    return label;
}

static UIView *FullscreenRoot(void)
{
    UIView *root = [[UIView alloc] initWithFrame:UIScreen.mainScreen.bounds];
    root.accessibilityIdentifier = @"zen_fullscreen_root";
    root.backgroundColor = AdZativeColor(FullscreenBackground);
    root.autoresizingMask = UIViewAutoresizingFlexibleWidth | UIViewAutoresizingFlexibleHeight;
    return root;
}

/// The native ad view inside the safe area, top below it (landscape interstitials: the top bar's height, so its
/// controls sit above the ad, not over its assets; Android layout-land marginTop).
static GADNativeAdView *FullscreenAdView(UIView *root, CGFloat top)
{
    GADNativeAdView *adView = [[GADNativeAdView alloc] init];
    adView.translatesAutoresizingMaskIntoConstraints = NO;
    adView.accessibilityIdentifier = AdZativeIdNativeView;
    [root addSubview:adView];
    UILayoutGuide *safe = root.safeAreaLayoutGuide;
    [NSLayoutConstraint activateConstraints:@[
        [adView.topAnchor constraintEqualToAnchor:safe.topAnchor constant:top],
        [adView.bottomAnchor constraintEqualToAnchor:safe.bottomAnchor],
        [adView.leadingAnchor constraintEqualToAnchor:safe.leadingAnchor],
        [adView.trailingAnchor constraintEqualToAnchor:safe.trailingAnchor],
    ]];
    return adView;
}

static GADMediaView *PlaceholderMedia(void)
{
    GADMediaView *media = AdZativeMediaView();
    media.backgroundColor = AdZativeColor(MediaPlaceholder);
    return media;
}

/// Chip background (zen_bg_chip / zen_bg_circle).
static void Chip(UIView *view, CGFloat radius)
{
    view.backgroundColor = AdZativeColor(ChipBackground);
    view.layer.cornerRadius = radius;
    view.layer.masksToBounds = YES;
}

/// zen_top_bar: pod label | (space) | countdown, Next ›, ✕.
static UIStackView *TopBar(UIView *root)
{
    AdZativeLabel *podLabel = AdZativeText(AdZativeIdPodLabel, 12, NO, AdZativeColor(TextPrimary), 1);
    podLabel.textAlignment = NSTextAlignmentCenter;
    podLabel.padding = UIEdgeInsetsMake(0, 14, 0, 14);
    Chip(podLabel, 12);
    AdZativeSize(podLabel, 0, 24);

    AdZativeLabel *countdown = AdZativeText(AdZativeIdCountdown, 12, NO, AdZativeColor(TextPrimary), 1);
    countdown.textAlignment = NSTextAlignmentCenter;
    Chip(countdown, CloseSize / 2);
    AdZativeSize(countdown, CloseSize, CloseSize);

    AdZativeLabel *nextLabel = AdZativeText(nil, 12, NO, AdZativeColor(TextPrimary), 1);
    nextLabel.text = @"Next";
    UIImageView *chevron = [[UIImageView alloc] initWithImage:IconImage(MaterialIconChevronRight, 24, AdZativeColor(TextPrimary))];
    chevron.translatesAutoresizingMaskIntoConstraints = NO;
    AdZativeSize(chevron, 24, 24);
    UIStackView *nextChip = AdZativeRow(@[ nextLabel, chevron ], 2, UIStackViewAlignmentCenter);
    nextChip.layoutMarginsRelativeArrangement = YES;
    nextChip.layoutMargins = UIEdgeInsetsMake(0, 14, 0, 8);
    Chip(nextChip, 14);
    AdZativeSize(nextChip, 0, 28);
    // zen_next has marginStart 8 and zen_close none: the margin goes with Next when it is hidden.
    UIStackView *next = AdZativeRow(@[ nextChip ], 0, UIStackViewAlignmentCenter);
    next.accessibilityIdentifier = AdZativeIdNext;
    next.layoutMarginsRelativeArrangement = YES;
    next.layoutMargins = UIEdgeInsetsMake(0, 8, 0, 0);

    UIImageView *closeIcon = [[UIImageView alloc] initWithImage:IconImage(MaterialIconClose, CloseSize - 10, AdZativeColor(TextPrimary))];
    closeIcon.contentMode = UIViewContentModeCenter;
    UIView *close = [[UIView alloc] init];
    close.translatesAutoresizingMaskIntoConstraints = NO;
    close.accessibilityIdentifier = AdZativeIdClose;
    close.accessibilityLabel = @"Close ad";
    Chip(close, CloseSize / 2);
    AdZativePin(closeIcon, close, UIEdgeInsetsMake(5, 5, 5, 5));
    AdZativeSize(close, CloseSize, CloseSize);
    close.hidden = YES;

    UIStackView *bar = AdZativeRow(@[ podLabel, AdZativeFill(UILayoutConstraintAxisHorizontal), countdown, next, close ], 0,
                                   UIStackViewAlignmentCenter);
    [bar setCustomSpacing:8 afterView:podLabel];
    bar.accessibilityIdentifier = AdZativeIdTopBar;
    bar.layoutMarginsRelativeArrangement = YES;
    bar.layoutMargins = UIEdgeInsetsMake(0, 12, 0, 12);
    [root addSubview:bar];
    UILayoutGuide *safe = root.safeAreaLayoutGuide;
    [NSLayoutConstraint activateConstraints:@[
        [bar.topAnchor constraintEqualToAnchor:safe.topAnchor],
        [bar.leadingAnchor constraintEqualToAnchor:safe.leadingAnchor],
        [bar.trailingAnchor constraintEqualToAnchor:safe.trailingAnchor],
        [bar.heightAnchor constraintEqualToConstant:TopBarHeight],
    ]];
    return bar;
}

/// Badge, advertiser, stars.
static UIStackView *AttributionLine(void)
{
    return AdZativeRow(@[
        AdZativeBadge(),
        Line(AdZativeText(AdZativeIdAdvertiser, 13, NO, AdZativeColor(TextSecondary), 1), 13),
        AdZativeStars(AdZativeColor(Star), AdZativeColor(0x4DFFFFFF)),
    ], 8, UIStackViewAlignmentCenter);
}

static UIStackView *StoreLine(void)
{
    AdZativeLabel *price = AdZativeText(AdZativeIdPrice, 12, NO, AdZativeColor(TextSecondary), 1);
    price.padding = UIEdgeInsetsMake(0, 8, 0, 0);   // marginStart 8, kept when there is no store
    return AdZativeRow(@[ Line(AdZativeText(AdZativeIdStore, 12, NO, AdZativeColor(TextSecondary), 1), 12), Line(price, 12) ], 0,
                       UIStackViewAlignmentCenter);
}

/// Android wrap_content for a stack whose views may all be hidden (no store / price): a stack has no height of its own,
/// so without this it took the free height meant for the fill spacers (priority 2), measured 80 pt on the iPhone.
static void HugHeight(UIStackView *stack)
{
    NSLayoutConstraint *zero = [stack.heightAnchor constraintEqualToConstant:0];
    zero.priority = 3;
    zero.active = YES;
}

/// Details of the "media top" layout (interstitial style 1): icon + headline / attribution, body,
/// store + price, CTA.
static UIStackView *Details(BOOL landscape)
{
    AdZativeLabel *headline = Line(AdZativeText(AdZativeIdHeadline, 18, YES, AdZativeColor(TextPrimary), 2), 18);
    UIStackView *texts = AdZativeColumn(@[ headline, AttributionLine() ], 4, UIStackViewAlignmentLeading);
    texts.layoutMarginsRelativeArrangement = YES;
    texts.directionalLayoutMargins = NSDirectionalEdgeInsetsMake(0, 12, 0, 0);   // marginStart 12, kept without the icon
    [headline.widthAnchor constraintEqualToAnchor:texts.layoutMarginsGuide.widthAnchor].active = YES;
    CGFloat iconSize = landscape ? 64 : 56;
    UIStackView *header = AdZativeRow(@[ AdZativeImage(AdZativeIdAppIcon, iconSize, iconSize, UIViewContentModeScaleAspectFit), texts ],
                                      0, UIStackViewAlignmentCenter);

    // Body marginTop 12, store row marginTop 6: 6 after the header plus 6 inside the body, so a missing body leaves 6.
    AdZativeLabel *body = AdZativeText(AdZativeIdBody, 14, NO, AdZativeColor(TextSecondary), landscape ? 2 : 3);
    body.padding = UIEdgeInsetsMake(6, 0, 0, 0);
    Line(body, 14);
    UIStackView *store = StoreLine();
    UIStackView *storeRow = AdZativeRow(@[ store, AdZativeFill(UILayoutConstraintAxisHorizontal) ], 0, UIStackViewAlignmentCenter);
    HugHeight(storeRow);

    AdZativeLabel *cta = AdZativeCta(17, NO, AdZativeColor(CtaBlue), 8);
    AdZativeSize(cta, 0, CtaHeight);

    UIStackView *details = AdZativeColumn(@[ header, body, storeRow, cta ], 0, UIStackViewAlignmentFill);
    [details setCustomSpacing:6 afterView:header];
    [details setCustomSpacing:6 afterView:body];
    [details setCustomSpacing:16 afterView:storeRow];
    details.layoutMarginsRelativeArrangement = YES;
    details.layoutMargins = landscape ? UIEdgeInsetsMake(0, 24, 16, 24) : UIEdgeInsetsMake(20, 20, 20, 20);
    return details;
}

/// Media on top (portrait) or on the left, 3 : 2 (landscape), the details next to it.
static void MediaTopContent(GADNativeAdView *adView, BOOL landscape)
{
    GADMediaView *media = PlaceholderMedia();
    UIStackView *details = Details(landscape);
    UIStackView *content;
    // The creative at its own aspect ratio: centred in the left column (landscape), right above the details (portrait).
    UIView *mediaFrame = AdZativeMediaFrame(media, landscape ? AdZativeMediaAlignCenter : AdZativeMediaAlignHug);
    if (landscape)
    {
        // gravity center_vertical: the details centred in the right-hand column.
        UIView *side = [[UIView alloc] init];
        side.translatesAutoresizingMaskIntoConstraints = NO;
        [side addSubview:details];
        [NSLayoutConstraint activateConstraints:@[
            [details.centerYAnchor constraintEqualToAnchor:side.centerYAnchor],
            [details.leadingAnchor constraintEqualToAnchor:side.leadingAnchor],
            [details.trailingAnchor constraintEqualToAnchor:side.trailingAnchor],
            [details.topAnchor constraintGreaterThanOrEqualToAnchor:side.topAnchor],
        ]];
        content = AdZativeRow(@[ mediaFrame, side ], 0, UIStackViewAlignmentFill);
        // weights 3 : 2; not required: a hidden media (no-media ad) gets width 0 from the stack, the side takes all.
        NSLayoutConstraint *ratio = [mediaFrame.widthAnchor constraintEqualToAnchor:side.widthAnchor multiplier:1.5];
        ratio.priority = UILayoutPriorityRequired - 1;
        ratio.active = YES;
    }
    else
    {
        // gravity center_vertical: media + details centred, the free height split above and below. The media frame
        // (weight 1, hugging the media) takes the free height first, up to the creative's aspect ratio.
        UIView *above = AdZativeFill(UILayoutConstraintAxisVertical);
        UIView *below = AdZativeFill(UILayoutConstraintAxisVertical);
        content = AdZativeColumn(@[ above, mediaFrame, details, below ], 0, UIStackViewAlignmentFill);
        [above.heightAnchor constraintEqualToAnchor:below.heightAnchor].active = YES;   // once in the same stack
    }
    AdZativePin(content, adView, UIEdgeInsetsZero);
}

/// Style 2: everything centered, media in the middle (portrait) or on the right (landscape), green CTA.
static void MediaCenterContent(GADNativeAdView *adView, BOOL landscape)
{
    UIImageView *icon = AdZativeImage(AdZativeIdAppIcon, 72, 72, UIViewContentModeScaleAspectFit);
    AdZativeLabel *headline = Line(AdZativeText(AdZativeIdHeadline, 20, YES, AdZativeColor(TextPrimary), 2), 20);
    headline.textAlignment = NSTextAlignmentCenter;
    AdZativeLabel *body = Line(AdZativeText(AdZativeIdBody, 14, NO, AdZativeColor(TextSecondary), 2), 14);
    body.textAlignment = NSTextAlignmentCenter;
    AdZativeLabel *cta = AdZativeCta(17, NO, AdZativeColor(CtaGreen), 18);
    AdZativeSize(cta, 0, CtaHeight);
    GADMediaView *media = PlaceholderMedia();

    // Centered rows: a column with centre alignment around the fixed-width pieces.
    UIStackView *iconRow = AdZativeColumn(@[ icon ], 0, UIStackViewAlignmentCenter);
    UIStackView *attribution = AdZativeColumn(@[ AttributionLine() ], 0, UIStackViewAlignmentCenter);
    UIStackView *store = AdZativeColumn(@[ StoreLine() ], 0, UIStackViewAlignmentCenter);
    HugHeight(store);

    // The media and its margins in one box, hidden with the media (binder), so the margins go too (Android GONE).
    UIView *mediaBox = [[UIView alloc] init];
    mediaBox.translatesAutoresizingMaskIntoConstraints = NO;
    mediaBox.restorationIdentifier = AdZativeTagMediaBox;

    if (landscape)
    {
        // gravity center_vertical: the column centred, the free height split above and below.
        UIView *above = AdZativeFill(UILayoutConstraintAxisVertical);
        UIView *below = AdZativeFill(UILayoutConstraintAxisVertical);
        body.padding = UIEdgeInsetsMake(12, 0, 0, 0);   // marginTop 12, gone with the body
        Line(body, 14);                                  // the minimum height again, with the padding
        UIStackView *side = AdZativeColumn(@[ above, iconRow, headline, attribution, body, store, cta, below ], 0,
                                           UIStackViewAlignmentFill);
        [above.heightAnchor constraintEqualToAnchor:below.heightAnchor].active = YES;   // once in the same stack
        [side setCustomSpacing:10 afterView:iconRow];
        [side setCustomSpacing:4 afterView:headline];
        [side setCustomSpacing:6 afterView:body];
        [side setCustomSpacing:14 afterView:store];
        side.layoutMarginsRelativeArrangement = YES;
        side.layoutMargins = UIEdgeInsetsMake(0, 24, 16, 24);
        AdZativePin(AdZativeMediaFrame(media, AdZativeMediaAlignCenter), mediaBox, UIEdgeInsetsMake(0, 0, 16, 16));
        UIStackView *content = AdZativeRow(@[ side, mediaBox ], 0, UIStackViewAlignmentFill);
        // Weights 2 : 3 of the width left after media's marginEnd 16: box = 1.5 x side + 16. Not required (hidden box).
        NSLayoutConstraint *ratio = [mediaBox.widthAnchor constraintEqualToAnchor:side.widthAnchor multiplier:1.5 constant:16];
        ratio.priority = UILayoutPriorityRequired - 1;
        ratio.active = YES;
        AdZativePin(content, adView, UIEdgeInsetsZero);
        return;
    }
    // marginTop / marginBottom 16; weight 1 (the frame), before the fill below.
    AdZativePin(AdZativeMediaFrame(media, AdZativeMediaAlignCenter), mediaBox, UIEdgeInsetsMake(16, 0, 16, 0));
    // Store row marginTop 6 as its own margin: kept when the body is missing (media 16 + 6).
    store.layoutMarginsRelativeArrangement = YES;
    store.layoutMargins = UIEdgeInsetsMake(6, 0, 0, 0);
    UIStackView *content = AdZativeColumn(@[ iconRow, headline, attribution, mediaBox, body, store, cta,
                                             AdZativeFill(UILayoutConstraintAxisVertical) ],
                                          0, UIStackViewAlignmentFill);
    [content setCustomSpacing:10 afterView:iconRow];
    [content setCustomSpacing:4 afterView:headline];
    [content setCustomSpacing:14 afterView:store];
    content.layoutMarginsRelativeArrangement = YES;
    content.layoutMargins = UIEdgeInsetsMake(8, 20, 20, 20);
    AdZativePin(content, adView, UIEdgeInsetsZero);
}

UIView *AdZativeInterstitialLayout(BOOL mediaCenter, BOOL landscape)
{
    UIView *root = FullscreenRoot();
    GADNativeAdView *adView = FullscreenAdView(root, landscape ? TopBarHeight : 0);
    if (mediaCenter)
        MediaCenterContent(adView, landscape);
    else
        MediaTopContent(adView, landscape);
    TopBar(root);
    return root;
}

/// White card rounded on the left. Android clamps a 24 dp radius to half the card's height (32 dp: a half circle);
/// a CALayer does not, and a radius above half the height drew uneven corners.
@interface AdZativeContinueCard : UIView
@end

@implementation AdZativeContinueCard

- (void)layoutSubviews
{
    [super layoutSubviews];
    self.layer.cornerRadius = MIN(24, CGRectGetHeight(self.bounds) / 2);
}

@end

/// zen_continue (AdMob app open style): white card flush with the right edge, rounded on the left: host app icon,
/// app name, "Continue to app" and a chevron. The presenter fills icon / name and the label.
static void ContinueBar(UIView *root)
{
    UIImageView *appIcon = AdZativeImage(AdZativeIdHostAppIcon, 24, 24, UIViewContentModeScaleAspectFit);
    AdZativeLabel *appName = Line(AdZativeText(AdZativeIdHostAppName, 16, NO, AdZativeColor(BarText), 1), 16);
    appName.font = [UIFont systemFontOfSize:16 weight:UIFontWeightMedium];
    AdZativeLabel *label = Line(AdZativeText(AdZativeIdContinueLabel, 14, NO, AdZativeColor(BarLabel), 1), 14);
    label.text = @"Continue to app";
    UIImageView *chevron = [[UIImageView alloc] initWithImage:IconImage(MaterialIconChevronRight, 24, AdZativeColor(BarAction))];
    chevron.translatesAutoresizingMaskIntoConstraints = NO;
    AdZativeSize(chevron, 24, 24);

    UIStackView *texts = AdZativeRow(@[ appName, label ], 8, UIStackViewAlignmentCenter);
    UIStackView *bar = AdZativeRow(@[ appIcon, texts, chevron ], 8, UIStackViewAlignmentCenter);
    [bar setCustomSpacing:4 afterView:appIcon];   // zen_app_name marginStart 4
    bar.layoutMarginsRelativeArrangement = YES;
    bar.layoutMargins = UIEdgeInsetsMake(4, 12, 4, 12);

    // The card (a plain view: a UIStackView draws no background before iOS 14) is the tappable zen_continue, so the
    // presenter's alpha and enabled state cover the whole bar. Elevation 2 dp.
    UIView *card = [[AdZativeContinueCard alloc] init];
    card.translatesAutoresizingMaskIntoConstraints = NO;
    card.accessibilityIdentifier = AdZativeIdContinue;
    card.backgroundColor = AdZativeColor(BarBackground);
    card.layer.maskedCorners = kCALayerMinXMinYCorner | kCALayerMinXMaxYCorner;
    card.layer.shadowOpacity = 0.2f;
    card.layer.shadowRadius = 2;
    card.layer.shadowOffset = CGSizeMake(0, 1);
    [root addSubview:card];
    bar.translatesAutoresizingMaskIntoConstraints = NO;
    [card addSubview:bar];
    UILayoutGuide *safe = root.safeAreaLayoutGuide;
    // The white card reaches the screen edge (as on Android); its content stays inside the safe area.
    [NSLayoutConstraint activateConstraints:@[
        [bar.topAnchor constraintEqualToAnchor:card.topAnchor],
        [bar.bottomAnchor constraintEqualToAnchor:card.bottomAnchor],
        [bar.leadingAnchor constraintEqualToAnchor:card.leadingAnchor],
        [bar.trailingAnchor constraintEqualToAnchor:safe.trailingAnchor],
        [card.topAnchor constraintEqualToAnchor:safe.topAnchor constant:AppOpenBarTop],
        [card.trailingAnchor constraintEqualToAnchor:root.trailingAnchor],
        [card.leadingAnchor constraintGreaterThanOrEqualToAnchor:safe.leadingAnchor],
    ]];
}

/// App open card content, portrait: media, icon, headline, badge / advertiser / stars / store / price, green pill CTA,
/// all centred; the media frame takes the free height first, up to the creative's aspect ratio.
static UIStackView *AppOpenPortraitContent(UIView *mediaBox)
{
    UIImageView *icon = AdZativeImage(AdZativeIdAppIcon, 64, 64, UIViewContentModeScaleAspectFit);
    AdZativeLabel *headline = Line(AdZativeText(AdZativeIdHeadline, 20, NO, AdZativeColor(AppOpenTextPrimary), 2), 20);
    headline.textAlignment = NSTextAlignmentCenter;
    UIStackView *info = AdZativeRow(@[
        AdZativeBadge(),
        Line(AdZativeText(AdZativeIdAdvertiser, 13, NO, AdZativeColor(AppOpenTextSecondary), 1), 13),
        AdZativeStars(AdZativeColor(Star), AdZativeColor(AppOpenStarEmpty)),
        Line(AdZativeText(AdZativeIdStore, 13, NO, AdZativeColor(AppOpenTextSecondary), 1), 13),
        Line(AdZativeText(AdZativeIdPrice, 13, NO, AdZativeColor(AppOpenTextSecondary), 1), 13),
    ], 8, UIStackViewAlignmentCenter);
    AdZativeLabel *cta = AdZativeCta(17, NO, AdZativeColor(CtaGreen), CtaHeight / 2);
    AdZativeSize(cta, 0, CtaHeight);

    UIView *above = AdZativeFill(UILayoutConstraintAxisVertical);
    UIView *below = AdZativeFill(UILayoutConstraintAxisVertical);
    UIStackView *iconRow = AdZativeColumn(@[ icon ], 0, UIStackViewAlignmentCenter);
    UIStackView *infoRow = AdZativeColumn(@[ info ], 0, UIStackViewAlignmentCenter);
    UIStackView *content = AdZativeColumn(@[ above, mediaBox, iconRow, headline, infoRow, cta, below ], 0,
                                          UIStackViewAlignmentFill);
    [above.heightAnchor constraintEqualToAnchor:below.heightAnchor].active = YES;   // once in the same stack
    [content setCustomSpacing:12 afterView:iconRow];
    [content setCustomSpacing:8 afterView:headline];
    [content setCustomSpacing:24 afterView:infoRow];
    content.layoutMarginsRelativeArrangement = YES;
    content.layoutMargins = UIEdgeInsetsMake(0, 24, 24, 24);
    return content;
}

/// App open card content, landscape: media on the left, the details centred on the right, equal widths.
static UIStackView *AppOpenLandscapeContent(UIView *mediaBox)
{
    UIImageView *icon = AdZativeImage(AdZativeIdAppIcon, 56, 56, UIViewContentModeScaleAspectFit);
    AdZativeLabel *headline = Line(AdZativeText(AdZativeIdHeadline, 18, NO, AdZativeColor(AppOpenTextPrimary), 2), 18);
    headline.textAlignment = NSTextAlignmentCenter;
    UIStackView *attribution = AdZativeRow(@[
        AdZativeBadge(),
        Line(AdZativeText(AdZativeIdAdvertiser, 13, NO, AdZativeColor(AppOpenTextSecondary), 1), 13),
        AdZativeStars(AdZativeColor(Star), AdZativeColor(AppOpenStarEmpty)),
    ], 8, UIStackViewAlignmentCenter);
    AdZativeLabel *price = AdZativeText(AdZativeIdPrice, 12, NO, AdZativeColor(AppOpenTextSecondary), 1);
    price.padding = UIEdgeInsetsMake(0, 8, 0, 0);   // marginStart 8, kept when there is no store
    UIStackView *store = AdZativeColumn(@[ AdZativeRow(@[
        Line(AdZativeText(AdZativeIdStore, 12, NO, AdZativeColor(AppOpenTextSecondary), 1), 12), Line(price, 12),
    ], 0, UIStackViewAlignmentCenter) ], 0, UIStackViewAlignmentCenter);
    HugHeight(store);
    AdZativeLabel *cta = AdZativeCta(16, NO, AdZativeColor(CtaGreen), 24);
    AdZativeSize(cta, 0, 48);

    UIView *above = AdZativeFill(UILayoutConstraintAxisVertical);
    UIView *below = AdZativeFill(UILayoutConstraintAxisVertical);
    UIStackView *iconRow = AdZativeColumn(@[ icon ], 0, UIStackViewAlignmentCenter);
    UIStackView *attributionRow = AdZativeColumn(@[ attribution ], 0, UIStackViewAlignmentCenter);
    UIStackView *side = AdZativeColumn(@[ above, iconRow, headline, attributionRow, store, cta, below ], 0,
                                       UIStackViewAlignmentFill);
    [above.heightAnchor constraintEqualToAnchor:below.heightAnchor].active = YES;   // once in the same stack
    [side setCustomSpacing:10 afterView:iconRow];
    [side setCustomSpacing:6 afterView:headline];
    [side setCustomSpacing:4 afterView:attributionRow];
    [side setCustomSpacing:16 afterView:store];

    UIStackView *content = AdZativeRow(@[ mediaBox, side ], 0, UIStackViewAlignmentFill);
    // Weights 1 : 1 of the width left after media's marginEnd 16. Not required: a hidden box (no-media ad) gets 0.
    NSLayoutConstraint *ratio = [mediaBox.widthAnchor constraintEqualToAnchor:side.widthAnchor constant:16];
    ratio.priority = UILayoutPriorityRequired - 1;
    ratio.active = YES;
    content.layoutMarginsRelativeArrangement = YES;
    content.layoutMargins = UIEdgeInsetsMake(0, 16, 16, 16);
    return content;
}

/// App open (AdMob app open style): the game shows, dimmed, behind a white card rising from the bottom (landscape: 60%
/// of the width, centred). The "Continue to app" bar sits on the strip above the card, so it never covers ad assets;
/// the card's top padding keeps the AdChoices corner of the native ad view clear of the rounded corner.
UIView *AdZativeAppOpenLayout(BOOL landscape)
{
    UIView *root = FullscreenRoot();
    root.backgroundColor = AdZativeColor(AppOpenScrim);

    UIView *card = [[UIView alloc] init];
    card.translatesAutoresizingMaskIntoConstraints = NO;
    card.backgroundColor = AdZativeColor(AppOpenCard);
    card.layer.cornerRadius = 16;
    card.layer.maskedCorners = kCALayerMinXMinYCorner | kCALayerMaxXMinYCorner;
    [root addSubview:card];

    GADNativeAdView *adView = [[GADNativeAdView alloc] init];
    adView.translatesAutoresizingMaskIntoConstraints = NO;
    adView.accessibilityIdentifier = AdZativeIdNativeView;
    [card addSubview:adView];

    UILayoutGuide *safe = root.safeAreaLayoutGuide;
    [NSLayoutConstraint activateConstraints:@[
        [card.topAnchor constraintEqualToAnchor:safe.topAnchor constant:AppOpenCardTop],
        [card.bottomAnchor constraintEqualToAnchor:root.bottomAnchor],
        [adView.topAnchor constraintEqualToAnchor:card.topAnchor constant:12],
        [adView.bottomAnchor constraintEqualToAnchor:safe.bottomAnchor],
        [adView.leadingAnchor constraintEqualToAnchor:card.leadingAnchor],
        [adView.trailingAnchor constraintEqualToAnchor:card.trailingAnchor],
    ]];
    if (landscape)
    {
        [NSLayoutConstraint activateConstraints:@[
            [card.centerXAnchor constraintEqualToAnchor:root.centerXAnchor],
            [card.widthAnchor constraintEqualToAnchor:root.widthAnchor multiplier:0.6],
        ]];
    }
    else
    {
        [NSLayoutConstraint activateConstraints:@[
            [card.leadingAnchor constraintEqualToAnchor:safe.leadingAnchor],
            [card.trailingAnchor constraintEqualToAnchor:safe.trailingAnchor],
        ]];
    }

    // The media and its margin in one box, hidden with the media (binder), so the margin goes too (Android GONE).
    UIView *mediaBox = [[UIView alloc] init];
    mediaBox.translatesAutoresizingMaskIntoConstraints = NO;
    mediaBox.restorationIdentifier = AdZativeTagMediaBox;
    GADMediaView *media = AdZativeMediaView();
    media.backgroundColor = AdZativeColor(AppOpenMediaBackground);
    AdZativePin(AdZativeMediaFrame(media, landscape ? AdZativeMediaAlignCenter : AdZativeMediaAlignHug), mediaBox,
                landscape ? UIEdgeInsetsMake(0, 0, 0, 16) : UIEdgeInsetsMake(0, 0, 20, 0));
    UIStackView *content = landscape ? AppOpenLandscapeContent(mediaBox) : AppOpenPortraitContent(mediaBox);
    AdZativePin(content, adView, UIEdgeInsetsZero);

    ContinueBar(root);
    return root;
}
