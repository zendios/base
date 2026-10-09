using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        // Unity 2022.3 uses a custom Android file only when its Player Settings switch is on.
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

            if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel25)
                yield return new ZenSetupIssue
                {
                    message = $"Android Minimum API Level is {(int)PlayerSettings.Android.minSdkVersion}: the Zen Android libraries need 25.",
                    type = MessageType.Error,
                    fixLabel = "Set 25",
                    fix = () => PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25,
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
