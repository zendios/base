#import "AdZativeSlotConfig.h"

static NSInteger ParseName(NSString *name, NSArray<NSString *> *names, NSInteger fallback)
{
    if (![name isKindOfClass:[NSString class]])
        return fallback;
    NSString *upper = [[name stringByTrimmingCharactersInSet:NSCharacterSet.whitespaceAndNewlineCharacterSet] uppercaseString];
    NSUInteger index = [names indexOfObject:upper];
    return index == NSNotFound ? fallback : (NSInteger)index;
}

static NSArray<NSString *> *SourcePolicyNames(void) { return @[ @"PRIORITY", @"FALLBACK", @"ROUND_ROBIN" ]; }
static NSArray<NSString *> *VideoModeNames(void) { return @[ @"ALLOW", @"GATE", @"BLOCK" ]; }
static NSArray<NSString *> *LayoutModeNames(void) { return @[ @"MANUAL", @"AUTO" ]; }
static NSArray<NSString *> *ButtonModeNames(void) { return @[ @"AUTO", @"SHOW", @"HIDE", @"LOCKED" ]; }

AdZativeSourcePolicy AdZativeParseSourcePolicy(NSString *name, AdZativeSourcePolicy fallback)
{
    return (AdZativeSourcePolicy)ParseName(name, SourcePolicyNames(), fallback);
}

AdZativeVideoMode AdZativeParseVideoMode(NSString *name, AdZativeVideoMode fallback)
{
    return (AdZativeVideoMode)ParseName(name, VideoModeNames(), fallback);
}

AdZativeLayoutMode AdZativeParseLayoutMode(NSString *name, AdZativeLayoutMode fallback)
{
    return (AdZativeLayoutMode)ParseName(name, LayoutModeNames(), fallback);
}

AdZativeButtonMode AdZativeParseButtonMode(NSString *name, AdZativeButtonMode fallback)
{
    return (AdZativeButtonMode)ParseName(name, ButtonModeNames(), fallback);
}

NSString *AdZativeSourcePolicyName(AdZativeSourcePolicy policy)
{
    return SourcePolicyNames()[(NSUInteger)policy];
}

NSString *AdZativeLayoutModeName(AdZativeLayoutMode mode)
{
    return LayoutModeNames()[(NSUInteger)mode];
}

// org.json opt* semantics: a missing or mistyped field gives the default; numbers and strings convert both ways.

static NSString *OptString(NSDictionary *o, NSString *key, NSString *fallback)
{
    id v = o[key];
    if ([v isKindOfClass:[NSString class]])
        return v;
    if ([v isKindOfClass:[NSNumber class]])
        return [v stringValue];
    return fallback;
}

static int64_t OptLong(NSDictionary *o, NSString *key, int64_t fallback)
{
    id v = o[key];
    if ([v isKindOfClass:[NSNumber class]])
        return [v longLongValue];
    if ([v isKindOfClass:[NSString class]])
    {
        NSScanner *scanner = [NSScanner scannerWithString:v];
        long long parsed = 0;
        if ([scanner scanLongLong:&parsed] && scanner.isAtEnd)
            return parsed;
    }
    return fallback;
}

static NSInteger OptInt(NSDictionary *o, NSString *key, NSInteger fallback)
{
    return (NSInteger)OptLong(o, key, fallback);
}

static BOOL OptBool(NSDictionary *o, NSString *key, BOOL fallback)
{
    id v = o[key];
    if ([v isKindOfClass:[NSNumber class]])
        return [v boolValue];
    if ([v isKindOfClass:[NSString class]])
    {
        if ([v caseInsensitiveCompare:@"true"] == NSOrderedSame)
            return YES;
        if ([v caseInsensitiveCompare:@"false"] == NSOrderedSame)
            return NO;
    }
    return fallback;
}

static NSInteger Clamp(NSInteger value, NSInteger low, NSInteger high)
{
    return MAX(low, MIN(high, value));
}

@implementation AdZativeSlotConfig

+ (instancetype)configWithJson:(NSString *)json error:(NSString **)error
{
    NSData *data = [json dataUsingEncoding:NSUTF8StringEncoding];
    NSError *jsonError = nil;
    id root = data != nil ? [NSJSONSerialization JSONObjectWithData:data options:0 error:&jsonError] : nil;
    if (![root isKindOfClass:[NSDictionary class]])
    {
        if (error != NULL)
            *error = [NSString stringWithFormat:@"invalid slot json: %@", jsonError.localizedDescription ?: @"not an object"];
        return nil;
    }
    return [[self alloc] initWithDictionary:root error:error];
}

