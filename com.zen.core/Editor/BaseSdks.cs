using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.Networking;
using System.Text.RegularExpressions;
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
    /// Mobile Ads, its mediation adapters, EDM4U and the Play plugins from OpenUPM (where Google publishes them),
    /// AppsFlyer from its GitHub repository (tag), Unity IAP from the Unity registry, Firebase from dl.google.com
    /// (FirebaseInstaller). Base > Hub > Setup installs them, offers the tested version back, and shows newer ones
    /// (checked once per editor session).
    /// </summary>
    internal static class BaseSdks
    {
        public static readonly Sdk[] All =
        {
            new Sdk { id = "com.google.ads.mobile", display = "AdMob", tested = "11.5.0", group = SdkGroup.Required, source = SdkSource.OpenUpm, info = "Google Mobile Ads for Unity." },
            new Sdk { id = "com.google.external-dependency-manager", display = "EDM4U", tested = "1.2.189", group = SdkGroup.Required, source = SdkSource.OpenUpm, info = "External Dependency Manager: resolves the Android / iOS libraries of the SDKs." },
            new Sdk { id = "dotween", display = "DOTween", tested = "1.2.825", group = SdkGroup.Required, source = SdkSource.AssetStore, info = "DOTween (free, Asset Store) or DOTween Pro (paid, added by hand) in Assets/Plugins/Demigiant. Not a package: Base cannot install it." },

            new Sdk { id = "com.google.ads.mobile.mediation.metaaudiencenetwork", display = "Meta", tested = "3.20.1", group = SdkGroup.Mediation, source = SdkSource.OpenUpm, defaultOn = true, info = "AdMob mediation adapter: Meta Audience Network." },
            new Sdk { id = "com.google.ads.mobile.mediation.liftoffmonetize", display = "Liftoff", tested = "5.7.9", group = SdkGroup.Mediation, source = SdkSource.OpenUpm, info = "AdMob mediation adapter: Liftoff Monetize (Vungle)." },
            new Sdk { id = "com.google.ads.mobile.mediation.applovin", display = "AppLovin", tested = "8.7.7", group = SdkGroup.Mediation, source = SdkSource.OpenUpm, info = "AdMob mediation adapter: AppLovin." },
            new Sdk { id = "com.google.ads.mobile.mediation.unity", display = "Unity Ads", tested = "3.21.1", group = SdkGroup.Mediation, source = SdkSource.OpenUpm, info = "AdMob mediation adapter: Unity Ads." },
            new Sdk { id = "com.google.ads.mobile.mediation.ironsource", display = "ironSource", tested = "4.7.0", group = SdkGroup.Mediation, source = SdkSource.OpenUpm, info = "AdMob mediation adapter: ironSource." },
            new Sdk { id = "com.google.ads.mobile.mediation.mintegral", display = "Mintegral", tested = "2.2.6", group = SdkGroup.Mediation, source = SdkSource.OpenUpm, info = "AdMob mediation adapter: Mintegral." },
            new Sdk { id = "com.google.ads.mobile.mediation.pangle", display = "Pangle", tested = "7.2.1", group = SdkGroup.Mediation, source = SdkSource.OpenUpm, info = "AdMob mediation adapter: Pangle." },

            new Sdk { id = "appsflyer-unity-plugin", display = "AppsFlyer", tested = "6.17.900", group = SdkGroup.Optional, source = SdkSource.AppsFlyerGit, defaultOn = true, info = "Attribution + ad revenue (github.com/AppsFlyerSDK, tag)." },
            new Sdk { id = "com.google.play.review", display = "In-App Review", tested = "1.8.4", group = SdkGroup.Optional, source = SdkSource.OpenUpm, defaultOn = true, info = "Google Play in-app review." },
            new Sdk { id = "com.google.play.appupdate", display = "In-App Update", tested = "1.8.5", group = SdkGroup.Optional, source = SdkSource.OpenUpm, defaultOn = true, info = "Google Play in-app update." },
            new Sdk { id = "com.unity.purchasing", display = "Unity IAP", tested = "5.4.3", group = SdkGroup.Optional, source = SdkSource.UnityRegistry, defaultOn = true, info = "Store, Remove Ads / VIP." },
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

        /// <summary>Asks the official sources for the newest version of every SDK (UnityWebRequest, all at once).
        /// Setup runs it once per editor session; "" = the check failed for that SDK.</summary>
        public static void CheckLatest()
        {
            if (Checking) return;
            Checking = true;
            SessionState.SetBool("Base.Sdk.Checked", true);
            var pending = All.Select(s => (sdk: s, req: Request(s))).ToList();
            void Poll()
            {
                if (pending.Any(p => p.req != null && !p.req.isDone))
                    return;
                EditorApplication.update -= Poll;
                foreach (var (s, req) in pending)
                {
                    if (req == null) { s.latest = "-"; continue; }   // no official feed to ask (Asset Store)
                    if (req.result != UnityWebRequest.Result.Success)
                        Debug.LogWarning($"[Base Setup] {s.display}: version check failed: {req.error}");
                    s.latest = req.result == UnityWebRequest.Result.Success ? Parse(s, req.downloadHandler.text) : "";
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

        private static UnityWebRequest Request(Sdk s)
        {
            string url;
            switch (s.source)
            {
                case SdkSource.OpenUpm: url = "https://package.openupm.com/" + s.id; break;
                case SdkSource.UnityRegistry: url = "https://packages.unity.com/" + s.id; break;
                case SdkSource.AppsFlyerGit: url = "https://api.github.com/repos/AppsFlyerSDK/appsflyer-unity-plugin/releases/latest"; break;
                case SdkSource.Firebase: url = "https://api.github.com/repos/firebase/firebase-unity-sdk/releases/latest"; break;
                default: return null;
            }
            var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("User-Agent", "Base-Hub");   // GitHub API needs one
            req.timeout = 20;
            req.SendWebRequest();
            return req;
        }

        private static string Parse(Sdk s, string json) =>
            s.source == SdkSource.AppsFlyerGit || s.source == SdkSource.Firebase
                ? Match(json, @"""tag_name""\s*:\s*""v?([^""]+)""")
                : Match(json, @"""latest""\s*:\s*""([^""]+)""");

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
            Add("Assets/ExternalDependencyManager", "External Dependency Manager .unitypackage (replaced by EDM4U above)");
            Add("Assets/Firebase", "Firebase .unitypackage (replaced by Firebase above)", () => HasCode("Assets/Firebase"));
            Add("Assets/AppsFlyer", "AppsFlyer .unitypackage (replaced by AppsFlyer above)", () => HasCode("Assets/AppsFlyer"));
            Add("Assets/GooglePlayPlugins", "Google Play plugins .unitypackage (replaced by In-App Review / Update above)");
            Add("Assets/Base", "Old Base copy (com.zen.base 1.0, replaced by Base Core)");
            return list;
        }
    }
}
