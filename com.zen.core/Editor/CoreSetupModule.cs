using Base.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Base.Setup
{
    /// <summary>Core: DOTween (required), Android templates and Player Settings of the Zen games.</summary>
    internal class CoreSetupModule : IZenSetupModule
    {
        public string Name => "Core";
        public int Order => 0;

        private const string AndroidFolder = "Assets/Plugins/Android";
        private const string OldTemplatesFolder = AndroidFolder + "/OldTemplates~";   // "~": Unity does not import it
        private const string BaseTemplate = "baseProjectTemplate.gradle";
        private static readonly Version MinIos = new Version(17, 0);
        private const string TmpShadersFolder = "Assets/TextMesh Pro/Shaders";
        private const string OldTmpLayout = "UnpackUV(input.texcoord1";   // TMP 3.x: UV packed in texcoord1, scale in texcoord1.y

        private static readonly string[] GradleTemplates =
        {
            "baseProjectTemplate.gradle", "launcherTemplate.gradle", "mainTemplate.gradle", "settingsTemplate.gradle",
            "gradleTemplate.properties",
        };

        // Unity uses a custom Android file only when its Player Settings switch is on.
        private static readonly Dictionary<string, string> TemplateSwitches = new Dictionary<string, string>
        {
            { "AndroidManifest.xml", "useCustomMainManifest" },
            { "LauncherManifest.xml", "useCustomLauncherManifest" },
            { "mainTemplate.gradle", "useCustomMainGradleTemplate" },
            { "launcherTemplate.gradle", "useCustomLauncherGradleManifest" },
            { "baseProjectTemplate.gradle", "useCustomBaseGradleTemplate" },
            { "gradleTemplate.properties", "useCustomGradlePropertiesTemplate" },
            { "settingsTemplate.gradle", "useCustomGradleSettingsTemplate" },
            { "proguard-user.txt", "useCustomProguardFile" },
        };

        private static SerializedObject PlayerSettingsAsset() =>
            new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);

        private static string PackageFolder
        {
            get
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(CoreSetupModule).Assembly);
                return info != null ? info.resolvedPath : "Packages/com.zen.core";
            }
        }

        private static string TemplatesFolder => Path.Combine(PackageFolder, "Editor", "AndroidTemplates~");

        public IEnumerable<ZenSetupIssue> Check()
        {
            // Base needs DOTween's core (DOTween.dll), from DOTween (free) or DOTween Pro in Assets; no asmdef needed.
            // DOTween Pro is added by hand from the Asset Store (paid, never shipped with Base).
            if (!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "DOTween"))
                yield return new ZenSetupIssue
                {
                    title = "DOTween or DOTween Pro is required",
                    message = "Base needs DOTween in Assets/Plugins/Demigiant: import DOTween (free) from the Asset Store, or DOTween Pro by hand " +
                              "if the game uses its features (DOTweenAnimation, paths, TextMeshPro tweens). No ASMDEF needed.",
                    type = MessageType.Error,
                    fixLabel = "Asset Store",
                    fix = () => Application.OpenURL("https://assetstore.unity.com/packages/tools/animation/dotween-hotween-v2-27676"),
                };

            // the package's AGENTS.md at the project root: Claude / Cursor / Copilot read it there (never in Packages/)
            string agentsSource = Path.Combine(PackageFolder, "AGENTS.md");
            if (File.Exists(agentsSource) && !File.Exists("AGENTS.md") && !File.Exists("CLAUDE.md"))
                yield return new ZenSetupIssue
                {
                    message = "AGENTS.md (Base rules and API map for AI agents and new developers) is not in the project root yet.",
                    fixLabel = "Copy",
                    fix = () =>
                    {
                        File.Copy(agentsSource, "AGENTS.md");
                        Debug.Log("[Base Setup] copied AGENTS.md to the project root (edit it: it is the game's now).");
                    },
                };

            if (Directory.Exists(TemplatesFolder))
            {
                var missing = Directory.GetFiles(TemplatesFolder).Select(Path.GetFileName)
                    .Where(f => !f.EndsWith(".meta") && !File.Exists(Path.Combine(AndroidFolder, f))).ToList();
                if (missing.Count > 0)
                    yield return new ZenSetupIssue
                    {
                        message = $"Android templates missing in {AndroidFolder}: {string.Join(", ", missing)} (gradle / manifest / proguard of the Zen games; copied once, then they are the game's).",
                        fixLabel = "Copy",
                        fix = () =>
                        {
                            Directory.CreateDirectory(AndroidFolder);
                            foreach (var f in missing)
                                File.Copy(Path.Combine(TemplatesFolder, f), Path.Combine(AndroidFolder, f));
                            Debug.Log($"[Base Setup] copied {missing.Count} Android templates to {AndroidFolder}");
                            EnableTemplates(missing);
                        },
                    };
            }

            int gameAgp = AgpMajor(Path.Combine(AndroidFolder, BaseTemplate));
            int baseAgp = AgpMajor(Path.Combine(TemplatesFolder, BaseTemplate));
            if (gameAgp > 0 && gameAgp < baseAgp)
                yield return new ZenSetupIssue
                {
                    message = $"Android Gradle templates in {AndroidFolder} are for an older Unity (Android Gradle Plugin {gameAgp}, Base needs {baseAgp}): " +
                              $"the Gradle build fails. Replace copies the old ones to {OldTemplatesFolder} first (manifests and proguard stay).",
                    type = MessageType.Error,
                    fixLabel = "Replace",
                    fix = ReplaceGradleTemplates,
                };

            var oldTmp = OldTmpShaders();
            if (oldTmp.Count > 0)
                yield return new ZenSetupIssue
                {
                    message = $"TextMesh Pro shaders from before Unity 6 in {TmpShadersFolder}: {string.Join(", ", oldTmp.Select(Path.GetFileName))}. " +
                              "Text drawn with them is invisible (the TMP vertex layout changed). Update copies the shaders of TMP Essential Resources " +
                              "(TMP Settings and fonts stay).",
                    type = MessageType.Error,
                    fixLabel = "Update",
                    fix = UpdateTmpShaders,
                };

            if (IosVersion.TryParse(PlayerSettings.iOS.targetOSVersionString, out var ios) && ios < MinIos)
                yield return new ZenSetupIssue
                {
                    message = $"iOS Target minimum version is {PlayerSettings.iOS.targetOSVersionString}: Base needs {MinIos} (pods and frameworks follow it).",
                    type = MessageType.Error,
                    fixLabel = $"Set {MinIos}",
                    fix = () => PlayerSettings.iOS.targetOSVersionString = MinIos.ToString(),
                };

            var settings = PlayerSettingsAsset();
            var off = TemplateSwitches.Where(t => File.Exists(Path.Combine(AndroidFolder, t.Key)))
                .Where(t => settings.FindProperty(t.Value) is SerializedProperty p && !p.boolValue).Select(t => t.Key).ToList();
            if (off.Count > 0)
                yield return new ZenSetupIssue
                {
                    message = $"Custom Android files not used by the build (Player Settings > Publishing Settings): {string.Join(", ", off)}.",
                    type = MessageType.Error,
                    fixLabel = "Enable",
                    fix = () => EnableTemplates(off),
                };

            if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel26)
                yield return new ZenSetupIssue
                {
                    message = $"Android Minimum API Level is {(int)PlayerSettings.Android.minSdkVersion}: Unity 6.6 and the Zen Android libraries need 26.",
                    type = MessageType.Error,
                    fixLabel = "Set 26",
                    fix = () => PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26,
                };

            var android = UnityEditor.Build.NamedBuildTarget.Android;
            if (PlayerSettings.GetScriptingBackend(android) != ScriptingImplementation.IL2CPP
                || (PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0)
                yield return new ZenSetupIssue
                {
                    message = "Google Play needs 64-bit builds: Scripting Backend IL2CPP with ARM64.",
                    fixLabel = "IL2CPP ARM64",
                    fix = () =>
                    {
                        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
                        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                    },
                };
        }

        private static List<string> OldTmpShaders() =>
            Directory.Exists(TmpShadersFolder)
                ? Directory.GetFiles(TmpShadersFolder, "*.shader").Where(f => File.ReadAllText(f).Contains(OldTmpLayout)).ToList()
                : new List<string>();

        /// <summary>
        /// Copies the shader files of uGUI's TMP Essential Resources over the project's (same GUIDs, so materials keep
        /// them). Only Shaders/: TMP Settings and fonts are the game's.
        /// </summary>
        private static void UpdateTmpShaders()
        {
            var ugui = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui");
            string essentials = ugui != null ? Path.Combine(ugui.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage") : null;
            if (essentials == null || !File.Exists(essentials))
            {
                Debug.LogError("[Base Setup] TMP Essential Resources.unitypackage not found in com.unity.ugui.");
                return;
            }

            // A .unitypackage is a .tgz of <guid>/{pathname, asset, asset.meta}.
            string tmp = Path.Combine(Path.GetTempPath(), "base-tmp-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            try
            {
                string error = EditorProcess.Run("tar", $"-xzf \"{essentials}\" -C \"{tmp}\"", out _);
                if (error != null)
                    throw new Exception(error);

                int copied = 0;
                foreach (var dir in Directory.GetDirectories(tmp))
                {
                    string pathnameFile = Path.Combine(dir, "pathname"), asset = Path.Combine(dir, "asset");
                    if (!File.Exists(pathnameFile) || !File.Exists(asset))
                        continue;
                    string target = File.ReadAllLines(pathnameFile)[0].Trim();
                    if (!target.StartsWith(TmpShadersFolder + "/"))
                        continue;
                    if (File.Exists(target) && File.ReadAllBytes(target).SequenceEqual(File.ReadAllBytes(asset)))
                        continue;
                    File.Copy(asset, target, true);
                    if (!File.Exists(target + ".meta") && File.Exists(asset + ".meta"))
                        File.Copy(asset + ".meta", target + ".meta");
                    copied++;
                }
                AssetDatabase.Refresh();
                Debug.Log($"[Base Setup] {copied} TextMesh Pro shader file(s) updated from {essentials}.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[Base Setup] TMP shaders not updated: " + ex.Message);
            }
            finally
            {
                try { Directory.Delete(tmp, true); } catch { }
            }
        }

        /// <summary>Major version of the Android Gradle Plugin declared in a base project template, 0 when none.</summary>
        private static int AgpMajor(string path)
        {
            if (!File.Exists(path))
                return 0;
            var match = Regex.Match(File.ReadAllText(path), @"com\.android\.application'\s+version\s+'(\d+)\.");
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }

        private static void ReplaceGradleTemplates()
        {
            Directory.CreateDirectory(OldTemplatesFolder);
            foreach (var f in GradleTemplates)
            {
                string game = Path.Combine(AndroidFolder, f);
                if (File.Exists(game))
                    File.Copy(game, Path.Combine(OldTemplatesFolder, f), true);
                File.Copy(Path.Combine(TemplatesFolder, f), game, true);
            }
            AssetDatabase.Refresh();
            EnableTemplates(GradleTemplates);
            Debug.Log($"[Base Setup] Gradle templates replaced by Base's; the old ones are in {OldTemplatesFolder}. " +
                      "External Dependency Manager writes its dependencies back at the next resolve or build.");
        }

        private static void EnableTemplates(IEnumerable<string> files)
        {
            var settings = PlayerSettingsAsset();
            foreach (var f in files)
                if (TemplateSwitches.TryGetValue(f, out var name) && settings.FindProperty(name) is SerializedProperty p)
                    p.boolValue = true;
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }
    }
}
