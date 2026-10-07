using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Base.Setup
{
    /// <summary>
    /// Release (zen-unity development project only): writes the version's notes into CHANGELOG.md, then runs
    /// tools/zen-release.sh (Base.dll build, samples / templates refresh, package version, commit, local tag zen/x.y.z).
    /// Publish (tools/base-publish.sh) pushes them and mirrors both packages into the public github.com/zendios/base
    /// (tag x.y.z, built by OpenUPM), after which games see the version in Setup.
    /// </summary>
    internal class HubRelease : IZenHubTab
    {
        public string Title => "Release";

        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        private static string Script => Path.Combine(Repo, "tools/zen-release.sh");
        private const string Changelog = "zen-unity/Packages/com.zen.core/CHANGELOG.md";

        /// <summary>Shown only where com.zen.core is embedded and the repository's release script is next to it.</summary>
        public static bool Available
        {
            get
            {
                var self = PackageInfo.FindForAssembly(typeof(HubRelease).Assembly);
                return self != null && self.source == PackageSource.Embedded && File.Exists(Script);
            }
        }

        private string current, version, notes, since;
        private List<Change> changes = new List<Change>();
        private List<string> pending = new List<string>();
        private Vector2 scroll, logScroll, changesScroll;
        private readonly StringBuilder log = new StringBuilder();
        private static volatile bool busy;
        internal static bool Running => busy;
        private string released;   // version tagged by the last Create in this session (Push target)

        private class Change
        {
            public string subject;
            public bool on;
        }

        /// <summary>Housekeeping commits, unticked by default.</summary>
        private static readonly Regex Noise = new Regex(@"^(Stop tracking|Merge|Revert|WIP|Typo|Fix typo|Format|Cleanup|Clean up|Bump|Zen packages)", RegexOptions.IgnoreCase);

        public void OnEnable()
        {
            current = PackageInfo.FindForAssembly(typeof(HubRelease).Assembly)?.version ?? "0.0.0";
            if (version == null)
                version = Version.TryParse(current, out var v) ? $"{v.Major}.{v.Minor}.{v.Build + 1}" : current;
            changes = Commits(current, out since);
            pending = Pending();
            if (notes == null)
            {
                notes = Unreleased(File.ReadAllText(Path.Combine(Repo, Changelog)));
                if (string.IsNullOrWhiteSpace(notes))
                    notes = Format(changes.Where(c => c.on).Select(c => c.subject));
            }
        }

        /// <summary>The commits that changed the packages since the release of this version (its tag zen/x.y.z, or its
        /// "Zen packages x.y.z" commit when the tag is not fetched), oldest first; housekeeping ones unticked.</summary>
        private static List<Change> Commits(string version, out string since)
        {
            since = null;
            try
            {
                string tag = $"zen/{version}";
                string bas = TryGit($"rev-parse --verify --quiet {tag}^{{commit}}");
                since = bas != null ? tag : null;
                if (bas == null)
                {
                    bas = TryGit($"log -1 --format=%H \"--grep=^Zen packages {Regex.Escape(version)}$\"");
                    since = bas != null ? $"the {version} release" : null;
                }
                string range = bas != null ? $"{bas}..HEAD" : "-50 HEAD";
                return ZenUpdateChecker.Git($"log --no-merges --reverse --format=%s {range} -- zen-unity/Packages zen-base tools", Repo)
                    .Split('\n').Select(l => l.Trim().Trim('﻿'))
                    .Where(l => l.Length > 0 && !l.StartsWith("Zen packages "))
                    .Select(l => new Change { subject = l, on = !Noise.IsMatch(l) })
                    .ToList();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[Base Hub] release notes: " + ex.Message);
                return new List<Change>();
            }
        }

        /// <summary>Real (content) changes not committed yet under zen-unity/Packages and zen-base, which the release
        /// script commits too; line-ending-only differences do not count.</summary>
        private static List<string> Pending()
        {
            const string paths = "-- zen-unity/Packages zen-base";
            string changed = TryGit($"diff --name-only HEAD {paths}") ?? "";
            string added = TryGit($"ls-files --others --exclude-standard {paths}") ?? "";
            return (changed + "\n" + added).Split('\n').Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.EndsWith("CHANGELOG.md")).Distinct().ToList();
        }

        private static string TryGit(string args)
        {
            try
            {
                string s = ZenUpdateChecker.Git(args, Repo).Trim();
                return s.Length > 0 ? s : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// Commit subjects -> CHANGELOG lines grouped by area (the part before ": ", old "Zen >" names read "Base >"):
        /// "- Area: point" for one point, "- Area:" + "  - point" lines for several, "- point" without an area.
        /// </summary>
        private static string Format(IEnumerable<string> subjects)
        {
            var groups = new List<(string area, List<string> points)>();
            foreach (var s in subjects)
            {
                // "Base > Hub > Setup > SDKs: x" and "Setup > SDKs: x" both go under "Setup" as "SDKs: x"
                string text = Regex.Replace(s.Trim(), @"^(Zen|Base) > Hub > ", "");
                text = Regex.Replace(text, @"^(Zen|Base) > Hub: ", "Hub: ");
                text = Regex.Replace(text, @"^(Zen|Base) > ", "Base > ");
                int colon = text.IndexOf(": ", StringComparison.Ordinal);
                // an area is a short name path ("Setup > SDKs", "Ad IDs"), not a sentence ("Hub is the only menu")
                bool hasArea = colon > 0 && colon <= 40 && text.Substring(0, colon).Split(new[] { " > " }, StringSplitOptions.None)
                    .All(p => p.Trim().Length > 0 && p.Trim().Split(' ').Length <= 3);
                string point = (hasArea ? text.Substring(colon + 2) : text).Trim().TrimEnd('.');
                if (point.Length == 0) continue;
                point = char.ToUpperInvariant(point[0]) + point.Substring(1);
                string area = "";
                if (hasArea)
                {
                    var parts = text.Substring(0, colon).Split(new[] { " > " }, StringSplitOptions.RemoveEmptyEntries);
                    area = Regex.Replace(parts[0].Trim(), @" tab$", "");
                    if (parts.Length > 1)
                        point = string.Join(" > ", parts.Skip(1).Select(p => p.Trim())) + ": " + point;
                }
                int i = groups.FindIndex(g => string.Equals(g.area, area, StringComparison.OrdinalIgnoreCase));
                if (i < 0) groups.Add((area, new List<string> { point }));
                else groups[i].points.Add(point);
            }
            var sb = new StringBuilder();
            foreach (var (area, points) in groups)
            {
                if (area.Length == 0)
                    foreach (var p in points) sb.Append("- ").Append(p).Append('\n');
                else if (points.Count == 1)
                    sb.Append("- ").Append(area).Append(": ").Append(points[0]).Append('\n');
                else
                {
                    sb.Append("- ").Append(area).Append(":\n");
                    foreach (var p in points) sb.Append("  - ").Append(p).Append('\n');
                }
            }
            return sb.ToString().TrimEnd();
        }

        public void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            ZenHub.Header($"Base Core {current} (embedded)", "Create update: notes into CHANGELOG.md, Base.dll build, samples / templates, version, commit and local tag zen/<version>. Publish: zen + tag, then the public github.com/zendios/base + tag (OpenUPM builds it; games then see the version in Setup).");
            using (new EditorGUI.DisabledScope(busy))
            {
                version = EditorGUILayout.TextField("New version", version);
                DrawChanges();

                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                GUILayout.Label("Notes (CHANGELOG, editable)", ZenHub.Note);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                notes = EditorGUILayout.TextArea(notes, GUILayout.MinHeight(140));

                EditorGUILayout.BeginHorizontal();
                if (EditorGUILayout.DropdownButton(new GUIContent("Export test package", "Build the packages into Builds/packages to try them in a game first. Nothing is committed, tagged or pushed."), FocusType.Passive, GUILayout.Width(150)))
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent(".tgz (Package Manager, file: in manifest.json)"), false, ExportTest);
                    menu.AddItem(new GUIContent(".unitypackage (Assets > Import Package)"), false, ExportUnityPackage);
                    menu.ShowAsContext();
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button($"Create update {version}", GUILayout.Width(170)))
                    Create();
                using (new EditorGUI.DisabledScope(released == null))
                    if (GUILayout.Button(new GUIContent(released != null ? $"Publish {released}" : "Publish", released != null ? "Push zen, then the public github.com/zendios/base (OpenUPM)" : "Available after Create update"), GUILayout.Width(130)))
                        Push();
                EditorGUILayout.EndHorizontal();
                if (pending.Count > 0)
                    EditorGUILayout.LabelField(new GUIContent($"Not committed yet, goes into the release commit: {string.Join(", ", pending.Select(Path.GetFileName))}", string.Join("\n", pending)), ZenHub.Warn);
                if (released == null && published == null)
                    EditorGUILayout.LabelField("Publish becomes available after Create update.", ZenHub.Note);
                if (published != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    bool live = !string.IsNullOrEmpty(openUpm) && openUpm == published;
                    EditorGUILayout.LabelField(new GUIContent(live ? $"OpenUPM has {published}: games can update." : openUpm == null ? $"OpenUPM: {published} pushed, check in 15-30 minutes." : $"OpenUPM does not have {published} yet (latest: {(openUpm == "" ? "none" : openUpm)}).",
                        "https://openupm.com/packages/com.zen.core/"), live ? ZenHub.Good : ZenHub.Note);
                    if (GUILayout.Button("Check OpenUPM", EditorStyles.miniButton, GUILayout.Width(110)))
                        openUpm = OpenUpmLatest();
                    if (GUILayout.Button("Open page", EditorStyles.miniButton, GUILayout.Width(80)))
                        Application.OpenURL("https://openupm.com/packages/com.zen.core/");
                    EditorGUILayout.EndHorizontal();
                }
            }
            if (busy)
                EditorGUILayout.HelpBox("Running...", MessageType.Info);

            string text;
            lock (log) text = log.ToString();
            if (text.Length > 0)
            {
                ZenHub.Header("Output");
                logScroll = EditorGUILayout.BeginScrollView(logScroll, EditorStyles.helpBox, GUILayout.MinHeight(160));
                EditorGUILayout.SelectableLabel(text, ZenHub.NoteWrapped, GUILayout.ExpandHeight(true), GUILayout.MinHeight(ZenHub.NoteWrapped.CalcHeight(new GUIContent(text), 600)));
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndScrollView();
        }

        /// <summary>The commits since the last release, each with a tick; "Write notes" turns the ticked ones into the
        /// notes, grouped by area.</summary>
        private void DrawChanges()
        {
            int on = changes.Count(c => c.on);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Changes", ZenHub.Note);
            GUILayout.Label(since != null ? $"{on} of {changes.Count} since {since}" : $"{on} of {changes.Count} recent", ZenHub.Note);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("All", EditorStyles.toolbarButton)) changes.ForEach(c => c.on = true);
            if (GUILayout.Button("None", EditorStyles.toolbarButton)) changes.ForEach(c => c.on = false);
            if (GUILayout.Button(new GUIContent("Reload", "Read the commits again"), EditorStyles.toolbarButton))
                changes = Commits(current, out since);
            using (new EditorGUI.DisabledScope(on == 0))
                if (GUILayout.Button(new GUIContent("Write notes", "Replace the notes with the ticked changes, grouped by area"), EditorStyles.toolbarButton)
                    && (string.IsNullOrWhiteSpace(notes) || EditorUtility.DisplayDialog("Release", "Replace the notes with the ticked changes?", "Replace", "Cancel")))
                {
                    notes = Format(changes.Where(c => c.on).Select(c => c.subject));
                    GUI.FocusControl(null);
                }
            EditorGUILayout.EndHorizontal();

            if (changes.Count == 0)
            {
                EditorGUILayout.HelpBox("No package change since the last release.", MessageType.None);
                return;
            }
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            changesScroll = EditorGUILayout.BeginScrollView(changesScroll, GUILayout.Height(Mathf.Min(changes.Count, 8) * 20 + 4));
            for (int i = 0; i < changes.Count; i++)
            {
                var r = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
                if (i % 2 == 1)
                    EditorGUI.DrawRect(r, EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.035f) : new Color(0f, 0f, 0f, 0.045f));
                var c = changes[i];
                c.on = EditorGUI.Toggle(new Rect(r.x + 2, r.y + 1, 16, r.height), c.on);
                GUI.Label(new Rect(r.x + 22, r.y, r.width - 24, r.height), new GUIContent(c.subject, c.subject), c.on ? ZenHub.Body : ZenHub.Muted);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Test build of the packages, without git: copies com.zen.core (version "x.y.z-test.yyyyMMddHHmm", from the New
        /// version field) and com.zen.plugins.adzative to a temp folder and packs them with Client.Pack into
        /// Builds/packages (ignored by git). A game installs them with file: entries in manifest.json.
        /// </summary>
        private void ExportTest()
        {
            string output = Path.Combine(Repo, "Builds/packages");
            string testVersion = $"{version.Trim()}-test.{DateTime.Now:yyyyMMddHHmm}";
            if (!EditorUtility.DisplayDialog("Export test package", $"Pack Base Core {testVersion} and AdZative into {output}?\n\nNothing is committed, tagged or pushed.", "Export", "Cancel"))
                return;
            Directory.CreateDirectory(output);
            lock (log) log.Clear().AppendLine($"> export {testVersion} to {output}");
            var files = new List<string>();
            try
            {
                foreach (var name in new[] { "com.zen.core", "com.zen.plugins.adzative" })
                {
                    string src = Path.GetFullPath($"Packages/{name}");
                    string tmp = Path.Combine(Path.GetTempPath(), "base-pack-" + Guid.NewGuid().ToString("N"), name);
                    Copy(src, tmp);
                    if (name == "com.zen.core")
                    {
                        string json = Path.Combine(tmp, "package.json");
                        File.WriteAllText(json, Regex.Replace(File.ReadAllText(json), "(\"version\"\\s*:\\s*\")[^\"]+\"", "${1}" + testVersion + "\"", RegexOptions.None, TimeSpan.FromSeconds(1)));
                    }
                    var req = Client.Pack(tmp, output);
                    while (!req.IsCompleted)
                    {
                        EditorUtility.DisplayProgressBar("Export test package", name, 0.5f);
                        System.Threading.Thread.Sleep(50);
                    }
                    if (req.Status != StatusCode.Success)
                        throw new Exception($"{name}: {req.Error?.message}");
                    files.Add(req.Result.tarballPath);
                    lock (log) log.AppendLine(req.Result.tarballPath);
                    try { Directory.Delete(Path.GetDirectoryName(tmp), true); } catch { }
                }
            }
            catch (Exception ex)
            {
                lock (log) log.AppendLine("Failed: " + ex.Message);
                return;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            lock (log)
            {
                log.AppendLine("Done. In the game's Packages/manifest.json:");
                foreach (var f in files)
                    log.AppendLine($"  \"{Path.GetFileName(f).Replace(".tgz", "").Split('-')[0]}\": \"file:{f.Replace('\\', '/')}\",");
            }
            EditorUtility.RevealInFinder(files.FirstOrDefault() ?? output);
        }

        /// <summary>
        /// Test build as a .unitypackage (Assets > Import Package > Custom Package): Packages/com.zen.core and
        /// Packages/com.zen.plugins.adzative as they are now, into Builds/packages. Importing it embeds both packages in
        /// the game's Packages/ folder (remove their manifest.json entries first). The version inside stays the current one.
        /// </summary>
        private void ExportUnityPackage()
        {
            string output = Path.Combine(Repo, "Builds/packages");
            string file = Path.Combine(output, $"Base-{version.Trim()}-test.{DateTime.Now:yyyyMMddHHmm}.unitypackage");
            if (!EditorUtility.DisplayDialog("Export test package", $"Export Packages/com.zen.core and Packages/com.zen.plugins.adzative to\n{file}?\n\nNothing is committed, tagged or pushed.", "Export", "Cancel"))
                return;
            Directory.CreateDirectory(output);
            lock (log) log.Clear().AppendLine($"> export {Path.GetFileName(file)}");
            try
            {
                AssetDatabase.ExportPackage(new[] { "Packages/com.zen.core", "Packages/com.zen.plugins.adzative" }, file, ExportPackageOptions.Recurse);
            }
            catch (Exception ex)
            {
                lock (log) log.AppendLine("Failed: " + ex.Message);
                return;
            }
            lock (log)
                log.AppendLine(file.Replace('\\', '/')).AppendLine("Done. In the test game: remove the com.zen.core and com.zen.plugins.adzative lines of Packages/manifest.json, " +
                                                                  "then Assets > Import Package > Custom Package (they become embedded packages in Packages/).");
            EditorUtility.RevealInFinder(file);
        }

        private static void Copy(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (var file in Directory.GetFiles(from))
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
            foreach (var dir in Directory.GetDirectories(from))
                Copy(dir, Path.Combine(to, Path.GetFileName(dir)));
        }

        private void Create()
        {
            version = version.Trim();
            if (!Regex.IsMatch(version, @"^\d+\.\d+\.\d+$") || !(new Version(version) > new Version(current)))
            {
                EditorUtility.DisplayDialog("Release", $"\"{version}\" must be x.y.z and newer than {current}.", "OK");
                return;
            }
            if (string.IsNullOrWhiteSpace(notes))
            {
                EditorUtility.DisplayDialog("Release", "Write the notes of this version first.", "OK");
                return;
            }
            try
            {
                if (ZenUpdateChecker.Git($"tag -l zen/{version}", Repo).Trim().Length > 0)
                {
                    EditorUtility.DisplayDialog("Release", $"Tag zen/{version} already exists.", "OK");
                    return;
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Release", ex.Message, "OK");
                return;
            }
            pending = Pending();
            string also = pending.Count > 0 ? "\n\nAlso in the release commit (not committed yet):\n" + string.Join("\n", pending.Take(15)) : "";
            if (!EditorUtility.DisplayDialog("Release", $"Create Base Core {version}?\n\nCHANGELOG notes, Base.dll build, samples / templates, version, commit and local tag zen/{version}. Nothing is pushed.{also}", "Create", "Cancel"))
                return;

            string path = Path.Combine(Repo, Changelog);
            File.WriteAllText(path, WithVersion(File.ReadAllText(path), version, notes));
            string v = version;
            Run(Bash(), $"\"{Script}\" {v}", ok =>
            {
                if (!ok) return;
                released = v;
                current = v;
                changes = Commits(v, out since);
                version = Version.TryParse(v, out var x) ? $"{x.Major}.{x.Minor}.{x.Build + 1}" : v;
                notes = "";
            });
        }

        /// <summary>tools/base-publish.sh: pushes zen + tag zen/x.y.z, then mirrors both packages into the public
        /// github.com/zendios/base with tag x.y.z (OpenUPM builds it).</summary>
        private void Push()
        {
            if (!EditorUtility.DisplayDialog("Release", $"Publish Base Core {released}?\n\n1. Push this branch and tag zen/{released}.\n2. Copy com.zen.core and com.zen.plugins.adzative to the PUBLIC repository github.com/zendios/base, tag {released} and push it.\n\nOpenUPM builds it within 15-30 minutes, then every game sees {released} in Setup.", "Publish", "Cancel"))
                return;
            string v = released;
            Run(Bash(), $"\"{PublishScript}\" {v}", ok =>
            {
                if (!ok) return;
                released = null;
                published = v;
                openUpm = null;
            });
        }

        private static string PublishScript => Path.Combine(Repo, "tools/base-publish.sh");
        private string published;   // version published in this session (OpenUPM status)
        private string openUpm;     // "" = not on OpenUPM yet, else its latest version

        /// <summary>The newest com.zen.core version on OpenUPM (main thread, short timeout).</summary>
        private static string OpenUpmLatest()
        {
            using (var req = UnityEngine.Networking.UnityWebRequest.Get("https://package.openupm.com/com.zen.core"))
            {
                req.timeout = 10;
                var op = req.SendWebRequest();
                while (!op.isDone) System.Threading.Thread.Sleep(20);
                if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success) return "";
                var m = Regex.Match(req.downloadHandler.text, @"""latest""\s*:\s*""([^""]+)""");
                return m.Success ? m.Groups[1].Value : "";
            }
        }

        /// <summary>The text under "## [Unreleased]" (the notes waiting for a version).</summary>
        private static string Unreleased(string changelog)
        {
            int start = changelog.IndexOf("## [Unreleased]", StringComparison.Ordinal);
            if (start < 0) return "";
            start = changelog.IndexOf('\n', start) + 1;
            int end = changelog.IndexOf("\n## [", start, StringComparison.Ordinal);
            return (end > 0 ? changelog.Substring(start, end - start) : changelog.Substring(start)).Trim();
        }

        /// <summary>Empties [Unreleased] and adds "## [version] - date" with the notes under it.</summary>
        private static string WithVersion(string changelog, string version, string notes)
        {
            string nl = changelog.Contains("\r\n") ? "\r\n" : "\n";
            string section = $"## [{version}] - {DateTime.Now:yyyy-MM-dd}{nl}{notes.Trim().Replace("\r\n", "\n").Replace("\n", nl)}{nl}{nl}";
            int start = changelog.IndexOf("## [Unreleased]", StringComparison.Ordinal);
            if (start < 0)
            {
                int first = changelog.IndexOf("## [", StringComparison.Ordinal);
                return first < 0 ? changelog + nl + section : changelog.Insert(first, section);
            }
            int body = changelog.IndexOf('\n', start) + 1;
            int end = changelog.IndexOf("\n## [", body, StringComparison.Ordinal);
            end = end < 0 ? changelog.Length : end + 1;
            return changelog.Substring(0, body) + nl + section + changelog.Substring(end);
        }

        private static string Bash()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
                return "/bin/bash";
            foreach (var p in new[] { @"C:\Program Files\Git\bin\bash.exe", @"C:\Program Files (x86)\Git\bin\bash.exe" })
                if (File.Exists(p)) return p;
            return "bash";
        }

        /// <summary>Runs a command in the repository on a worker thread; its output goes to the Output box.</summary>
        private void Run(string file, string args, Action<bool> done)
        {
            busy = true;
            lock (log) log.Clear().AppendLine($"> {Path.GetFileName(file)} {args}");
            new Thread(() =>
            {
                bool ok = false;
                try
                {
                    var info = new ProcessStartInfo(file, args)
                    {
                        WorkingDirectory = Repo,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    };
                    info.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
                    using (var p = new Process { StartInfo = info })
                    {
                        DataReceivedEventHandler add = (_, e) => { if (e.Data != null) lock (log) log.AppendLine(e.Data); };
                        p.OutputDataReceived += add;
                        p.ErrorDataReceived += add;
                        p.Start();
                        p.BeginOutputReadLine();
                        p.BeginErrorReadLine();
                        p.WaitForExit();
                        ok = p.ExitCode == 0;
                        lock (log) log.AppendLine(ok ? "Done." : $"Failed (exit {p.ExitCode}).");
                    }
                }
                catch (Exception ex)
                {
                    lock (log) log.AppendLine("Failed: " + ex.Message);
                }
                EditorApplication.delayCall += () =>
                {
                    busy = false;
                    done(ok);
                    AssetDatabase.Refresh();
                    if (EditorWindow.HasOpenInstances<ZenHub>())
                        EditorWindow.GetWindow<ZenHub>(false, null, false).Repaint();
                };
            }) { IsBackground = true }.Start();
        }
    }
}
