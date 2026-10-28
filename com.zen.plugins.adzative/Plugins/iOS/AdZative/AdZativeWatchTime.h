// Countdown of each ad of a full-screen show (interstitial, app open, rewarded; one ad or a pod) and when its ✕ / Next
// appear (Android: core/WatchTime). All values come from the slot, so every prefab / A/B variant sets its own.
//
//  Image ad: countdown = imageFirstSec for the first ad, imageNextSec for every later ad;
//            ✕ / Next at min(closeAfterSec, countdown)                      e.g. 10 s (✕ at 5 s), then 5 s (✕ at 5 s).
//  Video ad: countdown = the video length (unknown yet -> its cap), capped so the whole show stays within videoMaxSec
//            and every later ad runs at most 1/podRatio of the previous one  e.g. long videos, pod 2: 20 s, then 10 s.
//            ✕ / Next (rewarded: the reward) when the countdown ends, or earlier when a shorter video ends.

#import "AdZativeSlotConfig.h"

NS_ASSUME_NONNULL_BEGIN

/// @param k position of the ad in the show (0 = first)
/// @param showSize ads in the show (pod size)
/// @param prevSec countdown of the previous ad (0 for the first)
/// @param usedSec countdowns of the previous ads, summed
NSInteger AdZativeWatchCountdown(AdZativeSlotConfig *config, BOOL video, float durationSec, NSInteger k,
                                 NSInteger showSize, NSInteger prevSec, NSInteger usedSec);

/// Seconds after which ✕ / Next appear (interstitial, app open). Rewarded: the reward comes at the countdown end.
NSInteger AdZativeWatchCloseAt(AdZativeSlotConfig *config, BOOL video, NSInteger countdown);

NS_ASSUME_NONNULL_END
