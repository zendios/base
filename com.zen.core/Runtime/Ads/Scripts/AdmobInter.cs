using System;
using System.Collections;
using UnityEngine;

#if USE_ADMOB
using GoogleMobileAds.Api;
#endif

namespace Base.Ads
{
    public class AdmobInter : AdmobBase
    {
        protected InterstitialAd ad = null;

#if !USE_ADMOB
        protected class InterstitialAd : FakeAd { }
        protected class InterstitialAdPreloader : FakeAd
        {
        }
#endif

        public override bool isCanShow
        {
            get
            {
                if (!ADMOB.IsInitialized)
                {
                    state = AdState.Exception;
                    LogWarning($"MobileAds.initialize must be called before using the Google Mobile Ads SDK --> RETURN");
                    return false;
                }

                // Check only: dequeue right before Show() (Google: "Avoid calling DequeueAd until you're ready to show an ad").
                if (ad != null)
                    return ad.CanShowAd();
                return FindAvailablePreloadId(InterstitialAdPreloader.IsAdAvailable) != null;
            }
        }

        public override IEnumerator IELoadAd(Action<AdType, AdState> result, string item, float timeOut, ShowLoadingMode showLoadingMode = ShowLoadingMode.Toast)
        {
            if (!isCanLoad)
                yield break;

            if (!string.IsNullOrEmpty(item))
                this.item = item;
            this.result = result;

            if (!isCanShow)
            {
                yield return base.IELoadAd(result, item, timeOut, showLoadingMode);
                yield return IELoadAdWaterfall(LoadAd, result, item, timeOut);
            }
            else
            {
                state = AdState.LoadRequest;
                state = AdState.LoadAvailable;
            }
        }

        protected override void LoadAd()
        {
            base.LoadAd();
#if USE_ADMOB
            if (bufferSize == 0)
            {
                Log($"-------------- Simple");
                AdRequest request = new AdRequest();
                int seq = loadSeq;
                string requestId = id;
                InterstitialAd.Load(requestId, request, (ad, error) =>
                {
                    if (ad == null || error != null)
                    {
                        OnAdLoadError(error, requestId, seq);
                        return;
                    }

                    UnityMainThreadDispatcher.Enqueue(() =>
                    {
                        if (!IsCurrentStep(requestId, seq))
                        {
                            // Late fill of an earlier step (after its timeout): keep it for the next show if nothing is held,
                            // without changing the state of the step in progress.
                            if (this.ad == null && state != AdState.ShowRequest && state != AdState.Show)
                            {
                                GetSourceInfo(ad.GetResponseInfo());
                                this.ad = ad;
                                Log($"Late fill of {requestId} kept for the next show");
                            }
                            else
                            {
                                ad.Destroy();
                            }
                            return;
                        }

                        GetSourceInfo(ad.GetResponseInfo());
                        if (this.ad != null)
                        {
                            this.ad.Destroy();
                            this.ad = null;
                            Log("LoadAvailable --> Destroy Old Ad");
                        }
                        this.ad = ad;
                        state = AdState.LoadAvailable;
                    });
                });
            }
            else if (!isCanShow)
            {
                Log($"-------------- Preload bufferSize {bufferSize}");
                var preloadConfiguration = new PreloadConfiguration
                {
                    AdUnitId = id,
                    Request = new AdRequest(),
                    BufferSize = bufferSize
                };

                bool started = InterstitialAdPreloader.Preload(id,
                                            preloadConfiguration,
                                            OnAdPreloaded,
                                            OnAdFailedToPreload,
                                            OnAdsExhausted);
                if (!started)
                {
                    // Same preload ID already running (Preload is a no-op, returns false): the SDK keeps its buffer
                    // full by itself. If it already holds an ad, the step succeeds now instead of waiting for a timeout.
                    Log($"Preload {id} already running - waiting for the SDK buffer");
                    if (InterstitialAdPreloader.IsAdAvailable(id))
                        state = AdState.LoadAvailable;
                }
            }
            else
            {
                if (isCanShow)
                    state = AdState.LoadAvailable;
                else
                    state = AdState.LoadNotAvailable;
            }
#endif
        }

        protected override void DestroyPreloader(string preloadId)
        {
            InterstitialAdPreloader.Destroy(preloadId);
        }

#if USE_ADMOB
        /// <summary>Takes the ad to show NOW: the loaded one, or one dequeued from the first preload ID that has one.</summary>
        private bool TakeAd()
        {
            if (ad != null)
                return ad.CanShowAd();
            string preloadId = FindAvailablePreloadId(InterstitialAdPreloader.IsAdAvailable);
            if (preloadId == null)
                return false;
            ad = InterstitialAdPreloader.DequeueAd(preloadId);
            if (ad == null)
                return false;
            id = preloadId;
            GetSourceInfo(ad.GetResponseInfo());
            Log($"DequeueAd {preloadId} to show (left in buffer: {InterstitialAdPreloader.GetNumAdsAvailable(preloadId)})");
            return ad.CanShowAd();
        }
#endif

        public override IEnumerator IEShowAd(Action<AdType, AdState> result, string itemName, float delayShow = 0.25f)
        {
            yield return IEShowAd(IEShowAd(), result, itemName, delayShow);
        }

        public IEnumerator IEShowAd()
        {
#if USE_ADMOB
            if (TakeAd())
            {
                ad.OnAdFullScreenContentClosed += OnAdClose;
                ad.OnAdFullScreenContentFailed += OnAdShowFailed;
                ad.OnAdImpressionRecorded += OnSuccess;
                ad.OnAdPaid += OnAdPaid;
                state = AdState.Show;
                ad.Show();
            }
            else
            {
                state = AdState.ShowNotAvailable;
            }
#endif
            yield return new WaitForEndOfFrame();
        }

        public override void DestroyAd()
        {
            if (ad != null)
            {
                ad.OnAdFullScreenContentClosed -= OnAdClose;
                ad.OnAdFullScreenContentFailed -= OnAdShowFailed;
                ad.OnAdImpressionRecorded -= OnSuccess;
                ad.OnAdPaid -= OnAdPaid;
                ad.Destroy();
                ad = null;
                Log($"DestroyAd {placement} SUCCESS");
            }
            base.DestroyAd();
        }

        protected override void OnDestroy()
        {
            DestroyAd();
            base.OnDestroy();
        }
    }
}
