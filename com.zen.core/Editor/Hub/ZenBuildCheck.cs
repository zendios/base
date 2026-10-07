using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Base.Ads;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Base.Setup
{
    /// <summary>
    /// What would make a store build lose revenue or crash at start: test IDs left on, missing / malformed / foreign ad
    /// unit IDs, no AdMob App ID, Firebase without google-services.json. Base > Hub > Build Check shows it; a release
    /// (non-development) build stops on the errors.
    /// </summary>
    public class ZenBuildCheck : IPreprocessBuildWithReport
    {
        public const string GoogleTestPublisher = "ca-app-pub-3940256099942544";
        private static readonly Regex UnitId = new Regex(@"^ca-app-pub-\d{16}/\d{10}$");

        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            bool release = (report.summary.options & BuildOptions.Development) == 0;
            var issues = Run(release);
            foreach (var i in issues.Where(i => i.type != MessageType.Error))
                Debug.LogWarning("[Base Build Check] " + i.message);
            var errors = issues.Where(i => i.type == MessageType.Error).ToList();
            foreach (var e in errors)
                Debug.LogError("[Base Build Check] " + e.message);
            if (release && errors.Count > 0)
                throw new BuildFailedException($"Base Build Check: {errors.Count} error(s) for a release build (Base > Hub > Build Check). Make a Development Build to test anyway.");
        }

        /// <summary>All checks; release = the problems that must not reach the store are errors.</summary>
        public static List<ZenSetupIssue> Run(bool release)
        {
            var issues = new List<ZenSetupIssue>();
            MessageType severe = release ? MessageType.Error : MessageType.Warning;

            var config = Resources.Load<AdConfig>(AdConfig.ResourcePath);
            if (config != null && new SerializedObject(config).FindProperty("useIdTest").boolValue)
                issues.Add(new ZenSetupIssue { type = severe, message = "AdConfig.useIdTest is ON: every placement loads Google test ads (no revenue).", fixLabel = "Turn off", fix = () => SetUseIdTest(config, false) });

            string appId = AdMobAppId(out var gmaSettings);
            if (string.IsNullOrEmpty(appId))
                issues.Add(new ZenSetupIssue { type = MessageType.Error, message = "No AdMob Android App ID (Assets > Google Mobile Ads > Settings): the app crashes at start.", fixLabel = gmaSettings != null ? "Open" : null, fix = gmaSettings != null ? () => Selection.activeObject = gmaSettings : (Action)null });
            else if (appId.StartsWith(GoogleTestPublisher))
                issues.Add(new ZenSetupIssue { type = severe, message = $"The AdMob App ID {appId} is Google's test app." });

            var ids = Resources.Load<ZenAdIds>(ZenAdIds.ResourcePath);
            if (ids == null)
                issues.Add(new ZenSetupIssue { type = severe, message = "No ZenAdIds in Resources: every placement loads Google test IDs." });
            else
                issues.AddRange(CheckIds(ids, appId, severe));

            bool firebase = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Firebase.App");
            if (firebase && !Directory.GetFiles("Assets", "google-services.json", SearchOption.AllDirectories).Any())
                issues.Add(new ZenSetupIssue { type = MessageType.Error, message = "Firebase is installed but there is no google-services.json under Assets." });
            return issues;
        }

        /// <summary>Per-ID problems of ZenAdIds (also drawn in the Ad IDs tab).</summary>
        public static List<ZenSetupIssue> CheckIds(ZenAdIds ids, string appId, MessageType severe)
        {
            var issues = new List<ZenSetupIssue>();
            string publisher = string.IsNullOrEmpty(appId) ? null : appId.Split('~')[0];
            var seen = new Dictionary<string, PlacementType>();
            foreach (var e in ids.entries)
            {
                foreach (var (list, platform) in new[] { (e.android, "Android"), (e.ios, "iOS") })
                    foreach (var raw in list.Where(x => !string.IsNullOrWhiteSpace(x)))
                    {
                        string id = raw.Trim();
                        if (id.StartsWith(GoogleTestPublisher))
                            issues.Add(new ZenSetupIssue { type = severe, message = $"{e.placement} ({platform}): {id} is a Google test ID." });
                        else if (!UnitId.IsMatch(id))
                            issues.Add(new ZenSetupIssue { type = MessageType.Error, message = $"{e.placement} ({platform}): \"{id}\" is not an ad unit ID (ca-app-pub-XXXXXXXXXXXXXXXX/XXXXXXXXXX)." });
                        else if (platform == "Android" && publisher != null && !publisher.StartsWith(GoogleTestPublisher) && !id.StartsWith(publisher + "/"))
                            issues.Add(new ZenSetupIssue { type = severe, message = $"{e.placement}: {id} belongs to another AdMob account than the App ID {appId}." });
                        string key = platform + id;
                        if (seen.TryGetValue(key, out var other) && other != e.placement)
                            issues.Add(new ZenSetupIssue { type = MessageType.Warning, message = $"{id} is used by {other} and {e.placement} ({platform}): their reports mix." });
                        seen[key] = e.placement;
                    }
            }
            var empty = ids.entries.Where(e => e.android.All(string.IsNullOrWhiteSpace)).Select(e => e.placement.ToString()).ToList();
            if (empty.Count > 0)
                issues.Add(new ZenSetupIssue { type = MessageType.Info, message = $"No Android ID (test ads if the game uses them): {string.Join(", ", empty)}." });
            return issues;
        }

        /// <summary>The AdMob Android App ID from the Google Mobile Ads settings asset.</summary>
        public static string AdMobAppId(out UnityEngine.Object settings)
        {
            settings = AssetDatabase.FindAssets("GoogleMobileAdsSettings t:ScriptableObject").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ScriptableObject>).FirstOrDefault(s => s != null);
            if (settings == null)
                return null;
            return new SerializedObject(settings).FindProperty("adMobAndroidAppId")?.stringValue?.Trim();
        }

        public static void SetUseIdTest(AdConfig config, bool on)
        {
            var so = new SerializedObject(config);
            so.FindProperty("useIdTest").boolValue = on;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }
    }
}
