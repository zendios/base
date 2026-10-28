#import "AdZativeLayoutRegistry.h"

#import "AdZativeLayouts.h"

NSString *const AdZativeIdNativeView = @"ad_native_view";
NSString *const AdZativeIdHeadline = @"ad_headline";
NSString *const AdZativeIdAttribution = @"ad_attribution";
NSString *const AdZativeIdMedia = @"ad_media";
NSString *const AdZativeIdBody = @"ad_body";
NSString *const AdZativeIdCallToAction = @"ad_call_to_action";
NSString *const AdZativeIdAppIcon = @"ad_app_icon";
NSString *const AdZativeIdAdvertiser = @"ad_advertiser";
NSString *const AdZativeIdStars = @"ad_stars";
NSString *const AdZativeIdStore = @"ad_store";
NSString *const AdZativeIdPrice = @"ad_price";
NSString *const AdZativeIdContainer = @"ad_container";

NSString *const AdZativeIdCountdown = @"zen_countdown";
NSString *const AdZativeIdClose = @"zen_close";
NSString *const AdZativeIdNext = @"zen_next";
NSString *const AdZativeIdPodLabel = @"zen_pod_label";
NSString *const AdZativeIdContinue = @"zen_continue";
NSString *const AdZativeIdContinueLabel = @"zen_continue_label";
NSString *const AdZativeIdHostAppIcon = @"zen_app_icon";
NSString *const AdZativeIdHostAppName = @"zen_app_name";
NSString *const AdZativeIdTopBar = @"zen_top_bar";

NSString *const AdZativeTagCompact = @"compact";
NSString *const AdZativeTagIconFallback = @"icon_fallback";
NSString *const AdZativeTagMediaBox = @"media_box";
NSString *const AdZativeTagMediaFrame = @"media_frame";

UIView *AdZativeFindView(UIView *root, NSString *viewId)
{
    if ([root.accessibilityIdentifier isEqualToString:viewId])
        return root;
    for (UIView *subview in root.subviews)
    {
        UIView *found = AdZativeFindView(subview, viewId);
        if (found != nil)
            return found;
    }
    return nil;
}

@interface AdZativeInflated ()
- (instancetype)initWithRoot:(UIView *)root name:(NSString *)name fellBack:(BOOL)fellBack problem:(nullable NSString *)problem;
@end

@implementation AdZativeInflated

- (instancetype)initWithRoot:(UIView *)root name:(NSString *)name fellBack:(BOOL)fellBack problem:(NSString *)problem
{
    if ((self = [super init]))
    {
        _root = root;
        _adView = (GADNativeAdView *)AdZativeFindView(root, AdZativeIdNativeView);
        _name = [name copy];
        _fellBack = fellBack;
        _problem = [problem copy];
    }
    return self;
}

- (UIView *)viewWithId:(NSString *)viewId
{
    return AdZativeFindView(_root, viewId);
}

- (void)unbind
{
    _adView.nativeAd = nil;
}

@end

// Asset -> view ID, and how much a missing asset hurts (media and icon drive CTR the most; Meta Native Banner needs
// the icon). Same order and weights as Android.
static NSArray<NSString *> *ScoredAssets(void)
{
    return @[ AdZativeAssetBody, AdZativeAssetCta, AdZativeAssetIcon, AdZativeAssetStars, AdZativeAssetStore,
              AdZativeAssetPrice, AdZativeAssetAdvertiser, AdZativeAssetMedia ];
}

static NSArray<NSString *> *ScoredIds(void)
{
    return @[ AdZativeIdBody, AdZativeIdCallToAction, AdZativeIdAppIcon, AdZativeIdStars, AdZativeIdStore,
              AdZativeIdPrice, AdZativeIdAdvertiser, AdZativeIdMedia ];
}

static const NSInteger Weights[] = { 2, 3, 3, 2, 1, 1, 1, 4 };

@implementation AdZativeLayoutRegistry

+ (nullable UIView *)createRoot:(NSString *)name landscape:(BOOL)landscape
{
    UIView *builtIn = AdZativeBuiltInLayout(name, landscape);
    if (builtIn != nil)
        return builtIn;
    if ([NSBundle.mainBundle pathForResource:name ofType:@"nib"] == nil)
        return nil;
    for (id object in [[UINib nibWithNibName:name bundle:nil] instantiateWithOwner:nil options:nil])
    {
        if ([object isKindOfClass:[UIView class]])
            return object;
    }
    return nil;
}

