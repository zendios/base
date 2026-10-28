#import "AdZativeNativeAdBinder.h"

#import "AdZativeLayoutKit.h"
#import "AdZativeStarsView.h"

#include <math.h>

static void SetText(UIView *view, NSString *text)
{
    if ([view isKindOfClass:[UILabel class]])
        ((UILabel *)view).text = text;
    else if ([view isKindOfClass:[UIButton class]])
        [(UIButton *)view setTitle:text forState:UIControlStateNormal];
}

static void SetTextOrHide(UIView *view, NSString *text)
{
    BOOL has = text.length > 0;
    SetText(view, has ? text : @"");
    view.hidden = !has;
}

/// A .xib may use a UILabel for ad_stars: "★★★★☆".
static void SetRating(UIView *view, double rating)
{
    if ([view isKindOfClass:[AdZativeStarsView class]])
    {
        ((AdZativeStarsView *)view).rating = rating;
        return;
    }
    NSInteger full = (NSInteger)llround(MAX(0, MIN(5, rating)));
    NSString *stars = [[@"" stringByPaddingToLength:(NSUInteger)full withString:@"★" startingAtIndex:0]
        stringByAppendingString:[@"" stringByPaddingToLength:(NSUInteger)(5 - full) withString:@"☆" startingAtIndex:0]];
    SetText(view, stars);
}

static void ApplyContentMode(UIView *view, UIViewContentMode mode)
{
    view.contentMode = mode;
    for (UIView *subview in view.subviews)
    {
        if ([subview isKindOfClass:[UIImageView class]])
            ApplyContentMode(subview, mode);
    }
}

@implementation AdZativeNativeAdBinder

+ (GADMediaView *)bindAdView:(GADNativeAdView *)adView ad:(GADNativeAd *)ad info:(AdZativeAdInfo *)info crop:(BOOL)crop
{
    UIView *headline = AdZativeFindView(adView, AdZativeIdHeadline);
    UIView *body = AdZativeFindView(adView, AdZativeIdBody);
    UIView *cta = AdZativeFindView(adView, AdZativeIdCallToAction);
    UIImageView *icon = [AdZativeFindView(adView, AdZativeIdAppIcon) isKindOfClass:[UIImageView class]]
        ? (UIImageView *)AdZativeFindView(adView, AdZativeIdAppIcon) : nil;
    UIView *advertiser = AdZativeFindView(adView, AdZativeIdAdvertiser);
    UIView *stars = AdZativeFindView(adView, AdZativeIdStars);
    UIView *price = AdZativeFindView(adView, AdZativeIdPrice);
    UIView *store = AdZativeFindView(adView, AdZativeIdStore);
    GADMediaView *media = [AdZativeFindView(adView, AdZativeIdMedia) isKindOfClass:[GADMediaView class]]
        ? (GADMediaView *)AdZativeFindView(adView, AdZativeIdMedia) : nil;

    if (headline != nil)
    {
        SetText(headline, ad.headline);
        adView.headlineView = headline;
    }
    if (body != nil)
    {
        SetTextOrHide(body, ad.body);
        adView.bodyView = body;
    }
    if (cta != nil)
    {
        SetTextOrHide(cta, ad.callToAction);
        cta.userInteractionEnabled = NO;   // the SDK handles the tap (required for a UIButton)
        adView.callToActionView = cta;
    }
    BOOL noMedia = info.media == AdZativeMediaNone;
    if (icon != nil)
    {
        BOOL fallbackOnly = [icon.restorationIdentifier isEqualToString:AdZativeTagIconFallback];
        UIImage *image = ad.icon.image;
        BOOL show = image != nil && (!fallbackOnly || noMedia || media == nil);
        icon.image = show ? image : nil;
        icon.hidden = !show;
        adView.iconView = icon;
    }
    BOOL hasStars = ad.starRating != nil && ad.starRating.doubleValue > 0;
    if (stars != nil)
    {
        if (hasStars)
            SetRating(stars, ad.starRating.doubleValue);
        stars.hidden = !hasStars;
        adView.starRatingView = stars;
    }
    if (advertiser != nil)
    {
        // Compact layouts show either the rating (app install) or the advertiser (content), not both.
        BOOL compact = [advertiser.restorationIdentifier isEqualToString:AdZativeTagCompact];
        SetTextOrHide(advertiser, compact && hasStars ? nil : ad.advertiser);
        adView.advertiserView = advertiser;
    }
    if (price != nil)
    {
        SetTextOrHide(price, ad.price);
        adView.priceView = price;
    }
    if (store != nil)
    {
        SetTextOrHide(store, ad.store);
        adView.storeView = store;
    }
    // The media's frame (AdZativeMediaFrame) and the box with its margins go with it (Android GONE).
    UIView *mediaFrame = [media.superview.restorationIdentifier isEqualToString:AdZativeTagMediaFrame] ? media.superview : media;
    if ([mediaFrame.superview.restorationIdentifier isEqualToString:AdZativeTagMediaBox])
        mediaFrame.superview.hidden = noMedia;
    if (media != nil)
    {
        if (noMedia)
        {
            mediaFrame.hidden = YES;   // never an empty box: the icon cell takes over
            media = nil;
        }
        else
        {
            mediaFrame.hidden = NO;
            media.hidden = NO;
            media.clipsToBounds = YES;
            media.mediaContent = ad.mediaContent;
            if (mediaFrame != media)
                AdZativeSetMediaAspect(media, ad.mediaContent.aspectRatio);   // the whole creative, no crop
        }
    }
    adView.mediaView = media;
    // Must be last: tells the SDK the views are populated. Starts the video in the MediaView.
    adView.nativeAd = ad;
    // GADMediaView leaves its image view on aspect fit whatever its own contentMode was when the content arrived
    // (measured: media 2 = fill, image 1 = fit), so CROP (Base's default) never filled the box: set it on both.
    if (media != nil)
        ApplyContentMode(media, crop ? UIViewContentModeScaleAspectFill : UIViewContentModeScaleAspectFit);
    return media;
}

@end
