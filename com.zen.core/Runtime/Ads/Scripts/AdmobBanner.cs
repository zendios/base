using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
#if UNITY_IOS
using System.Runtime.InteropServices;
#endif

#if USE_ADMOB
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
#endif

namespace Base.Ads
{
    public class AdmobBanner : AdmobBase
    {
        [SerializeField] private bool loadOnStart = true;
        [SerializeField] private bool showOnLoaded = true;
        [SerializeField] private int timeAutoReload = 20;
        [SerializeField] private bool reloadOnClick = true;

#if UNITY_IOS
		[DllImport("__Internal")] private static extern float GetScreenScale();
		[DllImport("__Internal")] private static extern float GetScreenNativeScale();
		[DllImport("__Internal")] private static extern bool GetIsDownsampling();
#endif

        private float _screenScale;
        private float screenScale
        {
            get
            {
                if (_screenScale == 0)
                {
#if UNITY_IOS

					bool isDownsampling = GetIsDownsampling();
					float _screenNativeScale = GetScreenNativeScale();
					_screenScale = GetScreenScale();
					LogWarning($"screenScale: isDownsampling: {isDownsampling} NativeScale: {_screenNativeScale} Scale: {_screenScale}");
					if (isDownsampling)
						_screenScale = _screenNativeScale;
#else
                    _screenScale = Screen.dpi / 160f;
#endif
                }
                return _screenScale;
            }
        }

        protected BannerView ad = null;

#if !USE_ADMOB
        protected class BannerView : FakeAd { }
#endif

        [Header("BANNER NATIVE")]
        [SerializeField]
        protected AdZative zative = null;

        protected override void Start()
        {
            base.Start();

            if (userData != null)
                OnRemoveAdChanged(userData.isVIP || userData.isRemovedAds);

            if (loadOnStart && type == AdType.Banner)
                ReloadAd();
        }

        protected override void Awake()
        {
            base.Awake();

            if (config.adBannerFlow == AdFlow.OnlyDefault || config.adBannerFlow == AdFlow.Both)
            {
                if (zative != null)
                    zative.gameObject.SetActive(false);
            }
            else if (config.adBannerFlow == AdFlow.OnlyNative)
            {
                if (zative != null)
                    zative.gameObject.SetActive(true);

                gameObject.SetActive(false);
            }

            if (mainCanvas == null)
                mainCanvas = GetComponentInParent<Canvas>();

            if (mainCanvas == null)
                throw new Exception("mainCanvas NULL");

            if (adPlaceholder == null)
                adPlaceholder = GetComponentInChildren<RectTransform>();

            if (adPlaceholder == null)
                throw new Exception("adPlaceholder NULL");

            isInitialized = true;
        }

        private void OnEnable()
        {
            GameStateManager.OnGameStateChanged += OnGameStateChanged;
            UserDataBase.OnVIPChanged += OnRemoveAdChanged;
            UserDataBase.OnRemovedAdsChanged += OnRemoveAdChanged;
            OnAppOpenStateChanged += OnAdStateChanged;
        }

        private void OnDisable()
        {
            GameStateManager.OnGameStateChanged -= OnGameStateChanged;
            UserDataBase.OnVIPChanged -= OnRemoveAdChanged;
            UserDataBase.OnRemovedAdsChanged -= OnRemoveAdChanged;
            OnAppOpenStateChanged -= OnAdStateChanged;
        }

        protected override void OnDestroy()
        {
            DestroyAd();
            base.OnDestroy();
        }

        private void OnApplicationPause(bool pause)
        {
            if (!pause)
            {
                OnGameStateChanged(GameStateManager.CurrentState, GameState.Other, null);
            }
        }

        private void OnGameStateChanged(GameState current, GameState last, object data = null)
        {
            if (isInitialized && type == AdType.Banner
                && (state == AdState.None || state == AdState.LoadNotAvailable || state == AdState.ShowFailed || state == AdState.ShowNotAvailable)
                && (current == GameState.Main || current == GameState.Play || current == GameState.Resume
                || current == GameState.Lose || current == GameState.Win))
            {
                ReloadAd();
            }
        }

        private void OnRemoveAdChanged(bool isVip)
        {
            if (isVip)
            {
                foreach (RectTransform rectTransform in rootRectTransform)
                    rectTransform.offsetMin = Vector2.zero;
                StopAllCoroutines();
                DestroyAd();
            }

            if (adPlaceholder != null)
                adPlaceholder.gameObject.SetActive(!isVip);
        }

        private void OnAdStateChanged(AdState state)
        {
            if (type == AdType.Banner || type == AdType.Mrec)
            {
                if (state == AdState.LoadRequest || state == AdState.Show)
                {
                    Log("App open showing -> Destroy banner to save memory and avoid rendering conflicts.");
                    HideAd();
                }
                else if (state == AdState.ShowFailed || state == AdState.Close
                    || state == AdState.LoadNotAvailable || state == AdState.LoadTimeOut || state == AdState.ShowNotAvailable)
                {
                    // Also when the app open ad never showed (load failed / timed out): the banner was hidden on its
                    // LoadRequest and would otherwise stay hidden (state NotTime) for the rest of the session.
                    Log("App open closed / not shown -> Show banner again.");
                    ShowAd(null);
                }
            }
        }

