using System;
using System.Globalization;
using UnityEngine;

namespace Base
{
    [Serializable]
    public class UserDataBase : PlayerDataBase
    {
        // --- EVENTS ---
        // UI and Managers should subscribe to these instead of polling checks in Update()
        public delegate void VIPChangedDelegate(bool isVip);
        public static event VIPChangedDelegate OnVIPChanged;

        public delegate void RemovedAdsChangedDelegate(bool isRemoveAds);
        public static event RemovedAdsChangedDelegate OnRemovedAdsChanged;

        public delegate void DataChangedDelegate(long changedValue, long newValue);
        public static event DataChangedDelegate OnSoftCurrencyChanged;
        public static event DataChangedDelegate OnHardCurrencyChanged;

        // --- ANALYTICS PROPERTIES ---

        [Header("Analytics")]
        [SerializeField]
        protected bool _isNonOrganic = false;
        public bool isNonOrganic
        {
            get => _isNonOrganic;
            set
            {
                if (_isNonOrganic != value)
                {
                    _isNonOrganic = value;
                    SetUserProperty("is_Non_Organic", _isNonOrganic);
                }
            }
        }

        [SerializeField]
        private bool _isNew;
        public bool isNew
        {
            get => _isNew;
            set
            {
                if (_isNew != value)
                {
                    _isNew = value;
                    SetUserProperty("is_new", _isNew);
                }
            }
        }

        [SerializeField]
        protected bool _isRemovedAds = false;
        public bool isRemovedAds
        {
            get => _isRemovedAds;
            set
            {
                if (value != _isRemovedAds)
                {
                    _isRemovedAds = value;
                    SetUserProperty("is_removed_ads", _isRemovedAds);
                    OnRemovedAdsChanged?.Invoke(_isRemovedAds);
                }
            }
        }

        [SerializeField]
        protected bool _isVip = false;
        public bool isVIP
        {
            get => _isVip;
            set
            {
                if (value != _isVip)
                {
                    _isVip = value;
                    SetUserProperty("is_VIP", _isVip);
                    OnVIPChanged?.Invoke(_isVip);
                }
            }
        }

        // --- CURRENCY ---

        [Header("Money")]
        [SerializeField]
        protected int _totalSoftCurrency = 0;
        public int totalSoftCurrency
        {
            get => _totalSoftCurrency;
            set
            {
                if (_totalSoftCurrency != value)
                {
                    int changed;
                    if (_totalSoftCurrency > value)
                    {
                        changed = _totalSoftCurrency - value;
                        _totalSoftSpend += changed;
                        // Optimus: Consolidated tracking. removed individual spend tracking here to avoid spam.
                    }
                    else
                    {
                        changed = value - _totalSoftCurrency;
                        _totalSoftEarn += changed;
                    }

                    _totalSoftCurrency = value;
                    SetUserProperty("soft_currency", _totalSoftCurrency);
                    OnSoftCurrencyChanged?.Invoke(changed, _totalSoftCurrency);
                }
            }
        }

        [SerializeField] protected int _totalSoftEarn = 0;
        [SerializeField] protected int _totalSoftSpend = 0;

        [SerializeField]
        private int _totalHardCurrency;
        public int totalHardCurrency
        {
            get => _totalHardCurrency;
            set
            {
                if (_totalHardCurrency != value)
                {
                    int changed;
                    if (_totalHardCurrency > value)
                    {
                        changed = _totalHardCurrency - value;
                        _totalHardSpend += changed;
                    }
                    else
                    {
                        changed = value - _totalHardCurrency;
                        _totalHardEarn += changed;
                    }

                    _totalHardCurrency = value;
                    SetUserProperty("hard_currency", _totalHardCurrency);
                    OnHardCurrencyChanged?.Invoke(changed, _totalHardCurrency);
                }
            }
        }

        [SerializeField] protected int _totalHardEarn = 0;
        [SerializeField] protected int _totalHardSpend = 0;

        // --- VERSIONING & SESSION ---

        [SerializeField]
        private int _versionInstall;
        public int versionInstall
        {
            get => _versionInstall;
            set
            {
                if (_versionInstall != value)
                {
                    _versionInstall = value;
                    SetUserProperty("version_install", _versionInstall);
                }
            }
        }

        [SerializeField]
        private int _versionCurrent;
        public int versionCurrent
        {
            get => _versionCurrent;
            set
            {
                if (_versionCurrent != value)
                {
                    _versionCurrent = value;
                    SetUserProperty("version_current", _versionCurrent);
                }
            }
        }

