using System;
using System.Collections.Generic;
using UnityEngine;

namespace Base.Ads
{
    /// <summary>
    /// Editor / unsupported-platform stub. Fakes the native lifecycle and draws placeholders exactly
    /// where the Android views would be (Canvas rects included), so UI can be laid out in Play Mode.
    /// </summary>
    internal sealed class EditorAdZativeBridge : IAdZativeBridge
    {
        private Action<string, string, string> _emit;
        private readonly Dictionary<string, Cfg> _slots = new Dictionary<string, Cfg>();
        private EditorAdView _view;

        [Serializable] private class Cfg { public string slotId; public string format; public string layout; public List<string> adUnitIds; }

        public void Initialize(string appId, Action<string, string, string> onEvent)
        {
            _emit = onEvent;
            var go = new GameObject("[AdZativeSDK Editor Preview]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _view = go.AddComponent<EditorAdView>();
            _view.Bridge = this;
            _emit("", "initialized", "{}");
        }

        public void InitializeExternal(Action<string, string, string> onEvent) => Initialize("", onEvent);

        public void LoadUnit(string slotId, string adUnitId)
        {
            _emit("", "load_request", "{\"ad_unit\":\"" + adUnitId + "\",\"attempt\":1}");
            _emit("", "loaded", "{\"ad_unit\":\"" + adUnitId + "\",\"latency_ms\":350,\"network\":\"AdMob Network\"}");
            _emit(slotId, "unit_loaded", "{\"ad_unit\":\"" + adUnitId + "\",\"network\":\"AdMob Network\"}");
        }

        public void SetMaxCachedAds(int n) { }

        public void ConfigureSlot(string json)
        {
            var c = JsonUtility.FromJson<Cfg>(json);
            _slots[c.slotId] = c;
            string unit = c.adUnitIds != null && c.adUnitIds.Count > 0 ? c.adUnitIds[0] : "";
            _emit(c.slotId, "configured", "{\"format\":\"" + c.format + "\",\"layout\":\"" + c.layout + "\"}");
            _emit("", "load_request", "{\"ad_unit\":\"" + unit + "\",\"attempt\":1}");
            _emit("", "loaded", "{\"ad_unit\":\"" + unit + "\",\"latency_ms\":350,\"has_video\":true}");
        }

        public bool IsReady(string id) => _slots.ContainsKey(id);
        public bool HasReadyAd(string id) => _slots.ContainsKey(id);

        private string Unit(string id) => _slots.TryGetValue(id, out var c) && c.adUnitIds != null && c.adUnitIds.Count > 0 ? c.adUnitIds[0] : "";
        private string Label(string id) => _slots.TryGetValue(id, out var c) ? $"{c.format} · {c.layout}" : id;

        private void Impression(string id)
        {
            string u = "\"ad_unit\":\"" + Unit(id) + "\",\"source\":0";
            _emit(id, "shown", "{" + u + "}");
            _emit(id, "impression", "{" + u + "}");
            _emit(id, "paid", "{" + u + ",\"value_micros\":1200,\"currency\":\"USD\",\"precision\":\"ESTIMATED\"}");
        }

        public void ShowSlotInRect(string id, float nx, float ny, float nw, float nh)
        {
            _emit(id, "opportunity", "{\"ready\":true}");
            _view.Inline[id] = new EditorAdView.Slot { Rect = new Rect(nx, ny, nw, nh), Normalized = true, Label = Label(id) };
            Impression(id);
        }

        public void UpdateSlotRect(string id, float nx, float ny, float nw, float nh)
        {
            if (_view.Inline.TryGetValue(id, out var s)) { s.Rect = new Rect(nx, ny, nw, nh); _view.Inline[id] = s; }
        }

        public void ShowSlotAtAnchor(string id, AdZativePosition position, float xDp, float yDp, float wDp, float hDp)
        {
            _emit(id, "opportunity", "{\"ready\":true}");
            bool mrec = _slots.TryGetValue(id, out var c) && c.format == "MREC";
            _view.Inline[id] = new EditorAdView.Slot
            {
                Anchor = position, X = xDp, Y = yDp,
                W = wDp > 0 ? wDp : (mrec ? 300 : (position == AdZativePosition.Top || position == AdZativePosition.Bottom ? -1 : 320)),
                H = hDp > 0 ? hDp : (mrec ? 250 : 64),
                Label = Label(id)
            };
            Impression(id);
        }

        public void SetSlotLayout(string id, string layoutName)
        {
            if (_slots.TryGetValue(id, out var c)) c.layout = layoutName;
            if (_view.Inline.TryGetValue(id, out var s)) { s.Label = Label(id); _view.Inline[id] = s; }
        }

        public void HideSlot(string id) { _view.Inline.Remove(id); _emit(id, "hidden", "{}"); }

        public string GetPodInfo(string id, int podSize)
        {
            int n = Mathf.Clamp(podSize <= 0 ? 1 : podSize, 1, 3);
            string ad = "{\"ad_unit\":\"" + Unit(id) + "\",\"media_type\":\"VIDEO\",\"video_duration_s\":15,\"watch_sec\":15,\"duration_known\":true}";
            var ads = new List<string>();
            for (int i = 0; i < n; i++) ads.Add(ad);
            return "{\"slot\":\"" + id + "\",\"requested\":" + n + ",\"ready\":" + n + ",\"total_sec\":" + (15 * n) +
                   ",\"ads\":[" + string.Join(",", ads) + "]}";
        }

        public void AcceptVideo(string id) => _emit(id, "shown", "{\"media_type\":\"VIDEO\"}");
        public void RejectVideo(string id) => _emit(id, "discarded", "{\"reason\":\"video_rejected\"}");
        public void ReplaceAd(string id, bool allowVideo) => _emit(id, "replaced", "{\"allow_video\":" + (allowVideo ? "true" : "false") + "}");
        public void SetVideoMode(string id, string mode) { }
        public void SetRefreshSec(string id, int seconds) { }
        public void SetLayoutMode(string id, string mode) { }

        public void ShowFullscreen(string id, int podSize)
        {
            _emit(id, "opportunity", "{\"ready\":true}");
            _view.Fullscreen = id;
            _view.FullscreenLabel = Label(id);
            _emit(id, "fullscreen_opened", "{}");
            Impression(id);
        }

        public void CloseFullscreen()
        {
            if (_view.Fullscreen == null) return;
            string id = _view.Fullscreen;
            bool rewarded = _slots.TryGetValue(id, out var c) && c.format == "REWARDED";
            if (rewarded) _emit(id, "reward_earned", "{\"has_video\":true}");
            _view.Fullscreen = null;
            _emit(id, "closed", "{\"rewarded\":" + (rewarded ? "true" : "false") + "}");
            _emit(id, "fullscreen_closed", "{}");
        }

        public void HandleBack() => CloseFullscreen();
        public void SetAppOpenSuppressed(bool s) { }
        public void SuppressNextForeground() { }
        public void DestroySlot(string id) { _slots.Remove(id); _view.Inline.Remove(id); _emit(id, "destroyed", "{}"); }
        public string DumpState() => "{\"editor\":true,\"slots\":" + _slots.Count + "}";
    }
}
