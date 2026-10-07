using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GoogleMobileAds.Common;

#if USE_ADMOB
using GoogleMobileAds.Api;
#endif

namespace Base.Ads
{
    public class AdmobBase : AdBase
    {
        [Header("Buffer size controls the number of preloaded ads held in memory.")]
        public uint bufferSize = 0;

        // Waterfall step ticket: callbacks of an earlier step (late fill after a timeout, preloader refills,
        // an error of the previous ID) must not change the state of the step in progress.
        internal int loadSeq;
        internal string loadingId;

        protected override void Awake()
        {
            base.Awake();
            if (idList.Count == 0)
            {
                idList = new List<string> { FallbackTestId };
                LogWarning($"No ad unit ID for {placement} in ZenAdIds (Base > Hub > Setup) --> Google test ID {idList[0]}");
            }
        }

        /// <summary>Test ID used when the game has no ID for this placement.</summary>
        protected virtual string FallbackTestId => GetIdTest(false);

        public IEnumerator IELoadAdWaterfall(Action action, Action<AdType, AdState> result, string itemName, float timeOut)
        {
            if (!isCanLoad)
                yield break;

            if (idList == null || idList.Count == 0)
            {
                error = $"idList {placementName} is NULL or EMPTY";
                OnAdLoadError(error, null);
                yield break;
            }

            if (state == AdState.LoadRequest)
            {
                result?.Invoke(type, AdState.Exception);
                LogWarning("LoadRequest --> RETURN");
                yield break;
            }

            for (int i = 0; i < idList.Count; i++)
            {
                if (state == AdState.LoadAvailable || state == AdState.ShowSuccess || state == AdState.LoadTimeOut)
                    break;

                id = idList[i];

                if (AdsManager.UseIdTest)
                {
                    id = GetIdTest(true);
                    LogWarning($"LoadAd TEST MODE ID: {id} START...");
                }

                if (string.IsNullOrEmpty(id))
                {
                    state = AdState.Exception;
                    LogError($"LoadAd ID [{i}] in {placementName} is NULL or EMPTY");
                    continue;
                }

                loadSeq++;
                loadingId = id;
                Log($"LoadAd Waterfall [{i}] - ID: {id} START... with timeout: {timeOut:#0.0} bufferSize {bufferSize}");

#if UNITY_EDITOR
                //Fake delay to simulate real ad loading time and make the editor testing more realistic.
                //In production, the action will be invoked immediately to start loading the ad.
                yield return new WaitForSecondsRealtime(2.25f);
#endif
                action?.Invoke();

                yield return IEWaitLoadAd(timeOut);
            }

            if (state != AdState.LoadAvailable && state != AdState.ShowSuccess)
            {
                LogWarning("Waterfall finished but NO Ad loaded");
            }
        }

        public IEnumerator IEShowAd(IEnumerator action, Action<AdType, AdState> result, string itemName, float delayShow = 0.25f)
        {
            yield return base.IEShowAd(result, itemName, delayShow);

            if (isCanShow)
            {
                yield return action;

                // The show did not start (no ad taken, failed synchronously): pausing now would leave the game at
                // timeScale 0 with nothing to resume it (IEWaitShowAd exits at once). A show that fails later via the
                // dispatcher is fine: its OnAdShowFailed resumes after this pause.
                bool notStarted = state == AdState.ShowNotAvailable || state == AdState.ShowFailed || state == AdState.None;
                if (!notStarted && (type == AdType.Inter || type == AdType.Reward || type == AdType.RewardInter || type == AdType.AppOpen))
                {
                    PauseApp(true, $"IEShowAd {placement} {type} {item}");
                }

                yield return IEWaitShowAd();
            }

            yield return waitForSeconds;
        }

        protected virtual void OnAdLoadError(AdError error, Action onMaxRetry = null)
        {
            try
            {
                if (error != null)
                    this.error = error.GetMessage();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                this.error = "Unknown error";
            }
            base.OnAdLoadError(this.error, onMaxRetry);
        }

