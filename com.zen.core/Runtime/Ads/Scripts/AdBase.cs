#if USE_APPSFLYER
using AppsFlyerSDK;
#endif
#if USE_ADMOB
using GoogleMobileAds.Api;
#endif
#if USE_ADMOB
using GoogleMobileAds.Common;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Base.Ads
{
    public abstract class AdBase : MonoBehaviour
    {
        protected AdPlatform platform = AdPlatform.ADMOB;
        public AdType type = AdType.Inter;
        public PlacementType placement = PlacementType.None;
        [SerializeField] protected string placementName = "default";
        [HideInInspector] public AdIdData adIdData;   // optional per-prefab override of the ZenAdIds row (hidden: IDs live in ZenAdIds)
        protected string item = "default";
        protected string TAG = "ADMOB -> "; [Tooltip("A unique name for this ad placement. This MUST be unique for each ZativeAd object in the scene.")]

        internal string internet;
        internal bool hasInternet
        {
            get
            {
                switch (Application.internetReachability)
                {
                    case NetworkReachability.ReachableViaCarrierDataNetwork: internet = "Carrier"; return true;
                    case NetworkReachability.ReachableViaLocalAreaNetwork: internet = "Wifi"; return true;
                    default: internet = "None"; return false;
                }
            }
        }

        internal string sourceId = "";
        internal string sourceName = "";
        internal string error = "";
        internal string id = "";
        internal List<string> idList = new List<string>();

        public const string errorUnknown = "Unknown Error";
        public const string errorNull = "Error Null";

        protected WaitForSeconds waitForSeconds;
        protected Action<AdType, AdState> result;
        protected DateTime loadTimeStart;
        protected double loadTime;
        protected DateTime durationTimeStart;
        protected double durationTime;

        #region RELOAD
        internal int retryAttempt = 0;
        internal const int MAX_RETRY_ATTEMPTS = 3;
        internal const int MAX_RETRY_DELAY_SECONDS = 30;
        internal Coroutine reloadCoroutine;
        #endregion

        protected UserData userData => DataManager.UserData;
        protected AdConfig config => AdConfig.Instance;

        [SerializeField]
        protected AdState _state = AdState.None;
        public AdState state
        {
            get => _state;
            protected set
            {
                if (_state != value)
                {
                    Log($"{_state} -> {value} item: {item}");
                    _state = value;

                    if (_state == AdState.LoadAvailable || _state == AdState.LoadTimeOut || _state == AdState.LoadNotAvailable
                        || _state == AdState.ShowSuccess || _state == AdState.ShowFailed || _state == AdState.ShowNotAvailable
                        || _state == AdState.Close || _state == AdState.NotTime)
                    {
                        result?.Invoke(type, _state);

                        if (_state == AdState.ShowSuccess)
                        {
                            if (type == AdType.Inter || type == AdType.AppOpen)
                            {
                                userData.adInterstitial++;
                            }
                            else if (type == AdType.Reward || type == AdType.RewardInter)
                            {
                                userData.adRewarded++;
                            }
                            else if (type == AdType.Banner || type == AdType.Mrec)
                            {
                                userData.adBanner++;
                            }
                        }
                    }

                    if (_state == AdState.LoadRequest)
                    {
                        ResetLogLoad();

                        if (type == AdType.AppOpen)
                            OnAppOpenStateChanged?.Invoke(_state);
                    }
                    else if (_state == AdState.LoadAvailable)
                    {
                        isCanShow = true;
                        retryAttempt = 0;
                        loadTime = 0;
                        isStillLoading = false;
                        loadTime = (DateTime.Now - loadTimeStart).TotalSeconds; Log(_state.ToString() + " " + id + " : " + loadTime.ToString("#0.0") + "s");
                    }
                    else if (_state == AdState.LoadNotAvailable)
                    {
                        isStillLoading = false;
                        loadTime = (DateTime.Now - loadTimeStart).TotalSeconds; LogError(_state.ToString() + " " + id + " : " + loadTime.ToString("#0.0") + "s" + " with ERROR: " + error);

                        if (type == AdType.AppOpen)
                            OnAppOpenStateChanged?.Invoke(_state);
                    }
                    else if (_state == AdState.LoadTimeOut || _state == AdState.ShowNotAvailable)
                    {
                        // App open that will not show: banners hidden/destroyed on its LoadRequest must come back.
                        if (type == AdType.AppOpen)
                            OnAppOpenStateChanged?.Invoke(_state);
                    }
                    else if (_state == AdState.Show)
                    {
                        ResetLogShow();

                        if (type == AdType.AppOpen)
                            OnAppOpenStateChanged?.Invoke(_state);
                    }
                    else if (_state == AdState.ShowFailed)
                    {
                        LogError(_state.ToString() + " " + id + " : " + " with ERROR: " + error);

                        if (type == AdType.AppOpen)
                            OnAppOpenStateChanged?.Invoke(_state);
                    }
                    else if (_state == AdState.Close)
                    {
                        durationTime = (DateTime.Now - durationTimeStart).TotalSeconds; Log(_state.ToString() + " " + id + " : " + durationTime.ToString("#0.0") + "s");

                        if (type == AdType.AppOpen)
                            OnAppOpenStateChanged?.Invoke(_state);
                    }

                    if (_state != AdState.None && _state != AdState.Show && _state != AdState.Close && _state != AdState.NotTime)
                        LogState();

                    if (_state == AdState.ShowSuccess)
                        LogMilestone();

                    AdsManager.lastStage = _state;
                    OnAnyStateChanged?.Invoke(this, _state);
                }
            }
        }

        public delegate void StateChanged(AdState state);
        public static StateChanged OnAppOpenStateChanged;

        /// <summary>Ad source of the last load / show (e.g. "AdMob Network"), and the last error message.</summary>
        /// <summary>Every state change of every placement (Test All Ads panel log).</summary>
        public static event Action<AdBase, AdState> OnAnyStateChanged;

        public string SourceName => sourceName;
        public string LastError => error;

        public virtual bool isCanShow { get { return false; } set { } }

        internal bool isInitialized = false;

        internal bool isStillLoading = false;

        internal ShowLoadingMode showLoadingMode;

        public bool isCanLoad
        {
            get
            {
                if (!ADMOB.IsInitialized)
                {
                    state = AdState.Exception;
                    LogWarning($"MobileAds.initialize must be called before using the Google Mobile Ads SDK --> RETURN");
                    return false;
                }

                if (type != AdType.Reward && type != AdType.RewardInter && AdsManager.IsRemovedAds)
                {
                    LogWarning($"Not allowed for removed ads users --> RETURN");
                    return false;
                }

                if (isStillLoading)
                {
                    if (isStillLoading && (DateTime.Now - lastTimeLoad).TotalSeconds > 60)
                    {
                        isStillLoading = false;
                        state = AdState.LoadTimeOut;
                        LogWarning($"Wait load ad too long --> RESET state");
                        return true;
                    }
                    LogWarning("Ad is still loading --> RETURN");
                    return false;
                }

                if (state == AdState.LoadRequest || state == AdState.ShowRequest || state == AdState.Show)
                {
                    LogWarning("State: " + state.ToString() + " --> RETURN");
                    return false;
                }
                return true;
            }
        }

        public virtual void OnValidate()
        {
            if (adIdData != null && adIdData.name.Equals(placement.ToString()))
            {
                Debug.LogError($"AdIdData name {adIdData.name} should NOT be the same as PlacementType {placement}! Please change the name of AdIdData to a unique value.");
            }

            if (type.ToString().Contains(placement.ToString()))
            {
                Debug.LogError($"AdType {type} should NOT contain PlacementType {placement}! Please ensure that the AdType and PlacementType are distinct.");
            }

            TAG = "[" + placementName.ToString().ToUpper() + "] -> ";
        }

        /// <summary>
        /// Ad unit IDs of this placement, in waterfall order: the AdIdData set on the prefab (optional override), else the
        /// game's ZenAdIds row of this PlacementType. Empty when the game has none (AdmobBase then uses a test ID).
        /// </summary>
        protected List<string> ResolveIds()
        {
            if (adIdData != null)
                return adIdData.list.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
            var ids = ZenAdIds.Instance;
            return ids != null ? ids.Get(placement) : new List<string>();
        }

        protected virtual void Awake()
        {
            state = AdState.None;
            idList = ResolveIds();
            waitForSeconds = new WaitForSeconds(0.125f);
            TAG = "[" + placementName.ToString().ToUpper() + "] -> ";
        }

        protected virtual void Start()
        {
            if (AdsManager.List != null && !AdsManager.List.Contains(this))
                AdsManager.List.Add(this);
        }

        public readonly string noticeOnAdBrake = "ADs break in progress...";
        public readonly string noticeOnAdClose = "Thanks for watching ADs...";
        /// <summary>The "ADs break" toast of the current show is still up (it has no auto-hide).</summary>
        protected bool adBreakNoticeShown;

        protected DateTime lastTimeLoad = DateTime.Now;
        protected virtual void LoadAd()
        {
            if (isStillLoading)
            {
                Log($"Ad {type} is still loading...");
                return;
            }

            state = AdState.LoadRequest;
            lastTimeLoad = DateTime.Now;
            isStillLoading = true;

            if (!hasInternet)
            {
                error = "No internet connection. Aborting load immediately to prevent deadlocks";
                OnAdLoadError(error, null);
                return;
            }
        }

        public virtual IEnumerator IELoadShowAd(Action<AdType, AdState> result, string item, float timeOut = 6.8f, ShowLoadingMode showLoadingMode = ShowLoadingMode.None)
        {
            if (!string.IsNullOrEmpty(item))
                this.item = item;
            else
                this.item = GameStateManager.CurrentState.ToString();

            // The caller (e.g. a reward button) must ALWAYS get a terminal state: loads can end silently
            // (isCanLoad false, Exception, still loading) and the state setter does not forward Exception.
            AdState lastDelivered = AdState.None;
            Action<AdType, AdState> tracked = (t, s) =>
            {
                if (IsTerminalState(s)) lastDelivered = s;
                result?.Invoke(t, s);
            };
            this.result = tracked;
            this.showLoadingMode = showLoadingMode;

            if (isCanLoad)
                yield return IELoadAd(tracked, item, timeOut, showLoadingMode);

            if (isCanShow)
                yield return IEShowAd(tracked, item);

            bool finished = lastDelivered != AdState.None && lastDelivered != AdState.LoadAvailable;
            bool onScreen = state == AdState.ShowRequest || state == AdState.Show || state == AdState.ShowSuccess || state == AdState.Click;
            if (!finished && !onScreen)
            {
                Log($"IELoadShowAd ended without a result ({state}) --> ShowNotAvailable");
                if (showLoadingMode != ShowLoadingMode.None)
                    AdToast.HideLoading();
                result?.Invoke(type, AdState.ShowNotAvailable);
            }
        }

        /// <summary>States the state setter forwards to the caller's callback.</summary>
        internal static bool IsTerminalState(AdState s) =>
            s == AdState.LoadAvailable || s == AdState.LoadTimeOut || s == AdState.LoadNotAvailable
            || s == AdState.ShowSuccess || s == AdState.ShowFailed || s == AdState.ShowNotAvailable
            || s == AdState.Close || s == AdState.NotTime;

        public virtual IEnumerator IELoadAd(Action<AdType, AdState> result, string item, float timeOut, ShowLoadingMode showLoadingMode = ShowLoadingMode.None)
        {
            if (!string.IsNullOrEmpty(item))
                this.item = item;
            else
                this.item = GameStateManager.CurrentState.ToString();
            this.result = result;
            this.showLoadingMode = showLoadingMode;

            string notice = noticeOnAdBrake;
            if (DebugMode.IsOn)
                notice += placement.ToString();

            if (showLoadingMode == ShowLoadingMode.Full)
                AdToast.ShowLoading(notice, timeOut * 1.25f, true);
            else if (showLoadingMode == ShowLoadingMode.Toast)
                AdToast.ShowLoading(notice, timeOut * 1.25f, false);

            yield return new WaitForEndOfFrame();
        }

        protected IEnumerator IEWaitLoadAd(float timeOut)
        {
            // Real time: with timeScale 0 (pause menu, a fullscreen ad) a scaled timer never expires and the
            // waterfall step - and the caller waiting on it, e.g. a reward button - would wait forever.
            float end = Time.realtimeSinceStartup + timeOut;
            while (state == AdState.LoadRequest && Time.realtimeSinceStartup < end)
                yield return null;

            if (state == AdState.LoadRequest)
            {
                state = AdState.LoadTimeOut;
                LogWarning($"IEWaitLoadAd ID: {id} TIMEOUT! No response within {timeOut} seconds.");
            }
        }

        public virtual void ReloadAd(float delayTime = 0.25f)
        {
            if (AdsManager.IsRemovedAds)
            {
                if (DebugMode.IsOn)
                    LogWarning($"ReloadAd skipped: Ads are removed or an inter/open/reward ad is currently showing.");
                return;
            }

            if (delayTime == 0)
                delayTime = 0.25f;

            UnityMainThreadDispatcher.Enqueue(() =>
            {
                Log($"Ad re-load in {delayTime}s");
                if (reloadCoroutine != null)
                    StopCoroutine(reloadCoroutine);
                reloadCoroutine = StartCoroutine(IEReloadAd(delayTime));
            });
        }

        public virtual IEnumerator IEReloadAd(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            // Bounded: AdSplash.IsShowing stays true if the splash coroutine throws or a game has no splash scene.
            float splashEnd = Time.realtimeSinceStartup + 10f;
            while (AdSplash.IsShowing && Time.realtimeSinceStartup < splashEnd)
                yield return null;

            state = AdState.None;
            yield return IELoadAd(null, item, config.adLoadTimeout);
            reloadCoroutine = null;
        }

        public virtual IEnumerator IEShowAd(Action<AdType, AdState> result, string item, float delayShow = 0.25f)
        {
            if (!isCanShow)
                yield break;

            state = AdState.ShowRequest;

            if (!string.IsNullOrEmpty(item))
                this.item = item;
            else
                this.item = GameStateManager.CurrentState.ToString();
            this.result = result;

            if (type == AdType.Inter || type == AdType.Reward || type == AdType.RewardInter || type == AdType.AppOpen)
            {
                AdToast.SetUpdate(noticeOnAdBrake, 0.8f, delayShow);
                adBreakNoticeShown = true;   // no auto-hide: DestroyAd hides it (reward ends without its own notice)
                // Realtime: Reward is often shown from a pause menu (timeScale 0), where WaitForSeconds never ends.
                yield return new WaitForSecondsRealtime(delayShow);
            }
        }

        protected IEnumerator IEWaitShowAd()
        {
            float elapsed = 120f;
            while ((state == AdState.Show || state == AdState.ShowSuccess) && elapsed > 0)
            {
                elapsed -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (elapsed <= 0)
            {
                if (type != AdType.Banner && type != AdType.Mrec && type != AdType.InGame)
                    PauseApp(false, "IEWaitShowAd Timeout - Force Close Ad");
            }
        }

        protected virtual void OnAdLoadError(string error, Action onMaxRetry = null)
        {
            isCanShow = false;
#if USE_ADMOB
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                if (!string.IsNullOrEmpty(error))
                {
                    this.error = error;
                    if (this.error.Contains(".")) this.error = this.error.Split(new char[] { '.' }).FirstOrDefault();
                    this.error = this.error.Substring(0, Math.Min(40, this.error.Length));
                }
                state = AdState.LoadNotAvailable;

                if (showLoadingMode != ShowLoadingMode.None)
                {
                    if (type == AdType.Reward || type == AdType.RewardInter)
                    {
                        if (hasInternet)
                            AdToast.SetUpdate("Ad load FAILED: " + this.error, 1f, 1.5f, true);
                        else
                            AdToast.SetUpdate("No Internet connection! Please check...", 1f, 1.5f, true);
                    }
                    else if (type == AdType.AppOpen || type == AdType.Inter)
                    {
                        AdToast.HideLoading();
                    }
                }

                if (id.Equals(idList.LastOrDefault()))
                {
                    if (retryAttempt < MAX_RETRY_ATTEMPTS)
                    {
                        retryAttempt++;
                        int delay = retryAttempt * 3;
                        int clampedDelay = Mathf.Min(delay, MAX_RETRY_DELAY_SECONDS);
                        Log($"Reload {retryAttempt}/{MAX_RETRY_ATTEMPTS} in {clampedDelay} seconds");

                        ReloadAd(clampedDelay);
                    }
                    else
                    {
                        LogWarning("Max retry attempts reached. Will not attempt to reload.");
                        onMaxRetry?.Invoke();
                    }
                }
            });
#endif
        }

        protected void OnAdShowFailed(string error)
        {
#if USE_ADMOB
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                if (!string.IsNullOrEmpty(error))
                {
                    this.error = error;
                    if (this.error.Contains(".")) this.error = this.error.Split(new char[] { '.' }).FirstOrDefault();
                    this.error = this.error.Substring(0, Math.Min(40, this.error.Length));
                }
                state = AdState.ShowFailed;

                if (showLoadingMode != ShowLoadingMode.None && (type == AdType.Reward || type == AdType.RewardInter))
                {
                    AdToast.ShowNotice("Ad show FAILED: " + this.error, 1.5f);
                    adBreakNoticeShown = false;   // replaced by the failure notice (auto-hides)
                }

                // Always (not only when a loading UI was shown): the game was paused for this fullscreen ad
                // (timeScale 0, audio off) and IEWaitShowAd exits on ShowFailed without resuming it.
                if (type == AdType.Inter || type == AdType.Reward || type == AdType.RewardInter || type == AdType.AppOpen)
                    PauseApp(false, $"OnAdShowFailed {placement} {type} {item}");
                DestroyAd();
            });
