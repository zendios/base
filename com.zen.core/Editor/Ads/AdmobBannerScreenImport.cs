using System;
using UnityEditor;

/// <summary>
/// Runtime/Ads/Plugins/iOS (AdmobBannerScreen.m) builds for iOS only, not tvOS / visionOS / Editor, on every import.
/// </summary>
internal sealed class AdmobBannerScreenImport : AssetPostprocessor
{
    private const string PluginFolder = "Packages/com.zen.core/Runtime/Ads/Plugins/iOS";

    /// <summary>Imports the folder again, so its settings apply to .meta files written before
    /// (batchmode: -executeMethod AdmobBannerScreenImport.ReimportPlugins).</summary>
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
    }
}
