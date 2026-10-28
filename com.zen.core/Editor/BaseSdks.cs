using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.Networking;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Base.Setup
{
    internal enum SdkGroup { Required, Mediation, Optional }
    internal enum SdkSource { OpenUpm, AppsFlyerGit, UnityRegistry, Firebase, AssetStore }

    /// <summary>One SDK a Base game can use: where it officially comes from and the version Base was tested with.</summary>
    internal class Sdk
    {
        public string id, display, info, tested;
        public SdkGroup group;
        public SdkSource source;
        public bool defaultOn;
        public string latest;   // newest official version (after BaseSdks.CheckLatest), "" = unknown
    }

    /// <summary>
    /// The SDKs of a Base game, pinned to the versions Base was tested with, from their official sources: Google
    /// Mobile Ads, its mediation adapters and the Play plugins from OpenUPM (where Google publishes them), the External
    /// Dependency Manager (Unity's EDM, which AdMob 11.6+ uses) and Unity IAP from the Unity registry,
    /// AppsFlyer from its GitHub repository (tag), Firebase from dl.google.com
    /// (FirebaseInstaller). Base > Hub > Setup installs them, offers the tested version back, and shows newer ones
    /// (checked once per editor session).
    /// </summary>
    internal static class BaseSdks
    {
        public static readonly Sdk[] All =
        {
            new Sdk { id = "com.google.ads.mobile", display = "AdMob", tested = "11.6.0", group = SdkGroup.Required, source = SdkSource.OpenUpm, info = "Google Mobile Ads for Unity (11.6+ uses Unity's External Dependency Manager)." },
            new Sdk { id = "com.unity.external-dependency-manager", display = "EDM (Unity)", tested = "2.1.1", group = SdkGroup.Required, source = SdkSource.UnityRegistry, info = "Unity's External Dependency Manager: resolves the Android / iOS libraries of the SDKs. Replaces Google's EDM4U, which becomes a disabled stub (Firebase still finds its dependency)." },
            new Sdk { id = "com.unity.ads.ios-support", display = "iOS 14 Ad Support", tested = "1.2.0", group = SdkGroup.Required, source = SdkSource.UnityRegistry, info = "App Tracking Transparency prompt (ATTHelper, AppsFlyer): an iOS build of Base does not compile without it." },
            new Sdk { id = "dotween", display = "DOTween", tested = "1.3.030", group = SdkGroup.Required, source = SdkSource.AssetStore, info = "DOTween (free, Asset Store) or DOTween Pro (paid, added by hand) in Assets/Plugins/Demigiant. Not a package: Base cannot install it." },

            new Sdk { id = "com.google.ads.mobile.mediation.metaaudiencenetwork", display = "Meta", tested = "3.20.1", group = SdkGroup.Mediation, source = SdkSource.OpenUpm, defaultOn = true, info = "AdMob mediation adapter: Meta Audience Network." },
            new Sdk { id = "com.google.ads.mobile.mediation.liftoffmonetize", display = "Liftoff", tested = "5.7.9", group = SdkGroup.Mediation, source = SdkSource.OpenUpm, info = "AdMob mediation adapter: Liftoff Monetize (Vungle)." },
            new Sdk { id = "com.google.ads.mobile.mediation.applovin", display = "AppLovin", tested = "8.7.7", group = SdkGroup.Mediation, source = SdkSource.OpenUpm, info = "AdMob mediation adapter: AppLovin." },
            new Sdk { id = "com.google.ads.mobile.mediation.unity", display = "Unity Ads", tested = "3.21.1", group = SdkGroup.Mediation, source = SdkSource.OpenUpm, info = "AdMob mediation adapter: Unity Ads." },
            new Sdk { id = "com.google.ads.mobile.mediation.ironsource", display = "ironSource", tested = "4.7.0", group = SdkGroup.Mediation, source = SdkSource.OpenUpm, info = "AdMob mediation adapter: ironSource." },
            new Sdk { id = "com.google.ads.mobile.mediation.mintegral", display = "Mintegral", tested = "2.2.6", group = SdkGroup.Mediation, source = SdkSource.OpenUpm, info = "AdMob mediation adapter: Mintegral." },
            new Sdk { id = "com.google.ads.mobile.mediation.pangle", display = "Pangle", tested = "7.2.1", group = SdkGroup.Mediation, source = SdkSource.OpenUpm, info = "AdMob mediation adapter: Pangle." },

            new Sdk { id = "appsflyer-unity-plugin", display = "AppsFlyer", tested = "7.0.3", group = SdkGroup.Optional, source = SdkSource.AppsFlyerGit, defaultOn = true, info = "Attribution + ad revenue (github.com/AppsFlyerSDK, tag)." },
            new Sdk { id = "com.google.play.review", display = "In-App Review", tested = "1.8.4", group = SdkGroup.Optional, source = SdkSource.OpenUpm, defaultOn = true, info = "Google Play in-app review." },
            new Sdk { id = "com.google.play.appupdate", display = "In-App Update", tested = "1.8.5", group = SdkGroup.Optional, source = SdkSource.OpenUpm, defaultOn = true, info = "Google Play in-app update." },
            new Sdk { id = "com.unity.purchasing", display = "Unity IAP", tested = "5.4.4", group = SdkGroup.Optional, source = SdkSource.UnityRegistry, defaultOn = true, info = "Store, Remove Ads / VIP." },
            new Sdk { id = "com.google.firebase.app", display = "Firebase", tested = FirebaseInstaller.Pinned, group = SdkGroup.Optional, source = SdkSource.Firebase, defaultOn = true, info = "Analytics, Remote Config, Crashlytics (dl.google.com .tgz)." },
        };

        public static bool Checking { get; private set; }
        public static event Action Changed;

        /// <summary>Client.Add id of this SDK at a version (null for Firebase: FirebaseInstaller).</summary>
        public static string InstallId(Sdk s, string version)
        {
            switch (s.source)
            {
                case SdkSource.AppsFlyerGit: return $"https://github.com/AppsFlyerSDK/appsflyer-unity-plugin.git#v{version}";
                case SdkSource.Firebase:
                case SdkSource.AssetStore: return null;
                default: return $"{s.id}@{version}";
            }
        }

        private static string PrefKey(Sdk s) => $"Base.Sdk.{Path.GetFileName(Path.GetDirectoryName(Application.dataPath))}.{s.id}";

        /// <summary>Ticked for "Install required + selected" (required SDKs always).</summary>
        public const string DotweenStoreUrl = "https://assetstore.unity.com/packages/tools/animation/dotween-hotween-v2-27676";

        /// <summary>DOTween's version (DG.Tweening.DOTween.Version), or null without DOTween; pro = DOTween Pro is there too.</summary>
        public static string DotweenVersion(out bool pro)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            pro = assemblies.Any(a => a.GetName().Name == "DOTweenPro");
            var type = assemblies.Where(a => a.GetName().Name == "DOTween").Select(a => a.GetType("DG.Tweening.DOTween")).FirstOrDefault(t => t != null);
            if (type == null)
                return null;
            try { return type.GetField("Version")?.GetValue(null) as string ?? "?"; }
            catch { return "?"; }
        }

        public static bool Selected(Sdk s) => s.group == SdkGroup.Required || EditorPrefs.GetBool(PrefKey(s), s.defaultOn);
        public static void SetSelected(Sdk s, bool on) => EditorPrefs.SetBool(PrefKey(s), on);

        /// <summary>Asks the official sources for the newest version of every SDK, all at once: registries (OpenUPM,
        /// Unity) by UnityWebRequest, GitHub projects (AppsFlyer, Firebase) by the tags of their repository
        /// (git ls-remote, no GitHub API rate limit). Setup runs it once per editor session; "" = the check failed.</summary>
        public static void CheckLatest()
        {
            if (Checking) return;
            Checking = true;
            SessionState.SetBool("Base.Sdk.Checked", true);
            var pending = All.Select(s => (sdk: s, req: Request(s))).ToList();
            var tags = new Dictionary<Sdk, string>();
            bool tagsDone = false;
            var gitSdks = All.Where(s => GitRepo(s) != null).ToList();
            new Thread(() =>
            {
                foreach (var s in gitSdks)
                {
                    string latest = "";
                    try { latest = s.source == SdkSource.Firebase ? LatestFirebase() : LatestTag(GitRepo(s)); }
                    catch (Exception ex) { Debug.LogWarning($"[Base Setup] {s.display}: version check failed: {ex.Message}"); }
                    lock (tags) tags[s] = latest;
                }
                tagsDone = true;
            }) { IsBackground = true }.Start();
            void Poll()
            {
                if (!tagsDone || pending.Any(p => p.req != null && !p.req.isDone))
                    return;
                EditorApplication.update -= Poll;
                foreach (var (s, req) in pending)
                {
                    if (req == null)
                    {
                        s.latest = tags.TryGetValue(s, out var t) ? t : "-";   // "-": no official feed (Asset Store)
                        continue;
                    }
                    if (req.result != UnityWebRequest.Result.Success)
                        Debug.LogWarning($"[Base Setup] {s.display}: version check failed: {req.error}");
                    s.latest = req.result == UnityWebRequest.Result.Success ? Match(req.downloadHandler.text, @"""latest""\s*:\s*""([^""]+)""") : "";
                    req.Dispose();
                }
                Checking = false;
                Changed?.Invoke();
            }
            EditorApplication.update += Poll;
        }

        /// <summary>Checks once per editor session (Setup opening).</summary>
        public static void CheckLatestOnce()
        {
            if (!SessionState.GetBool("Base.Sdk.Checked", false))
                CheckLatest();
        }

        private static string GitRepo(Sdk s) =>
            s.source == SdkSource.AppsFlyerGit ? "https://github.com/AppsFlyerSDK/appsflyer-unity-plugin.git"
            : s.source == SdkSource.Firebase ? "https://github.com/firebase/firebase-unity-sdk.git" : null;

        /// <summary>Highest x.y.z (or vx.y.z) tag of a repository, without the "v".</summary>
        private static string LatestTag(string repo) => Tags(repo).FirstOrDefault() ?? "";

        /// <summary>x.y.z (or vx.y.z) tags of a repository, newest first.</summary>
        private static List<string> Tags(string repo) =>
            ZenUpdateChecker.Git($"ls-remote --tags --refs {repo}").Split('\n')
                .Select(l => Regex.Match(l, @"refs/tags/v?(\d+\.\d+\.\d+)\s*$")).Where(m => m.Success)
                .Select(m => m.Groups[1].Value).OrderByDescending(v => new Version(v)).ToList();

        /// <summary>
        /// Newest Firebase version that can really be installed: a firebase-unity-sdk tag can come before Google puts its
        /// .tgz on dl.google.com, so the newest tags are checked against the four archives Base installs (app, analytics,
        /// remote-config, crashlytics). Runs on the background thread of CheckLatest.
        /// </summary>
        private static string LatestFirebase()
        {
            foreach (var version in Tags(GitRepo(All.First(s => s.source == SdkSource.Firebase))).Take(5))
                if (FirebaseInstaller.Parts.All(part => UrlExists(FirebaseInstaller.Url(part, version))))
                    return version;
            return "";
        }

        private static bool UrlExists(string url)
        {
            try
            {
                var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
                request.Method = "HEAD";
                request.Timeout = 15000;
                using (var response = (System.Net.HttpWebResponse)request.GetResponse())
                    return response.StatusCode == System.Net.HttpStatusCode.OK;
            }
            catch (System.Net.WebException)
            {
                return false;   // 404: not published yet
            }
        }

        private static UnityWebRequest Request(Sdk s)
        {
            string url;
            switch (s.source)
            {
                case SdkSource.OpenUpm: url = "https://package.openupm.com/" + s.id; break;
                case SdkSource.UnityRegistry: url = "https://packages.unity.com/" + s.id; break;
                default: return null;   // git tags (AppsFlyer, Firebase) or no feed (DOTween)
            }
            var req = UnityWebRequest.Get(url);
            req.timeout = 20;
            req.SendWebRequest();
            return req;
        }

        private static string Match(string text, string pattern)
        {
            var m = Regex.Match(text, pattern);
            return m.Success ? m.Groups[1].Value : "";
        }

        /// <summary>Old .unitypackage installs (and the old Assets/Base) that clash with the Base packages.</summary>
        public static List<(string path, string what)> Legacy()
        {
            var list = new List<(string, string)>();
            void Add(string path, string what, Func<bool> when = null)
            {
                if (AssetDatabase.IsValidFolder(path) && (when == null || when()))
                    list.Add((path, what));
            }
            bool HasCode(string path) => Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories).Any(f => f.EndsWith(".cs") || f.EndsWith(".dll"));
            Add("Assets/GoogleMobileAds", "Google Mobile Ads .unitypackage (replaced by AdMob above)", () => HasCode("Assets/GoogleMobileAds"));
            Add("Assets/ExternalDependencyManager", "External Dependency Manager .unitypackage (replaced by the External Dependency Manager above)");
            Add("Assets/Firebase", "Firebase .unitypackage (replaced by Firebase above)", () => HasCode("Assets/Firebase"));
            Add("Assets/AppsFlyer", "AppsFlyer .unitypackage (replaced by AppsFlyer above)", () => HasCode("Assets/AppsFlyer"));
            Add("Assets/GooglePlayPlugins", "Google Play plugins .unitypackage (replaced by In-App Review / Update above)");
            Add("Assets/Base", "Old Base copy (com.zen.base 1.0, replaced by Base Core)");
            return list;
        }
    }
}
