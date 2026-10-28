// Shared helpers of the AdZative iOS plugin (Android: core/MainThread, core/EventSink, SystemClock).

#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

#if !__has_feature(objc_arc)
#error "AdZative needs ARC: compile the plugin files with -fobjc-arc."
#endif

// Unity trampoline (UnityAppController.mm, declared in UnityInterface.h): the view controller whose view is the Unity view.
UIViewController *_Nullable UnityGetGLViewController(void);
UIView *_Nullable UnityGetGLView(void);

NS_ASSUME_NONNULL_BEGIN

/// Unity's view controller: ad loaders need it for click-through, the overlay sits on its view.
UIViewController *_Nullable AdZativeRootViewController(void);

/// Milliseconds of a monotonic clock that keeps running while the device sleeps (Android elapsedRealtime).
int64_t AdZativeNowMs(void);

/// Runs at once on the main thread, otherwise posts to it (Android MainThread.run).
void AdZativeRunOnMain(dispatch_block_t block);

/// Always defers to a later main-loop turn, so a call chain never re-enters itself (Android MainThread.post).
void AdZativePostOnMain(dispatch_block_t block);

/// JSON-safe number: NaN and infinity become 0 (NSJSONSerialization refuses them).
NSNumber *AdZativeNumber(double value);

/// Everything observable goes through one sink to Unity. Always called on the main thread.
typedef void (^AdZativeSink)(NSString *slotId, NSString *event, NSMutableDictionary *payload);

/// The user tapped an ad: it may leave the app, so no app-open ad on return.
typedef void (^AdZativeClickHook)(void);

/// A delayed main-thread action that can be cancelled (Android Runnable + MainThread.delay / cancel).
@interface AdZativeTask : NSObject

- (instancetype)initWithBlock:(dispatch_block_t)block NS_DESIGNATED_INITIALIZER;
- (instancetype)init NS_UNAVAILABLE;

/// Schedules one more run; runs scheduled earlier still happen unless cancelled.
- (void)runAfterMs:(int64_t)delayMs;

/// Drops every pending run.
- (void)cancel;

@end

NS_ASSUME_NONNULL_END
