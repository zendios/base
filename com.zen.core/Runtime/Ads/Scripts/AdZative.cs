using Base;
using Base.Ads;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
#if USE_ADMOB
using GoogleMobileAds.Api;
#endif
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Native ad placement (AdMob native, GMA Next-Gen) powered by the AdZativeSDK plugin (Packages/com.zen.plugins.adzative):
/// banner / MREC drawn on a Canvas rect, interstitial / app open / rewarded as full-screen Views - all inside the Unity
/// window, so the game never loses focus.
///
/// What comes from AdBase / AdmobBase (unchanged for every game): the waterfall over idList, states and the result
/// callback, Firebase / AppsFlyer / Taichi analytics (LogImpressionData), PauseApp, the reload chain, VIP / removed ads.
/// What comes from the plugin:
///  - READY before needed: after the first request of a placement the plugin keeps one ad cached for it, taking the IDs
///    in idList order (an ID is used only while the previous ones fail = the waterfall order). A show or a banner
///    refresh then uses the cached ad at once; with nothing cached the AdBase waterfall loads it (LoadUnit per step).
///    App open is never preloaded: it loads only when a show is requested.
///  - layouts zen_banner (Native Banner, portrait + landscape), zen_mrec, zen_inter_media_top / _center
///    (interstitial + rewarded), zen_appopen, or a custom one; Manual / Auto layout, VideoMode
///    (Allow / Gate / Block), media aspect / scale, pods (N interstitial / rewarded ads in a row).
/// iOS: not supported yet (the plugin is Android only) - loads answer LoadNotAvailable at once.
/// </summary>
public class AdZative : AdmobBase
{
    #region Inspector
    [Header("Layout")]
    [Tooltip("Built-in style of the format (interstitial / rewarded: 1 = media top, 2 = media center; banner, MREC, app open: 1), or Custom (customLayout).")]
    [SerializeField] private AdZativeLayoutStyle layoutStyle = AdZativeLayoutStyle.Style1;
    [Tooltip("Android layout name when Layout Style = Custom (must contain ad_native_view, ad_headline, ad_attribution, ad_media for MREC / full-screen).")]
    [FormerlySerializedAs("layoutName")]
    [SerializeField] private string customLayout = "";
    [Tooltip("Auto: the plugin picks the built-in style that shows the most assets of each ad (layoutStyle = fallback).")]
    [SerializeField] private AdZativeLayoutMode layoutMode = AdZativeLayoutMode.Manual;

    [Header("Media")]
    [Tooltip("Allow: video plays in the MediaView. Block: video ads are rejected (banner strips). Gate (inline): the game decides via OnVideoGate.")]
    [SerializeField] private AdZativeVideoMode videoMode = AdZativeVideoMode.Allow;
    [SerializeField] private MediaAspectRatio desiredAspectRatio = MediaAspectRatio.ANY;
    [SerializeField] private ImageScaleType desiredScaleType = ImageScaleType.FIT_CENTER;
    [Tooltip("Legacy prefab value: true = no video (VideoMode.Block).")]
    [SerializeField, HideInInspector] private bool forceImage = false;

    [Header("Behaviour")]
    [SerializeField] private bool loadOnStart = false;
    [SerializeField] private bool showOnLoaded = false;
    [Tooltip("Banner / MREC: reload (swap to the next cached ad) this many seconds after an impression, at least 30. 0 = off. Banner: Remote Config adBannerReload.")]
    [SerializeField] private int timeAutoReload = 20;
    [Tooltip("Interstitial / rewarded: ads shown in a row (\"Ad 1 of N\"). IDs of idList first, then more ads of the same IDs. 1 = single ad.")]
    [Range(1, 3)]
    [SerializeField] private int podSize = 1;

    [Header("Full-screen timing (Remote Config key = placement name, see ApplyRemoteConfig)")]
    [Tooltip("Image: 10s countdown, ✕ / Next at 5s; later pod ads 5s. Video: its length, whole show ≤ 30s, first ad 2× the next.")]
    [SerializeField] private AdZativeTiming timing = new AdZativeTiming();

    [Header("UI References")]
    [SerializeField] private Camera uiCamera;
    [SerializeField] private Canvas canvas;
    [Tooltip("Ad area for MREC / full-screen. Banner uses Background Rect (the reserved strip).")]
    [SerializeField] private RectTransform placeholder;

    [Header("Banner Strip (reserved area + game UI shift)")]
    [SerializeField] protected GameObject background;
    [SerializeField] protected RectTransform backgroundRect;
    [SerializeField] protected int offsetTop = 20;
    [SerializeField] protected int offsetTopDp = 10;
    [SerializeField] protected int size = 64;
    [SerializeField] protected int sizeDp = 32;
    [SerializeField] protected int sizeRequest = 240;
    [SerializeField] protected int sizeRequestDp = 120;
    [SerializeField] protected List<RectTransform> rootRectTransform;
    #endregion

