// What a loaded native ad contains, computed once at load time (Android: core/AdInfo). Reported to Unity with
// "loaded", "ad_ready", "video_gate", "shown"... so the game and the A/B analysis know the media type, video length,
// assets and network.
//
// Two families come back from the same native request: Native Advanced (media + text assets) and Native Banner
// (e.g. Meta "Native Banner": icon + text, NO media). The Meta adapter flags every Meta ad as video, so the media
// type is derived defensively.

#import <GoogleMobileAds/GoogleMobileAds.h>

#import "AdZativeCore.h"

NS_ASSUME_NONNULL_BEGIN

typedef NS_ENUM(NSInteger, AdZativeMedia) {
    AdZativeMediaNone,
    AdZativeMediaImage,
    AdZativeMediaVideo,
};

/// "NONE" | "IMAGE" | "VIDEO".
NSString *AdZativeMediaName(AdZativeMedia media);

// Asset keys (also used by AdZativeLayoutRegistry to score layouts).
extern NSString *const AdZativeAssetBody;
extern NSString *const AdZativeAssetCta;
extern NSString *const AdZativeAssetIcon;
extern NSString *const AdZativeAssetStars;
extern NSString *const AdZativeAssetStore;
extern NSString *const AdZativeAssetPrice;
extern NSString *const AdZativeAssetAdvertiser;
extern NSString *const AdZativeAssetMedia;

@interface AdZativeAdInfo : NSObject

@property (nonatomic, readonly) AdZativeMedia media;
@property (nonatomic, readonly) float aspect;
@property (nonatomic, readonly, copy) NSString *network;
@property (nonatomic, readonly, copy) NSString *adapter;
@property (nonatomic, readonly) double starRating;
/// Assets present in THIS ad (the headline is always present and not listed).
@property (nonatomic, readonly, copy) NSArray<NSString *> *assets;

+ (instancetype)infoForAd:(GADNativeAd *)ad;

- (instancetype)init NS_UNAVAILABLE;

/// Name of the ad source that served the ad (e.g. "AdMob Network", "Meta Audience Network"), "" if unknown.
+ (NSString *)networkOfAd:(GADNativeAd *)ad;

/// Video length in seconds, read live (often 0 right after load, until the metadata arrives).
+ (float)videoDurationOfAd:(nullable GADNativeAd *)ad;

- (BOOL)isVideo;

- (BOOL)has:(NSString *)asset;

/// Adds the flat fields (JsonUtility-friendly on the Unity side) and returns the same dictionary.
- (NSMutableDictionary *)putInto:(NSMutableDictionary *)payload ad:(GADNativeAd *)ad;

@end

NS_ASSUME_NONNULL_END