        protected void OnAdShowFailed(AdError error)
        {
            try
            {
                if (error != null) this.error = error.GetMessage();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                this.error = "Unknown error";
            }
            OnAdShowFailed(this.error);
        }

        protected virtual void OnAdPaid(AdValue adValue)
        {
            LogImpressionData(adValue);
        }

        protected virtual void OnSuccess()
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                state = AdState.ShowSuccess;
            });
        }

        public void GetSourceInfo(ResponseInfo responseInfo)
        {
            try
            {
                if (responseInfo != null)
                {
                    string responseId = responseInfo.GetResponseId();
                    if (!string.IsNullOrEmpty(responseId))
                    {
                        var adapter = responseInfo.GetLoadedAdapterResponseInfo();
                        if (adapter != null)
                        {
                            sourceId = adapter.AdSourceId;
                            sourceName = adapter.AdSourceName;
                            //Log($"GetSourceInfo responseId: {responseId} sourceId:{sourceId} sourceName:{sourceName}");
                        }
                    }
                }
            }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        /// <summary>True when a load callback belongs to the waterfall step currently waiting.</summary>
        protected bool IsCurrentStep(string adUnitId, int seq = -1)
        {
            return state == AdState.LoadRequest && adUnitId == loadingId && (seq < 0 || seq == loadSeq);
        }

        // Preloader callbacks run on an SDK thread (GMA 11.5.0: MobileAds.RaiseAction invokes them directly) and fire for
        // EVERY request the preloader makes, buffer refills included. Only the waiting waterfall step may change state.
        internal virtual void OnAdPreloaded(string preloadId, ResponseInfo responseInfo)
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                // Source info is taken from the dequeued ad at show time (TakeAd); a refill must not overwrite the
                // source of the ad on screen (it is reported with the paid event).
                if (IsCurrentStep(preloadId))
                {
                    GetSourceInfo(responseInfo);
                    state = AdState.LoadAvailable;
                }
                else
                    Log($"OnAdPreloaded {preloadId} (buffer refill / earlier step) - state stays {state}");
            });
        }

        internal virtual void OnAdFailedToPreload(string preloadId, AdError adError)
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                if (IsCurrentStep(preloadId))
                {
                    // The waterfall step failed: stop this preload ID so the waterfall moves on (original intent).
                    DestroyPreloader(preloadId);
                    OnAdLoadError(adError);
                }
                else
                {
                    // Refill failure while ads may still be buffered: do NOT destroy (Destroy drops every buffered ad and
                    // stops the SDK's managed retries with exponential backoff).
                    LogWarning($"OnAdFailedToPreload {preloadId} ignored (refill / earlier step)");
                }
            });
        }

        /// <summary>Simple-load failure of waterfall step {seq}: stale steps are ignored.</summary>
        protected void OnAdLoadError(AdError error, string adUnitId, int seq)
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                if (IsCurrentStep(adUnitId, seq))
                    OnAdLoadError(error);
                else
                    LogWarning($"Load error of {adUnitId} ignored (not the current waterfall step)");
            });
        }

        /// <summary>Stops preloading for this preload ID (format-specific preloader). Called on the main thread.</summary>
        protected virtual void DestroyPreloader(string preloadId) { }

        /// <summary>
        /// Preload ID that holds a ready ad: the current ID first, then the waterfall list in priority order.
        /// Check only - nothing is dequeued ("Avoid calling DequeueAd until you're ready to show an ad").
        /// </summary>
        protected string FindAvailablePreloadId(Func<string, bool> isAdAvailable)
        {
            if (!string.IsNullOrEmpty(id) && isAdAvailable(id))
                return id;
            if (idList == null)
                return null;
            for (int i = 0; i < idList.Count; i++)
            {
                string candidate = idList[i];
                if (!string.IsNullOrEmpty(candidate) && candidate != id && isAdAvailable(candidate))
                    return candidate;
            }
            return null;
        }

        internal void OnAdsExhausted(string preloadId)
        {
            LogWarning($"OnAdsExhausted Preload ad configuration {preloadId} {item} was exhausted with BufferSize {bufferSize}");
        }

        public string GetIdTest(bool random)
        {
            if (placementName.Contains("Zative"))
            {
                if (random)
                {
                    if (UnityEngine.Random.Range(0, 2) == 0)
                        return Application.platform == RuntimePlatform.IPhonePlayer ? "ca-app-pub-3940256099942544/3986624511" : "ca-app-pub-3940256099942544/2247696110";
                    else
                        return Application.platform == RuntimePlatform.IPhonePlayer ? "ca-app-pub-3940256099942544/2521693316" : "ca-app-pub-3940256099942544/1044960115";
                }
                else
                {
                    return Application.platform == RuntimePlatform.IPhonePlayer ? "ca-app-pub-3940256099942544/2521693316" : "ca-app-pub-3940256099942544/1044960115";
                }
            }
            else if (placementName.Contains("Banner") || placementName.Contains("Mrec"))
                return Application.platform == RuntimePlatform.IPhonePlayer ? "ca-app-pub-3940256099942544/2435281174" : "ca-app-pub-3940256099942544/9214589741";
            else if (placementName.Contains("RewardInter"))
                return Application.platform == RuntimePlatform.IPhonePlayer ? "ca-app-pub-3940256099942544/6978759866" : "ca-app-pub-3940256099942544/5354046379";
            else if (placementName.Contains("Inter"))
                return Application.platform == RuntimePlatform.IPhonePlayer ? "ca-app-pub-3940256099942544/4411468910" : "ca-app-pub-3940256099942544/1033173712";
            else if (placementName.Contains("Reward"))
                return Application.platform == RuntimePlatform.IPhonePlayer ? "ca-app-pub-3940256099942544/1712485313" : "ca-app-pub-3940256099942544/5224354917";
            else if (placementName.Contains("AppOpen"))
                return Application.platform == RuntimePlatform.IPhonePlayer ? "ca-app-pub-3940256099942544/5575463023" : "ca-app-pub-3940256099942544/9257395921";
            return "";
        }
    }

