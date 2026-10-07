#if UNITY_ANDROID
using System;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Base.Ads
{
    /// <summary>
    /// Build-time fixes that make native VIDEO work inside the Unity activity:
    ///  1. android:hardwareAccelerated="true" on every activity + application
    ///     (Unity's default manifest disables it; MediaView video needs it).
    ///  2. AdMob APPLICATION_ID meta-data (required by the UMP consent SDK).
    ///  3. Sanity checks: minSdk 24, legacy Google Mobile Ads Unity plugin conflict.
    /// </summary>
    public sealed class AdZativeAndroidBuild : IPostGenerateGradleAndroidProject, IPreprocessBuildWithReport
    {
        private const string AndroidNs = "http://schemas.android.com/apk/res/android";
        public int callbackOrder => 999;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;
            if ((int)PlayerSettings.Android.minSdkVersion < 24)
                throw new BuildFailedException("[AdZativeSDK] GMA Next-Gen SDK requires Minimum API Level 24. " +
                                               "Player Settings ▸ Other Settings ▸ Minimum API Level.");

            if (HostHasGmaUnity)
                Debug.Log("[AdZativeSDK] Google Mobile Ads Unity plugin detected: it owns the SDK dependency and the " +
                          "APPLICATION_ID; make sure it builds with the Next-Gen SDK (GMA Unity >= 11.x, NextGen).");
        }

        /// <summary>
        /// The game also uses the Google Mobile Ads Unity plugin (e.g. zen-unity Base): that plugin writes the real
        /// APPLICATION_ID and the SDK dependency, so this post-processor must never touch them.
        /// </summary>
        private static bool HostHasGmaUnity => AppDomain.CurrentDomain.GetAssemblies()
            .Any(a => a.GetType("GoogleMobileAds.Api.MobileAds", false) != null);

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            var settings = Resources.Load<AdZativeSettings>(AdZativeSettings.ResourcePath);
            bool host = HostHasGmaUnity;
            string appId = host ? null : settings != null ? settings.androidAppId : AdZativeTestIds.AndroidAppId;

            Patch(Path.Combine(unityLibraryPath, "src/main/AndroidManifest.xml"), appId);
            string launcher = Path.GetFullPath(Path.Combine(unityLibraryPath, "../launcher/src/main/AndroidManifest.xml"));
            if (File.Exists(launcher)) Patch(launcher, null);
            if (!host) AddSdkDependency(Path.Combine(unityLibraryPath, "build.gradle"));
        }

        /// <summary>
        /// Adds GMA Next-Gen SDK to unityLibrary when EDM4U has not already done it,
        /// so the project builds without any extra tooling.
        /// </summary>
        private static void AddSdkDependency(string gradlePath)
        {
            if (!File.Exists(gradlePath)) return;
            string text = File.ReadAllText(gradlePath);
            if (text.Contains("ads-mobile-sdk")) return;
            int idx = text.IndexOf("dependencies {", StringComparison.Ordinal);
            if (idx < 0) return;
            idx += "dependencies {".Length;
            text = text.Insert(idx, "\n    implementation 'com.google.android.libraries.ads.mobile.sdk:ads-mobile-sdk:" + SdkVersion + "'");
            File.WriteAllText(gradlePath, text);
        }

        public const string SdkVersion = "1.4.0";

        private static void Patch(string manifestPath, string appId)
        {
            if (!File.Exists(manifestPath)) return;
            var doc = new XmlDocument();
            doc.Load(manifestPath);
            var app = doc.SelectSingleNode("/manifest/application") as XmlElement;
            if (app == null)
            {
                app = doc.CreateElement("application");
                doc.DocumentElement.AppendChild(app);
            }
            app.SetAttribute("hardwareAccelerated", AndroidNs, "true");

            foreach (XmlElement activity in app.SelectNodes("activity"))
                activity.SetAttribute("hardwareAccelerated", AndroidNs, "true");

            if (!string.IsNullOrEmpty(appId))
            {
                const string key = "com.google.android.gms.ads.APPLICATION_ID";
                XmlElement meta = null;
                foreach (XmlElement m in app.SelectNodes("meta-data"))
                    if (m.GetAttribute("name", AndroidNs) == key) meta = m;
                if (meta == null)   // never overwrite an app ID written by the game / another plugin
                {
                    meta = doc.CreateElement("meta-data");
                    app.AppendChild(meta);
                    meta.SetAttribute("name", AndroidNs, key);
                    meta.SetAttribute("value", AndroidNs, appId);
                }
            }
            doc.Save(manifestPath);
        }
    }
}
#endif
