// Inline layouts (res/layout/zen_banner.xml, zen_banner_media.xml, zen_mrec.xml + res/layout-land) and the lookup.

#import "AdZativeLayouts.h"

#import "AdZativeLayoutKit.h"

NSString *const AdZativeContainerTopConstraint = @"ad_container_top";

static const uint32_t BannerBackground = 0xFF000408;
static const uint32_t BannerHeadline = 0xDBFFFFFF;
static const uint32_t BannerSecondary = 0xB3FFFFFF;   // 9.9:1 on the banner background (WCAG AA)
static const uint32_t CtaBlue = 0xFF1A73E8;
static const uint32_t TextPrimary = 0xFF1F1F1F;
static const uint32_t TextSecondary = 0xFF5F6368;
static const uint32_t CardStroke = 0x1F000000;
static const uint32_t MediaFrameBackground = 0xFFF1F3F4;   // around a creative narrower than the frame
static const uint32_t Star = 0xFFFBBC04;

/// Portrait: icon 60 | badge + stars + advertiser / headline / body | CTA 60 high.
static UIStackView *BannerPortraitRow(void)
{
    UIImageView *icon = AdZativeImage(AdZativeIdAppIcon, 60, 60, UIViewContentModeScaleAspectFill);

    AdZativeLabel *advertiser = AdZativeText(AdZativeIdAdvertiser, 10, NO, AdZativeColor(BannerSecondary), 1);
    AdZativeFlexible(advertiser, UILayoutConstraintAxisHorizontal);
    UIStackView *topLine = AdZativeRow(@[ AdZativeBadge(), AdZativeStars(AdZativeColor(Star), AdZativeColor(0x66FFFFFF)),
                                          advertiser ],
                                       4, UIStackViewAlignmentCenter);
    AdZativeSize(topLine, 0, 16);

    AdZativeLabel *headline = AdZativeText(AdZativeIdHeadline, 16, YES, AdZativeColor(BannerHeadline), 1);
    AdZativeAndroidLineHeight(headline, 16);
    AdZativeLabel *body = AdZativeText(AdZativeIdBody, 12, NO, AdZativeColor(BannerSecondary), 1);
    AdZativeAndroidLineHeight(body, 12);
    // Leading, not fill: with the advertiser hidden the badge + stars line has nothing to grow, and a fill column took
    // its width, so the row stretched the 60 pt icon instead (measured 167 x 60). marginStart 4 stays without the icon.
    UIStackView *texts = AdZativeColumn(@[ topLine, headline, body ], 0, UIStackViewAlignmentLeading);
    texts.layoutMarginsRelativeArrangement = YES;
    texts.directionalLayoutMargins = NSDirectionalEdgeInsetsMake(0, 4, 0, 0);

    AdZativeLabel *cta = AdZativeCta(16, YES, AdZativeColor(CtaBlue), 8);
    cta.padding = UIEdgeInsetsMake(0, 16, 0, 16);
    cta.minimumSize = CGSizeMake(120, 0);
    AdZativeSize(cta, 0, 60);

    UIStackView *row = AdZativeRow(@[ icon, texts, cta ], 0, UIStackViewAlignmentTop);
    [row setCustomSpacing:4 afterView:texts];   // the CTA's marginStart: gone with the CTA
    return row;
}