        public override IEnumerator IELoadAd(Action<AdType, AdState> result, string item, float timeOut = 6.8f, ShowLoadingMode showLoadingMode = ShowLoadingMode.None)
        {
            if (!string.IsNullOrEmpty(item))
                this.item = item;
            else
                this.item = GameStateManager.CurrentState.ToString();
            this.result = result;

            if (!isCanShow)
            {
                yield return base.IELoadAd(result, item, timeOut, showLoadingMode);
                yield return IELoadAdWaterfall(LoadAd, result, item, timeOut);
            }
        }

        public bool IsEditor
        {
            get
            {
#if UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        protected override void LoadAd()
        {
            base.LoadAd();
#if USE_ADMOB
            if (ad != null && !ad.IsDestroyed)
            {
                ad.OnBannerAdLoaded -= OnAdLoaded;
                ad.OnBannerAdLoadFailed -= OnAdLoadError;
                ad.OnAdImpressionRecorded -= OnSuccess;
                ad.OnAdPaid -= OnAdPaid;
                ad.OnAdClicked -= OnAdClicked;
                ad.Destroy();
            }

            if (type == AdType.Mrec)
            {
                if (nativeX > 0)
                    ad = new BannerView(id, AdSize.MediumRectangle, nativeX, nativeY);
                else
                    ad = new BannerView(id, AdSize.MediumRectangle, AdPosition.Center);
            }
            else
            {
                int adWidth = AdSize.FullWidth;
                if (adWidth < 0)
                {
                    float deviceScale = MobileAds.Utils.GetDeviceScale();
                    if (deviceScale <= 0f)
                        deviceScale = 1f;
                    int safeWidth = Mathf.RoundToInt(Screen.safeArea.width / deviceScale);
                    if (safeWidth > 0)
                        adWidth = safeWidth;
                }

                AdSize adaptiveSize = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(adWidth);
                if (nativeX > 0)
                    ad = new BannerView(id, adaptiveSize, nativeX, nativeY);
                else
                    ad = new BannerView(id, adaptiveSize, AdPosition.Bottom);
            }

            if (!showOnLoaded)
                ad.Hide();
            ad.OnBannerAdLoaded += OnAdLoaded;
            ad.OnBannerAdLoadFailed += OnAdLoadError;
            ad.OnAdImpressionRecorded += OnSuccess;
            ad.OnAdPaid += OnAdPaid;
            ad.OnAdClicked += OnAdClicked;

            AdRequest request = new AdRequest();
            ad.LoadAd(request);
#endif
        }

        protected override void OnAdPaid(AdValue adValue)
        {
            // Revenue first, on the SDK thread (LogImpressionData sends AppsFlyer immediately, Unity work is enqueued).
            try
            {
                LogImpressionData(adValue);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                if (timeAutoReload > 0)
                    ReloadAd(timeAutoReload);
            });
        }

        private void OnAdClicked()
        {
            if (reloadOnClick)
            {
                Log("Ad clicked --> Reload Ad in 0.5s");
                ReloadAd();
            }
        }

        private void OnAdLoaded()
        {
#if USE_ADMOB
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                if (ad != null)
                    GetSourceInfo(ad.GetResponseInfo());
                state = AdState.LoadAvailable;
                SetAdCustomPosition();

                if (showOnLoaded)
                    ad.Show();
            });
#endif
        }

        protected void OnAdLoadError(LoadAdError error)
        {
#if USE_ADMOB
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                base.OnAdLoadError(error, () =>
                {
                    if (zative != null)
                    {
                        Log("Show Zative Banner as fallback");
                        gameObject.SetActive(false);
                        zative.gameObject.SetActive(true);
                        zative.ReloadAd();
                    }
                });
            });
