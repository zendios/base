#if USE_FIREBASE
using Firebase;
using Firebase.Analytics;
using Firebase.Extensions;
using Firebase.RemoteConfig;
using Firebase.Crashlytics;
#if USE_FIREBASE_MESSAGE
using Firebase.Messaging;
#endif
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Base
{
    public class FirebaseManager : MonoBehaviour
    {
        public static WaitForSeconds waitTime = null;
        public static string FirebaseToken = "";
        public static Dictionary<string, object> DefaultRemoteConfig = new Dictionary<string, object>();
        public static string TAG = "Firebase ";
        protected static UserData userData => DataManager.UserData;

        internal static int session = 0;
        protected static long firebaseSessionId = 0;
        public static long FirebaseSessionId
        {
            get
            {
                return firebaseSessionId;
            }
            set
            {
                string lastValue = PlayerPrefs.GetString("FirebaseSessionId", "");
                if (lastValue != value.ToString())
                {
                    firebaseSessionId = value;
                    PlayerPrefs.SetString("FirebaseSessionId", value.ToString());
                    session = PlayerPrefs.GetInt("FirebaseSessionIdChangeCount", 0);
                    PlayerPrefs.SetInt("FirebaseSessionIdChangeCount", session + 1);
                }
            }
        }


        [SerializeField]
        private FirebaseStatus status = FirebaseStatus.UnAvailable;
        public static FirebaseStatus Status
        {
            get
            {
                if (instance)
                    return instance.status;
                return FirebaseStatus.Faulted;
            }
            private set
            {
                if (instance)
                    instance.status = value;
            }
        }

#if USE_FIREBASE && USE_FIREBASE_MESSAGE
        public static FirebaseMessage FirebaseMessage { get; set; }
#endif

        public static FirebaseStatus AnalyticStatus = FirebaseStatus.Initialing;
        public static FirebaseStatus RemoteStatus = FirebaseStatus.Initialing;
        public static FirebaseStatus MessageStatus = FirebaseStatus.Initialing;

        protected static FirebaseManager instance { get; set; }

        private void Awake()
        {
            try
            {
                if (instance != null)
                    Destroy(gameObject);
                if (instance == null)
                    instance = this;
                DontDestroyOnLoad(gameObject);
            }
            catch (Exception ex)
            {
                Debug.LogError(TAG + "Exception: " + ex.Message);
            }
        }

        #region Base
        public static IEnumerator DoCheckStatus(Dictionary<string, object> remoteDefaultConfig = null, float timeOut = 2.5f)
        {
            if (instance == null)
            {
                Debug.LogError(TAG + "NULL");
                yield break;
            }
#if !USE_FIREBASE
            Debug.LogWarning(TAG + "Set Symbol USE_FIREBASE in Player Settings");
            yield break;
#else
            if (Status == FirebaseStatus.Available
                && AnalyticStatus == FirebaseStatus.Initialized
                && RemoteStatus == FirebaseStatus.Initialized
                && MessageStatus == FirebaseStatus.Initialized)
            {
                yield break;
            }

            var elapsedTime = 0f;
            Status = FirebaseStatus.Checking;

            Debug.Log(TAG + "CheckDependencies: Checking");
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                if (task != null)
                {
                    if (task.Result == DependencyStatus.Available)
                    {
                        Status = FirebaseStatus.Available;
                        Debug.Log(TAG + "CheckDependencies: " + task.Result);
                    }
                    else
                    {
                        Debug.LogError(TAG + "CheckDependencies: " + task.Result);
                    }
                }
                else
                {
                    Debug.LogError(TAG + "CheckDependencies: task NULL");
                }
            });

            while (Status == FirebaseStatus.Checking && elapsedTime < timeOut)
            {
                elapsedTime += Time.deltaTime;
                yield return waitTime;
            }

            if (Status == FirebaseStatus.Available)
            {
                AnalyticStatus = FirebaseStatus.Initialing;
                Debug.Log(TAG + "Analytics " + "Initialing");

                try
                {
                    FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);

                    //var consentStatus = ConsentInformation.CanRequestAds() ? ConsentStatus.Granted : ConsentStatus.Denied;
                    //IDictionary<ConsentType, ConsentStatus> consentSettings = null;
                    //consentSettings.Add(ConsentType.AdStorage, consentStatus);
                    //consentSettings.Add(ConsentType.AdUserData, consentStatus);
                    //consentSettings.Add(ConsentType.AnalyticsStorage, consentStatus);
                    //consentSettings.Add(ConsentType.AdPersonalization, consentStatus);
                    //FirebaseAnalytics.SetConsent(consentSettings);

                    AnalyticStatus = FirebaseStatus.Initialized;
                    Debug.Log(TAG + "Analytics " + "Initialized");
                }
                catch (FirebaseException ex)
                {
                    Debug.LogError(TAG + "Analytics " + "Initialized " + ex.Message + "\n" + ex.StackTrace);
                    AnalyticStatus = FirebaseStatus.UnkownError;
                }
                catch (Exception ex)
                {
                    Debug.LogError(TAG + "Analytics " + "Initialized " + ex.Message + "\n" + ex.StackTrace);
                    AnalyticStatus = FirebaseStatus.UnkownError;
                }

                FirebaseAnalytics.GetSessionIdAsync().ContinueWithOnMainThread(task =>
                {
                    if (task.IsCompleted && !task.IsCanceled && !task.IsFaulted)
                    {
                        FirebaseSessionId = task.Result;
                        Debug.Log($"Firebase Analytics Session ID obtained: {FirebaseSessionId}");
                    }
                    else
                    {
                        Debug.LogWarning("Failed to obtain Firebase Analytics Session ID.");
                    }
                });

                if (remoteDefaultConfig != null && remoteDefaultConfig.Count > 0)
                {
                    var remoteConfig = FirebaseRemoteConfig.DefaultInstance;
                    if (remoteConfig != null)
                    {
                        DefaultRemoteConfig = remoteDefaultConfig;
                        elapsedTime = 0f;
                        RemoteStatus = FirebaseStatus.Initialing;
                        Debug.Log(TAG + "RemoteConfig " + "Initialing");

                        try
                        {
                            remoteConfig.SetDefaultsAsync(DefaultRemoteConfig).ContinueWithOnMainThread(task =>
                            {
                                RemoteStatus = LogTaskCompletion(task, TAG + "RemoteConfig SetDefaultsAsync");
                            });
                        }
                        catch (FirebaseException ex)
                        {
                            RemoteStatus = FirebaseStatus.Faulted;
                            Debug.LogError(TAG + "RemoteConfig " + "Initialized " + ex.Message + "\n" + ex.StackTrace);
                        }
                        catch (Exception ex)
                        {
                            RemoteStatus = FirebaseStatus.Faulted;
                            Debug.LogError(TAG + "RemoteConfig " + "Initialized " + ex.Message + "\n" + ex.StackTrace);
                        }

                        elapsedTime = 0f;
                        while (RemoteStatus == FirebaseStatus.Initialing && elapsedTime < timeOut)
                        {
                            elapsedTime += Time.deltaTime;
                            yield return waitTime;
                        }
                    }
                    else
                    {
                        Debug.LogError(TAG + "RemoteConfig " + " NULL");
                    }
                }

#if USE_FIREBASE_MESSAGE
                try
                {
                    MessageStatus = FirebaseStatus.Initialing;
                    Debug.Log(TAG + "Messaging " + "Initialing");

                    FirebaseMessaging.TokenReceived += OnTokenReceived;
                    FirebaseMessaging.MessageReceived += OnMessageReceived;

                    ///FirebaseMessaging.SubscribeAsync(topic).ContinueWith(task =>
                    ///{
                    ///    LogTaskCompletion(task, TAG + "SubscribeAsync");
                    ///});

                    /// This will display the prompt to request permission to receive
                    /// notifications if the prompt has not already been displayed before. (If
                    /// the user already responded to the prompt, thier decision is cached by
                    /// the OS and can be changed in the OS settings).
                    FirebaseMessaging.RequestPermissionAsync().ContinueWith(task =>
                    {
                        MessageStatus = LogTaskCompletion(task, TAG + "Messaging RequestPermissionAsync");
                    });
                    Debug.Log(TAG + "Messaging " + "Initialized");
                }
                catch (FirebaseException ex)
                {
                    MessageStatus = FirebaseStatus.Faulted;
                    Debug.LogError(TAG + "Messaging " + "Initialized " + ex.Message + "\n" + ex.StackTrace);
                }
                catch (Exception ex)
                {
                    MessageStatus = FirebaseStatus.Faulted;
                    Debug.LogError(TAG + "Messaging " + "Initialized " + ex.Message + "\n" + ex.StackTrace);
                }

                elapsedTime = 0f;
                while (MessageStatus == FirebaseStatus.Initialing && elapsedTime < timeOut)
                {
                    elapsedTime += Time.deltaTime;
                    yield return waitTime;
                }
#endif
            }
