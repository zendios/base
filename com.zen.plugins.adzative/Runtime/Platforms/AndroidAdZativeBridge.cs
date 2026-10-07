#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using UnityEngine;

namespace Base.Ads
{
    /// <summary>JNI bridge to com.zen.plugins.adzative.unity.AdZativeSDK (see the Android library).</summary>
    internal sealed class AndroidAdZativeBridge : IAdZativeBridge
    {
        private const string FacadeClass = "com.zen.plugins.adzative.unity.AdZativeSDK";
        private const string ListenerInterface = "com.zen.plugins.adzative.unity.AdZativeListener";

        private readonly AndroidJavaClass _facade = new AndroidJavaClass(FacadeClass);
        private ListenerProxy _proxy;   // strong ref so it is not GC'd

        private sealed class ListenerProxy : AndroidJavaProxy
        {
            private readonly Action<string, string, string> _onEvent;
            public ListenerProxy(Action<string, string, string> onEvent) : base(ListenerInterface) { _onEvent = onEvent; }

            // Called on the Android UI thread → handed straight to the main-thread queue.
            public void onEvent(string slotId, string evt, string payloadJson) => _onEvent(slotId, evt, payloadJson);
        }

        public void Initialize(string appId, Action<string, string, string> onEvent)
        {
            _proxy = new ListenerProxy(onEvent);
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                _facade.CallStatic("initialize", activity, appId, _proxy);
            }
        }

        public void InitializeExternal(Action<string, string, string> onEvent)
        {
            _proxy = new ListenerProxy(onEvent);
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                _facade.CallStatic("initializeExternal", activity, _proxy);
            }
        }

        public void LoadUnit(string slotId, string adUnitId) => _facade.CallStatic("loadUnit", slotId, adUnitId);
        public void SetMaxCachedAds(int n) => _facade.CallStatic("setMaxCachedAds", n);
        public void ConfigureSlot(string json) => _facade.CallStatic("configureSlot", json);
        public bool IsReady(string slotId) => _facade.CallStatic<bool>("isReady", slotId);
        public bool HasReadyAd(string slotId) => _facade.CallStatic<bool>("hasReadyAd", slotId);
        public void ShowSlotInRect(string slotId, float nx, float ny, float nw, float nh)
            => _facade.CallStatic("showSlotInRect", slotId, nx, ny, nw, nh);
        public void UpdateSlotRect(string slotId, float nx, float ny, float nw, float nh)
            => _facade.CallStatic("updateSlotRect", slotId, nx, ny, nw, nh);
        public void ShowSlotAtAnchor(string slotId, AdZativePosition position, float xDp, float yDp, float wDp, float hDp)
            => _facade.CallStatic("showSlotAtAnchor", slotId, (int)position, xDp, yDp, wDp, hDp);
        public void SetSlotLayout(string slotId, string layoutName) => _facade.CallStatic("setSlotLayout", slotId, layoutName);
        public void HideSlot(string slotId) => _facade.CallStatic("hideSlot", slotId);
        public void DestroySlot(string slotId) => _facade.CallStatic("destroySlot", slotId);
        public void ShowFullscreen(string slotId, int podSize) => _facade.CallStatic("showFullscreen", slotId, podSize);
        public string GetPodInfo(string slotId, int podSize) => _facade.CallStatic<string>("getPodInfo", slotId, podSize);
        public void AcceptVideo(string slotId) => _facade.CallStatic("acceptVideo", slotId);
        public void RejectVideo(string slotId) => _facade.CallStatic("rejectVideo", slotId);
        public void ReplaceAd(string slotId, bool allowVideo) => _facade.CallStatic("replaceAd", slotId, allowVideo);
        public void SetVideoMode(string slotId, string mode) => _facade.CallStatic("setVideoMode", slotId, mode);
        public void SetRefreshSec(string slotId, int seconds) => _facade.CallStatic("setRefreshSec", slotId, seconds);
        public void SetLayoutMode(string slotId, string mode) => _facade.CallStatic("setLayoutMode", slotId, mode);
        public void CloseFullscreen() => _facade.CallStatic("closeFullscreen");
        public void HandleBack() => _facade.CallStatic("handleBack");
        public void SetAppOpenSuppressed(bool suppressed) => _facade.CallStatic("setAppOpenSuppressed", suppressed);
        public void SuppressNextForeground() => _facade.CallStatic("suppressNextForeground");
        public string DumpState() => _facade.CallStatic<string>("dumpState");
    }
}
#endif
