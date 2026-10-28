#import "AdZativeCore.h"

#include <math.h>
#include <time.h>

UIViewController *AdZativeRootViewController(void)
{
    return UnityGetGLViewController();
}

int64_t AdZativeNowMs(void)
{
    // CLOCK_MONOTONIC_RAW keeps counting during sleep, like elapsedRealtime: ad TTLs are wall-clock (60 min).
    return (int64_t)(clock_gettime_nsec_np(CLOCK_MONOTONIC_RAW) / NSEC_PER_MSEC);
}

void AdZativeRunOnMain(dispatch_block_t block)
{
    if ([NSThread isMainThread])
        block();
    else
        dispatch_async(dispatch_get_main_queue(), block);
}

void AdZativePostOnMain(dispatch_block_t block)
{
    dispatch_async(dispatch_get_main_queue(), block);
}

NSNumber *AdZativeNumber(double value)
{
    return @(isfinite(value) ? value : 0);
}

@implementation AdZativeTask
{
    dispatch_block_t _block;
    NSUInteger _generation;
}

- (instancetype)initWithBlock:(dispatch_block_t)block
{
    if ((self = [super init]))
        _block = [block copy];
    return self;
}

- (void)runAfterMs:(int64_t)delayMs
{
    NSUInteger generation = _generation;
    __weak AdZativeTask *weakSelf = self;
    int64_t delayNs = MAX((int64_t)0, delayMs) * (int64_t)NSEC_PER_MSEC;
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, delayNs), dispatch_get_main_queue(), ^{
        AdZativeTask *task = weakSelf;
        if (task != nil && task->_generation == generation)
            task->_block();
    });
}

- (void)cancel
{
    _generation++;
}

@end