#endif
        }

        protected static FirebaseStatus LogTaskCompletion(Task task, string operation)
        {
            FirebaseStatus status = FirebaseStatus.Initialing;
#if USE_FIREBASE
            if (task.IsCanceled)
            {
                Debug.Log(operation + " Canceled");
                status = FirebaseStatus.Canceled;
            }
            else if (task.IsFaulted)
            {
                Debug.Log(operation + " Encounted an error");
                foreach (Exception exception in task.Exception.Flatten().InnerExceptions)
                {
                    string errorCode = "";
                    FirebaseException firebaseEx = exception as FirebaseException;
                    if (firebaseEx != null)
                        errorCode = string.Format("Error.{0}: ", firebaseEx.ErrorCode.ToString());
                    Debug.LogError(errorCode + exception.ToString());
                }
                status = FirebaseStatus.Faulted;
            }
            else if (task.IsCompleted)
            {
                Debug.Log(operation + " Completed");
                status = FirebaseStatus.Completed;
            }
#else
            status = FirebaseStatus.Faulted;
#endif
            return status;
        }
        #endregion

        #region FirebaseRemoteConfigs
        public static IEnumerator DoFetchRemoteData(Action<FirebaseStatus> status, int cacheExpirationHours = 12, float timeOut = 6.8f)
        {
#if !USE_FIREBASE
            Debug.LogWarning(TAG + "Set Symbol USE_FIREBASE in Player Settings");
            status?.Invoke(FirebaseStatus.Faulted);
            yield break;
#else
            Debug.Log(TAG + "DoFetchRemoteData");
            if (instance == null || RemoteStatus != FirebaseStatus.Completed)
            {
                Debug.LogError(TAG + "NULL");
                status?.Invoke(FirebaseStatus.Faulted);
                yield break;
            }

            LogEvent("DoFetchRemoteData_" + FirebaseStatus.Checking.ToString());

            var elapsedTime = 0f;

            while (elapsedTime < timeOut && RemoteStatus != FirebaseStatus.Completed)
            {
                elapsedTime += Time.deltaTime;
                yield return waitTime;
            }

            if (!IsConnected)
            {
                Debug.LogError(TAG + "DoFetchRemoteData NoInternet");
                RemoteStatus = FirebaseStatus.NoInternet;
                status?.Invoke(FirebaseStatus.NoInternet);
                LogEvent("DoFetchRemoteData_" + RemoteStatus.ToString());
                yield break;
            }

            elapsedTime = 0f;
            RemoteStatus = FirebaseStatus.Fetching;
            LogEvent("DoFetchRemoteData_" + RemoteStatus.ToString());

            FetchAsync((s) =>
            {
                if (RemoteStatus == FirebaseStatus.Fetching)
                {
                    RemoteStatus = s;
                    status?.Invoke(RemoteStatus);
                }
                LogEvent("DoFetchRemoteData_" + RemoteStatus.ToString());
            }, cacheExpirationHours);

            while (elapsedTime < timeOut && RemoteStatus == FirebaseStatus.Fetching)
            {
                elapsedTime += Time.deltaTime;
                yield return waitTime;
            }

            if (RemoteStatus == FirebaseStatus.Fetching)
            {
                Debug.LogError(TAG + "DoFetchRemoteData TimeOut " + elapsedTime.ToString("0.0"));
                RemoteStatus = FirebaseStatus.TimeOut;
                status?.Invoke(RemoteStatus);
                LogEvent("DoFetchRemoteData_" + RemoteStatus.ToString());
            }
#endif
        }

        public static void FetchAsync(Action<FirebaseStatus> status, int cacheExpirationHours = 6)
        {
#if USE_FIREBASE
            if (DebugMode.IsOn)
                cacheExpirationHours = 0;

            FirebaseRemoteConfig.DefaultInstance.FetchAsync(TimeSpan.FromHours(cacheExpirationHours)).ContinueWithOnMainThread((fetchTask) =>
            {
                try
                {
                    if (!fetchTask.IsCompleted)
                    {
                        if (fetchTask?.Exception?.Flatten()?.InnerExceptions != null)
                        {
                            foreach (Exception exception in fetchTask.Exception.Flatten().InnerExceptions)
                            {
                                string errorCode = "";
                                FirebaseException firebaseEx = exception as FirebaseException;
                                if (firebaseEx != null)
                                {
                                    errorCode = string.Format("Error.{0}: ", firebaseEx.ErrorCode.ToString());
                                }
                                Debug.LogError(errorCode + exception.ToString());
                            }
                        }

                        Debug.LogError(TAG + "DoFetchRemoteData Fetch " + fetchTask?.Status + " " + fetchTask?.Exception?.Message);
                        status?.Invoke(FirebaseStatus.Faulted);
                        status = null;
                    }
                    else
                    {
                        Debug.Log(TAG + "DoFetchRemoteData Fetch completed successfully!");
                        var info = FirebaseRemoteConfig.DefaultInstance.Info;
                        switch (info.LastFetchStatus)
                        {
                            case LastFetchStatus.Success:
                                if (RemoteStatus == FirebaseStatus.Fetching)
                                {
                                    FirebaseRemoteConfig.DefaultInstance.ActivateAsync().ContinueWithOnMainThread(task =>
                                    {
                                        Debug.Log(string.Format(TAG + "DoFetchRemoteData Remote data loaded and ready (last fetch time {0}).", info.FetchTime));

                                        string remoteData = TAG + "DoFetchRemoteData";
                                        foreach (var i in FirebaseRemoteConfig.DefaultInstance.Keys)
                                        {
                                            string key = i;
                                            remoteData += "\n" + key + ": " + FirebaseRemoteConfig.DefaultInstance.GetValue(key).StringValue;
                                        }

                                        Debug.Log(remoteData);
                                        status?.Invoke(FirebaseStatus.Success);
                                        status = null;
                                    });
                                }
                                break;
                            case LastFetchStatus.Failure:
                                switch (info.LastFetchFailureReason)
                                {
                                    case FetchFailureReason.Error:
                                        Debug.LogError(TAG + "DoFetchRemoteData LastFetchStatus.Failure:  Error -> Unknown reason");
                                        status?.Invoke(FirebaseStatus.UnkownError);
                                        break;
                                    case FetchFailureReason.Throttled:
                                        Debug.LogError(TAG + "DoFetchRemoteData LastFetchStatus.Failure: Throttled -> until " + info.ThrottledEndTime);
                                        status?.Invoke(FirebaseStatus.TimeOut);
                                        break;
                                    default:
                                        Debug.LogError(TAG + "DoFetchRemoteData LastFetchStatus.Failure: " + info.LastFetchFailureReason.ToString());
                                        status?.Invoke(FirebaseStatus.UnAvailable);
                                        break;
                                }
                                break;
                            case LastFetchStatus.Pending:
                                Debug.LogError(TAG + "DoFetchRemoteData Latest Fetch call still pending.");
                                status?.Invoke(FirebaseStatus.Pending);
                                break;
                            default:
                                Debug.LogError(TAG + "DoFetchRemoteData Unkown " + info.LastFetchStatus.ToString());
                                status?.Invoke(FirebaseStatus.UnAvailable);
                                break;
                        }
                    }
                }
                catch (FirebaseException ex)
                {
                    Debug.LogError(TAG + "DoFetchRemoteData FirebaseException: " + ex.Message);
                    status?.Invoke(FirebaseStatus.UnkownError);
                    status = null;
                }
                catch (Exception ex)
                {
                    Debug.LogError(TAG + "DoFetchRemoteData Exception: " + ex.Message);
                    status?.Invoke(FirebaseStatus.UnkownError);
                    status = null;
                }
            });
#endif
        }

        public static string RemoteGetValueString(string title, string defaultValue)
        {
            try
            {
#if USE_FIREBASE
                if (FirebaseRemoteConfig.DefaultInstance.Keys != null && FirebaseRemoteConfig.DefaultInstance.Keys.Contains(title))
                {
                    var value = FirebaseRemoteConfig.DefaultInstance.GetValue(title).StringValue;
                    //Debug.Log("-------> " + TAG + "Remote: " + title + " | " + value);
                    return value;
                }
                else
                {
                    Debug.LogWarning("-------> " + TAG + "Remote: " + title + " NOT FOUND");
                }
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError(title + " Exception: " + ex.Message);
                Debug.LogException(ex);
            }
            return defaultValue;
        }

        public static int RemoteGetValueInt(string title, int defaultValue)
        {
            try
            {
#if USE_FIREBASE
                if (FirebaseRemoteConfig.DefaultInstance.Keys != null && FirebaseRemoteConfig.DefaultInstance.Keys.Contains(title))
                {
                    var value = FirebaseRemoteConfig.DefaultInstance.GetValue(title).LongValue;
                    //Debug.Log("-------> " + TAG + "Remote: " + title + " | " + value);
                    return (int)value;
                }
                else
                {
                    Debug.LogWarning("-------> " + TAG + "Remote: " + title + " NOT FOUND");
                }
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError(title + " Exception: " + ex.Message);
                Debug.LogException(ex);
            }
            return defaultValue;
        }

        public static bool RemoteGetValueBoolean(string title, bool defaultValue)
        {
            try
            {
#if USE_FIREBASE
                if (FirebaseRemoteConfig.DefaultInstance.Keys != null && FirebaseRemoteConfig.DefaultInstance.Keys.Contains(title))
                {
                    var value = FirebaseRemoteConfig.DefaultInstance.GetValue(title).BooleanValue;
                    //Debug.Log("-------> " + TAG + "Remote: " + title + " | " + value);
                    return value;
                }
                else
                {
                    Debug.LogWarning("-------> " + TAG + "Remote: " + title + " NOT FOUND");
                }
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError("-------> " + TAG + "Remote: " + title + " Exception: " + ex.Message);
                Debug.LogException(ex);
            }
            return defaultValue;
        }

        public static float RemoteGetValueFloat(string title, float defaultValue)
        {
            try
            {
#if USE_FIREBASE

                if (FirebaseRemoteConfig.DefaultInstance.Keys != null && FirebaseRemoteConfig.DefaultInstance.Keys.Contains(title))
                {
                    var style = NumberStyles.Float;
                    var culture = CultureInfo.CreateSpecificCulture("en-US");
                    float value = defaultValue;
                    string stringValue = FirebaseRemoteConfig.DefaultInstance.GetValue(title).StringValue;

                    if (float.TryParse(stringValue, style, culture, out value))
                    {
                        //Debug.Log("-------> " + TAG + "Remote: " + title + "   " + FirebaseRemoteConfig.DefaultInstance.GetValue(title).StringValue + "  |  " + value + " DoubleValue: " + FirebaseRemoteConfig.DefaultInstance.GetValue(title).DoubleValue);
                        return value;
                    }
                    else
                    {
                        return (float)FirebaseRemoteConfig.DefaultInstance.GetValue(title).DoubleValue;
                    }
                }
                else
                {
                    Debug.LogWarning("-------> " + TAG + "Remote: " + title + " NOT FOUND");
                }
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError("-------> " + TAG + "Remote: " + title + " Exception: " + ex.Message);
                Debug.LogException(ex);
            }
            return defaultValue;
        }

        public static byte[] RemoteGetValueArray(string title)
        {
            try
            {
#if USE_FIREBASE
                if (FirebaseRemoteConfig.DefaultInstance.Keys != null && FirebaseRemoteConfig.DefaultInstance.Keys.Contains(title))
                {
                    var value = FirebaseRemoteConfig.DefaultInstance.GetValue(title).ByteArrayValue;
                    //Debug.Log("-------> " + TAG + "Remote: " + title + " | " + value);
                    return (byte[])value;
                }
                else
                {
                    Debug.LogWarning("-------> " + TAG + "Remote: " + title + " NOT FOUND");
                }
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError(title + " Exception: " + ex.Message);
                Debug.LogException(ex);
            }
            return null;
        }
        #endregion

        #region FirebaseAnalytic
        public static void SetUser(string title, object property)
        {
#if USE_FIREBASE
            try
            {
                if (instance == null || AnalyticStatus != FirebaseStatus.Initialized || property == null)
                {
                    if (instance == null)
                    {
                        Debug.LogWarning(TAG + "NULL");
                    }
                    else
                    {
                        Debug.LogWarning(TAG + "AnalyticStatus: " + AnalyticStatus);
                    }
                    return;
                }

                FirebaseAnalytics.SetUserProperty(title, property.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogError(TAG + "SetUser: " + ex.Message);
            }
#endif
        }

        public static void LogIAP(string screen, string productId, IAPStatus status, string localizedPrice, string isoCurrencyCode)
        {
#if USE_FIREBASE
            if (instance == null || AnalyticStatus != FirebaseStatus.Initialized)
            {
                if (instance == null)
                {
                    Debug.LogWarning(TAG + "NULL");
                }
                else
                {
                    Debug.LogWarning(TAG + "AnalyticStatus: " + AnalyticStatus);
                }
                return;
            }

            if (!string.IsNullOrEmpty(productId) && userData != null)
            {
                string id = "";
                if (!string.IsNullOrEmpty(productId) && productId.Split(".").Count() > 1)
                    id = productId.Split(".").LastOrDefault();

                if (string.IsNullOrEmpty(screen))
                    screen = GameStateManager.CurrentState.RegexToId();

                var param = new Parameter[]
                {
                    new Parameter( "product_id", productId ),
                    new Parameter( "localized_price", localizedPrice ),
                    new Parameter( "iso_currency_code", isoCurrencyCode ),
                    new Parameter( "total_play", userData.totalPlay ),
                    new Parameter( "total_win", userData.totalWin ),
                    new Parameter( "soft_currency", userData.totalSoftCurrency ),
                    new Parameter( "hard_currency", userData.totalHardCurrency ),
                    new Parameter( "login_day", userData.loginDay ),
                    new Parameter( "session", session )
                };

                FirebaseAnalytics.LogEvent("iap" + "_" + status.ToString().ToLower(), param);

                if (!string.IsNullOrEmpty(productId) && productId.Split(".").Count() > 1)
                    productId = productId.Split(".").LastOrDefault();
                FirebaseAnalytics.LogEvent("iap" + "_" + status.ToString().ToLower() + "_" + id, param);
            }
#endif
        }

        /// <summary>
        /// Trigger khi user nhận được tài nguyên
        /// </summary>
        /// <param name="value">Số lượng tài nguyên nhận được</param>
        /// <param name="currencyName">Tên loại tài nguyên</param>
        /// <param name="currencyType">Loại tài nguyên SortCurrency, Booster</param>
        /// <param name="placement">Vị trí sử dụng tài nguyên màn hình nào. Ví dụ: ScreenType.HomeScreen, InGameScreen, PopupIAP</param>
        /// <param name="reason">Lý do user nhận được từ tài nguyên nào, hành động gì = "from__by". Ví dụ: video_rewarded__suggesst_booster, daily_login__claim</param>
        /// <param name="level">Màn chơi thứ mấy.   Nếu là endless, classic thì truyền vào level của user</param>
        public static void LogResourceEarn(int value, string currencyName, CurrencyType currencyType,
            string placement, string reason = "video_rewarded__suggesst_booster", int level = 0)
        {
#if USE_FIREBASE
            if (instance == null || AnalyticStatus != FirebaseStatus.Initialized)
            {
                if (instance == null)
                {
                    Debug.LogWarning(TAG + "NULL");
                }
                else
                {
                    Debug.LogWarning(TAG + "AnalyticStatus: " + AnalyticStatus);
                }
                return;
            }

            var temp = virtualCurrencyParameters(value, currencyName, currencyType, placement, reason, level);
            FirebaseAnalytics.LogEvent("resource_earn", temp);
#endif
        }

        /// <summary>
        /// Trigger khi user sử dụng tài nguyên
        /// </summary>
        /// <param name="value">Số lượng tài nguyên đã sử dụng</param>
        /// <param name="currencyName">Tên loại tài nguyên</param>
        /// <param name="currencyType">Loại tài nguyên SortCurrency, Booster</param>
        /// <param name="placement">Vị trí sử dụng tài nguyên màn hình nào. Ví dụ: ScreenType.HomeScreen, InGameScreen, PopupIAP</param>
        /// <param name="reason">Vì sao tài nguyên được sử dụng hành động gì = "from__by". Ví dụ: hint__fail_5_times, upgrade_speed__suggesst_upgrade, add_more_time__turorial</param>
        /// <param name="level">Màn chơi thứ mấy. Nếu là endless, classic thì truyền vào level của user</param>
        public static void LogResourceSpend(int value, string currencyName, CurrencyType currencyType,
            string placement, string reason = "", int level = 0)
        {
#if USE_FIREBASE
            if (instance == null || AnalyticStatus != FirebaseStatus.Initialized)
            {
                if (instance == null)
                {
                    Debug.LogWarning(TAG + "NULL");
                }
                else
                {
                    Debug.LogWarning(TAG + "AnalyticStatus: " + AnalyticStatus);
                }
                return;
            }

            var temp = virtualCurrencyParameters(value, currencyName, currencyType, placement, reason, level);
            FirebaseAnalytics.LogEvent("resource_spend", temp);
#endif
        }


#if USE_FIREBASE
        public static Parameter[] virtualCurrencyParameters(int value, string currencyName, CurrencyType currencyType, string placement, string reason, int level)
        {
            var temp = new Parameter[] {
                    new Parameter(FirebaseAnalytics.ParameterValue, value),
                    new Parameter(FirebaseAnalytics.ParameterVirtualCurrencyName, currencyName.RegexToId()),
                    new Parameter("currency_type", StringExtend.RegexToId(currencyType.ToString())),
                    new Parameter("placement", placement.RegexToId()),
                    new Parameter(FirebaseAnalytics.ParameterItemName, reason.RegexToId()),
                    new Parameter("session", session ),
                    new Parameter("login_day", userData.loginDay ),
                    new Parameter(FirebaseAnalytics.ParameterLevel, level) };
            return temp;
        }
#endif

        public static void LogLevelUp(int userLevel = 0)
        {
#if USE_FIREBASE
            if (instance == null || AnalyticStatus != FirebaseStatus.Initialized)
            {
                if (instance == null)
                {
                    Debug.LogWarning(TAG + "NULL");
                }
                else
                {
                    Debug.LogWarning(TAG + "AnalyticStatus: " + AnalyticStatus);
                }
                return;
            }

            var temp = new List<Parameter>
            {
                new Parameter(FirebaseAnalytics.ParameterLevel, userLevel),
                new Parameter("session", session ),
                new Parameter( "login_day", userData.loginDay )
            };
            FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLevelUp, temp);
#endif
        }

        /// <summary>
        /// Trigger khi user kết thúc màn chơi
        /// </summary>
        /// <param name="state">Trạng thái của màn chơi</param>
        /// <param name="level">Level mà user chơi là level bao nhiêu</param>
        /// <param name="mode">Mode chơi của level là gì</param>
        /// <param name="play_type">User chơi level này trong hoàn cảnh nào</param>
        /// <param name="total_play">User chơi level này lần thứ mấy</param>
        /// <param name="total_lose">User đã thua level này mấy lần (trước khi bắt đầu lần chơi này)</param>
        /// <param name="duration_total">Thời gian cho phép chơi ban đầu của level (nếu game play không cho phép tăng thời gian chơi thì chỉ cần tên là total_duration)</param>
        /// <param name="duration_play">Thời gian user chơi level, tính từ lúc trigger level_play đến khi trigger level_end</param>
        /// <param name="item_total">Tổng số item cần xử lý trong level</param>
        /// <param name="item_cleared">Số item mà user đã xử lý được</param>
        /// <param name="player_rank">Hạng của người chơi như số sao 0,1,2,3</param>
        /// <param name="lose_by">Lý do thua (trong trường hợp user thua)</param>
        /// <param name="action_seq">Chuỗi hành động mà user thực hiện trong level - "GD gán alias cho mỗi action là 1, 2, 3, 4, 5,... => Dev log sequence theo thứ tự thực hiện action của user (lưu ý: trong chuỗi không có dầu cách sau dấu phẩy)"</param>
        public static void LogLevel(GameState state, int level, GameMode mode, PlayType play_type,
                                        int total_play, int total_lose,
                                        float duration_play = 0, int item_cleared = 0,
                                        float duration_total = 0, int item_total = 0,
                                        int player_rank = 0,
                                        string lose_by = null,
                                        string action_seq = null)
        {
#if USE_FIREBASE
            var parameter = levelParameter(level, mode, play_type, total_play, total_lose, duration_total, item_total);
            if (state != GameState.None)
                parameter = parameter.Append(new Parameter("state", state.RegexToId())).ToArray();

            if (duration_play > 0)
                parameter = parameter.Append(new Parameter("duration_play", Mathf.FloorToInt(duration_play))).ToArray();

            if (duration_total > 0)
                parameter = parameter.Append(new Parameter("duration_remain", Mathf.FloorToInt(duration_total - duration_play))).ToArray();

            if (item_cleared > 0)
                parameter = parameter.Append(new Parameter("item_cleared", item_cleared)).ToArray();

            if (player_rank > 0)
                parameter = parameter.Append(new Parameter("player_rank", player_rank)).ToArray();

            if (!string.IsNullOrEmpty(lose_by))
                parameter = parameter.Append(new Parameter("lose_by", lose_by)).ToArray();

            if (!string.IsNullOrEmpty(action_seq))
                parameter = parameter.Append(new Parameter("action_seq", action_seq)).ToArray();

            if (state == GameState.Play && userData != null)
                userData.currentLevel = level;

            LogEvent("level_" + state.RegexToId(), parameter);
#endif
        }

