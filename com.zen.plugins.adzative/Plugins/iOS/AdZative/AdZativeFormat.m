#import "AdZativeFormat.h"

static NSArray<NSString *> *FormatNames(void)
{
    return @[ @"BANNER", @"MREC", @"INTERSTITIAL", @"APP_OPEN", @"REWARDED" ];
}

BOOL AdZativeFormatParse(NSString *name, AdZativeFormat *format)
{
    NSString *upper = [[name stringByTrimmingCharactersInSet:NSCharacterSet.whitespaceAndNewlineCharacterSet] uppercaseString];
    NSUInteger index = upper != nil ? [FormatNames() indexOfObject:upper] : NSNotFound;
    if (index == NSNotFound)
        return NO;
    *format = (AdZativeFormat)index;
    return YES;
}

NSString *AdZativeFormatName(AdZativeFormat format)
{
    return FormatNames()[(NSUInteger)format];
}

BOOL AdZativeFormatIsFullscreen(AdZativeFormat format)
{
    return format == AdZativeFormatInterstitial || format == AdZativeFormatAppOpen || format == AdZativeFormatRewarded;
}

BOOL AdZativeFormatDefaultStartMuted(AdZativeFormat format)
{
    return format != AdZativeFormatRewarded;
}

BOOL AdZativeFormatRequiresMedia(AdZativeFormat format)
{
    return format != AdZativeFormatBanner;
}

NSArray<NSString *> *AdZativeFormatLayouts(AdZativeFormat format)
{
    switch (format)
    {
        case AdZativeFormatBanner:
            return @[ @"zen_banner" ];
        case AdZativeFormatMrec:
            return @[ @"zen_mrec" ];
        case AdZativeFormatAppOpen:
            return @[ @"zen_appopen" ];
        case AdZativeFormatInterstitial:
        case AdZativeFormatRewarded:
        default:
            // Style 1 = media on top, style 2 = media in the center.
            return @[ @"zen_inter_media_top", @"zen_inter_media_center" ];
    }
}

NSString *AdZativeFormatDefaultLayout(AdZativeFormat format)
{
    return AdZativeFormatLayouts(format).firstObject;
}

NSString *AdZativeFormatLayout(AdZativeFormat format, NSInteger style)
{
    NSArray<NSString *> *layouts = AdZativeFormatLayouts(format);
    return style >= 1 && style <= (NSInteger)layouts.count ? layouts[(NSUInteger)(style - 1)] : layouts.firstObject;
}