/// Landscape: icon 32 | headline + badge + body | advertiser + store + stars | CTA 28 high (margin 4), on one line.
static UIStackView *BannerLandscapeRow(void)
{
    UIImageView *icon = AdZativeImage(AdZativeIdAppIcon, 32, 32, UIViewContentModeScaleAspectFill);

    AdZativeLabel *headline = AdZativeText(AdZativeIdHeadline, 16, YES, AdZativeColor(BannerHeadline), 1);
    // Android measures the detail group (no weight) first: the headline is cut before the advertiser / store.
    [headline setContentCompressionResistancePriority:UILayoutPriorityDefaultHigh - 1 forAxis:UILayoutConstraintAxisHorizontal];
    AdZativeLabel *body = AdZativeText(AdZativeIdBody, 14, NO, AdZativeColor(BannerSecondary), 1);
    AdZativeFlexible(body, UILayoutConstraintAxisHorizontal);
    // The fill keeps the badge next to the headline when there is no body (Android: the weighted body is GONE).
    UIStackView *texts = AdZativeRow(@[ headline, AdZativeBadge(), body, AdZativeFill(UILayoutConstraintAxisHorizontal) ], 4,
                                     UIStackViewAlignmentCenter);
    [texts setCustomSpacing:0 afterView:body];
    texts.layoutMarginsRelativeArrangement = YES;
    texts.directionalLayoutMargins = NSDirectionalEdgeInsetsMake(0, 4, 0, 4);   // headline ms4, body / badge me4
    AdZativeSize(texts, 0, 32);

    AdZativeLabel *advertiser = AdZativeText(AdZativeIdAdvertiser, 14, NO, AdZativeColor(BannerSecondary), 1);
    [advertiser.widthAnchor constraintLessThanOrEqualToConstant:160].active = YES;
    // The zero-width first view turns the spacing into each detail's marginStart 4 (no gap when all are GONE).
    UIView *start = [[UIView alloc] init];
    [start.widthAnchor constraintEqualToConstant:0].active = YES;   // AdZativeSize treats 0 as unset
    UIStackView *details = AdZativeRow(@[
        start,
        advertiser,
        AdZativeText(AdZativeIdStore, 14, NO, AdZativeColor(BannerSecondary), 1),
        AdZativeStars(AdZativeColor(Star), AdZativeColor(0x66FFFFFF)),
    ], 4, UIStackViewAlignmentCenter);
    AdZativeSize(details, 0, 32);

    AdZativeLabel *cta = AdZativeCta(14, YES, AdZativeColor(CtaBlue), 8);
    cta.padding = UIEdgeInsetsMake(0, 16, 0, 16);
    cta.minimumSize = CGSizeMake(120, 0);
    cta.margins = UIEdgeInsetsMake(4, 4, 4, 4);
    AdZativeSize(cta, 0, 28 + 8);   // alignment rect: 28 high plus the margins

    return AdZativeRow(@[ icon, texts, details, cta ], 0, UIStackViewAlignmentTop);
}

/// zen_banner / zen_banner_media. The media variant keeps a full-size, almost transparent MediaView behind the card,
/// so video ads can be taken (the banner has no room to show them).
static UIView *Banner(BOOL landscape, BOOL media)
{
    GADNativeAdView *root = AdZativeNativeView();
    if (media)
    {
        GADMediaView *mediaView = AdZativeMediaView();
        mediaView.alpha = 0.01;
        AdZativePin(mediaView, root, UIEdgeInsetsZero);
    }
    UIView *container = AdZativeContainer(root, BannerBackground, 10);
    // Android padding 4 (portrait) with clipToPadding: a slot shorter than the content clips the 60 pt icon / CTA.
    UIView *clip = AdZativeClip(container, landscape ? UIEdgeInsetsZero : UIEdgeInsetsMake(4, 4, 4, 4));
    AdZativePinTop(landscape ? BannerLandscapeRow() : BannerPortraitRow(), clip, UIEdgeInsetsZero);
    return root;
}