    #region Types and Properties
    /// <summary>Fastest banner / MREC reload allowed (AdMob: refresh no faster than every 30 s).</summary>
    public const int MinInlineReloadSec = 30;

    public enum MediaAspectRatio { ANY = 1, LANDSCAPE = 2, PORTRAIT = 3, SQUARE = 4 }
    public enum ImageScaleType { FIT_CENTER = 0, CENTER_INSIDE = 1, CENTER_CROP = 2 }

    public override bool isCanShow { get; set; }

    /// <summary>Raised when a Gate-mode inline slot holds a video ad: call AcceptVideo() (after making room) or RejectVideo().</summary>
    public event Action<AdZative, AdZativeAdInfo> OnVideoGate;

    /// <summary>Last ad the plugin reported as next for this placement (media, video length, network, assets).</summary>
    public AdZativeAdInfo NextAdInfo { get; private set; }

    public AdZativeLayoutStyle LayoutStyle => layoutStyle;
    /// <summary>Built-in styles this placement's format has (interstitial / rewarded: 2, others: 1).</summary>
    public int LayoutStyleCount => AdZativeSlotConfig.StyleCount(PluginFormat);
    public AdZativeLayoutMode LayoutMode => layoutMode;
    public AdZativeVideoMode VideoMode => EffectiveVideoMode;

    private static bool IsSupportedPlatform => Application.platform == RuntimePlatform.Android;
    private static bool IsEditor => Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.OSXEditor;

    private bool IsFullscreenType => type == AdType.Inter || type == AdType.RewardInter || type == AdType.AppOpen || type == AdType.Reward;
    private bool IsInline => !IsFullscreenType;
    /// <summary>App open loads only when a show is requested (Base rule): no plugin preload.</summary>
    private bool Preloads => type != AdType.AppOpen;
    /// <summary>Banner = Native Banner (no MediaView): video ads are never shown in it.</summary>
    private AdZativeVideoMode EffectiveVideoMode => type == AdType.Banner || forceImage ? AdZativeVideoMode.Block : videoMode;
    #endregion

    #region Private Fields
    private float _scaleFactor;
    private bool slotRegistered;
    private bool subscribed;
    private bool inlineVisible;
    private bool swapPending;          // inline: an ad loaded while one is on screen (reload chain) -> swap on show
    private bool inlineHidden;         // Hide(): the ad is kept for Resume(); reloads must not pop it back up
    private string slotId;             // read on the Android callback thread (OnPaidRaw)
    private Rect lastRect;
    private int rectFrame;
    private readonly Vector3[] _cachedCorners = new Vector3[4];
    #endregion

    #region Unity Lifecycle
    protected override void Awake()
    {
        base.Awake();
        gameObject.name = placementName;
    }

    protected override void Start()
    {
        StartCoroutine(DOInitializePlugin());
    }

    public override void OnValidate()
    {
        base.OnValidate();
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();
        if (uiCamera == null)
            uiCamera = Camera.allCameras.FirstOrDefault(x => x.orthographic);
        if (placeholder == null && transform.childCount > 0)
            placeholder = transform.GetChild(0).GetComponent<RectTransform>();

        // Legacy prefabs: "layoutName" (ad_native_layout, ad_banner_layout...) was an AAR layout; only zen_* / custom
        // Android layouts exist now.
        if (layoutStyle != AdZativeLayoutStyle.Custom && !string.IsNullOrEmpty(customLayout) && !customLayout.StartsWith("zen_"))
            customLayout = "";
        // Legacy prefabs: forceImage = true -> VideoMode.Block.
        if (forceImage)
        {
            videoMode = AdZativeVideoMode.Block;
            forceImage = false;
        }

        OnRemoveAdChanged(false);
        UpdateConfig();
    }

    protected void UpdateConfig()
    {
        if (type != AdType.Banner)
            return;

        if (config != null)
        {
            if (config.adBannerReload > 0)
                timeAutoReload = config.adBannerReload;

            _scaleFactor = canvas.scaleFactor;
            offsetTopDp = config.adBannerOffset;
            offsetTop = (int)DpToCanvasUnits(offsetTopDp);
            size = (int)DpToCanvasUnits(sizeDp);
            sizeRequest = (int)DpToCanvasUnits(sizeRequestDp);
            RectTransform rectTransform = transform.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(0, sizeRequest);
            rectTransform.anchoredPosition = new Vector2(0, -(sizeRequest - size - offsetTop));
            Log($"UpdateConfig offsetTop {offsetTopDp}->{offsetTop}, size {sizeDp}->{size}, sizeRequest {sizeRequestDp}->{sizeRequest}");
        }

        if (backgroundRect == null && background != null)
            backgroundRect = background.GetComponent<RectTransform>();

        if (backgroundRect != null)
        {
            backgroundRect.sizeDelta = new Vector2(0, size);
            backgroundRect.anchoredPosition = new Vector2(0, -offsetTop);
        }

        foreach (RectTransform rectTransform in rootRectTransform)
        {
            if (rectTransform != null)
                rectTransform.offsetMin = new Vector2(0, size);
        }
    }

