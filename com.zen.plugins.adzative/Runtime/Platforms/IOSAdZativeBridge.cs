#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using AOT;

namespace Base.Ads
{
    /// <summary>
    /// P/Invoke bridge to the iOS plugin (Plugins/iOS/AdZative, C ABI in AdZativeBridge.h; source in zen-ios).
    /// Booleans cross as int. Events arrive on the iOS main thread, which is Unity's main thread.
    /// </summary>
    internal sealed class IOSAdZativeBridge : IAdZativeBridge
    {
        private delegate void EventCallback(string slotId, string evt, string json);

        // Kept in a static field: the native side holds the function pointer for the app's lifetime.
        private static readonly EventCallback Callback = OnNativeEvent;
        private static Action<string, string, string> _onEvent;

        [MonoPInvokeCallback(typeof(EventCallback))]
        private static void OnNativeEvent(string slotId, string evt, string json) => _onEvent?.Invoke(slotId, evt, json);

        [DllImport("__Internal")] private static extern void AdZative_Initialize(string appId, EventCallback callback);
        [DllImport("__Internal")] private static extern void AdZative_InitializeExternal(EventCallback callback);
        [DllImport("__Internal")] private static extern void AdZative_SetMaxCachedAds(int count);
        [DllImport("__Internal")] private static extern void AdZative_ConfigureSlot(string json);
        [DllImport("__Internal")] private static extern void AdZative_LoadUnit(string slotId, string adUnitId);
        [DllImport("__Internal")] private static extern int AdZative_IsReady(string slotId);
        [DllImport("__Internal")] private static extern int AdZative_HasReadyAd(string slotId);
        [DllImport("__Internal")] private static extern void AdZative_ShowSlotInRect(string slotId, float nx, float ny, float nw, float nh);
        [DllImport("__Internal")] private static extern void AdZative_UpdateSlotRect(string slotId, float nx, float ny, float nw, float nh);
        [DllImport("__Internal")] private static extern void AdZative_ShowSlotAtAnchor(string slotId, int position, float xDp, float yDp, float wDp, float hDp);
        [DllImport("__Internal")] private static extern void AdZative_SetSlotLayout(string slotId, string layoutName);
        [DllImport("__Internal")] private static extern void AdZative_HideSlot(string slotId);
        [DllImport("__Internal")] private static extern void AdZative_DestroySlot(string slotId);
        [DllImport("__Internal")] private static extern void AdZative_ShowFullscreen(string slotId, int podSize);
        [DllImport("__Internal")] private static extern string AdZative_GetPodInfo(string slotId, int podSize);
        [DllImport("__Internal")] private static extern void AdZative_AcceptVideo(string slotId);
        [DllImport("__Internal")] private static extern void AdZative_RejectVideo(string slotId);
        [DllImport("__Internal")] private static extern void AdZative_ReplaceAd(string slotId, int allowVideo);
        [DllImport("__Internal")] private static extern void AdZative_SetVideoMode(string slotId, string mode);
        [DllImport("__Internal")] private static extern void AdZative_SetRefreshSec(string slotId, int seconds);
        [DllImport("__Internal")] private static extern void AdZative_SetLayoutMode(string slotId, string mode);
        [DllImport("__Internal")] private static extern void AdZative_CloseFullscreen();
        [DllImport("__Internal")] private static extern void AdZative_SetFullscreenButtons(string slotId, string closeMode, string nextMode);
        [DllImport("__Internal")] private static extern void AdZative_SetFullscreenCountdown(string slotId, int secondsLeft, float alpha);
        [DllImport("__Internal")] private static extern void AdZative_SetFullscreenButtonAlpha(string slotId, float closeAlpha, float nextAlpha);
        [DllImport("__Internal")] private static extern void AdZative_NextFullscreen();
        [DllImport("__Internal")] private static extern void AdZative_HandleBack();
        [DllImport("__Internal")] private static extern void AdZative_SetAppOpenSuppressed(int suppressed);
        [DllImport("__Internal")] private static extern void AdZative_SuppressNextForeground();
        [DllImport("__Internal")] private static extern string AdZative_DumpState();
        [DllImport("__Internal")] private static extern float AdZative_GetScale();