/// zen_mrec: white card; icon + headline / badge, stars, advertiser, store, price; media; full-width CTA.
static UIView *Mrec(void)
{
    GADNativeAdView *root = AdZativeNativeView();
    root.frame = CGRectMake(0, 0, 300, 250);
    root.backgroundColor = UIColor.whiteColor;
    root.layer.cornerRadius = 8;
    root.layer.borderWidth = 1;
    root.layer.borderColor = AdZativeColor(CardStroke).CGColor;

    AdZativeLabel *advertiser = AdZativeText(AdZativeIdAdvertiser, 11, NO, AdZativeColor(TextSecondary), 1);
    advertiser.restorationIdentifier = AdZativeTagCompact;
    AdZativeLabel *store = AdZativeText(AdZativeIdStore, 11, NO, AdZativeColor(TextSecondary), 1);
    AdZativeLabel *price = AdZativeText(AdZativeIdPrice, 11, NO, AdZativeColor(TextSecondary), 1);
    // Android measures in order: the price is cut first, then the store, then the advertiser.
    [store setContentCompressionResistancePriority:UILayoutPriorityDefaultHigh - 1 forAxis:UILayoutConstraintAxisHorizontal];
    [price setContentCompressionResistancePriority:UILayoutPriorityDefaultHigh - 2 forAxis:UILayoutConstraintAxisHorizontal];
    UIStackView *details = AdZativeRow(@[
        AdZativeBadge(),
        AdZativeStars(AdZativeColor(Star), AdZativeColor(0xFFDADCE0)),
        advertiser,
        store,
        price,
    ], 6, UIStackViewAlignmentCenter);

    AdZativeLabel *headline = AdZativeText(AdZativeIdHeadline, 14, YES, AdZativeColor(TextPrimary), 1);
    AdZativeAndroidLineHeight(headline, 14);
    UIStackView *texts = AdZativeColumn(@[ headline, details ], 2, UIStackViewAlignmentLeading);
    texts.layoutMarginsRelativeArrangement = YES;
    texts.directionalLayoutMargins = NSDirectionalEdgeInsetsMake(0, 8, 0, 0);   // marginStart 8, kept without the icon

    UIStackView *header = AdZativeRow(@[ AdZativeImage(AdZativeIdAppIcon, 36, 36, UIViewContentModeScaleAspectFit), texts ],
                                      0, UIStackViewAlignmentCenter);
    header.layoutMarginsRelativeArrangement = YES;
    header.layoutMargins = UIEdgeInsetsMake(0, 0, 0, 18);   // AdChoices corner stays free of clickable assets

    // weight 1 (before the fill below); the creative at its own aspect ratio inside, centred.
    UIView *media = AdZativeMediaFrame(AdZativeMediaView(), AdZativeMediaAlignCenter);
    media.backgroundColor = AdZativeColor(MediaFrameBackground);

    AdZativeLabel *cta = AdZativeCta(14, NO, AdZativeColor(CtaBlue), 8);
    AdZativeSize(cta, 0, 38);

    // The fill packs everything to the top when the ad has no media (Android: media GONE, empty space below).
    UIStackView *column = AdZativeColumn(@[ header, media, cta, AdZativeFill(UILayoutConstraintAxisVertical) ], 6,
                                         UIStackViewAlignmentFill);
    [column setCustomSpacing:0 afterView:cta];
    AdZativePin(column, root, UIEdgeInsetsMake(6, 6, 6, 6));
    return root;
}

UIView *AdZativeBuiltInLayout(NSString *name, BOOL landscape)
{
    if ([name isEqualToString:@"zen_banner"])
        return Banner(landscape, NO);
    if ([name isEqualToString:@"zen_banner_media"])
        return Banner(landscape, YES);
    if ([name isEqualToString:@"zen_mrec"])
        return Mrec();
    if ([name isEqualToString:@"zen_inter_media_top"])
        return AdZativeInterstitialLayout(NO, landscape);
    if ([name isEqualToString:@"zen_inter_media_center"])
        return AdZativeInterstitialLayout(YES, landscape);
    if ([name isEqualToString:@"zen_appopen"])
        return AdZativeAppOpenLayout(landscape);
    return nil;
}

void AdZativeSetContainerMarginTop(UIView *root, CGFloat marginTop)
{
    UIView *container = AdZativeFindView(root, AdZativeIdContainer);
    for (NSLayoutConstraint *constraint in container.superview.constraints)
    {
        if ([constraint.identifier isEqualToString:AdZativeContainerTopConstraint])
        {
            constraint.constant = marginTop;
            return;
        }
    }
}
