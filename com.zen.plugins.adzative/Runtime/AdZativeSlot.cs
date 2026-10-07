using System.Collections.Generic;
using UnityEngine;

namespace Base.Ads
{
    /// <summary>
    /// Put this on any RectTransform under a Canvas (Screen Space - Camera, Overlay or World Space).
    /// The native ad is laid out exactly over the rect and follows it (move, resize, rotate, safe-area,
    /// orientation) — the Android layout stretches to the rect's size.
    ///
    /// Each slot has its own id, format, layout style and list of ad unit IDs.
    /// Native views draw above all Unity UI: call Hide()/DestroyAd() (or disable this GameObject)
    /// before a popup covers the slot.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    [AddComponentMenu("Zen/AdZative Slot")]
    public sealed class AdZativeSlot : MonoBehaviour
    {
        [Tooltip("Unique id of this position (also the analytics key).")]
        public string slotId = "canvas_slot";

        [Tooltip("BANNER or MREC (fullscreen formats are not positioned by a Canvas).")]
        public AdZativeFormat format = AdZativeFormat.MREC;

        [Tooltip("Ad unit IDs in priority order. 2 IDs recommended: e.g. high floor + default.")]
        public List<string> adUnitIds = new List<string> { AdZativeTestIds.NativeVideo, AdZativeTestIds.Native };

        public AdZativeSourcePolicy sourcePolicy = AdZativeSourcePolicy.Fallback;
        public AdZativeLayoutStyle layoutStyle = AdZativeLayoutStyle.Style1;
        [Tooltip("Android layout name when Layout Style = Custom (res/layout/<name>.xml).")]
        public string customLayout = "";

        [Header("Refresh & preload")]
        [Tooltip("Swap to a preloaded ad every N seconds while visible (0 = off, min 30).")]
        public int refreshSec = 60;
        public int minDisplaySec = 30;
        public bool waitVideoEnd = true;
        [Range(1, 3)] public int cacheSize = 1;
        public bool preloadWhenHidden = true;
        public bool startMuted = true;
        [Tooltip("ANY | LANDSCAPE | PORTRAIT | SQUARE — ANY maximises fill.")]
        public string mediaAspectRatio = "ANY";

        [Header("Media & layout")]
        [Tooltip("Allow: show video ads here. Gate: raise AdZativeSDK.OnVideoGate so the game can enlarge the slot " +
                 "(Follow a bigger RectTransform) then AcceptVideo(), or RejectVideo(). Block: never video.")]
        public AdZativeVideoMode videoMode = AdZativeVideoMode.Allow;
        [Tooltip("Auto: per ad, the built-in style that shows the most of its assets (icon, rating, store, price…).")]
        public AdZativeLayoutMode layoutMode = AdZativeLayoutMode.Manual;
        public AdZativeMediaScale mediaScale = AdZativeMediaScale.Fit;

        [Header("Behaviour")]
        public bool showOnEnable = true;
        public bool hideOnDisable = true;
        [Tooltip("Destroy the native ad when this GameObject is destroyed (frees memory).")]
        public bool destroyAdOnDestroy = true;
        [Tooltip("Resend the rect when it moves by more than this many screen pixels.")]
        public float syncThresholdPx = 1f;

        public bool IsShowing { get; private set; }

        private RectTransform _rt;
        private Rect _lastSent;
        private bool _registered;
        private int _lastScreenW, _lastScreenH;

        private void Awake() { _rt = (RectTransform)transform; }

        private void OnEnable()
        {
            if (showOnEnable) Show();
        }

        private void OnDisable()
        {
            if (hideOnDisable && IsShowing) Hide();
        }

        private void OnDestroy()
        {
            if (destroyAdOnDestroy && _registered && AdZativeSDK.IsInitialized) AdZativeSDK.Destroy(slotId);
        }

        public AdZativeSlotConfig BuildConfig() => new AdZativeSlotConfig
        {
            slotId = slotId, format = format, adUnitIds = new List<string>(adUnitIds), sourcePolicy = sourcePolicy,
            layoutStyle = layoutStyle, customLayout = customLayout, refreshSec = refreshSec, minDisplaySec = minDisplaySec,
            waitVideoEnd = waitVideoEnd, cacheSize = cacheSize, preloadWhenHidden = preloadWhenHidden,
            startMuted = startMuted, mediaAspectRatio = mediaAspectRatio,
            videoMode = videoMode, layoutMode = layoutMode, mediaScale = mediaScale
        };

