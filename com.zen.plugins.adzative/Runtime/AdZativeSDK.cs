using System;
using System.Collections.Generic;
using UnityEngine;

namespace Base.Ads
{
    /// <summary>
    /// Unified Unity API for Zen native ads (Android today; iOS plugs in behind the same bridge).
    ///
    /// Model: a SLOT is one display position (Canvas rect or anchor) with its own layout and a LIST of
    /// ad unit IDs. Slots draw ads from shared per-ID caches, so the next ad is always preloaded — from the
    /// same ID or another one, depending on <see cref="AdZativeSourcePolicy"/>.
    ///
    /// <code>
    /// AdZativeSDK.Initialize();
    /// // Canvas: add AdZativeSlot to a RectTransform (easiest), or from code:
    /// AdZativeSDK.RegisterSlot(new AdZativeSlotConfig { slotId = "shop_mrec", format = AdZativeFormat.MREC,
    ///     adUnitIds = { "ca-app-pub-x/high_floor", "ca-app-pub-x/default" }, layoutStyle = AdZativeLayoutStyle.Style2 });
    /// AdZativeSDK.ShowInRect("shop_mrec", myRectTransform);
    /// AdZativeSDK.ShowRewarded("revive", ok => { if (ok) Revive(); });
    /// </code>
    ///
    /// Ads are Views inside the Unity window: showing/closing never triggers OnApplicationPause/Focus.
    /// Native views always draw ABOVE Unity UI — call Hide(slotId) before opening a popup over the slot.
    /// All callbacks run on Unity's main thread.
    /// </summary>
    public static class AdZativeSDK
    {
        public static event Action<AdZativeEvent> OnEvent;
        /// <summary>(slotId, adUnitId, valueMicros, currency, precision) — impression-level revenue.</summary>
        public static event Action<string, string, long, string, string> OnPaid;
        /// <summary>
        /// (slotId, payloadJson) of every "paid" event, raised on the ANDROID CALLBACK THREAD before the main-thread
        /// hop, so impression-level revenue can be forwarded at once (Google: keep ILAR outside the Update queue).
        /// Handlers must not touch Unity APIs. Payload: ad_unit, value_micros, currency, precision, network.
        /// </summary>
        public static event Action<string, string> OnPaidRaw;
        /// <summary>Pixel rect (origin top-left) of a visible inline slot as laid out on Android.</summary>
        public static event Action<string, RectInt> OnInlineLayout;
        /// <summary>(slotId, info): the ad this slot will show next is loaded — media type, video length, assets, network.</summary>
        public static event Action<string, AdZativeAdInfo> OnAdReady;
        /// <summary>
        /// (slotId, info): VideoMode.Gate held a VIDEO ad for this inline slot. Make room (>= 120x120dp media,
        /// e.g. switch the slot to a bigger RectTransform) then AcceptVideo(slotId) — or RejectVideo(slotId).
        /// </summary>
        public static event Action<string, AdZativeAdInfo> OnVideoGate;
        public static event Action<string> OnFullscreenOpened;
        public static event Action<string> OnFullscreenClosed;

        public static bool IsInitialized { get; private set; }
        public static bool IsFullscreenShowing { get; private set; }
        public static AdZativeSettings Settings { get; private set; }

        private static IAdZativeBridge _bridge;
        private static readonly Dictionary<string, AdZativeSlotConfig> Slots = new Dictionary<string, AdZativeSlotConfig>();
        private static readonly Dictionary<string, Action<bool>> FullscreenCallbacks = new Dictionary<string, Action<bool>>();
        private static readonly HashSet<string> RewardEarned = new HashSet<string>();
        private static readonly Vector3[] Corners = new Vector3[4];

        private static float _savedVolume = 1f;
        private static float _savedTimeScale = 1f;

        // ------------------------------------------------------------------ setup

        public static void Initialize(AdZativeSettings settings = null)
        {
            if (IsInitialized) return;
            Settings = settings != null ? settings : Resources.Load<AdZativeSettings>(AdZativeSettings.ResourcePath);
            if (Settings == null)
            {
                Debug.LogWarning("[AdZativeSDK] No settings asset found, using defaults with Google test IDs.");
                Settings = ScriptableObject.CreateInstance<AdZativeSettings>();
            }

            AdZativeMainThread.Ensure();
#if UNITY_ANDROID && !UNITY_EDITOR
            _bridge = new AndroidAdZativeBridge();
#else
            _bridge = new EditorAdZativeBridge();
#endif
            Action<string, string, string> onEvent = (sid, evt, json) =>
            {
                if (evt == "paid")
                {
                    try { OnPaidRaw?.Invoke(sid, json); }
                    catch (Exception ex) { Debug.LogException(ex); }
                }
                AdZativeMainThread.Enqueue(() => Dispatch(new AdZativeEvent(sid, evt, json)));
            };
            if (Settings.externalSdkInit) _bridge.InitializeExternal(onEvent);
            else _bridge.Initialize(Settings.androidAppId, onEvent);
            _bridge.SetMaxCachedAds(Settings.maxCachedAds);
            IsInitialized = true;

            foreach (var s in Settings.slots) RegisterSlot(s);
        }

