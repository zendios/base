using GoogleMobileAds.Api;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Base.Ads
{
    public class AdsManager : MonoBehaviour
    {
        public static string TAG = "[ADMANAGER] -> ";
        internal static AdConfig Config => AdConfig.Instance;
        internal static UserData UserData => DataManager.UserData;
        internal static bool InReview => DataManager.InReview;

        internal static DateTime LastTimeShowAd = DateTime.Now;

        /// <summary>A purchase (com.zen.iap) holds interstitials for 60 s, so none pops over the store sheet.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void HoldAdsDuringPurchases()
        {
            ZenStore.OnPurchaseStarted -= DelayAdsForPurchase;
            ZenStore.OnPurchaseStarted += DelayAdsForPurchase;
        }

        private static void DelayAdsForPurchase() => LastTimeShowAd = DateTime.Now.AddSeconds(60);

        internal static AdState lastStage = AdState.None;

        internal static Dictionary<PlacementType, AdBase> Placements = new Dictionary<PlacementType, AdBase>();
        public bool runInBackground = true;

        [Header("PREFABs")]
        [SerializeField] private List<AdBase> list = new List<AdBase>();
        public static List<AdBase> List => instance != null ? instance.list : new List<AdBase>();

        public static bool UseIdTest => Config != null && Config.useIdTest;
        public static bool IsDebug;

        private static float preAdTimeScale = 1.0f;
        private static bool isAdCurrentlyPausingApp = false;
        private static float preAdVolume = 1.0f;
        private static bool preAdAudioPause = false;
        private static bool pendingLoadAds = false;

        /// <summary>A fullscreen ad (inter / reward / app open) currently holds the game paused.</summary>
        internal static bool IsFullscreenAdShowing => isAdCurrentlyPausingApp;

        public static bool IsInitialized { get; private set; } = false;

        public static bool IsInitializing { get; private set; } = false;

        protected static bool isTimeToShowAds = true;

        /// <summary>
        /// Debug only (Test All Ads): every ad call skips the frequency clock, the minimum plays, the in-app review
        /// block, VIP / Remove Ads / InReview and the ForceReward swap, so any format shows on request.
        /// </summary>
        public static bool ForceAds { get; set; }

        public static bool IsTimeToShowAds
        {
            get
            {
                if (ForceAds)
                    return true;

                if (Time.timeScale <= 0)
                {
                    Debug.LogWarning(TAG + "Time is paused, skipping ad display.");
                    return false;
                }

                if (IsRemovedAds)
                    return false;

                if (UserData != null && Config != null)
                {
                    isTimeToShowAds = false;

                    if (UserData.totalPlay < Config.adInterOnPlay)
                    {
                        LogWarning($"Exit early if totalPlay not met {UserData.totalPlay}/{Config.adInterOnPlay}");
                        return isTimeToShowAds;
                    }

                    float totalTimePlay = (float)(DateTime.Now - LastTimeShowAd).TotalSeconds;

                    if (Mathf.FloorToInt(totalTimePlay) >= TimePlayToShowAds)
                        isTimeToShowAds = true;

                    if (isTimeToShowAds && InAppReview.Instance != null && InAppReview.Instance.isTimeToShow)
                        isTimeToShowAds = false;

                    Log($"timePlayToShowAds: {isTimeToShowAds.ToString().ToUpper()} totalTimePlay: {totalTimePlay:#0.0}/{TimePlayToShowAds:#0.0} adInterOnPlay: {UserData.totalPlay}/{Config.adInterOnPlay}");

                    return isTimeToShowAds;
                }
                return true;
            }
        }

        /// <summary>Seconds until the play-time gap allows the next interstitial / app open (0 = allowed now). No side effects.</summary>
        public static float SecondsUntilAdAllowed => Mathf.Max(0f, TimePlayToShowAds - (float)(DateTime.Now - LastTimeShowAd).TotalSeconds);

        public static float TimePlayToShowAds
        {
            get
            {
                if (Config != null && UserData != null)
                {
                    float timePlayToShowAds = Config.adTimePlayToShow + (Config.adTimePlayReduceToShow * Mathf.Min(UserData.adTotal, 5));
                    timePlayToShowAds = Mathf.Max(timePlayToShowAds, Config.adTimeBetween);
                    return timePlayToShowAds;
                }
                return 0;
            }
        }

        public static bool IsRemovedAds
        {
            get
            {
                if (ForceAds)
                    return false;
                if (UserData != null && (UserData.isVIP || UserData.isRemovedAds || InReview))
                {
                    LogWarning($"Ads removed for VIP: {UserData.isVIP} -- RemovedAds: {UserData.isRemovedAds} -- InReview: {InReview} users.");
                    return true;
                }
                return false;
            }
        }

        private static bool suppressNextAppOpen;

        /// <summary>Skips the app open ad on the next return to the app (e.g. the player was sent to the store to rate).</summary>
        public static void SuppressNextAppOpen(string reason)
        {
            suppressNextAppOpen = true;
            Log($"Next app open suppressed: {reason}");
        }

        /// <summary>True once after SuppressNextAppOpen: the app open ad of this return is skipped.</summary>
        internal static bool ConsumeSuppressNextAppOpen()
        {
            bool suppressed = suppressNextAppOpen;
            suppressNextAppOpen = false;
            return suppressed;
        }

        public static bool ForceReward
        {
            get
            {
                if (ForceAds || Config.adInterVsRewardRatio <= 0)
                    return false;
                int expectedRewards = UserData.adInterstitial / Config.adInterVsRewardRatio;
                Log($"Checking if should force reward: Interstitials={UserData.adInterstitial}, Rewarded={UserData.adRewarded}, ExpectedRewards={expectedRewards}, Ratio={Config.adInterVsRewardRatio}");
                bool forceReward = expectedRewards > UserData.adRewarded + UserData.adRewardSkipped;
                return forceReward;
            }
        }

        public static bool InstanceExists
        {
            get
            {
                if (instance == null || IsInitialized == false)
                    Debug.LogError(TAG + "AdsManager instance is null. Ensure that an AdsManager is present in the scene.");
                return instance != null;
            }
        }

        public static int SoftCurrencyByReward
        {
            get
            {
                int reward = 500;
                if (Config != null && UserData != null)
                {
                    reward = Config.rewardByAd;
                    long adTotal = UserData.adTotal;
                    reward = Mathf.Clamp(Mathf.FloorToInt(reward * (1 + (adTotal * Config.rewardLevelScale))), reward, reward * 5);
                }
                return reward;
            }
        }

        protected static AdsManager instance;
        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            Application.runInBackground = runInBackground;
            try
            {
                LastTimeShowAd = DateTime.Now;
                Placements.Clear();
                foreach (AdBase ad in list)
                    Placements.Add(ad.placement, ad);

                if (DebugMode.IsOn)
                    IsDebug = true;
                DebugMode.OnChanged += (isOn) => IsDebug = isOn;
            }
            catch (Exception ex)
            {
                throw new Exception(TAG + "Error in Start: " + ex.Message);
            }
        }

        private void OnDestroy()
        {
            DebugMode.OnChanged -= (isOn) => IsDebug = isOn;
        }

        private void OnValidate()
        {
            list.Clear();
            list = GetComponentsInChildren<AdBase>().ToList();
        }

        public static IEnumerator IEInit(bool initAds = false)
        {
            if (IsInitializing)
                yield break;

            IsInitializing = true;

            if (!IsInitialized)
            {
                ZenAnalytics.LogEvent("ad_manganger_init");
                yield return ADMOB.IEInit((onDone) =>
                {
                    // Set first: an exception in the native init must never keep the whole ads stack uninitialized.
                    IsInitialized = onDone;
                    ZenAnalytics.LogEvent("ad_manganger_init_" + onDone);

                    // The SDK can finish after the splash already called LoadAds (mediation adapters, up to ~30 s):
                    // run the session preload now instead of skipping it for the whole session.
                    if (onDone && pendingLoadAds)
                        LoadAds();
                });
            }

            IsInitializing = false;
        }

        public static void LoadAds()
        {
            pendingLoadAds = !IsInitialized;   // not ready yet: the init callback will call LoadAds again
            if (IsInitialized)
            {
                if (!IsRemovedAds && (Config.adInterFlow == AdFlow.OnlyDefault || Config.adInterFlow == AdFlow.Both))
                    UnityMainThreadDispatcher.Enqueue(IELoad(PlacementType.AdmobInterDefault, null, "init", ShowLoadingMode.None));
                if (Config.adRewardFlow == AdFlow.OnlyDefault || Config.adRewardFlow == AdFlow.Both)
                    UnityMainThreadDispatcher.Enqueue(IELoad(PlacementType.AdmobRewardDefault, null, "init", ShowLoadingMode.None));
            }
        }

        public static void HandleAdPauseState(bool pause)
        {
            if (pause)
            {
                if (!isAdCurrentlyPausingApp)
                {
                    preAdTimeScale = Time.timeScale > 0 ? Time.timeScale : 1;
                    preAdVolume = AudioListener.volume;          // keep the player's own volume / mute
                    preAdAudioPause = AudioListener.pause;
                    isAdCurrentlyPausingApp = true;
                    Time.timeScale = 0.0f;
                    AudioListener.volume = 0.0f;
                    AudioListener.pause = true;
                }
            }
            else
            {
                if (isAdCurrentlyPausingApp)
                {
                    isAdCurrentlyPausingApp = false;
                    Time.timeScale = preAdTimeScale;
                    AudioListener.volume = preAdVolume;
                    AudioListener.pause = preAdAudioPause;
                }
            }
        }

        public static bool CheckAvailable(PlacementType placementType, string item, bool loadIfNotAvaiable = false)
        {
            bool avaiavle = Placements.TryGetValue(placementType, out AdBase ad) && ad != null && ad.isCanShow;
            if (loadIfNotAvaiable && !avaiavle)
                UnityMainThreadDispatcher.Enqueue(IELoad(placementType, null, item, ShowLoadingMode.None));
            return avaiavle;
        }

        #region INTER
        public static IEnumerator IELoadInter(Action<AdType, AdState> callback, string itemName, ShowLoadingMode showLoadingMode)
        {
            if (IsRemovedAds)
            {
                callback?.Invoke(AdType.Inter, AdState.NotTime);
                yield break;
            }

            AdState state = AdState.None;
            if (ForceReward)
            {
                Log($"Forcing reward ad instead of interstitial. Trigger: {itemName}");
                yield return IELoad(PlacementType.AdmobRewardDefault, (t, s) =>
                {
                    if (s == AdState.LoadAvailable)
                        state = s;
                    if (state == AdState.LoadNotAvailable || state == AdState.LoadTimeOut)
                        UserData.adRewardSkipped++;
                }, itemName, showLoadingMode);

                if (state == AdState.LoadAvailable)
                {
                    callback?.Invoke(AdType.Inter, state);
                    yield break;
                }
            }

            if (Config.adInterFlow == AdFlow.OnlyDefault || Config.adInterFlow == AdFlow.Both)
            {
                if (state == AdState.None && Placements.TryGetValue(PlacementType.AdmobInterDefault, out AdBase admobInter) && admobInter != null)
                {
                    Log($"Attempting to load interstitial from AdmobInterDefault for: {itemName}");
                    yield return admobInter.IELoadAd((t, s) =>
                    {
                        if (s == AdState.LoadAvailable)
                            state = s;
                    }, itemName, Config.adLoadTimeout, showLoadingMode);

                    if (state == AdState.LoadAvailable)
                    {
                        callback?.Invoke(AdType.Inter, state);
                        yield break;
                    }
                }
            }

            if (Config.adInterFlow == AdFlow.OnlyNative || Config.adInterFlow == AdFlow.Both)
            {
                if (state == AdState.None && Placements.TryGetValue(PlacementType.AdZativeInterDefault, out AdBase zativeInter) && zativeInter != null)
                {
                    Log($"Attempting to load interstitial from ZativeInterDefault for: {itemName}");
                    yield return zativeInter.IELoadAd((t, s) =>
                    {
                        state = s;
                    }, itemName, Config.adLoadTimeout, showLoadingMode);
                }
            }

            callback?.Invoke(AdType.Inter, state);
        }

        public static void ShowInter(Action<AdType, AdState> callback, string itemName, float delayShow = 0f, bool forceShow = false)
        {
            if (!InstanceExists)
            {
                callback?.Invoke(AdType.Inter, AdState.Exception);
                return;
            }

            instance.StartCoroutine(IEShowInter(callback, itemName, delayShow, forceShow));
        }

        public static IEnumerator IEShowInter(Action<AdType, AdState> callback, string itemName, float delayShow = 0f, bool forceShow = false)
        {
            if (IsRemovedAds)
            {
                callback?.Invoke(AdType.Inter, AdState.NotTime);
                yield break;
            }

            if (forceShow)
                LastTimeShowAd = LastTimeShowAd.AddHours(-1);

            if (!IsTimeToShowAds)
            {
                callback?.Invoke(AdType.Inter, AdState.NotTime);
                Log($"Not time to show ads yet. Skipping interstitial. Trigger: {itemName} forceShow: {forceShow}");
                yield break;
            }

            if (ForceReward)
            {
                Log($"Forcing reward ad instead of interstitial. Trigger: {itemName}");
                var checkReward = Placements.FirstOrDefault(p => p.Value != null && (p.Value.type == AdType.Reward || p.Value.type == AdType.RewardInter) && p.Value.isCanShow);
                if (checkReward.Value != null)
                {
                    yield return IELoadShow(checkReward.Value.placement, callback, itemName);
                    yield break;
                }
            }

            Log($"Attempting check all inters to show interstitial for: {itemName}");
            // Respect AdFlow: an AdMob interstitial only when adInterFlow allows AdMob, a native one only when it allows native
            // (a stale ready ad from another flow - e.g. after Remote Config changed the flow - must not bypass it).
            var checkInter = Placements.FirstOrDefault(p => p.Value != null && (p.Value.type == AdType.Inter) && IsAllowedByInterFlow(p.Value) && p.Value.isCanShow);
            if (checkInter.Value != null)
            {
                yield return checkInter.Value.IEShowAd(callback, itemName, delayShow); // Instant pop if interstitial is ready
                yield break;
            }

            if (Config.adInterFlow == AdFlow.OnlyDefault
                || Config.adInterFlow == AdFlow.Both)
            {
                bool avaiable = CheckAvailable(PlacementType.AdmobInterDefault, itemName);
                if (avaiable)
                {
                    yield return IEShow(PlacementType.AdmobInterDefault, callback, itemName, delayShow);
                    yield break;
                }

                if (Config.adInterFlow == AdFlow.OnlyDefault)
                {
                    yield return IELoadShow(PlacementType.AdmobInterDefault, callback, itemName);
                    yield break;
                }

                if (Config.adInterFlow == AdFlow.Both)
                    yield return IELoadShow(PlacementType.AdZativeInterDefault, callback, itemName);
            }
            else if (Config.adInterFlow == AdFlow.OnlyNative)
            {
                yield return IELoadShow(PlacementType.AdZativeInterDefault, callback, itemName);
            }
        }

        private static bool IsAllowedByInterFlow(AdBase ad)
        {
            if (Config == null)
                return true;
            bool isNative = ad is AdZative;
            switch (Config.adInterFlow)
            {
                case AdFlow.OnlyNative: return isNative;
                case AdFlow.OnlyDefault: return !isNative;
                default: return true;
            }
        }

        public static IEnumerator IELoadShowInter(PlacementType placementType, Action<AdType, AdState> callback, string itemName, ShowLoadingMode showLoadingMode = ShowLoadingMode.Toast)
        {
            if (IsRemovedAds)
            {
                callback?.Invoke(AdType.Inter, AdState.NotTime);
                yield break;
            }

            yield return IELoadShow(placementType, callback, itemName, showLoadingMode);
        }
        #endregion

        #region REWARD
        public static void ShowReward(Action<AdType, AdState> callback, string itemName, ShowLoadingMode showLoadingMode = ShowLoadingMode.Toast, float delayShow = 0f)
        {
            if (!InstanceExists)
            {
                callback?.Invoke(AdType.Native, AdState.Exception);
                return;
            }

            instance.StartCoroutine(IEShowReward(callback, itemName, showLoadingMode, delayShow));
        }

        /// <summary>
        /// Rewarded by adRewardFlow (Remote Config): OnlyDefault = AdMob, OnlyNative = native, Both = AdMob first and the
        /// native rewarded when AdMob has no ad. A ready ad is shown at once; the caller gets one terminal result.
        /// </summary>
        public static IEnumerator IEShowReward(Action<AdType, AdState> callback, string itemName, ShowLoadingMode showLoadingMode = ShowLoadingMode.Toast, float delayShow = 0f)
        {
            Placements.TryGetValue(PlacementType.AdmobRewardDefault, out AdBase admob);
            Placements.TryGetValue(PlacementType.AdZativeRewardDefault, out AdBase native);
            bool useAdmob = admob != null && Config.adRewardFlow != AdFlow.OnlyNative;
            bool useNative = native != null && Config.adRewardFlow != AdFlow.OnlyDefault;

            if (!useAdmob && !useNative)
            {
                callback?.Invoke(AdType.Reward, AdState.Exception);
                LogError($"No rewarded placement for adRewardFlow {Config.adRewardFlow}");
                yield break;
            }

            // Ready ad: show at once (AdMob first).
            if (useAdmob && admob.isCanShow)
            {
                yield return IELoadShow(PlacementType.AdmobRewardDefault, callback, itemName, showLoadingMode, delayShow);
                yield break;
            }
            if (useNative && native.isCanShow)
            {
                yield return IELoadShow(PlacementType.AdZativeRewardDefault, callback, itemName, showLoadingMode, delayShow);
                yield break;
            }

            if (useAdmob && useNative)
            {
                // Both: one loading toast for the whole attempt; AdMob's own fail toast stays silent (mode None),
                // the native step shows the final result.
                if (showLoadingMode != ShowLoadingMode.None)
                    AdToast.ShowLoading(admob.noticeOnAdBrake, Config.adLoadTimeout * 2.5f, showLoadingMode == ShowLoadingMode.Full);
                yield return admob.IELoadAd(null, itemName, Config.adLoadTimeout, ShowLoadingMode.None);
                if (admob.isCanShow)
                {
                    yield return IELoadShow(PlacementType.AdmobRewardDefault, callback, itemName, showLoadingMode, delayShow);
                    yield break;
                }
                Log($"Rewarded AdMob has no ad ({admob.state}) --> native. Trigger: {itemName}");
                yield return IELoadShow(PlacementType.AdZativeRewardDefault, callback, itemName, showLoadingMode, delayShow);
                yield break;
            }

            yield return IELoadShow(useAdmob ? PlacementType.AdmobRewardDefault : PlacementType.AdZativeRewardDefault, callback, itemName, showLoadingMode, delayShow);
        }
        #endregion

        #region BASE
        public static void Load(PlacementType placementType, Action<AdType, AdState> callback, string itemName, ShowLoadingMode showLoadingMode)
        {
            if (!InstanceExists)
            {
                callback?.Invoke(AdType.Native, AdState.Exception);
                return;
            }

            instance.StartCoroutine(IELoad(placementType, callback, itemName, showLoadingMode));
        }

        public static IEnumerator IELoad(PlacementType placementType, Action<AdType, AdState> callback, string itemName, ShowLoadingMode showLoadingMode)
        {
            if (Placements.TryGetValue(placementType, out AdBase ad) && ad != null)
            {
                yield return ad.IELoadAd(callback, itemName, Config.adLoadTimeout, showLoadingMode);
                yield break;
            }
            else
            {
                callback?.Invoke(AdType.Native, AdState.Exception);
                LogError($"Placement not found for: {placementType}");
            }
        }

        public static void Show(PlacementType placementType, Action<AdType, AdState> callback, string itemName, float delayShow)
        {
            if (!InstanceExists)
            {
                callback?.Invoke(AdType.Native, AdState.Exception);
                return;
            }

            instance.StartCoroutine(IEShow(placementType, callback, itemName, delayShow));
        }

        public static IEnumerator IEShow(PlacementType placementType, Action<AdType, AdState> callback, string itemName, float delayShow)
        {
            if (Placements.TryGetValue(placementType, out AdBase ad) && ad != null)
            {
                yield return ad.IEShowAd(callback, itemName, delayShow);
            }
            else
            {
                callback?.Invoke(AdType.Native, AdState.Exception);
                LogError($"Placement not found for: {placementType}");
            }
        }

        public static void LoadShow(PlacementType placementType, Action<AdType, AdState> callback, string itemName, ShowLoadingMode showLoadingMode = ShowLoadingMode.Toast, float delayShow = 0f)
        {
            if (!InstanceExists)
            {
                callback?.Invoke(AdType.Native, AdState.Exception);
                return;
            }

            instance.StartCoroutine(IELoadShow(placementType, callback, itemName, showLoadingMode, delayShow));
        }

        public static IEnumerator IELoadShow(PlacementType placementType, Action<AdType, AdState> callback, string itemName, ShowLoadingMode showLoadingMode = ShowLoadingMode.Toast, float delayShow = 0f)
        {
            if (Placements.TryGetValue(placementType, out AdBase ad) && ad != null)
            {
                if (ad.isCanShow)
                    yield return ad.IEShowAd(callback, itemName, delayShow);
                else
                    yield return ad.IELoadShowAd(callback, itemName, Config.adLoadTimeout, showLoadingMode);
            }
            else
            {
                callback?.Invoke(AdType.Native, AdState.Exception);
                LogError($"Placement not found for: {placementType}");
            }
        }
        #endregion

        /// <summary>Opens the SDK's Ad Inspector.</summary>
        public static void OpenAdInspector()
        {
            if (!IsInitialized)
            {
                Debug.LogError(TAG + "Cannot open Ad Inspector: SDK is not initialized yet.");
                return;
            }

            MobileAds.OpenAdInspector(error =>
            {
                if (error != null)
                    Debug.LogError(TAG + "Ad Inspector closed with error: " + error.GetMessage());
            });
        }

        #region LOG
        private static void Log(string value) { if (IsDebug) Debug.Log(TAG + value); }
        private static void LogWarning(string value) { if (IsDebug) Debug.LogWarning(TAG + value); }
        private static void LogError(string value) { if (IsDebug) Debug.LogError(TAG + value); }
        #endregion
    }
}