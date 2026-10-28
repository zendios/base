// C ABI called from Unity ([DllImport("__Internal")] in IOSAdZativeBridge.cs), 1:1 with IAdZativeBridge and the Android
// facade unity/AdZativeSDK.java.
//
//  - Strings are UTF-8. Returned strings are malloc'ed; the Unity marshaller frees them.
//  - Booleans are int (0 / 1), so the default 4-byte bool marshalling of C# matches.
//  - Every call returns at once; the work runs on the main thread in a later turn of the run loop, in call order (on
//    Android, Unity's thread is not the UI thread, so the work is always posted there too). The getters (IsReady,
//    HasReadyAd, GetPodInfo, DumpState, GetScale) answer synchronously.
//  - configureSlot, loadUnit, showSlotInRect, showSlotAtAnchor and showFullscreen that arrive before "initialized" wait
//    and run in order once it is emitted.

#import <Foundation/Foundation.h>

/// One event: slot id ("" for pool-level and global events), event name, flat JSON payload with "ts" (ms, monotonic).
/// The strings live only during the call: copy them.
typedef void (*AdZativeEventCallback)(const char *slotId, const char *event, const char *json);

/// Starts the Google Mobile Ads SDK. iOS reads the app ID from Info.plist (GADApplicationIdentifier, written by the GMA
/// Unity plugin), so appId is not used.
void AdZative_Initialize(const char *appId, AdZativeEventCallback callback);

/// The host app already started the SDK (zen-unity Base: UMP consent + MobileAds.Initialize); the plugin never does.
void AdZative_InitializeExternal(AdZativeEventCallback callback);

void AdZative_SetMaxCachedAds(int count);
void AdZative_ConfigureSlot(const char *json);
void AdZative_LoadUnit(const char *slotId, const char *adUnitId);
int AdZative_IsReady(const char *slotId);
int AdZative_HasReadyAd(const char *slotId);

/// Normalised (0..1) rect of the Unity screen, origin TOP-LEFT.
void AdZative_ShowSlotInRect(const char *slotId, float nx, float ny, float nw, float nh);
void AdZative_UpdateSlotRect(const char *slotId, float nx, float ny, float nw, float nh);

/// position: AdZativePosition (0..7); offsets and size in points (= Android dp); size 0 = default.
void AdZative_ShowSlotAtAnchor(const char *slotId, int position, float xDp, float yDp, float wDp, float hDp);

void AdZative_SetSlotLayout(const char *slotId, const char *layoutName);
void AdZative_HideSlot(const char *slotId);
void AdZative_DestroySlot(const char *slotId);

/// podSize: 0 = the slot's config; 1..3 ads in a row (interstitial / rewarded).
void AdZative_ShowFullscreen(const char *slotId, int podSize);
char *AdZative_GetPodInfo(const char *slotId, int podSize);

void AdZative_AcceptVideo(const char *slotId);
void AdZative_RejectVideo(const char *slotId);
void AdZative_ReplaceAd(const char *slotId, int allowVideo);
void AdZative_SetVideoMode(const char *slotId, const char *mode);
void AdZative_SetRefreshSec(const char *slotId, int seconds);
void AdZative_SetLayoutMode(const char *slotId, const char *mode);

void AdZative_CloseFullscreen(void);
void AdZative_NextFullscreen(void);
/// AUTO | SHOW | HIDE | LOCKED each; unknown = AUTO.
void AdZative_SetFullscreenButtons(const char *slotId, const char *closeMode, const char *nextMode);
void AdZative_SetFullscreenButtonAlpha(const char *slotId, float closeAlpha, float nextAlpha);
void AdZative_SetFullscreenCountdown(const char *slotId, int secondsLeft, float alpha);
void AdZative_HandleBack(void);

void AdZative_SetAppOpenSuppressed(int suppressed);
void AdZative_SuppressNextForeground(void);
char *AdZative_DumpState(void);

/// Unity screen pixels per point: converts the plugin's points (= Android dp) for the Canvas.
float AdZative_GetScale(void);
