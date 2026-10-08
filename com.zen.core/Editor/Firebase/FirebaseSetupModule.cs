using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Base.Setup;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace Base
{
    /// <summary>Firebase: the Firebase SDK packages and google-services.json.</summary>
    internal class FirebaseSetupModule : IZenSetupModule
    {
        public string Name => "Firebase";
        public int Order => 30;

        public IEnumerable<ZenSetupIssue> Check()
        {
            bool sdk = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Firebase.App");
            if (!sdk)
                yield return new ZenSetupIssue
                {
                    message = $"Firebase SDK not installed (optional): Install downloads Firebase {FirebaseInstaller.Pinned} from Google. Until then analytics and Remote Config do nothing.",
                    type = MessageType.Info,
                    fixLabel = "Install",
                    fix = () => FirebaseInstaller.Install(FirebaseInstaller.Pinned),
                };
            if (sdk && !Directory.GetFiles("Assets", "google-services.json", SearchOption.AllDirectories).Any())
                yield return new ZenSetupIssue
                {
                    message = "No google-services.json under Assets (download it from the Firebase console for this app's package name).",
                    type = MessageType.Error,
                };
        }
    }

    /// <summary>
    /// Installs the Firebase Unity SDK (app, analytics, remote-config, crashlytics) from Google's official package
    /// archive (dl.google.com/games/registry/unity): the .tgz go into Packages/ with file: entries in manifest.json.
    /// Base pins one tested version (Pinned); BaseSdks.CheckLatest reads the official firebase/firebase-unity-sdk tags
    /// for a newer one, which the Setup tab offers as an Update.
    /// </summary>
    internal static class FirebaseInstaller
    {
        public const string Pinned = "13.17.0";
        private static readonly string[] Parts = { "app", "analytics", "remote-config", "crashlytics" };
        private const string ManifestPath = "Packages/manifest.json";

        /// <summary>Raised after an install changed manifest.json.</summary>
        public static event Action Changed;

        private static string Url(string part, string version) =>
            $"https://dl.google.com/games/registry/unity/com.google.firebase.{part}/com.google.firebase.{part}-{version}.tgz";

        /// <summary>Downloads the 4 packages of this version into Packages/, points manifest.json at them, removes the
        /// other versions' .tgz and resolves.</summary>
        public static void Install(string version)
        {
            if (!EditorUtility.DisplayDialog("Firebase", $"Download Firebase {version} (app, analytics, remote-config, crashlytics, about 65 MB) from dl.google.com into Packages/ and use it?", "Download", "Cancel"))
                return;
            try
            {
                foreach (var part in Parts)
                {
                    string file = $"Packages/com.google.firebase.{part}-{version}.tgz";
                    if (File.Exists(file))
                        continue;
                    using (var req = UnityWebRequest.Get(Url(part, version)))
                    {
                        req.downloadHandler = new DownloadHandlerFile(file + ".part") { removeFileOnAbort = true };
                        var op = req.SendWebRequest();
                        while (!op.isDone)
                        {
                            if (EditorUtility.DisplayCancelableProgressBar("Firebase", $"com.google.firebase.{part} {version}", req.downloadProgress))
                            {
                                req.Abort();
                                Debug.Log("[Base Setup] Firebase download cancelled");
                                return;
                            }
                            Thread.Sleep(50);
                        }
                        if (req.result != UnityWebRequest.Result.Success)
                            throw new Exception($"{Url(part, version)}: {req.error}");
                    }
                    File.Move(file + ".part", file);
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Firebase", "Download failed: " + ex.Message, "OK");
                return;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            HubSetup.EnsureOpenUpm();   // Firebase depends on com.google.external-dependency-manager (OpenUPM)
            string json = File.ReadAllText(ManifestPath);
            foreach (var part in Parts)
            {
                string name = "com.google.firebase." + part, value = $"file:{name}-{version}.tgz";
                var entry = new Regex("\"" + Regex.Escape(name) + "\"\\s*:\\s*\"[^\"]*\"");
                json = entry.IsMatch(json)
                    ? entry.Replace(json, $"\"{name}\": \"{value}\"", 1)
                    : new Regex("\"dependencies\"\\s*:\\s*\\{").Replace(json, m => m.Value + $"\n    \"{name}\": \"{value}\",", 1);
            }
            File.WriteAllText(ManifestPath, json);
            foreach (var part in Parts)
                foreach (var old in Directory.GetFiles("Packages", $"com.google.firebase.{part}-*.tgz").Where(f => !f.EndsWith($"-{version}.tgz")))
                    File.Delete(old);
            Debug.Log($"[Base Setup] Firebase {version} installed from dl.google.com (Packages/*.tgz, manifest.json)");
            Client.Resolve();
            Changed?.Invoke();
        }
    }
}