+ (AdZativeInflated *)inflate:(NSString *)name format:(AdZativeFormat)format landscape:(BOOL)landscape
{
    NSString *problem;
    UIView *root = [self createRoot:name landscape:landscape];
    if (root != nil)
    {
        problem = [self validate:root format:format];
        if (problem == nil)
            return [[AdZativeInflated alloc] initWithRoot:root name:name fellBack:NO problem:nil];
    }
    else
    {
        problem = @"layout_not_found";
    }
    NSString *fallback = AdZativeFormatDefaultLayout(format);
    UIView *fallbackRoot = [self createRoot:fallback landscape:landscape];
    return [[AdZativeInflated alloc] initWithRoot:fallbackRoot name:fallback fellBack:YES problem:problem];
}

+ (nullable NSString *)validate:(UIView *)root format:(AdZativeFormat)format
{
    if (![AdZativeFindView(root, AdZativeIdNativeView) isKindOfClass:[GADNativeAdView class]])
        return @"missing_ad_native_view";
    if (AdZativeFindView(root, AdZativeIdHeadline) == nil)
        return @"missing_ad_headline";
    if (AdZativeFindView(root, AdZativeIdAttribution) == nil)
        return @"missing_ad_attribution";
    if (AdZativeFormatRequiresMedia(format) && ![AdZativeFindView(root, AdZativeIdMedia) isKindOfClass:[GADMediaView class]])
        return @"missing_ad_media";
    return nil;
}

+ (NSSet<NSString *> *)coverage:(NSString *)name landscape:(BOOL)landscape
{
    static NSMutableDictionary<NSString *, NSSet<NSString *> *> *cache;
    if (cache == nil)
        cache = [NSMutableDictionary dictionary];
    // Per orientation: a landscape variant can show other assets (zen_banner landscape has ad_store).
    NSString *key = [name stringByAppendingString:landscape ? @"|land" : @"|port"];
    NSSet<NSString *> *covered = cache[key];
    if (covered != nil)
        return covered;
    NSMutableSet<NSString *> *set = [NSMutableSet set];
    UIView *root = [self createRoot:name landscape:landscape];
    if (root != nil)
    {
        NSArray<NSString *> *ids = ScoredIds();
        for (NSUInteger i = 0; i < ids.count; i++)
        {
            if (AdZativeFindView(root, ids[i]) != nil)
                [set addObject:ScoredAssets()[i]];
        }
    }
    cache[key] = [set copy];
    return cache[key];
}

+ (NSArray<NSString *> *)missing:(NSString *)name info:(AdZativeAdInfo *)info landscape:(BOOL)landscape
{
    NSSet<NSString *> *covered = [self coverage:name landscape:landscape];
    NSMutableArray<NSString *> *missing = [NSMutableArray array];
    for (NSString *asset in info.assets)
    {
        if (![covered containsObject:asset])
            [missing addObject:asset];
    }
    return missing;
}

+ (NSInteger)score:(NSString *)name info:(AdZativeAdInfo *)info landscape:(BOOL)landscape
{
    NSSet<NSString *> *covered = [self coverage:name landscape:landscape];
    NSArray<NSString *> *assets = ScoredAssets();
    NSInteger score = 0;
    for (NSUInteger i = 0; i < assets.count; i++)
    {
        if ([info has:assets[i]] && [covered containsObject:assets[i]])
            score += Weights[i] + ([assets[i] isEqualToString:AdZativeAssetMedia] && [info isVideo] ? 1 : 0);
    }
    return score;
}

+ (NSString *)autoSelect:(AdZativeFormat)format
                configured:(NSString *)configured
                      info:(AdZativeAdInfo *)info
                 landscape:(BOOL)landscape
{
    NSString *best = configured;
    NSInteger bestScore = [self score:configured info:info landscape:landscape];
    for (NSString *name in AdZativeFormatLayouts(format))
    {
        if ([name isEqualToString:configured])
            continue;
        NSInteger score = [self score:name info:info landscape:landscape];
        if (score > bestScore)
        {
            best = name;
            bestScore = score;
        }
    }
    return best;
}

@end
