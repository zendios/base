using System;
using System.Collections.Generic;
using UnityEngine;

namespace Base.Ads
{
    /// <summary>
    /// Ad settings. Value order at runtime: Remote Config (AdConfigRemote) over the game's asset
    /// (Assets/Resources/AdConfig.asset, made by Base > Hub > Setup) over the defaults written in this class.
    /// A field added by a later package version starts at its default here; the game's other values stay.
    /// </summary>
    public class AdConfig : ScriptableObject
    {
        public const string ResourcePath = "AdConfig";

        private static AdConfig instance;

        /// <summary>The game's AdConfig, or an instance with this class's defaults when the game has none.</summary>
        public static AdConfig Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<AdConfig>(ResourcePath);
                    if (instance == null)
                    {
                        instance = CreateInstance<AdConfig>();
                        Debug.LogWarning("[AdConfig] no Resources/AdConfig asset in the game (Base > Hub > Setup creates it) --> package defaults");
                    }
                }
                return instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;   // Enter Play Mode without domain reload
        }

        [Header("TEST")]
        [SerializeField]
        internal bool useIdTest;

        protected AdPlatform _flagsNetWork;
        public AdPlatform flagsNetWork
        {
            get
            {
                if (_flagsNetWork == AdPlatform.NONE)
                {
                    if (adsNetWork.Contains(AdPlatform.IRON) && adsNetWork.Contains(AdPlatform.ADMOB))
                        _flagsNetWork = AdPlatform.IRON | AdPlatform.ADMOB;
                    else if (adsNetWork.Contains(AdPlatform.MAX) && adsNetWork.Contains(AdPlatform.ADMOB))
                        _flagsNetWork = AdPlatform.MAX | AdPlatform.ADMOB;
                    else if (adsNetWork.Contains(AdPlatform.ADMOB))
                        _flagsNetWork = AdPlatform.ADMOB;
                }
                return _flagsNetWork;
            }
            set
            {
                if (_flagsNetWork != value)
                {
                    _flagsNetWork = value;

                    if (_flagsNetWork == AdPlatform.NONE)
                    {
                        adsNetWork.Clear();
                        flagsNetWork = AdPlatform.NONE;
                    }

                    if (flagsNetWork == AdPlatform.IRON)
                    {
                        adsNetWork = new List<AdPlatform> { AdPlatform.IRON };
                        Debug.Log("Use Mediation: IRON");
                    }
                    else if (flagsNetWork == AdPlatform.MAX)
                    {
                        adsNetWork = new List<AdPlatform> { AdPlatform.MAX };
                        Debug.Log("Use Mediation: MAX");
                    }
                    else if (flagsNetWork == AdPlatform.ADMOB)
                    {
                        adsNetWork = new List<AdPlatform> { AdPlatform.ADMOB };
                        Debug.Log("Use Mediation: ADMOB");
                    }
                    else if (flagsNetWork == (AdPlatform.IRON | AdPlatform.ADMOB))
                    {
                        adsNetWork = new List<AdPlatform> { AdPlatform.IRON, AdPlatform.ADMOB };
                        Debug.Log("Use Mediation: IRON + ADMOB");
                    }
                    else if (flagsNetWork == (AdPlatform.MAX | AdPlatform.ADMOB))
                    {
                        adsNetWork = new List<AdPlatform> { AdPlatform.MAX, AdPlatform.ADMOB };
                        Debug.Log("Use Mediation: MAX + ADMOB");
                    }
                    else if (flagsNetWork != AdPlatform.NONE)
                    {
                        flagsNetWork = AdPlatform.NONE;
                        adsNetWork.Clear();
                        Debug.LogError("NOT SUPPORT");
                    }
                }
            }
        }

        public List<AdPlatform> adsNetWork = new List<AdPlatform> { AdPlatform.ADMOB };

        public string urlPrivacyPolicy
        {
            get
            {
                if (Application.platform == RuntimePlatform.Android)
                    return urlPrivacyPolicyIOS;
                return urlPrivacyPolicyIOS;
            }
        }
        [Header("POLICY")]
        [SerializeField] protected string urlPrivacyPolicyANDROID = "https://zendios.net/privacy-policy";
        [SerializeField] protected string urlPrivacyPolicyIOS = "https://zendios.net/privacy-policy";


        public string urlTermsOfService
        {
            get
            {
                if (Application.platform == RuntimePlatform.Android)
                    return urlTermsOfServiceANDROID;
                return urlTermsOfServiceIOS;
            }
        }
        [Header("TERM")]
        [SerializeField] protected string urlTermsOfServiceANDROID = "https://zendios.net/privacy-policy";
        [SerializeField] protected string urlTermsOfServiceIOS = "https://zendios.net/privacy-policy";

        [Header("APPSFLYER")]
        public string devKey = "Qhno4yJY6KHmZp9uS9DRe4";

        [Header("APPLE")]
        public string appId;

        [Header("BASE")]
        [SerializeField]
        protected float _adTimePlayToShow = 15;
        public float adTimePlayToShow
        {
            get
            {
                return _adTimePlayToShow;
            }
            set
            {
                _adTimePlayToShow = value;
            }
        }

        [SerializeField]
        protected float _adTimePlayReduceToShow = 15;
        /// <summary>
        /// On show any ads success --> increase or decrease timePlayToShowAd.
        /// Ex:
        /// firsTime timePlayToShowAd = 15s
        /// timePlayToShowAdRecuce = -5s
        /// --> show ads success = 2
        /// nextTime timePlayToShowAd = 15 + (2 * -5) = 5s
        /// </summary>
        public float adTimePlayReduceToShow
        {
            get
            {
                return _adTimePlayReduceToShow;
            }
            set
            {
                _adTimePlayReduceToShow = value;
            }
        }

        [SerializeField]
        protected float _adLoadTimeout = 6.8f;
        public float adLoadTimeout
        {
            get
            {
                return _adLoadTimeout;
            }
            set
            {
                _adLoadTimeout = value;
            }
        }

        [SerializeField]
        protected float _adTimeBetween = 6.8f;
        public float adTimeBetween
        {
            get
            {
                return _adTimeBetween;
            }
            set => _adTimeBetween = value;
        }

        [SerializeField]
        protected float _adCtrMeta = 0.5f;
        public float adCtrMeta
        {
            get
            {
                return _adCtrMeta;
            }
            set
            {
                _adCtrMeta = value;
                if (_adCtrMeta < 0.25f)
                    _adCtrMeta = 0.25f;
            }
        }

        [SerializeField]
        protected AdFlow _adBannerFlow = AdFlow.OnlyNative;
        public AdFlow adBannerFlow
        {
            get
            {
                return _adBannerFlow;
            }
            set
            {
                _adBannerFlow = value;
            }
        }

        [SerializeField]
        protected AdFlow _adAppOpenFlow = AdFlow.OnlyNative;
        public AdFlow adAppOpenFlow
        {
            get
            {
                return _adAppOpenFlow;
            }
            set
            {
                _adAppOpenFlow = value;
            }
        }

        [SerializeField]
        protected AdFlow _adInterFlow = AdFlow.Both;
        public AdFlow adInterFlow
        {
            get
            {
                return _adInterFlow;
            }
            set
            {
                _adInterFlow = value;
            }
        }

        [SerializeField, Tooltip("Rewarded: OnlyDefault = AdMob rewarded, OnlyNative = native rewarded, Both = AdMob first, native when AdMob has no ad.")]
        protected AdFlow _adRewardFlow = AdFlow.Both;
        public AdFlow adRewardFlow
        {
            get
            {
                return _adRewardFlow;
            }
            set
            {
                _adRewardFlow = value;
            }
        }

        [SerializeField]
        protected int _adInterOnPlay = 0;
        public int adInterOnPlay
        {
            get
            {
                if (_adInterOnPlay < 0)
                    _adInterOnPlay = 0;
                return _adInterOnPlay;
            }
            set
            {
                _adInterOnPlay = value;
            }
        }

        [SerializeField]
        protected bool _adInterOnStart = true;
        public bool adInterOnStart
        {
            get
            {
                return _adInterOnStart;
            }
            set => _adInterOnStart = value;
        }

        [SerializeField]
        protected bool _adInterOnComplete = true;
        public bool adInterOnComplete
        {
            get
            {
                return _adInterOnComplete;
            }
            set => _adInterOnComplete = value;
        }

        [Header("FTUE")]
        [SerializeField]
        public int _adInterOnFTUE = 1;
        public int adInterOnFTUE
        {
            get
            {
                return _adInterOnFTUE;
            }
            set
            {
                _adInterOnFTUE = value;
            }
        }

        [SerializeField]
        protected int _adMrecOnFTUE = 1;
        public int adMrecOnFTUE
        {
            get
            {
                return _adMrecOnFTUE;
            }
            set
            {
                _adMrecOnFTUE = value;
            }
        }

        [Header("BANNER")]
        [SerializeField]
        protected int _adBannerReload = 30;
        /// <summary>Banner reload after an impression, seconds. Never below 30 s (AdMob refresh guideline).</summary>
        public int adBannerReload
        {
            get
            {
                return Mathf.Max(AdZative.MinInlineReloadSec, _adBannerReload);
            }
            set
            {
                _adBannerReload = value;
            }
        }

        [SerializeField]
        protected int _adBannerOffset = 10;
        public int adBannerOffset
        {
            get
            {
                return _adBannerOffset;
            }
            set
            {
                _adBannerOffset = value;
            }
        }

        [Header("SURPRISE REWARD CONFIG")]
        [SerializeField, Range(1, 20), Tooltip("Show 1 Rewarded Ad after every N successful Interstitials")]
        public int _adInterVsRewardRatio = 5;
        public int adInterVsRewardRatio
        {
            get
            {
                return _adInterVsRewardRatio;
            }
            set => _adInterVsRewardRatio = value;
        }

        public int _rewardLevel = 100;
        public int rewardLevel
        {
            get
            {
                if (_rewardLevel <= 0)
                {
                    _rewardLevel = 10;
                }

                return _rewardLevel;
            }
            set
            {
                if (value > 0)
                {
                    _rewardLevel = value;
                }
            }
        }

        [SerializeField]
        protected int _rewardByAd = 500;
        public int rewardByAd
        {
            get
            {
                if (_rewardByAd <= 0)
                {
                    _rewardByAd = 100;
                }

                return _rewardByAd;
            }
            set
            {
                if (value > 0)
                {
                    _rewardByAd = value;
                }
            }
        }

        [SerializeField]
        protected float _rewardLevelScale = 0.05f;
        public float rewardLevelScale
        {
            get
            {
                if (_rewardLevelScale <= 0f)
                {
                    _rewardLevelScale = 0.05f;
                }

                return _rewardLevelScale;
            }
            set
            {
                if (value > 0f)
                {
                    _rewardLevelScale = value;
                }
            }
        }
    }

    [Serializable]
    public enum AdFlow
    {
        OnlyNative = 0,
        OnlyDefault = 1,
        Both = 2,
    }

}
