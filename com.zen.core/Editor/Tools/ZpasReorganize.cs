using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Base.Tools
{
    /// <summary>
    /// ZPAS §3 folder tree for a project whose assets sit in type folders (Texture2D/, AudioClip/, GameObject/... as an
    /// AssetRipper export, or Images/, Sounds/...): moves them to Assets/Production/{Scenes/Production, Scripts, UI, Art, Audio/SFX,
    /// Audio/BGM, Fonts}/&lt;Feature&gt;. The feature of an asset is the scene (or Resources) that depends on it; used by several, it goes
    /// to Commons. UI or Art: referenced from a MonoBehaviour (Image, Button...) or a UI animation, else Art.
    /// Resources/, Plugins/, Editor/ and Assets/Settings/ stay; GUIDs stay (AssetDatabase.MoveAsset); Build Settings follow by GUID.
    /// <code>
    /// Unity -batchmode -quit -nographics -projectPath . -executeMethod Base.Tools.ZpasReorganize.Run
    ///     [-zpasFeatures First=Splash,Menu=Main,Game=Gameplay] [-zpasResourcesFeature Gameplay] [-zpasApply]
    /// </code>
    /// Without -zpasApply it only writes the plan, Logs/Zpas/reorganize.csv (from, to, owners) and reorganize_summary.txt.
    /// Scenes without a -zpasFeatures entry use their own name. Check the plan, then run again with -zpasApply.
    /// </summary>
    public static class ZpasReorganize
    {
        private const string Root = "Assets/Production";
        private const string ReportFolder = "Logs/Zpas";
        private static readonly string[] FixedRoots = { "Assets/Resources/", "Assets/Plugins/", "Assets/Editor/", Root + "/", "Assets/Settings/" };

        public static void Run()
        {
            var args = System.Environment.GetCommandLineArgs();
            var features = Arg(args, "-zpasFeatures", "").Split(',').Select(p => p.Split('='))
                .Where(p => p.Length == 2).ToDictionary(p => p[0].Trim(), p => p[1].Trim());
            var plan = Plan(features, Arg(args, "-zpasResourcesFeature", "Commons"));
            WriteReport(plan);
            if (args.Contains("-zpasApply"))
                Apply(plan);
        }

        private static void Apply(List<(string from, string to, string owners)> plan)
        {
            // folders first: a folder created inside StartAssetEditing is not in the database yet and MoveAsset into it fails
            foreach (var folder in plan.Select(p => Path.GetDirectoryName(p.to).Replace('\\', '/')).Distinct())
                EnsureFolder(folder);
            int moved = 0, failed = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var (from, to, _) in plan)
                {
                    var error = AssetDatabase.MoveAsset(from, to);
                    if (string.IsNullOrEmpty(error)) moved++;
                    else { failed++; Debug.LogError($"[Base ZPAS] {from} -> {to}: {error}"); }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.Refresh();
            EditorBuildSettings.scenes = EditorBuildSettings.scenes
                .Select(s => new EditorBuildSettingsScene(AssetDatabase.GUIDToAssetPath(s.guid.ToString()), s.enabled)).ToArray();
            var removed = RemoveEmptyFolders("Assets");
            Debug.Log($"[Base ZPAS] reorganize: moved {moved}, failed {failed}, empty folders removed {removed}");
            if (failed > 0)
                EditorApplication.Exit(1);
        }

        private static List<(string from, string to, string owners)> Plan(Dictionary<string, string> features, string resourcesFeature)
        {
            var owners = Owners(features, resourcesFeature);
            var uiGuids = UiReferencedGuids();
            var plan = new List<(string, string, string)>();
            foreach (var path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/")).OrderBy(p => p))
            {
                if (AssetDatabase.IsValidFolder(path) || FixedRoots.Any(path.StartsWith) || path.Contains("/Editor/"))
                    continue;
                owners.TryGetValue(path, out var set);
                var to = Destination(path, set, uiGuids);
                if (to != null && to != path)
                    plan.Add((path, to, set == null ? "unused" : string.Join("+", set.OrderBy(s => s))));
            }
            return plan;
        }

        private static string Destination(string path, HashSet<string> owners, HashSet<string> uiGuids)
        {
            var file = Path.GetFileName(path);
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var feature = owners != null && owners.Count == 1 ? owners.First() : "Commons";

            if (ext == ".unity") return $"{Root}/Scenes/Production/{file}";
            var sceneFolder = Regex.Match(path, "^Assets/Scenes/([^/]+)/(.+)$");   // lighting data next to its scene
            if (sceneFolder.Success) return $"{Root}/Scenes/Production/{sceneFolder.Groups[1].Value}/{sceneFolder.Groups[2].Value}";
            if (ext == ".cs" || ext == ".asmdef")
            {
                var script = Regex.Match(path, "^Assets/Scripts/(?:Assembly-CSharp/)?(.+)$");
                return script.Success ? $"{Root}/Scripts/{script.Groups[1].Value}" : null;
            }
            if (ext == ".lighting") return $"Assets/Settings/{file}";

            var type = AssetDatabase.GetMainAssetTypeAtPath(path);
            if (type == typeof(AudioClip))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                return $"{Root}/Audio/{(clip != null && clip.length > 20f ? "BGM" : "SFX")}/{file}";
            }
            if (type == typeof(Font)) return $"{Root}/Fonts/{file}";
            // the atlas is packed at build time: no scene references it
            if (type == typeof(UnityEngine.U2D.SpriteAtlas) || ext == ".spriteatlas" || ext == ".spriteatlasv2") return $"{Root}/UI/Commons/{file}";
            // an Animator is not a MonoBehaviour: a controller is UI when its clips are
            var ui = uiGuids.Contains(AssetDatabase.AssetPathToGUID(path)) || (ext == ".controller"
                && AssetDatabase.GetDependencies(path, false).Any(d => uiGuids.Contains(AssetDatabase.AssetPathToGUID(d))));
            var isUiKind = type == typeof(Texture2D) || type == typeof(Sprite) || type == typeof(AnimationClip) || ext == ".controller";
            return $"{Root}/{(ui && isUiKind ? "UI" : "Art")}/{feature}/{file}";
        }

        /// <summary>Asset path -> features of the scenes / Resources assets that depend on it.</summary>
        private static Dictionary<string, HashSet<string>> Owners(Dictionary<string, string> features, string resourcesFeature)
        {
            var owners = new Dictionary<string, HashSet<string>>();
            void Mark(string root, string feature)
            {
                foreach (var dependency in AssetDatabase.GetDependencies(root, true))
                {
                    if (!owners.TryGetValue(dependency, out var set)) owners[dependency] = set = new HashSet<string>();
                    set.Add(feature);
                }
            }
            foreach (var scene in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var name = Path.GetFileNameWithoutExtension(scene);
                Mark(scene, features.TryGetValue(name, out var feature) ? feature : name);
            }
            foreach (var resource in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/Resources/") && !AssetDatabase.IsValidFolder(p)))
                Mark(resource, resourcesFeature);
            return owners;
        }

        /// <summary>GUIDs referenced from MonoBehaviours (uGUI) in scenes and prefabs, or from animation clips / controllers.</summary>
        private static HashSet<string> UiReferencedGuids()
        {
            var result = new HashSet<string>();
            var guidReference = new Regex("guid: ([0-9a-f]{32})");
            var files = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/")
                && (p.EndsWith(".unity") || p.EndsWith(".prefab") || p.EndsWith(".anim") || p.EndsWith(".controller")));
            foreach (var path in files)
            {
                var animation = path.EndsWith(".anim") || path.EndsWith(".controller");
                foreach (var document in Regex.Split(File.ReadAllText(path), "\n--- !u!"))
                {
                    if (!animation && !document.StartsWith("114 ")) continue;
                    foreach (Match match in guidReference.Matches(document)) result.Add(match.Groups[1].Value);
                }
            }
            return result;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static int RemoveEmptyFolders(string folder)
        {
            int removed = 0;
            foreach (var sub in AssetDatabase.GetSubFolders(folder)) removed += RemoveEmptyFolders(sub);
            var kept = folder == "Assets" || FixedRoots.Any(r => (folder + "/").StartsWith(r));
            if (!kept && Directory.GetFileSystemEntries(folder).Length == 0 && AssetDatabase.DeleteAsset(folder))
                removed++;
            return removed;
        }

        private static void WriteReport(List<(string from, string to, string owners)> plan)
        {
            Directory.CreateDirectory(ReportFolder);
            var lines = new List<string> { "from,to,owners" };
            lines.AddRange(plan.Select(p => $"\"{p.from}\",\"{p.to}\",{p.owners}"));
            File.WriteAllLines(Path.Combine(ReportFolder, "reorganize.csv"), lines, new UTF8Encoding(false));
            var summary = new StringBuilder($"moves={plan.Count}\n");
            foreach (var group in plan.GroupBy(p => string.Join("/", p.to.Split('/').Take(4))).OrderBy(g => g.Key))
                summary.AppendLine($"{group.Count(),5}  {group.Key}");
            File.WriteAllText(Path.Combine(ReportFolder, "reorganize_summary.txt"), summary.ToString());
            Debug.Log("[Base ZPAS] reorganize plan\n" + summary);
        }

        private static string Arg(string[] args, string name, string fallback)
        {
            int index = System.Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }
    }
}
