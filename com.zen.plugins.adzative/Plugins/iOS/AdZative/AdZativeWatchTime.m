#import "AdZativeWatchTime.h"

#include <math.h>

/// Budget left for this video, shared with the ads still to come in the ratio podRatio : 1 (the ad now gets the biggest
/// share), and never more than 1/podRatio of the previous ad.
static NSInteger VideoCap(AdZativeSlotConfig *config, NSInteger k, NSInteger showSize, NSInteger prevSec, NSInteger usedSec)
{
    NSInteger left = MAX(1, showSize - k);
    double weight = pow((double)config.podRatio, (double)(left - 1));
    double sum = 0;
    for (NSInteger i = 0; i < left; i++)
        sum += pow((double)config.podRatio, (double)i);
    NSInteger cap = (NSInteger)floor(MAX(0, config.videoMaxSec - usedSec) * weight / sum);
    if (prevSec > 0)
        cap = MIN(cap, prevSec / config.podRatio);
    return MAX(1, cap);
}

NSInteger AdZativeWatchCountdown(AdZativeSlotConfig *config, BOOL video, float durationSec, NSInteger k,
                                 NSInteger showSize, NSInteger prevSec, NSInteger usedSec)
{
    if (!video)
        return k == 0 ? config.imageFirstSec : config.imageNextSec;
    NSInteger cap = VideoCap(config, k, showSize, prevSec, usedSec);
    NSInteger length = durationSec > 0 ? (NSInteger)ceil(durationSec) : cap;
    return MAX(1, MIN(length, cap));
}

NSInteger AdZativeWatchCloseAt(AdZativeSlotConfig *config, BOOL video, NSInteger countdown)
{
    return video ? countdown : MIN(config.closeAfterSec, countdown);
}