        /// <summary>
        /// Make the ad follow another RectTransform (e.g. an expanded area for a video ad, then back to the compact
        /// one). The native view moves/resizes at once; keep both rects anchored top or bottom.
        /// </summary>
        public void Follow(RectTransform target)
        {
            if (target == null) return;
            _rt = target;
            _rt.hasChanged = true;
            _lastSent = default;
            if (!IsShowing || !AdZativeSDK.TryGetNormalizedRect(_rt, out var r)) return;
            _lastSent = r;
            AdZativeSDK.UpdateRect(slotId, r);
        }

        /// <summary>The RectTransform the ad currently follows (this one unless Follow() was called).</summary>
        public RectTransform FollowedRect => _rt;

        public void AcceptVideo() { if (_registered) AdZativeSDK.AcceptVideo(slotId); }
        public void RejectVideo() { if (_registered) AdZativeSDK.RejectVideo(slotId); }
        /// <summary>Destroy the ad on screen, swap in the next one (non-video unless allowVideo).</summary>
        public void ReplaceAd(bool allowVideo = false) { if (_registered) AdZativeSDK.ReplaceAd(slotId, allowVideo); }
        public void SetRefresh(int seconds) { refreshSec = seconds; if (_registered) AdZativeSDK.SetRefresh(slotId, seconds); }

        /// <summary>Registers (first time) and shows the ad over this RectTransform.</summary>
        public void Show()
        {
            EnsureRegistered();
            Canvas.ForceUpdateCanvases();
            if (!AdZativeSDK.TryGetNormalizedRect(_rt, out var r)) return;
            _lastSent = r;
            _lastScreenW = Screen.width; _lastScreenH = Screen.height;
            AdZativeSDK.ShowInRect(slotId, r);
            IsShowing = true;
        }

        public void Hide()
        {
            if (!_registered) return;
            AdZativeSDK.Hide(slotId);
            IsShowing = false;
        }

        /// <summary>Releases the ad and the slot. Show() registers it again.</summary>
        public void DestroyAd()
        {
            if (!_registered) return;
            AdZativeSDK.Destroy(slotId);
            _registered = false;
            IsShowing = false;
        }

        public void SetLayout(AdZativeLayoutStyle style, string custom = null)
        {
            layoutStyle = style;
            if (custom != null) customLayout = custom;
            if (_registered) AdZativeSDK.SetLayout(slotId, style, custom);
        }

        private void EnsureRegistered()
        {
            if (_registered) return;
            if (format != AdZativeFormat.BANNER && format != AdZativeFormat.MREC)
            {
                Debug.LogWarning($"[AdZativeSlot] {slotId}: only BANNER/MREC can be placed on a Canvas; using MREC.");
                format = AdZativeFormat.MREC;
            }
            AdZativeSDK.RegisterSlot(BuildConfig());
            _registered = true;
        }

        // Only talk to Android when the rect really changed: no JNI call per frame for static UI.
        private void LateUpdate()
        {
            if (!IsShowing || !_rt.hasChanged && Screen.width == _lastScreenW && Screen.height == _lastScreenH) return;
            _rt.hasChanged = false;
            if (!AdZativeSDK.TryGetNormalizedRect(_rt, out var r)) return;
            float tx = syncThresholdPx / Mathf.Max(1, Screen.width), ty = syncThresholdPx / Mathf.Max(1, Screen.height);
            if (Mathf.Abs(r.x - _lastSent.x) < tx && Mathf.Abs(r.y - _lastSent.y) < ty &&
                Mathf.Abs(r.width - _lastSent.width) < tx && Mathf.Abs(r.height - _lastSent.height) < ty) return;
            _lastSent = r;
            _lastScreenW = Screen.width; _lastScreenH = Screen.height;
            AdZativeSDK.UpdateRect(slotId, r);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (format != AdZativeFormat.BANNER && format != AdZativeFormat.MREC) format = AdZativeFormat.MREC;
            if (refreshSec > 0 && refreshSec < 30) refreshSec = 30;
        }
#endif
    }
}
