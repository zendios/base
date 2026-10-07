using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Base.Setup
{
    /// <summary>One thing the game still has to do; Fix does it when it can be done automatically.</summary>
    public class ZenSetupIssue
    {
        public string message;
        public string title;     // short line shown in the Hub (the message is its tooltip); null = the message's first clause
        public MessageType type = MessageType.Warning;
        public string fixLabel;
        public Action fix;
    }

    /// <summary>
    /// A Zen package's part of Base > Hub > Setup: what the game is missing for that package. Each package's editor
    /// assembly declares one; the window finds them by type, so a package that is not installed adds nothing.
    /// </summary>
    public interface IZenSetupModule
    {
        string Name { get; }
        int Order { get; }
        IEnumerable<ZenSetupIssue> Check();
    }

    /// <summary>Paths and helpers shared by the setup modules.</summary>
    public static class ZenSetupUtil
    {
        /// <summary>The game's own Zen data (AdConfig, ZenAdIds, ZenIapSettings): the project's root Resources folder, which package updates never write.</summary>
        public const string ResourcesFolder = "Assets/Resources";

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>The game's asset of type T in Resources (any folder), or null.</summary>
        public static T FindResource<T>(string resourceName) where T : UnityEngine.Object => Resources.Load<T>(resourceName);

        /// <summary>Creates Assets/Resources/{name}.asset with the class defaults and selects it.</summary>
        public static T CreateResource<T>(string resourceName, Action<T> init = null) where T : ScriptableObject
        {
            EnsureFolder(ResourcesFolder);
            var asset = ScriptableObject.CreateInstance<T>();
            init?.Invoke(asset);
            string path = $"{ResourcesFolder}/{resourceName}.asset";
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            Debug.Log($"[Base Setup] created {path}");
            return asset;
        }
    }

    /// <summary>Base > Hub > Setup: Base Core version (update / rollback), the game's SDKs (tested versions, install,
    /// newer versions, old .unitypackage installs), then the game's own settings.</summary>
    internal class HubSetup : IZenHubTab
    {
        public string Title => "Setup";

        private const string OpenUpmUrl = "https://package.openupm.com";

        private Dictionary<string, PackageInfo> installed = new Dictionary<string, PackageInfo>();
        private ListRequest listRequest;
        private AddAndRemoveRequest addRequest;
        private string busy;
        private Vector2 scroll;
        private List<(IZenSetupModule module, List<ZenSetupIssue> issues)> checks = new List<(IZenSetupModule, List<ZenSetupIssue>)>();
        private List<(string path, string what)> legacy = new List<(string, string)>();
        private readonly UnusedPackages unused = new UnusedPackages();

        private string pick;            // version picked in the list (update / rollback)
        private Vector2 notesScroll;

        public void OnEnable()
        {
            FirebaseInstaller.Changed -= OnChanged;
            FirebaseInstaller.Changed += OnChanged;
            BaseSdks.Changed -= Repaint;
            BaseSdks.Changed += Repaint;
            Refresh();
            BaseSdks.CheckLatestOnce();
        }

        private void OnChanged()
        {
            Refresh();
            Repaint();
        }

        private static void Repaint()
        {
            if (EditorWindow.HasOpenInstances<ZenHub>())
                EditorWindow.GetWindow<ZenHub>(false, null, false).Repaint();
        }

        private void Refresh()
        {
            listRequest = Client.List(true);
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
            legacy = BaseSdks.Legacy();
            RunChecks();
        }

        private void RunChecks()
        {
            checks = TypeCache.GetTypesDerivedFrom<IZenSetupModule>()
                .Where(t => !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)
                .Select(t => (IZenSetupModule)Activator.CreateInstance(t))
                .OrderBy(m => m.Order)
                .Select(m => (m, SafeCheck(m)))
                .ToList();
        }

        private static List<ZenSetupIssue> SafeCheck(IZenSetupModule module)
        {
            try { return module.Check().ToList(); }
            catch (Exception ex) { return new List<ZenSetupIssue> { new ZenSetupIssue { message = ex.Message, type = MessageType.Error } }; }
        }

        private void Poll()
        {
            if (listRequest != null && listRequest.IsCompleted)
            {
                if (listRequest.Status == StatusCode.Success)
                    installed = listRequest.Result.ToDictionary(p => p.name, p => p);
                listRequest = null;
                Repaint();
            }
            if (addRequest != null && addRequest.IsCompleted)
            {
                if (addRequest.Status == StatusCode.Failure)
                    Debug.LogError($"[Base Setup] {busy}: {addRequest.Error?.message}");
                addRequest = null;
                busy = null;
                Refresh();
            }
            if (listRequest == null && addRequest == null)
                EditorApplication.update -= Poll;
        }

        private string Version
        {
            get
            {
                var self = PackageInfo.FindForAssembly(typeof(HubSetup).Assembly);
                return self != null ? self.version : "1.1.0";
            }
        }

        public void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            ZenHub.Header($"Base Core {Version}");
            DrawVersions();
            EditorGUILayout.HelpBox("Code and default prefabs come from the packages (read-only, updated by tag). " +
                                    "The game's ad IDs, AdConfig, IAP settings and prefab variants live in the game (Assets/Resources, prefab variants), which updates never touch.",
                MessageType.None);

            DrawStatus();

            DrawSdks();
            DrawLegacy();
            unused.Draw(installed, Refresh);
            EditorGUILayout.EndScrollView();
        }

        /// <summary>Status of the game's settings, first in the tab: one row per module, details on hover.</summary>
        private void DrawStatus()
        {
            ZenHub.Header("Status");
            int fixable = checks.Sum(c => c.issues.Count(i => i.fix != null));
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Hover a row for details", ZenHub.Note);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(fixable == 0))
            {
                if (GUILayout.Button($"Fix all ({fixable})", EditorStyles.toolbarButton))
                {
                    foreach (var issue in checks.SelectMany(c => c.issues).Where(i => i.fix != null))
                        issue.fix();
                    AssetDatabase.Refresh();
                    RunChecks();
                    GUIUtility.ExitGUI();
                }
            }
            if (GUILayout.Button("Check again", EditorStyles.toolbarButton))
                Refresh();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foreach (var (module, issues) in checks)
                ZenHub.Issues(issues, () => { AssetDatabase.Refresh(); RunChecks(); }, module.Name, true);
            EditorGUILayout.EndVertical();
        }

        /// <summary>Update / rollback: every released version (zen/* tags), its CHANGELOG notes, Switch (see ZenUpdateChecker).</summary>
        private void DrawVersions()
        {
            var status = ZenUpdateChecker.Status;
            if (status == ZenUpdateChecker.State.Development)
            {
                EditorGUILayout.HelpBox("Embedded, local or test (.tgz) copy: no update or rollback here. Go back to a released version by its tag in manifest.json.", MessageType.None);
                return;
            }
            if (status == ZenUpdateChecker.State.UpdateAvailable)
                EditorGUILayout.HelpBox($"Base Core {ZenUpdateChecker.Latest} is available (this game has {ZenUpdateChecker.Installed}).", MessageType.Warning);
            else if (status == ZenUpdateChecker.State.Failed)
                EditorGUILayout.HelpBox($"Update check failed: {ZenUpdateChecker.Error}", MessageType.Warning);

            var versions = ZenUpdateChecker.Versions;
            EditorGUILayout.BeginHorizontal();
            if (versions.Count > 0)
            {
                if (pick == null || !versions.Contains(pick))
                    pick = versions.Contains(ZenUpdateChecker.Installed) ? ZenUpdateChecker.Installed : versions[0];
                var labels = versions.Select(v => v == ZenUpdateChecker.Installed ? v + " (installed)" : v == versions[0] ? v + " (latest)" : v).ToArray();
                pick = versions[EditorGUILayout.Popup("Version", versions.IndexOf(pick), labels)];
                bool older = Newer(ZenUpdateChecker.Installed, pick);
                using (new EditorGUI.DisabledScope(pick == ZenUpdateChecker.Installed))
                {
                    if (GUILayout.Button(older ? $"Roll back to {pick}" : $"Update to {pick}", GUILayout.Width(130))
                        && EditorUtility.DisplayDialog("Base Core", $"Move com.zen.core and com.zen.plugins.adzative to {pick}?\nThe game's Assets/Resources settings are not touched.", older ? "Roll back" : "Update", "Cancel"))
                        ZenUpdateChecker.UpdateTo(pick);
                }
            }
            else
            {
                EditorGUILayout.LabelField(status == ZenUpdateChecker.State.Checking ? "Checking the released versions..." : "Released versions not loaded yet.", ZenHub.Note);
                GUILayout.FlexibleSpace();
            }
            using (new EditorGUI.DisabledScope(status == ZenUpdateChecker.State.Checking))
                if (GUILayout.Button("Check for updates", GUILayout.Width(130)))
                    ZenUpdateChecker.Check(true);
            EditorGUILayout.EndHorizontal();

            string notes = pick != null ? ZenUpdateChecker.Notes(pick) : null;
            if (!string.IsNullOrEmpty(notes))
            {
                notesScroll = EditorGUILayout.BeginScrollView(notesScroll, EditorStyles.helpBox, GUILayout.MaxHeight(140));
                EditorGUILayout.LabelField(notes, ZenHub.NoteWrapped);
                EditorGUILayout.EndScrollView();
            }
        }

        private static bool Newer(string a, string b) =>
            System.Version.TryParse(a ?? "", out var va) && System.Version.TryParse(b ?? "", out var vb) && va > vb;

        private const float ColW = 66, ActW = 104, Pad = 6;

        /// <summary>The SDK table: one compact row per SDK (tick, name, installed, tested, latest, action), mediation
        /// folded to its installed adapters, versions that differ from the tested one in yellow, newer ones in green.</summary>
        private void DrawSdks()
        {
            ZenHub.Header("SDKs");
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            using (new EditorGUI.DisabledScope(addRequest != null))
                if (GUILayout.Button(new GUIContent("Install required + selected", "Installs the required SDKs and the ticked ones that are missing, at their tested versions"), EditorStyles.toolbarButton))
                    InstallSelected();
            GUILayout.FlexibleSpace();
            GUILayout.Label(BaseSdks.Checking ? "Checking the latest versions..." : "Tested = installed by Base, Latest = newest official", ZenHub.Note);
            using (new EditorGUI.DisabledScope(BaseSdks.Checking))
                if (GUILayout.Button(new GUIContent("Refresh", "Ask the official sources for the latest versions again"), EditorStyles.toolbarButton))
                    BaseSdks.CheckLatest();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            var cols = Columns(GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true)));
            GUI.Label(cols[1], "SDK", ZenHub.Note);
            GUI.Label(cols[2], "Installed", ZenHub.Note);
            GUI.Label(cols[3], "Tested", ZenHub.Note);
            GUI.Label(cols[4], "Latest", ZenHub.Note);

            using (new EditorGUI.DisabledScope(addRequest != null))
            {
                int row = 0;
                foreach (var group in BaseSdks.All.GroupBy(s => s.group))
                {
                    var sdks = group.ToList();
                    if (group.Key == SdkGroup.Mediation)
                    {
                        int count = sdks.Count(s => installed.ContainsKey(s.id));
                        bool open = SessionState.GetBool("Base.Sdk.Mediation", false);
                        bool next = EditorGUILayout.Foldout(open, $"AdMob mediation ({count} of {sdks.Count} installed)", true);
                        if (next != open) SessionState.SetBool("Base.Sdk.Mediation", next);
                        if (!next)
                            sdks = sdks.Where(s => installed.ContainsKey(s.id) || BaseSdks.Selected(s)).ToList();
                    }
                    else
                        EditorGUILayout.LabelField(group.Key.ToString(), ZenHub.Title);
                    foreach (var s in sdks)
                        DrawSdk(s, row++);
                }
            }
            EditorGUILayout.EndVertical();
            if (busy != null)
                EditorGUILayout.HelpBox($"Installing {busy}...", MessageType.Info);
        }

        /// <summary>Tick, name, installed, tested, latest, action.</summary>
        private static Rect[] Columns(Rect r)
        {
            float x = r.x + 2, right = r.xMax - 2;
            var action = new Rect(right - ActW, r.y, ActW, r.height);
            var latest = new Rect(action.x - Pad - ColW, r.y, ColW, r.height);
            var tested = new Rect(latest.x - Pad - ColW, r.y, ColW, r.height);
            var have = new Rect(tested.x - Pad - ColW, r.y, ColW, r.height);
            var tick = new Rect(x, r.y + 1, 16, r.height);
            var name = new Rect(x + 20, r.y, Mathf.Max(60, have.x - Pad - x - 20), r.height);
            return new[] { tick, name, have, tested, latest, action };
        }

        private void DrawSdk(Sdk s, int row)
        {
            var r = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
            if (row % 2 == 1)
                EditorGUI.DrawRect(r, EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.035f) : new Color(0f, 0f, 0f, 0.045f));
            var c = Columns(r);
            bool pro = false, store = s.source == SdkSource.AssetStore;   // DOTween: in Assets, not a package
            string have = store ? BaseSdks.DotweenVersion(out pro) : installed.TryGetValue(s.id, out var info) ? info.version : null;

            using (new EditorGUI.DisabledScope(s.group == SdkGroup.Required))
            {
                bool on = BaseSdks.Selected(s);
                bool next = EditorGUI.Toggle(c[0], GUIContent.none, on);
                if (next != on) BaseSdks.SetSelected(s, next);
            }
            GUI.Label(c[1], new GUIContent(s.display, s.info), ZenHub.Body);
            GUI.Label(c[2], new GUIContent(have == null ? "-" : pro ? have + " Pro" : have, have != null && have != s.tested ? "Not the tested version" : null),
                have == null ? ZenHub.Muted : have != s.tested ? ZenHub.Warn : ZenHub.Body);
            GUI.Label(c[3], s.tested, ZenHub.Body);
            bool newer = !string.IsNullOrEmpty(s.latest) && Newer(s.latest, have ?? s.tested);
            GUI.Label(c[4], s.latest == "-" ? new GUIContent("-", "No official version feed") : s.latest == null ? new GUIContent("...", "Not checked yet") : s.latest == "" ? new GUIContent("?", "Check failed (see the Console)") : new GUIContent(s.latest),
                newer ? ZenHub.Good : ZenHub.Muted);

            var button = new Rect(c[5].x, c[5].y + 2, c[5].width, c[5].height - 4);
            if (store)
            {
                if (have == null && GUI.Button(button, new GUIContent("Asset Store", "Import DOTween (free); DOTween Pro is added by hand"), EditorStyles.miniButton))
                    Application.OpenURL(BaseSdks.DotweenStoreUrl);
            }
            else if (have == null)
            {
                if (GUI.Button(button, $"Install {s.tested}", EditorStyles.miniButton))
                    Install(s, s.tested);
            }
            else if (have != s.tested)
            {
                if (GUI.Button(button, new GUIContent($"Use {s.tested}", "Back to the version Base was tested with"), EditorStyles.miniButton)
                    && EditorUtility.DisplayDialog("Base Setup", $"{s.display}: {have} -> {s.tested} (tested by Base)?", "OK", "Cancel"))
                    Install(s, s.tested);
            }
            else if (newer)
            {
                if (GUI.Button(button, new GUIContent($"Update {s.latest}", "Newer official version, not tested by Base yet"), EditorStyles.miniButton)
                    && EditorUtility.DisplayDialog("Base Setup", $"{s.display} {s.latest} has not been tested by Base yet (tested: {s.tested}). Update anyway?", "Update", "Cancel"))
                    Install(s, s.latest);
            }
        }

        /// <summary>Old .unitypackage installs that clash with the packages, each with Remove (to the OS trash).</summary>
        private void DrawLegacy()
        {
            if (legacy.Count == 0)
                return;
            ZenHub.Header("Old installs", "Installed from .unitypackage: they clash with the SDK packages above. Remove moves the folder to the trash.");
            foreach (var (path, what) in legacy)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox($"{path}: {what}", MessageType.Warning);
                EditorGUILayout.BeginVertical(GUILayout.Width(90));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Remove", EditorStyles.miniButton)
                    && EditorUtility.DisplayDialog("Base Setup", $"Move {path} to the trash?", "Remove", "Cancel"))
                {
                    if (!AssetDatabase.MoveAssetToTrash(path))
                        Debug.LogError($"[Base Setup] could not remove {path}");
                    Refresh();
                    GUIUtility.ExitGUI();
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
        }

        private void Install(Sdk s, string version)
        {
            if (s.source == SdkSource.Firebase)
                FirebaseInstaller.Install(version);
            else
                Add(new[] { BaseSdks.InstallId(s, version) }, $"{s.display} {version}");
        }

        /// <summary>New / old project: the required SDKs and the ticked ones that are missing, at their tested versions.</summary>
        private void InstallSelected()
        {
            var missing = BaseSdks.All.Where(s => s.source != SdkSource.AssetStore && BaseSdks.Selected(s) && !installed.ContainsKey(s.id)).ToList();
            if (missing.Count == 0)
            {
                EditorUtility.DisplayDialog("Base Setup", "Every required and ticked SDK is installed.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Base Setup", "Install (tested versions):\n\n" + string.Join("\n", missing.Select(s => $"{s.display} {s.tested}")), "Install", "Cancel"))
                return;
            var ids = missing.Where(s => s.source != SdkSource.Firebase).Select(s => BaseSdks.InstallId(s, s.tested)).ToArray();
            if (ids.Length > 0)
                Add(ids, string.Join(", ", missing.Where(s => s.source != SdkSource.Firebase).Select(s => s.display)));
            if (missing.Any(s => s.source == SdkSource.Firebase))
                FirebaseInstaller.Install(FirebaseInstaller.Pinned);
        }

        private void Add(string[] ids, string what)
        {
            if (ids.Any(i => i.StartsWith("com.google")))
                EnsureOpenUpm();
            busy = what;
            addRequest = Client.AddAndRemove(ids);
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        /// <summary>AdMob, its adapters, EDM4U, the Google Play packages (com.google) and Base itself (com.zen) come from
        /// OpenUPM: add the scoped registry once, or the missing scope to it.</summary>
        internal static void EnsureOpenUpm()
        {
            const string manifestPath = "Packages/manifest.json";
            string json = File.ReadAllText(manifestPath);
            int at = json.IndexOf(OpenUpmUrl, StringComparison.Ordinal);
            if (at >= 0)
            {
                // the registry is there: add a missing scope to its "scopes" list
                int scopes = json.IndexOf("\"scopes\"", at, StringComparison.Ordinal);
                int open = scopes >= 0 ? json.IndexOf('[', scopes) : -1, close = open >= 0 ? json.IndexOf(']', open) : -1;
                if (close < 0)
                    return;
                string list0 = json.Substring(open + 1, close - open - 1);
                var missing = new[] { "com.google", "com.zen" }.Where(s => !list0.Contains($"\"{s}\"")).ToList();
                if (missing.Count == 0)
                    return;
                string add = string.Join(", ", missing.Select(s => $"\"{s}\""));
                json = json.Substring(0, close).TrimEnd() + (list0.Trim().Length > 0 ? ", " : " ") + add + " " + json.Substring(close);
                File.WriteAllText(manifestPath, json);
                Debug.Log($"[Base Setup] OpenUPM registry: added the scope(s) {string.Join(", ", missing)} to Packages/manifest.json");
                Client.Resolve();
                return;
            }
            const string entry = "\n    {\n      \"name\": \"OpenUPM\",\n      \"url\": \"" + OpenUpmUrl + "\",\n      \"scopes\": [ \"com.google\", \"com.zen\" ]\n    }";
            int list = json.IndexOf("\"scopedRegistries\"", StringComparison.Ordinal);
            if (list >= 0)
            {
                int bracket = json.IndexOf('[', list);
                bool empty = json.Substring(bracket + 1).TrimStart().StartsWith("]");
                json = json.Substring(0, bracket + 1) + entry + (empty ? "" : ",") + json.Substring(bracket + 1);
            }
            else
            {
                int brace = json.IndexOf('{');
                json = json.Substring(0, brace + 1) + "\n  \"scopedRegistries\": [" + entry + "\n  ]," + json.Substring(brace + 1);
            }
            File.WriteAllText(manifestPath, json);
            Debug.Log("[Base Setup] added the OpenUPM scoped registry (com.google, com.zen) to Packages/manifest.json");
            Client.Resolve();
        }
    }
}
