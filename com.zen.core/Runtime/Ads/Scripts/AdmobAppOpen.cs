using System;
using System.Collections;
using UnityEngine;

#if USE_ADMOB
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
#endif

namespace Base.Ads
{
    public class AdmobAppOpen : AdmobBase
    {
        [Header("AppOpen Zative")]
        [SerializeField] protected AdZative zative;
        protected AppOpenAd ad = null;

#if !USE_ADMOB
        protected class AppOpenAd : FakeAd
        {

        }
        protected class AppOpenAdPreloader : FakeAd
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

                if (appState == AppState.Foreground)
                {
                    // Check only: dequeue right before Show() (Google: "Avoid calling DequeueAd until you're ready to show an ad").
                    if (ad != null)
                        return ad.CanShowAd();
                    return FindAvailablePreloadId(AppOpenAdPreloader.IsAdAvailable) != null;
                }
                return false;
            }
        }

        protected override void Start()
        {
#if USE_ADMOB
            AppStateEventNotifier.AppStateChanged += OnAppStateChanged;
            OnAppOpenStateChanged += OnAppOpenStateChanged;
#endif
        }

        protected override void OnDestroy()
        {
#if USE_ADMOB
            AppStateEventNotifier.AppStateChanged -= OnAppStateChanged;
            DestroyAd();
            base.OnDestroy();
#endif
        }

        private AppState appState = AppState.Background;
        private Coroutine coroutineCheckIsCanShow = null;

        private void OnAppStateChanged(AppState state)
        {
            appState = state;
            Log($"OnAppStateChanged: {appState}");

            UnityMainThreadDispatcher.Enqueue(OnForegroundCheck);
        }

        /// <summary>Test All Ads panel: run the "player came back to the app" flow now.</summary>
        public void DebugSimulateForeground()
        {
            appState = AppState.Foreground;
            OnForegroundCheck();
        }

        private void OnForegroundCheck()
        {
            if (appState == AppState.Background)
                return;

            // The player comes back from where the game sent them (store page to rate): no ad on this return.
            if (AdsManager.ConsumeSuppressNextAppOpen())
            {
                Log("Foreground after a suppressed exit (e.g. store review) --> skip app open");
                return;
            }

            if (!isCanLoad
            || !AdsManager.IsTimeToShowAds)
                return;

            // Never put an app open ad on top of a fullscreen ad still on screen (e.g. the player comes back from
            // the store after clicking an interstitial that is not closed yet).
            if (AdsManager.IsFullscreenAdShowing)
            {
                Log("Foreground while a fullscreen ad is showing --> skip app open");
                return;
            }

            if (coroutineCheckIsCanShow != null)
                return;
            coroutineCheckIsCanShow = StartCoroutine(IECheckIsCanShow());
        }

        IEnumerator IECheckIsCanShow()
        {
            yield return new WaitForEndOfFrame();

            item = GameStateManager.CurrentState.ToString();
            AdState adState = AdState.None;
            if (config.adAppOpenFlow == AdFlow.OnlyNative && zative != null)
            {
                yield return zative.IELoadShowAd((type, state) =>
                {
                    adState = state;
                }, item, config.adLoadTimeout, ShowLoadingMode.Full);
            }
            else if (config.adAppOpenFlow == AdFlow.OnlyDefault)
            {
                yield return IELoadShowAd((type, state) =>
                {
                    adState = state;
                }, item, config.adLoadTimeout, ShowLoadingMode.Full);
            }
            else
            {
                yield return IELoadAd((type, state) =>
                {
                    adState = state;
                }, item, config.adLoadTimeout, ShowLoadingMode.Full);

                if (adState == AdState.LoadAvailable)
                {
                    yield return IEShowAd((type, state) =>
                    {
                        adState = state;
                    }, item, 0.25f);
                }
                else if ((adState == AdState.LoadNotAvailable || adState == AdState.LoadTimeOut) && zative != null)
                {
                    yield return zative.IELoadShowAd((type, state) =>
                    {
                        adState = state;
                    }, item, config.adLoadTimeout, ShowLoadingMode.Full);
                }
            }

            coroutineCheckIsCanShow = null;
        }

        public override IEnumerator IELoadAd(Action<AdType, AdState> result, string item, float timeOut, ShowLoadingMode showLoadingMode = ShowLoadingMode.Full)
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
                AppOpenAd.Load(requestId, request, (ad, error) =>
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
            else if (!isCanShow) //isCanShow = AppOpenAdPreloader.IsAdAvailable(id);
            {
                Log($"-------------- Preload");
                var preloadConfiguration = new PreloadConfiguration
                {
                    AdUnitId = id,
                    Request = new AdRequest(),
                    BufferSize = bufferSize
                };

                bool started = AppOpenAdPreloader.Preload(id,
                                            preloadConfiguration,
                                            OnAdPreloaded,
                                            OnAdFailedToPreload,
                                            OnAdsExhausted);
                if (!started)
                {
                    // Same preload ID already running (Preload is a no-op, returns false): the SDK keeps its buffer
                    // full by itself. If it already holds an ad, the step succeeds now instead of waiting for a timeout.
                    Log($"Preload {id} already running - waiting for the SDK buffer");
                    if (AppOpenAdPreloader.IsAdAvailable(id))
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
            AppOpenAdPreloader.Destroy(preloadId);
        }

#if USE_ADMOB
        /// <summary>Takes the ad to show NOW: the loaded one, or one dequeued from the first preload ID that has one.</summary>
        private bool TakeAd()
        {
            if (ad != null)
                return ad.CanShowAd();
            string preloadId = FindAvailablePreloadId(AppOpenAdPreloader.IsAdAvailable);
            if (preloadId == null)
                return false;
            ad = AppOpenAdPreloader.DequeueAd(preloadId);
            if (ad == null)
                return false;
            id = preloadId;
            GetSourceInfo(ad.GetResponseInfo());
            Log($"DequeueAd {preloadId} to show (left in buffer: {AppOpenAdPreloader.GetNumAdsAvailable(preloadId)})");
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
            if (appState == AppState.Foreground && TakeAd())
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

        protected override void OnAdClose()
        {
            base.OnAdClose();
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
    }
}