    private void OnEnable()
    {
        GameStateManager.OnGameStateChanged += OnGameStateChanged;
        UserDataBase.OnVIPChanged += OnRemoveAdChanged;
        UserDataBase.OnRemovedAdsChanged += OnRemoveAdChanged;
        OnAppOpenStateChanged += OnAppOpenStateChangedHandler;
        if (userData != null)
            OnRemoveAdChanged(userData.isVIP || userData.isRemovedAds);
    }

    private void OnDisable()
    {
        GameStateManager.OnGameStateChanged -= OnGameStateChanged;
        UserDataBase.OnVIPChanged -= OnRemoveAdChanged;
        UserDataBase.OnRemovedAdsChanged -= OnRemoveAdChanged;
        OnAppOpenStateChanged -= OnAppOpenStateChangedHandler;
    }

    protected override void OnDestroy()
    {
        if (subscribed)
        {
            AdZativeSDK.OnEvent -= OnPluginEvent;
            AdZativeSDK.OnPaidRaw -= OnPluginPaidRaw;
            AdZativeSDK.OnAdReady -= OnPluginAdReady;
            AdZativeSDK.OnVideoGate -= OnPluginVideoGate;
            subscribed = false;
        }
        base.OnDestroy();
    }

    private void OnRemoveAdChanged(bool isVip)
    {
        if (isVip)
        {
            if (type == AdType.Banner)
            {
                foreach (RectTransform rectTransform in rootRectTransform)
                    if (rectTransform != null)
                        rectTransform.offsetMin = Vector2.zero;
            }
            StopAllCoroutines();
            DestroyAd();
        }
        else if (type == AdType.Banner)
        {
            foreach (RectTransform rectTransform in rootRectTransform)
                if (rectTransform != null)
                    rectTransform.offsetMin = new Vector2(0, size);
        }
        gameObject.SetActive(!isVip);
    }

    private void OnApplicationPause(bool pause)
    {
        if (!pause)
            OnGameStateChanged(GameStateManager.CurrentState, GameState.Other, null);
    }

    private void OnGameStateChanged(GameState current, GameState last, object data = null)
    {
        item = current.ToString();
        if (isInitialized && type == AdType.Banner
            && (state == AdState.None || state == AdState.LoadNotAvailable || state == AdState.ShowFailed || state == AdState.ShowNotAvailable)
            && (current == GameState.Main || current == GameState.Play || current == GameState.Resume
            || current == GameState.Lose || current == GameState.Win))
        {
            ReloadAd();
        }
    }

    private void OnAppOpenStateChangedHandler(AdState state)
    {
        if (type != AdType.Banner && type != AdType.Mrec)
            return;

        // Hidden by the game (Hide(): a popup is open): keep the ad hidden through the app open cycle. Destroying it
        // cleared the flag, and the reload afterwards popped the ad back up over the popup (native views draw above
        // Unity UI). The game's Resume() / show brings it back.
        if (inlineHidden)
        {
            Log($"App open {state} -> ad hidden by the game: kept hidden.");
            return;
        }

        if (state == AdState.LoadRequest || state == AdState.Show)
        {
            Log("App open showing -> Destroy banner to save memory and avoid rendering conflicts.");
            DestroyAd();
        }
        else if (state == AdState.ShowFailed || state == AdState.Close
            || state == AdState.LoadNotAvailable || state == AdState.LoadTimeOut || state == AdState.ShowNotAvailable)
        {
            // Also when the app open ad never showed: the banner was destroyed on its LoadRequest.
            Log("App open closed / not shown -> Request fresh banner load.");
            ReloadAd();
        }
    }

    /// <summary>The ad area can move (banner offset from Remote Config, layout / orientation changes): keep the ad on it.</summary>
    private void LateUpdate()
    {
        if (!inlineVisible || AdArea == null || ++rectFrame % 5 != 0)
            return;
        if (TryGetNormalizedRect(out var r) && !Approximately(r, lastRect))
        {
            lastRect = r;
            AdZativeSDK.UpdateRect(placementName, r);
        }
    }
    #endregion

    #region AdBase flow
    public override IEnumerator IELoadShowAd(Action<AdType, AdState> result, string item, float timeOut = 6.8F, ShowLoadingMode showLoadingMode = ShowLoadingMode.None)
    {
        // A hidden banner / MREC (popup closed): show the kept ad again instead of a new waterfall.
        if (IsInline && inlineHidden && IsSupportedPlatform)
        {
            if (slotRegistered)
            {
                Resume();
                result?.Invoke(type, AdState.ShowSuccess);
                yield break;
            }
            inlineHidden = false;   // nothing kept (hidden before an ad was shown): an explicit show loads normally
        }
        yield return base.IELoadShowAd(result, item, timeOut, showLoadingMode);
    }