#endif
        }

        protected virtual void OnAdClose()
        {
#if USE_ADMOB
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                if (state == AdState.None)
                    return;

                state = AdState.Close;

                if (type != AdType.Banner && type != AdType.Mrec && type != AdType.InGame)
                {
                    DestroyAd();
                }
            });
#endif
        }

        public virtual void DestroyAd()
        {
#if USE_ADMOB
            if (!UnityMainThreadDispatcher.IsExist)
                return;

            UnityMainThreadDispatcher.Enqueue(() =>
            {
                isCanShow = false;

                state = AdState.None;

                if (type != AdType.Banner && type != AdType.Mrec && type != AdType.InGame)
                {
                    PauseApp(false, $"DestroyAd {placement} {type} {item}");

                    if (type != AdType.Reward && type != AdType.RewardInter)
                    {
                        AdsManager.LastTimeShowAd = DateTime.Now;
                        AdToast.HideLoading();
                    }
                    else if (adBreakNoticeShown && AdToast.instance != null)
                    {
                        // Rewarded closed early / native rewarded: the ad-break toast would stay on screen
                        // (Full mode blocks every touch). A granted reward already replaced it with noticeOnAdClose.
                        AdToast.HideLoading();
                    }
                    adBreakNoticeShown = false;
                }
            });
#endif
        }

        #region LOGGER
        private void ResetLogLoad()
        {
            error = null;
            revenue = 0;
            sourceName = null;
            loadTime = 0;
            loadTimeStart = DateTime.Now;
            durationTime = 0;
            durationTimeStart = DateTime.Now;
        }

        private void ResetLogShow()
        {
            revenue = 0;
            durationTime = 0;
            durationTimeStart = DateTime.Now;
        }

        public void LogState()
        {
            if (!ZenAnalytics.IsReady)
                return;
            var parameter = AdParameter();
            if (loadTime > 0)
                parameter["ad_load_time"] = loadTime.ToString("#0", culture);
            if (!string.IsNullOrEmpty(error))
                parameter["ad_error"] = error;
            if (durationTime > 0)
                parameter["ad_duration_time"] = durationTime.ToString("#0", culture);

            ZenAnalytics.LogEventExact(("ad_" + type.RegexToId() + "_" + state.RegexToId()).RegexToId(32), parameter);

            if (DebugMode.IsOn)
                Log(string.Format("AdParameter ad_status: {0} -- ad_item: {1} -- ad_id: {2} -- ad_source: {3} -- internet: {4} -- loadTime: {5} -- durationTime: {6}",
                                               state.RegexToId(), item, id, sourceName, internet, loadTime.ToString("#0", culture), durationTime.ToString("#0", culture)));
        }

        private static readonly HashSet<int> adMilestones = new HashSet<int>
        {
            5, 10, 20, 30, 40, 50, 100, 150, 200, 300, 400, 500, 750, 1000
        };
        public void LogMilestone()
        {
            if (!ZenAnalytics.IsReady)
                return;
            if (type == AdType.Inter && adMilestones.Contains(userData.adInterstitial))
                ZenAnalytics.LogEventExact($"ad_{type}_{userData.adInterstitial}_times".RegexToId(32), AdParameter());

            if ((type == AdType.Reward || type == AdType.RewardInter) && adMilestones.Contains(userData.adRewarded))
                ZenAnalytics.LogEventExact($"ad_{type}_{userData.adRewarded}_times".RegexToId(32), AdParameter());

            bool isTrackingType = type == AdType.Inter || type == AdType.Reward || type == AdType.RewardInter;
            if (isTrackingType && adMilestones.Contains(userData.adTotal))
                ZenAnalytics.LogEventExact($"ad_success_{userData.adTotal}_times".RegexToId(32), AdParameter());
        }

        protected long valueMicros = 0;
        protected double revenue = 0;
        protected string currency = "USD";
        protected CultureInfo culture = CultureInfo.CreateSpecificCulture("en-US");

        /// <summary>
        /// Maps the AdMob ad source name (AdapterResponseInfo.AdSourceName, e.g. "AdMob Network", "Meta Audience Network",
        /// "Liftoff Monetize") to a stable AppsFlyer monetization network name. AdMob keeps "googleadmob" (the previous
        /// value) so existing dashboards stay continuous; unknown sources pass through lower-cased.
        /// </summary>
        internal static string ToMonetizationNetwork(string adSourceName)
        {
            if (string.IsNullOrEmpty(adSourceName)) return "googleadmob";
            string s = adSourceName.ToLowerInvariant();
            if (s.Contains("admob") || s.Contains("google")) return "googleadmob";
            if (s.Contains("meta") || s.Contains("facebook")) return "facebook";
            if (s.Contains("liftoff") || s.Contains("vungle")) return "liftoff";
            if (s.Contains("applovin")) return "applovin";
            if (s.Contains("unity")) return "unityads";
            if (s.Contains("mintegral")) return "mintegral";
            if (s.Contains("pangle")) return "pangle";
            if (s.Contains("ironsource")) return "ironsource";
            return s.Trim();
        }

        public void LogImpressionData(object data)
        {
            try
            {
                // Impression-level revenue is time-sensitive: Google asks to send it OUTSIDE the main-thread hop
                // (developers.google.com/admob/unity/global-settings). The dispatcher runs in Update, which waits while
                // the app is in the background (e.g. the player opened the store from the ad) and is lost if the app is
                // closed meanwhile. This part uses no Unity API.
                long micros = 0;
                double rev = 0;
                string cur = "USD";
                string precision = "Unknown";
#if USE_ADMOB
                if (data is AdValue adValue)
                {
                    micros = adValue.Value;
                    rev = micros / 1000000d;
                    cur = string.IsNullOrEmpty(adValue.CurrencyCode) ? "USD" : adValue.CurrencyCode;
                    precision = adValue.Precision.ToString();
                }
#endif
                // Reset per impression: never re-send the previous impression's value.
                valueMicros = micros;
                revenue = rev;
                currency = cur;

#if USE_APPSFLYER
                try
                {
                    // AppsFlyer: monetizationNetwork = "the network that served the ad" (dev.appsflyer.com/hc/docs/ad-revenue-1),
                    // mediationNetwork = the mediation platform (AdMob for both AdMob and native paths).
                    string media = ToMonetizationNetwork(sourceName);
                    MediationNetwork network = MediationNetwork.GoogleAdMob;
                    Dictionary<string, string> additionalParams = new Dictionary<string, string>
                    {
                        { AdRevenueScheme.AD_UNIT, id },
                        { AdRevenueScheme.AD_TYPE, type.RegexToId() },
                        { AdRevenueScheme.PLACEMENT, item },
                        { "precision", precision }
                    };
                    var adRevenueData = new AFAdRevenueData(media, network, cur, rev);
                    AppsFlyer.logAdRevenue(adRevenueData, additionalParams);
                }
                catch (Exception ex) { Debug.LogException(ex); }
#endif

                // Unity-side bookkeeping (user data, Taichi PlayerPrefs / Firebase, logs) stays on the main thread.
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    if (data == null) Debug.LogWarning("LogImpressionData: data NULL");
                    Log($"AdValue: valueMicros={micros} revenue={rev.ToString("#0.000000", culture)} currency={cur} precision={precision}");

                    if (userData != null)
                        userData.lifeTimeValue += rev;
                    if (TaichiManager.Instance != null && userData != null)
                        TaichiManager.Instance.ProcessAdRevenue(rev, cur, userData.lifeTimeValue);
                    else
                        LogWarning("TaichiManager.Instance or userData is null --> revenue not processed by Taichi");

                    Log($"ad_impression {item.RegexToId()} {sourceName.RegexToId()} {rev.ToString("#0.000000", culture)} {cur} lifeTimeValue: {(userData != null ? userData.lifeTimeValue.ToString("#0.000000", culture) : "-")}");
                });
            }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        /// <summary>Firebase ad parameters (ad_platform, ad_format... = FirebaseAnalytics.Parameter* names).</summary>
        protected Dictionary<string, object> AdParameter()
        {
            var parameter = new Dictionary<string, object>
            {
                { "ad_platform", platform.RegexToId() },
                { "ad_format", type.RegexToId() },
                { "ad_unit_name", id.RegexToId() },
                { "ad_source", sourceName.RegexToId() },
                { "value", revenue },
                { "currency", currency },
                { "ad_state", state.RegexToId() },
                { "ad_placement", placementName },
                { "ad_item", item.RegexToId() },
                { "internet", internet.RegexToId() },
            };
            if (userData != null)
            {
                parameter["session"] = userData.session;
                parameter["level"] = userData.currentLevel;
                parameter["login_day"] = userData.loginDay;
            }
            return parameter;
        }
        #endregion

        protected virtual void OnDestroy()
        {
            StopAllCoroutines();
            CancelInvoke();
            DestroyAd();
            LogWarning("OnDestroy");
        }

        internal void PauseApp(bool isPause, string from) { Debug.Log($"{TAG} PauseApp Request: {isPause} triggered from: {from}"); UnityMainThreadDispatcher.Enqueue(() => { AdsManager.HandleAdPauseState(isPause); }); }
        internal void Log(string v) { if (DebugMode.IsOn) Debug.Log(TAG + v); }
        internal void LogWarning(string v) { if (DebugMode.IsOn || Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.LinuxEditor) Debug.LogWarning(TAG + v); }
        internal void LogError(string v) { if (DebugMode.IsOn || Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.LinuxEditor) Debug.LogError(TAG + v); }
    }

    public enum ShowLoadingMode
    {
        None,
        Toast,
        Full
    }

#if !USE_APPSFLYER
    public enum MediationNetwork { GoogleAdMob }
#endif
}