        /// <summary>Adds or replaces a slot (also at runtime, e.g. from Remote Config for A/B tests).</summary>
        public static void RegisterSlot(AdZativeSlotConfig config)
        {
            EnsureInit();
            var c = config.Clone();
            if (Settings.useTestAdUnits)
            {
                // Two distinct test IDs so multi-ID policies can be exercised on device.
                int n = Mathf.Max(1, c.adUnitIds.Count);
                c.adUnitIds.Clear();
                bool video = c.format != AdZativeFormat.BANNER;
                for (int i = 0; i < n; i++)
                    c.adUnitIds.Add((i % 2 == 0) == video ? AdZativeTestIds.NativeVideo : AdZativeTestIds.Native);
            }
            Slots[c.slotId] = c;
            _bridge.ConfigureSlot(c.ToJson());
        }

        public static bool HasSlot(string slotId) => Slots.ContainsKey(slotId);

        /// <summary>
        /// manualLoad slots: load one ad from {adUnitId} now (a step of the host's own waterfall). The answer is a SLOT
        /// event "unit_loaded" / "unit_failed" (payload.ad_unit == adUnitId; code / message on failure). Pools are
        /// shared by ad unit, so never use the pool events ("loaded" with an empty slot id) for this. A cached ad
        /// answers at once. Unknown IDs get a pool on the fly.
        /// </summary>
        public static void LoadUnit(string slotId, string adUnitId) { if (IsInitialized) _bridge.LoadUnit(slotId, adUnitId); }
        public static bool IsReady(string slotId) => IsInitialized && _bridge.IsReady(slotId);
        /// <summary>A cached ad the slot can show NEXT (inline: not counting the ad on screen) - e.g. before a swap.</summary>
        public static bool HasReadyAd(string slotId) => IsInitialized && _bridge.HasReadyAd(slotId);

        // ------------------------------------------------------------------ inline: Canvas

        /// <summary>Show an inline slot stretched over a RectTransform (Canvas Overlay / Camera / World).</summary>
        public static void ShowInRect(string slotId, RectTransform rect)
        {
            if (TryGetNormalizedRect(rect, out var r)) ShowInRect(slotId, r);
        }

        /// <summary>Normalised screen rect, origin TOP-LEFT, 0..1.</summary>
        public static void ShowInRect(string slotId, Rect normalizedTopLeft)
        {
            EnsureInit();
            _bridge.ShowSlotInRect(slotId, normalizedTopLeft.x, normalizedTopLeft.y, normalizedTopLeft.width, normalizedTopLeft.height);
        }

        public static void UpdateRect(string slotId, Rect normalizedTopLeft)
        {
            if (!IsInitialized) return;
            _bridge.UpdateSlotRect(slotId, normalizedTopLeft.x, normalizedTopLeft.y, normalizedTopLeft.width, normalizedTopLeft.height);
        }

