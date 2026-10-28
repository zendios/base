using System.Collections;
using UnityEngine;

namespace Base.Ads
{
    /// <summary>
    /// Unity-driven countdown of a full-screen ad (AdZativeTiming.countdownDimmedSec / closeLockedSec): the countdown
    /// chip is dimmed at first, then ✕ / "Next ›". The native layer only draws (SetFullscreenCountdown,
    /// SetFullscreenButtons Locked, SetFullscreenButtonAlpha). One full-screen ad at a time.
    /// This player's CTR of the ad unit ID (AdCtr): at or below AdCtr.Min, ✕ / Next stay dimmed and locked closeLockedSec
    /// after the countdown; above AdCtr.Max after a click on the ad, the next ad (or close) when the player comes back.
    /// </summary>
    internal static class AdZativeCloseTimer
    {
        private static Coroutine running;
        private static string runningSlotId;
        private static string runningAdUnitId;
        private static bool isLastAd;
        private static bool isClicked;
        private static bool wentBackground;

        internal static bool IsUsedBy(AdZativeTiming timing) => timing.countdownDimmedSec > 0 || timing.closeLockedSec > 0;

        /// <summary>
        /// On "shown" (every ad of a pod). watchSec: the ad's countdown from the native rules. isLast: last ad of the
        /// pod; canClose: ✕ on this ad (the last one, or every one with podCloseOnEveryAd).
        /// </summary>
        internal static void Begin(string slotId, AdZativeTiming timing, string adUnitId, int watchSec, bool isLast, bool canClose)
        {
            End();
            runningSlotId = slotId;
            runningAdUnitId = adUnitId;
            isLastAd = isLast;
            int lockedSec = AdCtr.Get(adUnitId) <= AdCtr.Min ? timing.closeLockedSec : 0;
            running = AdZativeMainThread.Run(Countdown(slotId, timing, watchSec, canClose, lockedSec));
        }

        /// <summary>On close: stops the countdown and gives the slot back to the native rules.</summary>
        internal static void End()
        {
            if (running != null)
                AdZativeMainThread.Halt(running);
            running = null;
            isClicked = false;
            wentBackground = false;
            runningAdUnitId = null;
            if (runningSlotId == null)
                return;
            AdZativeSDK.SetFullscreenCountdown(runningSlotId, -1, 1f);
            AdZativeSDK.SetFullscreenButtonAlpha(runningSlotId, 1f, 1f);
            AdZativeSDK.SetFullscreenButtons(runningSlotId, AdZativeButtonMode.Auto, AdZativeButtonMode.Auto);
            runningSlotId = null;
        }

        /// <summary>"clicked" on the ad on screen.</summary>
        internal static void OnClicked(string slotId)
        {
            if (slotId == runningSlotId)
                isClicked = true;
        }

        /// <summary>App pause / resume (AdZativeMainThread.OnApplicationPause).</summary>
        internal static void OnAppPause(bool paused)
        {
            if (runningSlotId == null)
                return;
            if (paused)
            {
                wentBackground = true;
                return;
            }
            if (!wentBackground)
                return;
            wentBackground = false;
            AdZativeMainThread.Run(MoveOnAfterClick(runningSlotId));
        }

        // One frame after the return, so the "clicked" event queued before the pause has been dispatched and counted
        // (AdBase → AdCtr.RecordClick).
        private static IEnumerator MoveOnAfterClick(string slotId)
        {
            yield return null;
            if (slotId != runningSlotId || !isClicked || AdCtr.Get(runningAdUnitId) <= AdCtr.Max)
                yield break;
            isClicked = false;
            if (isLastAd)
                AdZativeSDK.CloseFullscreen();
            else
                AdZativeSDK.NextFullscreen();
        }

        private static IEnumerator Countdown(string slotId, AdZativeTiming timing, int watchSec, bool canClose, int lockedSec)
        {
            var oneSecond = new WaitForSecondsRealtime(1f);
            // ✕ and Next both wait for this countdown: with Next left to the native clock, it appeared a few frames
            // before the chip was hidden and pushed the chip aside.
            AdZativeSDK.SetFullscreenButtons(slotId, AdZativeButtonMode.Hide, AdZativeButtonMode.Hide);
            for (int elapsedSec = 0; elapsedSec < watchSec; elapsedSec++)
            {
                float alpha = elapsedSec < timing.countdownDimmedSec ? timing.dimmedAlpha : 1f;
                AdZativeSDK.SetFullscreenCountdown(slotId, watchSec - elapsedSec, alpha);
                yield return oneSecond;
            }
            AdZativeSDK.SetFullscreenCountdown(slotId, 0, 1f);
            // ✕ only on the last ad of a pod (or on every ad, podCloseOnEveryAd); the native layer shows "Next ›" only
            // while the pod has a next ad. Both are dimmed and locked the same way ("close_blocked" / "next_blocked").
            var close = canClose ? AdZativeButtonMode.Show : AdZativeButtonMode.Hide;
            if (lockedSec > 0)
            {
                AdZativeSDK.SetFullscreenButtonAlpha(slotId, timing.dimmedAlpha, timing.dimmedAlpha);
                AdZativeSDK.SetFullscreenButtons(slotId, canClose ? AdZativeButtonMode.Locked : AdZativeButtonMode.Hide,
                    AdZativeButtonMode.Locked);
                yield return new WaitForSecondsRealtime(lockedSec);
            }
            AdZativeSDK.SetFullscreenButtonAlpha(slotId, 1f, 1f);
            AdZativeSDK.SetFullscreenButtons(slotId, close, AdZativeButtonMode.Show);
        }
    }
}