    public override IEnumerator IELoadAd(Action<AdType, AdState> result, string itemName, float timeOut = 6.8f, ShowLoadingMode showLoadingMode = ShowLoadingMode.None)
    {
        if (!IsSupportedPlatform && !IsEditor)
        {
            // iOS / other platforms: no native engine yet. Answer at once so AdFlow "Both" falls back to AdMob.
            result?.Invoke(type, AdState.LoadNotAvailable);
            yield break;
        }

        if (!isCanLoad)
            yield break;

        yield return base.IELoadAd(result, itemName, timeOut, showLoadingMode);
        yield return IELoadAdWaterfall(LoadAd, result, itemName, timeOut);
    }

    /// <summary>One waterfall step: a cached ad answers at once, otherwise the plugin loads one ad from <see cref="id"/>.</summary>
    protected override void LoadAd()
    {
        base.LoadAd();

        if (!ADMOB.IsInitialized)
        {
            state = AdState.LoadNotAvailable;
            isStillLoading = false;
            LogError("Aborted loadAd: MobileAds.initialize() has not finished completing. Blocking Next-Gen SDK crash.");
            return;
        }

        if (string.IsNullOrEmpty(id))
            id = idList.FirstOrDefault();

        if (string.IsNullOrEmpty(id))
        {
            state = AdState.Exception;
            LogError($"LoadAd ID [{id}] is null or empty.");
            return;
        }

        if (IsEditor)
        {
            DOVirtual.DelayedCall(1.5f, () =>
            {
                state = AdState.LoadAvailable;
                isCanShow = true;
            });
            return;
        }

        if (!IsSupportedPlatform)
        {
            OnAdLoadError("Native ads are not supported on this platform");
            return;
        }

        // Test mode: AdmobBase.GetIdTest picks the image or the video native test ID at random; a placement that can't
        // show video (banner) would then fail every other step. Use the image test ID for it.
        if (AdsManager.UseIdTest && EffectiveVideoMode == AdZativeVideoMode.Block)
            id = loadingId = TestIdImage;

        EnsureSlot();
        if (AdZativeSDK.HasReadyAd(placementName))
        {
            // Preloaded by the plugin: READY before the opportunity.
            swapPending = inlineVisible;
            sourceId = "";
            sourceName = NextAdInfo?.Network ?? "";
            state = AdState.LoadAvailable;
            if (showOnLoaded)
                StartCoroutine(IEShowAd());
            return;
        }
        AdZativeSDK.LoadUnit(placementName, id);
    }

    public override IEnumerator IEShowAd(Action<AdType, AdState> result, string itemName, float delayShow = 0.25f)
    {
        yield return IEShowAd(IEShowAd(), result, itemName, delayShow);
    }

    public IEnumerator IEShowAd()
    {
        yield return new WaitForEndOfFrame();
        if (IsInline)
        {
            while (Time.timeScale == 0)
                yield return null;
        }
        ShowAd();
    }

    /// <summary>Shows the loaded ad (full-screen timings: <see cref="Timing"/>).</summary>
    public void ShowAd()
    {
        // Full-screen: every exit must end the show (OnAdShowFailed resumes the game AdmobBase paused for it).
        if (!isCanShow)
        {
            if (IsFullscreenType)
                OnAdShowFailed("not_ready");
            return;
        }

        // Only inline slots map a Canvas rect through the UI camera; full-screen ads don't need one.
        if (IsInline && !IsHasCamera)
        {
            LogWarning($"ShowAd Failed: {state} IsReady: {isCanShow} isHasCamera: false");
            OnAdShowFailed("no_ui_camera");
            return;
        }

        if (IsEditor)
        {
            state = AdState.ShowRequest;
            state = AdState.Show;
            DOVirtual.DelayedCall(1.0f, () => state = AdState.ShowSuccess);
            DOVirtual.DelayedCall(2.0f, () =>
            {
                state = AdState.Close;
                base.DestroyAd();
            });
            return;
        }

        if (!IsSupportedPlatform)
        {
            OnAdShowFailed("platform_not_supported");
            return;
        }

        EnsureSlot();
        if (IsFullscreenType)
        {
            if (AdZativeSDK.IsFullscreenShowing)
            {
                // The plugin refuses silently here (no "show_failed"): fail the show so PauseApp / callbacks never hang.
                OnAdShowFailed("another_fullscreen_showing");
                return;
            }
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                state = AdState.Show;
                PauseApp(true, "ShowAd");
            });