#endif
        }

        public void HideAd()
        {
            ad?.Hide();
            state = AdState.NotTime;
        }

        public void ShowAd(Action<AdType, AdState> result)
        {
            this.result = result;
            if (isCanShow)
            {
                state = AdState.Show;
                ad?.Show();
            }
            else
            {
                state = AdState.ShowNotAvailable;
            }
        }

        public override void DestroyAd()
        {
#if USE_ADMOB
            if (ad != null && !ad.IsDestroyed)
            {
                ad.OnBannerAdLoaded -= OnAdLoaded;
                ad.OnBannerAdLoadFailed -= OnAdLoadError;
                ad.OnAdImpressionRecorded -= OnSuccess;
                ad.OnAdPaid -= OnAdPaid;
                ad.OnAdClicked -= OnAdClicked;
                ad.Destroy();
                Log($"DestroyAd {placement} SUCCESS");
            }
#endif
            base.DestroyAd();
        }

        public void SetAdCustomPosition()
        {
#if USE_ADMOB
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                try
                {
                    if (ad == null)
                    {
                        LogWarning("Optimus: Cannot set position. BannerView is null.");
                        return;
                    }

                    float adWidth = 320;
                    float adHeight = 50;
                    float scaleFactor = mainCanvas.scaleFactor;
                    if (type == AdType.Mrec)
                    {
                        adWidth = ad.GetWidthInPixels() / scaleFactor;
                        adHeight = ad.GetHeightInPixels() / scaleFactor;
                        adPlaceholder.sizeDelta = new Vector2(adWidth, adHeight);
                    }
                    else
                    {
                        adWidth = ad.GetWidthInPixels() / scaleFactor;
                        adHeight = ad.GetHeightInPixels() / scaleFactor;
                        adPlaceholder.sizeDelta = new Vector2(0, adHeight);

                        foreach (RectTransform rectTransform in rootRectTransform)
                            rectTransform.offsetMin = new Vector2(0, adHeight);
                    }

                    adPlaceholder.ForceUpdateRectTransforms();
                    adPlaceholder.GetWorldCorners(worldCorners);

                    Vector3 topLeftWorldPosition = worldCorners[1];
                    Vector2 screenPosition;

                    if (mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        // For Overlay, world position IS screen position (mostly), but GetWorldCorners handles it safely.
                        screenPosition = topLeftWorldPosition;
                    }
                    else
                    {
                        if (uiCamera == null)
                            uiCamera = Camera.allCameras.FirstOrDefault(x => x.orthographic);

                        // For Screen Space - Camera or World Space
                        if (uiCamera != null)
                        {
                            screenPosition = uiCamera.WorldToScreenPoint(topLeftWorldPosition);
                        }
                        else
                        {
                            // Fallback safe mode
                            screenPosition = RectTransformUtility.WorldToScreenPoint(null, adPlaceholder.position);
                        }
                    }

                    // 1. Invert Y-Axis
                    // Unity: (0,0) is Bottom-Left.
                    // AdMob: (0,0) is Top-Left.
                    // We need to calculate the distance from the TOP of the screen.
                    var safeArea = Screen.safeArea;
                    Vector2 anchorMin = safeArea.position;
                    float bottomHeight = anchorMin.y;
                    Vector2 anchorMax = safeArea.position + safeArea.size;
                    float topHeight = Screen.height - bottomHeight - safeArea.height;

                    float yFromTop = 0;
                    float xFromLeft = 0;

                    yFromTop = Screen.height - screenPosition.y;
                    xFromLeft = screenPosition.x;

                    // 2. Convert Pixels to Native Units (Dp or Points)
                    if (Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.OSXPlayer)
                    {
                        if (type == AdType.Mrec)
                        {
                            nativeX = ScalePixelsToNativeUnits(xFromLeft - anchorMin.x);
                        }
                        else
                        {
                            nativeX = ScalePixelsToNativeUnits(xFromLeft);
                        }
                        nativeY = ScalePixelsToNativeUnits(yFromTop - topHeight);
                    }
                    else
                    {
                        nativeX = ScalePixelsToNativeUnits(xFromLeft);
                        nativeY = ScalePixelsToNativeUnits(yFromTop);
                    }

                    Log($"screen ({Screen.width}x{Screen.height}) safeArea {safeArea} anchorMin: {anchorMin} anchorMax: {anchorMax} topHeight: {topHeight} bottomHeight: {bottomHeight} scaleFactor: {scaleFactor} screenPosition: {screenPosition} xFromLeft: {xFromLeft} yFromTop: {yFromTop} -> Native: ({nativeX}, {nativeY}) AdSize: ({adWidth}x{adHeight})");

                    // 3. Apply to AdMob
                    if (type == AdType.Banner && Application.platform == RuntimePlatform.Android)
                        ad.SetPosition(AdPosition.Bottom);
                    else
                        ad.SetPosition(nativeX, nativeY);

#if UNITY_EDITOR
                    if (type == AdType.Banner)
                        ad.SetPosition(AdPosition.Bottom);
#endif
                }
                catch (Exception ex)
                {
                    LogError("Failed to set ad position: " + ex.Message);
                }
            });
#endif
        }

        /// <summary>
        /// Converts Unity physical pixels to Device Independent Pixels (Android) or Points (iOS).
        /// </summary>
        private int ScalePixelsToNativeUnits(float pixelValue)
        {
            Log($"ScalePixelsToNativeUnits: pixelValue: {pixelValue} dpi: {Screen.dpi} screenScale: {screenScale}");
            return Mathf.RoundToInt(pixelValue / screenScale);
        }

        [Header("OPTIONs")]
        [SerializeField] private Camera uiCamera;
        private Vector3[] worldCorners = new Vector3[4];
        [SerializeField] protected Canvas mainCanvas;
        [SerializeField] protected RectTransform adPlaceholder;
        [SerializeField]
        [Tooltip("This is the list RectTransform, we need to set for offsetBottom when Banner Loaded")]
        protected List<RectTransform> rootRectTransform;
        private int nativeX;
        private int nativeY;
    }
}