        /// <summary>
        /// RectTransform → normalised screen rect (origin top-left). Uses the canvas camera for
        /// Screen Space - Camera / World Space and null for Overlay. Rotated rects use their bounding box.
        /// </summary>
        public static bool TryGetNormalizedRect(RectTransform rt, out Rect result)
        {
            result = default;
            if (rt == null || Screen.width <= 0 || Screen.height <= 0) return false;
            var canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = null;
            if (canvas != null)
            {
                canvas = canvas.rootCanvas;
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            }
            rt.GetWorldCorners(Corners);
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, Corners[i]);
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
            }
            float sw = Screen.width, sh = Screen.height;
            // Unity screen space: origin bottom-left, pixels → Android: origin top-left, normalised.
            float x0 = Mathf.Clamp01(minX / sw), x1 = Mathf.Clamp01(maxX / sw);
            float y0 = Mathf.Clamp01(1f - maxY / sh), y1 = Mathf.Clamp01(1f - minY / sh);
            if (x1 - x0 <= 0f || y1 - y0 <= 0f) return false;
            result = new Rect(x0, y0, x1 - x0, y1 - y0);
            return true;
        }

        // ------------------------------------------------------------------ inline: anchors

        /// <param name="sizeDp">(0,0) = default size: banner 64dp full width, MREC 300x250dp.</param>
        public static void ShowBanner(string slotId, AdZativePosition position = AdZativePosition.Bottom, Vector2 sizeDp = default, Vector2 offsetDp = default)
        {
            EnsureInit();
            _bridge.ShowSlotAtAnchor(slotId, position, offsetDp.x, offsetDp.y, sizeDp.x, sizeDp.y);
        }

        public static void ShowMrec(string slotId, AdZativePosition position = AdZativePosition.Center, Vector2 sizeDp = default, Vector2 offsetDp = default)
            => ShowBanner(slotId, position, sizeDp, offsetDp);

        /// <summary>Switch the layout of a slot at runtime (current ad is re-bound, no new request).</summary>
        public static void SetLayout(string slotId, AdZativeLayoutStyle style, string customLayout = null)
        {
            EnsureInit();
            if (!Slots.TryGetValue(slotId, out var c)) return;
            c.layoutStyle = style;
            if (customLayout != null) c.customLayout = customLayout;
            _bridge.SetSlotLayout(slotId, c.LayoutName);
        }

        // ------------------------------------------------------------------ inline: video / refresh / layout (A/B)

        /// <summary>VideoMode.Gate: the slot has room for video now → show the held video ad.</summary>
        public static void AcceptVideo(string slotId) { if (IsInitialized) _bridge.AcceptVideo(slotId); }

        /// <summary>VideoMode.Gate: destroy the held video ad; the next NON-video ad is shown instead.</summary>
        public static void RejectVideo(string slotId) { if (IsInitialized) _bridge.RejectVideo(slotId); }

        /// <summary>
        /// Destroy the ad on screen and swap in the next ready ad (e.g. the player collapsed an expanded
        /// video banner). allowVideo = false → the replacement is a non-video ad.
        /// </summary>
        public static void ReplaceAd(string slotId, bool allowVideo = false) { if (IsInitialized) _bridge.ReplaceAd(slotId, allowVideo); }

        public static void SetVideoMode(string slotId, AdZativeVideoMode mode)
        {
            if (!IsInitialized) return;
            if (Slots.TryGetValue(slotId, out var c)) c.videoMode = mode;
            _bridge.SetVideoMode(slotId, mode.ToString().ToUpperInvariant());
        }

        /// <summary>Inline refresh interval (0 = off, min 30 s), applied immediately — for refresh-rate A/B tests.</summary>
        public static void SetRefresh(string slotId, int seconds)
        {
            if (!IsInitialized) return;
            if (Slots.TryGetValue(slotId, out var c)) c.refreshSec = seconds;
            _bridge.SetRefreshSec(slotId, seconds);
        }

        /// <summary>Manual / Auto layout for an inline slot (fullscreen slots: set layoutMode in the config).</summary>
        public static void SetLayoutMode(string slotId, AdZativeLayoutMode mode)
        {
            if (!IsInitialized) return;
            if (Slots.TryGetValue(slotId, out var c)) c.layoutMode = mode;
            _bridge.SetLayoutMode(slotId, mode.ToString().ToUpperInvariant());
        }

        /// <summary>Hide (keeps the ad for an instant re-show). Use before a popup covers the slot.</summary>
        public static void Hide(string slotId) { if (IsInitialized) _bridge.HideSlot(slotId); }

        /// <summary>Destroy the slot and release its ad. RegisterSlot again to reuse the id.</summary>
        public static void Destroy(string slotId)
        {
            if (!IsInitialized) return;
            _bridge.DestroySlot(slotId);
            Slots.Remove(slotId);
        }

        // ------------------------------------------------------------------ fullscreen

        /// <param name="onClosed">Invoked exactly once: after close, or right away if nothing is ready.</param>
        /// <param name="podSize">0 = slot config; 2..3 = that many ads in a row ("Ad 1 of N", Next ›), one per ad unit ID.</param>
        public static void ShowInterstitial(string slotId, Action onClosed = null, int podSize = 0)
            => ShowFullscreen(slotId, _ => onClosed?.Invoke(), podSize);
        public static void ShowAppOpen(string slotId, Action onClosed = null) => ShowFullscreen(slotId, _ => onClosed?.Invoke(), 1);
        /// <param name="onFinished">true if the reward was earned (after the LAST ad of a pod). Invoked exactly once.</param>
        /// <param name="podSize">0 = slot config; 2..3 = watch that many ads for one reward. Ask the player first
        /// (GetPodInfo gives the ad count and total seconds for your UI).</param>
        public static void ShowRewarded(string slotId, Action<bool> onFinished, int podSize = 0) => ShowFullscreen(slotId, onFinished, podSize);

        /// <summary>
        /// Which ads a (pod) show would use right now, with video lengths — build the "Watch 2 ads (~45 s)?" prompt
        /// from it, then call ShowRewarded with the same podSize. Ready may be lower than requested.
        /// </summary>
        public static AdZativePodInfo GetPodInfo(string slotId, int podSize = 0)
        {
            if (!IsInitialized) return new AdZativePodInfo { slot = slotId };
            try { return JsonUtility.FromJson<AdZativePodInfo>(_bridge.GetPodInfo(slotId, podSize)) ?? new AdZativePodInfo { slot = slotId }; }
            catch (Exception ex) { Debug.LogException(ex); return new AdZativePodInfo { slot = slotId }; }
        }

        private static void ShowFullscreen(string slotId, Action<bool> onFinished, int podSize)
        {
            EnsureInit();
            if (IsFullscreenShowing) { onFinished?.Invoke(false); return; }
            FullscreenCallbacks[slotId] = onFinished;
            RewardEarned.Remove(slotId);
            _bridge.ShowFullscreen(slotId, podSize);   // "show_failed" / "closed" completes the callback
        }

        public static void CloseFullscreen() { if (IsInitialized) _bridge.CloseFullscreen(); }

        // ------------------------------------------------------------------ app open control

        /// <summary>Call before IAP / share / permission flows so returning doesn't pop an app-open ad.</summary>
        public static void SuppressNextAppOpen() { if (IsInitialized) _bridge.SuppressNextForeground(); }
        public static void SetAppOpenSuppressed(bool suppressed) { if (IsInitialized) _bridge.SetAppOpenSuppressed(suppressed); }

        public static string DumpState() => IsInitialized ? _bridge.DumpState() : "{}";

        internal static void HandleBackKey()
        {
            if (IsFullscreenShowing && Settings.routeBackKeyToAd) _bridge.HandleBack();
        }

        // ------------------------------------------------------------------ dispatch (main thread)

        private static void Dispatch(AdZativeEvent e)
        {
            if (Settings.verboseLogging) Debug.Log("[AdZativeSDK] " + e);
            AdZativeAnalytics.Track(e);
            var p = e.Payload;

            switch (e.Name)
            {
                case "paid":
                    OnPaid?.Invoke(e.SlotId, p.ad_unit, p.value_micros, p.currency, p.precision);
                    break;
                case "layout":
                    OnInlineLayout?.Invoke(e.SlotId, new RectInt(p.x, p.y, p.width, p.height));
                    break;
                case "fullscreen_opened":
                    IsFullscreenShowing = true;
                    EnterFullscreenMode();
                    OnFullscreenOpened?.Invoke(e.SlotId);
                    break;
                case "fullscreen_closed":
                    IsFullscreenShowing = false;
                    ExitFullscreenMode();
                    OnFullscreenClosed?.Invoke(e.SlotId);
                    break;
                case "reward_earned":
                    RewardEarned.Add(e.SlotId);
                    break;
                case "ad_ready":
                    OnAdReady?.Invoke(e.SlotId, p.ToAdInfo());
                    break;
                case "video_gate":
                    OnVideoGate?.Invoke(e.SlotId, p.ToAdInfo());
                    break;
                case "closed":
                    Complete(e.SlotId, p.rewarded || RewardEarned.Contains(e.SlotId));
                    break;
                case "show_failed":
                    Complete(e.SlotId, false);
                    break;
                case "layout_invalid":
                case "media_too_small":
                case "config_error":
                    Debug.LogWarning("[AdZativeSDK] " + e);
                    break;
            }

            try { OnEvent?.Invoke(e); }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        private static void Complete(string slotId, bool rewarded)
        {
            if (!FullscreenCallbacks.TryGetValue(slotId, out var cb)) return;
            FullscreenCallbacks.Remove(slotId);
            RewardEarned.Remove(slotId);
            try { cb?.Invoke(rewarded); }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        // The game keeps focus and keeps rendering; we only mute / freeze gameplay if configured.
        private static void EnterFullscreenMode()
        {
            if (Settings.duckAudioDuringFullscreen) { _savedVolume = AudioListener.volume; AudioListener.volume = 0f; }
            if (Settings.pauseTimeScaleDuringFullscreen) { _savedTimeScale = Time.timeScale; Time.timeScale = 0f; }
        }

        private static void ExitFullscreenMode()
        {
            if (Settings.duckAudioDuringFullscreen) AudioListener.volume = _savedVolume;
            if (Settings.pauseTimeScaleDuringFullscreen) Time.timeScale = _savedTimeScale;
        }

        private static void EnsureInit()
        {
            if (!IsInitialized) Initialize();
        }
    }
}