            switch (type)
            {
                case AdType.AppOpen: AdZativeSDK.ShowAppOpen(placementName); break;
                case AdType.Reward: AdZativeSDK.ShowRewarded(placementName, _ => { }, podSize); break;
                default: AdZativeSDK.ShowInterstitial(placementName, null, podSize); break;
            }
        }
        else if (inlineHidden)
        {
            // Hidden by the game (popup): never pop the ad back up from a reload; Resume() shows the newer ad.
            swapPending = true;
        }
        else if (inlineVisible)
        {
            // Reload chain: swap to the cached / just loaded ad without a blank frame. A second show of the SAME load
            // (showOnLoaded + LoadShow) must not replace the ad that has just appeared (it would waste it).
            if (swapPending)
            {
                swapPending = false;
                AdZativeSDK.ReplaceAd(placementName, EffectiveVideoMode != AdZativeVideoMode.Block);
            }
        }
        else if (TryGetNormalizedRect(out lastRect, true))
        {
            AdZativeSDK.ShowInRect(placementName, lastRect);
            inlineVisible = true;
        }
        else
        {
            OnAdShowFailed("ad_area_invalid");
        }
    }

    public override void DestroyAd()
    {
        try
        {
#if USE_ADMOB
            if (!UnityMainThreadDispatcher.IsExist)
                return;

            UnityMainThreadDispatcher.Enqueue(() =>
            {
                if (!IsSupportedPlatform)
                    return;

                // Inline: remove the view (the slot and its cache are rebuilt on the next load). Full-screen: nothing is
                // on screen; a cached ad stays for the next show (no wasted request).
                if (IsInline && slotRegistered)
                {
                    AdZativeSDK.Destroy(placementName);
                    slotRegistered = false;
                }
                inlineVisible = false;
                inlineHidden = false;
                swapPending = false;
                HandleAdClosed();
            });
#endif
        }
        catch (Exception ex)
        {
            LogError($"DestroyAd Exception: {ex.Message}. StackTrace: {ex.StackTrace}");
        }
    }
    #endregion

    #region Public controls (A/B, test panel, game UI)
    /// <summary>Hides an inline ad and keeps it for an instant re-show (e.g. a popup covers the banner). See Resume().</summary>
    public void Hide()
    {
        if (!IsSupportedPlatform || !IsInline) return;
        // Also without a slot (destroyed for an app open, still loading): a later load must not show the ad over the
        // game's popup until Resume().
        if (slotRegistered)
            AdZativeSDK.Hide(placementName);
        inlineVisible = false;
        inlineHidden = true;
    }

    /// <summary>Shows a hidden inline ad again (after Hide); an ad loaded meanwhile replaces it at once.</summary>
    public void Resume()
    {
        if (!IsSupportedPlatform || !inlineHidden) return;
        if (!slotRegistered)
        {
            // Hidden before any ad was on screen: nothing to re-show; the next load shows normally.
            inlineHidden = false;
            swapPending = false;
            if (isCanShow && isActiveAndEnabled)
                StartCoroutine(IEShowAd());
            return;
        }
        if (!TryGetNormalizedRect(out lastRect, true)) return;
        inlineHidden = false;
        AdZativeSDK.ShowInRect(placementName, lastRect);
        inlineVisible = true;
        if (swapPending)
        {
            swapPending = false;
            AdZativeSDK.ReplaceAd(placementName, EffectiveVideoMode != AdZativeVideoMode.Block);
        }
    }

    /// <summary>Built-in style (see LayoutStyleCount) or Custom (customLayoutName). Applies to the next ad shown.</summary>
    public void SetLayout(AdZativeLayoutStyle style, string customLayoutName = null)
    {
        layoutStyle = style;
        if (style == AdZativeLayoutStyle.Custom && !string.IsNullOrEmpty(customLayoutName))
            customLayout = customLayoutName;
        if (IsSupportedPlatform && slotRegistered)
            AdZativeSDK.SetLayout(placementName, style, customLayout);
    }

    public void SetLayoutMode(AdZativeLayoutMode mode)
    {
        layoutMode = mode;
        if (IsSupportedPlatform && slotRegistered && IsInline)
            AdZativeSDK.SetLayoutMode(placementName, mode);
        else
            slotRegistered = slotRegistered && IsInline;   // full-screen: re-register with the new mode on next load
    }

    public void SetVideoMode(AdZativeVideoMode mode)
    {
        forceImage = false;
        videoMode = mode;
        if (IsSupportedPlatform && slotRegistered && IsInline)
            AdZativeSDK.SetVideoMode(placementName, mode);
    }

    /// <summary>Inline: swap to the next cached ad now (allowVideo = false keeps a banner strip image-only).</summary>
    public void ReplaceAd(bool allowVideo = false)
    {
        if (IsSupportedPlatform && slotRegistered && IsInline)
            AdZativeSDK.ReplaceAd(placementName, allowVideo);
    }

    /// <summary>Gate mode: show the held video ad (after making room, >= 120x120dp media).</summary>
    public void AcceptVideo() { if (IsSupportedPlatform && slotRegistered) AdZativeSDK.AcceptVideo(placementName); }

    /// <summary>Gate mode: drop the held video ad and show the next non-video one.</summary>
    public void RejectVideo() { if (IsSupportedPlatform && slotRegistered) AdZativeSDK.RejectVideo(placementName); }

    /// <summary>Interstitial / rewarded pods: ads per show (1..3, one per ad unit ID).</summary>
    public int PodSize
    {
        get => podSize;
        set
        {
            podSize = Mathf.Clamp(value, 1, 3);
            slotRegistered = slotRegistered && IsInline;   // full-screen: re-register so every pod ID stays warm
        }
    }

    /// <summary>Full-screen timing of this placement (prefab values, or Remote Config after ApplyRemoteConfig).</summary>
    public AdZativeTiming Timing => timing;

    [Serializable]
    private class RemoteOverride { public int podSize = -1; public int layoutStyle = -1; }

    /// <summary>
    /// A/B test: Remote Config value of the key named like this placement (e.g. "AdZative_Inter_Default"), a JSON object
    /// with any of podSize, layoutStyle and the AdZativeTiming fields, e.g.
    /// {"podSize":2,"imageFirstSec":10,"imageNextSec":5,"closeAfterSec":5,"videoMaxSec":30,"podRatio":2}.
    /// Missing fields keep the prefab values. Applies from the next show.
    /// </summary>
    public void ApplyRemoteConfig(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            JsonUtility.FromJsonOverwrite(json, timing);
            var o = JsonUtility.FromJson<RemoteOverride>(json);
            if (o.podSize > 0) podSize = Mathf.Clamp(o.podSize, 1, 3);
            if (o.layoutStyle >= 0) layoutStyle = (AdZativeLayoutStyle)o.layoutStyle;
            if (IsFullscreenType) slotRegistered = false;   // re-registered with the new values on the next load
            else if (IsSupportedPlatform && slotRegistered) AdZativeSDK.SetLayout(placementName, layoutStyle, customLayout);
            Log($"Remote config applied: {json}");
        }
        catch (Exception ex)
        {
            LogError($"Remote config {placementName} ignored: {ex.Message}");
        }
    }

    /// <summary>What a (pod) show would use right now - for a "Watch N ads (~T s)?" prompt before a rewarded pod.</summary>
    public AdZativePodInfo GetPodInfo(int size = 0) => IsSupportedPlatform && slotRegistered ? AdZativeSDK.GetPodInfo(placementName, size) : null;

    /// <summary>No effect (kept for API compatibility): the close control follows Timing.</summary>
    [Obsolete("No effect with the AdZativeSDK engine.")]
    public void CheckCtrMetaToEnableClick() { }

    /// <summary>No effect (kept for API compatibility).</summary>
    [Obsolete("No effect with the AdZativeSDK engine.")]
    public void SetEnableCloseAd(bool value) { }
    #endregion

    #region Plugin setup
    private IEnumerator DOInitializePlugin()
    {
        if (IsEditor)
        {
            isInitialized = true;
            yield break;
        }

        if (!IsSupportedPlatform)
            yield break;

        // Base owns UMP + MobileAds.Initialize: start the plugin only after it.
        while (!ADMOB.IsInitialized)
            yield return null;

        if (!AdZativeSDK.IsInitialized)
        {
            var settings = ScriptableObject.CreateInstance<AdZativeSettings>();
            settings.externalSdkInit = true;               // never calls MobileAds.initialize itself
            settings.useTestAdUnits = false;               // Base owns the IDs (AdsManager.UseIdTest)
            settings.duckAudioDuringFullscreen = false;    // Base owns audio / pause (PauseApp)
            settings.pauseTimeScaleDuringFullscreen = false;
            settings.verboseLogging = DebugMode.IsOn;
            settings.slots.Clear();
            AdZativeSDK.Initialize(settings);
        }

        slotId = placementName;
        if (!subscribed)
        {
            AdZativeSDK.OnEvent += OnPluginEvent;
            AdZativeSDK.OnPaidRaw += OnPluginPaidRaw;
            AdZativeSDK.OnAdReady += OnPluginAdReady;
            AdZativeSDK.OnVideoGate += OnPluginVideoGate;
            subscribed = true;
        }

        yield return new WaitForEndOfFrame();

        UpdateConfig();
        isInitialized = true;

        if (loadOnStart)
            ReloadAd();
    }

    private AdZativeFormat PluginFormat
    {
        get
        {
            switch (type)
            {
                case AdType.Banner: return AdZativeFormat.BANNER;
                case AdType.AppOpen: return AdZativeFormat.APP_OPEN;
                case AdType.Reward: return AdZativeFormat.REWARDED;
                case AdType.Inter:
                case AdType.RewardInter: return AdZativeFormat.INTERSTITIAL;
                default: return AdZativeFormat.MREC;
            }
        }
    }

    /// <summary>
    /// Ad unit IDs of the slot, in waterfall order. Test mode (AdsManager.UseIdTest): Google's native test IDs only -
    /// never a production ID, also for the plugin's preloading.
    /// </summary>
    private const string TestIdImage = "ca-app-pub-3940256099942544/2247696110";   // Google native (image) test ID
    private const string TestIdVideo = "ca-app-pub-3940256099942544/1044960115";   // Google native video test ID

    /// <summary>Image test ID: every native placement can show it (an inline slot blocks video).</summary>
    protected override string FallbackTestId =>
        Application.platform == RuntimePlatform.IPhonePlayer ? "ca-app-pub-3940256099942544/3986624511" : TestIdImage;

    private List<string> SlotAdUnitIds()
    {
        if (AdsManager.UseIdTest)
        {
            return EffectiveVideoMode == AdZativeVideoMode.Block ? new List<string> { TestIdImage } : new List<string> { TestIdImage, TestIdVideo };
        }
        var ids = idList.Where(x => !string.IsNullOrEmpty(x)).ToList();
        if (ids.Count == 0 && !string.IsNullOrEmpty(id)) ids.Add(id);
        return ids;
    }

    private void EnsureSlot()
    {
        if (slotRegistered) return;
        var cfg = new AdZativeSlotConfig
        {
            slotId = placementName,
            format = PluginFormat,
            adUnitIds = SlotAdUnitIds(),
            sourcePolicy = AdZativeSourcePolicy.Fallback,   // = waterfall: an ID is used only while the previous ones fail
            manualLoad = !Preloads,                    // app open: loads only when a show is requested
            preloadWhenHidden = Preloads,
            cacheSize = 1,
            refreshSec = 0,                            // refresh = AdBase reload chain (timeAutoReload / adBannerReload)
            layoutStyle = layoutStyle,
            customLayout = customLayout,
            layoutMode = layoutMode,
            videoMode = IsInline ? EffectiveVideoMode : AdZativeVideoMode.Allow,
            mediaAspectRatio = desiredAspectRatio.ToString(),
            mediaScale = desiredScaleType == ImageScaleType.CENTER_CROP ? AdZativeMediaScale.Crop : AdZativeMediaScale.Fit,
            timing = timing.Clone(),
            podSize = type == AdType.Inter || type == AdType.RewardInter || type == AdType.Reward ? podSize : 1,
        };
        AdZativeSDK.RegisterSlot(cfg);
        slotRegistered = true;
    }
    #endregion

    #region Ad area (Canvas rect)
    /// <summary>
    /// Screen area of the ad. Banner: the reserved strip (backgroundRect), because the banner placeholder covers the
    /// whole screen. MREC: the placeholder.
    /// </summary>
    private RectTransform AdArea => type == AdType.Banner && backgroundRect != null ? backgroundRect : placeholder;

    /// <summary>AdArea -> normalised screen rect (0..1, origin top-left) through this ad's UI camera (null for Overlay).</summary>
    private bool TryGetNormalizedRect(out Rect result, bool log = false)
    {
        result = default;
        var area = AdArea;
        if (area == null || canvas == null || Screen.width <= 0 || Screen.height <= 0)
            return false;

        area.GetWorldCorners(_cachedCorners);
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : (uiCamera != null ? uiCamera : canvas.worldCamera);
        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(cam, _cachedCorners[0]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(cam, _cachedCorners[2]);

        float x = Mathf.Clamp01(bottomLeft.x / Screen.width);
        float y = Mathf.Clamp01((Screen.height - topRight.y) / Screen.height);
        float w = Mathf.Clamp01((topRight.x - bottomLeft.x) / Screen.width);
        float h = Mathf.Clamp01((topRight.y - bottomLeft.y) / Screen.height);
        result = new Rect(x, y, w, h);
        if (log && DebugMode.IsOn)
            Log($"Ad area {area.name}: screen {Screen.width}x{Screen.height} px({bottomLeft.x:0},{Screen.height - topRight.y:0},{topRight.x - bottomLeft.x:0},{topRight.y - bottomLeft.y:0}) -> normalized {result}");
        return w > 0f && h > 0f;
    }

    private static bool Approximately(Rect a, Rect b)
    {
        const float e = 0.001f;
        return Mathf.Abs(a.x - b.x) < e && Mathf.Abs(a.y - b.y) < e && Mathf.Abs(a.width - b.width) < e && Mathf.Abs(a.height - b.height) < e;
    }

    private bool IsHasCamera
    {
        get
        {
            if (canvas == null)
                return false;
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return true;
            if (uiCamera == null || canvas.worldCamera == null)
                uiCamera = canvas.worldCamera = Camera.allCameras.FirstOrDefault(x => x.orthographic);
            if (uiCamera == null || canvas.worldCamera == null)
            {
                LogWarning("No UI Camera found. Please assign a Camera to the 'uiCamera' field.");
                return false;
            }
            return true;
        }
    }
    #endregion

    #region Unit Conversion
    /// <summary>
    /// Converts a value from DP (Density-independent Pixels) to Unity Canvas Units.
    /// Designed for Canvas Render Mode: Screen Space - Camera.
    /// </summary>
    public float DpToCanvasUnits(float dp)
    {
        float targetPixels = dp * physicDpi;

        if (Mathf.Approximately(_scaleFactor, 0f))
            _scaleFactor = 1f;

#if UNITY_EDITOR
        _scaleFactor = 0.5f; // Simulate a scale factor for editor testing
#endif
        float unit = targetPixels / _scaleFactor;
        Log($"DpToCanvasUnits: dp: {dp} -> {targetPixels} physicalPixels: {physicDpi} -> scaleFactor: {_scaleFactor} -> canvasUnits: {unit}");
        return unit;
    }

    public float physicDpi
    {
        get
        {
            float dpi = Screen.dpi > 0 ? Screen.dpi : 160f;
#if UNITY_EDITOR
            dpi = 160f;
#endif
            return dpi / 160f;
        }
    }
    #endregion

    #region Plugin events
    private void HandleAdImpression()
    {
        isCanShow = false;
        base.OnSuccess();

        if (IsInline && timeAutoReload > 0)
            UnityMainThreadDispatcher.Enqueue(() => ReloadAd(Mathf.Max(MinInlineReloadSec, timeAutoReload)));
    }

    private void HandleAdClosed()
    {
        state = AdState.Close;
        base.DestroyAd();
    }

    private void OnPluginAdReady(string slot, AdZativeAdInfo info)
    {
        if (slot == placementName)
            NextAdInfo = info;
    }

    private void OnPluginVideoGate(string slot, AdZativeAdInfo info)
    {
        if (slot != placementName) return;
        if (OnVideoGate != null) OnVideoGate(this, info);
        else RejectVideo();   // nobody makes room: keep the placement image-only
    }

    // Main thread (AdZativeSDK dispatches on Update).
    private void OnPluginEvent(AdZativeEvent e)
    {
        if (e.SlotId != placementName)
            return;
        var p = e.Payload;

        switch (e.Name)
        {
            // Answer to LoadUnit for THIS slot (pools are shared by ad unit): only the waiting waterfall step reacts.
            case "unit_loaded":
                if (state != AdState.LoadRequest || p == null || p.ad_unit != loadingId)
                    break;
                if (EffectiveVideoMode == AdZativeVideoMode.Block && p.has_video)
                {
                    OnAdLoadError("video ad (VideoMode Block)");
                    break;
                }
                swapPending = inlineVisible;
                sourceId = "";
                sourceName = p.network;
                state = AdState.LoadAvailable;
                if (showOnLoaded)
                    StartCoroutine(IEShowAd());
                break;
            case "unit_failed":
                if (state == AdState.LoadRequest && p != null && p.ad_unit == loadingId)
                    OnAdLoadError(string.IsNullOrEmpty(p.message) ? $"code {p.code}" : p.message);
                break;
            case "shown":
            case "replaced":
            case "refreshed":
                if (p != null && !string.IsNullOrEmpty(p.network))
                    sourceName = p.network;
                if (IsInline && (state == AdState.ShowRequest || state == AdState.LoadAvailable))
                    state = AdState.Show;
                break;
            case "impression":
                // Rewarded: success = reward earned, never the impression.
                if (type != AdType.Reward)
                    HandleAdImpression();
                break;
            case "reward_earned":
                if (type == AdType.Reward)
                    HandleAdImpression();
                break;
            case "clicked":
                state = AdState.Click;
                break;
            case "closed":
                HandleAdClosed();
                break;
            case "show_failed":
                if (state == AdState.ShowRequest || state == AdState.Show || state == AdState.LoadAvailable)
                {
                    inlineVisible = false;
                    OnAdShowFailed(string.IsNullOrEmpty(p?.reason) ? "show_failed" : p.reason);
                }
                break;
        }
    }

    // Android callback thread: forward revenue at once (no Unity API here; LogImpressionData is thread-safe).
    private void OnPluginPaidRaw(string sid, string json)
    {
        if (sid != slotId)
            return;
#if USE_ADMOB
        try
        {
            var p = JsonUtility.FromJson<AdZativePayload>(json);
            if (!string.IsNullOrEmpty(p.network))
                sourceName = p.network;
            var value = new AdValue { Value = p.value_micros, CurrencyCode = p.currency, Precision = ParsePrecision(p.precision) };
            LogImpressionData(value);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
#endif
    }

#if USE_ADMOB
    private static AdValue.PrecisionType ParsePrecision(string s)
    {
        if (!string.IsNullOrEmpty(s) && Enum.TryParse(s.Replace("_", ""), true, out AdValue.PrecisionType t))
            return t;
        return AdValue.PrecisionType.Unknown;
    }
#endif
    #endregion
}
