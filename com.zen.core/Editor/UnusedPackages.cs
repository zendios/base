using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Base.Setup
{
    /// <summary>
    /// Setup > Unused packages: the manifest's packages (built-in modules included) that the game does not use, to
    /// remove them. A package is "in use" when one of these says so:
    ///  1. code: a compiled assembly of the game or of another package references its assembly (the compiler only
    ///     records the assemblies a DLL really uses; every built-in module is its own UnityEngine.*Module assembly);
    ///  2. scenes / prefabs / assets: a built-in component of the module is serialized there (its class ID, e.g.
    ///     NavMeshAgent, WheelCollider), or a script of the package is (its GUID), or a video file for Video;
    ///  3. dependencies: another installed package depends on it.
    /// Base's own needs and the SDKs are never listed. Remove backs manifest.json up first (Restore puts it back).
    /// </summary>
    internal class UnusedPackages
    {
        private const string ManifestPath = "Packages/manifest.json";
        private const string BackupPath = "Packages/manifest.json.bak";

        /// <summary>Never offered: what Base Core needs.</summary>
        private static readonly string[] Protected =
        {
            "com.unity.textmeshpro", "com.unity.ugui", "com.unity.inputsystem", "com.unity.2d.sprite",
            "com.unity.modules.androidjni", "com.unity.modules.animation", "com.unity.modules.audio",
            "com.unity.modules.imageconversion", "com.unity.modules.imgui", "com.unity.modules.jsonserialize",
            "com.unity.modules.particlesystem", "com.unity.modules.ui", "com.unity.modules.uielements",
            "com.unity.modules.unitywebrequest", "com.unity.modules.unitywebrequestassetbundle", "com.unity.modules.unitywebrequestaudio",
            "com.unity.modules.unitywebrequesttexture", "com.unity.modules.unitywebrequestwww", "com.unity.modules.assetbundle",
        };

        /// <summary>Built-in components of a module (serialized class IDs).</summary>
        private static readonly Dictionary<string, (int id, string name)[]> ModuleClasses = new Dictionary<string, (int, string)[]>
        {
            ["ai"] = new[] { (195, "NavMeshAgent"), (208, "NavMeshObstacle"), (191, "OffMeshLink"), (238, "NavMeshData") },
            ["cloth"] = new[] { (183, "Cloth") },
            ["director"] = new[] { (320, "PlayableDirector") },
            ["physics"] = new[] { (54, "Rigidbody"), (65, "BoxCollider"), (135, "SphereCollider"), (136, "CapsuleCollider"), (64, "MeshCollider"),
                (143, "CharacterController"), (59, "HingeJoint"), (138, "FixedJoint"), (145, "SpringJoint"), (144, "CharacterJoint"),
                (153, "ConfigurableJoint"), (75, "ConstantForce"), (134, "PhysicMaterial"), (171, "ArticulationBody") },
            ["physics2d"] = new[] { (50, "Rigidbody2D"), (58, "CircleCollider2D"), (60, "PolygonCollider2D"), (61, "BoxCollider2D"), (68, "EdgeCollider2D"),
                (70, "CapsuleCollider2D"), (66, "CompositeCollider2D"), (62, "PhysicsMaterial2D") },
            ["terrain"] = new[] { (218, "Terrain"), (156, "TerrainData") },
            ["terrainphysics"] = new[] { (154, "TerrainCollider") },
            ["tilemap"] = new[] { (1839735485, "Tilemap"), (483693784, "TilemapRenderer"), (19719996, "TilemapCollider2D") },
            ["umbra"] = new[] { (192, "OcclusionArea"), (41, "OcclusionPortal") },
            ["vehicles"] = new[] { (146, "WheelCollider") },
            ["video"] = new[] { (328, "VideoPlayer"), (329, "VideoClip") },
            ["wind"] = new[] { (182, "WindZone") },
        };

        private static readonly string[] VideoExtensions = { ".mp4", ".mov", ".webm", ".m4v", ".avi", ".ogv", ".vp8" };

        internal class Row
        {
            public string id;
            public string usedBy;     // null = unused
            public bool selected;
        }

        public List<Row> Rows { get; private set; }
        private AddAndRemoveRequest request;
        private Vector2 scroll;

        public static bool HasBackup => File.Exists(BackupPath);

        /// <summary>Checks every candidate (code, assets, dependencies).</summary>
        public void Scan(Dictionary<string, PackageInfo> installed)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Unused packages", "Reading the compiled assemblies...", 0.1f);
                var code = CodeReferences(installed);
                EditorUtility.DisplayProgressBar("Unused packages", "Reading scenes, prefabs and assets...", 0.4f);
                var assets = ScanAssets(installed);
                EditorUtility.DisplayProgressBar("Unused packages", "Checking dependencies...", 0.9f);

                var sdks = new HashSet<string>(BaseSdks.All.Select(s => s.id));
                Rows = ManifestPackages()
                    .Where(id => !Protected.Contains(id) && !sdks.Contains(id) && !id.StartsWith("com.zen.") && !id.StartsWith("com.google.") && !id.StartsWith("appsflyer"))
                    .OrderBy(id => id.StartsWith("com.unity.modules.") ? 1 : 0).ThenBy(id => id)
                    .Select(id => new Row { id = id, usedBy = UsedBy(id, installed, code, assets) })
                    .ToList();
                foreach (var r in Rows)
                    r.selected = r.usedBy == null;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public void Draw(Dictionary<string, PackageInfo> installed, Action changed)
        {
            ZenHub.Header("Unused packages", "Packages (built-in modules included) the game does not use: not referenced by any compiled code, " +
                                             "not in any scene / prefab / asset, not needed by another package. Check the list before removing.");
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            using (new EditorGUI.DisabledScope(request != null))
            {
                if (GUILayout.Button(Rows == null ? "Scan" : "Scan again", EditorStyles.toolbarButton))
                    Scan(installed);
                using (new EditorGUI.DisabledScope(Rows == null || !Rows.Any(r => r.selected)))
                    if (GUILayout.Button("Remove selected", EditorStyles.toolbarButton))
                        Remove(changed);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!HasBackup))
                    if (GUILayout.Button(new GUIContent("Restore", "Put back Packages/manifest.json from before the last Remove"), EditorStyles.toolbarButton))
                        Restore(changed);
            }
            EditorGUILayout.EndHorizontal();
            if (request != null)
            {
                EditorGUILayout.HelpBox("Removing...", MessageType.Info);
                if (request.IsCompleted)
                {
                    if (request.Status == StatusCode.Failure)
                        Debug.LogError("[Base Setup] remove failed: " + request.Error?.message);
                    request = null;
                    Rows = null;
                    changed();
                }
            }
            if (Rows == null)
                return;
            if (Rows.Count == 0)
            {
                EditorGUILayout.HelpBox("Nothing to remove.", MessageType.Info);
                return;
            }
            foreach (var r in Rows)
            {
                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(r.usedBy != null))
                    r.selected = EditorGUILayout.Toggle(r.selected && r.usedBy == null, GUILayout.Width(18));
                EditorGUILayout.LabelField(r.id.Replace("com.unity.modules.", "module: "), ZenHub.Body, GUILayout.Width(260));
                EditorGUILayout.LabelField(r.usedBy == null ? "Unused" : "In use: " + r.usedBy, r.usedBy == null ? ZenHub.Good : ZenHub.Muted);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void Remove(Action changed)
        {
            var ids = Rows.Where(r => r.selected && r.usedBy == null).Select(r => r.id).ToArray();
            if (!EditorUtility.DisplayDialog("Unused packages", "Remove from Packages/manifest.json (backup: manifest.json.bak, Restore puts it back):\n\n" + string.Join("\n", ids), "Remove", "Cancel"))
                return;
            File.Copy(ManifestPath, BackupPath, true);
            Debug.Log($"[Base Setup] removing {string.Join(", ", ids)} (backup {BackupPath})");
            request = Client.AddAndRemove(null, ids);
        }

        private void Restore(Action changed)
        {
            if (!EditorUtility.DisplayDialog("Unused packages", "Put back Packages/manifest.json from manifest.json.bak?", "Restore", "Cancel"))
                return;
            File.Copy(BackupPath, ManifestPath, true);
            File.Delete(BackupPath);
            Debug.Log("[Base Setup] manifest.json restored from the backup");
            Rows = null;
            Client.Resolve();
            changed();
        }

        // ---- checks ----

        private static string UsedBy(string id, Dictionary<string, PackageInfo> installed, Dictionary<string, string> code, Assets assets)
        {
            // 3. another installed package needs it
            var needer = installed.Values.FirstOrDefault(p => p.name != id && p.dependencies.Any(d => d.name == id));
            if (needer != null)
                return "needed by " + needer.name;

            // 1. code
            foreach (var asm in AssemblyNames(id, installed))
                if (code.TryGetValue(asm, out var user))
                    return $"{asm} used by {user}";

            // 2. assets
            if (id.StartsWith("com.unity.modules."))
            {
                string module = id.Substring("com.unity.modules.".Length);
                if (ModuleClasses.TryGetValue(module, out var classes))
                    foreach (var (cid, name) in classes)
                        if (assets.classIds.TryGetValue(cid, out var file))
                            return $"{name} in {file}";
                if (module == "video" && assets.video != null)
                    return "video file " + assets.video;
            }
            else if (installed.TryGetValue(id, out var info) && !string.IsNullOrEmpty(info.resolvedPath))
            {
                foreach (var meta in Directory.EnumerateFiles(info.resolvedPath, "*.cs.meta", SearchOption.AllDirectories))
                {
                    var m = Regex.Match(File.ReadAllText(meta), @"guid: ([0-9a-f]{32})");
                    if (m.Success && assets.scriptGuids.TryGetValue(m.Groups[1].Value, out var file))
                        return $"{Path.GetFileNameWithoutExtension(meta.Substring(0, meta.Length - 5))} in {file}";
                }
            }
            return null;
        }

        /// <summary>The assemblies a package brings: UnityEngine.XModule for a built-in module, its asmdef names otherwise.</summary>
        private static IEnumerable<string> AssemblyNames(string id, Dictionary<string, PackageInfo> installed)
        {
            if (id.StartsWith("com.unity.modules."))
            {
                string module = id.Substring("com.unity.modules.".Length);
                string match = module.Replace(".", "") + "module";
                return AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetName().Name)
                    .Where(n => n.StartsWith("UnityEngine.") && n.Substring("UnityEngine.".Length).ToLowerInvariant() == match)
                    .ToList();
            }
            if (!installed.TryGetValue(id, out var info) || string.IsNullOrEmpty(info.resolvedPath))
                return Enumerable.Empty<string>();
            return Directory.EnumerateFiles(info.resolvedPath, "*.asmdef", SearchOption.AllDirectories)
                .Select(f => Regex.Match(File.ReadAllText(f), "\"name\"\\s*:\\s*\"([^\"]+)\""))
                .Where(m => m.Success).Select(m => m.Groups[1].Value)
                .Concat(Directory.EnumerateFiles(info.resolvedPath, "*.dll", SearchOption.AllDirectories).Select(Path.GetFileNameWithoutExtension))
                .ToList();
        }

        /// <summary>Referenced assembly name -> the first project assembly (game or package) that uses it.</summary>
        private static Dictionary<string, string> CodeReferences(Dictionary<string, PackageInfo> installed)
        {
            string project = Path.GetFullPath(".").Replace('\\', '/');
            var owners = CompilationPipeline.GetAssemblies(AssembliesType.Editor)
                .ToDictionary(a => a.name, a => Owner(a.sourceFiles.FirstOrDefault() ?? "", installed));
            var result = new Dictionary<string, string>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string location;
                try { location = asm.IsDynamic ? "" : asm.Location.Replace('\\', '/'); }
                catch { continue; }
                if (!location.StartsWith(project))
                    continue;   // Unity / .NET itself
                string name = asm.GetName().Name;
                string owner = owners.TryGetValue(name, out var o) ? o : Owner(location.Substring(project.Length + 1), installed);
                foreach (var r in asm.GetReferencedAssemblies())
                {
                    string key = r.Name;
                    // a package's own assemblies referencing each other do not count as a use of that package
                    if (!result.ContainsKey(key) && OwnerOfAssembly(key, owners) != owner)
                        result[key] = name;
                }
            }
            return result;
        }

        private static string OwnerOfAssembly(string assembly, Dictionary<string, string> owners) => owners.TryGetValue(assembly, out var o) ? o : null;

        /// <summary>The package a project path belongs to ("game" for Assets/).</summary>
        private static string Owner(string path, Dictionary<string, PackageInfo> installed)
        {
            path = path.Replace('\\', '/');
            if (path.StartsWith("Assets/"))
                return "game";
            var m = Regex.Match(path, @"(?:Packages|Library/PackageCache)/([^/@]+)");
            return m.Success ? m.Groups[1].Value : "game";
        }

        private class Assets
        {
            public readonly Dictionary<int, string> classIds = new Dictionary<int, string>();
            public readonly Dictionary<string, string> scriptGuids = new Dictionary<string, string>();
            public string video;
        }

        /// <summary>Class IDs and script GUIDs serialized in the game's scenes / prefabs / assets (Assets/, plus the Base
        /// packages and embedded / local packages, whose prefabs the game uses).</summary>
        private static Assets ScanAssets(Dictionary<string, PackageInfo> installed)
        {
            var result = new Assets();
            var roots = new List<string> { "Assets" };
            roots.AddRange(installed.Values
                .Where(p => p.name.StartsWith("com.zen.") || p.source == PackageSource.Embedded || p.source == PackageSource.Local)
                .Select(p => p.resolvedPath).Where(Directory.Exists));
            var header = new Regex(@"^--- !u!(\d+) &", RegexOptions.Multiline);
            var script = new Regex(@"m_Script: \{fileID: \d+, guid: ([0-9a-f]{32})");
            foreach (var root in roots.Distinct())
                foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (VideoExtensions.Contains(ext))
                    {
                        result.video = result.video ?? Path.GetFileName(file);
                        continue;
                    }
                    if (ext != ".unity" && ext != ".prefab" && ext != ".asset" && ext != ".playable" && ext != ".controller")
                        continue;
                    string text;
                    try { text = File.ReadAllText(file); }
                    catch { continue; }
                    if (!text.StartsWith("%YAML"))
                        continue;   // binary serialization: not readable here
                    string name = Path.GetFileName(file);
                    foreach (Match m in header.Matches(text))
                    {
                        int id = int.Parse(m.Groups[1].Value);
                        if (!result.classIds.ContainsKey(id)) result.classIds[id] = name;
                    }
                    foreach (Match m in script.Matches(text))
                        if (!result.scriptGuids.ContainsKey(m.Groups[1].Value)) result.scriptGuids[m.Groups[1].Value] = name;
                }
            return result;
        }

        /// <summary>The direct dependencies of Packages/manifest.json.</summary>
        private static IEnumerable<string> ManifestPackages()
        {
            string json = File.ReadAllText(ManifestPath);
            int start = json.IndexOf("\"dependencies\"", StringComparison.Ordinal);
            if (start < 0) return Enumerable.Empty<string>();
            int open = json.IndexOf('{', start), close = json.IndexOf('}', open);
            return Regex.Matches(json.Substring(open, close - open), "\"([^\"]+)\"\\s*:").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        }
    }
}