        /// <summary>Unity screen pixels per point (the unit of the native layouts).</summary>
        internal static float GetScale() => AdZative_GetScale();

        public void Initialize(string appId, Action<string, string, string> onEvent)
        {
            _onEvent = onEvent;
            AdZative_Initialize(appId, Callback);
        }

        public void InitializeExternal(Action<string, string, string> onEvent)
        {
            _onEvent = onEvent;
            AdZative_InitializeExternal(Callback);
        }

        public void LoadUnit(string slotId, string adUnitId) => AdZative_LoadUnit(slotId, adUnitId);
        public void SetMaxCachedAds(int n) => AdZative_SetMaxCachedAds(n);
        public void ConfigureSlot(string json) => AdZative_ConfigureSlot(json);
        public bool IsReady(string slotId) => AdZative_IsReady(slotId) != 0;
        public bool HasReadyAd(string slotId) => AdZative_HasReadyAd(slotId) != 0;
        public void ShowSlotInRect(string slotId, float nx, float ny, float nw, float nh) => AdZative_ShowSlotInRect(slotId, nx, ny, nw, nh);
        public void UpdateSlotRect(string slotId, float nx, float ny, float nw, float nh) => AdZative_UpdateSlotRect(slotId, nx, ny, nw, nh);
        public void ShowSlotAtAnchor(string slotId, AdZativePosition position, float xDp, float yDp, float wDp, float hDp)
            => AdZative_ShowSlotAtAnchor(slotId, (int)position, xDp, yDp, wDp, hDp);
        public void SetSlotLayout(string slotId, string layoutName) => AdZative_SetSlotLayout(slotId, layoutName);
        public void HideSlot(string slotId) => AdZative_HideSlot(slotId);
        public void DestroySlot(string slotId) => AdZative_DestroySlot(slotId);
        public void ShowFullscreen(string slotId, int podSize) => AdZative_ShowFullscreen(slotId, podSize);
        public string GetPodInfo(string slotId, int podSize) => AdZative_GetPodInfo(slotId, podSize);
        public void AcceptVideo(string slotId) => AdZative_AcceptVideo(slotId);
        public void RejectVideo(string slotId) => AdZative_RejectVideo(slotId);
        public void ReplaceAd(string slotId, bool allowVideo) => AdZative_ReplaceAd(slotId, allowVideo ? 1 : 0);
        public void SetVideoMode(string slotId, string mode) => AdZative_SetVideoMode(slotId, mode);
        public void SetRefreshSec(string slotId, int seconds) => AdZative_SetRefreshSec(slotId, seconds);
        public void SetLayoutMode(string slotId, string mode) => AdZative_SetLayoutMode(slotId, mode);
        public void CloseFullscreen() => AdZative_CloseFullscreen();
        public void SetFullscreenButtons(string slotId, string closeMode, string nextMode)
            => AdZative_SetFullscreenButtons(slotId, closeMode, nextMode);
        public void SetFullscreenCountdown(string slotId, int secondsLeft, float alpha)
            => AdZative_SetFullscreenCountdown(slotId, secondsLeft, alpha);
        public void SetFullscreenButtonAlpha(string slotId, float closeAlpha, float nextAlpha)
            => AdZative_SetFullscreenButtonAlpha(slotId, closeAlpha, nextAlpha);
        public void NextFullscreen() => AdZative_NextFullscreen();
        public void HandleBack() => AdZative_HandleBack();
        public void SetAppOpenSuppressed(bool suppressed) => AdZative_SetAppOpenSuppressed(suppressed ? 1 : 0);
        public void SuppressNextForeground() => AdZative_SuppressNextForeground();
        public string DumpState() => AdZative_DumpState();
    }
}
#endif