#if USE_FIREBASE
        /// <param name="level">Level mà user chơi là level bao nhiêu</param>
        /// <param name="mode">Mode chơi của level là gì</param>
        /// <param name="play_type">User chơi level này trong hoàn cảnh nào</param>
        /// <param name="play_index">User chơi level này lần thứ mấy</param>
        /// <param name="lose_index">User đã thua level này mấy lần (trước khi bắt đầu lần chơi này)</param>
        /// <param name="duration_total">Tổng số thời gian cho phép hoàn thành level</param>
        /// <param name="item_total">Tổng số item cần xử lý trong level</param>
        protected static Parameter[] levelParameter(long level, GameMode mode, PlayType play_type,
                                                    long play_index, long lose_index,
                                                    float duration_total, long item_total)
        {
            var parameter = new Parameter[] {
                        new Parameter(FirebaseAnalytics.ParameterLevel, level),
                        new Parameter(FirebaseAnalytics.ParameterLevelName, "level_" + level.ToString("#000000")),
                        new Parameter("mode", mode.RegexToId()),
                        new Parameter("play_type",play_type.RegexToId()),
                        new Parameter("play_index", play_index),
                        new Parameter("lose_index", lose_index),
                        new Parameter("session", session )
            };

            if (duration_total > 0)
                parameter = parameter.Append(new Parameter("duration_total", Mathf.FloorToInt(duration_total))).ToArray();
            if (item_total > 0)
                parameter = parameter.Append(new Parameter("item_total", Mathf.FloorToInt(item_total))).ToArray();
            if (userData != null)
                parameter = parameter.Append(new Parameter("login_day", userData.loginDay)).ToArray();
            return parameter;
        }