        [SerializeField]
        private long _session = 0;
        public long session
        {
            get => _session;
            set
            {
                if (_session != value && value > 0)
                {
                    _session = value;
                    SetUserProperty("session", _session);
                }
            }
        }

        // --- GAMEPLAY STATS ---

        private const string KEY_LOGIN_COUNT = "LOGIN_COUNT";

        [SerializeField]
        private int _loginDay = -999;
        public int loginDay
        {
            get
            {
                if (_loginDay == -999)
                    _loginDay = PlayerPrefs.GetInt(KEY_LOGIN_COUNT, -1);
                return _loginDay;
            }
            set
            {
                if (_loginDay != value)
                {
                    _loginDay = value;
                    // Optimus: Firebase Logic moved to AdRevenueTracker/AnalyticsManager
                    // Optimus: PlayerPrefs.SetInt moved to specific Init/CheckDailyLogin methods to avoid redundancy
                }
            }
        }

        [SerializeField]
        private int _totalPlay = 0;
        public int totalPlay
        {
            get => _totalPlay;
            set
            {
                if (_totalPlay != value && value > 0)
                {
                    _totalPlay = value;
                    SetUserProperty("total_play", _totalPlay);
                }
            }
        }

        [SerializeField]
        private int _totalWin = 0;
        public int totalWin
        {
            get => _totalWin;
            set
            {
                if (_totalWin != value && value > 0)
                {
                    _totalWin = value;
                    SetUserProperty("total_win", _totalWin);
                }
            }
        }

        [SerializeField]
        private int _totalLose = 0;
        public int totalLose
        {
            get => _totalLose;
            set
            {
                if (_totalLose != value && value > 0)
                {
                    _totalLose = value;
                    SetUserProperty("totalLose", _totalLose);
                }
            }
        }

        [SerializeField]
        private int _totalTimePlay = 0;
        public int totalTimePlay
        {
            get => _totalTimePlay;
            set
            {
                if (_totalTimePlay != value && value > 0)
                {
                    _totalTimePlay = value;
                    SetUserProperty("total_time_play", _totalTimePlay);
                }
            }
        }

        [SerializeField]
        private int _currentLevel = 0;
        public int currentLevel
        {
            get => _currentLevel;
            set
            {
                if (_currentLevel != value && value > 0)
                {
                    _currentLevel = value;
                    SetUserProperty("current_level", _currentLevel);
                }
            }
        }

        // --- ADS DATA (Logic Removed - Pure Data) ---

        [Header("Ads")]
        [SerializeField] private int _adBanner = 0;
        public int adBanner
        {
            get => _adBanner;
            set { if (_adBanner != value) { _adBanner = value; SetUserProperty("ad_banner", _adBanner); } }
        }

        [SerializeField] private int _adInterstitial = 0;
        public int adInterstitial
        {
            get => _adInterstitial;
            set
            {
                if (_adInterstitial != value)
                {
                    _adInterstitial = value;
                    SetUserProperty("ad_interstitial", _adInterstitial);
                    SetUserProperty("ad_total", adTotal);
                    // Legacy logic moved to AdRevenueTracker
                }
            }
        }

        [SerializeField] private int _adRewarded = 0;
        public int adRewarded
        {
            get => _adRewarded;
            set
            {
                if (_adRewarded != value)
                {
                    _adRewarded = value;
                    SetUserProperty("ad_rewarded", _adRewarded);
                    SetUserProperty("ad_total", adTotal);
                    // Legacy logic moved to AdRevenueTracker
                }
            }
        }

        [SerializeField] private int _adRewardSkipped = 0;
        public int adRewardSkipped
        {
            get => _adRewardSkipped;
            set
            {
                if (_adRewardSkipped != value)
                    _adRewardSkipped = value;
            }
        }

        public int adTotal => adInterstitial + adRewarded;

        // --- UA & A/B TESTING ---

        [SerializeField] private string _abTesting;
        public string abTesting
        {
            get => _abTesting;
            set { if (!string.IsNullOrEmpty(value) && _abTesting != value) { _abTesting = value; SetUserProperty("ab_testing", value); } }
        }

        [SerializeField] protected string _ua_network;
        public string ua_network { get => _ua_network; set { if (!string.IsNullOrEmpty(value) && _ua_network != value) { _ua_network = value; SetUserProperty("ua_network", value); } } }