- (nullable instancetype)initWithDictionary:(NSDictionary *)o error:(NSString **)error
{
    if (!(self = [super init]))
        return nil;

    NSString *slotId = OptString(o, @"slotId", nil);
    if (slotId == nil)
    {
        if (error != NULL)
            *error = @"No value for slotId";
        return nil;
    }
    _slotId = [slotId copy];

    NSString *formatName = OptString(o, @"format", @"BANNER");
    AdZativeFormat format;
    if (!AdZativeFormatParse(formatName, &format))
    {
        if (error != NULL)
            *error = [NSString stringWithFormat:@"slot %@: unknown format %@", slotId, formatName];
        return nil;
    }
    _format = format;

    NSMutableArray<NSString *> *ids = [NSMutableArray array];
    id array = o[@"adUnitIds"];
    if ([array isKindOfClass:[NSArray class]])
    {
        for (id item in (NSArray *)array)
        {
            NSString *raw = [item isKindOfClass:[NSString class]] ? item
                : [item isKindOfClass:[NSNumber class]] ? [item stringValue] : @"";
            NSString *adUnitId = [raw stringByTrimmingCharactersInSet:NSCharacterSet.whitespaceAndNewlineCharacterSet];
            if (adUnitId.length > 0 && ![ids containsObject:adUnitId])
                [ids addObject:adUnitId];
        }
    }
    if (ids.count == 0)
    {
        if (error != NULL)
            *error = [NSString stringWithFormat:@"slot %@: adUnitIds is empty", slotId];
        return nil;
    }
    _adUnitIds = [ids copy];

    _sourcePolicy = AdZativeParseSourcePolicy(OptString(o, @"sourcePolicy", @"FALLBACK"), AdZativeSourcePolicyFallback);
    NSString *layout = OptString(o, @"layout", @"");
    _layout = layout.length == 0 ? AdZativeFormatDefaultLayout(format) : [layout copy];
    _cacheSize = Clamp(OptInt(o, @"cacheSize", 1), 1, 3);
    // AdMob and Meta ads expire after 60 min; 55 min keeps a margin so a dead ad is never shown.
    _ttlMs = MAX((int64_t)60, OptLong(o, @"ttlSec", 3300)) * 1000;
    _startMuted = OptBool(o, @"startMuted", AdZativeFormatDefaultStartMuted(format));
    _mediaAspectRatio = [OptString(o, @"mediaAspectRatio", @"ANY") copy];
    _adChoicesPlacement = [OptString(o, @"adChoicesPlacement", AdZativeFormatIsFullscreen(format) ? @"TOP_LEFT" : @"TOP_RIGHT") copy];

    NSInteger refresh = OptInt(o, @"refreshSec", 0);
    _refreshSec = refresh <= 0 ? 0 : MAX(30, refresh);   // never refresh faster than 30 s
    _minDisplaySec = MAX(0, OptInt(o, @"minDisplaySec", 30));
    _waitVideoEnd = OptBool(o, @"waitVideoEnd", YES);
    _preloadWhenHidden = OptBool(o, @"preloadWhenHidden", YES);

    _imageFirstSec = MAX(1, OptInt(o, @"imageFirstSec", 10));
    _imageNextSec = MAX(1, OptInt(o, @"imageNextSec", 5));
    _closeAfterSec = MAX(0, OptInt(o, @"closeAfterSec", format == AdZativeFormatAppOpen ? 0 : 5));
    _videoMaxSec = MAX(1, OptInt(o, @"videoMaxSec", 30));
    _podRatio = MAX(1, OptInt(o, @"podRatio", 2));
    _showCountdown = OptBool(o, @"showCountdown", YES);
    _autoShowOnForeground = OptBool(o, @"autoShowOnForeground", NO);
    _minIntervalSec = MAX(0, OptInt(o, @"minIntervalSec", 30));

    _podSize = Clamp(OptInt(o, @"podSize", 1), 1, 3);
    _podCloseOnEveryAd = OptBool(o, @"podCloseOnEveryAd", NO);

    // BANNER: the inline presenter still forces BLOCK when the layout has no MediaView (zen_banner).
    _videoMode = AdZativeParseVideoMode(OptString(o, @"videoMode", @"ALLOW"), AdZativeVideoModeAllow);
    _layoutMode = AdZativeParseLayoutMode(OptString(o, @"layoutMode", @"MANUAL"), AdZativeLayoutModeManual);
    // FIT keeps the whole creative visible (Meta forbids cropping media); CROP only for A/B tests.
    _mediaCrop = [OptString(o, @"mediaScale", @"FIT") caseInsensitiveCompare:@"CROP"] == NSOrderedSame;
    _manualLoad = OptBool(o, @"manualLoad", NO);
    _containerMarginTopDp = OptInt(o, @"containerMarginTopDp", -1);
    return self;
}

- (NSString *)poolKey:(NSString *)adUnitId
{
    return [NSString stringWithFormat:@"%@|%@|%@|%@", adUnitId, _mediaAspectRatio, _startMuted ? @"m" : @"s",
                                      _adChoicesPlacement];
}

@end
