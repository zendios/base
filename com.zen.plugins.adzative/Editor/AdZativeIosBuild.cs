using System;
using UnityEditor;
#if UNITY_IOS
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
#endif

namespace Base.Ads
{
    /// <summary>
    /// Setup of the iOS plugin (Plugins/iOS/AdZative, Objective-C copied from zen-ios):
    ///  1. on every import, its sources build for iOS only (not tvOS / visionOS / Editor) and the .m files with ARC
    ///     (-fobjc-arc: Unity compiles plugin sources without ARC unless asked; the plugin stops with #error otherwise);
    ///  2. in the Xcode project, UnityFramework links Network.framework (network-back monitor of the pools).
    /// </summary>
    internal sealed class AdZativeIosBuild : AssetPostprocessor
    {
        private const string PluginFolder = "Packages/com.zen.plugins.adzative/Plugins/iOS";

        /// <summary>Imports the plugin folder again, so its settings apply to .meta files written before
        /// (batchmode: -executeMethod Base.Ads.AdZativeIosBuild.ReimportPlugins).</summary>
        public static void ReimportPlugins()
        {
            AssetDatabase.ImportAsset(PluginFolder, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        }

        private void OnPreprocessAsset()
        {
            if (!assetPath.StartsWith(PluginFolder + "/") || !(assetImporter is PluginImporter importer))
                return;
            importer.SetCompatibleWithAnyPlatform(false);
            importer.SetCompatibleWithEditor(false);
            importer.SetCompatibleWithPlatform(BuildTarget.iOS, true);
            importer.SetCompatibleWithPlatform(BuildTarget.tvOS, false);
            if (Enum.TryParse("VisionOS", out BuildTarget visionOS))
                importer.SetCompatibleWithPlatform(visionOS, false);
            if (assetPath.EndsWith(".m"))
                importer.SetPlatformData(BuildTarget.iOS, "CompileFlags", "-fobjc-arc");
        }

#if UNITY_IOS
        [PostProcessBuild(999)]
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS)
                return;
            string projectPath = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "Network.framework", false);
            project.WriteToFile(projectPath);
        }
#endif
    }
}
