using System;

namespace Base.Ads
{
    /// <summary>Platform seam: Android, iOS, Editor stub for iteration. Game code never sees it.</summary>
    internal interface IAdZativeBridge
    {
        void Initialize(string appId, Action<string, string, string> onEvent);
        /// <summary>MobileAds already initialised by the host app.</summary>
        void InitializeExternal(Action<string, string, string> onEvent);
        /// <summary>manualLoad slots: request one ad from this ad unit now.</summary>
        void LoadUnit(string slotId, string adUnitId);
        void SetMaxCachedAds(int n);
        void ConfigureSlot(string json);
        bool IsReady(string slotId);
        /// <summary>A cached ad the slot can show NEXT (inline: not counting the ad on screen).</summary>
        bool HasReadyAd(string slotId);
        /// <summary>Normalised screen rect, origin TOP-LEFT, 0..1.</summary>
        void ShowSlotInRect(string slotId, float nx, float ny, float nw, float nh);
        void UpdateSlotRect(string slotId, float nx, float ny, float nw, float nh);
        void ShowSlotAtAnchor(string slotId, AdZativePosition position, float xDp, float yDp, float wDp, float hDp);
        void SetSlotLayout(string slotId, string layoutName);
        void HideSlot(string slotId);
        void DestroySlot(string slotId);
        /// <param name="podSize">0 = slot config; 1..3 ads in a row (interstitial / rewarded).</param>
        void ShowFullscreen(string slotId, int podSize);
        /// <summary>JSON snapshot of the ads a (pod) show would use right now.</summary>
        string GetPodInfo(string slotId, int podSize);
        void AcceptVideo(string slotId);
        void RejectVideo(string slotId);
        void ReplaceAd(string slotId, bool allowVideo);
        void SetVideoMode(string slotId, string mode);
        void SetRefreshSec(string slotId, int seconds);
        void SetLayoutMode(string slotId, string mode);
        void CloseFullscreen();
        /// <param name="closeMode">AUTO | SHOW | HIDE | LOCKED (✕); nextMode likewise for Next.</param>
        void SetFullscreenButtons(string slotId, string closeMode, string nextMode);
        /// <summary>Countdown chip: secondsLeft >= 1 shows "Ns" at alpha, 0 hides it, -1 = native countdown.</summary>
        void SetFullscreenCountdown(string slotId, int secondsLeft, float alpha);
        void SetFullscreenButtonAlpha(string slotId, float closeAlpha, float nextAlpha);
        /// <summary>Next ad of the pod on screen now: "pod_next" (trigger host), or "next_ignored".</summary>
        void NextFullscreen();
        void HandleBack();
        void SetAppOpenSuppressed(bool suppressed);
        void SuppressNextForeground();
        string DumpState();
    }
}