#if !USE_ADMOB
    public class FakeAd
    {
        internal bool CanShowAd() { return false; }
        internal void Load(string id, object request, Action<object, object> value) { }
        internal void Show() { }
        internal void Destroy() { }
        internal void Hide() { }
        internal static int GetNumAdsAvailable(string id) { return 0; }
        internal static bool IsAdAvailable(string id) { return false; }
        internal static void Destroy(string id) { }
        internal Action OnAdFullScreenContentClosed { get; set; }
        internal Action<AdError> OnAdFullScreenContentFailed { get; set; }
        internal Action OnAdImpressionRecorded { get; set; }
        internal Action<AdValue> OnAdPaid { get; set; }
        internal static object DequeueAd(string id)
        {
            throw new NotImplementedException();
        }
    }
    public class AdError 
    { 
        public AdError(string code, string message, string domain) { }
        internal string GetMessage() { return null; } 
    }
    public class LoadAdError : AdError 
    { 
        public LoadAdError(string code, string message, string domain) : base(code, message, domain) { }
    }
    public class AdValue { public long Value { get; internal set; } public string CurrencyCode { get; internal set; } public PrecisionType Precision { get; internal set; } public enum PrecisionType { } }
    public class ResponseInfo { internal AdapterResponseInfo GetLoadedAdapterResponseInfo() { return new AdapterResponseInfo(); } internal string GetResponseId() { return null; } }
    public class AdapterResponseInfo { public string AdSourceId { get; internal set; } public string AdSourceName { get; internal set; } }
    public enum AdPosition { Top, Bottom, Center }
    public enum AppState { Foreground, Background }

    public class MobileAdsEventExecutor
    {
        internal static void ExecuteInUpdate(Action value)
        {
            value?.Invoke();
        }
    }
#endif
}