#import "AdZativeAdInfo.h"

#include <math.h>

NSString *const AdZativeAssetBody = @"body";
NSString *const AdZativeAssetCta = @"cta";
NSString *const AdZativeAssetIcon = @"icon";
NSString *const AdZativeAssetStars = @"stars";
NSString *const AdZativeAssetStore = @"store";
NSString *const AdZativeAssetPrice = @"price";
NSString *const AdZativeAssetAdvertiser = @"advertiser";
NSString *const AdZativeAssetMedia = @"media";

NSString *AdZativeMediaName(AdZativeMedia media)
{
    switch (media)
    {
        case AdZativeMediaImage:
            return @"IMAGE";
        case AdZativeMediaVideo:
            return @"VIDEO";
        case AdZativeMediaNone:
        default:
            return @"NONE";
    }
}

static BOOL NonEmpty(NSString *text)
{
    return [text stringByTrimmingCharactersInSet:NSCharacterSet.whitespaceAndNewlineCharacterSet].length > 0;
}

static NSString *NonNull(NSString *text)
{
    return text ?: @"";
}

@implementation AdZativeAdInfo

- (instancetype)initWithMedia:(AdZativeMedia)media
                       aspect:(float)aspect
                      network:(NSString *)network
                      adapter:(NSString *)adapter
                   starRating:(double)starRating
                       assets:(NSArray<NSString *> *)assets
{
    if ((self = [super init]))
    {
        _media = media;
        _aspect = aspect;
        _network = [network copy];
        _adapter = [adapter copy];
        _starRating = starRating;
        _assets = [assets copy];
    }
    return self;
}

+ (NSString *)networkOfAd:(GADNativeAd *)ad
{
    return NonNull(ad.responseInfo.loadedAdNetworkResponseInfo.adSourceName);
}

+ (instancetype)infoForAd:(GADNativeAd *)ad
{
    GADAdNetworkResponseInfo *loaded = ad.responseInfo.loadedAdNetworkResponseInfo;
    NSString *network = NonNull(loaded.adSourceName);
    NSString *adapter = NonNull(loaded.adNetworkClassName);
    BOOL meta = [[adapter stringByAppendingString:network].lowercaseString containsString:@"facebook"]
        || [network.lowercaseString containsString:@"meta"];

    GADMediaContent *content = ad.mediaContent;
    float aspect = content != nil ? (float)content.aspectRatio : 0.f;
    if (!isfinite(aspect))
        aspect = 0.f;
    BOOL mainImage = content.mainImage != nil || ad.images.firstObject.image != nil;
    BOOL video = content != nil && content.hasVideoContent;
    AdZativeMedia media;
    if (content == nil)
        media = AdZativeMediaNone;
    else if (video && !(meta && aspect <= 0.f && !mainImage))   // Meta Native Banner: no media at all
        media = AdZativeMediaVideo;
    else if (mainImage || aspect > 0.f)
        media = AdZativeMediaImage;
    else
        media = AdZativeMediaNone;

    NSMutableArray<NSString *> *assets = [NSMutableArray array];
    if (NonEmpty(ad.body))
        [assets addObject:AdZativeAssetBody];
    if (NonEmpty(ad.callToAction))
        [assets addObject:AdZativeAssetCta];
    if (ad.icon.image != nil)
        [assets addObject:AdZativeAssetIcon];
    double stars = ad.starRating != nil ? ad.starRating.doubleValue : 0;
    if (stars > 0)
        [assets addObject:AdZativeAssetStars];
    if (NonEmpty(ad.store))
        [assets addObject:AdZativeAssetStore];
    if (NonEmpty(ad.price))
        [assets addObject:AdZativeAssetPrice];
    if (NonEmpty(ad.advertiser))
        [assets addObject:AdZativeAssetAdvertiser];
    if (media != AdZativeMediaNone)
        [assets addObject:AdZativeAssetMedia];
    return [[self alloc] initWithMedia:media aspect:aspect network:network adapter:adapter starRating:stars assets:assets];
}

+ (float)videoDurationOfAd:(GADNativeAd *)ad
{
    GADMediaContent *content = ad.mediaContent;
    if (content == nil || !content.hasVideoContent)
        return 0.f;
    float duration = (float)content.duration;
    return isfinite(duration) && duration > 0.f ? duration : 0.f;
}

- (BOOL)isVideo
{
    return _media == AdZativeMediaVideo;
}

- (BOOL)has:(NSString *)asset
{
    return [_assets containsObject:asset];
}

- (NSMutableDictionary *)putInto:(NSMutableDictionary *)payload ad:(GADNativeAd *)ad
{
    BOOL video = [self isVideo];
    payload[@"media_type"] = AdZativeMediaName(_media);
    payload[@"has_video"] = @(video);
    payload[@"video_duration_s"] = AdZativeNumber(video ? [AdZativeAdInfo videoDurationOfAd:ad] : 0);
    payload[@"aspect"] = AdZativeNumber(_aspect);
    payload[@"network"] = _network;
    payload[@"adapter"] = _adapter;
    payload[@"has_body"] = @([self has:AdZativeAssetBody]);
    payload[@"has_cta"] = @([self has:AdZativeAssetCta]);
    payload[@"has_icon"] = @([self has:AdZativeAssetIcon]);
    payload[@"has_stars"] = @([self has:AdZativeAssetStars]);
    payload[@"star_rating"] = AdZativeNumber(_starRating);
    payload[@"has_store"] = @([self has:AdZativeAssetStore]);
    payload[@"has_price"] = @([self has:AdZativeAssetPrice]);
    payload[@"has_advertiser"] = @([self has:AdZativeAssetAdvertiser]);
    payload[@"assets"] = [_assets componentsJoinedByString:@","];
    payload[@"asset_count"] = @(_assets.count + 1);   // + headline
    return payload;
}

@end
