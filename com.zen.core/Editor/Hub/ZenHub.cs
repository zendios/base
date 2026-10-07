using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Base.Setup
{
    /// <summary>One tab of Base > Hub.</summary>
    public interface IZenHubTab
    {
        string Title { get; }
        void OnEnable();
        void OnGUI();
    }

    /// <summary>
    /// Base > Hub, the only Base menu: Setup (packages, the game's missing settings, update / rollback), Ad Config
    /// (changes from the defaults, presets, Remote Config export, Remove Ads product), Ad IDs (Android / iOS tabs,
    /// paste from a sheet, checks), Build Check (what a release build must not ship with) and, in the zen-unity
    /// development project only, Release (CHANGELOG notes, version, tag).
    /// </summary>
    public class ZenHub : EditorWindow
    {
        private List<IZenHubTab> tabs;
        private int tab;

        [MenuItem("Base/Hub", priority = -10)]
        public static void Open() => Open(null);

        /// <summary>Opens the Hub on the tab with this title (null = the last one shown).</summary>
        public static void Open(string title)
        {
            var w = GetWindow<ZenHub>("Base Hub");
            w.minSize = new Vector2(720, 480);
            int i = title == null ? -1 : w.tabs.FindIndex(t => t.Title == title);
            if (i >= 0 && i != w.tab)
            {
                w.tab = i;
                w.tabs[i].OnEnable();
            }
        }

        private void OnEnable()
        {
            tabs = new List<IZenHubTab> { new HubSetup(), new HubAdConfig(), new HubAdIds(), new HubBuildCheck() };
            if (HubRelease.Available)
                tabs.Add(new HubRelease());
            tab = Mathf.Clamp(tab, 0, tabs.Count - 1);
            ZenUpdateChecker.Changed -= Repaint;
            ZenUpdateChecker.Changed += Repaint;
            foreach (var t in tabs)
                t.OnEnable();
        }

        private void OnDisable() => ZenUpdateChecker.Changed -= Repaint;

        private void OnInspectorUpdate()
        {
            if (HubRelease.Running)
                Repaint();   // live release output
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            int next = GUILayout.Toolbar(tab, tabs.Select(t => t.Title).ToArray(), EditorStyles.toolbarButton, GUI.ToolbarButtonSize.FitToContents);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            if (next != tab)
            {
                tab = next;
                tabs[tab].OnEnable();
                GUI.FocusControl(null);
            }
            GUILayout.Space(4);
            tabs[tab].OnGUI();
        }

        // ---- shared drawing helpers ----

        /// <summary>
        /// Compact issue rows: icon, group name (first row only), a short summary (the full message as its tooltip)
        /// and the Fix button (mini) when the issue has one. ready = a "Ready" row when there is no issue.
        /// </summary>
        internal static void Issues(IEnumerable<ZenSetupIssue> issues, System.Action afterFix = null, string group = null, bool ready = false)
        {
            var list = issues.ToList();
            bool column = group != null;
            if (list.Count == 0 && ready)
                IssueRow(column, group, Icon(MessageType.None), "Ready", null, null);
            foreach (var i in list)
            {
                if (IssueRow(column, group, Icon(i.type), i.title ?? Short(i.message), i.message, i.fix != null ? i.fixLabel ?? "Fix" : null))
                {
                    i.fix();
                    afterFix?.Invoke();
                    GUIUtility.ExitGUI();
                }
                group = null;   // the group name only on its first row
            }
        }

        /// <summary>One 20 px row; true when its button was pressed.</summary>
        private static bool IssueRow(bool column, string group, GUIContent icon, string text, string tooltip, string button)
        {
            var r = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
            float x = r.x + 2;
            GUI.Label(new Rect(x, r.y + 2, 16, 16), icon);
            x += 20;
            if (column)
            {
                GUI.Label(new Rect(x, r.y, 80, r.height), group ?? "", Title);
                x += 84;
            }
            float right = r.xMax - (button != null ? 94 : 0);
            GUI.Label(new Rect(x, r.y, right - x - 4, r.height), new GUIContent(text, tooltip), Body);
            return button != null && GUI.Button(new Rect(right, r.y + 2, 90, r.height - 4), button, EditorStyles.miniButton);
        }
        private static GUIContent Icon(MessageType type)
        {
            switch (type)
            {
                case MessageType.Error: return EditorGUIUtility.IconContent("console.erroricon.sml");
                case MessageType.Warning: return EditorGUIUtility.IconContent("console.warnicon.sml");
                case MessageType.Info: return EditorGUIUtility.IconContent("console.infoicon.sml");
                default: return EditorGUIUtility.IconContent("TestPassed");
            }
        }

        /// <summary>The first clause of a message (up to ": ", ". " or " ("), at most 80 characters.</summary>
        internal static string Short(string message)
        {
            if (string.IsNullOrEmpty(message)) return "";
            int cut = new[] { ": ", ". ", " (" }.Select(s => message.IndexOf(s, System.StringComparison.Ordinal)).Where(i => i > 0).DefaultIfEmpty(message.Length).Min();
            string s = message.Substring(0, cut).Trim().TrimEnd('.');
            return s.Length > 80 ? s.Substring(0, 77) + "..." : s;
        }

        internal static void Header(string text, string help = null)
        {
            GUILayout.Space(6);
            EditorGUILayout.LabelField(text, Title);
            if (help != null)
                EditorGUILayout.LabelField(help, NoteWrapped);
        }

        // ---- typography: the Hub uses these text styles only (two sizes: 12 for Title / Body, 10 for Note) ----

        /// <summary>Section headers and row group names (bold 12).</summary>
        internal static GUIStyle Title => EditorStyles.boldLabel;
        /// <summary>Names, values, table cells (12).</summary>
        internal static GUIStyle Body => EditorStyles.label;
        /// <summary>Hints, column headers, toolbar captions (10).</summary>
        internal static GUIStyle Note => EditorStyles.miniLabel;
        /// <summary>Note text that wraps (10).</summary>
        internal static GUIStyle NoteWrapped => EditorStyles.wordWrappedMiniLabel;
        /// <summary>Body in yellow: needs attention (missing ID, not the tested version).</summary>
        internal static GUIStyle Warn => warn ?? (warn = Tinted(new Color(1f, 0.8f, 0.3f), new Color(0.65f, 0.4f, 0f)));
        /// <summary>Body in green: good news (newer version, unused package).</summary>
        internal static GUIStyle Good => good ?? (good = Tinted(new Color(0.45f, 0.85f, 0.45f), new Color(0.1f, 0.5f, 0.1f)));
        /// <summary>Body greyed: empty / off / not applicable.</summary>
        internal static GUIStyle Muted => muted ?? (muted = Tinted(new Color(0.6f, 0.6f, 0.6f), new Color(0.45f, 0.45f, 0.45f)));
        private static GUIStyle warn, good, muted;

        private static GUIStyle Tinted(Color dark, Color light) =>
            new GUIStyle(EditorStyles.label) { normal = { textColor = EditorGUIUtility.isProSkin ? dark : light } };

        internal static readonly Color Changed = new Color(1f, 0.85f, 0.35f);
        internal static readonly Color Bad = new Color(1f, 0.55f, 0.5f);
    }
}
