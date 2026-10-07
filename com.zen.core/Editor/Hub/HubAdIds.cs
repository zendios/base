using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Base.Ads;
using UnityEditor;
using UnityEngine;

namespace Base.Setup
{
    /// <summary>
    /// Ad IDs: Android / iOS tabs (the build target's tab opens first), one row per PlacementType (several IDs =
    /// waterfall order), paste from a Google Sheet, checks.
    /// </summary>
    internal class HubAdIds : IZenHubTab
    {
        public string Title => "Ad IDs";

        private static readonly Regex AnyId = new Regex(@"ca-app-pub-\d+/\d+");

        private ZenAdIds ids;
        private SerializedObject so;
        private Vector2 scroll;
        private int platform = -1;   // 0 Android, 1 iOS
        private List<ZenSetupIssue> issues = new List<ZenSetupIssue>();

        public void OnEnable()
        {
            if (platform < 0)
                platform = EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS ? 1 : 0;
            ids = Resources.Load<ZenAdIds>(ZenAdIds.ResourcePath);
            so = ids != null ? new SerializedObject(ids) : null;
            if (ids != null && ids.AddMissingPlacements())
            {
                EditorUtility.SetDirty(ids);
                so.Update();
            }
            Recheck();
        }

        private void Recheck()
        {
            issues = ids == null ? new List<ZenSetupIssue>()
                : ZenBuildCheck.CheckIds(ids, ZenBuildCheck.AdMobAppId(out _), MessageType.Error).Where(i => i.type != MessageType.Info).ToList();
        }

        public void OnGUI()
        {
            if (ids == null)
            {
                EditorGUILayout.HelpBox("No ZenAdIds in Assets/Resources: every placement loads Google test IDs.", MessageType.Error);
                if (GUILayout.Button("Create Assets/Resources/ZenAdIds", GUILayout.Width(260)))
                {
                    ZenSetupUtil.CreateResource<ZenAdIds>(ZenAdIds.ResourcePath, a => a.AddMissingPlacements());
                    OnEnable();
                }
                return;
            }

            bool ios = platform == 1;
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            platform = GUILayout.Toolbar(platform, new[] { "Android", "iOS" }, EditorStyles.toolbarButton, GUILayout.Width(160));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent($"Paste from clipboard ({(ios ? "iOS" : "Android")})",
                    "Copy the Google Sheet rows (name, ID), e.g. native_inter_default_high_220926 ca-app-pub-.../..., then click. " +
                    "Rows of the same placement are joined in waterfall order high, medium, low, all."), EditorStyles.toolbarButton))
                Paste(ios);
            EditorGUILayout.EndHorizontal();

            so.Update();
            var entries = so.FindProperty("entries");
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Placement", ZenHub.Note, GUILayout.Width(170));
            GUILayout.Label((ios ? "iOS" : "Android") + " IDs (one per line, waterfall order)", ZenHub.Note, GUILayout.MinWidth(150));
            EditorGUILayout.EndHorizontal();
            string publisher = ios ? null : ZenBuildCheck.AdMobAppId(out _)?.Split('~')[0];
            for (int i = 0; i < entries.arraySize; i++)
            {
                var e = entries.GetArrayElementAtIndex(i);
                var placement = (PlacementType)e.FindPropertyRelative("placement").intValue;
                var list = e.FindPropertyRelative(ios ? "ios" : "android");
                bool empty = !Enumerable.Range(0, list.arraySize).Any(k => !string.IsNullOrWhiteSpace(list.GetArrayElementAtIndex(k).stringValue));
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent(placement.ToString(), empty ? "No ID: this placement loads Google test ads" : null),
                    empty ? ZenHub.Warn : ZenHub.Body, GUILayout.Width(170));
                ListField(list, publisher, empty);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            if (so.ApplyModifiedProperties())
                Recheck();