#endif

#if USE_FIREBASE
        public static void LogEvent(string eventName, Parameter[] parameters)
        {

            try
            {
                if (string.IsNullOrEmpty(eventName))
                {
                    Debug.LogWarning("eventName IsNullOrEmpty");
                    return;
                }

                if (instance == null || AnalyticStatus != FirebaseStatus.Initialized)
                {
                    if (instance == null)
                    {
                        Debug.LogWarning(TAG + "NULL");
                    }
                    else
                    {
                        Debug.LogWarning(TAG + "AnalyticStatus: " + AnalyticStatus + " " + eventName);
                    }
                    return;
                }

                eventName = eventName.RegexToId(32);

                if (parameters != null)
                    FirebaseAnalytics.LogEvent(eventName, parameters);
                else
                    FirebaseAnalytics.LogEvent(eventName);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
#endif

        public static void LogEvent(string eventName, Dictionary<string, object> dictionary = null)
        {
#if USE_FIREBASE
            try
            {
                if (string.IsNullOrEmpty(eventName))
                {
                    Debug.LogWarning("eventName IsNullOrEmpty");
                    return;
                }

                if (instance == null || AnalyticStatus != FirebaseStatus.Initialized)
                {
                    if (instance == null)
                    {
                        Debug.LogWarning(TAG + "NULL");
                    }
                    else
                    {
                        Debug.LogWarning(TAG + "AnalyticStatus: " + AnalyticStatus + " " + eventName);
                    }
                    return;
                }

                eventName = eventName.RegexToId(32);

                if (dictionary != null)
                {
                    dictionary.Add("session", session);
                    var parameters = dictionary.Where(x => x.Value != null && x.Key != null).Select(x =>
                    {
                        string key = StringExtend.RegexToId(x.Key);
                        if (x.Value is float f)
                            return new Parameter(key, f);
                        else if (x.Value is double d)
                            return new Parameter(key, d);
                        else if (x.Value is long l)
                            return new Parameter(key, l);
                        else if (x.Value is int i)
                            return new Parameter(key, i);
                        else if (x.Value is string s && !string.IsNullOrEmpty(s))
                        {
                            s = s.RegexToId();
                            return new Parameter(key, s);
                        }
                        else
                            return new Parameter(key, x.Value.ToString());
                    }).ToArray();

                    if (parameters != null)
                        FirebaseAnalytics.LogEvent(eventName, parameters);
                    else
                        FirebaseAnalytics.LogEvent(eventName);
                }
                else
                {
                    FirebaseAnalytics.LogEvent(eventName);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
#endif
        }

        public static void LogException(Exception ex)
        {
#if USE_FIREBASE
            if (instance == null || AnalyticStatus != FirebaseStatus.Initialized)
            {
                if (instance == null)
                {
                    Debug.LogWarning(TAG + "NULL");
                }
                else
                {
                    Debug.LogWarning(TAG + "AnalyticStatus: " + AnalyticStatus);
                }
                return;
            }

            Crashlytics.LogException(ex);
#endif
        }

        #region FirebaseMessage
#if USE_FIREBASE && USE_FIREBASE_MESSAGE
        private static void OnTokenReceived(object sender, TokenReceivedEventArgs token)
        {
            FirebaseToken = token.Token;
#if UNITY_ANDROID && USE_APPSFLYER
            AppsFlyerSDK.AppsFlyer.updateServerUninstallToken(FirebaseToken);
#endif

            Debug.Log(TAG + "On Token Received:" + "\n" + FirebaseToken);
        }


        public static void OnMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            FirebaseMessage = e.Message;

            if (FirebaseMessage != null)
            {
                string logData = "FirebaseMessage: Received a new message";
                var notification = FirebaseMessage?.Notification;
                if (notification != null)
                {
                    logData += "\n" + "Title: " + notification.Title;
                    logData += "\n" + "Body: " + notification.Body;
                    var android = notification.Android;
                    if (android != null)
                        logData += "\n" + "ChannelId: " + android.ChannelId;
                }

                if (FirebaseMessage.From.Length > 0)
                    logData += "\n" + "From: " + e.Message.From;

                if (FirebaseMessage.Link != null)
                    logData += "\n" + "Link: " + e.Message.Link.ToString();

                if (FirebaseMessage.Data != null)
                {
                    logData += "\n" + "Data:";
                    foreach (KeyValuePair<string, string> i in e.Message.Data)
                        logData += "\n" + i.Key + ": " + i.Value;
                }
            }
        }
#endif
        #endregion

        public static bool IsConnected
        {
            get
            {
                switch (Application.internetReachability)
                {
                    case NetworkReachability.ReachableViaLocalAreaNetwork:
                        return true;
                    case NetworkReachability.ReachableViaCarrierDataNetwork:
                        return true;
                    case NetworkReachability.NotReachable:
                    default:
                        return false;
                }
            }
        }

        public static void LogStatus()
        {
            string remoteData = "------------------";
            remoteData += "\n" + "Firebase Status: " + Status;
            remoteData += "\n" + "Analytic Status: " + AnalyticStatus;
            remoteData += "\n" + "Remote Status: " + RemoteStatus;
            remoteData += "\n" + "Message Status: " + MessageStatus;

#if USE_FIREBASE
            remoteData += "\n" + JsonUtility.ToJson(DataManager.GameConfig);
#endif
            Debug.Log(remoteData);
        }
    }

    public enum FirebaseStatus
    {
        UnAvailable,
        Checking,
        Available,
        Initialing,
        Initialized,
        Getting,
        Completed,
        Faulted,
        Canceled,
        TimeOut,
        NoInternet,
        UnkownError,
        Success,
        Fetching,
        Pending
    }
    #endregion
}