using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEditor;
using UnityEditor.PackageManager;
using Debug = UnityEngine.Debug;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Base.Setup
{
    /// <summary>
    /// Tells a game that a newer Base Core exists and lets it go back to an older one. Once a day (and from
    /// Base > Hub > Setup > Check for updates) it lists the zen/* tags of the repository with the machine's git (the
    /// same access that installs the package) and reads the CHANGELOG.md of the newest tag; Base > Hub > Setup shows
    /// it with an Update button and a version list (rollback). Switching moves the manifest's com.zen.core /
    /// com.zen.plugins.adzative entries to that version: the zen/x.y.z tag for a git URL install, the version itself for an
    /// OpenUPM install (versions from package.openupm.com, notes from the public github.com/zendios/base). Embedded
    /// (development) copies are never checked.
    /// </summary>
    [InitializeOnLoad]
    public static class ZenUpdateChecker
    {
        public const string RepoUrl = "https://github.com/zendios/zen.git";
        private const string ChangelogPath = "zen-unity/Packages/com.zen.core/CHANGELOG.md";
        private static readonly string[] ZenPackages = { "com.zen.core", "com.zen.plugins.adzative" };
        private static string LastCheckKey => "Zen.UpdateCheck.Last." + PlayerSettingsKey;
        private static string PlayerSettingsKey => Path.GetFileName(Path.GetDirectoryName(UnityEngine.Application.dataPath));

        public enum State { Idle, Checking, UpToDate, UpdateAvailable, Failed, Development }

        public static State Status { get; private set; } = State.Idle;
        public static string Installed { get; private set; }
        public static string Latest { get; private set; }
        /// <summary>Every released version (zen/* tags), newest first; empty before a check.</summary>
        public static List<string> Versions { get; private set; } = new List<string>();
        /// <summary>CHANGELOG sections newer than the installed version.</summary>
        public static string Changes { get; private set; }
        /// <summary>CHANGELOG.md of the newest tag (notes of every version).</summary>
        public static string Changelog { get; private set; }
        public static string Error { get; private set; }

        /// <summary>Raised on the main thread when a check finishes.</summary>
        public static event Action Changed;

        static ZenUpdateChecker()
        {
            EditorApplication.delayCall += () =>
            {
                long last = long.TryParse(EditorPrefs.GetString(LastCheckKey, "0"), out var t) ? t : 0;
                if (DateTime.UtcNow.Ticks - last > TimeSpan.FromHours(24).Ticks)
                    Check(false);
            };
        }

        public static void Check(bool manual)
        {
            var self = PackageInfo.FindForAssembly(typeof(ZenUpdateChecker).Assembly);
            Installed = self?.version;
            if (self == null || self.source == PackageSource.Embedded || self.source == PackageSource.Local || self.source == PackageSource.LocalTarball)
            {
                Status = State.Development;
                Changed?.Invoke();
                if (manual)
                    Debug.Log("[Base] Base Core is embedded / local here (development copy): no update check.");
                return;
            }
            if (Status == State.Checking)
                return;
            Status = State.Checking;
            EditorPrefs.SetString(LastCheckKey, DateTime.UtcNow.Ticks.ToString());
            if (self.source == PackageSource.Registry)
            {
                CheckRegistry(manual);
                return;
            }
            string installed = Installed;
            new Thread(() =>
            {
                List<string> versions = null;
                string changelog = null, error = null;
                try
                {
                    versions = Tags();
                    if (versions.Count > 0)
                        changelog = ReadChangelog(versions[0]);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
                EditorApplication.delayCall += () => Finish(versions, changelog, error, manual);
            }) { IsBackground = true }.Start();
        }

        /// <summary>OpenUPM install: versions from the registry, notes from the public repository's CHANGELOG.</summary>
        private static void CheckRegistry(bool manual)
        {
            Fetch(OpenUpmUrl, (text, error) =>
            {
                if (error != null)
                {
                    Finish(null, null, error, manual);
                    return;
                }
                var versions = Regex.Matches(text, @"""version""\s*:\s*""(\d+\.\d+\.\d+)""").Cast<Match>()
                    .Select(m => m.Groups[1].Value).Distinct().OrderByDescending(v => new Version(v)).ToList();
                if (versions.Count == 0)
                {
                    Finish(versions, null, null, manual);
                    return;
                }
                Fetch($"{PublicRaw}/{versions[0]}/com.zen.core/CHANGELOG.md", (changelog, _) => Finish(versions, changelog, null, manual));
            });
        }

        private const string OpenUpmUrl = "https://package.openupm.com/com.zen.core";
        private const string PublicRaw = "https://raw.githubusercontent.com/zendios/base";

        /// <summary>GET on the main thread (UnityWebRequest), then done(text, error).</summary>
        private static void Fetch(string url, Action<string, string> done)
        {
            var req = UnityEngine.Networking.UnityWebRequest.Get(url);
            req.timeout = 20;
            req.SendWebRequest();
            void Poll()
            {
                if (!req.isDone) return;
                EditorApplication.update -= Poll;
                bool ok = req.result == UnityEngine.Networking.UnityWebRequest.Result.Success;
                string text = ok ? req.downloadHandler.text : null, error = ok ? null : $"{url}: {req.error}";
                req.Dispose();
                done(text, error);
            }
            EditorApplication.update += Poll;
        }

        private static void Finish(List<string> versions, string changelog, string error, bool manual)
        {
            Versions = versions ?? new List<string>();
            Latest = Versions.FirstOrDefault();
            Changelog = changelog;
            Changes = ChangesSince(changelog, Installed);
            Error = error;
            Status = error != null ? State.Failed : Latest != null && Newer(Latest, Installed) ? State.UpdateAvailable : State.UpToDate;
            if (Status == State.UpdateAvailable)
            {
                Debug.LogWarning($"[Base] Base Core {Latest} is available (this game has {Installed}). Base > Hub > Setup > Update.");
                ZenHub.Open("Setup");
            }
            else if (Status == State.Failed)
                Debug.LogWarning($"[Base] Update check failed: {error}");
            else if (manual)
                Debug.Log($"[Base] Base Core {Installed} is the latest version.");
            Changed?.Invoke();
        }

        /// <summary>Moves com.zen.core and com.zen.plugins.adzative in Packages/manifest.json to this version (a newer
        /// version = update, an older one = rollback): the git tag zen/{version} for a git URL entry, the version itself
        /// for an OpenUPM entry.</summary>
        public static void UpdateTo(string version)
        {
            const string manifestPath = "Packages/manifest.json";
            string json = File.ReadAllText(manifestPath);
            foreach (var name in ZenPackages)
                json = Regex.Replace(json, "(\"" + Regex.Escape(name) + "\"\\s*:\\s*\")([^\"#]*)(#[^\"]*)?\"",
                    m => m.Groups[1].Value + (!(m.Groups[2].Value.Contains("://") || m.Groups[2].Value.EndsWith(".git")) ? version
                        : m.Groups[2].Value + (m.Groups[2].Value.Contains("zendios/base") ? "#" : "#zen/") + version) + "\"");   // public repo tags: x.y.z
            File.WriteAllText(manifestPath, json);
            Debug.Log($"[Base] manifest.json: Base Core -> {version}");
            Client.Resolve();
        }

        /// <summary>The CHANGELOG section of one version ("## [1.2.3] ..." up to the next section), or null.</summary>
        public static string Notes(string version)
        {
            if (string.IsNullOrEmpty(Changelog))
                return null;
            int start = Changelog.IndexOf($"## [{version}]", StringComparison.Ordinal);
            if (start < 0)
                return null;
            int end = Changelog.IndexOf("\n## [", start + 1, StringComparison.Ordinal);
            return (end > start ? Changelog.Substring(start, end - start) : Changelog.Substring(start)).Trim();
        }

        private static List<string> Tags()
        {
            string output = Git($"ls-remote --tags --refs {RepoUrl} \"refs/tags/zen/*\"");
            return output.Split('\n')
                .Select(l => Regex.Match(l, @"refs/tags/zen/(\d+\.\d+\.\d+)\s*$"))
                .Where(m => m.Success)
                .Select(m => m.Groups[1].Value)
                .OrderByDescending(v => new Version(v))
                .ToList();
        }

        /// <summary>CHANGELOG.md of a tag (blob-only partial clone, no checkout).</summary>
        private static string ReadChangelog(string version)
        {
            string dir = Path.Combine(Path.GetTempPath(), "zen-changelog-" + Guid.NewGuid().ToString("N"));
            try
            {
                Git($"clone --quiet --depth 1 --branch zen/{version} --filter=blob:none --no-checkout {RepoUrl} \"{dir}\"");
                return Git($"-C \"{dir}\" show HEAD:{ChangelogPath}");
            }
            finally
            {
                try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
            }
        }

        /// <summary>The CHANGELOG sections above the installed version's.</summary>
        private static string ChangesSince(string text, string installed)
        {
            if (string.IsNullOrEmpty(text))
                return null;
            int start = text.IndexOf("## [", StringComparison.Ordinal);
            int end = text.IndexOf($"## [{installed}]", StringComparison.Ordinal);
            if (start < 0)
                return null;
            return (end > start ? text.Substring(start, end - start) : text.Substring(start)).Trim();
        }

        private static bool Newer(string a, string b) =>
            Version.TryParse(a, out var va) && (!Version.TryParse(b ?? "", out var vb) || va > vb);

        internal static string Git(string args, string workingDir = null, int timeoutMs = 60000)
        {
            var info = new ProcessStartInfo(File.Exists("/usr/bin/git") ? "/usr/bin/git" : "git", args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            if (workingDir != null)
                info.WorkingDirectory = workingDir;
            info.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";   // never hang on a password prompt
            using (var p = Process.Start(info))
            {
                var error = p.StandardError.ReadToEndAsync();
                string output = p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(); } catch { }
                    throw new Exception("git timed out");
                }
                if (p.ExitCode != 0)
                    throw new Exception($"git {args.Split(' ')[0]}: {error.Result.Trim()}");
                return output;
            }
        }
    }
}
