using System.Collections.Generic;
using UnityEngine;

namespace Base.Ads
{
    /// <summary>OnGUI placeholders for the editor bridge (own file so Unity resolves the MonoBehaviour).</summary>
    internal sealed class EditorAdView : MonoBehaviour
    {
        public struct Slot
        {
            public bool Normalized; public Rect Rect;              // canvas rect, origin top-left, 0..1
            public AdZativePosition Anchor; public float X, Y, W, H;  // anchor mode, dp (W = -1 full width)
            public string Label;
        }

        public EditorAdZativeBridge Bridge;
        public readonly Dictionary<string, Slot> Inline = new Dictionary<string, Slot>();
        public string Fullscreen;
        public string FullscreenLabel;
        private GUIStyle _style;

        private void OnGUI()
        {
            if (_style == null) _style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            GUI.depth = -1000;
            float dp = Screen.dpi > 0 ? Screen.dpi / 160f : 2f;
            foreach (var kv in Inline)
            {
                var s = kv.Value;
                Rect r;
                if (s.Normalized)
                {
                    r = new Rect(s.Rect.x * Screen.width, s.Rect.y * Screen.height, s.Rect.width * Screen.width, s.Rect.height * Screen.height);
                }
                else
                {
                    float w = s.W < 0 ? Screen.width : s.W * dp, h = s.H * dp;
                    float x = (Screen.width - w) / 2f, y = Screen.height - h;
                    switch (s.Anchor)
                    {
                        case AdZativePosition.Top: y = 0; break;
                        case AdZativePosition.TopLeft: x = 0; y = 0; break;
                        case AdZativePosition.TopRight: x = Screen.width - w; y = 0; break;
                        case AdZativePosition.BottomLeft: x = 0; break;
                        case AdZativePosition.BottomRight: x = Screen.width - w; break;
                        case AdZativePosition.Center: y = (Screen.height - h) / 2f; break;
                        case AdZativePosition.Custom: x = s.X * dp; y = s.Y * dp; break;
                    }
                    r = new Rect(x, y, w, h);
                }
                GUI.Box(r, $"[Ad] {kv.Key}\n{s.Label}", _style);
            }
            if (Fullscreen != null)
            {
                GUI.Box(new Rect(0, 0, Screen.width, Screen.height), $"[Ad] {Fullscreen}\n{FullscreenLabel}", _style);
                if (GUI.Button(new Rect(Screen.width - 140, 20, 120, 60), "✕ Close")) Bridge.CloseFullscreen();
            }
        }
    }
}
