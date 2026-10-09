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
    /// Base > Rename Assets: pick a folder and an asset kind, see the assets with their ZPAS check, type the new names and
    /// rename the selected ones. AssetDatabase.RenameAsset keeps the GUID, so prefabs and scenes keep their references.
    /// Names loaded by string (Resources.Load, Resources/ClassName) are flagged when the old name appears in a .cs file.
    /// </summary>
    public class RenameAssetWindow : EditorWindow
    {
        private enum AssetKind { Texture, Audio, Model, Material, Animation, Controller, Prefab, Scene, Atlas, Other }

        private class Row
        {
            public string Path;
            public string OldName;
            public string NewName;
            public AssetKind Kind;
            public bool Selected;
            public string Issue;
            public string CodeHits;
        }

        private static readonly string[] KindNames = { "Texture", "Audio", "Model", "Material", "Animation", "Controller", "Prefab", "Scene", "Atlas", "Other" };
        private static readonly string[] ForbiddenWords = { "final", "new", "temp", "demo", "abc", "fix", "backup", "old", "copy", "latest" };
        private static readonly Regex SubAssetName = new Regex("^[a-z0-9]+(_[a-z0-9]+)*$");
        private static readonly Regex PascalName = new Regex("^[A-Z][A-Za-z0-9]*(_[A-Za-z0-9]+)*$");
        private static readonly string[] InfixWords = { "mesh", "tex", "mat", "spr", "ani", "anm", "sfx", "bgm", "vfx", "atlas" };
        // words that start a property (direction, colour, state, texture map): they come after the part
        private static readonly string[] PropertyWords =
        {
            "horizontal", "vertical", "left", "right", "up", "down", "close", "back", "next", "normal", "pressed",
            "disabled", "hover", "on", "off", "albedo", "orm", "clean", "dirty", "fill", "background", "bg",
            "green", "red", "navy", "yellow", "blue", "white", "black",
        };
        // type folders inside a Parent folder: the Parent (One Parent = One Folder) is the nearest folder that is not one of these
        private static readonly string[] GenericFolders =
        {
            "images", "textures", "sprites", "materials", "particle_materials", "particles", "prefabs", "audio", "sounds",
            "sfxs", "sfx", "bgm", "vfxs", "models", "meshes", "animations", "runtime", "scripts", "resources", "editor", "ui",
            "common", "commons", "shared", "packages", "assets", "fonts", "scenes",
        };
        // first words that are a part on their own: what follows is the property (icon_remove_ads -> icon + remove_ads)
        private static readonly string[] LeadParts = { "icon", "btn", "button", "bg", "bar", "toggle", "panel", "frame" };
        // words that repeat the type: "FillFXAnimation" is fill_fx + ani, not fill_fx_animation_ani
        private static readonly Dictionary<AssetKind, string[]> TypeWords = new Dictionary<AssetKind, string[]>
        {
            { AssetKind.Animation, new[] { "animation", "anim" } },
            { AssetKind.Controller, new[] { "controller", "animator" } },
            { AssetKind.Material, new[] { "material" } },
            { AssetKind.Texture, new[] { "texture", "sprite" } },
            { AssetKind.Model, new[] { "model" } },
            { AssetKind.Audio, new[] { "sound", "audio" } },
        };
        private static readonly Dictionary<string, string> Typos = new Dictionary<string, string>
        {
            { "vetical", "vertical" }, { "recieve", "receive" }, { "buton", "button" },
        };

        private readonly List<string> _assetPaths = new List<string>();
        private readonly Dictionary<string, Dictionary<AssetKind, int>> _stats = new Dictionary<string, Dictionary<AssetKind, int>>();
        private readonly Dictionary<string, int> _folderIssues = new Dictionary<string, int>();   // assets failing ZPAS per folder
        private readonly List<Row> _rows = new List<Row>();
        private List<string> _folders = new List<string>();
        private string[] _codeTexts = new string[0];
        private string[] _codeNames = new string[0];

        private int _folderIndex;          // 0 = all folders
        private int _kindIndex;            // 0 = all kinds
        private bool _showStats = true;
        private bool _allowIssues;
        private string _status = "";
        private Vector2 _statsScroll;
        private Vector2 _rowsScroll;

        [MenuItem("Base/Rename Assets")]
        public static void Open()
        {
            var window = GetWindow<RenameAssetWindow>("Rename Assets");
            window.minSize = new Vector2(760, 520);
            window.Scan();
        }

        private void OnEnable()
        {
            if (_assetPaths.Count == 0)
                Scan();
        }

        // ---- scan ----

        private void Scan()
        {
            _assetPaths.Clear();
            _stats.Clear();
            _folderIssues.Clear();

            var guids = AssetDatabase.FindAssets("", new[] { "Assets", "Packages" });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path) || IsExcluded(path))
                    continue;
                _assetPaths.Add(path);

                var folder = Folder(path);
                var kind = KindOf(path);
                if (!_stats.TryGetValue(folder, out var counts))
                    _stats[folder] = counts = new Dictionary<AssetKind, int>();
                counts[kind] = counts.TryGetValue(kind, out var n) ? n + 1 : 1;
                if (Check(kind, path, Path.GetFileNameWithoutExtension(path)) != null)
                    _folderIssues[folder] = _folderIssues.TryGetValue(folder, out var issues) ? issues + 1 : 1;
            }

            _folders = _stats.Keys.OrderBy(f => f).ToList();
            if (_folderIndex > _folders.Count)
                _folderIndex = 0;

            LoadCodeTexts();
            Rebuild();
            _status = $"{_assetPaths.Count} assets in {_folders.Count} folders.";
        }

        /// <summary>Only the game's own assets and Base's packages: third-party SDK folders (AppsFlyer, AdMob, LevelPlay...) are skipped.</summary>
        private static bool IsExcluded(string path)
        {
            if (!path.StartsWith("Assets/") && !path.StartsWith("Packages/com.zen."))
                return true;
            if (Path.GetFileName(path).StartsWith("google-services", System.StringComparison.OrdinalIgnoreCase))
                return true;   // Firebase config files (google-services.json, google-services-desktop.json...)
            if (ThirdPartyFiles.Contains(Path.GetFileNameWithoutExtension(path).ToLowerInvariant()))
                return true;   // SDK settings assets that live in Assets/Resources
            return path.Split('/').Any(segment => ThirdPartyFolders.Contains(segment.ToLowerInvariant()));
        }

        private static readonly HashSet<string> ThirdPartyFiles = new HashSet<string>
        {
            "dotweensettings", "googlemobileadssettings", "tmp settings", "tmpsettings", "googleplayservicessettings",
            "externaldependencymanagersettings", "mobileadssettings", "appsflyersettings", "mainconfig",
        };

        private static readonly HashSet<string> ThirdPartyFolders = new HashSet<string>
        {
            "plugins", "plugins~", "samples", "samples~", "documentation~", "streamingassets", "generatedlocalrepo", "externaldependencymanager",
            "textmesh pro", "gizmos", "editor default resources", "appsflyer", "googlemobileads", "admob", "google",
            "firebase", "ironsource", "levelplay", "applovin", "max", "maxsdk", "meta", "facebook", "audiencenetwork",
            "adjust", "unity", "mediation", "googleplaypluginsdk", "external-dependency-manager", "com.unity",
        };

        private static string Folder(string path)
        {
            var folder = Path.GetDirectoryName(path);
            return folder == null ? "" : folder.Replace('\\', '/');
        }

        private static AssetKind KindOf(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".png": case ".jpg": case ".jpeg": case ".psd": case ".tga": case ".tif": case ".tiff": case ".exr":
                    return AssetKind.Texture;
                case ".wav": case ".mp3": case ".ogg": case ".aif": case ".aiff":
                    return AssetKind.Audio;
                case ".fbx": case ".obj": case ".blend": case ".dae":
                    return AssetKind.Model;
                case ".mat": return AssetKind.Material;
                case ".anim": return AssetKind.Animation;
                case ".controller": return AssetKind.Controller;
                case ".prefab": return AssetKind.Prefab;
                case ".unity": return AssetKind.Scene;
                case ".spriteatlas": case ".spriteatlasv2": return AssetKind.Atlas;
                default: return AssetKind.Other;
            }
        }

        /// <summary>The .cs files, read once: a rename shows the old name where code still uses it as a string.</summary>
        private void LoadCodeTexts()
        {
            var scripts = AssetDatabase.FindAssets("t:Script", new[] { "Assets", "Packages" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".cs") && !IsExcluded(p)).ToArray();
            _codeNames = scripts;
            _codeTexts = scripts.Select(p => File.Exists(p) ? File.ReadAllText(p) : "").ToArray();
        }

        // ---- rows ----

        private void Rebuild()
        {
            _rows.Clear();
            var folder = _folderIndex == 0 ? null : _folders[_folderIndex - 1];
            foreach (var path in _assetPaths)
            {
                var kind = KindOf(path);
                if (folder != null && Folder(path) != folder) continue;
                if (_kindIndex != 0 && kind != (AssetKind)(_kindIndex - 1)) continue;
                var name = Path.GetFileNameWithoutExtension(path);
                // only the files that need a rename are listed
                if (Check(kind, path, name) == null)
                    continue;

                var row = new Row { Path = path, OldName = name, NewName = name, Kind = kind };
                // the suggestion is a starting point: edit it in the New name field before renaming
                row.NewName = Suggest(kind, path, name);
                row.Issue = Check(kind, path, row.NewName);
                row.Selected = row.Issue == null && row.NewName != name;
                row.CodeHits = CodeHits(name);
                _rows.Add(row);
            }
            _rows.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
        }

        private string CodeHits(string name)
        {
            if (name.Length < 3) return null;
            var pattern = @"\b" + Regex.Escape(name) + @"\b";
            int hits = 0; string first = null;
            for (int i = 0; i < _codeTexts.Length; i++)
            {
                if (_codeTexts[i].Length == 0 || !Regex.IsMatch(_codeTexts[i], pattern)) continue;
                hits++;
                first = first ?? _codeNames[i];
            }
            return hits == 0 ? null : $"{hits} .cs (e.g. {first})";
        }

        /// <summary>ZPAS check of one name at a path: null when it passes.</summary>
        private static string Check(AssetKind kind, string path, string name)
        {
            if (string.IsNullOrEmpty(name)) return "empty name";
            var words = name.Split('_');
            if (words.Any(w => ForbiddenWords.Contains(w.ToLowerInvariant())))
                return "forbidden word (final, new, temp, old, copy, fix...)";

            switch (kind)
            {
                case AssetKind.Prefab:
                case AssetKind.Scene:
                    return PascalName.IsMatch(name) ? null : "prefab / scene: PascalCase or Pascal_Snake_Case";
                case AssetKind.Other:
                    return null;
            }

            if (!SubAssetName.IsMatch(name)) return "not lowercase_snake (no spaces, capitals or __)";
            var infixes = InfixesOf(kind);
            if (!words.Any(w => infixes.Contains(w))) return "missing type infix: " + string.Join(" or ", infixes);

            // Parent in the name = the folder that holds it (One Parent = One Folder)
            var prefix = ExpectedPrefix(kind, path, words);
            if (prefix != null && name != prefix && !name.StartsWith(prefix + "_"))
                return $"must start with {prefix}_ (parent folder)";
            return null;
        }

        private static string[] InfixesOf(AssetKind kind)
        {
            switch (kind)
            {
                case AssetKind.Texture: return new[] { "spr", "tex" };
                case AssetKind.Audio: return new[] { "sfx", "bgm" };
                case AssetKind.Model: return new[] { "mesh" };
                case AssetKind.Material: return new[] { "mat" };
                case AssetKind.Animation: return new[] { "ani" };
                case AssetKind.Controller: return new[] { "anm" };
                case AssetKind.Atlas: return new[] { "atlas" };
                default: return new string[0];
            }
        }

        /// <summary>
        /// How a name in this folder must start: the parent folder (its name carries the category, like
        /// Vehicle_Lamborghini_Huracan); UI sprites and atlases put "ui" first, audio puts "sfx" / "bgm" first.
        /// </summary>
        private static string ExpectedPrefix(AssetKind kind, string path, string[] words)
        {
            var parent = ParentFolderWord(path);
            string Join(string first) => parent == null ? first : first + "_" + parent;
            switch (kind)
            {
                case AssetKind.Audio:
                    return Join(words.Contains("bgm") ? "bgm" : "sfx");
                case AssetKind.Atlas:
                    return Join("ui");
                case AssetKind.Texture:
                    return words.Contains("spr") ? Join("ui") : parent;
                default:
                    return parent;
            }
        }

        /// <summary>
        /// A ZPAS name suggested from the old name and its folder: parent_part_type_property, where the parent is the
        /// nearest folder that is not a type folder (Images, Materials...). UI sprites and atlases start with "ui",
        /// audio with "sfx" / "bgm". Words that repeat the parent at the start or repeat the type are dropped, so an asset
        /// does not repeat the parent (Ads/AdsBanner.png -> ui_ads_banner_spr).
        /// Prefabs and scenes keep their name (only the casing is checked).
        /// </summary>
        private static string Suggest(AssetKind kind, string path, string name)
        {
            if (kind == AssetKind.Prefab || kind == AssetKind.Scene || kind == AssetKind.Other)
                return name;

            var infix = InfixFor(kind, path, name);
            var parent = ParentFolderWord(path);
            var tokens = SplitWords(name)
                .Select(w => Typos.TryGetValue(w, out var fixedWord) ? fixedWord : w)
                .Select(w => WithoutTypeWord(kind, w))
                .Where(w => w.Length > 0 && !ForbiddenWords.Contains(w) && !InfixWords.Contains(w) && w != "ui")
                .ToList();

            // the parent is already in front: "ads" in "AdsBanner" (Ads folder) is not repeated
            if (parent != null)
            {
                var parentWords = parent.Split('_');
                if (tokens.Count >= parentWords.Length && tokens.Take(parentWords.Length).SequenceEqual(parentWords))
                    tokens = tokens.Skip(parentWords.Length).ToList();
            }

            if (kind == AssetKind.Atlas)
                return JoinPieces("ui", parent, "atlas", tokens.Count > 0 ? string.Join("_", tokens) : "common");

            // part: a lead word (icon, btn...) alone, else every word before the first property word
            int split = tokens.Count;
            if (tokens.Count > 1 && LeadParts.Contains(tokens[0]))
                split = 1;
            else if (tokens.Count > 1)
            {
                var found = tokens.FindIndex(1, w => PropertyWords.Contains(w));
                if (found > 0) split = found;
            }
            var part = string.Join("_", tokens.Take(split));
            var property = string.Join("_", tokens.Skip(split));

            switch (infix)
            {
                case "sfx":
                case "bgm":
                    return JoinPieces(infix, parent, part, property);
                case "spr":
                    return JoinPieces("ui", parent, part, "spr", property);
                default:
                    return parent == null && part.Length == 0 ? name : JoinPieces(parent, part, infix, property);
            }
        }

        private static string JoinPieces(params string[] pieces) => string.Join("_", pieces.Where(p => !string.IsNullOrEmpty(p)));

        /// <summary>A word that only repeats the type is dropped; a word ending with it loses the ending (fxanimation -> fx).</summary>
        private static string WithoutTypeWord(AssetKind kind, string word)
        {
            if (!TypeWords.TryGetValue(kind, out var typeWords))
                return word;
            foreach (var typeWord in typeWords)
            {
                if (word == typeWord) return "";
                if (word.Length > typeWord.Length && word.EndsWith(typeWord))
                    return word.Substring(0, word.Length - typeWord.Length);
            }
            return word;
        }

        /// <summary>Words of a name: camelCase, spaces, hyphens and underscores split it, all lower case.</summary>
        private static IEnumerable<string> SplitWords(string name)
        {
            // "SoftCurrency" -> soft_currency, "FXManager" -> fx_manager
            var spaced = Regex.Replace(name, "([a-z0-9])([A-Z])", "$1_$2");
            spaced = Regex.Replace(spaced, "([A-Z])([A-Z][a-z])", "$1_$2");
            return Regex.Split(spaced.ToLowerInvariant(), "[^a-z0-9]+").Where(w => w.Length > 0);
        }

        private static string ParentFolderWord(string path)
        {
            var folders = Folder(path).Split('/');
            for (int i = folders.Length - 1; i >= 0; i--)
            {
                // "SFXs" is a type folder: compare the raw name too, the acronym split makes it "sf_xs"
                var raw = folders[i].ToLowerInvariant();
                var word = string.Join("_", SplitWords(folders[i]));
                if (word.Length > 0 && !GenericFolders.Contains(raw) && !GenericFolders.Contains(word)
                    && word != "com_zen_core" && !word.StartsWith("com_"))
                    return word;
            }
            return null;
        }

        private static string InfixFor(AssetKind kind, string path, string name)
        {
            var lower = name.ToLowerInvariant();
            switch (kind)
            {
                case AssetKind.Texture:
                    // UI sprites carry "spr"; textures outside a UI folder keep "tex"
                    return path.Contains("/UI/") || path.Contains("/Ads/") || path.Contains("/IAP/") || lower.StartsWith("ui") || lower.Contains("icon") || lower.Contains("button") || lower.Contains("gradient") ? "spr" : "tex";
                case AssetKind.Audio: return lower.Contains("bgm") || lower.Contains("music") ? "bgm" : "sfx";
                case AssetKind.Model: return "mesh";
                case AssetKind.Material: return "mat";
                case AssetKind.Animation: return "ani";
                case AssetKind.Controller: return "anm";
                default: return "spr";
            }
        }

        // ---- GUI ----

        private void OnGUI()
        {
            DrawFilters();
            if (_showStats) DrawStats();
            DrawRows();
            DrawFooter();
        }

        private void DrawFilters()
        {
            EditorGUILayout.BeginHorizontal();
            var folderOptions = new[] { "All folders" }.Concat(_folders).ToArray();
            var newFolder = EditorGUILayout.Popup("Folder", _folderIndex, folderOptions);
            var newKind = EditorGUILayout.Popup("Type", _kindIndex, new[] { "All types" }.Concat(KindNames).ToArray());
            if (GUILayout.Button("Rescan", GUILayout.Width(80)))
            {
                Scan();
                EditorGUILayout.EndHorizontal();
                return;
            }
            EditorGUILayout.EndHorizontal();
            _showStats = EditorGUILayout.Foldout(_showStats, "Statistics by folder and type", true);

            if (newFolder != _folderIndex || newKind != _kindIndex)
            {
                _folderIndex = newFolder;
                _kindIndex = newKind;
                Rebuild();
            }
        }

        private void DrawStats()
        {
            _statsScroll = EditorGUILayout.BeginScrollView(_statsScroll, GUILayout.Height(150));
            // only the folders that still have assets to fix
            var toFix = _folders.Where(f => _folderIssues.ContainsKey(f)).ToList();
            if (toFix.Count == 0)
                EditorGUILayout.LabelField("All folders pass the ZPAS check.", EditorStyles.miniLabel);
            foreach (var folder in toFix)
            {
                var counts = _stats[folder];
                var summary = string.Join("  ", KindNames.Select((k, i) => counts.TryGetValue((AssetKind)i, out var n) ? $"{k} {n}" : null)
                    .Where(s => s != null));
                // yellow: this folder holds assets that fail the ZPAS check
                var issues = _folderIssues.TryGetValue(folder, out var failing) ? failing : 0;
                var previousColor = GUI.backgroundColor;
                if (issues > 0) GUI.backgroundColor = Color.yellow;

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    _folderIndex = _folders.IndexOf(folder) + 1;
                    Rebuild();
                }
                EditorGUILayout.LabelField(folder, GUILayout.Width(360));
                EditorGUILayout.LabelField(summary, EditorStyles.miniLabel);
                EditorGUILayout.LabelField(issues > 0 ? $"{issues} to fix" : "ok", EditorStyles.miniLabel, GUILayout.Width(80));
                EditorGUILayout.EndHorizontal();

                GUI.backgroundColor = previousColor;
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawRows()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("", GUILayout.Width(20));
            GUILayout.Label("Current name", EditorStyles.boldLabel, GUILayout.Width(220));
            GUILayout.Label("New name", EditorStyles.boldLabel, GUILayout.Width(220));
            GUILayout.Label("ZPAS check / code", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            _rowsScroll = EditorGUILayout.BeginScrollView(_rowsScroll);
            foreach (var row in _rows)
            {
                // yellow: this asset fails the ZPAS check (the suggested name is in the New name field)
                var previousColor = GUI.backgroundColor;
                if (row.Issue != null) GUI.backgroundColor = Color.yellow;

                EditorGUILayout.BeginHorizontal();
                row.Selected = EditorGUILayout.Toggle(row.Selected, GUILayout.Width(20));
                // click: select the asset in the Project window and ping it
                if (GUILayout.Button(row.OldName, EditorStyles.linkLabel, GUILayout.Width(220)))
                    PingAsset(row.Path);
                var edited = EditorGUILayout.TextField(row.NewName, GUILayout.Width(220));
                if (edited != row.NewName)
                {
                    row.NewName = edited;
                    row.Issue = Check(row.Kind, row.Path, edited);
                    row.Selected = true;
                }
                var message = row.Issue ?? (row.NewName == row.OldName ? "ok (unchanged)" : "ok");
                if (row.CodeHits != null && row.NewName != row.OldName) message += "  |  in code: " + row.CodeHits;
                var style = row.Issue != null ? EditorStyles.boldLabel : EditorStyles.label;
                GUILayout.Label(message, style);
                EditorGUILayout.EndHorizontal();
                GUI.backgroundColor = previousColor;
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawFooter()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            _allowIssues = EditorGUILayout.ToggleLeft("Allow names that fail the ZPAS check", _allowIssues, GUILayout.Width(300));
            var pending = _rows.Where(r => r.Selected && r.NewName != r.OldName && (_allowIssues || r.Issue == null)).ToList();
            GUI.enabled = pending.Count > 0;
            if (GUILayout.Button($"Rename {pending.Count} selected", GUILayout.Width(200)))
                Apply(pending);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(_status, MessageType.None);
        }

        private static void PingAsset(string path)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null)
                return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private void Apply(List<Row> pending)
        {
            var list = string.Join("\n", pending.Take(15).Select(r => $"{r.OldName}  ->  {r.NewName}"));
            if (pending.Count > 15) list += $"\n... and {pending.Count - 15} more";
            if (!EditorUtility.DisplayDialog("Rename asset",
                $"Rename {pending.Count} asset(s)? GUIDs stay, so prefabs and scenes keep working.\n\n{list}", "Rename", "Cancel"))
                return;

            var errors = new StringBuilder();
            int done = 0;
            foreach (var row in pending)
            {
                var error = AssetDatabase.RenameAsset(row.Path, row.NewName);
                if (string.IsNullOrEmpty(error)) done++;
                else errors.AppendLine($"{row.OldName}: {error}");
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Base Rename] renamed {done}/{pending.Count}" + (errors.Length > 0 ? "\n" + errors : ""));

            Scan();
            _status = $"Renamed {done} of {pending.Count}." + (errors.Length > 0 ? " Errors in the Console." : "")
                      + " Names still in code (CodeHits) must be changed by hand.";
        }
    }
}