        [SerializeField] protected string _ua_campaign;
        public string ua_campaign { get => _ua_campaign; set { if (!string.IsNullOrEmpty(value) && _ua_campaign != value) { _ua_campaign = value; SetUserProperty("ua_campaign", value); } } }

        [SerializeField] protected string _ua_adgroup;
        public string ua_adgroup { get => _ua_adgroup; set { if (!string.IsNullOrEmpty(value) && _ua_adgroup != value) { _ua_adgroup = value; SetUserProperty("ua_adgroup", value); } } }

        [SerializeField] protected string _ua_creative;
        public string ua_creative { get => _ua_creative; set { if (!string.IsNullOrEmpty(value) && _ua_creative != value) { _ua_creative = value; SetUserProperty("ua_creative", value); } } }

        [SerializeField] protected string _ua_tracker_name;
        public string ua_tracker_name { get => _ua_tracker_name; set { if (!string.IsNullOrEmpty(value) && _ua_tracker_name != value) { _ua_tracker_name = value; SetUserProperty("ua_tracker_name", value); } } }

        // --- STREAKS ---

        [SerializeField] protected int _winStreak;
        public int winStreak
        {
            get => _winStreak;
            set { if (_winStreak != value) { _winStreak = value; if (_winStreak > 0) { loseStreak = 0; SetUserProperty("winStreak", _winStreak); } } }
        }

        [SerializeField] protected int _loseStreak;
        public int loseStreak
        {
            get => _loseStreak;
            set { if (_loseStreak != value) { _loseStreak = value; if (_loseStreak > 0) { _winStreak = 0; SetUserProperty("loseStreak", _loseStreak); } } }
        }

        // --- LTV (Pure Data) ---

        [SerializeField] protected double _lifeTimeValue;
        public double lifeTimeValue
        {
            get => _lifeTimeValue;
            set
            {
                if (_lifeTimeValue != value)
                {
                    _lifeTimeValue = value;
                    if (_lifeTimeValue > 0)
                        SetUserProperty("life_time_value", _lifeTimeValue.ToString("#0.0000", culture));
                    // Milestone logic moved to AdRevenueTracker
                }
            }
        }

        public static CultureInfo culture = CultureInfo.CreateSpecificCulture("en-US");

        // --- HELPERS ---

        public void SetUserProperty(string title, object value)
        {
            try
            {
#if USE_FIREBASE
                // Optimus Note: Ensure RegexToId() is optimized or cached if possible.
                ZenAnalytics.SetUser(title.RegexToId(32), value.RegexToId());
#endif
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private const string KEY_INSTALL_DATE = "INSTALL_DATE";
        private const string KEY_LAST_LOGIN_DATE = "LAST_LOGIN_DATE";

        public void Init()
        {
            if (!PlayerPrefs.HasKey(KEY_INSTALL_DATE))
            {
                string todayTicks = DateTime.Today.Ticks.ToString();
                PlayerPrefs.SetString(KEY_INSTALL_DATE, todayTicks);
                PlayerPrefs.Save(); // Save once at initialization
                Debug.Log($"Optimus: First Install Date Recorded: {DateTime.Today}");
            }

            CheckDailyLogin();
            GetDaysSinceInstall();
        }

        public int GetDaysSinceInstall()
        {
            string ticksString = PlayerPrefs.GetString(KEY_INSTALL_DATE);
            if (long.TryParse(ticksString, out long installTicks))
            {
                DateTime installDate = new DateTime(installTicks);
                DateTime today = DateTime.Today;
                TimeSpan difference = today - installDate;
                return difference.Days + 1;
            }
            return 1;
        }

        public int CheckDailyLogin()
        {
            string lastLoginStr = PlayerPrefs.GetString(KEY_LAST_LOGIN_DATE, "0");
            long.TryParse(lastLoginStr, out long lastLoginTicks);

            DateTime lastLoginDate = new DateTime(lastLoginTicks);
            DateTime today = DateTime.Today;

            if (lastLoginTicks == 0 || lastLoginDate < today)
            {
                int currentDay = PlayerPrefs.GetInt(KEY_LOGIN_COUNT, 0);
                loginDay = currentDay + 1; // Update property

                PlayerPrefs.SetInt(KEY_LOGIN_COUNT, loginDay);
                PlayerPrefs.SetString(KEY_LAST_LOGIN_DATE, today.Ticks.ToString());
                PlayerPrefs.Save(); // Save once per day is acceptable

                Debug.Log($"Optimus: New Day Login! Count: {loginDay}");
            }
            return loginDay;
        }
    }
}