            EditorGUILayout.LabelField("Yellow = no ID (the placement loads Google test ads; fine if the game does not use it). Red = malformed, Google test or other-account ID.", ZenHub.NoteWrapped);
            GUILayout.Space(4);
            ZenHub.Issues(issues);
        }

        /// <summary>A string list as a multi-line text box; IDs that look wrong turn red, an empty list yellow.</summary>
        private static void ListField(SerializedProperty list, string publisher, bool empty)
        {
            var values = Enumerable.Range(0, list.arraySize).Select(k => list.GetArrayElementAtIndex(k).stringValue).ToList();
            string text = string.Join("\n", values);
            bool bad = values.Any(v => !string.IsNullOrWhiteSpace(v) && (!AnyId.IsMatch(v.Trim()) || v.StartsWith(ZenBuildCheck.GoogleTestPublisher)
                                                                         || (publisher != null && !publisher.StartsWith(ZenBuildCheck.GoogleTestPublisher) && !v.Trim().StartsWith(publisher + "/"))));
            var old = GUI.backgroundColor;
            if (bad) GUI.backgroundColor = ZenHub.Bad;
            else if (empty) GUI.backgroundColor = ZenHub.Changed;
            int lines = Mathf.Max(1, values.Count);
            string next = EditorGUILayout.TextArea(text, GUILayout.Height(18 * lines + 2), GUILayout.MinWidth(150));
            GUI.backgroundColor = old;
            if (next == text)
                return;
            var parsed = next.Split(new[] { '\n', ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
            list.arraySize = parsed.Count;
            for (int k = 0; k < parsed.Count; k++)
                list.GetArrayElementAtIndex(k).stringValue = parsed[k];
        }

        /// <summary>Clipboard rows "name ID..." (tab, comma or spaces) -> the pasted placements' IDs of this platform.</summary>
        private void Paste(bool ios)
        {
            var rows = new List<(PlacementType placement, int tier, List<string> ids)>();
            var unknown = new List<string>();
            foreach (var line in EditorGUIUtility.systemCopyBuffer.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                var found = AnyId.Matches(line).Cast<Match>().Select(m => m.Value).ToList();
                if (found.Count == 0)
                    continue;
                string name = line.Substring(0, line.IndexOf(found[0], StringComparison.Ordinal)).Trim();
                var placement = ParseName(name, out int tier);
                if (placement == PlacementType.None)
                    unknown.Add(name.Length > 0 ? name : line.Trim());
                else
                    rows.Add((placement, tier, found));
            }
            if (rows.Count == 0)
            {
                EditorUtility.DisplayDialog("Base Hub", "The clipboard has no row \"name  ca-app-pub-.../...\".", "OK");
                return;
            }

            Undo.RecordObject(ids, "Paste ad IDs");
            var placements = rows.GroupBy(r => r.placement).ToList();
            foreach (var g in placements)
            {
                var list = g.OrderBy(r => r.tier).SelectMany(r => r.ids).Distinct().ToList();   // stable: same tier keeps sheet order
                var entry = ids.entries.First(e => e.placement == g.Key);
                if (ios) entry.ios = list; else entry.android = list;
            }
            EditorUtility.SetDirty(ids);
            AssetDatabase.SaveAssets();
            so.Update();
            Recheck();
            string msg = $"{(ios ? "iOS" : "Android")}: {placements.Count} placement(s) filled ({string.Join(", ", placements.Select(g => g.Key))}).";
            if (unknown.Count > 0)
                msg += "\n\nNot recognised: " + string.Join(", ", unknown);
            Debug.Log("[Base Hub] " + msg);
            EditorUtility.DisplayDialog("Base Hub", msg, "OK");
        }

        private static readonly string[] Tiers = { "high", "medium", "low", "all" };
        private static readonly (string key, string value)[] Formats =
            { ("app_open", "AppOpen"), ("appopen", "AppOpen"), ("banner", "Banner"), ("inter", "Inter"), ("mrec", "Mrec"), ("reward", "Reward") };

        /// <summary>
        /// Sheet name -> placement and waterfall tier (high 0, medium 1, low 2, all / none 3). "native_" = AdZative,
        /// else AdMob; then app_open / banner / inter / mrec / reward; then default / open / in_app (none = default);
        /// a tier and a date suffix are ignored for the placement. The enum name itself (Admob_Inter_Default,
        /// AdZativeBanner) also works.
        /// </summary>
        internal static PlacementType ParseName(string name, out int tier)
        {
            tier = Tiers.Length - 1;
            string flat = Regex.Replace(name, "[^A-Za-z0-9]", "");
            if (TryEnum(flat, out var exact))
                return exact;

            var t = Regex.Split(name.ToLowerInvariant(), "[^a-z0-9]+").Where(x => x.Length > 0 && !x.All(char.IsDigit)).ToList();
            foreach (var x in t.ToList())
            {
                int i = Array.IndexOf(Tiers, x == "mid" ? "medium" : x);
                if (i >= 0) { tier = i; t.Remove(x); }
            }
            bool native = t.Remove("native");
            string rest = string.Join("_", t);
            string format = null;
            foreach (var (key, value) in Formats)
                if (rest.StartsWith(key))
                {
                    format = value;
                    rest = rest.Substring(key.Length).Trim('_');
                    break;
                }
            if (format == null)
                return PlacementType.None;
            string sub = rest == "" || rest == "default" ? "Default" : rest == "open" ? "Open" : rest == "in_app" || rest == "iap" ? "IAP" : null;
            if (sub == null)
                return PlacementType.None;
            string baseName = (native ? "AdZative" : "Admob") + format;
            if (TryEnum(baseName + sub, out var p) || (sub == "Default" && TryEnum(baseName, out p)))
                return p;
            return PlacementType.None;
        }

        private static bool TryEnum(string name, out PlacementType p)
        {
            p = Enum.GetValues(typeof(PlacementType)).Cast<PlacementType>()
                .FirstOrDefault(x => x != PlacementType.None && string.Equals(x.ToString(), name, StringComparison.OrdinalIgnoreCase));
            return p != PlacementType.None;
        }
    }
}
